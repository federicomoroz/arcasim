namespace ArcaSim.Application;

/// <summary>
/// The real time, moved by an offset or frozen at a moment. The admin API and
/// the tests use it to expire tickets, cross the 01/12/2026 change or open a
/// CAEA window without waiting.
/// </summary>
public sealed class SimulatedClock(TimeProvider time) : IClock
{
    private readonly object _gate = new();
    private TimeSpan _offset;
    private DateTimeOffset? _frozenAt;

    public DateTimeOffset Now
    {
        get
        {
            lock (_gate) return _frozenAt ?? time.GetUtcNow() + _offset;
        }
    }

    public bool Frozen
    {
        get
        {
            lock (_gate) return _frozenAt is not null;
        }
    }

    public void Freeze(DateTimeOffset at)
    {
        lock (_gate) _frozenAt = at;
    }

    /// <summary>Moves the clock forward (or back), frozen or not.</summary>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            if (_frozenAt is { } frozen) _frozenAt = frozen + by;
            else _offset += by;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _offset = TimeSpan.Zero;
            _frozenAt = null;
        }
    }
}
