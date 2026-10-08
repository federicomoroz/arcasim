using System.Globalization;
using System.Xml.Linq;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// Reading a request and filling an answer by element name, the way a
/// service's rules work on the contract: the sample already has every element
/// in schema order, so the rules only set values, repeat list items and drop
/// what does not apply.
/// Two families read: Find, Text, Long, Int, Decimal and Date look anywhere
/// below the element, ignoring case; Child, Children, ChildText, ChildLong,
/// ChildDecimal and ChildDate look only at its direct children, by exact local
/// name in any namespace, the way the Java services bind their unqualified fields.
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

    /// <summary>The number, or 0 when it does not read or does not fit an int: 4294967297 is not 1.</summary>
    public static int Int(this XElement element, string name) =>
        element.Long(name) is var value and >= int.MinValue and <= int.MaxValue ? (int)value : 0;

    public static decimal Decimal(this XElement element, string name) =>
        decimal.TryParse(element.Text(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>A date as ARCA's services send them: yyyyMMdd, yyyy-MM-dd or an xsd:dateTime.</summary>
    public static DateOnly? Date(this XElement element, string name) => ParseDate(element.Text(name));

    private static DateOnly? ParseDate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (DateOnly.TryParseExact(text, ["yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? DateOnly.FromDateTime(moment.DateTime) : null;
    }

    /// <summary>The text anywhere below, or null when it is missing or empty: an optional field of a request.</summary>
    public static string? OptionalText(this XElement element, string name) => element.Text(name) is { Length: > 0 } text ? text : null;

    /// <summary>The number anywhere below, or null when it is missing or does not read.</summary>
    public static long? OptionalLong(this XElement element, string name) =>
        long.TryParse(element.Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>A boolean the way the Java services bind one: true or 1, false or 0; null for anything else.</summary>
    public static bool? Flag(this XElement element, string name) => element.Text(name)?.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => null,
    };

    /// <summary>The first direct child with that local name, whatever its namespace; null without one.</summary>
    public static XElement? Child(this XElement? element, string name) =>
        element?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    public static IEnumerable<XElement> Children(this XElement? element, string name) =>
        element?.Elements().Where(e => e.Name.LocalName == name) ?? [];

    /// <summary>The direct child's text, trimmed; null without the child.</summary>
    public static string? ChildText(this XElement? element, string name) => element.Child(name)?.Value.Trim();

    public static long? ChildLong(this XElement? element, string name) =>
        long.TryParse(element.ChildText(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// The direct child's number as an int: null when it is missing or does not read, and
    /// int.MinValue, which no code, type or point of sale is, when it is a number an int
    /// cannot hold. A rule then refuses 4294967297 as the invalid value it is, instead of
    /// reading it as 1.
    /// </summary>
    public static int? ChildInt(this XElement? element, string name) =>
        element.ChildLong(name) is { } value ? (value is >= int.MinValue and <= int.MaxValue ? (int)value : int.MinValue) : null;

    /// <summary>The same for a field the schema types as xsd:short: short.MinValue when the number does not fit one.</summary>
    public static short? ChildShort(this XElement? element, string name) =>
        element.ChildLong(name) is { } value ? (value is >= short.MinValue and <= short.MaxValue ? (short)value : short.MinValue) : null;

    public static decimal? ChildDecimal(this XElement? element, string name) =>
        decimal.TryParse(element.ChildText(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>The direct child's date, in any of the forms Date reads.</summary>
    public static DateOnly? ChildDate(this XElement? element, string name) => ParseDate(element.ChildText(name));

    /// <summary>Sets the first element with that name, if the answer has one. Numbers and dates are written invariantly.</summary>
    public static XElement Set(this XElement element, string name, object? value)
    {
        if (element.Find(name) is { } target) target.Value = Format(value);
        return element;
    }

    /// <summary>Sets the element when there is a value and removes it when there is none: an optional field of an answer.</summary>
    public static XElement SetOrDrop(this XElement element, string name, object? value)
    {
        if (element.Find(name) is not { } target) return element;
        if (value is null || value is string { Length: 0 }) target.Remove();
        else target.Value = Format(value);
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

    /// <summary>
    /// A codigo + descripcion list, the shape in which FCE, MTXCA and WSCT answer
    /// their parameter tables, errors and observations: one item per row under the
    /// block's name, which goes out empty when there are no rows. The item is
    /// codigoDescripcion, or codigoDescripcionString where the code travels as text.
    /// Elements without a namespace, as those WSDLs leave their children unqualified.
    /// </summary>
    public static XElement CodeList<TCode>(string block, IEnumerable<(TCode Code, string Text)> rows, string item = "codigoDescripcion") =>
        new(block, rows.Select(row => new XElement(item, new XElement("codigo", row.Code), new XElement("descripcion", row.Text))));

    /// <summary>The same list, or null when there are no rows: an errors or observations block goes out only with something to say.</summary>
    public static XElement? Codes(string block, IEnumerable<(long Code, string Text)> codes, string item = "codigoDescripcion") =>
        CodeList(block, codes, item) is { HasElements: true } list ? list : null;

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
