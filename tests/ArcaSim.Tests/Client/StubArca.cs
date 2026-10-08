using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Xml.Linq;

namespace ArcaSim.Tests.Client;

/// <summary>What WSAA was asked: the service in the TRA the client signed, and which login it is (1 for the first).</summary>
internal sealed record LoginCall(string Service, int Number);

/// <summary>What WSFEv1 was asked: the operation, its element, and the token of its Auth (null when it has none).</summary>
internal sealed record WsfeCall(string Operation, XElement Request, string? Token);

/// <summary>
/// WSAA and WSFEv1 as far as Arca.Client can tell, answered in memory. Every request goes to a
/// delegate a test can replace, which by default answers the way ARCA does when all is well. Nothing
/// is checked in what the client sent but the service in the TRA and the operation called: this is
/// about how the client reads answers, not about what ArcaSim does with requests.
/// </summary>
internal sealed class StubArca(FakeTime time) : HttpMessageHandler
{
    public const string FevNamespace = "http://ar.gov.afip.dif.FEV1/";
    private const string WsaaNamespace = "http://wsaa.view.sua.dvadac.desein.afip.gov";

    private readonly ConcurrentQueue<string> _wsfeRequests = new();
    private int _logins;

    public static Uri WsaaUrl { get; } = new("http://localhost:7080/ws/services/LoginCms");

    public static Uri WsfeUrl { get; } = new("http://localhost:7080/wsfev1/service.asmx");

    public Func<LoginCall, Task<HttpResponseMessage>>? OnLogin { get; set; }

    public Func<WsfeCall, Task<HttpResponseMessage>>? OnWsfe { get; set; }

    /// <summary>The number of the last voucher the default FECompUltimoAutorizado answer reports.</summary>
    public long LastVoucher { get; set; }

    /// <summary>How many times WSAA has been asked for a ticket.</summary>
    public int Logins => Volatile.Read(ref _logins);

    /// <summary>The calls WSFEv1 has received, in order, as "Operation:token" (the token is empty when the call carried none).</summary>
    public IReadOnlyList<string> WsfeRequests => _wsfeRequests.ToArray();

    /// <summary>The credentials the default WSAA answer gives for the nth login of a service.</summary>
    public static (string Token, string Sign) CredentialsOf(string service, int number) => ($"token-{service}-{number}", $"sign-{service}-{number}");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var document = XDocument.Parse(request.Content is null ? "<none/>" : await request.Content.ReadAsStringAsync(ct));
        if (request.RequestUri!.AbsolutePath.EndsWith("LoginCms", StringComparison.Ordinal))
        {
            var cms = new SignedCms();
            cms.Decode(Convert.FromBase64String(document.Descendants().First(e => e.Name.LocalName == "in0").Value));
            var service = XDocument.Parse(Encoding.UTF8.GetString(cms.ContentInfo.Content)).Root!.Element("service")!.Value;
            var login = new LoginCall(service, Interlocked.Increment(ref _logins));
            return await (OnLogin?.Invoke(login) ?? Task.FromResult(LoginAnswer(login.Service, login.Number)));
        }

