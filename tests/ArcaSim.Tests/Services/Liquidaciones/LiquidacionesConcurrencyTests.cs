using System.Xml.Linq;
using ArcaSim.Domain;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>
/// Clients that call at once. What a liquidation must be unique in (a bale, a
/// delivery note, a period, a CATHE) and what may happen to a voucher only once
/// (annulled, adjusted) is decided one request at a time: of several that try
/// the same thing, exactly one takes it. Each caller uses a point of sale of its
/// own, so the numbering does not tell them apart, only the thing they share.
/// </summary>
public class LiquidacionesConcurrencyTests
{
    private const int Callers = 16;

    /// <summary>The point of sale the original vouchers go out on; the callers use 1 to <see cref="Callers"/>, one each.</summary>
    private const int OriginalPoint = Callers + 1;

    private static readonly PointOfSale[] Points = [.. Enumerable.Range(1, Callers + 1).Select(n => new PointOfSale(n, PointOfSaleKind.WebServiceCae))];

    private static async Task<LiquidacionesSim> StartAsync(string service, string wsdl, string cuitField = "cuit")
    {
        var sim = await LiquidacionesSim.StartAsync(service, wsdl, cuitField);
        await sim.Sim.PutTaxpayerAsync(Issuer, "Industrias del Campo SA", VatCondition.ResponsableInscripto, Points);
        return sim;
    }

    [Fact]
    public async Task Ltv_uses_a_bale_in_one_liquidation_only()
    {
        await using var ltv = await StartAsync("wsltv", "wsltv-homologacion.wsdl");

        var answers = await AllAtOnce(point => ltv.CallAsync("generarLiquidacion", Liquidation(point)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["1039"], Errors(a)));
    }

    [Fact]
    public async Task Ltv_adjusts_a_liquidation_physically_only_once()
    {
        await using var ltv = await StartAsync("wsltv", "wsltv-homologacion.wsdl");
        await ltv.CallAsync("generarLiquidacion", Liquidation(OriginalPoint, bales: "ORIGINAL"));

        var answers = await AllAtOnce(point => ltv.CallAsync("generarAjusteFisico", Physical(point, target: 1)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["1135"], Errors(a)));
    }

    [Fact]
    public async Task Lca_liquidates_a_delivery_note_once()
    {
        await using var lca = await StartAsync("wslca", "wslca-homologacion.wsdl", "cuitRepresentada");

        var answers = await AllAtOnce(point => lca.CallAsync("generarLiquidacion", CaneLiquidation(point)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["1303"], Errors(a)));
    }

    [Fact]
    public async Task Lca_annuls_a_liquidation_once()
    {
        await using var lca = await StartAsync("wslca", "wslca-homologacion.wsdl", "cuitRepresentada");
        await lca.CallAsync("generarLiquidacion", CaneLiquidation(OriginalPoint));

        var answers = await AllAtOnce(point => lca.CallAsync("generarAjusteFisico", CanePhysical(point, target: 1)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["1604"], Errors(a)));
    }

    [Fact]
    public async Task Lum_makes_one_liquidation_per_period_producer_and_RENSPA()
    {
        await using var lum = await StartAsync("wslum", "wslum-homologacion.wsdl");

        var answers = await AllAtOnce(point => lum.CallAsync("generarLiquidacion", MonthlyLiquidation(point)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["2078"], Errors(a)));
    }

    [Fact]
    public async Task Lsp_annuls_a_liquidation_once()
    {
        await using var lsp = await StartAsync("wslsp", "wslsp-homologacion.wsdl");
        await lsp.CallAsync("generarLiquidacion", Purchase(OriginalPoint));

        var answers = await AllAtOnce(point => lsp.CallAsync("generarAjuste", Annulment(point, target: 1)));

        Assert.Equal(1, answers.Count(a => Errors(a).Count == 0));
        Assert.All(answers.Where(a => Errors(a).Count > 0), a => Assert.Equal(["5002"], Errors(a)));
    }

