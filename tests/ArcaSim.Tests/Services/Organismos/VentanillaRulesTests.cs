using ArcaSim.Application.Services.Organismos;
using ArcaSim.Infrastructure.InMemory;
using ArcaSim.Tests.Services.Aduana;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Ventanilla Electrónica: the inbox per CUIT, its filters and pages, reading that marks as read, and errors 100 to 111.</summary>
public class VentanillaRulesTests
{
    private const string Core = "http://core.tecno.afip.gov.ar/model/ws/types";
    private const long Caller = ServiceProbe.Caller;

    [Fact]
    public async Task A_new_inbox_has_three_unread_communications_and_reading_one_marks_it_read()
    {
        await using var sim = ArcaSimHarness.Start();
        var ve = await ServiceProbe.StartAsync(sim, "veconsumerws");

        var unread = (await ListAsync(ve, "<estado>1</estado><fechaDesde>2026-09-10</fechaDesde>")).Valid();
        Assert.Equal("3", unread.Value("totalItems"));
        var withAttachment = unread.All("ComunicacionSimplificada").Single(c => c.Value("tieneAdjunto") == "true");
        Assert.Equal("Comunicacion No Leida", withAttachment.Value("estadoDesc"));
        Assert.Equal("ARCASIM - NOTIFICACIONES DE PRUEBA", withAttachment.Value("sistemaPublicadorDesc"));
        var id = withAttachment.Value("idComunicacion");

        var read = (await ve.CallAsync("consumirComunicacion", Auth(ve) + $"<idComunicacion>{id}</idComunicacion><incluirAdjuntos>true</incluirAdjuntos>")).Valid();
        Assert.Equal("2", read.Value("estado"));
        Assert.EndsWith(".0", read.Value("fechaPublicacion"));
        Assert.Equal("constancia-arcasim.txt", read.Value("filename"));
        Assert.Equal("Adjunto ficticio generado por ArcaSim.", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(read.Value("content"))));

