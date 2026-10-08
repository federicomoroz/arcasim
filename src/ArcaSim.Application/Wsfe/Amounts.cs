using System.Globalization;

namespace ArcaSim.Application.Wsfe;

/// <summary>The arithmetic every invoicing manual shares: the margin of error, the precision of an amount and the exchange-rate band.</summary>
public static class Amounts
{
    /// <summary>
    /// The manuals' "Margen de error" (wsfev1.md §4.3, wsct.md, wsmtxca.md, the
    /// same words in each): the absolute error |computed − informed| up to 0.01
    /// per element added, or the relative error, absolute / |informed|, up to 0.01 %.
    /// </summary>
    public static bool WithinMargin(decimal computed, decimal informed, int lines)
    {
        var absolute = Math.Abs(computed - informed);
        if (absolute <= 0.01m * Math.Max(lines, 1)) return true;
        return informed != 0 && absolute / Math.Abs(informed) <= 0.0001m;
    }

    /// <summary>At most the given integer digits and decimals, as in "Double (13+2)".</summary>
    public static bool HasPrecision(double value, int integers, int decimals)
    {
        var amount = Math.Abs((decimal)value);
        return Math.Round(amount, decimals) == amount && Math.Truncate(amount).ToString(CultureInfo.InvariantCulture).Length <= integers;
    }

    /// <summary>
    /// A rate inside the band the manuals allow around ARCA's orientative one:
    /// from 2 % of it to <paramref name="ceiling"/> times it. "No superior al
    /// 400 %" is 4 in WSFEv1, MTXCA, WSFEXv1 and FCE.
    /// </summary>
    public static bool WithinRateBand(decimal rate, decimal reference, decimal ceiling = 4m) =>
        rate >= reference * 0.02m && rate <= reference * ceiling;
}

public static class ExchangeRates
{
    /// <summary>A currency's rate for a day: pesos are 1 on that very day; any other currency, its latest rate on or before it.</summary>
    public static async Task<(decimal Rate, DateOnly Day)?> QuoteAsync(this IExchangeRates rates, string currency, DateOnly day, CancellationToken ct = default) =>
        currency == "PES" ? (1m, day) : await rates.RateAsync(currency, day, ct);
}