    [Fact]
    public async Task Tabaco_denatures_a_CATHE_once()
    {
        await using var tabaco = await StartAsync("wstabaco", "wstabaco-homologacion.wsdl", "cuitRepresentada");
        var cathe = (await RequestAsync(tabaco, 1)).Single();
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathe, Producer, 50));

        var answers = await AllAtOnce(_ => tabaco.CallAsync("solicitarDesnaturalizacion",
            $"<deposito>10</deposito><fecha>{Day(20)}</fecha><motivo>Tabaco en mal estado</motivo><arrayCathes><cathe>{cathe}</cathe></arrayCathes>"));

        Assert.Equal(1, answers.Count(a => a.Element("resultado")!.Value == "A"));
        Assert.All(answers.Where(a => a.Element("resultado")!.Value != "A"), a => Assert.Equal(["1703"], Errors(a)));
    }

    [Fact]
    public async Task Tabaco_links_a_CATHE_once()
    {
        await using var tabaco = await StartAsync("wstabaco", "wstabaco-homologacion.wsdl", "cuitRepresentada");
        var cathe = (await RequestAsync(tabaco, 1)).Single();

        var answers = await AllAtOnce(_ => tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathe, Producer, 50)));

        Assert.Equal(1, answers.Count(a => a.Element("resultado")!.Value == "A"));
        Assert.All(answers.Where(a => a.Element("resultado")!.Value != "A"), a => Assert.Equal(["1402"], Errors(a)));
    }

    /// <summary>The same call from <see cref="Callers"/> clients that start together; each gets the point of sale 1 to <see cref="Callers"/> its number says.</summary>
    private static async Task<List<XElement>> AllAtOnce(Func<int, Task<XElement>> call)
    {
        using var start = new ManualResetEventSlim();
        var calls = Enumerable.Range(1, Callers).Select(point => Task.Run(() =>
        {
            start.Wait();
            return call(point);
        })).ToList();
        start.Set();
        return [.. await Task.WhenAll(calls)];
    }

    // ---- wsltv ---------------------------------------------------------------------------

    private static string Liquidation(int point, string bales = "SAME") =>
        "<solicitud><liquidacion>" +
        $"<tipoComprobante>150</tipoComprobante><nroComprobante>1</nroComprobante><puntoVenta>{point}</puntoVenta>" +
        $"<codDepositoAcopio>1</codDepositoAcopio><fechaLiquidacion>{Day()}</fechaLiquidacion><tipoCompra>CPS</tipoCompra>" +
        "<condicionVenta><codigo>1</codigo></condicionVenta><variedadTabaco>BR</variedadTabaco><codProvinciaOrigenTabaco>10</codProvinciaOrigenTabaco>" +
        "<fechaInicioActividad>2010-01-01</fechaInicioActividad></liquidacion>" +
        $"<receptor><cuit>{Producer}</cuit></receptor>" +
        $"<romaneo><nroRomaneo>1001</nroRomaneo><fechaRomaneo>{Day(-1)}</fechaRomaneo>" +
        $"<fardo><codTrazabilidad>{bales}-1</codTrazabilidad><claseTabaco>1</claseTabaco><peso>50</peso></fardo></romaneo>" +
        "<precioClase><claseTabaco>1</claseTabaco><precio>100.00</precio></precioClase></solicitud>";

    private static string Physical(int point, long target) =>
        $"<solicitud><tipoComprobante>150</tipoComprobante><puntoVenta>{point}</puntoVenta><nroComprobante>1</nroComprobante>" +
        $"<fechaLiquidacion>{Day()}</fechaLiquidacion><fechaInicioActividad>2010-01-01</fechaInicioActividad>" +
        $"<comprobanteAAjustar><tipoComprobante>150</tipoComprobante><puntoVenta>{OriginalPoint}</puntoVenta><nroComprobante>{target}</nroComprobante></comprobanteAAjustar></solicitud>";

    // ---- wslca ---------------------------------------------------------------------------

    private static string CaneLiquidation(int point) =>
        $"<solicitud><emisor><comprobante><puntoVenta>{point}</puntoVenta><tipoComprobante>171</tipoComprobante><nroComprobante>1</nroComprobante></comprobante>" +
        "<fechaInicioActividades>2010-01-01</fechaInicioActividades></emisor>" +
        $"<receptor><cuit>{Producer}</cuit><localidad>1</localidad><provincia>23</provincia></receptor>" +
        $"<datosGenerales><fechaComprobante>{Day()}</fechaComprobante><condicionVenta><codigo>1</codigo></condicionVenta>" +
        "<medioPago><codigo>1</codigo></medioPago></datosGenerales>" +
        "<remito><nroRemito>00007-00979871</nroRemito><kilos>10000</kilos></remito>" +
        "<detalle><producto>1</producto><cantidad>10000</cantidad><unidadMedida>1</unidadMedida><precioUnitario>15.50</precioUnitario>" +
        "<alicuotaIVA>21</alicuotaIVA></detalle></solicitud>";

    private static string CanePhysical(int point, long target) =>
        $"<solicitud><emisor><comprobante><puntoVenta>{point}</puntoVenta><tipoComprobante>171</tipoComprobante><nroComprobante>1</nroComprobante></comprobante>" +
        $"<comprobanteAjustado><puntoVenta>{OriginalPoint}</puntoVenta><tipoComprobante>171</tipoComprobante><nroComprobante>{target}</nroComprobante></comprobanteAjustado></emisor>" +
        $"<fechaComprobante>{Day()}</fechaComprobante><devolucionMercaderia>true</devolucionMercaderia></solicitud>";

    // ---- wslum ---------------------------------------------------------------------------

    private static string MonthlyLiquidation(int point) =>
        "<solicitud><liquidacion>" +
        $"<periodo>2026/10</periodo><fechaComprobante>2026-10-01</fechaComprobante><puntoVenta>{point}</puntoVenta>" +
        "<tipoComprobante>27</tipoComprobante><nroComprobante>1</nroComprobante><alicuotaIVA>21</alicuotaIVA>" +
        "<condicionVenta><codigo>1</codigo></condicionVenta></liquidacion>" +
        $"<tambero><cuit>{Producer}</cuit></tambero>" +
        "<tambo><nroTamboInterno>1234</nroTamboInterno><nroRenspa>01.001.0.00001/00</nroRenspa>" +
        "<ubicacionTambo><latitud>-34.600000</latitud><longitud>-58.400000</longitud><domicilio>Ruta 5 km 100</domicilio>" +
        "<codLocalidad>1</codLocalidad><codProvincia>1</codProvincia><nombrePartidoDepto>Chivilcoy</nombrePartidoDepto><codigoPostal>6620</codigoPostal></ubicacionTambo>" +
        "<fechaVencCertTuberculosis>2027-01-01</fechaVencCertTuberculosis><fechaVencCertBrucelosis>2027-01-01</fechaVencCertBrucelosis></tambo>" +
        "<balanceLitrosPorcentajesSolidos><litrosRemitidos>100000</litrosRemitidos><litrosDecomisados>0</litrosDecomisados>" +
        "<kgGrasa>3500</kgGrasa><kgProteina>3300</kgProteina></balanceLitrosPorcentajesSolidos>" +
        "<conceptosBasicosMercadoInterno><kgProduccionGB>3500</kgProduccionGB><precioPorKgProduccionGB>100</precioPorKgProduccionGB>" +
        "<kgProduccionPR>3300</kgProduccionPR><precioPorKgProduccionPR>120</precioPorKgProduccionPR></conceptosBasicosMercadoInterno></solicitud>";

    // ---- wslsp ---------------------------------------------------------------------------

    private static string Purchase(int point) =>
        "<solicitud><codOperacion>4</codOperacion>" +
        $"<emisor><puntoVenta>{point}</puntoVenta><tipoComprobante>183</tipoComprobante><nroComprobante>1</nroComprobante>" +
        "<codCaracter>5</codCaracter><fechaInicioActividades>2010-01-01</fechaInicioActividades></emisor>" +
        $"<receptor><codCaracter>3</codCaracter><operador><cuit>{Producer}</cuit></operador></receptor>" +
        $"<datosLiquidacion><fechaComprobante>{Day()}</fechaComprobante><fechaOperacion>{Day()}</fechaOperacion><codMotivo>6</codMotivo></datosLiquidacion>" +
        "<itemDetalleLiquidacion><codCategoria>51020102</codCategoria><tipoLiquidacion>1</tipoLiquidacion><cantidad>10</cantidad>" +
        "<precioUnitario>1500.000</precioUnitario><alicuotaIVA>10.5</alicuotaIVA><cantidadCabezas>10</cantidadCabezas></itemDetalleLiquidacion></solicitud>";

    private static string Annulment(int point, long target) =>
        $"<solicitud><tipoAjuste>C</tipoAjuste><fechaComprobante>{Day()}</fechaComprobante>" +
        $"<emisor><puntoVenta>{point}</puntoVenta><nroComprobante>1</nroComprobante>" +
        $"<comprobanteAAjustar><tipoComprobante>183</tipoComprobante><puntoVenta>{OriginalPoint}</puntoVenta><nroComprobante>{target}</nroComprobante></comprobanteAAjustar></emisor>" +
        "<itemDetalleAjusteLiquidacion><nroItemAjustar>1</nroItemAjustar><ajusteFisico><cantidad>10</cantidad></ajusteFisico></itemDetalleAjusteLiquidacion></solicitud>";

    // ---- wstabaco ------------------------------------------------------------------------

    private static async Task<List<long>> RequestAsync(LiquidacionesSim tabaco, int quantity)
    {
        var answer = await tabaco.CallAsync("solicitarCathesTabacoElaborado", $"<inicial>S</inicial><deposito>10</deposito><cantidad>{quantity}</cantidad>");
        return answer.Element("arrayCathes")!.Elements("cathe").Select(c => long.Parse(c.Value)).ToList();
    }

    private static string Link(long cathe, long holder, decimal kilos) =>
        $"<recupero>S</recupero><tipoMercaderia>3</tipoMercaderia><arrayCathesElaborados><datosTabacoElaborado><cathe>{cathe}</cathe>" +
        $"<cuitTitular>{holder}</cuitTitular><kilosBrutos>{kilos + 5}</kilosBrutos><kilosNetos>{kilos}</kilosNetos></datosTabacoElaborado></arrayCathesElaborados>";
}
