using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// One operation of a WSDL binding: the element that comes in the Body (none
/// when the input message has no parts, as some Java services declare dummy),
/// the one that goes back, and the headers the response carries.
/// </summary>
public sealed record OperationContract(
    string Name,
    string? Action,
    XName? Input,
    XName Output,
    IReadOnlyList<XName> OutputHeaders);

/// <summary>
/// An operation the WSDL also binds to plain HTTP GET (the ASMX services'
/// dummy): its path under the address and the element it answers, sent bare.
/// </summary>
public sealed record HttpOperation(string Name, string Location, XName Output);

/// <summary>
/// What an ARCA WSDL promises, read from the file ARCA publishes: its
/// operations (document/literal, as every ARCA service is), the schemas of
/// their messages, the address path and whether the binding speaks SOAP 1.2.
/// The simulator answers from this, so a service ArcaSim has no rules for
/// still answers with the right elements, in the right namespaces.
/// </summary>
public sealed class ServiceContract
{
    private static readonly XNamespace Wsdl = "http://schemas.xmlsoap.org/wsdl/";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/wsdl/soap/";
    private static readonly XNamespace Soap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";
    private static readonly XNamespace Http = "http://schemas.xmlsoap.org/wsdl/http/";

    private readonly Dictionary<XName, OperationContract> _byInput = [];
    private readonly Dictionary<string, OperationContract> _byAction = new(StringComparer.Ordinal);

    public string File { get; }
    public string TargetNamespace { get; }
    public string AddressPath { get; }
    public bool SpeaksSoap12 { get; }
    public IReadOnlyList<OperationContract> Operations { get; }
    public XmlSchemaSet Schemas { get; }

    /// <summary>The files the WSDL imports, by the name it uses for them, so they can be served next to it.</summary>
    public IReadOnlyDictionary<string, string> Imports { get; }

    /// <summary>The operations the WSDL binds to HTTP GET besides SOAP; none in most services.</summary>
    public IReadOnlyList<HttpOperation> HttpOperations { get; private init; } = [];

    private ServiceContract(string file, string targetNamespace, string addressPath, bool soap12,
        List<OperationContract> operations, XmlSchemaSet schemas, Dictionary<string, string> imports)
    {
        File = file;
        TargetNamespace = targetNamespace;
        AddressPath = addressPath;
        SpeaksSoap12 = soap12;
        Operations = operations;
        Schemas = schemas;
        Imports = imports;
        foreach (var operation in operations)
        {
            if (operation.Input is not null) _byInput.TryAdd(operation.Input, operation);
            if (!string.IsNullOrEmpty(operation.Action)) _byAction.TryAdd(operation.Action, operation);
        }
    }

    /// <summary>
    /// The operation a request is for: by the Body's element, as the
    /// document/literal binding says, else by its action. An empty Body is the
    /// operation whose input has no parts, and an element named after such an
    /// operation is taken for it too, since that is what clients send.
    /// </summary>
    public OperationContract? Find(XName? bodyElement, string? action)
    {
        if (bodyElement is null)
            return (action is null ? null : _byAction.GetValueOrDefault(action)) ?? Operations.FirstOrDefault(o => o.Input is null);
        return _byInput.GetValueOrDefault(bodyElement)
               ?? (string.IsNullOrEmpty(action) ? null : _byAction.GetValueOrDefault(action))
               ?? _byInput.FirstOrDefault(p => p.Key.LocalName == bodyElement.LocalName && Lenient(p.Key.NamespaceName, bodyElement.NamespaceName)).Value
               ?? Operations.FirstOrDefault(o => o.Input is null && o.Name == bodyElement.LocalName);
    }

    /// <summary>
    /// Manuals that moved to arca.gob.ar sometimes print namespaces with .gob.ar
    /// where the WSDL says .gov.ar (wsmtxca); ARCA's servers reject those, and
    /// so does ArcaSim, except for that one swap, which clients copy from the manual.
    /// </summary>
    private static bool Lenient(string declared, string sent) =>
        declared.Replace(".gob.ar", ".gov.ar") == sent.Replace(".gob.ar", ".gov.ar");

