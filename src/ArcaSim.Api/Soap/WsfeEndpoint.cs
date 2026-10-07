using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ArcaSim.Application;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Api.Soap;

/// <summary>
/// /wsfev1/service.asmx as ARCA's ASP.NET host answers it (docs/arca/wsfev1.md
/// §1.2, §1.5, §1.6, §8.1): SOAP 1.1 and 1.2, routing by SOAPAction or by the
/// body's element, HTTP 400 with no body for broken XML, HTTP 500 faults for
/// what the serializer cannot read, and business answers in one line with the
/// FEHeaderInfo header. Requests and answers go through XmlSerializer, the
/// serializer ASMX itself uses, so its tolerances and its output come for free.
/// </summary>
public sealed class WsfeEndpoint(WsfeService service, SimulationSettings settings, IClock clock, ILogger<WsfeEndpoint> logger)
{
    private const string Path = "/wsfev1/service.asmx";

    private sealed record Operation(Type Request, Func<WsfeService, object, CancellationToken, Task<object>> Invoke);

    private static readonly Dictionary<string, Operation> Operations = new()
    {
        ["FEDummy"] = Sync<FEDummyRequest>((s, _) => s.FEDummy()),
        ["FECompTotXRequest"] = Sync<AuthOnlyRequest>((s, r) => s.FECompTotXRequest(r)),
        ["FECompUltimoAutorizado"] = Async<FECompUltimoAutorizadoRequest>((s, r, ct) => Box(s.FECompUltimoAutorizadoAsync(r, ct))),
        ["FECompConsultar"] = Async<FECompConsultarRequest>((s, r, ct) => Box(s.FECompConsultarAsync(r, ct))),
        ["FECAESolicitar"] = Async<FECAESolicitarRequest>((s, r, ct) => Box(s.FECAESolicitarAsync(r, ct))),
        ["FECAEASolicitar"] = Async<CaeaPeriodRequest>((s, r, ct) => Box(s.FECAEASolicitarAsync(r, ct))),
        ["FECAEAConsultar"] = Async<CaeaPeriodRequest>((s, r, ct) => Box(s.FECAEAConsultarAsync(r, ct))),
        ["FECAEARegInformativo"] = Async<FECAEARegInformativoRequest>((s, r, ct) => Box(s.FECAEARegInformativoAsync(r, ct))),
        ["FECAEASinMovimientoInformar"] = Async<FECAEASinMovimientoInformarRequest>((s, r, ct) => Box(s.FECAEASinMovimientoInformarAsync(r, ct))),
        ["FECAEASinMovimientoConsultar"] = Async<FECAEASinMovimientoConsultarRequest>((s, r, ct) => Box(s.FECAEASinMovimientoConsultarAsync(r, ct))),
        ["FEParamGetCotizacion"] = Async<FEParamGetCotizacionRequest>((s, r, ct) => Box(s.FEParamGetCotizacionAsync(r, ct))),
        ["FEParamGetTiposTributos"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposTributos(r)),
        ["FEParamGetTiposMonedas"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposMonedas(r)),
        ["FEParamGetTiposIva"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposIva(r)),
        ["FEParamGetTiposOpcional"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposOpcional(r)),
        ["FEParamGetTiposConcepto"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposConcepto(r)),
        ["FEParamGetPtosVenta"] = Async<AuthOnlyRequest>((s, r, ct) => Box(s.FEParamGetPtosVentaAsync(r, ct))),
        ["FEParamGetTiposCbte"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposCbte(r)),
        ["FEParamGetCondicionIvaReceptor"] = Sync<FEParamGetCondicionIvaReceptorRequest>((s, r) => s.FEParamGetCondicionIvaReceptor(r)),
        ["FEParamGetTiposDoc"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposDoc(r)),
        ["FEParamGetTiposPaises"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetTiposPaises(r)),
        ["FEParamGetActividades"] = Sync<AuthOnlyRequest>((s, r) => s.FEParamGetActividades(r)),
    };

