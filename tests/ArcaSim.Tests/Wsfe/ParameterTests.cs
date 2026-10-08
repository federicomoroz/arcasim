using ArcaSim.Application.Wsfe;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Wsfe;

public class ParameterTests
{
    [Fact]
    public async Task Points_of_sale_list_only_web_service_ones_with_the_literal_NULL_for_no_end_date()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;

        var (_, body) = await sim.PostWsfeAsync("FEParamGetPtosVenta", await sim.WsfeAuthAsync());

        Assert.Contains("<ResultGet><PtoVenta><Nro>1</Nro><EmisionTipo>CAE - Ri Iva</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>NULL</FchBaja></PtoVenta><PtoVenta><Nro>900</Nro><EmisionTipo>CAEA - Ri Iva</EmisionTipo>", body);
    }

    [Fact]
    public async Task Receiver_conditions_can_be_asked_for_one_voucher_class_and_an_unknown_class_is_10244()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var auth = await sim.WsfeAuthAsync();

        var (_, classA) = await sim.PostWsfeAsync("FEParamGetCondicionIvaReceptor", auth + "<ar:ClaseCmp>A</ar:ClaseCmp>");
        var (_, unknown) = await sim.PostWsfeAsync("FEParamGetCondicionIvaReceptor", auth + "<ar:ClaseCmp>Z</ar:ClaseCmp>");

        Assert.Contains("<CondicionIvaReceptor><Id>1</Id><Desc>IVA Responsable Inscripto</Desc><Cmp_Clase>A</Cmp_Clase></CondicionIvaReceptor>", classA);
        Assert.DoesNotContain("<Id>5</Id>", classA);
        Assert.Contains("<Code>10244</Code>", unknown);
    }

    [Fact]
    public async Task The_exchange_rate_is_the_last_one_loaded_before_the_day_asked()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var auth = await sim.WsfeAuthAsync();
        var rate = await sim.Http.PutAsync("/arcasim/api/rates", System.Net.Http.Json.JsonContent.Create(new { currency = "DOL", day = "2026-09-30", rate = 1385.5m }));
        rate.EnsureSuccessStatusCode();

        var (_, dollar) = await sim.PostWsfeAsync("FEParamGetCotizacion", auth + "<ar:MonId>DOL</ar:MonId>");
        var (_, unknown) = await sim.PostWsfeAsync("FEParamGetCotizacion", auth + "<ar:MonId>XYZ</ar:MonId>");

        Assert.Contains("<ResultGet><MonId>DOL</MonId><MonCotiz>1385.5</MonCotiz><FchCotiz>20260930</FchCotiz></ResultGet>", dollar);
        Assert.Contains("<Code>12000</Code>", unknown);
    }

    /// <summary>Every rule points at a code that really is in its method's table, so a typo cannot slip into an answer.</summary>
    [Fact]
    public void Every_rule_code_exists_in_the_manual_s_table_for_its_method()
    {
        var catalog = ValidationCatalog.Load();
        var known = catalog.Codes.ToHashSet();

        var missing = RuleCodes.All
            .SelectMany(r => new[] { (RuleCodes.Cae, r.Cae), (RuleCodes.Caea, r.Caea) })
            .Where(p => p.Item2 is not null && !known.Contains((p.Item1, p.Item2!.Value)))
            .ToList();

        Assert.Empty(missing);
    }
}
