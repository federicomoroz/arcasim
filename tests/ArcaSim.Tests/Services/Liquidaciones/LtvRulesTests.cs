using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>WSLTV: green tobacco liquidations by romaneo and bale, their credit, debit and physical adjustments, and the queries.</summary>
public class LtvRulesTests
{
    private static Task<LiquidacionesSim> StartAsync() => LiquidacionesSim.StartAsync("wsltv", "wsltv-homologacion.wsdl");

    private const string Last = "<solicitud><puntoVenta>1</puntoVenta><tipoComprobante>150</tipoComprobante></solicitud>";

    private static string Voucher(long number, string name = "comprobante") =>
        $"<{name}><tipoComprobante>150</tipoComprobante><puntoVenta>1</puntoVenta><nroComprobante>{number}</nroComprobante></{name}>";

    /// <summary>Three bales, two of class 1 (95 kg at 100) and one of class 2 (40 kg at 80), A with 21% VAT, minus a retention of 100: 15267.</summary>
    private static string Liquidation(long number, string bales = "A", int type = 150, long seller = Producer) =>
        "<solicitud><liquidacion>" +
        $"<tipoComprobante>{type}</tipoComprobante><nroComprobante>{number}</nroComprobante><puntoVenta>1</puntoVenta>" +
        $"<codDepositoAcopio>1</codDepositoAcopio><fechaLiquidacion>{Day()}</fechaLiquidacion><tipoCompra>CPS</tipoCompra>" +
        "<condicionVenta><codigo>1</codigo></condicionVenta><variedadTabaco>BR</variedadTabaco><codProvinciaOrigenTabaco>10</codProvinciaOrigenTabaco>" +
        "<fechaInicioActividad>2010-01-01</fechaInicioActividad></liquidacion>" +
        $"<receptor><cuit>{seller}</cuit></receptor>" +
        $"<romaneo><nroRomaneo>1001</nroRomaneo><fechaRomaneo>{Day(-1)}</fechaRomaneo>" +
        $"<fardo><codTrazabilidad>{bales}-1</codTrazabilidad><claseTabaco>1</claseTabaco><peso>50</peso></fardo>" +
        $"<fardo><codTrazabilidad>{bales}-2</codTrazabilidad><claseTabaco>1</claseTabaco><peso>45</peso></fardo>" +
        $"<fardo><codTrazabilidad>{bales}-3</codTrazabilidad><claseTabaco>2</claseTabaco><peso>40</peso></fardo></romaneo>" +
        "<precioClase><claseTabaco>1</claseTabaco><precio>100.00</precio></precioClase>" +
        "<precioClase><claseTabaco>2</claseTabaco><precio>80.00</precio></precioClase>" +
        "<retencion><codRetencion>1</codRetencion><importe>100.00</importe></retencion></solicitud>";

    private static string Adjustment(long number, string kind, long seller = Producer, params long[] targets) =>
        "<solicitud><liquidacionAjuste>" +
        $"<tipoComprobante>150</tipoComprobante><nroComprobante>{number}</nroComprobante><fechaAjusteLiquidacion>{Day()}</fechaAjusteLiquidacion>" +
        $"<puntoVenta>1</puntoVenta><codDepositoAcopio>1</codDepositoAcopio><tipoAjuste>{kind}</tipoAjuste>" +
        string.Concat(targets.Select(t => Voucher(t, "comprobanteAAjustar"))) +
        $"<cuitReceptor>{seller}</cuitReceptor><fechaInicioActividad>2010-01-01</fechaInicioActividad></liquidacionAjuste>" +
        "<precioClase><claseTabaco>1</claseTabaco><totalKilos>95</totalKilos><totalFardos>2</totalFardos><precio>10.00</precio></precioClase></solicitud>";

    private static string Physical(long number, long target) =>
        $"<solicitud><tipoComprobante>150</tipoComprobante><puntoVenta>1</puntoVenta><nroComprobante>{number}</nroComprobante>" +
        $"<fechaLiquidacion>{Day()}</fechaLiquidacion><fechaInicioActividad>2010-01-01</fechaInicioActividad>{Voucher(target, "comprobanteAAjustar")}</solicitud>";