    private static readonly ConcurrentDictionary<(Type, string), XmlSerializer> Serializers = new();

    public static void Map(WebApplication app)
    {
        app.MapGet(Path, (HttpContext context, SimulationSettings settings) =>
            WsdlDocuments.AsksForWsdl(context.Request)
                ? Results.Text(WsdlDocuments.Wsfev1(settings.Environment, WsdlDocuments.BaseUrl(context.Request)), "text/xml; charset=utf-8")
                : Results.Text(HelpPage, "text/html; charset=utf-8"));
        app.MapPost(Path, (HttpContext context, WsfeEndpoint endpoint) => endpoint.HandleAsync(context));
    }

    public async Task HandleAsync(HttpContext context)
    {
        var request = await SoapRequest.ReadAsync(context.Request);
        if (request is null)
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        var chaos = settings.ChaosOf(WsfeService.Name);
        if (await ChaosGate.RefusedAsync(context, chaos)) return;

        try
        {
            request.EnsureWellFormed();
        }
        catch (XmlException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var body = OpenBodyOrNull(request);
        var name = ResolveOperation(request, body, out var faultMessage);
        if (name is null)
        {
            await WriteFaultAsync(context, request, faultMessage!);
            return;
        }

        var operation = Operations[name];
        object input;
        try
        {
            input = body is null
                ? Activator.CreateInstance(operation.Request)!
                : SerializerFor(operation.Request, name).Deserialize(body)!;
        }
        catch (InvalidOperationException ex)
        {
            await WriteFaultAsync(context, request, ReadFailure(ex));
            return;
        }

        object result;
        try
        {
            result = await operation.Invoke(service, input, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            // ASMX answers an exception nobody handled with a fault, never a bare HTTP 500.
            logger.LogError(ex, "WSFEv1 {Operation} failed", name);
            await WriteFaultAsync(context, request, ContractHost.Unexpected(Dialect.Asmx, ex));
            return;
        }

        if (name == "FECAESolicitar" && chaos.TryTakeDropNextResponse())
        {
            // The voucher is already authorized and stored; the client never hears about it.
            context.Abort();
            return;
        }
        await WriteResultAsync(context, request, name, result);
    }

    private static XmlReader? OpenBodyOrNull(SoapRequest request)
    {
        try
        {
            return request.OpenBody();
        }
        catch (SoapBodyEmptyException)
        {
            return null;
        }
    }

    /// <summary>The SOAPAction decides when there is one; without it, the body's element does.</summary>
    private static string? ResolveOperation(SoapRequest request, XmlReader? body, out string? faultMessage)
    {
        faultMessage = null;
        if (!string.IsNullOrEmpty(request.Action))
        {
            var name = request.Action.StartsWith(Fev1.Namespace, StringComparison.Ordinal) ? request.Action[Fev1.Namespace.Length..] : null;
            if (name is not null && Operations.ContainsKey(name)) return name;
            faultMessage = $"System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: {request.Action}.";
            return null;
        }
        if (body is not null && body.NamespaceURI == Fev1.Namespace && Operations.ContainsKey(body.LocalName)) return body.LocalName;
        faultMessage = "System.Web.Services.Protocols.SoapException: Unable to handle request without a valid action parameter. Please supply a valid soap action.";
        return null;
    }

    /// <summary>The fault text ASMX gives when XmlSerializer cannot read the request, in .NET Framework's words.</summary>
    private static string ReadFailure(InvalidOperationException ex)
    {
        var inner = ex.InnerException switch
        {
            FormatException => "System.FormatException: Input string was not in a correct format.",
            OverflowException => "System.OverflowException: Value was either too large or too small for an Int32.",
            { } other => $"{other.GetType().FullName}: {other.Message}",
            null => null,
        };
        var text = $"System.Web.Services.Protocols.SoapException: Server was unable to read request. ---> System.InvalidOperationException: {ex.Message}";
        return inner is null ? text : $"{text} ---> {inner}";
    }

    private async Task WriteResultAsync(HttpContext context, SoapRequest request, string operation, object result)
    {
        var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, WriterSettings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("soap", "Envelope", request.EnvelopeNamespace);
            writer.WriteAttributeString("xmlns", "soap", null, request.EnvelopeNamespace);
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

            writer.WriteStartElement("soap", "Header", request.EnvelopeNamespace);
            writer.WriteStartElement("", "FEHeaderInfo", Fev1.Namespace);
            writer.WriteElementString("ambiente", Fev1.Namespace, settings.Profile.WsfeAmbiente);
            writer.WriteElementString("fecha", Fev1.Namespace,
                clock.Now.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture));
            writer.WriteElementString("id", Fev1.Namespace, settings.Profile.WsfeVersion);
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("soap", "Body", request.EnvelopeNamespace);
            writer.WriteStartElement("", $"{operation}Response", Fev1.Namespace);
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", Fev1.Namespace);
            SerializerFor(result.GetType(), $"{operation}Result").Serialize(writer, result, namespaces);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        await SendAsync(context, request, StatusCodes.Status200OK, buffer);
    }

    /// <summary>ASMX faults carry no FEHeaderInfo and always blame the client, as observed.</summary>
    private static async Task WriteFaultAsync(HttpContext context, SoapRequest request, string message)
    {
        var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, WriterSettings))
        {
            var soap = request.EnvelopeNamespace;
            writer.WriteStartDocument();
            writer.WriteStartElement("soap", "Envelope", soap);
            writer.WriteAttributeString("xmlns", "soap", null, soap);
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
            writer.WriteStartElement("soap", "Body", soap);
            writer.WriteStartElement("soap", "Fault", soap);
            if (request.Version == SoapVersion.Soap12)
            {
                writer.WriteStartElement("soap", "Code", soap);
                writer.WriteElementString("soap", "Value", soap, "soap:Sender");
                writer.WriteEndElement();
                writer.WriteStartElement("soap", "Reason", soap);
                writer.WriteStartElement("soap", "Text", soap);
                writer.WriteAttributeString("xml", "lang", null, "en");
                writer.WriteString(message);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("soap", "Detail", soap);
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteElementString("faultcode", "soap:Client");
                writer.WriteElementString("faultstring", message);
                writer.WriteStartElement("detail");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        await SendAsync(context, request, StatusCodes.Status500InternalServerError, buffer);
    }

    private static async Task SendAsync(HttpContext context, SoapRequest request, int status, MemoryStream buffer)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = request.Version == SoapVersion.Soap12
            ? "application/soap+xml; charset=utf-8"
            : "text/xml; charset=utf-8";
        context.Response.ContentLength = buffer.Length;
        buffer.Position = 0;
        await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = false,
        NewLineHandling = NewLineHandling.None,
    };

    /// <summary>Serializers with a root override are expensive to build and leak if not reused.</summary>
    private static XmlSerializer SerializerFor(Type type, string root) =>
        Serializers.GetOrAdd((type, root), key => new XmlSerializer(key.Item1, new XmlRootAttribute(key.Item2) { Namespace = Fev1.Namespace }));

    private static Operation Sync<TRequest>(Func<WsfeService, TRequest, object> invoke) =>
        new(typeof(TRequest), (s, r, _) => Task.FromResult(invoke(s, (TRequest)r)));

    private static Operation Async<TRequest>(Func<WsfeService, TRequest, CancellationToken, Task<object>> invoke) =>
        new(typeof(TRequest), (s, r, ct) => invoke(s, (TRequest)r, ct));

    private static async Task<object> Box<T>(Task<T> task) where T : class => await task;

    private const string HelpPage =
        "<html><head><title>Service</title></head><body><h1>Service</h1>" +
        "<p>ArcaSim: WSFEv1 para desarrollo y pruebas. No es ARCA y no emite comprobantes válidos.</p>" +
        "<p><a href=\"service.asmx?WSDL\">Descripción del servicio</a></p></body></html>";
}
