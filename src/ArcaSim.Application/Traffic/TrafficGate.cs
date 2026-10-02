using System.Collections.Concurrent;
using ArcaSim.Application.Events;

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

/// <summary>What the gate holds for a service right now.</summary>
public sealed record TrafficState(string Service, TrafficLimits Limits, int InFlight, int Queued);

/// <summary>
/// Admission control in front of ARCA's endpoints: a rate limit and slots with
/// a bounded queue. It only admits or refuses, and says so on the event bus;
/// the meter and the activity log listen. Uses real time, not ArcaSim's clock:
/// saturation is about how fast requests arrive now, even with the date frozen.
/// </summary>
public sealed class TrafficGate(TimeProvider time, EventManager events)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>ARCA's services ArcaSim answers; they show on the meter before their first request.</summary>
    public static readonly string[] KnownServices = ["wsaa", "wsfe"];

    private readonly ConcurrentDictionary<string, ServiceTraffic> _services = new(StringComparer.OrdinalIgnoreCase);

    public void SetLimits(string service, TrafficLimits limits) => For(service).SetLimits(limits);

    public void Reset() => _services.Clear();

    public IReadOnlyList<TrafficState> States()
    {
        foreach (var service in KnownServices) For(service);
        return _services.Select(s => s.Value.State(s.Key)).OrderBy(s => s.Service).ToList();
    }

    /// <summary>A pass to go through, or null when the service is saturated and the request has to be refused.</summary>
    public async Task<Admission?> EnterAsync(string service, CancellationToken ct)
    {
        var arrived = time.GetUtcNow();
        var slot = await For(service).TryEnterAsync(arrived - Window, arrived, ct);
        if (slot is null)
        {
            events.Publish(new RequestRefused(arrived, service));
            return null;
        }
        return new Admission(service, slot, arrived, time, events);
    }

    private ServiceTraffic For(string service) => _services.GetOrAdd(service, _ => new ServiceTraffic());

    /// <summary>Held while ArcaSim answers; disposing it frees the slot and reports how long it took.</summary>
    public sealed class Admission : IAsyncDisposable
    {
        private readonly string _service;
        private readonly Slot _slot;
        private readonly DateTimeOffset _arrived;
        private readonly TimeProvider _time;
        private readonly EventManager _events;

        internal Admission(string service, Slot slot, DateTimeOffset arrived, TimeProvider time, EventManager events)
        {
            _service = service;
            _slot = slot;
            _arrived = arrived;
            _time = time;
            _events = events;
        }

        /// <summary>The time the configured service takes: the request keeps its slot meanwhile.</summary>
        public Task ServeAsync(CancellationToken ct) =>
            _slot.ServiceTime > TimeSpan.Zero ? Task.Delay(_slot.ServiceTime, ct) : Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _slot.Release();
            var now = _time.GetUtcNow();
            _events.Publish(new RequestServed(now, _service, now - _arrived));
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

    internal sealed class ServiceTraffic
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _admittedTimes = new();
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

        public TrafficState State(string service)
        {
            lock (_gate) return new TrafficState(service, _limits, Math.Max(0, _inFlight), Math.Max(0, _queued));
        }

        public async Task<Slot?> TryEnterAsync(DateTimeOffset windowStart, DateTimeOffset now, CancellationToken ct)
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
                _admittedTimes.Enqueue(now);
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
    }
}
