namespace ArcaSim.Tests.Client;

/// <summary>A clock a test moves by hand, for the parts of Arca.Client that look at the time (a ticket's renewal margin).</summary>
internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate) _now += by;
    }
}
