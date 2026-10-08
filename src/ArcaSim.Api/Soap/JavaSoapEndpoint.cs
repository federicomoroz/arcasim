using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ArcaSim.Application;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Soap;

namespace ArcaSim.Api.Soap;

/// <summary>
/// One ARCA service answered in the Java dialect (the padrón, and the
/// fwshomo/serviciosjava services): SOAP 1.1, dispatch by the body's element,
/// unqualified children, the response element with an ns2 prefix, and faults
/// with HTTP 500.
/// </summary>
public sealed record JavaSoapService(
    string Path,
    string Wsdl,
    string Namespace,
    string ChaosKey,
    IReadOnlyCollection<string> Operations,
    Func<IServiceProvider, string, XElement, CancellationToken, Task<SoapResult>> Handle);

public static class JavaSoapEndpoint
{
    private const string Envelope = "http://schemas.xmlsoap.org/soap/envelope/";

    public static void Map(WebApplication app, JavaSoapService service)
    {
        app.Services.GetRequiredService<ServiceDirectory>().Route(service.Path, service.ChaosKey);

        app.MapGet(service.Path, (HttpContext context) =>
            WsdlDocuments.AsksForWsdl(context.Request)
                ? Results.Text(WsdlDocuments.WithAddress(service.Wsdl, WsdlDocuments.BaseUrl(context.Request) + service.Path), "text/xml;charset=UTF-8")
                : Results.Text($"<html><body><h1>{System.Net.WebUtility.HtmlEncode(service.Path)}</h1><p>ArcaSim · servicio SOAP. Descripción en ?wsdl.</p></body></html>",
                    "text/html;charset=UTF-8"));

        app.MapPost(service.Path, async (HttpContext context) =>
        {
            if (await ChaosGate.RefusedAsync(context, context.RequestServices.GetRequiredService<SimulationSettings>().ChaosOf(service.ChaosKey))) return;

            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            XElement request;
            try
            {
                var document = SafeXml.Parse(body);
                request = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body")?.Elements().FirstOrDefault()
                          ?? throw new XmlException("The SOAP body has no element.");
            }
            catch (XmlException ex)
            {
                await WriteAsync(context, 500, Fault(new SoapFault("soap:Client", $"Error reading XMLStreamReader: {ex.Message}")));
                return;
            }

            if (request.Name.NamespaceName != service.Namespace || !service.Operations.Contains(request.Name.LocalName))
            {
                await WriteAsync(context, 500, Fault(new SoapFault("soap:Client",
                    $"Unexpected wrapper element {{{request.Name.NamespaceName}}}{request.Name.LocalName} found.")));
                return;
            }

            SoapResult result;
            try
            {
                result = await service.Handle(context.RequestServices, request.Name.LocalName, request, context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
            {
                // CXF answers an exception nobody handled with its generic fault, never a bare HTTP 500.
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(JavaSoapEndpoint)).LogError(ex, "{Path} failed", service.Path);
                result = SoapResult.Fail(new SoapFault("soap:Server", ContractHost.Unexpected(Dialect.Cxf, ex)));
            }
            if (result.Fault is { } fault) await WriteAsync(context, 500, Fault(fault));
            else await WriteAsync(context, 200, Content(result.Body!, service.Namespace));
        });
    }

    /// <summary>The response element carries the service's namespace as ns2; everything under it is unqualified.</summary>
    private static string Content(XElement body, string ns)
    {
        body.SetAttributeValue(XNamespace.Xmlns + "ns2", ns);
        return Wrap(body.ToString(SaveOptions.DisableFormatting));
    }

    private static string Fault(SoapFault fault)
    {
        var detail = fault.Detail is null ? "" : $"<detail>{Named(fault.Detail)}</detail>";
        return Wrap($"<soap:Fault><faultcode>{fault.Code}</faultcode><faultstring>{System.Security.SecurityElement.Escape(fault.Message)}</faultstring>{detail}</soap:Fault>");
    }

    private static string Named(XElement element)
    {
        if (element.Name.NamespaceName.Length > 0) element.SetAttributeValue(XNamespace.Xmlns + "ns1", element.Name.NamespaceName);
        return element.ToString(SaveOptions.DisableFormatting);
    }

    private static string Wrap(string inner) =>
        $"<soap:Envelope xmlns:soap=\"{Envelope}\"><soap:Body>{inner}</soap:Body></soap:Envelope>";

    private static async Task WriteAsync(HttpContext context, int status, string envelope)
    {
        var bytes = Encoding.UTF8.GetBytes(envelope);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/xml;charset=UTF-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}
