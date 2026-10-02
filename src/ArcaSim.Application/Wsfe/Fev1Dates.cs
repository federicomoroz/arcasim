using System.Globalization;

namespace ArcaSim.Application.Wsfe;

/// <summary>WSFEv1's date formats: yyyymmdd for dates, yyyymmddhhmiss for processing times, both in Argentina's time.</summary>
public static class Fev1Dates
{
    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string Format(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string FormatProcessed(DateTimeOffset moment) =>
        moment.ToArgentina().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    public static DateOnly Today(this IClock clock) => DateOnly.FromDateTime(clock.Now.ToArgentina().DateTime);

    /// <summary>The weekday before the given day. ARCA also skips holidays; ArcaSim does not know them.</summary>
    public static DateOnly PreviousBusinessDay(DateOnly day)
    {
        var previous = day.AddDays(-1);
        while (previous.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) previous = previous.AddDays(-1);
        return previous;
    }

    /// <summary>An empty element (&lt;FchServDesde&gt;&lt;/FchServDesde&gt;) counts as not sent, as in the manual's own examples.</summary>
    public static string? Blank(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
