using ArcaSim.Application.Services.Organismos;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Mi Argentina: a CUIL's working life, preloaded or fictitious, with a growing transaction id.</summary>
public class MiArgentinaRulesTests
{
    [Fact]
    public async Task A_preloaded_worker_shows_its_employments_and_each_answer_has_a_new_id()
    {
        await using var sim = ArcaSimHarness.Start();
        var mi = await ServiceProbe.StartAsync(sim, "miargentina-ws");
        await mi.Store.PutAsync(MiArgentinaRules.Workers, "20222222223", new Worker(20222222223, "ANA", "GOMEZ",
            [new Employment(30000000007, "DISTRIBUIDORA DEL PLATA S.A.", new DateOnly(2022, 5, 2), "RUTA 22 KM 1200", 0, "08/2026", "TIEMPO INDETERMINADO", 1, 2, 3,
                3002, "No se registran pagos efectuados por tu empleador")],
            [new HouseholdEmployment(20111111112, "EMPRESA DE PRUEBA SA", "OSDE", new DateOnly(2025, 1, 6), "CALLE 1", "CASEROS", "12", "PERMANENTE", "ART DE PRUEBA", "09/2026", 0, "")]));

        var first = (await QueryAsync(mi, 20222222223)).Valid();
        var second = (await QueryAsync(mi, 20222222223)).Valid();

        Assert.Equal("0", first.Value("Codigo"));
        Assert.Equal(int.Parse(first.Value("Id")) + 1, int.Parse(second.Value("Id")));
        Assert.Equal("GOMEZ", first.Value("Apellido"));
        var job = first.All("MisAportes").Single().All("Item").Single();
        Assert.Equal("3002", job.Value("CodigoMensaje"));
        Assert.Equal("02/05/2022", job.Value("FechaInicioRelacionLaboral"));
        Assert.Equal("3", job.Value("EstadoContribucionesObraSocial"));
        Assert.Equal("CASEROS", first.All("MisAportesCasasParticulares").Single().Value("CategoriaProfesional"));
    }

    [Fact]
    public async Task An_unknown_CUIL_gets_a_fictitious_employment_and_a_bad_one_arcasims_own_code()
    {
        await using var sim = ArcaSimHarness.Start();
        var mi = await ServiceProbe.StartAsync(sim, "miargentina-ws");
        await sim.PutTaxpayerAsync(27000000006, "Laura Perez", VatCondition.ConsumidorFinal);

        var known = (await QueryAsync(mi, 27000000006)).Valid();
        Assert.Equal("PEREZ", known.Value("Apellido"));
        Assert.Equal("Última vez que fuiste declarado por este empleador: Período 09/2026", known.All("Item").Single().Value("Mensaje"));

        var bad = (await QueryAsync(mi, 27000000007)).Valid();
        Assert.Equal("9000", bad.Value("Codigo"));
        Assert.Empty(bad.All("Datos"));
    }

    private static Task<SoapAnswer> QueryAsync(ServiceProbe mi, long cuil) =>
        mi.CallAsync("ObtenerInformacionLaboral",
            $"<credencial><Token>{mi.Token}</Token><Sign>{mi.Sign}</Sign><CUITrepresentada>{ServiceProbe.Caller}</CUITrepresentada></credencial><cuil>{cuil}</cuil>",
            qualified: true);
}
