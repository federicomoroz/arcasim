using System.Xml.Linq;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>
/// What wsbfev1 and wsseg check the same way and refuse with their own codes:
/// the currency, CanMisMonExt and the rate, the receiver's VAT condition, the
/// rate query and the conditions annex. One scenario per refusal, with the
/// code each manual gives it.
/// </summary>
public class AsmxSharedChecksTests
{
    private const string Dolar = "DOL";

    private static string Value(XElement answer, string name) => answer.Descendants().First(e => e.Name.LocalName == name).Value;

    // ---- wsbfev1 ------------------------------------------------------------------------

    private static Task<ServiceDesk> OpenBfeAsync(ArcaSimHarness sim) => ServiceDesk.OpenAsync(sim, "wsbfev1-homologacion.wsdl", "wsbfe", "x");

    private static string BfeAuth(ServiceDesk desk, string name = "Auth") =>
        $"<x:{name}><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit></x:{name}>";

    private static string BfeCmp(long id, string currency = "PES", string? rate = "1", string flag = "", string condition = "1", int type = 1, long number = 1) =>
        $"<x:Cmp><x:Id>{id}</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>20888888883</x:Nro_doc><x:Zona>1</x:Zona><x:Tipo_cbte>{type}</x:Tipo_cbte>" +
        $"<x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>{number}</x:Cbte_nro><x:Imp_total>1754.50</x:Imp_total><x:Imp_tot_conc>0.00</x:Imp_tot_conc>" +
        "<x:Imp_neto>1450.00</x:Imp_neto><x:Impto_liq>304.50</x:Impto_liq><x:Impto_liq_rni>0.00</x:Impto_liq_rni><x:Imp_op_ex>0.00</x:Imp_op_ex>" +
        "<x:Imp_perc>0.00</x:Imp_perc><x:Imp_iibb>0.00</x:Imp_iibb><x:Imp_perc_mun>0.00</x:Imp_perc_mun><x:Imp_internos>0.00</x:Imp_internos>" +
        $"<x:Imp_moneda_Id>{currency}</x:Imp_moneda_Id>{(rate is null ? "" : $"<x:Imp_moneda_ctz>{rate}</x:Imp_moneda_ctz>")}<x:Fecha_cbte>20261001</x:Fecha_cbte>" +
        $"{(condition.Length == 0 ? "" : $"<x:CondicionIVAReceptorId>{condition}</x:CondicionIVAReceptorId>")}{flag}" +
        "<x:Items><x:Item><x:Pro_codigo_ncm>2101.11.10</x:Pro_codigo_ncm><x:Pro_codigo_sec></x:Pro_codigo_sec><x:Pro_ds>Cafe</x:Pro_ds><x:Pro_qty>10.00</x:Pro_qty>" +
        "<x:Pro_umed>5</x:Pro_umed><x:Pro_precio_uni>150.00</x:Pro_precio_uni><x:Imp_bonif>50.00</x:Imp_bonif><x:Imp_total>1754.50</x:Imp_total><x:Iva_id>5</x:Iva_id></x:Item></x:Items>" +
        "</x:Cmp>";

    private static async Task<string> BfeAuthorizeAsync(ServiceDesk desk, string cmp) =>
        Value(await desk.CallAsync("BFEAuthorize", BfeAuth(desk) + cmp), "ErrCode");

    [Fact]
    public async Task Wsbfev1_refuses_the_currency_the_rate_and_the_receiver_with_its_codes()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenBfeAsync(sim);
        const string same = "<x:CanMisMonExt>S</x:CanMisMonExt>";

