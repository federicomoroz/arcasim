namespace ArcaSim.Application.Wsfe;

/// <summary>
/// The CAEA's periods and fortnights, the same in WSFEv1 (docs/arca/wsfev1.md §6.3)
/// and WSMTXCA: a period is yyyymm from 190001 to 999912, and "orden" 1 is the 1st
/// to the 15th of the month, 2 the 16th to its end. Each service answers a period or
/// an order that is not one with its own code.
/// </summary>
public static class CaeaFortnights
{
    public static bool IsPeriod(int period) => period is >= 190_001 and <= 999_912 && period % 100 is >= 1 and <= 12;

    public static bool IsOrder(short order) => order is 1 or 2;

    /// <summary>The days a fortnight covers, for a period and an order that are valid.</summary>
    public static (DateOnly From, DateOnly To) Days(int period, short order)
    {
        var first = new DateOnly(period / 100, period % 100, 1);
        return order == 1 ? (first, first.AddDays(14)) : (first.AddDays(15), first.AddMonths(1).AddDays(-1));
    }
}
