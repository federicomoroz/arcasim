using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ArcaSim.Application;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Soap;
using ArcaSim.Domain;

namespace ArcaSim.Api.Soap;

/// <summary>
/// Puts every service of the catalog on the path ARCA serves it from: its WSDL
/// (and the files it imports) on GET, and its operations on POST, in SOAP 1.1
/// or 1.2 as the request comes, written the way the service's dialect writes.
/// The operations its WSDL binds to HTTP GET (the ASMX dummies) answer there too.
/// </summary>
public static class ContractEndpoint
{
    public static void Map(WebApplication app, ContractHost host)
    {
        foreach (var definition in host.Catalog.Services)
        {
            var contract = host.ContractOf(definition);
            foreach (var id in definition.Wsaa) WebService.Register(id, definition.Name);
            ServiceRoutes.Register(contract.AddressPath, definition.Id);

            app.MapGet(contract.AddressPath, (Delegate)((HttpContext context) => DescribeAsync(context, host, definition, contract)));
            app.MapPost(contract.AddressPath, (HttpContext context) => AnswerAsync(context, host, definition, contract));
            foreach (var operation in contract.HttpOperations)
                app.MapMethods(contract.AddressPath + operation.Location, ["GET", "POST"], () => Bare(host, definition, operation));
        }
    }

    private static async Task<IResult> DescribeAsync(HttpContext context, ContractHost host, ServiceDefinition definition, ServiceContract contract)
    {
        var address = WsdlDocuments.BaseUrl(context.Request) + contract.AddressPath;
        if (context.Request.Query.TryGetValue("import", out var name))
        {
            var file = contract.Imports.Values.FirstOrDefault(f => Path.GetFileName(f).Equals(name.ToString(), StringComparison.OrdinalIgnoreCase));
            return file is null ? Results.NotFound() : Results.Text(Relink(File.ReadAllText(file), contract, address), "text/xml;charset=UTF-8");
        }
        if (WsdlDocuments.AsksForWsdl(context.Request))
            return Results.Text(Relink(WsdlDocuments.WithAddress(definition.Wsdl, address), contract, address), "text/xml;charset=UTF-8");
        // The factu.fisca Spring Boot services answer a plain GET with their dummy (wsfecredagente.md, wsfecredsca.md).
        if (definition.DummyOnGet && contract.Operations.FirstOrDefault(o => o.Name.Equals("dummy", StringComparison.OrdinalIgnoreCase)) is { } dummy)
        {
            var writer = new DialectWriter(definition, SoapVersion.Soap11) { ServiceHeader = host.HeaderOf(definition), Now = host.Now };
            await writer.WriteAsync(context, await host.AnswerAsync(definition, dummy, null, context.RequestAborted));
            return Results.Empty;
        }
        return Results.Text($"<html><body><h1>{System.Net.WebUtility.HtmlEncode(definition.Name)}</h1>" +
                            "<p>ArcaSim · servicio SOAP. Descripción en ?wsdl.</p></body></html>", "text/html;charset=UTF-8");
    }

