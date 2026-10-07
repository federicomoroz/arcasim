using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>Comprobantes T: hotel invoices with their VAT refund, numbered, stored and queried back, valid for wsct's WSDL.</summary>
public class WsctRulesTests
{
    private static Task<ServiceDesk> OpenAsync(ArcaSimHarness sim) => ServiceDesk.OpenAsync(sim, "wsct-homologacion.wsdl", "wsct", "cts");

    private static string Auth(ServiceDesk desk) =>
        $"<authRequest><token>{desk.Token}</token><sign>{desk.Sign}</sign><cuitRepresentada>{ServiceDesk.Issuer}</cuitRepresentada></authRequest>";

    /// <summary>A night at the hotel for a foreign tourist with a passport: 121 with 21 of VAT, refunded, plus a municipal tax of 1.</summary>
    private static string HotelInvoice(long number, string extra = "", string refund = "<importeReintegro>-21.00</importeReintegro>", int type = 195,
        string total = "101.00", string items = "<item><tipo>0</tipo><codigoTurismo>1</codigoTurismo><descripcion>Habitacion doble</descripcion><codigoAlicuotaIVA>5</codigoAlicuotaIVA><importeIVA>21.00</importeIVA><importeItem>121.00</importeItem></item>",
        string vat = "21.00", string taxed = "100.00") =>
        "<comprobanteRequest>" +
        $"<codigoTipoComprobante>{type}</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>{number}</numeroComprobante>" +
        "<fechaEmision>2026-10-01</fechaEmision><codigoTipoAutorizacion>E</codigoTipoAutorizacion>" +
        "<codigoTipoDocumento>94</codigoTipoDocumento><numeroDocumento>AB123456</numeroDocumento><idImpositivo>9</idImpositivo>" +
        "<codigoPais>203</codigoPais><domicilioReceptor>Rua N.76 km 34.5 Alagoas</domicilioReceptor><codigoRelacionEmisorReceptor>1</codigoRelacionEmisorReceptor>" +
        $"<importeGravado>{taxed}</importeGravado><importeOtrosTributos>1.00</importeOtrosTributos>{refund}<importeTotal>{total}</importeTotal>" +
        "<codigoMoneda>PES</codigoMoneda><cotizacionMoneda>1</cotizacionMoneda>" +
        $"<arrayItems>{items}</arrayItems>{extra}" +
        "<arrayOtrosTributos><otroTributo><codigo>99</codigo><descripcion>Tasa municipal</descripcion><baseImponible>100.00</baseImponible><importe>1.00</importe></otroTributo></arrayOtrosTributos>" +
        $"<arraySubtotalesIVA><subtotalIVA><codigo>5</codigo><importe>{vat}</importe></subtotalIVA></arraySubtotalesIVA>" +
        "<arrayFormasPago><formaPago><codigo>2</codigo><tipoTarjeta>1</tipoTarjeta><numeroTarjeta>450000</numeroTarjeta></formaPago></arrayFormasPago>" +
        "</comprobanteRequest>";

    private static Task<XElement> AuthorizeAsync(ServiceDesk desk, string comprobante) => desk.CallAsync("autorizarComprobante", Auth(desk) + comprobante);

    private static IEnumerable<string> Codes(XElement answer, string array = "arrayErrores") =>
        answer.Descendants(array).Elements("codigoDescripcion").Select(e => e.Element("codigo")!.Value);

    [Fact]
    public async Task An_invoice_gets_a_CAE_becomes_the_last_number_and_is_answered_back_with_it()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var authorized = await AuthorizeAsync(desk, HotelInvoice(1));
        var response = authorized.Descendants("comprobanteResponse").Single();
        Assert.Equal("A", authorized.Descendants("resultado").Single().Value);
        Assert.Matches("^[1-9][0-9]{13}$", response.Element("CAE")!.Value);
        Assert.Equal("2026-10-11", response.Element("fechaVencimientoCAE")!.Value);
        Assert.Equal(ServiceDesk.Issuer.ToString(), response.Element("cuit")!.Value);

