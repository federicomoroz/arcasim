using ArcaSim.Application;
using ArcaSim.Application.Services.Aduana;
using ArcaSim.Infrastructure.InMemory;

namespace ArcaSim.Tests.Services.Aduana;

/// <summary>wgestiendaslibres: goods enter a free shop's depósito, are sold, destroyed or left short, and the stock follows.</summary>
public class TiendasLibresRulesTests
{
    private const string Place = "<aduana>001</aduana><lugarOperativo>TL001</lugarOperativo>";
    private const string Query = "<Aduana>001</Aduana><LugarOperativo>TL001</LugarOperativo>";

    private static Task<AduanaService> ShopAsync(AduanaKit kit) =>
        kit.ServiceAsync("wgestiendaslibres", (t, c) => AduanaKit.Empresa(t, c));

    private static string Entry(string transaction, string receipt = "REM-0001", int quantity = 10) =>
        $"<argIngresarMercaderiaParams>{Place}<idComprobante>{receipt}</idComprobante><origen>N</origen><transaccion>{transaction}</transaccion>" +
        "<ListaMercaderiaIngresada><MercaderiaIngresada><NCM>2208.30.20</NCM><codProducto>WHISKY-1L</codProducto><descProducto>Whisky 1 litro</descProducto>" +
        $"<cantidad>{quantity}</cantidad><valorUnitarioDol>25</valorUnitarioDol></MercaderiaIngresada></ListaMercaderiaIngresada></argIngresarMercaderiaParams>";

    private static string Sale(string transaction, string number, int quantity) =>
        $"<argVentaMercaderiaParams>{Place}<tipoLocal>ARRI</tipoLocal><docIdentidad>X123</docIdentidad><nacionalidad>CL</nacionalidad><edad>30</edad>" +
        $"<tipoComprobante>TIQ</tipoComprobante><nroComprobante>{number}</nroComprobante><listaMercaderiaVendida><MercaderiaVendida><NCM>2208.30.20</NCM>" +
        $"<codProducto>WHISKY-1L</codProducto><origen>N</origen><cantidad>{quantity}</cantidad><valorUnitarioDol>40</valorUnitarioDol></MercaderiaVendida>" +
        $"</listaMercaderiaVendida><transaccion>{transaction}</transaccion></argVentaMercaderiaParams>";

    private static string Destroy(string transaction, string product, int quantity) =>
        $"<argDestruirMercaderiaParams>{Place}<idComprobante>ACTA-1</idComprobante><listaMercaderiaDestruida><MercaderiaDestruida><NCM>2208.30.20</NCM>" +
        $"<codProducto>{product}</codProducto><origen>N</origen><cantidad>{quantity}</cantidad></MercaderiaDestruida></listaMercaderiaDestruida>" +
        $"<transaccion>{transaction}</transaccion></argDestruirMercaderiaParams>";

