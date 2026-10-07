using ArcaSim.Application.Events;

namespace ArcaSim.Application.Traffic;

/// <summary>A service's meter: the gate's state now, and what the last minute looked like.</summary>
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
/// Listens to the gate's events and keeps one minute of them per service, for
/// the saturation meter: the same split as rate-guardian, where the gateway
/// blocks or forwards and a listener records.
/// </summary>
public sealed class TrafficMeter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly TimeProvider _time;
    private readonly TrafficGate _gate;
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<(DateTimeOffset At, bool Served, double Milliseconds)>> _log = new(StringComparer.OrdinalIgnoreCase);

    public TrafficMeter(TimeProvider time, TrafficGate gate, EventManager events)
    {
        _time = time;
        _gate = gate;
        events.Subscribe<RequestServed>(e => Add(e.Service, e.At, true, e.Took.TotalMilliseconds));
        events.Subscribe<RequestRefused>(e => Add(e.Service, e.At, false, 0));
    }

    public void Reset()
    {
        lock (_lock) _log.Clear();
    }

    public IReadOnlyList<TrafficSnapshot> Snapshot()
    {
        var since = _time.GetUtcNow() - Window;
        lock (_lock)
        {
            return _gate.States().Select(state =>
            {
                var log = _log.TryGetValue(state.Service, out var entries) ? Trim(entries, since) : [];
                var served = log.Where(e => e.Served).Select(e => e.Milliseconds).Order().ToList();
                return new TrafficSnapshot(
                    state.Service, state.Limits, state.InFlight, state.Queued,
                    log.Count, served.Count, log.Count - served.Count,
                    served.Count == 0 ? 0 : Math.Round(served.Average(), 1),
                    served.Count == 0 ? 0 : Math.Round(served[(int)Math.Ceiling(served.Count * 0.95) - 1], 1));
            }).ToList();
        }
    }

    private void Add(string service, DateTimeOffset at, bool served, double milliseconds)
    {
        lock (_lock)
        {
            if (!_log.TryGetValue(service, out var entries)) _log[service] = entries = new Queue<(DateTimeOffset, bool, double)>();
            entries.Enqueue((at, served, milliseconds));
            // Trimmed as it grows, not only when the panel asks: a simulator nobody watches keeps a minute, not every request.
            Drop(entries, at - Window);
        }
    }

    private static List<(DateTimeOffset At, bool Served, double Milliseconds)> Trim(
        Queue<(DateTimeOffset At, bool Served, double Milliseconds)> entries, DateTimeOffset since)
    {
        Drop(entries, since);
        return entries.ToList();
    }

    private static void Drop(Queue<(DateTimeOffset At, bool Served, double Milliseconds)> entries, DateTimeOffset before)
    {
        while (entries.Count > 0 && entries.Peek().At < before) entries.Dequeue();
    }
}
