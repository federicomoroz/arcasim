using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// What the sampler fills values from: the CUIT that asked, and ArcaSim's
/// clock. Always names optional elements written in every answer, problem
/// blocks included (the events a service repeats on every response).
/// </summary>
public sealed record SampleContext(long Cuit, DateTimeOffset Now)
{
    public IReadOnlyCollection<string>? Always { get; init; }
}

/// <summary>
/// Where a service writes an error in its answer, when the names do not say
/// it on their own: the block (EstadoTransaccion, or "Parent/Block" when the
/// same name sits at several depths, as wsagr's Respuesta/Err), and the fields
/// for the code and the text inside it.
/// </summary>
public sealed record ErrorShape(string? Block = null, string? CodeField = null, string? TextField = null)
{
    public static readonly ErrorShape ByName = new();
}

/// <summary>A successful answer with its data, or the smallest answer that carries one error block and nothing else optional.</summary>
public enum SampleMode
{
    Data,
    Error,
}

/// <summary>
/// Writes an element the way its schema describes it: every required child,
/// the optional ones that carry data (never the error, event or observation
/// blocks, which would read as a failure), one item of each list, and values
/// that pass the type's facets. What comes out validates against the WSDL's
/// own schema, which is what a generated client deserializes.
/// </summary>
public sealed partial class SchemaSampler(XmlSchemaSet schemas)
{
    private const int MaxDepth = 14;

    /// <summary>Elements a successful answer leaves out: they report problems.</summary>
    [GeneratedRegex(@"(?i)(^err|errs?$|error|errores|^evt|events?$|eventos|^obs$|observ|fault|warning|alerta)")]
    public static partial Regex ProblemName();

    /// <summary>Error blocks: what an answer that failed fills (Errors, FEXErr, arrayErrores...), not events or observations.</summary>
    [GeneratedRegex(@"(?i)(^err|errs?$|error)")]
    private static partial Regex ErrorName();

    /// <summary>The code inside an error block.</summary>
    [GeneratedRegex(@"(?i)^(code|codigo|cod|errcode|coderr|codigoerror|coderror|errnum|nroerror|id)$")]
    private static partial Regex CodeName();

    /// <summary>The text inside an error block.</summary>
    [GeneratedRegex(@"(?i)^(msg|errmsg|mensaje|descripcion|desc|descripcionerror|deserror|descerror|message|text|infoadicional)$")]
    private static partial Regex MessageName();

    /// <summary>A field that holds a message although it is not named exactly so (DescError, ErrMsgDetalle).</summary>
    [GeneratedRegex(@"(?i)(msg|mensaje|descrip|desc)")]
    private static partial Regex LooseMessageName();

    private sealed class Walk(SampleContext context, SampleMode mode, ErrorShape shape)
    {
        public SampleContext Context { get; } = context;
        public SampleMode Mode { get; } = mode;
        public ErrorShape Shape { get; } = shape;

        /// <summary>The block the shape names (under the parent it names, if any); else its code field, loose among the result's fields; else any name that says error.</summary>
        public bool IsError(string name, string? parent) =>
            Shape.Block is { } block ? IsBlock(block, name, parent)
            : Shape.CodeField is { } code ? name.Equals(code, StringComparison.OrdinalIgnoreCase)
            : ErrorName().IsMatch(name);

        /// <summary>The same, for an element whose parent is the one being written.</summary>
        public bool IsError(string name) => IsError(name, Names.Count > 0 ? Names.Peek() : null);

