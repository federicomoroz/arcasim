using ArcaSim.Application.Services.Organismos;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Beneficios en créditos y débitos bancarios: a bank's accounts, their states at a date, news, and the manual's errors.</summary>
public class IcdbRulesTests
{
    private const string Active = "9990001800000000000116";
    private const string Closed = "9990001800000000000222";

    [Fact]
    public async Task An_accounts_state_at_a_date_follows_its_history_and_a_change_is_news()
    {
        await using var sim = ArcaSimHarness.Start();
        var icdb = await ServiceProbe.StartAsync(sim, "wsicdb");

        Assert.Equal("AC", (await StateAsync(icdb, 20333333334, Closed, "2026-08-15")).Valid().Value("codigoEstado"));
        Assert.Equal("BA", (await StateAsync(icdb, 20333333334, Closed, "2026-09-15")).Valid().Value("codigoEstado"));
        var registered = (await icdb.CallAsync("consultarInscriptosRegistro", Auth(icdb))).Valid();
        Assert.Equal([Active], registered.All("cbu").Select(c => c.Value));

        var key = IcdbRegistry.Key(ServiceProbe.Caller, 20555555556, Active);
        var account = (await icdb.Store.GetAsync<BankAccount>(IcdbRegistry.Accounts, key))!;
        await icdb.Store.PutAsync(IcdbRegistry.Accounts, key, account with { History = [.. account.History, new AccountState("UI", new DateOnly(2026, 9, 30))] });

        var news = (await icdb.CallAsync("consultarNovedadesPorFecha", Auth(icdb) + "<solicitud><fecha>2026-09-30</fecha></solicitud>")).Valid();
        Assert.Equal("UI", news.Value("codigoEstado"));
        Assert.Equal("20555555556", news.Value("cuit"));
        Assert.Empty((await icdb.CallAsync("consultarInscriptosRegistro", Auth(icdb))).Valid().All("registro"));
    }

    [Theory]
    [InlineData(20555555556, "9990001800000000000117", "2026-09-15", "1003", "El número de CBU es inválido.")]
    [InlineData(20555555556, Active, "2026-10-02", "1002", "La fecha a consultar no debe ser posterior al día actual.")]
    [InlineData(30666666662, Active, "2026-09-15", "1001", "La cuitCliente ingresada, pertenece a un ente público Exento por Ley 25.413 art. 2.")]
    [InlineData(20555555557, Active, "2026-09-15", "1000", "La CUIT ingresada es inválida o inexistente.")]
    [InlineData(20555555556, Active, "2024-01-01", "1004", "No existen registros según los parámetros de búsqueda ingresados.")]
    public async Task A_query_the_manual_refuses_carries_its_error(long cuit, string cbu, string date, string code, string text)
    {
        await using var sim = ArcaSimHarness.Start();
        var icdb = await ServiceProbe.StartAsync(sim, "wsicdb");

        var answer = (await StateAsync(icdb, cuit, cbu, date)).Valid();

        Assert.Equal(code, answer.Value("codigo"));
        Assert.Equal(text, answer.Value("descripcion"));
    }

    [Fact]
    public async Task States_benefits_and_exempt_entities_come_from_their_tables()
    {
        await using var sim = ArcaSimHarness.Start();
        var icdb = await ServiceProbe.StartAsync(sim, "wsicdb");

        var states = (await icdb.CallAsync("consultarEstados", Auth(icdb))).Valid();
        Assert.Equal(["AC", "BA", "EX", "UI"], states.All("codigo").Select(c => c.Value));
        Assert.Equal("Anexo del Decreto N° 380/2001", (await icdb.CallAsync("consultarBeneficios", Auth(icdb))).Valid().Value("norma"));
        var entity = (await icdb.CallAsync("consultarEnteExentoLey25413", Auth(icdb) + "<solicitud><cuitCliente>30666666662</cuitCliente></solicitud>")).Valid();
        Assert.Equal("ENTE PUBLICO FICTICIO DE ARCASIM", entity.Value("razonSocial"));
        Assert.Single((await icdb.CallAsync("consultarEntesExentosLey25413", Auth(icdb))).Valid().All("ente"));
    }

    private static Task<SoapAnswer> StateAsync(ServiceProbe icdb, long cuit, string cbu, string date) =>
        icdb.CallAsync("consultarEstadoCuentaPorFecha", Auth(icdb) +
                                                       $"<solicitud><cuitCliente>{cuit}</cuitCliente><cbu>{cbu}</cbu><fecha>{date}</fecha></solicitud>");

    private static string Auth(ServiceProbe icdb) =>
        $"<auth><token>{icdb.Token}</token><sign>{icdb.Sign}</sign><cuit>{ServiceProbe.Caller}</cuit></auth>";
}
