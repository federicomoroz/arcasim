namespace ArcaSim.Tests.Support;

internal static class TestTime
{
    /// <summary>
    /// The moment the suite freezes ArcaSim's clock on unless a test needs another: Thursday 01/10/2026 at
    /// noon in Argentina, a weekday before 01/12/2026, when the receiver's VAT condition becomes mandatory.
    /// </summary>
    public static readonly DateTimeOffset Reference = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));
}