        private static bool IsBlock(string block, string name, string? parent)
        {
            var slash = block.IndexOf('/');
            return slash < 0
                ? name.Equals(block, StringComparison.OrdinalIgnoreCase)
                : name.Equals(block[(slash + 1)..], StringComparison.OrdinalIgnoreCase)
                  && block[..slash].Equals(parent, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The names of the elements being written, innermost on top.</summary>
        public Stack<string> Names { get; } = new();

        public HashSet<XmlSchemaType> Path { get; } = [];
        public bool ErrorWritten { get; set; }

        /// <summary>Inside an element of SampleContext.Always: everything in it is written.</summary>
        public int AlwaysDepth { get; set; }

        public bool IsAlways(string name) => Context.Always?.Contains(name, StringComparer.OrdinalIgnoreCase) == true;
    }

    public XElement Sample(XName element, SampleContext context, SampleMode mode = SampleMode.Data) =>
        Element(Global(element), element, new Walk(context, mode, ErrorShape.ByName), 0, inError: false)!;

    /// <summary>
    /// The answer a service gives when it refuses the request in the body
    /// rather than with a fault: the response with one error block, its code
    /// and its text. Null when the response has no place for an error.
    /// </summary>
    public XElement? ErrorResponse(XName element, SampleContext context, long code, string message, ErrorShape? shape = null) =>
        ErrorResponse(element, context, [(code, message)], shape);

    /// <summary>
    /// The same, with several errors in the one answer (wsfecredagente sends
    /// 505 and 506 together): the first fills the error block, and each of the
    /// others goes in a copy of the row that holds the code, right after it.
    /// </summary>
    public XElement? ErrorResponse(XName element, SampleContext context, IReadOnlyList<(long Code, string Message)> errors, ErrorShape? shape = null)
    {
        shape ??= ErrorShape.ByName;
        var walk = new Walk(context, SampleMode.Error, shape);
        var response = Element(Global(element), element, walk, 0, inError: false)!;
        var block = response.Descendants().FirstOrDefault(e => walk.IsError(e.Name.LocalName, e.Parent?.Name.LocalName));
        if (block is null) return null;
        // The customs services put the code and the text loose in a Recibo (CodErr, DesError): the block is their parent.
        if (!block.HasElements && block.Parent is { } holder) block = holder;
        var leaves = block.DescendantsAndSelf().Where(e => !e.HasElements).ToList();
        bool IsCode(XElement e) => shape.CodeField is { } f ? e.Name.LocalName.Equals(f, StringComparison.OrdinalIgnoreCase) : CodeName().IsMatch(e.Name.LocalName);
        bool IsText(XElement e) => shape.TextField is { } f ? e.Name.LocalName.Equals(f, StringComparison.OrdinalIgnoreCase) : MessageName().IsMatch(e.Name.LocalName);
        var codeLeaf = leaves.FirstOrDefault(IsCode);
        var text = leaves.FirstOrDefault(IsText)
                   ?? leaves.FirstOrDefault(e => shape.TextField is null && LooseMessageName().IsMatch(e.Name.LocalName));
        if (errors.Count == 0) return response;
        if (codeLeaf is not null) codeLeaf.Value = errors[0].Code.ToString(CultureInfo.InvariantCulture);
        if (text is not null) text.Value = errors[0].Message;
        if (codeLeaf?.Parent is not { } row || row == response) return response;

        var last = row;
        foreach (var (code, message) in errors.Skip(1))
        {
            var copy = new XElement(row);
            Leaf(copy, row, codeLeaf)!.Value = code.ToString(CultureInfo.InvariantCulture);
            if (text is not null && Leaf(copy, row, text) is { } copyText) copyText.Value = message;
            last.AddAfterSelf(copy);
            last = copy;
        }
        return response;
    }

    /// <summary>The element of a copy that sits where the original's leaf sits.</summary>
    private static XElement? Leaf(XElement copy, XElement original, XElement leaf)
    {
        if (leaf.AncestorsAndSelf().All(e => e != original)) return null;
        var steps = leaf.AncestorsAndSelf().TakeWhile(e => e != original).Reverse()
            .Select(e => e.ElementsBeforeSelf().Count()).ToList();
        XElement? current = copy;
        foreach (var index in steps) current = current?.Elements().ElementAtOrDefault(index);
        return current;
    }

    /// <summary>Whether a request element asks for an access ticket: it, or something under it, has a token.</summary>
    public bool CarriesTicket(XName element) =>
        schemas.GlobalElements[new XmlQualifiedName(element.LocalName, element.NamespaceName)] is XmlSchemaElement global
        && HasToken(global.ElementSchemaType, 0, []);

    private XmlSchemaElement Global(XName element) =>
        schemas.GlobalElements[new XmlQualifiedName(element.LocalName, element.NamespaceName)] as XmlSchemaElement
        ?? throw new InvalidOperationException($"The schema has no element {element}.");

    private bool HasToken(XmlSchemaType? type, int depth, HashSet<XmlSchemaType> seen)
    {
        if (type is not XmlSchemaComplexType complex || depth > MaxDepth || !seen.Add(complex)) return false;
        return Elements(complex.ContentTypeParticle).Any(e =>
            NameOf(e).LocalName.Equals("token", StringComparison.OrdinalIgnoreCase) || HasToken(TypeOf(e), depth + 1, seen));
    }

    private bool LeadsToError(XmlSchemaType? type, string name, Walk walk, int depth, HashSet<XmlSchemaType> seen)
    {
        if (type is not XmlSchemaComplexType complex || depth > 6 || !seen.Add(complex)) return false;
        return Elements(complex.ContentTypeParticle).Any(e =>
            walk.IsError(NameOf(e).LocalName, name) || LeadsToError(TypeOf(e), NameOf(e).LocalName, walk, depth + 1, seen));
    }

    private static IEnumerable<XmlSchemaElement> Elements(XmlSchemaParticle? particle) => particle switch
    {
        XmlSchemaElement element => [element],
        XmlSchemaGroupBase group => group.Items.OfType<XmlSchemaParticle>().SelectMany(Elements),
        _ => [],
    };

    private XElement? Element(XmlSchemaElement declaration, XName name, Walk walk, int depth, bool inError)
    {
        var always = walk.IsAlways(name.LocalName);
        if (always) walk.AlwaysDepth++;
        try
        {
            return Build(declaration, name, walk, depth, inError);
        }
        finally
        {
            if (always) walk.AlwaysDepth--;
        }
    }

    private XElement? Build(XmlSchemaElement declaration, XName name, Walk walk, int depth, bool inError)
    {
        var element = new XElement(name);
        switch (TypeOf(declaration))
        {
            case XmlSchemaComplexType complex:
                var entered = walk.Path.Add(complex);
                if (!entered && depth > 2) return null;
                foreach (var use in complex.AttributeUses.Values.OfType<XmlSchemaAttribute>().Where(a => a.Use == XmlSchemaUse.Required))
                    element.SetAttributeValue(XName.Get(use.QualifiedName.Name, use.QualifiedName.Namespace),
                        Value(use.AttributeSchemaType, use.QualifiedName.Name, walk.Context));
                if (complex.ContentType is XmlSchemaContentType.TextOnly)
                    element.Value = Value(SimpleOf(complex), name.LocalName, walk.Context);
                else
                {
                    var error = walk.Mode == SampleMode.Error && walk.IsError(name.LocalName);
                    walk.Names.Push(name.LocalName);
                    Fill(element, complex.ContentTypeParticle, walk, depth + 1, inError || error);
                    walk.Names.Pop();
                }
                if (entered) walk.Path.Remove(complex);
                break;
            case XmlSchemaSimpleType simple:
                element.Value = Value(simple, name.LocalName, walk.Context);
                break;
        }
        return element;
    }

    private void Fill(XElement parent, XmlSchemaParticle? particle, Walk walk, int depth, bool inError)
    {
        if (particle is null || depth > MaxDepth) return;
        switch (particle)
        {
            case XmlSchemaElement child:
                var name = NameOf(child);
                var count = (int)Math.Min(child.MinOccurs, 3);
                if (count == 0 && Wanted(child, name, depth, walk, inError)) count = 1;
                for (var i = 0; i < count; i++)
                    if (Element(child, name, walk, depth, inError) is { } built) parent.Add(built);
                break;
            case XmlSchemaChoice choice:
                var items = choice.Items.OfType<XmlSchemaParticle>().ToList();
                var branch = walk.Mode == SampleMode.Error && !inError
                    ? items.FirstOrDefault(p => p is XmlSchemaElement e && walk.IsError(NameOf(e).LocalName))
                    : null;
                branch ??= items.FirstOrDefault(p => p is not XmlSchemaElement e || !ProblemName().IsMatch(NameOf(e).LocalName)) ?? items.FirstOrDefault();
                if (choice.MinOccurs > 0 || walk.Mode == SampleMode.Error || branch is not XmlSchemaElement { MinOccurs: 0 })
                    Fill(parent, branch, walk, depth, inError);
                break;
            case XmlSchemaGroupBase group:
                if (group.MinOccurs == 0 && depth > 6) return;
                var members = group.Items.OfType<XmlSchemaParticle>().ToList();
                // A code and a text loose among other fields (Recibo: CodErr, DesError) are written together, as one error block.
                if (walk.Mode == SampleMode.Error && !inError && !walk.ErrorWritten
                    && members.OfType<XmlSchemaElement>().Any(e => walk.IsError(NameOf(e).LocalName) && TypeOf(e) is XmlSchemaSimpleType))
                {
                    walk.ErrorWritten = true;
                    inError = true;
                }
                foreach (var item in members) Fill(parent, item, walk, depth, inError);
                break;
        }
    }

    /// <summary>
    /// Whether an optional element is written. With data: when it carries data,
    /// its type is not already being written above it, and the tree is still
    /// shallow. For an error: the first error block, everything inside it, and
    /// whatever leads to one.
    /// </summary>
    private bool Wanted(XmlSchemaElement child, XName name, int depth, Walk walk, bool inError)
    {
        if (depth > 6 || TypeOf(child) is { } type && walk.Path.Contains(type)) return false;
        if (walk.AlwaysDepth > 0 || walk.IsAlways(name.LocalName)) return true;
        if (walk.Mode == SampleMode.Data) return !ProblemName().IsMatch(name.LocalName);
        if (inError) return true;
        if (walk.IsError(name.LocalName))
        {
            if (walk.ErrorWritten) return false;
            walk.ErrorWritten = true;
            return true;
        }
        return !walk.ErrorWritten && LeadsToError(TypeOf(child), name.LocalName, walk, depth, []);
    }

    /// <summary>The element's name as it travels: its namespace only when the schema qualifies it.</summary>
    private static XName NameOf(XmlSchemaElement element)
    {
        var qualified = element.RefName.IsEmpty ? element.QualifiedName : element.RefName;
        return XName.Get(qualified.Name, qualified.Namespace);
    }

    private XmlSchemaType? TypeOf(XmlSchemaElement element)
    {
        if (element.ElementSchemaType is { } type) return type;
        return !element.RefName.IsEmpty && schemas.GlobalElements[element.RefName] is XmlSchemaElement global ? global.ElementSchemaType : null;
    }

    private static XmlSchemaSimpleType? SimpleOf(XmlSchemaComplexType complex)
    {
        for (XmlSchemaType? type = complex; type is not null; type = type.BaseXmlSchemaType)
            if (type is XmlSchemaSimpleType simple) return simple;
        return null;
    }

    // ---- Values ---------------------------------------------------------------------

    private static string Value(XmlSchemaSimpleType? type, string name, SampleContext context)
    {
        if (type is null) return "";
        var facets = Facets(type);
        var enumeration = facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value!).FirstOrDefault();
        if (enumeration is not null) return enumeration;

        return (type.Datatype?.TypeCode ?? XmlTypeCode.String) switch
        {
            XmlTypeCode.Boolean => "false",
            XmlTypeCode.Date => context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XmlTypeCode.DateTime => context.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            XmlTypeCode.Time => context.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            XmlTypeCode.GYearMonth => context.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            XmlTypeCode.GYear => context.Now.ToString("yyyy", CultureInfo.InvariantCulture),
            XmlTypeCode.Base64Binary or XmlTypeCode.HexBinary => "",
            XmlTypeCode.Decimal or XmlTypeCode.Double or XmlTypeCode.Float => Number(facets, name, context, 0),
            >= XmlTypeCode.Integer and <= XmlTypeCode.PositiveInteger => Number(facets, name, context, 1),
            _ => Text(facets, name, context),
        };
    }

