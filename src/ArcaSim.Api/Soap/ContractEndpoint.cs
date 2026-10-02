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

            app.MapGet(contract.AddressPath, (HttpContext context) => Describe(context, definition, contract));
            app.MapPost(contract.AddressPath, (HttpContext context) => AnswerAsync(context, host, definition, contract));
        }
    }

    private static IResult Describe(HttpContext context, ServiceDefinition definition, ServiceContract contract)
    {
        var address = WsdlDocuments.BaseUrl(context.Request) + contract.AddressPath;
        if (context.Request.Query.TryGetValue("import", out var name))
        {
            var file = contract.Imports.Values.FirstOrDefault(f => Path.GetFileName(f).Equals(name.ToString(), StringComparison.OrdinalIgnoreCase));
            return file is null ? Results.NotFound() : Results.Text(Relink(File.ReadAllText(file), contract, address), "text/xml;charset=UTF-8");
        }
        if (WsdlDocuments.AsksForWsdl(context.Request))
            return Results.Text(Relink(WsdlDocuments.WithAddress(definition.Wsdl, address), contract, address), "text/xml;charset=UTF-8");
        return Results.Text($"<html><body><h1>{System.Net.WebUtility.HtmlEncode(definition.Name)}</h1>" +
                            "<p>ArcaSim · servicio SOAP. Descripción en ?wsdl.</p></body></html>", "text/html;charset=UTF-8");
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
        var writer = new DialectWriter(definition, request.Version);

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
            await writer.WriteAsync(context, ContractAnswer.Failed(new SoapFault("soap:Client", writer.UnknownOperation(body?.Name, request.Action))));
            return;
        }

        await writer.WriteAsync(context, await host.AnswerAsync(definition, operation, body, context.RequestAborted));
    }
}

/// <summary>How one dialect writes envelopes and faults, in the SOAP version the request came in.</summary>
public sealed class DialectWriter(ServiceDefinition definition, SoapVersion version)
{
    private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private const string Xsd = "http://www.w3.org/2001/XMLSchema";

    private bool Soap12 => version == SoapVersion.Soap12;
    private string EnvelopeNs => Soap12 ? SoapRequest.Soap12Namespace : SoapRequest.Soap11Namespace;
    private string Prefix => definition.EnvelopePrefix ?? definition.Dialect switch
    {
        Dialect.JaxWs => "S",
        Dialect.SpringWs => "SOAP-ENV",
        _ => "soap",
    };

    /// <summary>JAX-WS opens with an XML declaration in single quotes; CXF and Spring-WS send none.</summary>
    private string Declaration => definition.Dialect switch
    {
        Dialect.Asmx => "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        Dialect.JaxWs => "<?xml version='1.0' encoding='UTF-8'?>",
        _ => "",
    };

    /// <summary>Spring-WS always writes an empty Header.</summary>
    private string EmptyHeader => definition.Dialect == Dialect.SpringWs ? $"<{Prefix}:Header/>" : "";

    public string Unreadable(string message) => definition.Dialect switch
    {
        Dialect.Asmx => "Server was unable to read request. ---> There is an error in XML document. ---> " + message,
        Dialect.Cxf => "Error reading XMLStreamReader: " + message,
        _ => "Couldn't create SOAP message due to exception: " + message,
    };

    public string UnknownOperation(XName? element, string? action) => definition.Dialect switch
    {
        Dialect.Asmx => $"Server did not recognize the value of HTTP Header SOAPAction: {action}.",
        Dialect.Cxf => element is null ? "No such operation" : $"Unexpected wrapper element {{{element.NamespaceName}}}{element.LocalName} found.",
        _ => element is null ? "Cannot find dispatch method" : $"Cannot find dispatch method for {{{element.NamespaceName}}}{element.LocalName}",
    };

