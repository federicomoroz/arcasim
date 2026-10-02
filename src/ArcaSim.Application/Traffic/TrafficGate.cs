using System.Collections.Concurrent;

namespace ArcaSim.Application.Traffic;

/// <summary>
/// How much one service takes before it saturates. Zero switches a limit off.
/// <list type="bullet">
/// <item>RequestsPerMinute: a rate limit over the last 60 seconds.</item>
/// <item>Capacity: requests served at once; each one holds its slot ServiceTime.</item>
/// <item>QueueLimit: requests that may wait for a slot. The rest are turned away.</item>
/// </list>
/// Capacity and service time make a bottleneck: past Capacity / ServiceTime
/// requests per second, the queue grows, latency with it, and then requests
/// are refused, the way an overloaded ARCA behaves at the end of the month.
/// </summary>
public sealed record TrafficLimits(int RequestsPerMinute = 0, int Capacity = 0, int ServiceTimeMilliseconds = 0, int QueueLimit = 0)
{
    public static readonly TrafficLimits None = new();
}

/// <summary>What the gate saw for one service over the last minute, and what it holds now.</summary>
public sealed record TrafficSnapshot(
    string Service,
    TrafficLimits Limits,
    int InFlight,
    int Queued,
    int Requests,
    int Admitted,
    int Refused,
    double AverageMilliseconds,
    double P95Milliseconds)
{
    /// <summary>Refused over total in the last minute: the needle of the meter.</summary>
    public double SaturationPercent => Requests == 0 ? 0 : 100.0 * Refused / Requests;

    /// <summary>How full the slots and the queue are right now.</summary>
    public double LoadPercent =>
        Limits.Capacity == 0 ? 0 : Math.Min(100.0, 100.0 * (InFlight + Queued) / (Limits.Capacity + Limits.QueueLimit));
}

/// <summary>
/// Admission control in front of ARCA's endpoints: rate limit, slots with a
/// queue, and the numbers the panel's meter shows. Uses real time, not
/// ArcaSim's clock: saturation is about how fast requests arrive now, even
/// with the simulated date frozen.
/// </summary>
public sealed class TrafficGate(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, ServiceTraffic> _services = new(StringComparer.OrdinalIgnoreCase);

    public void SetLimits(string service, TrafficLimits limits) => For(service).SetLimits(limits);

    public void Reset() => _services.Clear();

    /// <summary>ARCA's services ArcaSim answers; they show on the meter before their first request.</summary>
    public static readonly string[] KnownServices = ["wsaa", "wsfe"];

    public IReadOnlyList<TrafficSnapshot> Snapshot()
    {
        foreach (var service in KnownServices) For(service);
        return _services.Select(s => s.Value.Snapshot(s.Key, time.GetUtcNow() - Window)).OrderBy(s => s.Service).ToList();
    }

    /// <summary>A pass to go through, or null when the service is saturated and the request has to be refused.</summary>
    public async Task<Admission?> EnterAsync(string service, CancellationToken ct)
    {
        var traffic = For(service);
        var arrived = time.GetUtcNow();
        var slot = await traffic.TryEnterAsync(arrived - Window, ct);
        if (slot is null)
        {
            traffic.Record(arrived, admitted: false, TimeSpan.Zero);
            return null;
        }
        return new Admission(traffic, slot, arrived, time);
    }

    private ServiceTraffic For(string service) => _services.GetOrAdd(service, _ => new ServiceTraffic(time));

    /// <summary>Held while ArcaSim answers; disposing it frees the slot and records how long it took.</summary>
    public sealed class Admission : IAsyncDisposable
    {
        private readonly ServiceTraffic _traffic;
        private readonly Slot _slot;
        private readonly DateTimeOffset _arrived;
        private readonly TimeProvider _time;

        internal Admission(ServiceTraffic traffic, Slot slot, DateTimeOffset arrived, TimeProvider time)
        {
            _traffic = traffic;
            _slot = slot;
            _arrived = arrived;
            _time = time;
        }

        /// <summary>The time the configured service takes: the request keeps its slot meanwhile.</summary>
        public Task ServeAsync(CancellationToken ct) =>
            _slot.ServiceTime > TimeSpan.Zero ? Task.Delay(_slot.ServiceTime, ct) : Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _slot.Release();
            _traffic.Record(_arrived, admitted: true, _time.GetUtcNow() - _arrived);
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class Slot(SemaphoreSlim? semaphore, TimeSpan serviceTime, Action released)
    {
        public TimeSpan ServiceTime { get; } = serviceTime;

        public void Release()
        {
            semaphore?.Release();
            released();
        }
    }

    internal sealed class ServiceTraffic(TimeProvider time)
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _admittedTimes = new();
        private readonly Queue<(DateTimeOffset At, bool Admitted, double Milliseconds)> _log = new();
        private TrafficLimits _limits = TrafficLimits.None;
        private SemaphoreSlim? _slots;
        private int _inFlight;
        private int _queued;

        public void SetLimits(TrafficLimits limits)
        {
            lock (_gate)
            {
                _limits = limits;
                // Requests already holding a slot release the old semaphore; new ones use the new size.
                _slots = limits.Capacity > 0 ? new SemaphoreSlim(limits.Capacity, limits.Capacity) : null;
            }
        }

        public async Task<Slot?> TryEnterAsync(DateTimeOffset windowStart, CancellationToken ct)
        {
            SemaphoreSlim? slots;
            TrafficLimits limits;
            var queued = false;
            lock (_gate)
            {
                limits = _limits;
                while (_admittedTimes.Count > 0 && _admittedTimes.Peek() < windowStart) _admittedTimes.Dequeue();
                if (limits.RequestsPerMinute > 0 && _admittedTimes.Count >= limits.RequestsPerMinute) return null;

                slots = _slots;
                if (slots is not null && slots.CurrentCount == 0)
                {
                    if (_queued >= limits.QueueLimit) return null;
                    _queued++;
                    queued = true;
                }
                _admittedTimes.Enqueue(time.GetUtcNow());
            }

            if (slots is not null)
            {
                try
                {
                    await slots.WaitAsync(ct);
                }
                finally
                {
                    if (queued) Interlocked.Decrement(ref _queued);
                }
            }

            Interlocked.Increment(ref _inFlight);
            return new Slot(slots, TimeSpan.FromMilliseconds(limits.ServiceTimeMilliseconds), () => Interlocked.Decrement(ref _inFlight));
        }

        public void Record(DateTimeOffset at, bool admitted, TimeSpan took)
        {
            lock (_gate) _log.Enqueue((at, admitted, took.TotalMilliseconds));
        }

        public TrafficSnapshot Snapshot(string service, DateTimeOffset windowStart)
        {
            lock (_gate)
            {
                while (_log.Count > 0 && _log.Peek().At < windowStart) _log.Dequeue();
                var served = _log.Where(e => e.Admitted).Select(e => e.Milliseconds).Order().ToList();
                return new TrafficSnapshot(
                    service,
                    _limits,
                    Math.Max(0, _inFlight),
                    Math.Max(0, _queued),
                    _log.Count,
                    served.Count,
                    _log.Count - served.Count,
                    served.Count == 0 ? 0 : Math.Round(served.Average(), 1),
                    served.Count == 0 ? 0 : Math.Round(served[(int)Math.Ceiling(served.Count * 0.95) - 1], 1));
            }
        }
    }
}