    public static ServiceContract Load(string file)
    {
        var imports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var documents = new List<XDocument>();
        Collect(Path.GetFullPath(file), documents, imports, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var main = documents[0];

        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) throw e.Exception;
        };
        foreach (var document in documents)
        foreach (var schema in document.Descendants(Wsdl + "types").Elements(Xsd + "schema"))
            schemas.Add(ReadInline(schema, document.BaseUri));
        schemas.Compile();
        foreach (XmlSchema schema in schemas.Schemas())
        foreach (var include in schema.Includes.OfType<XmlSchemaExternal>().Where(i => i.SchemaLocation is not null))
        {
            var local = Path.Combine(Path.GetDirectoryName(new Uri(schema.SourceUri ?? file).LocalPath)!, include.SchemaLocation!);
            if (System.IO.File.Exists(local)) imports.TryAdd(include.SchemaLocation!, Path.GetFullPath(local));
        }

        var definitions = documents.Select(d => d.Root!).ToList();
        var messages = definitions.SelectMany(d => d.Elements(Wsdl + "message")).ToDictionary(m => QName(m, m.Attribute("name")!.Value), m => m);
        var portTypes = definitions.SelectMany(d => d.Elements(Wsdl + "portType")).ToDictionary(p => QName(p, p.Attribute("name")!.Value), p => p);
        var service = definitions.SelectMany(d => d.Elements(Wsdl + "service")).First();
        var ports = service.Elements(Wsdl + "port").ToList();
        var bindings = definitions.SelectMany(d => d.Elements(Wsdl + "binding")).ToDictionary(b => QName(b, b.Attribute("name")!.Value), b => b);

        // ARCA's .NET services publish a SOAP 1.1 and a SOAP 1.2 binding of the same port type; the 1.1 one names the actions.
        var portBindings = ports.Select(p => bindings[Resolve(p, p.Attribute("binding")!.Value)]).ToList();
        var soap12 = portBindings.Any(b => b.Element(Soap12 + "binding") is not null);
        var binding = portBindings.FirstOrDefault(b => b.Element(Soap11 + "binding") is not null) ?? portBindings[0];
        var portType = portTypes[Resolve(binding, binding.Attribute("type")!.Value)];

        var address = ports.Select(p => p.Elements().FirstOrDefault(e => e.Name.LocalName == "address")?.Attribute("location")?.Value)
            .FirstOrDefault(a => a is not null) ?? "/";

        var operations = new List<OperationContract>();
        foreach (var op in binding.Elements(Wsdl + "operation"))
        {
            var name = op.Attribute("name")!.Value;
            var abstractOp = portType.Elements(Wsdl + "operation").First(o => o.Attribute("name")!.Value == name);
            var action = op.Elements().FirstOrDefault(e => e.Name.LocalName == "operation")?.Attribute("soapAction")?.Value;
            var input = BodyElement(abstractOp.Element(Wsdl + "input")!, op.Element(Wsdl + "input"), messages);
            var output = BodyElement(abstractOp.Element(Wsdl + "output")!, op.Element(Wsdl + "output"), messages)
                         ?? throw new XmlException($"Operation {name} answers nothing.");
            var headers = op.Element(Wsdl + "output")?.Elements().Where(e => e.Name.LocalName == "header")
                .Select(h => PartElement(messages[Resolve(h, h.Attribute("message")!.Value)], h.Attribute("part")!.Value))
                .ToList() ?? [];
            operations.Add(new OperationContract(name, action, input, output, headers));
        }

        var httpOperations = (
            from httpBinding in portBindings.Where(b => b.Element(Http + "binding")?.Attribute("verb")?.Value == "GET").Distinct()
            let httpType = portTypes[Resolve(httpBinding, httpBinding.Attribute("type")!.Value)]
            from op in httpBinding.Elements(Wsdl + "operation")
            let location = op.Element(Http + "operation")?.Attribute("location")?.Value
            let abstractOp = httpType.Elements(Wsdl + "operation").FirstOrDefault(o => o.Attribute("name")?.Value == op.Attribute("name")?.Value)
            let output = abstractOp?.Element(Wsdl + "output")
            let part = output is null ? null : messages[Resolve(output, output.Attribute("message")!.Value)].Elements(Wsdl + "part").FirstOrDefault(p => p.Attribute("element") is not null)
            where location is not null && part is not null
            select new HttpOperation(op.Attribute("name")!.Value, location, Resolve(part, part.Attribute("element")!.Value))).ToList();