        Assert.Equal("1014", await BfeAuthorizeAsync(desk, BfeCmp(1, currency: "XXX")));
        Assert.Equal("4959", await BfeAuthorizeAsync(desk, BfeCmp(1, flag: "<x:CanMisMonExt>X</x:CanMisMonExt>")));
        Assert.Equal("4958", await BfeAuthorizeAsync(desk, BfeCmp(1, flag: same)));
        Assert.Equal("4957", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: null)));
        Assert.Equal("4957", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: "0")));
        Assert.Equal("4957", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: null, flag: same)));

        await desk.SetRateAsync(Dolar, new DateOnly(2026, 9, 30), 1450.5m);
        Assert.Equal("4957", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: null, flag: same, type: 2)));
        Assert.Equal("4960", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: "1451.51")));
        Assert.Equal("0", await BfeAuthorizeAsync(desk, BfeCmp(1, Dolar, rate: "1451.5")));
        Assert.Equal("0", await BfeAuthorizeAsync(desk, BfeCmp(2, Dolar, rate: null, flag: same, number: 2)));

        Assert.Equal("4963", await BfeAuthorizeAsync(desk, BfeCmp(3, condition: "")));
        Assert.Equal("4961", await BfeAuthorizeAsync(desk, BfeCmp(3, condition: "abc")));
        Assert.Equal("4961", await BfeAuthorizeAsync(desk, BfeCmp(3, condition: "2")));
        Assert.Equal("4962", await BfeAuthorizeAsync(desk, BfeCmp(3, condition: "5")));
    }

    [Fact]
    public async Task Wsbfev1_answers_the_rate_query_with_4965_4966_4964_and_the_annex_with_4967()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenBfeAsync(sim);
        await desk.SetRateAsync(Dolar, new DateOnly(2026, 9, 30), 1450.5m);

        async Task<XElement> Quote(string inner) => await desk.CallAsync("BFEGetCotizacion", BfeAuth(desk) + inner);

        Assert.Equal("4965", Value(await Quote("<x:FchCotiz>20261001</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("4965", Value(await Quote("<x:MonId>XXX</x:MonId>"), "ErrCode"));
        Assert.Equal("4966", Value(await Quote("<x:MonId>DOL</x:MonId><x:FchCotiz>01/10/2026</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("4964", Value(await Quote("<x:MonId>060</x:MonId><x:FchCotiz>20261001</x:FchCotiz>"), "ErrCode"));
        Assert.Equal("1", Value(await Quote("<x:MonId>PES</x:MonId><x:FchCotiz>20261001</x:FchCotiz>"), "MonCotiz"));
        Assert.Equal("1450.5", Value(await Quote("<x:MonId>DOL</x:MonId>"), "MonCotiz"));

        var annex = await desk.CallAsync("BFEGetPARAM_CondicionIvaReceptor", BfeAuth(desk, "auth"));
        var wrong = await desk.CallAsync("BFEGetPARAM_CondicionIvaReceptor", BfeAuth(desk, "auth") + "<x:ClaseCmp>C</x:ClaseCmp>");
        Assert.Equal(["1", "4", "5", "6", "7", "8", "9", "10", "13", "15", "16"], annex.Descendants(desk.Ns + "Id").Select(e => e.Value));
        Assert.Equal(["A", "B", "B", "A", "B", "B", "B", "B", "A", "B", "A"], annex.Descendants(desk.Ns + "Cmp_Clase").Select(e => e.Value));
        Assert.Equal("4967", Value(wrong, "ErrCode"));
    }

    // ---- wsseg --------------------------------------------------------------------------

    private static Task<ServiceDesk> OpenSegAsync(ArcaSimHarness sim) => ServiceDesk.OpenAsync(sim, "wsseg-homologacion.wsdl", "wsseg", "x");

    private static string SegAuth(ServiceDesk desk, string name = "Auth") =>
        $"<x:{name}><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit></x:{name}>";

    private static string SegCmp(long id, string currency = "PES", string? rate = "1", string flag = "", string condition = "1", int type = 1, long number = 1) =>
        $"<x:Cmp><x:Id>{id}</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>33693450239</x:Nro_doc><x:Tipo_cbte>{type}</x:Tipo_cbte><x:Punto_vta>1900</x:Punto_vta>" +
        $"<x:Cbte_nro>{number}</x:Cbte_nro><x:Imp_total>1210</x:Imp_total><x:Imp_tot_conc>0</x:Imp_tot_conc><x:Imp_neto>1000</x:Imp_neto>" +
        "<x:Impto_liq>210</x:Impto_liq><x:Impto_liq_rni>0</x:Impto_liq_rni><x:Imp_op_ex>0</x:Imp_op_ex><x:Imp_perc>0</x:Imp_perc><x:Imp_iibb>0</x:Imp_iibb>" +
        $"<x:Imp_perc_mun>0</x:Imp_perc_mun><x:Imp_internos>0</x:Imp_internos><x:Imp_moneda_Id>{currency}</x:Imp_moneda_Id>" +
        $"{(rate is null ? "" : $"<x:Imp_moneda_ctz>{rate}</x:Imp_moneda_ctz>")}<x:Imp_otrib_prov>0</x:Imp_otrib_prov><x:Fecha_cbte>20261001</x:Fecha_cbte>" +
        $"{(condition.Length == 0 ? "" : $"<x:CondicionIVAReceptorId>{condition}</x:CondicionIVAReceptorId>")}{flag}" +
        "<x:Items><x:Item><x:Poliza>SESC8302/2009</x:Poliza><x:Endoso>843220/23</x:Endoso><x:Ds>TRC5 caucion contra ARCA por 100kusd </x:Ds><x:Qty>1</x:Qty>" +
        "<x:Precio_uni>1200</x:Precio_uni><x:Imp_bonif>200</x:Imp_bonif><x:Imp_total>1000</x:Imp_total><x:Imp_valor_aseg>100000</x:Imp_valor_aseg><x:Iva_id>5</x:Iva_id></x:Item></x:Items>" +
        "</x:Cmp>";

    private static async Task<string> SegAuthorizeAsync(ServiceDesk desk, string cmp) =>
        Value(await desk.CallAsync("SEGAuthorize", SegAuth(desk) + cmp), "ErrCode");

    [Fact]
    public async Task Wsseg_refuses_the_currency_the_rate_and_the_receiver_with_its_codes()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenSegAsync(sim);
        const string same = "<x:CanMisMonExt>S</x:CanMisMonExt>";

        Assert.Equal("1014", await SegAuthorizeAsync(desk, SegCmp(1, currency: "XXX")));
        Assert.Equal("1033", await SegAuthorizeAsync(desk, SegCmp(1, flag: "<x:CanMisMonExt>X</x:CanMisMonExt>")));
        Assert.Equal("1035", await SegAuthorizeAsync(desk, SegCmp(1, flag: same)));
        Assert.Equal("1014", await SegAuthorizeAsync(desk, SegCmp(1, Dolar, rate: null)));
        Assert.Equal("1014", await SegAuthorizeAsync(desk, SegCmp(1, Dolar, rate: "0")));
        Assert.Equal("1014", await SegAuthorizeAsync(desk, SegCmp(1, Dolar, rate: null, flag: same)));

        await desk.SetRateAsync(Dolar, new DateOnly(2026, 9, 30), 1450.5m);
        Assert.Equal("1034", await SegAuthorizeAsync(desk, SegCmp(1, Dolar, rate: "1451.51")));
        Assert.Equal("0", await SegAuthorizeAsync(desk, SegCmp(1, Dolar, rate: "1451.5")));
        Assert.Equal("0", await SegAuthorizeAsync(desk, SegCmp(2, Dolar, rate: null, flag: same, type: 2)));

        Assert.Equal("1032", await SegAuthorizeAsync(desk, SegCmp(3, condition: "")));
        Assert.Equal("1030", await SegAuthorizeAsync(desk, SegCmp(3, condition: "abc")));
        Assert.Equal("1030", await SegAuthorizeAsync(desk, SegCmp(3, condition: "2")));
        Assert.Equal("1031", await SegAuthorizeAsync(desk, SegCmp(3, condition: "5")));
    }

    [Fact]
    public async Task Wsseg_answers_the_annex_and_refuses_another_class_with_1002()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenSegAsync(sim);

        var annex = await desk.CallAsync("SEGGetCondicionIvaReceptor", SegAuth(desk, "auth"));
        var classB = await desk.CallAsync("SEGGetCondicionIvaReceptor", SegAuth(desk, "auth") + "<x:ClaseCmp>B</x:ClaseCmp>");
        var wrong = await desk.CallAsync("SEGGetCondicionIvaReceptor", SegAuth(desk, "auth") + "<x:ClaseCmp>C</x:ClaseCmp>");
        var pesos = await desk.CallAsync("SEGGetPARAM_Ctz", SegAuth(desk, "auth") + "<x:MonId>PES</x:MonId><x:FchCotiz>20261001</x:FchCotiz>");

        Assert.Equal(["1", "4", "5", "6", "7", "8", "9", "10", "13", "15", "16"], annex.Descendants(desk.Ns + "Id").Select(e => e.Value));
        Assert.Equal(["4", "5", "7", "8", "9", "10", "15"], classB.Descendants(desk.Ns + "Id").Select(e => e.Value));
        Assert.Equal("1002", Value(wrong, "ErrCode"));
        Assert.Equal("1", Value(pesos, "MonCotiz"));
    }
}