    private static List<XmlSchemaFacet> Facets(XmlSchemaSimpleType type)
    {
        var facets = new List<XmlSchemaFacet>();
        for (XmlSchemaType? t = type; t is XmlSchemaSimpleType simple; t = t.BaseXmlSchemaType)
            if (simple.Content is XmlSchemaSimpleTypeRestriction restriction)
                facets.AddRange(restriction.Facets.OfType<XmlSchemaFacet>());
        return facets;
    }

    private static string Number(List<XmlSchemaFacet> facets, string name, SampleContext context, decimal preferred)
    {
        if (name.Contains("cuit", StringComparison.OrdinalIgnoreCase) && context.Cuit > 0) preferred = context.Cuit;
        var min = Bound<XmlSchemaMinInclusiveFacet>(facets) ?? (Bound<XmlSchemaMinExclusiveFacet>(facets) + 1);
        var max = Bound<XmlSchemaMaxInclusiveFacet>(facets) ?? (Bound<XmlSchemaMaxExclusiveFacet>(facets) - 1);
        var digits = facets.OfType<XmlSchemaTotalDigitsFacet>().Select(f => int.Parse(f.Value!, CultureInfo.InvariantCulture)).FirstOrDefault();
        if (digits > 0 && preferred >= (decimal)Math.Pow(10, digits)) preferred = 1;
        if (min is { } low && preferred < low) preferred = low;
        if (max is { } high && preferred > high) preferred = high;
        return preferred.ToString(CultureInfo.InvariantCulture);
    }