        Assert.Equal("2", (await ListAsync(ve, "<estado>1</estado><fechaDesde>2026-09-10</fechaDesde>")).Valid().Value("totalItems"));
        var now = (await ListAsync(ve, "<estado>2</estado><fechaDesde>2026-09-10</fechaDesde>")).Valid();
        Assert.Equal(id, now.Value("idComunicacion"));
        Assert.Equal("Comunicacion Leida", now.Value("estadoDesc"));
    }

    [Fact]
    public async Task Preloaded_communications_are_filtered_and_paged()
    {
        await using var sim = ArcaSimHarness.Start();
        var ve = await ServiceProbe.StartAsync(sim, "veconsumerws");
        var published = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.FromHours(-3));
        for (var i = 1; i <= 3; i++)
            await Inbox.PublishAsync(ve.Store, new Communication(0, Caller, published.AddDays(i), null, 2, null,
                $"Mensaje numero {i} de una intimacion ficticia que supera los cincuenta caracteres del asunto", 1, 1, i == 2 ? "EXP-2" : null, null, false, []));

        var page = (await ListAsync(ve, "<fechaDesde>2026-09-20</fechaDesde><fechaHasta>2026-10-01</fechaHasta><pagina>2</pagina><resultadosPorPagina>2</resultadosPorPagina>")).Valid();
        Assert.Equal("3", page.Value("totalItems"));
        Assert.Equal("2", page.Value("totalPaginas"));
        Assert.Single(page.All("ComunicacionSimplificada"));
        Assert.Equal("Mensaje numero 3 de una intimacion ficticia que su", page.Value("asunto"));

        var byReference = (await ListAsync(ve, "<fechaDesde>2026-09-20</fechaDesde><referencia1>EXP-2</referencia1>")).Valid();
        Assert.Equal("EXP-2", byReference.Value("referencia1"));
        Assert.Single(byReference.All("ComunicacionSimplificada"));
    }

    [Theory]
    [InlineData("<fechaDesde>01/09/2026</fechaDesde>", "Error 102: Formato de fecha no soportado para [01/09/2026]. Se esperaba [yyyy-MM-dd]")]
    [InlineData("<fechaDesde>2025-01-01</fechaDesde>", "Error 101: Fecha desde no soportada. Mínima fecha [06/10/25 00:00]")]
    [InlineData("<fechaDesde>2026-09-20</fechaDesde><fechaHasta>2026-09-10</fechaHasta>", "Error 108: Fecha desde [2026-09-20] se solapa con Fecha hasta [2026-09-10]")]
    [InlineData("<fechaDesde>2026-08-01</fechaDesde>", "Error 111: La cantidad de días no puede superar los [31] entre la fechaDesde y fechaHasta o entre la fechaDesde y la fecha actual")]
    [InlineData("<fechaDesde>2026-09-20</fechaDesde><comunicacionIdDesde>9</comunicacionIdDesde><comunicacionIdHasta>3</comunicacionIdHasta>", "Error 107: Id Comunicación desde [9] se solapa con Id Comunicación hasta [3]")]
    [InlineData("<estado>7</estado><fechaDesde>2026-09-20</fechaDesde>", "Error 103: Código de estado inválido [7]")]
    [InlineData("<fechaDesde>2026-09-20</fechaDesde><sistemaPublicadorId>4400</sistemaPublicadorId>", "Error 109: idSistema [4400] no es valido")]
    [InlineData("<fechaDesde>2026-09-20</fechaDesde><resultadosPorPagina>501</resultadosPorPagina>", "Error 106: Cantidad de ítems por página no válida [501]")]
    [InlineData("<fechaDesde>2026-09-20</fechaDesde><pagina>5</pagina>", "Error 100: Número de página inválida [5]")]
    public async Task A_filter_the_manual_forbids_is_refused_with_its_error(string filter, string expected)
    {
        await using var sim = ArcaSimHarness.Start();
        var ve = await ServiceProbe.StartAsync(sim, "veconsumerws");

        var answer = await ListAsync(ve, filter);

        Assert.Equal(500, answer.Status);
        Assert.Equal(expected, answer.Fault);
    }

    [Fact]
    public async Task Reading_refuses_what_does_not_exist_belongs_to_someone_else_or_is_internal()
    {
        await using var sim = ArcaSimHarness.Start();
        var ve = await ServiceProbe.StartAsync(sim, "veconsumerws");
        var other = await Inbox.PublishAsync(ve.Store, new Communication(0, 30000000007, sim.Clock.Now, null, 1, "Ajena", "Ajena", 2, 1, null, null, false, []));
        var mine = await Inbox.PublishAsync(ve.Store, new Communication(0, Caller, sim.Clock.Now, null, 1, "Interna", "Interna", 2, 1, null, null, true, []));

        Assert.Equal("Error 104: La Comunicación [999999] no existe", (await ReadAsync(ve, 999999)).Fault);
        Assert.Equal($"Error 105: La CUIT representada [{Caller}] no es la destinataria de la Comunicación indicada [{other.Id}]", (await ReadAsync(ve, other.Id)).Fault);
        Assert.Equal($"Error 110: La Comunicación por la que se está consultando [{mine.Id}] no es posible obtenerla a través de este servicio", (await ReadAsync(ve, mine.Id)).Fault);
        Assert.Equal("0", (await ListAsync(ve, "<fechaDesde>2026-09-20</fechaDesde>")).Valid().Value("totalItems"));
    }

    [Fact]
    public async Task States_and_publishing_systems_come_from_their_tables()
    {
        await using var sim = ArcaSimHarness.Start();
        var ve = await ServiceProbe.StartAsync(sim, "veconsumerws");

        var states = (await ve.CallAsync("consultarEstados", Auth(ve))).Valid();
        Assert.Equal(["1", "2", "0"], states.All("Estado").Select(s => s.Value("id")));
        Assert.Equal("Comunicacion sin procesar - No disponible", states.All("Estado")[2].Value("descripcion"));

        var systems = (await ve.CallAsync("consultarSistemasPublicadores", Auth(ve) + "<idSistemaPublicador>2</idSistemaPublicador>")).Valid();
        Assert.Equal("ARCASIM - RECORDATORIOS DE PRUEBA", systems.Value("descripcion"));
        Assert.Equal("vencimientos", systems.Value("nombre"));
        Assert.Equal("Error 109: idSistema [77] no es valido",
            (await ve.CallAsync("consultarSistemasPublicadores", Auth(ve) + "<idSistemaPublicador>77</idSistemaPublicador>")).Fault);
    }

    private static Task<SoapAnswer> ListAsync(ServiceProbe ve, string filter) =>
        ve.CallAsync("consultarComunicaciones", Auth(ve) + $"<filter>{filter}</filter>");

    private static Task<SoapAnswer> ReadAsync(ServiceProbe ve, long id) =>
        ve.CallAsync("consumirComunicacion", Auth(ve) + $"<idComunicacion>{id}</idComunicacion>");

    private static string Auth(ServiceProbe ve) =>
        $"<authRequest><c:token xmlns:c=\"{Core}\">{ve.Token}</c:token><c:sign xmlns:c=\"{Core}\">{ve.Sign}</c:sign>" +
        $"<c:cuitRepresentada xmlns:c=\"{Core}\">{Caller}</c:cuitRepresentada></authRequest>";

    [Fact]
    public async Task An_inbox_asked_for_by_requests_that_arrive_together_is_seeded_once()
    {
        var store = new YieldingDocumentStore(new InMemoryStore());
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => VentanillaInbox.SeedAsync(store, Caller, now, CancellationToken.None)));

        var inbox = await store.ListAsync<Communication>(VentanillaInbox.Communications);
        Assert.Equal([1L, 2L, 3L], inbox.Where(c => c.Cuit == Caller).Select(c => c.Id).Order());
    }
}
