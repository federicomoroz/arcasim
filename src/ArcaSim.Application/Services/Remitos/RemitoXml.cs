using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// Reading and writing the remitos' XML. Their schemas leave every child
/// unqualified, so the rules work on plain local names; dates go out with the
/// zone, the way the family's examples show them (2020-05-29-03:00).
/// </summary>
public static class RemitoXml
{
    /// <summary>A copy without namespaces, so a request reads the same however the client prefixed it.</summary>
    public static XElement Plain(XElement element) =>
        new(element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => new XAttribute(a.Name.LocalName, a.Value)),
            element.Nodes().Select(n => n is XElement child ? Plain(child) : n is XText text ? new XText(text.Value) : (XNode?)null));

    public static XElement Parse(string xml) => XElement.Parse(xml);

    public static string Date(DateOnly date) => ArgentinaTime.DateWithOffset(date);

    public static string? Child(this XElement? element, string name) => element?.Element(name)?.Value.Trim();

    public static long? ChildLong(this XElement? element, string name) =>
        long.TryParse(element.Child(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static decimal? ChildDecimal(this XElement? element, string name) =>
        decimal.TryParse(element.Child(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static DateOnly? ChildDate(this XElement? element, string name) => element?.Element(name)?.Date(name);

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// The lines of a reception by their orden. The manuals document no error
    /// for an orden sent twice (harina 2.5.7.5 lists 120, 160, 1000, 3023-3027),
    /// so the line informed last counts, as it would in a map keyed by orden.
    /// </summary>
    public static Dictionary<long, T> ByOrder<T>(IEnumerable<XElement> lines, Func<XElement, T> value)
    {
        var byOrder = new Dictionary<long, T>();
        foreach (var line in lines) byOrder[line.ChildLong("orden") ?? 0] = value(line);
        return byOrder;
    }

    /// <summary>
    /// Sets a child in its schema place: replaced when it is there, otherwise
    /// inserted after the last sibling that comes before it in the order.
    /// </summary>
    public static void Put(XElement parent, XElement child, IReadOnlyList<string> order)
    {
        if (parent.Element(child.Name) is { } existing)
        {
            existing.ReplaceWith(child);
            return;
        }
        var rank = IndexOf(order, child.Name.LocalName);
        var before = parent.Elements().LastOrDefault(e => IndexOf(order, e.Name.LocalName) < rank);
        if (before is null) parent.AddFirst(child);
        else before.AddAfterSelf(child);
    }

    public static void Put(XElement parent, string name, object value, IReadOnlyList<string> order) =>
        Put(parent, new XElement(name, ContractXml.Format(value)), order);

    /// <summary>
    /// A new element with the children of the order list, in that order: the
    /// replacement when one is given (null drops it), else the source's own.
    /// Children the order does not name are left out.
    /// </summary>
    public static XElement Shape(string name, XElement source, IReadOnlyList<string> order, IReadOnlyDictionary<string, object?>? replace = null)
    {
        var shaped = new XElement(name);
        foreach (var child in order)
        {
            if (replace is not null && replace.TryGetValue(child, out var value))
            {
                if (value is XElement element) shaped.Add(element);
                else if (value is IEnumerable<XElement> elements) shaped.Add(elements);
                else if (value is not null) shaped.Add(new XElement(child, ContractXml.Format(value)));
                continue;
            }
            shaped.Add(source.Elements(child).Select(e => new XElement(e)));
        }
        return shaped;
    }

    /// <summary>A codigo/descripcion list, or null when it has no items (the family's arrays need at least one).</summary>
    public static XElement? Codes(string name, string item, IEnumerable<(object Code, string Text)> rows)
    {
        var items = rows.Select(r => new XElement(item, new XElement("codigo", ContractXml.Format(r.Code)), new XElement("descripcion", r.Text))).ToList();
        return items.Count == 0 ? null : new XElement(name, items);
    }

    public static XElement? Errors(IReadOnlyCollection<RemitoProblem> problems) =>
        Codes("arrayErrores", "codigoDescripcion", problems.Select(p => ((object)p.Code, p.Text)));

    private static int IndexOf(IReadOnlyList<string> order, string name)
    {
        for (var i = 0; i < order.Count; i++)
            if (order[i] == name) return i;
        return int.MaxValue;
    }
}