    private static async Task<decimal> StockAsync(AduanaService shop) =>
        decimal.Parse((await shop.CallAsync("ConsultarStock", $"<argConsultarStockParams>{Query}</argConsultarStockParams>")).V("Cantidad"),
            System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Goods_enter_are_sold_and_the_movement_and_stock_show_it()
    {
        await using var kit = await AduanaKit.StartAsync();
        var shop = await ShopAsync(kit);

        var entered = await shop.CallAsync("IngresarMercaderia", Entry("T-1"));
        var sold = await shop.CallAsync("VentaMercaderia", Sale("T-2", "0001-00000001", 4));
        var replay = await shop.CallAsync("VentaMercaderia", Sale("T-2", "0001-00000099", 1));
        var movement = await shop.CallAsync("GetMovimiento",
            $"<argConsultarMovimientosParams>{Query}<IdMovimiento>{sold.V("idMovimiento")}</IdMovimiento></argConsultarMovimientosParams>");
        var movements = await shop.CallAsync("ConsultarMovimientos",
            $"<argConsultarMovimientosParams>{Query}<FechaDesde>2026-10-01T00:00:00</FechaDesde><FechaHasta>2026-10-01T23:59:59</FechaHasta></argConsultarMovimientosParams>");

        Assert.Equal("0", entered.Code());
        Assert.Equal("10.30.32.108", entered.V("Server")); // the address the catalog says the service always sends (live capture)
        Assert.Equal("0", sold.Code());
        Assert.Equal(sold.V("idMovimiento"), replay.V("idMovimiento"));
        Assert.Equal(6m, await StockAsync(shop));
        Assert.Equal("VTA", movement.V("CodMovimiento"));
        Assert.Equal("TIQ 0001-00000001", movement.V("IdComprobante"));
        Assert.Equal("4", movement.V("Cantidad"));
        Assert.Equal(["ING", "VTA"], movements.All("CodMovimiento").Select(m => m.Value));
    }

    [Fact]
    public async Task A_sale_without_stock_goes_through_and_leaves_a_DIFE_to_justify()
    {
        await using var kit = await AduanaKit.StartAsync();
        var shop = await ShopAsync(kit);
        await shop.CallAsync("IngresarMercaderia", Entry("T-1", quantity: 2));

        var sold = await shop.CallAsync("VentaMercaderia", Sale("T-2", "0001-00000002", 5));
        var repeated = await shop.CallAsync("VentaMercaderia", Sale("T-3", "0001-00000002", 1));
        var dife = await shop.CallAsync("ConsultarDIFE",
            "<argConsultarDIFEParams><codEstado>REG</codEstado><fechaDesde>2026-10-01T00:00:00</fechaDesde><fechaHasta>2026-10-01T00:00:00</fechaHasta></argConsultarDIFEParams>");
        string Justify(string transaction, int quantity) =>
            $"<argRegistrarJustificacionDIFEParams><idDIFE>{dife.V("idDIFE")}</idDIFE><listaJustificacion><DetalleJustificacion><codJustificacion>CANT</codJustificacion>" +
            $"<textoJustificacion>Error de conteo</textoJustificacion><cantidadJustificacion>{quantity}</cantidadJustificacion></DetalleJustificacion>" +
            $"</listaJustificacion><transaccion>{transaction}</transaccion></argRegistrarJustificacionDIFEParams>";
        var wrongTotal = await shop.CallAsync("RegistrarJustificacionDIFE", Justify("T-4", 2));
        var justified = await shop.CallAsync("RegistrarJustificacionDIFE", Justify("T-5", 3));
        var presented = await shop.CallAsync("ConsultarDIFE",
            $"<argConsultarDIFEParams><idDIFE>{dife.V("idDIFE")}</idDIFE><fechaDesde>2026-10-01T00:00:00</fechaDesde><fechaHasta>2026-10-01T00:00:00</fechaHasta></argConsultarDIFEParams>");

        Assert.Equal("0", sold.Code());
        Assert.Equal("Se registra diferencia por stock en negativo", sold.V("DescripcionAdicional"));
        Assert.Equal(-3m, await StockAsync(shop));
        Assert.Equal("21526", repeated.Code());
        Assert.Equal("10.30.32.108", repeated.V("Server")); // a refusal sends the same server as a success
        Assert.Equal("Venta ya registrada TIQ 0001-00000002", repeated.V("Descripcion"));
        Assert.Equal("3", dife.V("cantidad"));
        Assert.Equal("2026-10-31", dife.V("fechaVenc")[..10]);
        Assert.Equal("21550", wrongTotal.Code());
        Assert.Equal("0", justified.Code());
        Assert.Equal("PRE", presented.V("codEstado"));
        Assert.Equal("CANT", presented.V("codJustificacion"));
    }

    [Fact]
    public async Task A_particulars_goods_wait_for_their_salida_and_destruction_needs_stock()
    {
        await using var kit = await AduanaKit.StartAsync();
        var shop = await ShopAsync(kit);
        await shop.CallAsync("IngresarMercaderia", Entry("T-1", receipt: "26001PI01000001A"));

        var none = await shop.CallAsync("ConsultarStock", $"<argConsultarStockParams>{Query}</argConsultarStockParams>");
        var released = await shop.CallAsync("SalidaParticular",
            $"<argSalidaParticularParams>{Place}<idDeclaracion>26001PI01000001A</idDeclaracion><transaccion>T-2</transaccion></argSalidaParticularParams>");
        var stock = await StockAsync(shop);
        var tooMuch = await shop.CallAsync("DestruirMercaderia", Destroy("T-3", "WHISKY-1L", 11));
        var unknown = await shop.CallAsync("DestruirMercaderia", Destroy("T-4", "GIN-1L", 1));
        var destroyed = await shop.CallAsync("DestruirMercaderia", Destroy("T-5", "WHISKY-1L", 3));

        Assert.Equal("30286", none.Code());
        Assert.Equal("0", released.Code());
        Assert.StartsWith("26001SALP", released.V("nroSalida"));
        Assert.Equal(10m, stock);
        Assert.Equal("42302", tooMuch.Code());
        Assert.Equal("42303", unknown.Code());
        Assert.Equal("0", destroyed.Code());
        Assert.Equal(7m, await StockAsync(shop));
    }

    [Fact]
    public async Task Sales_that_run_together_all_take_their_units_off_the_stock()
    {
        var clock = new SimulatedClock(TimeProvider.System);
        clock.Freeze(AduanaKit.Today);
        var shop = new RulesProbe(new TiendasLibresRules(new YieldingDocumentStore(new InMemoryStore()), clock), AduanaKit.Today);
        await shop.CallAsync("IngresarMercaderia", Entry("T-0", quantity: 100), AduanaKit.Caller);

        await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
            shop.CallAsync("VentaMercaderia", Sale($"T-{i}", $"0001-{i:D8}", 1), AduanaKit.Caller)));

        var stock = await shop.CallAsync("ConsultarStock", $"<argConsultarStockParams>{Query}</argConsultarStockParams>", AduanaKit.Caller);
        Assert.Equal("80", stock.V("Cantidad"));
    }

    [Fact]
    public async Task The_same_transaction_sent_together_is_processed_once_and_the_others_wait_or_replay()
    {
        var clock = new SimulatedClock(TimeProvider.System);
        clock.Freeze(AduanaKit.Today);
        var shop = new RulesProbe(new TiendasLibresRules(new YieldingDocumentStore(new InMemoryStore()), clock), AduanaKit.Today);
        await shop.CallAsync("IngresarMercaderia", Entry("T-0", quantity: 100), AduanaKit.Caller);

        var answers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => shop.CallAsync("VentaMercaderia", Sale("T-1", "0001-00000001", 1), AduanaKit.Caller)));

        Assert.All(answers, a => Assert.Contains(a.Code(), new[] { "0", "41973" })); // the answer it had, or "in course" (p.9)
        Assert.Contains(answers, a => a.Code() == "0");
        var stock = await shop.CallAsync("ConsultarStock", $"<argConsultarStockParams>{Query}</argConsultarStockParams>", AduanaKit.Caller);
        Assert.Equal("99", stock.V("Cantidad"));
    }
}
