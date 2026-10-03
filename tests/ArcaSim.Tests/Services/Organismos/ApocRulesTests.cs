using ArcaSim.Application.Services.Organismos;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>APOC: the base of apócrifos, whole, by publication date and by CUIT.</summary>
public class ApocRulesTests
{
    [Fact]
    public async Task The_seeded_base_answers_by_CUIT_and_by_publication_date()
    {
        await using var sim = ArcaSimHarness.Start();
        var apoc = await ServiceProbe.StartAsync(sim, "wsapoc");

        var all = (await apoc.CallAsync("GetAll", Auth(apoc), qualified: true)).Valid();
        Assert.Equal("0", all.Value("codigo"));
        Assert.Equal(3, all.All("PublicacionAPOC").Count);

        var one = (await apoc.CallAsync("GetPublicacionAPOC", Auth(apoc) + "<cuit>20888888889</cuit>", qualified: true)).Valid();
        Assert.Equal("17/03/2025", one.Value("FechaPublicacion"));
        Assert.Equal("10/03/2025", one.Value("FechaCondicion"));

        var clean = (await apoc.CallAsync("GetPublicacionAPOC", Auth(apoc) + $"<cuit>{ServiceProbe.Caller}</cuit>", qualified: true)).Valid();
        Assert.Equal("0", clean.Value("codigo"));
        Assert.Empty(clean.All("PublicacionAPOC"));

        var september = (await apoc.CallAsync("GetAllByPublicacion", Auth(apoc) + "<desde>01/09/2026</desde><hasta>30/09/2026</hasta>", qualified: true)).Valid();
        Assert.Equal(["20777777778"], september.All("Cuit").Select(c => c.Value));
    }

    [Fact]
    public async Task A_malformed_date_is_error_200()
    {
        await using var sim = ArcaSimHarness.Start();
        var apoc = await ServiceProbe.StartAsync(sim, "wsapoc");

        var answer = (await apoc.CallAsync("GetAllByPublicacion", Auth(apoc) + "<desde>2026-09-01</desde>", qualified: true)).Valid();

        Assert.Equal("200", answer.Value("codigo"));
        Assert.Equal("Errores de validación de parámetros: la fecha 2026-09-01 no tiene el formato DD/MM/YYYY.", answer.Value("descripcion"));
    }

    [Fact]
    public async Task A_preloaded_base_replaces_the_seed()
    {
        await using var sim = ArcaSimHarness.Start();
        var apoc = await ServiceProbe.StartAsync(sim, "wsapoc");
        await apoc.Store.SkipSeedAsync(ApocRules.Cases);
        await apoc.Store.PutAsync(ApocRules.Cases, "30000000007", new ApocCase(30000000007, "CASO DE LA PRUEBA", new(2026, 9, 1), new(2026, 9, 2)));

        var all = (await apoc.CallAsync("GetAll", Auth(apoc), qualified: true)).Valid();

        Assert.Equal(["30000000007"], all.All("Cuit").Select(c => c.Value));
        Assert.Equal("CASO DE LA PRUEBA", all.Value("Descripcion"));
    }

    private static string Auth(ServiceProbe apoc) =>
        $"<Credencial><Token>{apoc.Token}</Token><Sign>{apoc.Sign}</Sign><CUITDelegado>{ServiceProbe.Caller}</CUITDelegado></Credencial>";
}
