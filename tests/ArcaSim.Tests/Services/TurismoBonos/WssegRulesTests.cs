using System.Xml.Linq;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>Seguros de caución: vouchers with policy and endorsement, the Id reproceso, the retirement notice, and answers valid for wsseg's WSDL.</summary>
public class WssegRulesTests
{
    private static Task<ServiceDesk> OpenAsync(ArcaSimHarness sim) => ServiceDesk.OpenAsync(sim, "wsseg-homologacion.wsdl", "wsseg", "x");

    private static string Auth(ServiceDesk desk, string name = "Auth") =>
        $"<x:{name}><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit></x:{name}>";

    /// <summary>The manual's second example (a 100k USD caución, 1000 plus 21 % VAT), dated in ArcaSim's today.</summary>
    private static string Cmp(long id, long number, string condition = "<x:CondicionIVAReceptorId>1</x:CondicionIVAReceptorId>", int type = 1) =>
        $"<x:Cmp><x:Id>{id}</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>33693450239</x:Nro_doc><x:Tipo_cbte>{type}</x:Tipo_cbte><x:Punto_vta>1900</x:Punto_vta>" +
        $"<x:Cbte_nro>{number}</x:Cbte_nro><x:Imp_total>1210</x:Imp_total><x:Imp_tot_conc>0</x:Imp_tot_conc><x:Imp_neto>1000</x:Imp_neto>" +
        "<x:Impto_liq>210</x:Impto_liq><x:Impto_liq_rni>0</x:Impto_liq_rni><x:Imp_op_ex>0</x:Imp_op_ex><x:Imp_perc>0</x:Imp_perc><x:Imp_iibb>0</x:Imp_iibb>" +
        "<x:Imp_perc_mun>0</x:Imp_perc_mun><x:Imp_internos>0</x:Imp_internos><x:Imp_moneda_Id>PES</x:Imp_moneda_Id><x:Imp_moneda_ctz>1</x:Imp_moneda_ctz>" +
        $"<x:Imp_otrib_prov>0</x:Imp_otrib_prov><x:Fecha_cbte>20261001</x:Fecha_cbte>{condition}" +
        "<x:Items><x:Item><x:Poliza>SESC8302/2009</x:Poliza><x:Endoso>843220/23</x:Endoso><x:Ds>TRC5 caucion contra ARCA por 100kusd </x:Ds><x:Qty>1</x:Qty>" +
        "<x:Precio_uni>1200</x:Precio_uni><x:Imp_bonif>200</x:Imp_bonif><x:Imp_total>1000</x:Imp_total><x:Imp_valor_aseg>100000</x:Imp_valor_aseg><x:Iva_id>5</x:Iva_id></x:Item></x:Items>" +
        "</x:Cmp>";

    private static Task<XElement> AuthorizeAsync(ServiceDesk desk, string cmp) => desk.CallAsync("SEGAuthorize", Auth(desk) + cmp);

    private static string Value(XElement answer, string name) => answer.Descendants().First(e => e.Name.LocalName == name).Value;

    [Fact]
    public async Task A_caucion_gets_a_CAE_moves_the_last_number_and_Id_and_keeps_its_policy()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var authorized = await AuthorizeAsync(desk, Cmp(45, 1));
        Assert.Equal("0", Value(authorized, "ErrCode"));
        Assert.Equal("A", Value(authorized, "Resultado"));
        Assert.Equal("N", Value(authorized, "Reproceso"));
        Assert.Equal("20261001", Value(authorized, "Fch_cbte"));

        var last = await desk.CallAsync("SEGGetLast_CMP",
            $"<x:Auth><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit><x:Pto_venta>1900</x:Pto_venta><x:Tipo_cbte>1</x:Tipo_cbte></x:Auth>");
        Assert.Equal("1", Value(last, "Cbte_nro"));
        Assert.Equal("20261001", Value(last, "Cbte_fecha"));
        Assert.Equal("45", Value(await desk.CallAsync("SEGGetLast_ID", Auth(desk)), "Id"));

