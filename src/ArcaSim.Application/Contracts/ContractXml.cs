using System.Globalization;
using System.Xml.Linq;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// Reading a request and filling an answer by element name, the way a
/// service's rules work on the contract: the sample already has every element
/// in schema order, so the rules only set values, repeat list items and drop
/// what does not apply.
/// </summary>
public static class ContractXml
{
    /// <summary>The first element with that local name, this one or below it.</summary>
    public static XElement? Find(this XElement element, string name) =>
        element.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<XElement> FindAll(this XElement element, string name) =>
        element.Descendants().Where(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static string? Text(this XElement element, string name) => element.Find(name)?.Value.Trim();

    public static long Long(this XElement element, string name) =>
        long.TryParse(element.Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static int Int(this XElement element, string name) => (int)element.Long(name);

    public static decimal Decimal(this XElement element, string name) =>
        decimal.TryParse(element.Text(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>A date as ARCA's services send them: yyyyMMdd, yyyy-MM-dd or an xsd:dateTime.</summary>
    public static DateOnly? Date(this XElement element, string name)
    {
        var text = element.Text(name);
        if (string.IsNullOrEmpty(text)) return null;
        if (DateOnly.TryParseExact(text, ["yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? DateOnly.FromDateTime(moment.DateTime) : null;
    }

    /// <summary>Sets the first element with that name, if the answer has one. Numbers and dates are written invariantly.</summary>
    public static XElement Set(this XElement element, string name, object? value)
    {
        if (element.Find(name) is { } target) target.Value = Format(value);
        return element;
    }

    /// <summary>Sets every element with that name.</summary>
    public static XElement SetAll(this XElement element, string name, object? value)
    {
        foreach (var target in element.FindAll(name)) target.Value = Format(value);
        return element;
    }

    /// <summary>
    /// Writes one copy of the sample's list item per item, in its place, each
    /// filled by the callback; with no items, the sample item goes away.
    /// </summary>
    public static XElement Repeat<T>(this XElement element, string itemName, IEnumerable<T> items, Action<XElement, T> fill)
    {
        var template = element.Find(itemName);
        if (template is null) return element;
        var anchor = template;
        foreach (var item in items)
        {
            var copy = new XElement(template);
            fill(copy, item);
            anchor.AddAfterSelf(copy);
            anchor = copy;
        }
        template.Remove();
        return element;
    }

    /// <summary>Removes every element with that name: an optional block that does not apply.</summary>
    public static XElement Drop(this XElement element, string name)
    {
        element.FindAll(name).ToList().ForEach(e => e.Remove());
        return element;
    }

    public static string Format(object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