    /// <summary>
    /// An operation bound to HTTP GET and POST: its element alone, indented,
    /// with the namespace declarations .NET's serializer writes (wsbfe.md, wsseg.md).
    /// </summary>
    private static IResult Bare(ContractHost host, ServiceDefinition definition, HttpOperation operation)
    {
        var element = host.SampleOf(definition, operation.Output);
        element.SetAttributeValue(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema");
        element.SetAttributeValue(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance");
        var text = new StringBuilder();
        using (var writer = XmlWriter.Create(new StringWriter(text), new XmlWriterSettings { Indent = true, NewLineChars = "\r\n", OmitXmlDeclaration = true }))
            element.WriteTo(writer);
        return Results.Text("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" + text, "text/xml; charset=utf-8");
    }

    /// <summary>
    /// A request no operation takes: the dialect's fault, or what the catalog
    /// says the service answers (DDJJ's Server.processError with its detail,
    /// SUD's empty 404).
    /// </summary>
    private static async Task RefuseUnknownAsync(
        HttpContext context, ContractHost host, ServiceDefinition definition, ServiceContract contract, DialectWriter writer, XName? element, string? action)
    {
        var rule = definition.UnknownOperation;
        if (rule is { Text: "" })
        {
            context.Response.StatusCode = rule.Status ?? StatusCodes.Status404NotFound;
            context.Response.ContentLength = 0;
            return;
        }
        var expected = element is null ? null : contract.Operations.FirstOrDefault(o => o.Input?.LocalName == element.LocalName)?.Input;
        var address = WsdlDocuments.BaseUrl(context.Request) + contract.AddressPath;
        var values = new PlaceholderValues(host.Now)
        {
            Service = definition.Id,
            Element = element is null ? "" : $"{{{element.NamespaceName}}}{element.LocalName}",
            Expected = expected is null ? "" : $"{{{expected.NamespaceName}}}{expected.LocalName}",
        };
        var text = rule?.Text is { } template ? Placeholders.Fill(template, values) : writer.UnknownOperation(element, action, address, expected);
        var detail = rule?.Detail is { Length: > 0 } fragment ? new XElement(ContractHost.DetailFragment, Placeholders.Fill(fragment, values)) : null;
        await writer.WriteAsync(context,
            ContractAnswer.Failed(new SoapFault(rule?.FaultCode ?? "soap:Client", text, detail, rule?.Status ?? StatusCodes.Status500InternalServerError)),
            withServiceHeader: false);
    }

    /// <summary>The imports ARCA's WSDL points at its own host now point at ArcaSim's copies.</summary>
    private static string Relink(string document, ServiceContract contract, string address)
    {
        foreach (var (location, file) in contract.Imports)
            document = document.Replace($"\"{location}\"", $"\"{address}?import={Uri.EscapeDataString(Path.GetFileName(file))}\"");
        return document;
    }

    private static async Task AnswerAsync(HttpContext context, ContractHost host, ServiceDefinition definition, ServiceContract contract)
    {
        var settings = context.RequestServices.GetRequiredService<SimulationSettings>();
        var chaos = settings.ChaosFor(definition.Id);
        if (chaos.Delay > TimeSpan.Zero) await Task.Delay(chaos.Delay, context.RequestAborted);
        if (chaos.Down)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var request = await SoapRequest.ReadAsync(context.Request);
        if (request is null)
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
        var writer = new DialectWriter(definition, request.Version)
        {
            ServiceHeader = host.HeaderOf(definition),
            BalancerMask = chaos.BalancerMask,
            Now = host.Now,
        };

        XElement? body;
        try
        {
            var document = XDocument.Parse(request.Body);
            var soapBody = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body")
                           ?? throw new XmlException("The SOAP envelope has no Body.");
            body = soapBody.Elements().FirstOrDefault();
        }
        catch (XmlException ex)
        {
            await writer.WriteAsync(context, ContractAnswer.Failed(new SoapFault("soap:Client", writer.Unreadable(ex.Message))));
            return;
        }

        var operation = contract.Find(body?.Name, request.Action);
        if (operation is null)
        {
            await RefuseUnknownAsync(context, host, definition, contract, writer, body?.Name, request.Action);
            return;
        }

        await writer.WriteAsync(context, await host.AnswerAsync(definition, operation, body, context.RequestAborted));
    }
}

/// <summary>
/// How one dialect writes envelopes and faults, in the SOAP version the request
/// came in. ServiceHeader is the header the service adds on its own (the
/// catalog's Header); BalancerMask turns every HTTP 500 into fwshomo's F5 line.
/// </summary>
public sealed class DialectWriter(ServiceDefinition definition, SoapVersion version)
{
    private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private const string Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly TimeSpan Argentina = TimeSpan.FromHours(-3);

    public string? ServiceHeader { get; init; }

    public bool BalancerMask { get; init; }

    /// <summary>The moment the answer goes out, from ArcaSim's clock, for the mask's date.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    private bool Soap12 => version == SoapVersion.Soap12;
    private string EnvelopeNs => Soap12 ? SoapRequest.Soap12Namespace : SoapRequest.Soap11Namespace;
    private string Prefix => definition.EnvelopePrefix ?? definition.Dialect switch
    {
        Dialect.JaxWs => "S",
        Dialect.SpringWs => "SOAP-ENV",
        Dialect.Axis2 => "soapenv",
        _ => "soap",
    };

    /// <summary>JAX-WS and Axis2 open with an XML declaration in single quotes; CXF and Spring-WS send none.</summary>
    private string Declaration => definition.Dialect switch
    {
        Dialect.Asmx => "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        Dialect.JaxWs => "<?xml version='1.0' encoding='UTF-8'?>",
        Dialect.Axis2 => "<?xml version='1.0' encoding='utf-8'?>",
        _ => "",
    };

    /// <summary>Spring-WS always writes an empty Header.</summary>
    private string EmptyHeader => definition.Dialect == Dialect.SpringWs ? $"<{Prefix}:Header/>" : "";

    public string Unreadable(string message) => definition.Dialect switch
    {
        Dialect.Asmx => "Server was unable to read request. ---> There is an error in XML document. ---> " + message,
        Dialect.Cxf => "Error reading XMLStreamReader: " + message,
        Dialect.Axis2 => "Acceso Denegado  - " + message,
        _ => "Couldn't create SOAP message due to exception: " + message,
    };

    public string UnknownOperation(XName? element, string? action) => UnknownOperation(element, action, "");

    public string UnknownOperation(XName? element, string? action, string address) => UnknownOperation(element, action, address, null);

    /// <summary>
    /// The fault for a request no operation takes. Axis2 names the endpoint's
    /// address in it (wsmtxca.md); CXF names the element it expected when one
    /// has that name in another namespace (uploadPresentacionService.md).
    /// </summary>
    public string UnknownOperation(XName? element, string? action, string address, XName? expected) => definition.Dialect switch
    {
        Dialect.Asmx => $"Server did not recognize the value of HTTP Header SOAPAction: {action}.",
        Dialect.Cxf => element is null ? "No such operation"
            : $"Unexpected wrapper element {{{element.NamespaceName}}}{element.LocalName} found." +
              (expected is null ? "" : $"   Expected {{{expected.NamespaceName}}}{expected.LocalName}."),
        Dialect.Axis2 => $"The endpoint reference (EPR) for the Operation not found is {address} and the WSA Action = {(string.IsNullOrEmpty(action) ? "null" : action)}",
        _ => element is null ? "Cannot find dispatch method" : $"Cannot find dispatch method for {{{element.NamespaceName}}}{element.LocalName}",
    };

    public Task WriteAsync(HttpContext context, ContractAnswer answer) => WriteAsync(context, answer, withServiceHeader: true);

    public async Task WriteAsync(HttpContext context, ContractAnswer answer, bool withServiceHeader)
    {
        var status = answer.Fault?.Status ?? 200;
        if (BalancerMask && status >= 500)
        {
            await WriteMaskAsync(context);
            return;
        }

        var header = withServiceHeader && (answer.Fault is null || definition.HeaderOnFaults) ? ServiceHeader : null;
        var headers = header ?? string.Concat(answer.Headers.Select(Payload));
        var text = answer.Fault is { } fault ? Fault(fault, headers) : Envelope(headers, answer.Body!);
        var contentType = (Soap12 ? "application/soap+xml" : "text/xml") + definition.Dialect switch
        {
            Dialect.Asmx => "; charset=utf-8",
            Dialect.JaxWs or Dialect.Axis2 => ";charset=utf-8",
            _ => ";charset=UTF-8",
        };
        if (definition.Mtom && answer.Fault is null) (text, contentType) = Mtom(text);

        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    /// <summary>
    /// fwshomo's F5 in homologación: 200, no Content-Type, "BL" and a 13-digit
    /// incident number, the local time and 500. ARCA's goes out as HTTP/1.0,
    /// which Kestrel does not write.
    /// </summary>
    private async Task WriteMaskAsync(HttpContext context)
    {
        var line = $"BL{RandomDigits(13)} {Now.ToOffset(Argentina).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} 500";
        var bytes = Encoding.ASCII.GetBytes(line);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private static string RandomDigits(int count) =>
        string.Concat(Enumerable.Range(0, count).Select(i => (char)('0' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(i == 0 ? 1 : 0, 10))));

    /// <summary>The envelope as the only part of an MTOM package, the way veconsumerws (CXF) sends every answer (veconsumerws.md).</summary>
    private (string Text, string ContentType) Mtom(string envelope)
    {
        var boundary = $"uuid:{Guid.NewGuid()}";
        const string start = "<root.message@cxf.apache.org>";
        var type = Soap12 ? "application/soap+xml" : "text/xml";
        var text = $"\r\n--{boundary}\r\nContent-Type: application/xop+xml; charset=UTF-8; type=\"{type}\";\r\n" +
                   $"Content-Transfer-Encoding: binary\r\nContent-ID: {start}\r\n\r\n{envelope}\r\n--{boundary}--";
        return (text, $"multipart/related; type=\"application/xop+xml\"; boundary=\"{boundary}\"; start=\"{start}\"; start-info=\"{type}\"");
    }

    private string Envelope(string headers, XElement body)
    {
        var header = headers.Length == 0 ? EmptyHeader : $"<{Prefix}:Header>{headers}</{Prefix}:Header>";
        return Wrap($"{header}<{Prefix}:Body>{Payload(body)}</{Prefix}:Body>");
    }

    private string Wrap(string content) => definition.Dialect == Dialect.Asmx
        ? $"{Declaration}<{Prefix}:Envelope xmlns:{Prefix}=\"{EnvelopeNs}\" xmlns:xsi=\"{Xsi}\" xmlns:xsd=\"{Xsd}\">{content}</{Prefix}:Envelope>"
        : $"{Declaration}<{Prefix}:Envelope xmlns:{Prefix}=\"{EnvelopeNs}\">{content}</{Prefix}:Envelope>";

    /// <summary>
    /// ASMX writes its elements under a default namespace. The Java servers give
    /// the response's namespace the prefix ns2 (ns1 in a few) and number the
    /// other namespaces after it; unqualified children carry none.
    /// </summary>
    private string Payload(XElement element)
    {
        if (definition.Dialect != Dialect.Asmx)
        {
            var namespaces = element.DescendantsAndSelf().Select(e => e.Name.Namespace)
                .Where(ns => ns != XNamespace.None).Distinct().ToList();
            var number = int.Parse(Regex.Match(definition.Prefix, @"\d+").Value);
            for (var i = 0; i < namespaces.Count; i++)
                element.SetAttributeValue(XNamespace.Xmlns + (i == 0 ? definition.Prefix : $"ns{number + i}"), namespaces[i].NamespaceName);
        }
        return element.ToString(SaveOptions.DisableFormatting);
    }

    private string Fault(SoapFault fault, string headers)
    {
        var sender = fault.Code.EndsWith("Client", StringComparison.Ordinal) || fault.Code.EndsWith("Sender", StringComparison.Ordinal)
                     || fault.Code.Contains("Client.", StringComparison.Ordinal);
        var message = System.Security.SecurityElement.Escape(fault.Message);
        var detail = fault.Detail is null ? null
            : fault.Detail.Name == ContractHost.DetailFragment ? fault.Detail.Value
            : Payload(fault.Detail);
        var p = Prefix;
        var header = headers.Length == 0 ? EmptyHeader : $"<{p}:Header>{headers}</{p}:Header>";

        if (Soap12)
        {
            var detail12 = detail is null ? definition.Dialect == Dialect.Asmx ? $"<{p}:Detail />" : "" : $"<{p}:Detail>{detail}</{p}:Detail>";
            return Wrap($"{header}<{p}:Body><{p}:Fault><{p}:Code><{p}:Value>{p}:{(sender ? "Sender" : "Receiver")}</{p}:Value></{p}:Code>" +
                        $"<{p}:Reason><{p}:Text xml:lang=\"{(definition.Dialect == Dialect.Asmx ? "es-AR" : "en")}\">{message}</{p}:Text></{p}:Reason>" +
                        $"{detail12}</{p}:Fault></{p}:Body>");
        }

        return definition.Dialect switch
        {
            Dialect.Asmx => Wrap($"{header}<{p}:Body><{p}:Fault><faultcode>{p}:{(sender ? "Client" : "Server")}</faultcode><faultstring>{message}</faultstring>" +
                                 $"{(detail is null ? "<detail />" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
            // The fault as the wscommon manuals print it, and as a 2021 wsct capture shows it:
            // a SOAP 1.2 code, with a space after the prefix, inside a SOAP 1.1 envelope.
            Dialect.JaxWs => Wrap($"{header}<{p}:Body><ns2:Fault xmlns:ns2=\"{SoapRequest.Soap11Namespace}\" xmlns:ns3=\"{SoapRequest.Soap12Namespace}\">" +
                                  $"<faultcode>ns3: {(sender ? "Sender" : "Receiver")}</faultcode><faultstring>{message}</faultstring>" +
                                  $"{(detail is null ? "" : $"<detail>{detail}</detail>")}</ns2:Fault></{p}:Body>"),
            Dialect.SpringWs => Wrap($"{header}<{p}:Body><{p}:Fault><faultcode>{p}:{(sender ? "Client" : "Server")}</faultcode>" +
                                     $"<faultstring xml:lang=\"en\">{message}</faultstring>{(detail is null ? "" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
            Dialect.Axis2 => Wrap($"{header}<{p}:Body><{p}:Fault><faultcode>{Code(fault.Code)}</faultcode><faultstring>{message}</faultstring>" +
                                  $"{(detail is null ? "<detail />" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
            _ => Wrap($"{header}<{p}:Body><{p}:Fault><faultcode>{Code(fault.Code)}</faultcode>" +
                      $"<faultstring>{message}</faultstring>{(detail is null ? "" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
        };
    }

    /// <summary>The fault code with the envelope's own prefix: "soap:Client" becomes "soapenv:Client" where the envelope is soapenv.</summary>
    private string Code(string code) =>
        !code.Contains(':') ? $"{Prefix}:{code}"
        : code.StartsWith("soap:", StringComparison.Ordinal) ? $"{Prefix}:{code[5..]}"
        : code;
}