    [Fact]
    public async Task A_liquidation_gets_a_CAE_and_reads_back_by_number_and_by_CAE()
    {
        await using var ltv = await StartAsync();

        Assert.Null((await ltv.CallAsync("consultarUltimoComprobanteXPuntoVenta", Last)).Element("nroComprobante"));

        var issued = (await ltv.CallAsync("generarLiquidacion", Liquidation(1))).Element("liquidacion")!;
        var cae = issued.Element("cabecera")!.Element("cae")!.Value;
        Assert.Equal(14, cae.Length);
        Assert.Equal("15267.00", issued.Element("totalesOperacion")!.Element("total")!.Value);
        Assert.Equal("3", issued.Element("detalleOperacion")!.Element("cantidadTotalFardos")!.Value);
        Assert.Equal(2, issued.Element("detalleOperacion")!.Element("romaneo")!.Elements("detalleClase").Count());
        Assert.StartsWith("JVBERi0xLjQK", issued.Element("pdf")!.Value);

        Assert.Equal("1", (await ltv.CallAsync("consultarUltimoComprobanteXPuntoVenta", Last)).Element("nroComprobante")!.Value);
        var byNumber = (await ltv.CallAsync("consultarLiquidacionXNroComprobante",
            "<solicitud><puntoVenta>1</puntoVenta><tipoComprobante>150</tipoComprobante><nroComprobante>1</nroComprobante><pdf>false</pdf></solicitud>")).Element("liquidacion")!;
        Assert.Equal(cae, byNumber.Element("cabecera")!.Element("cae")!.Value);
        Assert.Null(byNumber.Element("pdf"));
        var byCae = (await ltv.CallAsync("consultarLiquidacionXCAE", $"<solicitud><cae>{cae}</cae><pdf>true</pdf></solicitud>")).Element("liquidacion")!;
        Assert.NotNull(byCae.Element("pdf"));

        Assert.NotNull(await ltv.Sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Issuer, 1, 150, 1));
    }

    [Fact]
    public async Task Bales_numbers_and_the_sellers_class_are_checked()
    {
        await using var ltv = await StartAsync();
        await ltv.CallAsync("generarLiquidacion", Liquidation(1));

        Assert.Equal(["1039"], Errors(await ltv.CallAsync("generarLiquidacion", Liquidation(2))));
        Assert.Equal(["1071"], Errors(await ltv.CallAsync("generarLiquidacion", Liquidation(3, "B"))));
        Assert.Equal(["1014"], Errors(await ltv.CallAsync("generarLiquidacion", Liquidation(1, "B", type: 151))));
        Assert.Equal(["1015"], Errors(await ltv.CallAsync("generarLiquidacion", Liquidation(2, "B", seller: Monotributista))));
        Assert.Empty(Errors(await ltv.CallAsync("generarLiquidacion", Liquidation(2, "B"))));
    }

    [Fact]
    public async Task Adjustments_add_up_the_classes_and_a_physical_one_happens_once()
    {
        await using var ltv = await StartAsync();
        var cae = (await ltv.CallAsync("generarLiquidacion", Liquidation(1))).Descendants("cae").First().Value;

        var totals = await ltv.CallAsync("consultarTotalesDeClasesPorComprobantesParaAjustar", $"<solicitud>{Voucher(1)}</solicitud>");
        var first = totals.Elements("detalleTotalClase").Single(c => c.Element("codClase")!.Value == "1");
        Assert.Equal("2", first.Element("totalFardos")!.Value);
        Assert.Equal("95", first.Element("totalKilos")!.Value);

        Assert.Equal(["1118"], Errors(await ltv.CallAsync("ajustarLiquidacion", Adjustment(2, "D", Monotributista, 1))));
        Assert.Equal(["1120"], Errors(await ltv.CallAsync("ajustarLiquidacion", Adjustment(2, "D", Producer, 9))));
        var debit = (await ltv.CallAsync("ajustarLiquidacion", Adjustment(2, "D", Producer, 1))).Element("liquidacion")!;
        Assert.Equal("D", debit.Element("cabecera")!.Element("tipoAjuste")!.Value);
        Assert.Equal("1149.50", debit.Element("totalesOperacion")!.Element("total")!.Value);
        Assert.Equal(cae, debit.Element("caeAjustado")!.Value);
        Assert.Equal(["1127"], Errors(await ltv.CallAsync("consultarTotalesDeClasesPorComprobantesParaAjustar", $"<solicitud>{Voucher(2)}</solicitud>")));

        var physical = (await ltv.CallAsync("generarAjusteFisico", Physical(3, 1))).Element("liquidacion")!;
        Assert.Equal("F", physical.Element("cabecera")!.Element("tipoAjuste")!.Value);
        Assert.Equal(2, physical.Element("detalleOperacion")!.Elements("claseAjuste").Count());
        Assert.Equal(["1135"], Errors(await ltv.CallAsync("generarAjusteFisico", Physical(4, 1))));
        Assert.Equal("3", (await ltv.CallAsync("consultarUltimoComprobanteXPuntoVenta", Last)).Element("nroComprobante")!.Value);
    }
}