    private static decimal? Bound<T>(List<XmlSchemaFacet> facets) where T : XmlSchemaFacet =>
        facets.OfType<T>().Select(f => decimal.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null).FirstOrDefault();

    private static string Text(List<XmlSchemaFacet> facets, string name, SampleContext context)
    {
        var lower = name.ToLowerInvariant();
        string[] preferred = lower switch
        {
            "appserver" or "dbserver" or "authserver" => ["OK"],
            _ when lower.Contains("cuit") => [context.Cuit > 0 ? context.Cuit.ToString(CultureInfo.InvariantCulture) : "20111111112"],
            _ when lower.Contains("fecha") || lower.Contains("date") || lower.EndsWith("fch", StringComparison.Ordinal) =>
                [context.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
            _ when lower.Contains("resultado") => ["A"],
            _ => ["SIMULADO"],
        };
        var patterns = facets.OfType<XmlSchemaPatternFacet>().Select(f => new Regex($"^(?:{f.Value})$")).ToList();
        string[] fallbacks = ["S", "A", "1", "01", "001", "0001", "20111111112", context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            context.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), "AAA", "SIMULADO"];
        var generated = facets.OfType<XmlSchemaPatternFacet>().Select(f => PatternText.For(f.Value!)).OfType<string>();
        var candidates = preferred.Concat(fallbacks).Select(c => Fit(c, facets)).Concat(generated);
        return candidates.FirstOrDefault(c => patterns.All(p => p.IsMatch(c))) ?? Fit(preferred[0], facets);
    }

    private static string Fit(string value, List<XmlSchemaFacet> facets)
    {
        int? Size<T>() where T : XmlSchemaFacet =>
            facets.OfType<T>().Select(f => int.Parse(f.Value!, CultureInfo.InvariantCulture)).Cast<int?>().FirstOrDefault();
        var exact = Size<XmlSchemaLengthFacet>();
        var min = exact ?? Size<XmlSchemaMinLengthFacet>();
        var max = exact ?? Size<XmlSchemaMaxLengthFacet>();
        if (max is { } m && value.Length > m) value = value[..m];
        if (min is { } n && value.Length < n) value = value.PadRight(n, char.IsDigit(value.LastOrDefault()) ? '0' : 'X');
        return value;
    }
}
