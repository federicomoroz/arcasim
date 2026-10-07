using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.Mtxca;

/// <summary>
/// WSMTXCA with rules, over HTTP as a client of the manual would call it: CAE
/// with items and numbering, the queries over what was authorized, the CAEA
/// regime and the parameter tables, every answer valid for the WSDL.
/// </summary>
public class MtxcaRulesTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private const long Receiver = 30000000007;
    private const string Namespace = "http://impl.service.wsmtxca.afip.gov.ar/service/";

    private static readonly ServiceContract Contract =
        ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", "wsmtxca-homologacion.wsdl"));

    [Fact]
    public async Task A_voucher_with_items_gets_a_CAE_then_the_last_number_and_the_voucher_come_back()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var answer = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{FacturaA(1)}</comprobanteCAERequest>");

        Assert.Equal("A", answer.Element("resultado")!.Value);
        var response = answer.Element("comprobanteResponse")!;
        var cae = response.Element("CAE")!.Value;
        Assert.Matches("^[0-9]{14}$", cae);
        Assert.Equal("2026-10-01", response.Element("fechaEmision")!.Value);
        Assert.Equal("2026-10-11", response.Element("fechaVencimientoCAE")!.Value);

        var last = await client.CallAsync("consultarUltimoComprobanteAutorizado",
            "<consultaUltimoComprobanteAutorizadoRequest><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta></consultaUltimoComprobanteAutorizadoRequest>");
        Assert.Equal("1", last.Element("numeroComprobante")!.Value);

        var consulted = await client.CallAsync("consultarComprobante",
            "<consultaComprobanteRequest><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>1</numeroComprobante></consultaComprobanteRequest>");
        var voucher = consulted.Element("comprobante")!;
        Assert.Equal("E", voucher.Element("codigoTipoAutorizacion")!.Value);
        Assert.Equal(cae, voucher.Element("codigoAutorizacion")!.Value);
        Assert.Equal("1210.00", voucher.Element("importeTotal")!.Value);
        Assert.Equal("Producto de prueba", voucher.Element("arrayItems")!.Element("item")!.Element("descripcion")!.Value);

        var recorded = await sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Caller, 1, 1, 1);
        Assert.NotNull(recorded);
        Assert.Equal("wsmtxca", recorded.Service);
        Assert.Equal(cae, recorded.Code);
        Assert.Equal(1210m, recorded.Total);
    }

    [Fact]
    public async Task Numbering_goes_on_from_the_last_voucher_and_a_resend_is_rejected_with_102()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);
        await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{FacturaA(1)}</comprobanteCAERequest>");

        var resent = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{FacturaA(1)}</comprobanteCAERequest>");
        Assert.Equal("R", resent.Element("resultado")!.Value);
        Assert.Null(resent.Element("comprobanteResponse"));
        Assert.Equal(["102"], Codes(resent, "arrayErrores"));

        var second = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{FacturaA(2)}</comprobanteCAERequest>");
        Assert.Equal("A", second.Element("resultado")!.Value);
        var last = await client.CallAsync("consultarUltimoComprobanteAutorizado",
            "<consultaUltimoComprobanteAutorizadoRequest><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta></consultaUltimoComprobanteAutorizadoRequest>");
        Assert.Equal("2", last.Element("numeroComprobante")!.Value);
    }

    [Fact]
    public async Task A_VAT_subtotal_that_does_not_match_its_items_is_rejected_with_401_and_the_total_with_115()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var answer = await client.CallAsync("autorizarComprobante",
            $"<comprobanteCAERequest>{FacturaA(1, subtotalIva: "200.00")}</comprobanteCAERequest>");

        Assert.Equal("R", answer.Element("resultado")!.Value);
        Assert.Equal(["115", "401"], Codes(answer, "arrayErrores").Order());
        var error = answer.Element("arrayErrores")!.Elements("codigoDescripcion").Single(e => e.Element("codigo")!.Value == "401");
        Assert.StartsWith("Para comprobantes clase “A” o “A con leyenda OPERACIÓN SUJETA A RETENCIÓN”: Deberá coincidir con la sumatoria",
            error.Element("descripcion")!.Value);

        var last = await client.CallAsync("consultarUltimoComprobanteAutorizado",
            "<consultaUltimoComprobanteAutorizadoRequest><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta></consultaUltimoComprobanteAutorizadoRequest>");
        Assert.Equal(["1502"], Codes(last, "arrayErrores"));
    }

    [Fact]
    public async Task The_relative_margin_of_the_total_is_measured_on_the_total_informed()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);
        string Voucher(string total) =>
            "<codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>1</numeroComprobante>" +
            $"<fechaEmision>2026-10-01</fechaEmision><codigoTipoDocumento>80</codigoTipoDocumento><numeroDocumento>{Receiver}</numeroDocumento><condicionIVAReceptor>1</condicionIVAReceptor>" +
            "<importeGravado>100000.00</importeGravado><importeSubtotal>100000.00</importeSubtotal>" +
            $"<importeTotal>{total}</importeTotal><codigoMoneda>PES</codigoMoneda><cotizacionMoneda>1</cotizacionMoneda><codigoConcepto>1</codigoConcepto>" +
            "<arrayItems><item><unidadesMtx>1</unidadesMtx><codigoMtx>7790001000012</codigoMtx><codigo>P-1</codigo><descripcion>Producto caro</descripcion>" +
            "<cantidad>1</cantidad><codigoUnidadMedida>7</codigoUnidadMedida><precioUnitario>100000</precioUnitario><codigoCondicionIVA>5</codigoCondicionIVA>" +
            "<importeIVA>21000.00</importeIVA><importeItem>121000.00</importeItem></item></arrayItems>" +
            "<arraySubtotalesIVA><subtotalIVA><codigo>5</codigo><importe>21000.00</importe></subtotalIVA></arraySubtotalesIVA>";

        // The items and the VAT add up to 121000.00. 12.10 under it is exactly 0.01 % of the sum but a little over 0.01 % of the 120987.90 informed.
        var under = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{Voucher("120987.90")}</comprobanteCAERequest>");
        var over = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{Voucher("121012.10")}</comprobanteCAERequest>");

        Assert.Equal(["115", "116"], Codes(under, "arrayErrores").Order());
        Assert.Equal("A", over.Element("resultado")!.Value);
    }

    [Fact]
    public async Task Class_A_needs_a_CUIT_and_a_CAE_point_of_sale()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var answer = await client.CallAsync("autorizarComprobante",
            $"<comprobanteCAERequest>{FacturaA(1, pointOfSale: 900, docType: 96)}</comprobanteCAERequest>");

        Assert.Equal("R", answer.Element("resultado")!.Value);
        Assert.Equal(["101", "129"], Codes(answer, "arrayErrores").Order());
    }

    [Fact]
    public async Task The_manuals_gob_ar_namespace_is_taken_and_answered_with_gov_ar()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var answer = await client.CallAsync("autorizarComprobante", $"<comprobanteCAERequest>{FacturaA(1)}</comprobanteCAERequest>",
            "http://impl.service.wsmtxca.afip.gob.ar/service/");

        Assert.Equal(XName.Get("autorizarComprobanteResponse", Namespace), answer.Name);
        Assert.Equal("A", answer.Element("resultado")!.Value);
    }

    [Fact]
    public async Task A_CAEA_is_granted_once_per_fortnight_and_takes_the_vouchers_issued_with_it()
    {
        await using var sim = await StartAsync(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(-3)));
        var client = await MtxcaClient.LoginAsync(sim);

        var granted = (await client.CallAsync("solicitarCAEA", "<solicitudCAEA><periodo>202610</periodo><orden>1</orden></solicitudCAEA>"))
            .Element("CAEAResponse")!;
        var caea = granted.Element("CAEA")!.Value;
        Assert.Equal("2026-10-01", granted.Element("fechaDesde")!.Value);
        Assert.Equal("2026-10-15", granted.Element("fechaHasta")!.Value);
        Assert.Equal("2026-11-15", granted.Element("fechaTopeInforme")!.Value);

        var again = await client.CallAsync("solicitarCAEA", "<solicitudCAEA><periodo>202610</periodo><orden>1</orden></solicitudCAEA>");
        Assert.Equal(["604"], Codes(again, "arrayErrores"));

        var consulted = await client.CallAsync("consultarCAEA", $"<CAEA>{caea}</CAEA>");
        Assert.Equal(caea, consulted.Element("CAEAResponse")!.Element("CAEA")!.Value);
        var between = await client.CallAsync("consultarCAEAEntreFechas", "<fechaDesde>2026-10-10</fechaDesde><fechaHasta>2026-10-20</fechaHasta>");
        Assert.Equal([caea], between.Element("arrayCAEAResponse")!.Elements("CAEAResponse").Select(c => c.Element("CAEA")!.Value));

        var pending = await client.CallAsync("consultarPtosVtaCAEANoInformados", $"<CAEA>{caea}</CAEA>");
        Assert.Equal(["900"], pending.Element("arrayPuntosVenta")!.Elements("puntoVenta").Select(p => p.Element("numeroPuntoVenta")!.Value));

        var informed = await client.CallAsync("informarComprobanteCAEA", $"<comprobanteCAEARequest>{FacturaACaea(1, caea)}</comprobanteCAEARequest>");
        Assert.Equal("A", informed.Element("resultado")!.Value);
        Assert.Equal(caea, informed.Element("comprobanteCAEAResponse")!.Element("CAEA")!.Value);

        pending = await client.CallAsync("consultarPtosVtaCAEANoInformados", $"<CAEA>{caea}</CAEA>");
        Assert.Empty(pending.Element("arrayPuntosVenta")!.Elements());
        var unused = await client.CallAsync("informarCAEANoUtilizado", $"<CAEA>{caea}</CAEA>");
        Assert.Equal("R", unused.Element("resultado")!.Value);
        Assert.Equal(["1202"], Codes(unused, "arrayErrores"));

        var voucher = (await client.CallAsync("consultarComprobante",
            "<consultaComprobanteRequest><codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>900</numeroPuntoVenta><numeroComprobante>1</numeroComprobante></consultaComprobanteRequest>"))
            .Element("comprobante")!;
        Assert.Equal("A", voucher.Element("codigoTipoAutorizacion")!.Value);
        Assert.Equal(caea, voucher.Element("codigoAutorizacion")!.Value);
        Assert.Equal("2026-10-15", voucher.Element("fechaVencimiento")!.Value);
        var recorded = await sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Caller, 900, 1, 1);
        Assert.Equal("CAEA", recorded!.EmissionType);
    }

    [Fact]
    public async Task A_point_of_sale_declared_without_movement_cannot_say_it_twice_and_observes_a_late_voucher()
    {
        await using var sim = await StartAsync(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(-3)));
        var client = await MtxcaClient.LoginAsync(sim);
        var caea = (await client.CallAsync("solicitarCAEA", "<solicitudCAEA><periodo>202610</periodo><orden>1</orden></solicitudCAEA>"))
            .Element("CAEAResponse")!.Element("CAEA")!.Value;

        var first = await client.CallAsync("informarCAEANoUtilizadoPtoVta", $"<CAEA>{caea}</CAEA><numeroPuntoVenta>900</numeroPuntoVenta>");
        Assert.Equal("A", first.Element("resultado")!.Value);
        Assert.Equal("900", first.Element("numeroPuntoVenta")!.Value);

        var second = await client.CallAsync("informarCAEANoUtilizadoPtoVta", $"<CAEA>{caea}</CAEA><numeroPuntoVenta>900</numeroPuntoVenta>");
        Assert.Equal(["1207"], Codes(second, "arrayErrores"));
        var cae = await client.CallAsync("informarCAEANoUtilizadoPtoVta", $"<CAEA>{caea}</CAEA><numeroPuntoVenta>1</numeroPuntoVenta>");
        Assert.Equal(["1204"], Codes(cae, "arrayErrores"));

        var informed = await client.CallAsync("informarComprobanteCAEA", $"<comprobanteCAEARequest>{FacturaACaea(1, caea)}</comprobanteCAEARequest>");
        Assert.Equal("O", informed.Element("resultado")!.Value);
        Assert.Equal(["717"], Codes(informed, "arrayObservaciones"));
    }

    [Fact]
    public async Task CAEA_requests_and_queries_reject_what_the_manual_rejects()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var badOrder = await client.CallAsync("solicitarCAEA", "<solicitudCAEA><periodo>202610</periodo><orden>3</orden></solicitudCAEA>");
        Assert.Equal(["601"], Codes(badOrder, "arrayErrores"));
        var tooLate = await client.CallAsync("solicitarCAEA", "<solicitudCAEA><periodo>202609</periodo><orden>2</orden></solicitudCAEA>");
        Assert.Equal(["602"], Codes(tooLate, "arrayErrores"));
        var unknown = await client.CallAsync("consultarCAEA", "<CAEA>12345678901234</CAEA>");
        Assert.Equal(["1300"], Codes(unknown, "arrayErrores"));
        var reversed = await client.CallAsync("consultarCAEAEntreFechas", "<fechaDesde>2026-10-20</fechaDesde><fechaHasta>2026-10-10</fechaHasta>");
        Assert.Equal(["1400"], Codes(reversed, "arrayErrores"));
        var early = await client.CallAsync("informarComprobanteCAEA", $"<comprobanteCAEARequest>{FacturaACaea(1, "12345678901234")}</comprobanteCAEARequest>");
        Assert.Equal("R", early.Element("resultado")!.Value);
        Assert.Contains("705", Codes(early, "arrayErrores"));
    }

    [Fact]
    public async Task The_parameter_queries_answer_the_documented_tables_and_the_issuers_points_of_sale()
    {
        await using var sim = await StartAsync();
        var client = await MtxcaClient.LoginAsync(sim);

        var types = await client.CallAsync("consultarTiposComprobante", "");
        Assert.Equal(15, Rows(types, "arrayTiposComprobante").Count);
        Assert.Contains(("201", "Factura de Crédito Electrónica MiPyMEs (FCE) A"), Rows(types, "arrayTiposComprobante"));

        var rates = await client.CallAsync("consultarAlicuotasIVA", "");
        Assert.Equal(["3", "4", "5", "6"], Rows(rates, "arrayAlicuotasIVA").Select(r => r.Code));
        var units = await client.CallAsync("consultarUnidadesMedida", "");
        Assert.Contains(("7", "UNIDAD"), Rows(units, "arrayUnidadesMedida"));
        Assert.Contains(("99", "BONIFICACION"), Rows(units, "arrayUnidadesMedida"));
        var documents = await client.CallAsync("consultarTiposDocumento", "");
        Assert.Contains(Rows(documents, "arrayTiposDocumento"), r => r.Code == "80");

        var currencies = await client.CallAsync("consultarMonedas", "");
        Assert.Contains(Rows(currencies, "arrayMonedas"), r => r.Code == "PES");
        var pesos = await client.CallAsync("consultarCotizacionMoneda", "<codigoMoneda>PES</codigoMoneda>");
        Assert.Equal("1", pesos.Element("cotizacionMoneda")!.Value);
        var nonsense = await client.CallAsync("consultarCotizacionMoneda", "<codigoMoneda>XXX</codigoMoneda>");
        Assert.Equal(["1600"], Codes(nonsense, "arrayErrores"));

        var conditions = await client.CallAsync("consultarCondicionesIVAReceptor", "<codigoTipoComprobante>6</codigoTipoComprobante>");
        Assert.Contains(Rows(conditions, "arrayCondicionesIVAReceptor"), r => r.Code == "5");
        Assert.DoesNotContain(Rows(conditions, "arrayCondicionesIVAReceptor"), r => r.Code == "1");

        var points = await client.CallAsync("consultarPuntosVenta", "");
        Assert.Equal(["1", "900"], points.Element("arrayPuntosVenta")!.Elements("puntoVenta").Select(p => p.Element("numeroPuntoVenta")!.Value));
        Assert.All(points.Descendants("bloqueado"), b => Assert.Equal("N", b.Value));
        var caeaPoints = await client.CallAsync("consultarPuntosVentaCAEA", "");
        Assert.Equal(["900"], caeaPoints.Element("arrayPuntosVenta")!.Elements("puntoVenta").Select(p => p.Element("numeroPuntoVenta")!.Value));
    }

    private static async Task<ArcaSimHarness> StartAsync(DateTimeOffset? now = null)
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(now ?? new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(Caller, "Empresa de Prueba SA", VatCondition.ResponsableInscripto,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(900, PointOfSaleKind.WebServiceCaea));
        await sim.PutTaxpayerAsync(Receiver, "Distribuidora del Plata S.A.", VatCondition.ResponsableInscripto);
        return sim;
    }

    /// <summary>A Factura A for two units at 500 plus 21 % VAT, the way the manual's example is written.</summary>
    private static string FacturaA(int number, int pointOfSale = 1, int docType = 80, string subtotalIva = "210.00",
        string authorization = "", string date = "2026-10-01") =>
        $"<codigoTipoComprobante>1</codigoTipoComprobante><numeroPuntoVenta>{pointOfSale}</numeroPuntoVenta><numeroComprobante>{number}</numeroComprobante>" +
        $"<fechaEmision>{date}</fechaEmision>{authorization}" +
        $"<codigoTipoDocumento>{docType}</codigoTipoDocumento><numeroDocumento>{Receiver}</numeroDocumento><condicionIVAReceptor>1</condicionIVAReceptor>" +
        "<importeGravado>1000.00</importeGravado><importeSubtotal>1000.00</importeSubtotal>" +
        "<importeTotal>1210.00</importeTotal><codigoMoneda>PES</codigoMoneda><cotizacionMoneda>1</cotizacionMoneda><codigoConcepto>1</codigoConcepto>" +
        (authorization.Length > 0 ? $"<fechaHoraGen>{date}T09:30:00-03:00</fechaHoraGen>" : "") +
        "<arrayItems><item><unidadesMtx>2</unidadesMtx><codigoMtx>7790001000012</codigoMtx><codigo>P-1</codigo><descripcion>Producto de prueba</descripcion>" +
        "<cantidad>2</cantidad><codigoUnidadMedida>7</codigoUnidadMedida><precioUnitario>500</precioUnitario><codigoCondicionIVA>5</codigoCondicionIVA>" +
        "<importeIVA>210.00</importeIVA><importeItem>1210.00</importeItem></item></arrayItems>" +
        $"<arraySubtotalesIVA><subtotalIVA><codigo>5</codigo><importe>{subtotalIva}</importe></subtotalIVA></arraySubtotalesIVA>";

    private static string FacturaACaea(int number, string caea) => FacturaA(number, pointOfSale: 900, date: "2026-10-02",
        authorization: $"<codigoTipoAutorizacion>A</codigoTipoAutorizacion><codigoAutorizacion>{caea}</codigoAutorizacion><fechaVencimiento>2026-10-15</fechaVencimiento>");

    private static List<string> Codes(XElement answer, string array) =>
        answer.Element(array)?.Elements("codigoDescripcion").Select(c => c.Element("codigo")!.Value).ToList() ?? [];

    private static List<(string Code, string Description)> Rows(XElement answer, string array) =>
        answer.Element(array)!.Elements().Select(r => (r.Element("codigo")!.Value, r.Element("descripcion")!.Value)).ToList();

    /// <summary>Calls wsmtxca with a real ticket and returns the answer after checking it against the WSDL.</summary>
    private sealed class MtxcaClient(ArcaSimHarness sim, string auth)
    {
        public static async Task<MtxcaClient> LoginAsync(ArcaSimHarness sim)
        {
            var certificate = await sim.IssueCertificateAsync(Caller, "mtxca", "wsmtxca");
            var ticket = await sim.Wsaa(Caller, certificate).LoginAsync("wsmtxca");
            return new MtxcaClient(sim, $"<authRequest><token>{ticket.Token}</token><sign>{ticket.Sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada></authRequest>");
        }

        public async Task<XElement> CallAsync(string operation, string inner, string ns = Namespace)
        {
            var envelope = $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ser=\"{ns}\">" +
                           $"<soapenv:Header/><soapenv:Body><ser:{operation}Request>{auth}{inner}</ser:{operation}Request></soapenv:Body></soapenv:Envelope>";
            var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + Contract.AddressPath), envelope, Namespace + operation);
            Assert.True(status == 200, body);
            var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
            Assert.Equal(XName.Get(operation + "Response", Namespace), answer.Name);

            var problems = new List<string>();
            new XDocument(answer).Validate(Contract.Schemas, (_, e) =>
            {
                if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
            });
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + body);
            return answer;
        }
    }
}