        var consulted = await desk.CallAsync("SEGGetCMP", Auth(desk) + "<x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>1900</x:Punto_vta><x:Cbte_nro>1</x:Cbte_nro></x:Cmp>");
        Assert.Equal(Value(authorized, "Cae"), Value(consulted, "Cae"));
        Assert.Equal("SESC8302/2009", Value(consulted, "Poliza"));
        Assert.Equal("843220/23", Value(consulted, "Endoso"));
        Assert.Equal("100000", Value(consulted, "Imp_valor_aseg"));

        var again = await AuthorizeAsync(desk, Cmp(45, 1));
        Assert.Equal("S", Value(again, "Reproceso"));
        Assert.Equal(Value(authorized, "Cae"), Value(again, "Cae"));
    }

    [Fact]
    public async Task The_receivers_VAT_condition_is_required_and_must_match_the_class()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var missing = await AuthorizeAsync(desk, Cmp(45, 1, condition: ""));
        var wrongClass = await AuthorizeAsync(desk, Cmp(45, 1, condition: "<x:CondicionIVAReceptorId>5</x:CondicionIVAReceptorId>"));
        var unknown = await AuthorizeAsync(desk, Cmp(45, 1, condition: "<x:CondicionIVAReceptorId>2</x:CondicionIVAReceptorId>"));

        Assert.Equal("1032", Value(missing, "ErrCode"));
        Assert.Equal("1031", Value(wrongClass, "ErrCode"));
        Assert.Equal("1030", Value(unknown, "ErrCode"));
        Assert.Equal("0", Value(await desk.CallAsync("SEGGetLast_ID", Auth(desk)), "Id"));
    }

    [Fact]
    public async Task Homologacion_carries_the_catalogs_event_39_on_every_answer()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var authorized = await AuthorizeAsync(desk, Cmp(45, 1));
        var types = await desk.CallAsync("SEGGetPARAM_Tipo_Cbte", Auth(desk, "auth"));

        foreach (var answer in new[] { authorized, types })
        {
            Assert.Equal("39", Value(answer, "EventCode"));
            Assert.StartsWith("IMPORTANTE: Por motivos de mantenimiento", Value(answer, "EventMsg"));
        }
    }

    [Fact]
    public async Task Production_announces_the_retirement_with_event_47_on_every_answer()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        sim.Settings.Environment = ArcaEnvironment.Produccion;

        var authorized = await AuthorizeAsync(desk, Cmp(45, 1));
        var types = await desk.CallAsync("SEGGetPARAM_Tipo_Cbte", Auth(desk, "auth"));

        foreach (var answer in new[] { authorized, types })
        {
            Assert.Equal("47", Value(answer, "EventCode"));
            Assert.StartsWith("El servicio WSSEG sera dado de baja.", Value(answer, "EventMsg"));
            Assert.Contains("Resolución General Nro 5866/2026", Value(answer, "EventMsg"));
        }
        Assert.Equal(["1", "2", "3", "6", "7", "8"], types.Descendants(desk.Ns + "Cbte_Id").Select(e => e.Value));
    }

    [Fact]
    public async Task The_rate_query_answers_1004_1003_1006_and_the_rates_ArcaSim_has()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        await desk.SetRateAsync("DOL", new DateOnly(2026, 9, 30), 1450.5m);

        async Task<XElement> Quote(string inner) => await desk.CallAsync("SEGGetPARAM_Ctz", Auth(desk, "auth") + inner);

        Assert.Equal("1004", Value(await Quote("<x:FchCotiz>20261001</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("1003", Value(await Quote("<x:MonId>XXX</x:MonId><x:FchCotiz>20261001</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("1006", Value(await Quote("<x:MonId>060</x:MonId><x:FchCotiz>20261001</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("1005", Value(await Quote("<x:MonId>DOL</x:MonId><x:FchCotiz>01/10/2026</x:FchCotiz>"), "ErrCode"));
        var rate = await Quote("<x:MonId>DOL</x:MonId><x:FchCotiz>20261001</x:FchCotiz>");
        Assert.Equal("1450.5", Value(rate, "MonCotiz"));
    }
}
