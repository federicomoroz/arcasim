using System.Net.Http.Json;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>WSLUM: the month's dairy liquidation with a CAE, its credit and debit notes, and the queries by number and by CAE.</summary>
public class LumRulesTests
{
    private const string Renspa = "01.001.0.00001/00";

    private static Task<LiquidacionesSim> StartAsync() => LiquidacionesSim.StartAsync("wslum", "wslum-homologacion.wsdl");

    private static string Last(int type) => $"<solicitud><puntoVenta>1</puntoVenta><tipoComprobante>{type}</tipoComprobante></solicitud>";

    /// <summary>
    /// October's liquidation: 3500 kg of fat at 100 and 3300 kg of protein at 120, a commercial bonus of 1000,
    /// 21% VAT and another tax of 1.5% over 100000: 746000 + 1000 + 156870 − 1500 = 902370.
    /// </summary>
    internal static string Liquidation(long number, int type = 27, string rate = "<alicuotaIVA>21</alicuotaIVA>", string period = "2026/10",
        string date = "2026-10-01", string adjustment = "", bool physical = true, string bonus = "<importe>1000</importe>", int pointOfSale = 1, long tambero = Producer) =>
        "<solicitud><liquidacion>" +
        $"<periodo>{period}</periodo><fechaComprobante>{date}</fechaComprobante><puntoVenta>{pointOfSale}</puntoVenta>" +
        $"<tipoComprobante>{type}</tipoComprobante><nroComprobante>{number}</nroComprobante>{rate}{adjustment}" +
        "<condicionVenta><codigo>1</codigo></condicionVenta></liquidacion>" +
        $"<tambero><cuit>{tambero}</cuit></tambero>" +
        $"<tambo><nroTamboInterno>1234</nroTamboInterno><nroRenspa>{Renspa}</nroRenspa>" +
        "<ubicacionTambo><latitud>-34.600000</latitud><longitud>-58.400000</longitud><domicilio>Ruta 5 km 100</domicilio>" +
        "<codLocalidad>1</codLocalidad><codProvincia>1</codProvincia><nombrePartidoDepto>Chivilcoy</nombrePartidoDepto><codigoPostal>6620</codigoPostal></ubicacionTambo>" +
        "<fechaVencCertTuberculosis>2027-01-01</fechaVencCertTuberculosis><fechaVencCertBrucelosis>2027-01-01</fechaVencCertBrucelosis></tambo>" +
        (physical
            ? "<balanceLitrosPorcentajesSolidos><litrosRemitidos>100000</litrosRemitidos><litrosDecomisados>0</litrosDecomisados>" +
              "<kgGrasa>3500</kgGrasa><kgProteina>3300</kgProteina></balanceLitrosPorcentajesSolidos>" +
              "<conceptosBasicosMercadoInterno><kgProduccionGB>3500</kgProduccionGB><precioPorKgProduccionGB>100</precioPorKgProduccionGB>" +
              "<kgProduccionPR>3300</kgProduccionPR><precioPorKgProduccionPR>120</precioPorKgProduccionPR></conceptosBasicosMercadoInterno>"
            : "") +
        $"<bonificacionPenalizacion><codBonificacionPenalizacion>41</codBonificacionPenalizacion><detalle>Bonificacion</detalle>{bonus}</bonificacionPenalizacion>" +
        "<otroImpuesto><tipo>1</tipo><alicuota>1.5</alicuota><baseImponible>100000</baseImponible></otroImpuesto>" +
        "<remito>123456789</remito></solicitud>";

    [Fact]
    public async Task The_months_liquidation_gets_a_CAE_and_reads_back_by_number_and_by_CAE()
    {
        await using var lum = await StartAsync();

        Assert.Equal(["2044"], Errors(await lum.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(27))));

        var issued = (await lum.CallAsync("generarLiquidacion", Liquidation(1))).Element("liquidacion")!;
        var header = issued.Element("encabezado")!;
        Assert.Equal(14, header.Element("cae")!.Value.Length);
        Assert.Equal("2026-10-11", header.Element("fechaVencimiento")!.Value);
        Assert.Equal("902370.00", issued.Element("resumenTotales")!.Element("totalNetoLiquidacion")!.Value);
        Assert.Equal("100000", issued.Element("balanceLitrosPorcentajesSolidos")!.Element("litrosNetosLiquidados")!.Value);
        Assert.Equal("JUAN PRODUCTOR", issued.Element("tambero")!.Element("razonSocial")!.Value);
        Assert.StartsWith("JVBERi0xLjQK", issued.Element("pdf")!.Value);

