using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>WSLCA: sugar cane liquidations on the CXF stack, delivery notes liquidated once, price and physical adjustments.</summary>
public class LcaRulesTests
{
    private static Task<LiquidacionesSim> StartAsync() => LiquidacionesSim.StartAsync("wslca", "wslca-homologacion.wsdl", "cuitRepresentada");

    private const string Last = "<solicitud><puntoVenta>3000</puntoVenta><tipoComprobante>171</tipoComprobante></solicitud>";

    private static string Voucher(long number, string name = "comprobante", int type = 171) =>
        $"<{name}><puntoVenta>3000</puntoVenta><tipoComprobante>{type}</tipoComprobante><nroComprobante>{number}</nroComprobante></{name}>";

    /// <summary>10000 kg of cane at 15.50 with 21% VAT and a tax of 100: 155000 + 32550 + 100 = 187650.</summary>
    private static string Liquidation(long number, string note = "00007-00979871", int type = 171, long grower = Producer, long kilos = 10000) =>
        $"<solicitud><emisor>{Voucher(number, type: type)}<fechaInicioActividades>2010-01-01</fechaInicioActividades></emisor>" +
        $"<receptor><cuit>{grower}</cuit><localidad>1</localidad><provincia>23</provincia></receptor>" +
        $"<datosGenerales><fechaComprobante>{Day()}</fechaComprobante><condicionVenta><codigo>1</codigo></condicionVenta>" +
        "<medioPago><codigo>1</codigo></medioPago></datosGenerales>" +
        $"<remito><nroRemito>{note}</nroRemito><kilos>{kilos}</kilos></remito>" +
        "<detalle><producto>1</producto><cantidad>10000</cantidad><unidadMedida>1</unidadMedida><precioUnitario>15.50</precioUnitario>" +
        "<alicuotaIVA>21</alicuotaIVA></detalle>" +
        "<tributo><codTributo>1</codTributo><importe>100.00</importe></tributo></solicitud>";

    private static string Price(long number, long target, int kind = 2, int item = 1) =>
        $"<solicitud><emisor>{Voucher(number)}<tipoAjuste>{kind}</tipoAjuste></emisor>" +
        $"<datosGenerales><fechaComprobante>{Day()}</fechaComprobante><condicionVenta><codigo>1</codigo></condicionVenta>" +
        "<medioPago><codigo>1</codigo></medioPago></datosGenerales>" +
        $"<detalle>{Voucher(target, "comprobanteAjustado")}<nroOrdenItemAjustado>{item}</nroOrdenItemAjustado><diferenciaPrecio>1.00</diferenciaPrecio></detalle></solicitud>";

    private static string Physical(long number, long target) =>
        $"<solicitud><emisor>{Voucher(number)}{Voucher(target, "comprobanteAjustado")}</emisor>" +
        $"<fechaComprobante>{Day()}</fechaComprobante><devolucionMercaderia>true</devolucionMercaderia></solicitud>";

    [Fact]
    public async Task A_liquidation_gets_a_CAE_and_reads_back_by_number()
    {
        await using var lca = await StartAsync();

        Assert.Equal("0", (await lca.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last)).Element("nroComprobante")!.Value);

        var issued = await lca.CallAsync("generarLiquidacion", Liquidation(1));
        var cae = issued.Element("autorizacion")!.Element("cae")!.Value;
        Assert.Equal(14, cae.Length);
        Assert.Equal("2026-10-01-03:00", issued.Element("autorizacion")!.Element("fechaProcesoAFIP")!.Value);
        Assert.Equal("1", issued.Element("detalle")!.Element("nroOrden")!.Value);
        Assert.Equal("187650.00", issued.Element("resumenTotales")!.Element("importeTotal")!.Value);
        Assert.StartsWith("JVBERi0xLjQK", issued.Element("pdf")!.Value);
        Assert.NotNull(issued.Element("metadata"));

        Assert.Equal("1", (await lca.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last)).Element("nroComprobante")!.Value);
        var read = await lca.CallAsync("consultarLiquidacionPorNroComprobante", $"<solicitud>{Voucher(1)}</solicitud>");
        Assert.Equal(cae, read.Element("autorizacion")!.Element("cae")!.Value);
        Assert.Equal(["800"], Errors(await lca.CallAsync("consultarLiquidacionPorNroComprobante", $"<solicitud>{Voucher(2)}</solicitud>")));

        Assert.NotNull(await lca.Sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Issuer, 3000, 171, 1));
    }

    [Fact]
    public async Task Delivery_notes_types_and_numbers_are_checked()
    {
        await using var lca = await StartAsync();
        await lca.CallAsync("generarLiquidacion", Liquidation(1));

        var again = await lca.CallAsync("generarLiquidacion", Liquidation(2));
        Assert.Equal(["1303"], Errors(again));
        Assert.Equal("Remito #00007-00979871: El remito que desea agregar ya se encuentra liquidado.", again.Descendants("descripcion").First().Value);
        Assert.Equal(["1500"], Errors(await lca.CallAsync("generarLiquidacion", Liquidation(3, "00007-00979872"))));
        Assert.Equal(["1206"], Errors(await lca.CallAsync("generarLiquidacion", Liquidation(2, "00007-00979872", type: 173))));
        Assert.Equal(["1207"], Errors(await lca.CallAsync("generarLiquidacion", Liquidation(1, "00007-00979872", type: 172))));
        Assert.Equal(["1304"], Errors(await lca.CallAsync("generarLiquidacion", Liquidation(2, "00007-00979872", kilos: 9000))));
        Assert.Equal(["1100"], Errors(await lca.CallAsync("generarLiquidacion", Liquidation(2, "00007-00979872", grower: Issuer))));
        Assert.Empty(Errors(await lca.CallAsync("generarLiquidacion", Liquidation(1, "00007-00979872", type: 172, grower: Monotributista))));
    }

    [Fact]
    public async Task A_price_adjustment_follows_the_sequence_and_a_physical_one_annuls()
    {
        await using var lca = await StartAsync();
        await lca.CallAsync("generarLiquidacion", Liquidation(1));

        Assert.Equal(["1603"], Errors(await lca.CallAsync("generarAjustePrecio", Price(2, 1, kind: 7))));
        Assert.Equal(["1602"], Errors(await lca.CallAsync("generarAjustePrecio", Price(2, 1, item: 5))));
        Assert.Equal(["1601"], Errors(await lca.CallAsync("generarAjustePrecio", Price(2, 9))));
        var price = await lca.CallAsync("generarAjustePrecio", Price(2, 1));
        Assert.Equal("2", price.Element("ajuste")!.Element("tipoAjuste")!.Value);
        Assert.Equal("12100.00", price.Element("resumenTotales")!.Element("importeTotal")!.Value);
        Assert.Equal("1", price.Element("detalle")!.Element("detalleAjuste")!.Element("nroOrdenAjustado")!.Value);

        var physical = await lca.CallAsync("generarAjusteFisico", Physical(3, 1));
        Assert.Equal("1", physical.Element("ajuste")!.Element("tipoAjuste")!.Value);
        Assert.Equal("true", physical.Element("ajuste")!.Element("esDevolucionMercaderia")!.Value);
        Assert.Equal(["1604"], Errors(await lca.CallAsync("generarAjusteFisico", Physical(4, 1))));
        Assert.Equal(["1604"], Errors(await lca.CallAsync("generarAjustePrecio", Price(4, 1))));
        Assert.Equal("3", (await lca.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last)).Element("nroComprobante")!.Value);
    }
}