        return new ServiceContract(Path.GetFullPath(file), main.Root!.Attribute("targetNamespace")?.Value ?? "",
            PathOf(address), soap12, operations, schemas, imports) { HttpOperations = httpOperations };
    }

    /// <summary>The absolute path of the address, without a trailing slash or the port ARCA sometimes prints.</summary>
    public static string PathOf(string address)
    {
        var path = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.AbsolutePath : address;
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }

    private static void Collect(string file, List<XDocument> documents, Dictionary<string, string> imports, HashSet<string> seen)
    {
        if (!seen.Add(file)) return;
        var document = XDocument.Load(file, LoadOptions.SetBaseUri);
        documents.Add(document);
        foreach (var import in document.Root!.Elements(Wsdl + "import"))
        {
            var location = import.Attribute("location")?.Value;
            if (location is null || LocalCopy(file, location) is not { } local) continue;
            imports.TryAdd(location, local);
            Collect(local, documents, imports, seen);
        }
    }

    /// <summary>
    /// The saved copy of an imported file: next to the WSDL, or, when ARCA
    /// imports it by URL ("...?wsdl=Parent.wsdl"), in the folder named after the WSDL.
    /// </summary>
    private static string? LocalCopy(string file, string location)
    {
        var folder = Path.GetDirectoryName(file)!;
        var name = location.Contains("?wsdl=", StringComparison.OrdinalIgnoreCase) || location.Contains("?xsd=", StringComparison.OrdinalIgnoreCase)
            ? location[(location.IndexOf('=') + 1)..]
            : Path.GetFileName(location.Split('?')[0]);
        var stem = Path.GetFileNameWithoutExtension(file).Replace("-homologacion", "").Replace("-produccion", "");
        string[] candidates = Uri.TryCreate(location, UriKind.Absolute, out _)
            ? [Path.Combine(folder, stem, name), Path.Combine(folder, name)]
            : [Path.Combine(folder, location), Path.Combine(folder, stem, name)];
        return candidates.Select(Path.GetFullPath).FirstOrDefault(System.IO.File.Exists);
    }

    /// <summary>An inline schema carries the namespace prefixes declared on wsdl:definitions; it only reads alone with them copied in.</summary>
    private static XmlSchema ReadInline(XElement schema, string baseUri)
    {
        var copy = new XElement(schema);
        for (var scope = schema.Parent; scope is not null; scope = scope.Parent)
        foreach (var declaration in scope.Attributes().Where(a => a.IsNamespaceDeclaration))
            if (copy.Attribute(declaration.Name) is null)
                copy.SetAttributeValue(declaration.Name, declaration.Value);
        using var reader = XmlReader.Create(new StringReader(copy.ToString()), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }, baseUri);
        return XmlSchema.Read(reader, null)!;
    }

    private static XName? BodyElement(XElement abstractMessage, XElement? bound, Dictionary<XName, XElement> messages)
    {
        var message = messages[Resolve(abstractMessage, abstractMessage.Attribute("message")!.Value)];
        var parts = bound?.Elements().FirstOrDefault(e => e.Name.LocalName == "body")?.Attribute("parts")?.Value;
        var part = parts is not null
            ? message.Elements(Wsdl + "part").FirstOrDefault(p => p.Attribute("name")!.Value == parts.Split(' ')[0])
            : message.Elements(Wsdl + "part").FirstOrDefault(p => p.Attribute("element") is not null);
        return part is null ? null : Resolve(part, part.Attribute("element")!.Value);
    }

    private static XName PartElement(XElement message, string part) =>
        message.Elements(Wsdl + "part").Where(p => p.Attribute("name")!.Value == part)
            .Select(p => Resolve(p, p.Attribute("element")!.Value)).First();

    // Trimmed: wsremharina declares a message named " consultarTiposEmbalajeFault".
    private static XName QName(XElement element, string localName) =>
        XName.Get(localName.Trim(), element.Document!.Root!.Attribute("targetNamespace")?.Value ?? "");

    private static XName Resolve(XElement scope, string qualified)
    {
        qualified = qualified.Trim();
        var colon = qualified.IndexOf(':');
        if (colon < 0) return XName.Get(qualified, scope.GetDefaultNamespace().NamespaceName);
        var ns = scope.GetNamespaceOfPrefix(qualified[..colon]) ?? throw new XmlException($"Prefix {qualified[..colon]} is not declared.");
        return ns + qualified[(colon + 1)..];
    }
}