        var operation = document.Descendants().First(e => e.Name.LocalName == "Body").Elements().First();
        var token = operation.Element(operation.Name.Namespace + "Auth")?.Element(operation.Name.Namespace + "Token")?.Value;
        _wsfeRequests.Enqueue($"{operation.Name.LocalName}:{token}");
        var call = new WsfeCall(operation.Name.LocalName, operation, token);
        return await (OnWsfe?.Invoke(call) ?? Task.FromResult(HappyWsfe(call)));
    }

    private HttpResponseMessage HappyWsfe(WsfeCall call) => Soap(call.Operation switch
    {
        "FEDummy" => Result("FEDummy", "<AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer>"),
        "FECompUltimoAutorizado" => Result("FECompUltimoAutorizado", $"<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>{LastVoucher}</CbteNro>"),
        "FECAESolicitar" => Result("FECAESolicitar", Approved(call.Request)),
        "FECompConsultar" => Result("FECompConsultar", Authorized(LastVoucher + 1, 121m)),
        _ => throw new InvalidOperationException($"The stub has no default answer for {call.Operation}."),
    });

    // ---- WSAA ----------------------------------------------------------------------------------

    /// <summary>A WSAA answer with a ticket for the service that expires twelve hours from the stub's clock.</summary>
    public HttpResponseMessage LoginAnswer(string service, int number)
    {
        var (token, sign) = CredentialsOf(service, number);
        return LoginAnswer(Ticket(token, sign, time.GetUtcNow(), time.GetUtcNow().AddHours(12)));
    }

    /// <summary>A WSAA answer around this loginCmsReturn text: a ticket's XML, or whatever a test wants the client to choke on.</summary>
    public static HttpResponseMessage LoginAnswer(string loginCmsReturn) =>
        Soap(new XElement(XName.Get("loginCmsResponse", WsaaNamespace), new XElement(XName.Get("loginCmsReturn", WsaaNamespace), loginCmsReturn)).ToString(SaveOptions.DisableFormatting));

    public static string Ticket(string token, string sign, DateTimeOffset generated, DateTimeOffset expires) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><loginTicketResponse version=\"1\"><header>" +
        "<source>CN=wsaahomo, O=AFIP, C=AR</source><destination>SERIALNUMBER=CUIT 20111111112, CN=tests</destination><uniqueId>1</uniqueId>" +
        $"<generationTime>{Stamp(generated)}</generationTime><expirationTime>{Stamp(expires)}</expirationTime></header>" +
        $"<credentials><token>{token}</token><sign>{sign}</sign></credentials></loginTicketResponse>";

    /// <summary>A moment as WSAA writes it in a ticket: Argentina's offset and milliseconds.</summary>
    public static string Stamp(DateTimeOffset moment) =>
        moment.ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    // ---- WSFEv1 --------------------------------------------------------------------------------

    /// <summary>The element of a WSFEv1 answer: an opResponse around an opResult that holds <paramref name="inner"/>.</summary>
    public static string Result(string operation, string inner) =>
        $"<{operation}Response xmlns=\"{FevNamespace}\"><{operation}Result>{inner}</{operation}Result></{operation}Response>";

    public static string Error(int code, string message) => $"<Errors><Err><Code>{code}</Code><Msg>{message}</Msg></Err></Errors>";

    /// <summary>The answer WSFEv1 gives a ticket whose dates do not hold: the client drops the ticket and asks again.</summary>
    public static HttpResponseMessage StaleTicket(string operation) =>
        Soap(Result(operation, Error(600, "ValidacionDeToken: No validaron las fechas del token. GenTime=1, ExpTime=2, NowUTC=3")));

    /// <summary>The result of an approved FECAESolicitar for the voucher the request carries.</summary>
    public static string Approved(XElement request)
    {
        var number = request.Descendants().First(e => e.Name.LocalName == "CbteDesde").Value;
        return "<FeCabResp><Cuit>20111111112</Cuit><PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><FchProceso>20261001120000</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp>" +
               $"<FeDetResp><FECAEDetResponse><Concepto>1</Concepto><DocTipo>99</DocTipo><DocNro>0</DocNro><CbteDesde>{number}</CbteDesde><CbteHasta>{number}</CbteHasta><CbteFch>20261001</CbteFch><Resultado>A</Resultado><CAE>71263951827464</CAE><CAEFchVto>20261011</CAEFchVto></FECAEDetResponse></FeDetResp>";
    }

    /// <summary>The result of FECompConsultar for an authorized voucher.</summary>
    public static string Authorized(long number, decimal total) =>
        $"<ResultGet><Concepto>1</Concepto><DocTipo>99</DocTipo><DocNro>0</DocNro><CbteDesde>{number}</CbteDesde><CbteHasta>{number}</CbteHasta><CbteFch>20261001</CbteFch>" +
        $"<ImpTotal>{total.ToString(CultureInfo.InvariantCulture)}</ImpTotal><ImpTotConc>0</ImpTotConc><ImpNeto>100</ImpNeto><ImpOpEx>0</ImpOpEx><ImpTrib>0</ImpTrib><ImpIVA>21</ImpIVA>" +
        "<MonId>PES</MonId><MonCotiz>1</MonCotiz><Resultado>A</Resultado><CodAutorizacion>71263951827464</CodAutorizacion><EmisionTipo>CAE</EmisionTipo><FchVto>20261011</FchVto>" +
        "<FchProceso>20261001120000</FchProceso><PtoVta>1</PtoVta><CbteTipo>6</CbteTipo></ResultGet>";

    // ---- HTTP ----------------------------------------------------------------------------------

    /// <summary>An HTTP answer with a SOAP envelope around the element (200 unless a status is given).</summary>
    public static HttpResponseMessage Soap(string bodyElement, HttpStatusCode status = HttpStatusCode.OK) =>
        Text(status, $"<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>{bodyElement}</soap:Body></soap:Envelope>", "text/xml");

    /// <summary>A SOAP 1.1 fault with this text, the way ASMX sends one: with HTTP 500.</summary>
    public static HttpResponseMessage Fault(string faultString, string faultCode = "soap:Server") =>
        Soap($"<soap:Fault><faultcode>{faultCode}</faultcode><faultstring>{faultString}</faultstring></soap:Fault>", HttpStatusCode.InternalServerError);

    public static HttpResponseMessage Text(HttpStatusCode status, string body, string contentType) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
}
