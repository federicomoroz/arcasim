using System.Globalization;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>How the organisms' services write a day: ISO where they send it as data, day/month/year where they print it for a person.</summary>
internal static class DayText
{
    /// <summary>2026-10-07.</summary>
    public static string Iso(this DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>07/10/2026.</summary>
    public static string DayMonthYear(this DateOnly day) => day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
