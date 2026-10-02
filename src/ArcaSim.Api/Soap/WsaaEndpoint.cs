using System.Text;
using System.Xml;
using ArcaSim.Application;
using ArcaSim.Application.Wsaa;

namespace ArcaSim.Api.Soap;

/// <summary>
/// /ws/services/LoginCms as ARCA's Apache Axis 1.4 answers it (docs/arca/wsaa.md
/// §1.3, §1.4, §6.3): SOAPAction required in SOAP 1.1 but never checked, the
/// element's namespace not checked either, the TA escaped as text inside
/// loginCmsReturn, and every fault with HTTP 500, ns1 bound to Axis'
/// namespace and characters beyond ASCII written as references.
/// </summary>
public sealed class WsaaEndpoint(WsaaService service, SimulationSettings settings)
{
    private const string Path = "/ws/services/LoginCms";
    private const string WsaaNamespace = "http://wsaa.view.sua.dvadac.desein.afip.gov";
    private const string AxisNamespace = "http://xml.apache.org/axis/";

    public static void Map(WebApplication app)
    {
        app.MapGet(Path, (HttpContext context, SimulationSettings settings) =>
            WsdlDocuments.AsksForWsdl(context.Request)
                ? Results.Text(WsdlDocuments.Wsaa(settings.Environment, WsdlDocuments.BaseUrl(context.Request)), "text/xml;charset=utf-8")
                : Results.Text("<h1>LoginCms</h1>\n<p>Hi there, this is an AXIS service!</p>\n<i>Perhaps there will be a form for invoking the service here...</i>",
                    "text/html;charset=utf-8"));
        app.MapPost(Path, (HttpContext context, WsaaEndpoint endpoint) => endpoint.HandleAsync(context));
    }

    public async Task HandleAsync(HttpContext context)
    {
        var request = await SoapRequest.ReadAsync(context.Request);
        if (request is null)
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        var chaos = settings.ChaosFor("wsaa");
        if (chaos.Delay > TimeSpan.Zero) await Task.Delay(chaos.Delay, context.RequestAborted);

        if (request.Version == SoapVersion.Soap11 && !context.Request.Headers.ContainsKey("SOAPAction"))
        {
            await SendAsync(context, request, 500, Fault(request, "ns1:Client.NoSOAPAction", "no SOAPAction header!", exceptionName: false));
            return;
        }

        string? in0;
        try
        {
            request.EnsureWellFormed();
            using var body = request.OpenBody();
            in0 = ReadIn0(body);
        }
        catch (XmlException ex)
        {
            await SendAsync(context, request, 500, Fault(request, "soapenv:Server.userException",
                $"org.xml.sax.SAXParseException; lineNumber: {ex.LineNumber}; columnNumber: {ex.LinePosition}; {ex.Message}", exceptionName: false));
            return;
        }
        catch (SoapBodyEmptyException)
        {
            in0 = null;
        }
        if (in0 is null)
        {
            await SendAsync(context, request, 500, Fault(request, "soapenv:Server.userException",
                "javax.ejb.EJBException: java.lang.NullPointerException", exceptionName: false));
            return;
        }

        var result = await service.LoginAsync(in0, context.RequestAborted);
        if (result.Fault is { } fault)
        {
            await SendAsync(context, request, 500, Fault(request, $"ns1:{fault.Code}", fault.Message, exceptionName: true));
            return;
        }

        var envelope =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            $"<soapenv:Envelope xmlns:soapenv=\"{request.EnvelopeNamespace}\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            $"<soapenv:Body><loginCmsResponse xmlns=\"{WsaaNamespace}\"><loginCmsReturn>{Escape(result.TicketXml!)}</loginCmsReturn></loginCmsResponse></soapenv:Body></soapenv:Envelope>";
        await SendAsync(context, request, 200, envelope);
    }

    /// <summary>in0 is found by its local name inside the operation's element, whatever their namespace.</summary>
    private static string? ReadIn0(XmlReader body)
    {
        if (body.IsEmptyElement) return null;
        var depth = body.Depth;
        while (body.Read() && body.Depth > depth)
        {
            if (body.NodeType == XmlNodeType.Element && body.LocalName == "in0" && body.Depth == depth + 1)
                return body.ReadElementContentAsString();
        }
        return null;
    }

    private string Fault(SoapRequest request, string code, string message, bool exceptionName)
    {
        var details =
            (exceptionName ? $"<ns2:exceptionName xmlns:ns2=\"{AxisNamespace}\">gov.afip.desein.dvadac.sua.view.wsaa.LoginFault</ns2:exceptionName>" : "") +
            $"<ns3:hostname xmlns:ns3=\"{AxisNamespace}\">{settings.Profile.FaultHostname}</ns3:hostname>";
        var fault = request.Version == SoapVersion.Soap12
            ? $"<soapenv:Fault><soapenv:Code xmlns:ns1=\"{AxisNamespace}\"><soapenv:Value>{code}</soapenv:Value></soapenv:Code>" +
              $"<soapenv:Reason><soapenv:Text xml:lang=\"en\">{Escape(message)}</soapenv:Text></soapenv:Reason><soapenv:Detail>{details}</soapenv:Detail></soapenv:Fault>"
            : $"<soapenv:Fault><faultcode xmlns:ns1=\"{AxisNamespace}\">{code}</faultcode><faultstring>{Escape(message)}</faultstring><detail>{details}</detail></soapenv:Fault>";
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               $"<soapenv:Envelope xmlns:soapenv=\"{request.EnvelopeNamespace}\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               $"<soapenv:Body>{fault}</soapenv:Body></soapenv:Envelope>";
    }

    /// <summary>Axis' escaping: the five markup characters as entities and anything beyond ASCII as a hexadecimal reference.</summary>
    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                > '\u007F' => $"&#x{(int)c:X};",
                _ => c.ToString(),
            });
        }
        return builder.ToString();
    }

    private static async Task SendAsync(HttpContext context, SoapRequest request, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = status;
        context.Response.ContentType = request.Version == SoapVersion.Soap12
            ? "application/soap+xml;charset=UTF-8"
            : "text/xml;charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}
