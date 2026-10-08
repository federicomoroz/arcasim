using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>
/// Reading and writing the amounts and dates these services exchange. Their
/// plain children are read with ContractXml's Child family; what is here is the
/// amount, which .NET sends as a double (so 1E3 reads, 1,000 does not).
/// </summary>
internal static class Figures
{
    /// <summary>A double the way .NET serializes it: 1754.50 travels back as 1754.5, 10.00 as 10.</summary>
    public static string Number(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    public static string Day(DateOnly day) => day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string IsoDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A date written yyyymmdd, as the ASMX services take them; null when it is not one.</summary>
    public static DateOnly? ParseDay(string? text) =>
        DateOnly.TryParseExact(text?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    public static DateOnly? ParseIsoDay(string? text) =>
        DateOnly.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>The direct child's amount, read as a double is (exponent allowed, no thousands separator); null when it is missing or does not read.</summary>
    public static decimal? Amount(this XElement element, string name) =>
        decimal.TryParse(element.ChildText(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>The same element with every name stripped of its namespace, to keep a request's detail as data.</summary>
    public static XElement Strip(XElement element) =>
        new(element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.None),
            element.Nodes().Select(n => n is XElement child ? Strip(child) : n));

    /// <summary>
    /// The fields of a stored detail in the order a response's schema wants
    /// them, in its namespace. "Name!" is a number the schema requires (0 when
    /// it did not come), "Name#" an optional number; numbers go out the way
    /// .NET writes doubles.
    /// </summary>
    public static IEnumerable<XElement> Ordered(XNamespace ns, XElement source, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            var name = field.TrimEnd('!', '#');
            var required = field.EndsWith('!');
            var numeric = required || field.EndsWith('#');
            var value = source.ChildText(name);
            if (value is null && !required) continue;
            if (numeric) value = decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? Number(number) : "0";
            yield return new XElement(ns + name, value);
        }
    }
}
