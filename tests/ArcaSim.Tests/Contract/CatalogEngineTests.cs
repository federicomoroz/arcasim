using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Contract;

/// <summary>
/// What the catalog can say about a service beyond its WSDL, end to end: the
/// exact rows a refused ticket gets, faults with their own detail and status,
/// headers the WSDL does not declare, events every answer repeats, MTOM, the
/// dummy on GET, the F5 mask, and the answers to the manual's wrong namespaces.
/// </summary>
public class CatalogEngineTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));
    private static readonly string Wsdls = Path.Combine(AppContext.BaseDirectory, "arca-wsdl");

    // ---- Placeholders ---------------------------------------------------------------

    [Fact]
    public void Placeholders_fill_dates_in_Argentina_counters_and_the_ticket_and_leave_other_braces()
    {
        var now = new DateTimeOffset(2026, 10, 2, 18, 13, 20, TimeSpan.Zero);
        var values = new PlaceholderValues(now) { Service = "test-seq", Token = "abc", Sign = "abc", ExpirationTime = 1759443200 };

        Assert.Equal("Vence 02-10-2025 19:13:20 - Ahora 02-10-2026 15:13:20",
            Placeholders.Fill("Vence {exp:dd-MM-yyyy HH:mm:ss} - Ahora {now:dd-MM-yyyy HH:mm:ss}", values));
        Assert.Equal("{token = [abc]firma = [abc]} {abc} {} 2 bytes", Placeholders.Fill("{token = [{token}]firma = [{sign}]} {{token}} {} {signbytes} bytes", values));
        Assert.Equal("{file: [not available]; line: 1} One of '{auth}'", Placeholders.Fill("{file: [not available]; line: 1} One of '{auth}'", values));
        Assert.Equal("1759443200000", Placeholders.Fill("{exp:ms}", values));
        Assert.Equal(["7", "8", "9"], Enumerable.Range(0, 3).Select(_ => Placeholders.Fill("{seq:7}", values)));
        Assert.Matches("^[a-zA-Z]{32}$", Placeholders.Fill("{letters:32}", values));
    }

    [Fact]
    public void Every_catalog_header_and_detail_is_well_formed_and_every_row_names_a_problem()
    {
        var keys = Enum.GetNames<Application.Access.TicketProblem>().Append("NoTicket").Append("*").ToHashSet();
        var values = new PlaceholderValues(DateTimeOffset.Now) { Token = "abc", Sign = "abc" };
        var problems = new List<string>();
        foreach (var service in Catalog.Services)
        {
            var fragments = new List<string?> { service.Header, service.UnknownOperation?.Detail };
            foreach (var (key, rows) in service.Errors.Rows ?? [])
            {
                if (!keys.Contains(key)) problems.Add($"{service.Id}: row {key}");
                fragments.AddRange(rows.Select(r => r.Detail));
            }
            foreach (var fragment in fragments.Where(f => f is { Length: > 0 } && f.StartsWith('<')))
                try
                {
                    XElement.Parse($"<x>{Placeholders.Fill(fragment!, values)}</x>");
                }
                catch (System.Xml.XmlException ex)
                {
                    problems.Add($"{service.Id}: {ex.Message}");
                }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // ---- Sampler --------------------------------------------------------------------

    [Fact]
    public void Several_errors_go_in_copies_of_the_row_and_validate()
    {
        var contract = ServiceContract.Load(Path.Combine(Wsdls, "wsrgiva-homologacion.wsdl"));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Name.StartsWith("consultarConstancia", StringComparison.Ordinal));

        var answer = sampler.ErrorResponse(operation.Output, new SampleContext(Caller, DateTimeOffset.Now),
            [(505, "uno"), (506, "dos")], new ErrorShape("constancia", "codigoError", "descripcionError"))!;

        var rows = answer.Descendants().Where(e => e.Name.LocalName == "constancia").ToList();
        Assert.Equal(["505", "506"], rows.Select(r => r.Elements().First(e => e.Name.LocalName == "codigoError").Value));
        Assert.Equal(["uno", "dos"], rows.Select(r => r.Elements().First(e => e.Name.LocalName == "descripcionError").Value));
        Validate(answer, contract);
    }

    [Fact]
    public void A_parent_and_block_path_picks_the_error_block_at_that_depth()
    {
        var contract = ServiceContract.Load(Path.Combine(Wsdls, "wsagr-homologacion.wsdl"));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Name == "Consulta");

        var byName = sampler.ErrorResponse(operation.Output, new SampleContext(Caller, DateTimeOffset.Now), 501, "x", new ErrorShape("Err"))!;
        var byPath = sampler.ErrorResponse(operation.Output, new SampleContext(Caller, DateTimeOffset.Now), 501, "x", new ErrorShape("Respuesta/Err"))!;

        Assert.Equal("DetalleCuits", byName.Descendants().First(e => e.Name.LocalName == "Err").Parent!.Name.LocalName);
        Assert.Equal("Respuesta", byPath.Descendants().First(e => e.Name.LocalName == "Err").Parent!.Name.LocalName);
        Assert.DoesNotContain(byPath.Descendants(), e => e.Name.LocalName == "Det");
        Validate(byPath, contract);
    }

    [Fact]
    public void Elements_listed_as_always_are_written_with_data_and_with_errors()
    {
        var contract = ServiceContract.Load(Path.Combine(Wsdls, "wsfexv1-homologacion.wsdl"));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Name == "FEXGetPARAM_MON");
        var context = new SampleContext(Caller, DateTimeOffset.Now) { Always = ["FEXEvents"] };

        var data = sampler.Sample(operation.Output, context);
        var error = sampler.ErrorResponse(operation.Output, context, 1000, "x")!;

        Assert.Contains(data.Descendants(), e => e.Name.LocalName == "EventCode");
        Assert.Contains(error.Descendants(), e => e.Name.LocalName == "EventCode");
        Validate(data, contract);
        Validate(error, contract);
    }

    // ---- Refused tickets ------------------------------------------------------------

    [Fact]
    public async Task A_refusal_can_carry_several_errors()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, body, _) = await CallAsync(sim, "wsfecredagente", "obtenerMotivosRechazo", "abc", "abc");

        Assert.Equal(200, status);
        Assert.Contains("<codigo>505</codigo><descripcion>El Token no se corresponde con la Firma. {token = [abc]firma = [abc]}</descripcion>", body);
        Assert.Contains("<codigo>506</codigo><descripcion>El Token no cumple con el Esquema (XSD) de Autenticacion. {abc}</descripcion>", body);
        Assert.Contains("<soap:Header><info xmlns=\"http://headers.springbootws.factu.fisca.afip.gob.ar/xml\"><ambiente>homologacion-externa - FI1</ambiente>", body);
        ValidateBody(body, "wsfecredagente");
    }

    [Fact]
    public async Task A_missing_ticket_and_an_empty_one_are_told_apart()
    {
        await using var sim = ArcaSimHarness.Start();
        var (_, missing, _) = await CallAsync(sim, "ws_sr_padron_a4", "getPersona", null, null);
        var (_, empty, _) = await CallAsync(sim, "ws_sr_padron_a4", "getPersona", "", "");

        Assert.Contains("<faultstring>Falta token y/o sign.</faultstring><detail><ns1:SRValidationException xmlns:ns1=\"http://a4.soap.ws.server.puc.sr/\"/></detail>", missing);
        Assert.Contains("<faultstring>Token malformado</faultstring></soap:Fault>", empty);
    }

    [Fact]
    public async Task A_service_that_answers_in_the_body_can_answer_one_problem_with_a_fault()
    {
        await using var sim = ArcaSimHarness.Start();
        var (noAuth, noAuthBody, _) = await CallAsync(sim, "WGesINV", "ConsultaDespachosPendientes", null, null);
        var (bad, badBody, _) = await CallAsync(sim, "WGesINV", "ConsultaDespachosPendientes", "abc", "abc");

        Assert.Equal(500, noAuth);
        Assert.Contains("<faultcode>soap:Server</faultcode><faultstring>El servidor no puede procesar la solicitud. ---&gt; Referencia a objeto no establecida como instancia de un objeto.</faultstring>", noAuthBody);
        Assert.Equal(200, bad);
        Assert.Contains("<CodErr>7008</CodErr><DesError>El Token no se encuentra en formato base 64.</DesError><DescAdicErr>(SERVER:xxx.xxx.xxx.103) </DescAdicErr>", badBody);
        ValidateBody(badBody, "WGesINV");
    }

    [Fact]
    public async Task ASMX_invoicing_answers_with_FEHeaderInfo_and_the_homologacion_event()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, body, _) = await CallAsync(sim, "wsfexv1", "FEXGetPARAM_MON", "abc", "abc");

        Assert.Equal(200, status);
        Assert.Matches(@"<soap:Header><FEHeaderInfo xmlns=""http://ar.gov.afip.dif.fexv1/""><ambiente>Homologacion-Ext - srext</ambiente><fecha>\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?-03:00</fecha><id>5.0.1.0</id></FEHeaderInfo></soap:Header>", body);
        Assert.Contains("<FEXErr><ErrCode>1000</ErrCode><ErrMsg>Usuario no autorizado a realizar esta operacion. ValidacionDeToken: Error al verificar hash: VerificacionDeHash: Error al convertir de Base64 al token: abc</ErrMsg></FEXErr><FEXEvents><EventCode>103</EventCode>", body);
        ValidateBody(body, "wsfexv1");
    }

    [Fact]
    public async Task Constatacion_answers_events_with_code_0_and_no_message()
    {
        await using var sim = ArcaSimHarness.Start();
        var (_, missing, _) = await CallAsync(sim, "wscdc", "ComprobantesModalidadConsultar", null, null);

        Assert.Contains("<Errors><Err><Code>500</Code><Msg>Campo Auth no fue ingresado o esta mal formado.</Msg></Err></Errors>", missing);
        Assert.Contains("<Events><Evt><Code>0</Code></Evt></Events>", missing);
        ValidateBody(missing, "wscdc");
    }

    [Fact]
    public async Task Axis2_faults_and_the_expired_token_read_as_wsmtxca_writes_them()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, bad, _) = await CallAsync(sim, "wsmtxca", "consultarTiposComprobante", "abc", "abc");
        var (_, missing, _) = await CallAsync(sim, "wsmtxca", "consultarTiposComprobante", null, null);

        Assert.Equal(500, status);
        Assert.Equal("<?xml version='1.0' encoding='utf-8'?><soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault>" +
                     "<faultcode>soapenv:Client</faultcode><faultstring>Token inválido</faultstring><detail /></soapenv:Fault></soapenv:Body></soapenv:Envelope>", bad);
        Assert.Contains("<faultstring>Acceso Denegado  - El token o la firma son nulos.</faultstring>", missing);

        var ticket = await TicketAsync(sim, "wsmtxca");
        sim.Clock.Advance(TimeSpan.FromHours(13));
        var (_, expired, _) = await CallAsync(sim, "wsmtxca", "consultarTiposComprobante", ticket.Token, ticket.Sign);
        Assert.Matches(@"<faultstring>Token vencido Fecha y Hora de Vencimiento del Token Enviado: \d\d-\d\d-\d{4} \d\d:\d\d:\d\d - Fecha y Hora Actual del Servidor: \d\d-\d\d-\d{4} \d\d:\d\d:\d\d</faultstring>", expired);
    }

    [Fact]
    public async Task JAX_WS_faults_carry_the_info_header_and_the_manuals_fault_code()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, body, _) = await CallAsync(sim, "wsct", "consultarMonedas", "abc", "abc");

        Assert.Equal(500, status);
        Assert.Matches(@"^<\?xml version='1.0' encoding='UTF-8'\?><S:Envelope xmlns:S=""http://schemas.xmlsoap.org/soap/envelope/""><S:Header><info xmlns=""https://ar.gob.afip.wsct/CTService/""><ambiente>Producción - FI1</ambiente><fecha>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d</fecha><id>1.6.4</id></info></S:Header>", body);
        Assert.Contains("<faultcode>ns3: Receiver</faultcode>", body);
    }

    [Fact]
    public async Task Upload_refuses_with_a_client_fault_that_travels_with_200_and_its_own_detail()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, body, _) = await CallAsync(sim, "uploadPresentacionService", "consulta", "abc", "abc");

        Assert.Equal(200, status);
        Assert.Matches("<faultcode>soap:Client.contentError</faultcode><faultstring>Acceso denegado. El token y/o la firma es invalido</faultstring>" +
                       @"<detail><server>67</server><url>awshomo.afip.gov.ar</url><faultid>[0-9a-f-]{36}</faultid><datetime>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d</datetime></detail>", body);
    }

    [Fact]
    public async Task A_service_whose_request_names_no_CUIT_acts_for_the_tickets_own()
    {
        await using var sim = ArcaSimHarness.Start();
        var ticket = await TicketAsync(sim, "wEnysa");
        var (status, body, _) = await CallAsync(sim, "wEnysa", "CargaEventoEntradaSalida", ticket.Token, ticket.Sign);

        Assert.Equal(200, status);
        Assert.DoesNotContain("No autorizado para utilizar este servicio", body);
        Assert.DoesNotContain("relaciones", body);
    }

    [Fact]
    public async Task Respuesta_Err_holds_wsagr_ticket_errors()
    {
        await using var sim = ArcaSimHarness.Start();
        var (_, body, _) = await CallAsync(sim, "wsagr", "Consulta", "", "");

        Assert.Contains("<Respuesta><Err><Code>115</Code><Msg>El campo Token no fue ingresado</Msg></Err>", body);
        ValidateBody(body, "wsagr");
    }

    // ---- Transport ------------------------------------------------------------------

    [Fact]
    public async Task The_balancer_mask_is_off_by_default_and_hides_every_fault_when_switched_on()
    {
        await using var sim = ArcaSimHarness.Start();
        var (before, plain, _) = await CallAsync(sim, "wsfecred", "consultarTiposRetenciones", "abc", "abc");
        Assert.Equal(500, before);
        Assert.Contains("Fault", plain);

        var response = await sim.Http.PutAsJsonAsync("/arcasim/api/chaos/wsfecred", new { balancerMask = true });
        response.EnsureSuccessStatusCode();
        var (status, body, contentType) = await CallAsync(sim, "wsfecred", "consultarTiposRetenciones", "abc", "abc");
        var (dummy, dummyBody, _) = await CallAsync(sim, "wsfecred", "dummy", null, null);

        Assert.Equal(200, status);
        Assert.Null(contentType);
        Assert.Matches(@"^BL\d{13} \d{4}-\d\d-\d\d \d\d:\d\d:\d\d 500$", body);
        Assert.Equal(200, dummy);
        Assert.Contains("dummyResponse", dummyBody);
    }

    [Fact]
    public async Task Ventanilla_answers_in_an_MTOM_package_and_refuses_in_plain_SOAP()
    {
        await using var sim = ArcaSimHarness.Start();
        var (status, body, contentType) = await CallAsync(sim, "veconsumerws", "dummy", null, null);
        var (_, fault, faultType) = await CallAsync(sim, "veconsumerws", "consultarEstados", "abc", "abc");

        Assert.Equal(200, status);
        Assert.StartsWith("multipart/related; type=\"application/xop+xml\"; boundary=\"uuid:", contentType);
        Assert.StartsWith("\r\n--uuid:", body);
        Assert.Contains("Content-ID: <root.message@cxf.apache.org>\r\n\r\n<soap:Envelope", body);
        ValidateBody(CatalogServiceTests.Soap(body), "veconsumerws");
        Assert.StartsWith("text/xml", faultType);
        Assert.Contains("No se pudo procesar el SSO Token xml recibido, error [Parsing Error : Content is not allowed in prolog.", fault);
    }

    [Fact]
    public async Task Spring_Boot_services_answer_their_dummy_on_GET()
    {
        await using var sim = ArcaSimHarness.Start();
        var contract = ServiceContract.Load(Path.Combine(Wsdls, Catalog.Find("wsfecredagente")!.Wsdl));

        var body = await sim.Http.GetStringAsync(contract.AddressPath);

        Assert.Contains("<ns2:dummyResponse", body);
        Assert.Contains("<info xmlns=\"http://headers.springbootws.factu.fisca.afip.gob.ar/xml\">", body);
    }

    [Fact]
    public async Task ASMX_dummies_bound_to_HTTP_GET_answer_bare_and_indented()
    {
        await using var sim = ArcaSimHarness.Start();

        var response = await sim.Http.GetAsync("/wsbfev1/service.asmx/BFEDummy");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/xml; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<DummyResponse ", body);
        Assert.Contains("xmlns=\"http://ar.gov.afip.dif.bfev1/\"", body);
        Assert.Contains("\r\n  <AppServer>OK</AppServer>", body);
    }

    [Fact]
    public async Task The_manuals_namespaces_get_the_answers_ARCA_gives_them()
    {
        await using var sim = ArcaSimHarness.Start();
        var sud = ServiceContract.Load(Path.Combine(Wsdls, Catalog.Find("sud_restricciones")!.Wsdl));
        var upload = ServiceContract.Load(Path.Combine(Wsdls, Catalog.Find("uploadPresentacionService")!.Wsdl));

        var (sudStatus, sudBody) = await sim.PostSoapAsync(new Uri("http://localhost" + sud.AddressPath),
            Envelope("<sud:tieneDeudaRequest xmlns:sud=\"http://afip.gob.ar/ws/sud\"/>"), "");
        var (uploadStatus, uploadBody) = await sim.PostSoapAsync(new Uri("http://localhost" + upload.AddressPath),
            Envelope("<dom:dummy xmlns:dom=\"http://domain.presentacion.seti.osiris.arca.gov/\"/>"), "");

        Assert.Equal(404, sudStatus);
        Assert.Equal("", sudBody);
        Assert.Equal(500, uploadStatus);
        Assert.Contains("<faultcode>soap:Server.processError</faultcode><faultstring>Unexpected wrapper element {http://domain.presentacion.seti.osiris.arca.gov/}dummy found.   " +
                        "Expected {http://domain.presentacion.seti.osiris.afip.gov/}dummy.</faultstring><detail><server>67</server>", uploadBody);
    }

    // ---- Helpers --------------------------------------------------------------------

    private static async Task<Arca.Client.AccessTicket> TicketAsync(ArcaSimHarness sim, string id)
    {
        var definition = Catalog.Find(id)!;
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(Caller, "catalogo", definition.Wsaa[0]);
        return await sim.Wsaa(Caller, certificate).LoginAsync(definition.Wsaa[0]);
    }

    /// <summary>
    /// Calls one operation with a request built from its schema: the token and
    /// the sign given (null takes both elements out), the caller's CUIT next to them.
    /// </summary>
    private static async Task<(int Status, string Body, string? ContentType)> CallAsync(
        ArcaSimHarness sim, string id, string operationName, string? token, string? sign)
    {
        var definition = Catalog.Find(id)!;
        var contract = ServiceContract.Load(Path.Combine(Wsdls, definition.Wsdl));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Name == operationName);
        var request = operation.Input is null ? null : sampler.Sample(operation.Input, new SampleContext(Caller, DateTimeOffset.Now));
        if (request?.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("token", StringComparison.OrdinalIgnoreCase)) is { } tokenElement)
        {
            var scope = tokenElement.Parent!;
            foreach (var element in scope.Elements().ToList())
            {
                var name = element.Name.LocalName.ToLowerInvariant();
                if (name is "token" or "sign" or "firma")
                {
                    if (token is null) element.Remove();
                    else element.Value = name == "token" ? token : sign ?? "";
                }
                else if (name.Contains("cuit")) element.Value = Caller.ToString();
            }
        }

        using var content = new StringContent(Envelope(request?.ToString(SaveOptions.DisableFormatting) ?? ""), Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost" + contract.AddressPath)) { Content = content };
        message.Headers.Add("SOAPAction", operation.Action ?? "");
        using var response = await sim.Http.SendAsync(message);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.ToString());
    }

    private static string Envelope(string body) =>
        $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Header/><soapenv:Body>{body}</soapenv:Body></soapenv:Envelope>";

    private static void ValidateBody(string envelope, string id)
    {
        var contract = ServiceContract.Load(Path.Combine(Wsdls, Catalog.Find(id)!.Wsdl));
        var answer = XDocument.Parse(envelope).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Validate(answer, contract);
    }

    private static void Validate(XElement answer, ServiceContract contract)
    {
        var problems = new List<string>();
        new XDocument(new XElement(answer)).Validate(contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + answer);
    }
}
