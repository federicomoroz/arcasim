using System.Globalization;
using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>
/// Turns a contract sample into an answer about a stored document: copies the
/// values of a stored request into the sample by element name, then removes
/// the optional elements nothing filled, so the answer says only what the
/// document has and still follows the schema's order and cardinality.
/// </summary>
internal sealed class AnswerFill
{
    private readonly HashSet<XElement> _filled = [];
    private readonly Dictionary<string, XmlSchemaElement?> _declarations = new(StringComparer.Ordinal);

    public AnswerFill(XElement sample, XmlSchemaSet schemas)
    {
        Root = sample;
        var copy = new XDocument(new XElement(sample));
        copy.Validate(schemas, (_, _) => { }, addSchemaInfo: true);
        foreach (var element in copy.Root!.DescendantsAndSelf())
            _declarations.TryAdd(PathOf(element), element.GetSchemaInfo()?.SchemaElement);
    }

    public XElement Root { get; }

    /// <summary>The first element with that name under the scope, marked as filled with the value.</summary>
    public XElement? Set(XElement scope, string name, object? value)
    {
        var target = scope.Find(name);
        if (target is null) return null;
        target.Value = ContractXml.Format(value);
        _filled.Add(target);
        return target;
    }

    /// <summary>Marks an element, and everything in it, as part of the answer.</summary>
    public void Keep(XElement element)
    {
        foreach (var e in element.DescendantsAndSelf()) _filled.Add(e);
    }

    /// <summary>
    /// Copies the source into the target by local name: children of the same
    /// name first, and for a value the target has but the source keeps deeper
    /// (origen/operador/planta for origen/planta), the first one below. A list
    /// in the source becomes as many copies as the schema allows.
    /// </summary>
    public void Merge(XElement? target, XElement? source)
    {
        if (target is null || source is null) return;
        if (!target.HasElements)
        {
            if (source.HasElements) return;
            target.Value = source.Value;
            _filled.Add(target);
            return;
        }
        foreach (var child in target.Elements().ToList())
        {
            var name = child.Name.LocalName;
            var matches = source.Elements().Where(e => e.Name.LocalName == name).ToList();
            if (matches.Count == 0 && !child.HasElements)
                matches = source.Descendants().Where(e => e.Name.LocalName == name && !e.HasElements).Take(1).ToList();
            if (matches.Count == 0) continue;
            var template = new XElement(child);
            Merge(child, matches[0]);
            var max = Declaration(child)?.MaxOccurs ?? 1;
            var anchor = child;
            foreach (var more in matches.Skip(1).Take((int)Math.Min(max - 1, 50)))
            {
                var copy = new XElement(template);
                anchor.AddAfterSelf(copy);
                anchor = copy;
                Merge(copy, more);
            }
        }
    }

    /// <summary>The answer without the optional elements nothing filled; required ones keep the sample's values.</summary>
    public XElement Done()
    {
        Prune(Root);
        return Root;
    }

    private bool Prune(XElement element)
    {
        if (!element.HasElements) return _filled.Contains(element);
        var holds = _filled.Contains(element);
        foreach (var child in element.Elements().ToList())
        {
            if (Prune(child)) holds = true;
            else if (Declaration(child) is { MinOccurs: 0 }) child.Remove();
        }
        return holds;
    }

    private XmlSchemaElement? Declaration(XElement element) => _declarations.GetValueOrDefault(PathOf(element));

    private static string PathOf(XElement element) =>
        string.Join('/', element.AncestorsAndSelf().Reverse().Select(e => e.Name.LocalName));
}

/// <summary>What wscpe and wslpg share: dates and amounts as ARCA writes them, the PDF they attach and a request's elements without namespaces.</summary>
internal static class GrainsFormat
{
    /// <summary>xsd:dateTime the way the manuals print it, in Argentina's time and without offset: 2016-11-17T11:32:23.</summary>
    public static string DateTime(DateTimeOffset moment) =>
        moment.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>xsd:date without zone (AAAA-MM-DD).</summary>
    public static string Date(DateTimeOffset moment) => moment.ToArgentina().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Round half even, as §4.2 of the wslpg manual asks; two decimals unless told otherwise.</summary>
    public static decimal Round(decimal value, int decimals = 2) => Math.Round(value, decimals, MidpointRounding.ToEven);

    public static string Amount(decimal value, int decimals = 2) =>
        Round(value, decimals).ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture);

    /// <summary>
    /// A copy of the element and what is in it without namespaces or attributes: the Java services bind
    /// unqualified fields, and what is stored of a request should read the way they bind it.
    /// </summary>
    public static XElement Unqualified(XElement e) =>
        new(e.Name.LocalName, e.Nodes().Select(n => n is XElement c ? Unqualified(c) : n));

    /// <summary>
    /// The document's PDF: ARCA sends the same file its web application
    /// prints; ArcaSim sends a one-page PDF naming the document, enough for a
    /// client that stores or shows it.
    /// </summary>
    public static string Pdf(string title) => Liquidaciones.SimplePdf.Line(title);
}