        var last = await desk.CallAsync("consultarUltimoComprobanteAutorizado",
            Auth(desk) + "<codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta>");
        Assert.Equal("1", last.Descendants("numeroComprobante").Single().Value);
        Assert.Equal("2026-10-01", last.Descendants("fechaEmision").Single().Value);

        var consulted = await desk.CallAsync("consultarComprobanteTipoPVentaNro",
            Auth(desk) + "<codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>1</numeroComprobante>");
        var comprobante = consulted.Descendants("comprobante").Single();
        Assert.Equal(response.Element("CAE")!.Value, comprobante.Element("codigoAutorizacion")!.Value);
        Assert.Equal("E", comprobante.Element("codigoTipoAutorizacion")!.Value);
        Assert.Equal("-21.00", comprobante.Element("importeReintegro")!.Value);
        Assert.Equal("Habitacion doble", comprobante.Descendants("descripcion").First().Value);

        var recorded = await sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(ServiceDesk.Issuer, 1, 195, 1);
        Assert.Equal(101m, recorded!.Total);
        Assert.Equal("wsct", recorded.Service);
    }

    [Fact]
    public async Task The_last_number_of_a_sequence_without_vouchers_is_1002_not_zero()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var last = await desk.CallAsync("consultarUltimoComprobanteAutorizado",
            Auth(desk) + "<codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta>");

        Assert.Equal(["1002"], Codes(last));
        Assert.Empty(last.Descendants("numeroComprobante"));
    }

    [Fact]
    public async Task A_hotel_item_without_its_refund_is_rejected_with_every_error_and_takes_no_number()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var rejected = await AuthorizeAsync(desk, HotelInvoice(2, refund: "", total: "122.00"));

        Assert.Equal("R", rejected.Descendants("resultado").Single().Value);
        Assert.Empty(rejected.Descendants("comprobanteResponse"));
        Assert.Equal(["364", "302"], Codes(rejected));
        Assert.Equal("A", (await AuthorizeAsync(desk, HotelInvoice(1))).Descendants("resultado").Single().Value);
    }

    [Fact]
    public async Task The_relative_margin_of_the_total_is_measured_on_the_total_informed_as_the_manual_defines_it()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        const string longStay = "<item><tipo>0</tipo><codigoTurismo>1</codigoTurismo><descripcion>Estadia larga</descripcion><codigoAlicuotaIVA>5</codigoAlicuotaIVA><importeIVA>21000.00</importeIVA><importeItem>121000.00</importeItem></item>";
        string Invoice(string total) =>
            HotelInvoice(1, refund: "<importeReintegro>-21000.00</importeReintegro>", total: total, items: longStay, vat: "21000.00", taxed: "100000.00");

        // The amounts add up to 100001.00. 10.00 under it is 0.01 % of the sum but over 0.01 % of the 99991.00 informed (wsct.md, Aritmética: error relativo = error absoluto / |real|).
        var under = await AuthorizeAsync(desk, Invoice("99991.00"));
        var over = await AuthorizeAsync(desk, Invoice("100011.00"));

        Assert.Equal(["369"], Codes(under));
        Assert.Equal("A", over.Descendants("resultado").Single().Value);
    }

    [Fact]
    public async Task A_voucher_sent_again_is_refused_as_out_of_sequence()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        await AuthorizeAsync(desk, HotelInvoice(1));

        var again = await AuthorizeAsync(desk, HotelInvoice(1));

        Assert.Equal(["302"], Codes(again));
    }

    [Fact]
    public async Task A_credit_note_needs_its_invoice_and_is_observed_when_it_exceeds_it()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        await AuthorizeAsync(desk, HotelInvoice(1));
        const string invoice = "<arrayComprobantesAsociados><comprobanteAsociado><codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>1</numeroComprobante></comprobanteAsociado></arrayComprobantesAsociados>";
        const string twoNights = "<item><tipo>0</tipo><codigoTurismo>1</codigoTurismo><descripcion>Dos noches</descripcion><codigoAlicuotaIVA>5</codigoAlicuotaIVA><importeIVA>42.00</importeIVA><importeItem>242.00</importeItem></item>";

        var orphan = await AuthorizeAsync(desk, HotelInvoice(1, type: 197));
        var exceeding = await AuthorizeAsync(desk, HotelInvoice(1, invoice, "<importeReintegro>-42.00</importeReintegro>", 197, "201.00", twoNights, "42.00", "200.00"));

        Assert.Contains("800", Codes(orphan));
        Assert.Equal("O", exceeding.Descendants("resultado").Single().Value);
        Assert.Equal(["807"], Codes(exceeding, "arrayObservaciones"));
        var consulted = await desk.CallAsync("consultarComprobanteTipoPVentaNro",
            Auth(desk) + "<codigoTipoComprobante>197</codigoTipoComprobante><numeroPuntoVenta>1</numeroPuntoVenta><numeroComprobante>1</numeroComprobante>");
        Assert.Equal(["807"], Codes(consulted, "arrayObservaciones"));
    }

    [Fact]
    public async Task In_production_a_point_of_sale_still_to_be_deactivated_can_issue_and_one_already_deactivated_cannot()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        sim.Settings.Environment = ArcaEnvironment.Produccion;
        await desk.PutPointOfSaleAsync(1, new DateOnly(2026, 12, 31));
        await desk.PutPointOfSaleAsync(2, new DateOnly(2026, 9, 30));
        string Last(int point) => Auth(desk) + $"<codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>{point}</numeroPuntoVenta>";

        var coming = await desk.CallAsync("consultarUltimoComprobanteAutorizado", Last(1));
        var gone = await desk.CallAsync("consultarUltimoComprobanteAutorizado", Last(2));

        Assert.Equal(["1002"], Codes(coming));
        Assert.Equal(["1001"], Codes(gone));
    }

    [Fact]
    public async Task A_number_below_one_gets_the_schema_validators_format_errors()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var answer = await desk.CallAsync("consultarComprobanteTipoPVentaNro",
            Auth(desk) + "<codigoTipoComprobante>195</codigoTipoComprobante><numeroPuntoVenta>4000</numeroPuntoVenta><numeroComprobante>0</numeroComprobante>");

        var codes = answer.Descendants("codigoDescripcionString").Select(e => e.Element("codigo")!.Value);
        Assert.Equal(["cvc-minInclusive-valid", "cvc-type.3.1.3"], codes);
        Assert.Empty(answer.Descendants("arrayErrores"));
    }

    [Fact]
    public async Task The_parameter_queries_answer_the_documented_values()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        await desk.SetRateAsync("DOL", new DateOnly(2026, 9, 30), 1450.5m);

        var types = await desk.CallAsync("consultarTiposComprobantes", Auth(desk));
        var relations = await desk.CallAsync("consultarRelacionEmisorReceptor", Auth(desk));
        var cards = await desk.CallAsync("consultarTiposTarjeta", Auth(desk) + "<formaPago>3</formaPago>");
        var rate = await desk.CallAsync("consultarCotizacion", Auth(desk) + "<codigoMoneda>DOL</codigoMoneda><fechaCotizacion>2026-10-01</fechaCotizacion>");
        var news = await desk.CallAsync("consultarNovedades", Auth(desk));

        Assert.Equal(["195", "196", "197"], Codes(types, "arrayTiposComprobantes"));
        Assert.Equal(6, relations.Descendants("codigoDescripcion").Count());
        Assert.Equal(["1200"], Codes(cards));
        Assert.Equal("1450.5", rate.Descendants("cotizacionMoneda").Single().Value);
        Assert.Empty(news.Elements().Single().Elements());
    }
}
