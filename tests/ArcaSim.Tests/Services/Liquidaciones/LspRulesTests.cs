using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>WSLSP: liquidations with a CAE on último + 1, their queries, adjustments and annulments, valid for the WSDL.</summary>
public class LspRulesTests
{
    private static Task<LiquidacionesSim> StartAsync() => LiquidacionesSim.StartAsync("wslsp", "wslsp-homologacion.wsdl");

    private static string Last(int pointOfSale, int type) =>
        $"<solicitud><puntoVenta>{pointOfSale}</puntoVenta><tipoComprobante>{type}</tipoComprobante></solicitud>";

    private static string ByNumber(int pointOfSale, int type, long number) =>
        $"<solicitud><puntoVenta>{pointOfSale}</puntoVenta><tipoComprobante>{type}</tipoComprobante><nroComprobante>{number}</nroComprobante></solicitud>";

    /// <summary>A purchase (operation 4, type 183): 10 head at 1500 with 10.5% VAT, an expense of 1000 + 21% VAT and a tax of 150.</summary>
    private static string Purchase(long number, int pointOfSale = 1, int type = 183, int operation = 4, string? date = null) =>
        $"<solicitud><codOperacion>{operation}</codOperacion>" +
        $"<emisor><puntoVenta>{pointOfSale}</puntoVenta><tipoComprobante>{type}</tipoComprobante><nroComprobante>{number}</nroComprobante>" +
        "<codCaracter>5</codCaracter><fechaInicioActividades>2010-01-01</fechaInicioActividades></emisor>" +
        $"<receptor><codCaracter>3</codCaracter><operador><cuit>{Producer}</cuit></operador></receptor>" +
        $"<datosLiquidacion><fechaComprobante>{date ?? Day()}</fechaComprobante><fechaOperacion>{date ?? Day()}</fechaOperacion><codMotivo>6</codMotivo></datosLiquidacion>" +
        "<itemDetalleLiquidacion><codCategoria>51020102</codCategoria><tipoLiquidacion>1</tipoLiquidacion><cantidad>10</cantidad>" +
        "<precioUnitario>1500.000</precioUnitario><alicuotaIVA>10.5</alicuotaIVA><cantidadCabezas>10</cantidadCabezas></itemDetalleLiquidacion>" +
        "<gasto><codGasto>16</codGasto><importe>1000</importe><alicuotaIVA>21</alicuotaIVA></gasto>" +
        "<tributo><codTributo>5</codTributo><importe>150</importe></tributo></solicitud>";

    private static string Adjustment(string kind, long number, long target, string items, string date = "") =>
        $"<solicitud><tipoAjuste>{kind}</tipoAjuste><fechaComprobante>{(date.Length > 0 ? date : Day())}</fechaComprobante>" +
        $"<emisor><puntoVenta>1</puntoVenta><nroComprobante>{number}</nroComprobante>" +
        $"<comprobanteAAjustar><tipoComprobante>183</tipoComprobante><puntoVenta>1</puntoVenta><nroComprobante>{target}</nroComprobante></comprobanteAAjustar></emisor>" +
        $"{items}</solicitud>";

    private const string FullReturn = "<itemDetalleAjusteLiquidacion><nroItemAjustar>1</nroItemAjustar><ajusteFisico><cantidad>10</cantidad></ajusteFisico></itemDetalleAjusteLiquidacion>";

    [Fact]
    public async Task A_liquidation_takes_the_next_number_gets_a_CAE_and_reads_back_the_same()
    {
        await using var lsp = await StartAsync();

        Assert.Equal("0", (await lsp.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(1, 183))).Element("nroComprobante")!.Value);

        var issued = await lsp.CallAsync("generarLiquidacion", Purchase(1));
        var cae = issued.Element("cabecera")!.Element("cae")!.Value;
        Assert.Empty(Errors(issued));
        Assert.Equal(14, cae.Length);
        Assert.Equal("2026-10-11", issued.Element("cabecera")!.Element("fechaVencimientoCae")!.Value);
        Assert.Null(issued.Element("cabecera")!.Element("nroCodigoBarras"));
        Assert.Equal("1", issued.Element("itemDetalleLiquidacion")!.Element("nroItem")!.Value);
        Assert.Equal("15215.00", issued.Element("resumenTotales")!.Element("importeTotalNeto")!.Value);
        Assert.StartsWith("JVBERi0xLjQK", issued.Element("pdf")!.Value);
        Assert.NotNull(issued.Element("metadata"));