    public async Task WriteAsync(HttpContext context, ContractAnswer answer)
    {
        var text = answer.Fault is { } fault ? Fault(fault) : Envelope(answer.Headers, answer.Body!);
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = answer.Fault?.Status ?? 200;
        context.Response.ContentType = (Soap12 ? "application/soap+xml" : "text/xml") + definition.Dialect switch
        {
            Dialect.Asmx => "; charset=utf-8",
            Dialect.JaxWs => ";charset=utf-8",
            _ => ";charset=UTF-8",
        };
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private string Envelope(IReadOnlyList<XElement> headers, XElement body)
    {
        var header = headers.Count == 0 ? EmptyHeader : $"<{Prefix}:Header>{string.Concat(headers.Select(Payload))}</{Prefix}:Header>";
        return Wrap($"{header}<{Prefix}:Body>{Payload(body)}</{Prefix}:Body>");
    }

    private string Wrap(string content) => definition.Dialect == Dialect.Asmx
        ? $"{Declaration}<soap:Envelope xmlns:soap=\"{EnvelopeNs}\" xmlns:xsi=\"{Xsi}\" xmlns:xsd=\"{Xsd}\">{content}</soap:Envelope>"
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

    private string Fault(SoapFault fault)
    {
        var sender = fault.Code.EndsWith("Client", StringComparison.Ordinal) || fault.Code.EndsWith("Sender", StringComparison.Ordinal);
        var message = System.Security.SecurityElement.Escape(fault.Message);
        var detail = fault.Detail is null ? null : Payload(fault.Detail);
        var p = Prefix;

        if (Soap12)
        {
            var detail12 = detail is null ? definition.Dialect == Dialect.Asmx ? $"<{p}:Detail />" : "" : $"<{p}:Detail>{detail}</{p}:Detail>";
            return Wrap($"{EmptyHeader}<{p}:Body><{p}:Fault><{p}:Code><{p}:Value>{p}:{(sender ? "Sender" : "Receiver")}</{p}:Value></{p}:Code>" +
                        $"<{p}:Reason><{p}:Text xml:lang=\"{(definition.Dialect == Dialect.Asmx ? "es-AR" : "en")}\">{message}</{p}:Text></{p}:Reason>" +
                        $"{detail12}</{p}:Fault></{p}:Body>");
        }

        return definition.Dialect switch
        {
            Dialect.Asmx => Wrap($"<soap:Body><soap:Fault><faultcode>soap:{(sender ? "Client" : "Server")}</faultcode><faultstring>{message}</faultstring>" +
                                 $"{(detail is null ? "<detail />" : $"<detail>{detail}</detail>")}</soap:Fault></soap:Body>"),
            // The fault as the wscommon manuals print it: a SOAP 1.2 code inside a SOAP 1.1 envelope.
            Dialect.JaxWs => Wrap($"<{p}:Body><ns2:Fault xmlns:ns2=\"{SoapRequest.Soap11Namespace}\" xmlns:ns3=\"{SoapRequest.Soap12Namespace}\">" +
                                  $"<faultcode>ns3:{(sender ? "Sender" : "Receiver")}</faultcode><faultstring>{message}</faultstring>" +
                                  $"{(detail is null ? "" : $"<detail>{detail}</detail>")}</ns2:Fault></{p}:Body>"),
            Dialect.SpringWs => Wrap($"{EmptyHeader}<{p}:Body><{p}:Fault><faultcode>{p}:{(sender ? "Client" : "Server")}</faultcode>" +
                                     $"<faultstring xml:lang=\"en\">{message}</faultstring>{(detail is null ? "" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
            _ => Wrap($"<{p}:Body><{p}:Fault><faultcode>{(fault.Code.Contains(':') ? fault.Code : $"{p}:{fault.Code}")}</faultcode>" +
                      $"<faultstring>{message}</faultstring>{(detail is null ? "" : $"<detail>{detail}</detail>")}</{p}:Fault></{p}:Body>"),
        };
    }
}