        Assert.Equal("1", (await lum.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(27))).Element("nroComprobante")!.Value);

        var byNumber = (await lum.CallAsync("consultarLiquidacionPorNroComprobante",
            $"<solicitud><cuitComprador>{Issuer}</cuitComprador><puntoVenta>1</puntoVenta><tipoComprobante>27</tipoComprobante><nroComprobante>1</nroComprobante><pdf>false</pdf></solicitud>"))
            .Element("liquidacion")!;
        Assert.Equal(header.Element("cae")!.Value, byNumber.Element("encabezado")!.Element("cae")!.Value);
        Assert.Null(byNumber.Element("pdf"));

        var byCae = (await lum.CallAsync("consultarLiquidacionPorCae",
            $"<solicitud><cae>{header.Element("cae")!.Value}</cae><pdf>true</pdf></solicitud>")).Element("liquidacion")!;
        Assert.Equal("1", byCae.Element("encabezado")!.Element("nroComprobante")!.Value);
        Assert.NotNull(byCae.Element("pdf"));
        Assert.Equal(["2044"], Errors(await lum.CallAsync("consultarLiquidacionPorCae", "<solicitud><cae>12345678901234</cae><pdf>false</pdf></solicitud>")));

        var voucher = await lum.Sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(Issuer, 1, 27, 1);
        Assert.Equal(Producer, voucher!.ReceiverDocNumber);
    }

    [Fact]
    public async Task One_liquidation_per_period_producer_and_RENSPA_and_numbers_in_sequence()
    {
        await using var lum = await StartAsync();
        await lum.CallAsync("generarLiquidacion", Liquidation(1));

        Assert.Equal(["2074"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1))));
        Assert.Equal(["2078"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(2))));
        Assert.Equal(["2132"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(2, period: "2026/09", date: "2026-09-25"))));
    }

    [Fact]
    public async Task Period_date_and_VAT_rules_answer_their_codes()
    {
        await using var lum = await StartAsync();

        Assert.Equal(["2121"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, date: "2026-09-30"))));
        Assert.Equal(["2055"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, period: "2026/08", date: "2026-08-30"))));
        Assert.Equal(["2114"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, rate: ""))));
        Assert.Equal(["2115"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, type: 28))));
        Assert.Empty(Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, period: "2026/09", date: "2026-09-25"))));
    }

    [Fact]
    public async Task A_tambero_the_registry_holds_inactive_is_refused_and_one_it_does_not_hold_is_accepted()
    {
        await using var lum = await StartAsync();
        (await lum.Sim.Http.PutAsJsonAsync("/arcasim/api/taxpayers/20333333334",
            new { name = "Tambo de Baja", vatCondition = "ResponsableInscripto", active = false, pointsOfSale = Array.Empty<object>() })).EnsureSuccessStatusCode();

        Assert.Equal(["2103"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, tambero: 20333333334))));
        Assert.Empty(Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, tambero: 20444444445))));
    }

    [Fact]
    public async Task Points_of_sale_come_from_the_issuer_and_one_it_lacks_is_refused()
    {
        await using var lum = await StartAsync();

        Assert.Equal(["1", "3000"], (await lum.CallAsync("consultarPuntosVenta")).Elements("puntoVenta").Select(p => p.Element("codigo")!.Value));
        Assert.Empty((await lum.CallAsync("consultarPuntosVenta", auth: await lum.AuthForAsync(Producer))).Elements("puntoVenta"));
        Assert.Equal(["2086"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, pointOfSale: 7))));
        Assert.Equal(["2082"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1), await lum.AuthForAsync(Producer))));
    }

    [Fact]
    public async Task A_credit_note_adjusts_the_liquidation_by_its_CAE_on_its_own_sequence()
    {
        await using var lum = await StartAsync();
        var cae = (await lum.CallAsync("generarLiquidacion", Liquidation(1))).Descendants("cae").First().Value;
        string Monetary(string target) => $"<ajuste><tipoAjuste>MONETARIO</tipoAjuste><caeAAjustar>{target}</caeAAjustar></ajuste>";

        Assert.Equal(["1000"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, type: 48))));
        Assert.Equal(["1003"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(2, adjustment: Monetary(cae)))));
        Assert.Equal(["1005"], Errors(await lum.CallAsync("generarLiquidacion", Liquidation(1, type: 48, adjustment: Monetary(cae)))));
        Assert.Equal(["2004"], Errors(await lum.CallAsync("generarLiquidacion",
            Liquidation(1, type: 48, adjustment: Monetary("12345678901234"), physical: false, bonus: "<importe>5000</importe>"))));

        var note = (await lum.CallAsync("generarLiquidacion",
            Liquidation(1, type: 48, adjustment: Monetary(cae), physical: false, bonus: "<importe>5000</importe>"))).Element("liquidacion")!;
        Assert.Equal("48", note.Element("encabezado")!.Element("tipoComprobante")!.Value);
        Assert.Equal(cae, note.Element("ajuste")!.Element("caeAAjustar")!.Value);
        Assert.Equal("1", (await lum.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(48))).Element("nroComprobante")!.Value);
        Assert.Equal("1", (await lum.CallAsync("consultarUltimoNroComprobantePorPtoVta", Last(27))).Element("nroComprobante")!.Value);
    }
}