        Assert.Equal("1", (await lsp.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(1, 183))).Element("nroComprobante")!.Value);
        Assert.Equal("0", (await lsp.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(1, 185))).Element("nroComprobante")!.Value);

        var read = await lsp.CallAsync("consultarLiquidacionPorNroComprobante", ByNumber(1, 183, 1));
        Assert.Equal(cae, read.Element("cabecera")!.Element("cae")!.Value);
        Assert.Equal(Producer.ToString(), read.Element("receptor")!.Element("cuit")!.Value);
        Assert.Equal("JUAN PRODUCTOR", read.Element("receptor")!.Element("nombre")!.Value);

        var voucher = await lsp.Sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Issuer, 1, 183, 1);
        Assert.Equal(cae, voucher!.Code);
        Assert.Equal("wslsp", voucher.Service);
    }

    [Fact]
    public async Task A_number_out_of_sequence_is_1009_and_is_not_consumed()
    {
        await using var lsp = await StartAsync();

        Assert.Equal(["1009"], Errors(await lsp.CallAsync("generarLiquidacion", Purchase(2))));
        Assert.Empty(Errors(await lsp.CallAsync("generarLiquidacion", Purchase(1))));
        Assert.Equal(["1009"], Errors(await lsp.CallAsync("generarLiquidacion", Purchase(1))));
        Assert.Empty(Errors(await lsp.CallAsync("generarLiquidacion", Purchase(2))));
        Assert.Equal("2", (await lsp.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(1, 183))).Element("nroComprobante")!.Value);
    }

    [Fact]
    public async Task Business_rules_answer_their_codes_in_respuesta_errores()
    {
        await using var lsp = await StartAsync();

        var wrongType = await lsp.CallAsync("generarLiquidacion", Purchase(1, type: 180));
        Assert.Equal(["2006"], Errors(wrongType));
        Assert.Equal("Emisor: El tipo de comprobante no es válido para el tipo de operación que intenta realizar.",
            wrongType.Element("errores")!.Element("error")!.Element("descripcion")!.Value);
        Assert.NotNull(wrongType.Element("metadata"));
        Assert.Equal(["1008"], Errors(await lsp.CallAsync("generarLiquidacion", Purchase(1, pointOfSale: 7))));
        Assert.Equal(["1007"], Errors(await lsp.CallAsync("generarLiquidacion", Purchase(1), await lsp.AuthForAsync(Producer))));
        Assert.Equal(["2200"], Errors(await lsp.CallAsync("generarLiquidacion", Purchase(1, date: Day(-6)))));
        Assert.Equal(["1000"], Errors(await lsp.CallAsync("consultarLiquidacionPorNroComprobante", ByNumber(1, 183, 1))));
    }

    [Fact]
    public async Task Taking_every_item_back_annuls_the_liquidation_once()
    {
        await using var lsp = await StartAsync();
        await lsp.CallAsync("generarLiquidacion", Purchase(1));

        var annulment = await lsp.CallAsync("generarAjuste", Adjustment("C", 2, 1, FullReturn));
        Assert.Empty(Errors(annulment));
        Assert.Equal("C", annulment.Element("ajuste")!.Element("tipoAjuste")!.Value);
        Assert.Equal("Fisico", annulment.Element("ajuste")!.Element("modoAjuste")!.Value);
        Assert.Equal("1", annulment.Element("ajuste")!.Element("comprobanteAjustado")!.Element("nroComprobante")!.Value);
        Assert.Equal("2", annulment.Element("emisor")!.Element("nroComprobante")!.Value);
        Assert.Equal("16575.00", annulment.Element("resumenTotales")!.Element("importeTotalNeto")!.Value);
        Assert.NotEqual(annulment.Element("cabecera")!.Element("cae")!.Value,
            (await lsp.CallAsync("consultarLiquidacionPorNroComprobante", ByNumber(1, 183, 1))).Element("cabecera")!.Element("cae")!.Value);

        Assert.Equal("2", (await lsp.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(1, 183))).Element("nroComprobante")!.Value);
        Assert.Equal(["5002"], Errors(await lsp.CallAsync("generarAjuste", Adjustment("C", 3, 1, FullReturn))));
        Assert.Equal(["3007"], Errors(await lsp.CallAsync("generarAjuste", Adjustment("D", 3, 2,
            "<itemDetalleAjusteLiquidacion><nroItemAjustar>1</nroItemAjustar><ajusteMonetario><precioUnitario>10</precioUnitario></ajusteMonetario></itemDetalleAjusteLiquidacion>"))));
    }

    [Fact]
    public async Task A_price_adjustment_follows_the_liquidations_sequence_and_checks_its_mode()
    {
        await using var lsp = await StartAsync();
        await lsp.CallAsync("generarLiquidacion", Purchase(1));
        const string price = "<itemDetalleAjusteLiquidacion><nroItemAjustar>1</nroItemAjustar><ajusteMonetario><precioUnitario>100</precioUnitario></ajusteMonetario></itemDetalleAjusteLiquidacion>";

        Assert.Equal(["3000"], Errors(await lsp.CallAsync("generarAjuste", Adjustment("D", 2, 9, price))));
        Assert.Equal(["3002"], Errors(await lsp.CallAsync("generarAjuste", Adjustment("D", 2, 1, price + FullReturn))));
        Assert.Equal(["3006"], Errors(await lsp.CallAsync("generarAjuste", Adjustment("D", 2, 1, ""))));

        var debit = await lsp.CallAsync("generarAjuste", Adjustment("D", 2, 1, price));
        Assert.Equal("Monetario", debit.Element("ajuste")!.Element("modoAjuste")!.Value);
        Assert.Equal("1000.00", debit.Element("resumenTotales")!.Element("importeBruto")!.Value);

        var read = await lsp.CallAsync("consultarLiquidacionPorNroComprobante", ByNumber(1, 183, 2));
        Assert.Equal("D", read.Element("ajuste")!.Element("tipoAjuste")!.Value);
        Assert.Empty(Errors(await lsp.CallAsync("generarAjuste", Adjustment("C", 3, 1, FullReturn))));
    }

    [Theory]
    [InlineData("emisor", "puntoVenta")]
    [InlineData("emisor", "nroComprobante")]
    [InlineData("datosLiquidacion", "fechaComprobante")]
    public async Task An_adjustment_of_a_stored_voucher_without_a_field_it_overwrites_fails_instead_of_answering_without_it(string block, string field)
    {
        await using var lsp = await StartAsync();
        lsp.Sim.ExpectLoggedErrors();
        await lsp.CallAsync("generarLiquidacion", Purchase(1));
        await lsp.EditStoredAsync("wslsp", Issuer, 1, 183, 1, detail => detail.Element(block)!.Element(field)!.Remove());
        const string price = "<itemDetalleAjusteLiquidacion><nroItemAjustar>1</nroItemAjustar><ajusteMonetario><precioUnitario>100</precioUnitario></ajusteMonetario></itemDetalleAjusteLiquidacion>";

        var (status, body) = await lsp.PostAsync("generarAjuste", Adjustment("D", 2, 1, price));

        Assert.True(status == 500, body);
        var failure = Assert.IsType<InvalidOperationException>(Assert.Single(lsp.Sim.LoggedErrors).Exception);
        Assert.Equal($"The stored voucher has no <{field}> to set.", failure.Message);
    }

    [Fact]
    public async Task A_poultry_liquidation_has_its_own_operations()
    {
        await using var lsp = await StartAsync();
        var poultry =
            "<solicitud><codOperacion>201</codOperacion>" +
            "<emisor><puntoVenta>1</puntoVenta><tipoComprobante>160</tipoComprobante><nroComprobante>1</nroComprobante>" +
            "<codCaracter>5</codCaracter><fechaInicioActividades>2010-01-01</fechaInicioActividades></emisor>" +
            $"<receptor><codCaracter>3</codCaracter><tipoDoc>80</tipoDoc><nroDoc>{Producer}</nroDoc><nombreApellido>Juan Productor</nombreApellido></receptor>" +
            $"<datosLiquidacion><fechaComprobante>{Day(-8)}</fechaComprobante><fechaOperacion>{Day(-8)}</fechaOperacion><codMotivo>201</codMotivo>" +
            "<condicionVenta><codigo>1</codigo></condicionVenta></datosLiquidacion>" +
            "<itemDetalleLiquidacion><tipoLiquidacion>2</tipoLiquidacion><cantidad>5000</cantidad><precioUnitario>2.500</precioUnitario>" +
            "<alicuotaIVA>10.5</alicuotaIVA></itemDetalleLiquidacion>" +
            "<resultadoProductivo><bbIngresados>5200</bbIngresados><cantidadCabezasAvesFaenadas>5000</cantidadCabezasAvesFaenadas>" +
            "<cantidadKilosAvesFaenadas>14000</cantidadKilosAvesFaenadas><edad>48</edad><kilosConsumoAlimentoBalanceado>25000</kilosConsumoAlimentoBalanceado>" +
            "<fep>310.50</fep></resultadoProductivo></solicitud>";

        Assert.Equal(["2006"], Errors(await lsp.CallAsync("generarLiquidacion", poultry)));
        var issued = await lsp.CallAsync("generarLiquidacionAvicola", poultry);
        Assert.Empty(Errors(issued));
        Assert.Equal("200", issued.Element("resultadoProductivo")!.Element("cantidadCabezasMortandad")!.Value);

        var read = await lsp.CallAsync("consultarLiquidacionAvicolaPorNroComp", ByNumber(1, 160, 1));
        Assert.Equal(issued.Element("cabecera")!.Element("cae")!.Value, read.Element("cabecera")!.Element("cae")!.Value);
        Assert.Equal(["1000"], Errors(await lsp.CallAsync("consultarLiquidacionPorNroComprobante", ByNumber(1, 160, 1))));
    }

    [Fact]
    public async Task Points_of_sale_and_voucher_types_come_from_the_issuer_and_the_manual()
    {
        await using var lsp = await StartAsync();

        var points = await lsp.CallAsync("consultarPuntosVenta");
        Assert.Equal(["1", "3000"], points.Elements("puntoVenta").Select(p => p.Element("codigo")!.Value));
        Assert.Empty((await lsp.CallAsync("consultarPuntosVenta", auth: await lsp.AuthForAsync(Producer))).Elements("puntoVenta"));

        var types = await lsp.CallAsync("consultarTiposComprobante");
        Assert.Contains(types.Elements("tipoComprobante"), t => t.Element("codigo")!.Value == "183");
    }
}
