using ArcaSim.Application.Services.Organismos;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Juegos de azar: machines declared by lot, daily summaries with their continuity rules, and counter requests.</summary>
public class JazaRulesTests
{
    private const long Caller = ServiceProbe.Caller;

    [Fact]
    public async Task A_machine_declared_by_lot_reports_consecutive_days_and_stops_being_pending()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");

        var lots = (await jaza.CallAsync("consultarLoteME", Auth(jaza) + "<ptoExplotacion>7</ptoExplotacion>")).Valid();
        Assert.Equal("TE", lots.Value("estado"));
        Assert.Equal("Cant. operaciones registradas: 1", lots.Value("observaciones"));
        Assert.Equal("2026-10-01-03:00", lots.Value("fechaEnvio"));

        Assert.Equal("2026-09-29", (await Pending(jaza)).Value("fecha"));
        Assert.Equal("A", (await DayAsync(jaza, "ME-01", "2026-09-29", 1, 1, (100, 5000), (180, 7400))).Valid().Value("resultado"));
        Assert.Equal("2026-09-30", (await Pending(jaza)).Value("fecha"));
        Assert.Equal("A", (await DayAsync(jaza, "ME-01", "2026-09-30", 1, 1, (180, 7400), (260, 9100))).Valid().Value("resultado"));
        Assert.Empty((await Pending(jaza)).All("idFechaMaquina"));

        var informed = (await jaza.CallAsync("consultarIdsMEInformadas", Auth(jaza) + "<nroPuntoExplotacion>7</nroPuntoExplotacion><fechaPresentacion>2026-09-30</fechaPresentacion>")).Valid();
        Assert.Equal(["ME-01"], informed.All("idMaquina").Select(m => m.Value));
        var detail = (await jaza.CallAsync("consultarMEInformada", Auth(jaza) +
            "<nroPuntoExplotacion>7</nroPuntoExplotacion><fechaPresentacion>2026-09-30</fechaPresentacion><idMaquina>ME-01</idMaquina>")).Valid();
        Assert.Equal("9100", detail.All("contadoresFinal").Single().Value("coinIn"));
    }

    [Fact]
    public async Task Days_must_be_reported_in_order_with_continuous_counters()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");

        var skipped = (await DayAsync(jaza, "ME-01", "2026-09-30", 1, 1, (0, 0), (10, 10))).Valid();
        Assert.Equal("R", skipped.Value("resultado"));
        Assert.Equal("1003", skipped.Value("codigo"));
        Assert.Equal("Antes de informar los datos para la fecha 30/09/2026 debe informar los datos para la fecha 29/09/2026", skipped.Value("descripcion"));

        (await DayAsync(jaza, "ME-01", "2026-09-29", 1, 1, (0, 0), (10, 10))).Valid();
        var broken = (await DayAsync(jaza, "ME-01", "2026-09-30", 1, 1, (11, 10), (20, 20))).Valid();
        Assert.Equal(["1011"], broken.All("codigo").Select(c => c.Value));

        var unknown = (await DayAsync(jaza, "ME-99", "2026-09-29", 1, 1, (0, 0), (1, 1))).Valid();
        Assert.Equal("1001", unknown.Value("codigo"));
    }

    [Fact]
    public async Task A_rectification_replaces_the_day_and_a_wrong_presentation_is_refused()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");
        (await DayAsync(jaza, "ME-01", "2026-09-29", 1, 1, (0, 0), (10, 10))).Valid();
        (await DayAsync(jaza, "ME-01", "2026-09-29", 1, 2, (10, 10), (15, 15))).Valid();

        Assert.Equal("A", (await DayAsync(jaza, "ME-01", "2026-09-29", 2, 1, (0, 0), (12, 12))).Valid().Value("resultado"));
        var detail = (await jaza.CallAsync("consultarMEInformada", Auth(jaza) +
            "<nroPuntoExplotacion>7</nroPuntoExplotacion><fechaPresentacion>2026-09-29</fechaPresentacion><idMaquina>ME-01</idMaquina>")).Valid();
        Assert.Single(detail.All("detalleMaquinaElectronica"));

        var wrong = (await DayAsync(jaza, "ME-01", "2026-09-29", 5, 1, (0, 0), (12, 12))).Valid();
        Assert.Equal("1009", wrong.Value("codigo"));
    }

    [Fact]
    public async Task A_sequence_time_without_an_offset_is_Argentinas_time_wherever_the_host_is()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");

        // It ends half an hour after it starts only if the end, which has no offset, is read as 10:30 at -03:00.
        var sequence = await DayAsync(jaza, "ME-01", "2026-09-29", 1, 1, (0, 0), (10, 10), start: "2026-09-29T10:00:00-03:00", end: "2026-09-29T10:30:00");
        var backwards = await DayAsync(jaza, "ME-01", "2026-09-29", 1, 2, (10, 10), (20, 20), start: "2026-09-29T11:00:00", end: "2026-09-29T10:00:00-03:00");

        Assert.Equal("A", sequence.Valid().Value("resultado"));
        Assert.Equal("1105", backwards.Valid().Value("codigo"));
    }

    [Fact]
    public async Task A_summary_that_breaks_several_rules_names_them_all_in_the_manuals_order()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");

        Assert.Equal([1020, 1003], await CodesAsync(jaza, "2026-10-02", 1, 1, (0, 0), (10, 10))); // in the future, and 09-29 to 10-01 are missing
        Assert.Equal([1002], await CodesAsync(jaza, "2026-09-28", 1, 1, (0, 0), (10, 10))); // before the machine started
        Assert.Equal([1101, 1102], await CodesAsync(jaza, "2026-09-29", 1, 1, (10, 10), (5, 5))); // games and coin-in went backwards
        Assert.Equal([1105], await CodesAsync(jaza, "2026-09-29", 1, 1, (0, 0), (10, 10), "2026-09-29T11:00:00-03:00", "2026-09-29T10:00:00-03:00"));
        Assert.Equal([1005], await CodesAsync(jaza, "2026-09-29", 2, 1, (0, 0), (10, 10))); // the first send of a date is presentation 1...
        Assert.Equal([1006], await CodesAsync(jaza, "2026-09-29", 1, 2, (0, 0), (10, 10))); // ...and sequence 1
        Assert.Equal([1005, 1006], await CodesAsync(jaza, "2026-09-29", 2, 2, (0, 0), (10, 10)));
    }

    [Fact]
    public async Task Further_sequences_and_rectifications_follow_the_manuals_continuity_rules()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await DeclareAsync(jaza, "ME-01");
        Assert.Equal([], await CodesAsync(jaza, "2026-09-29", 1, 1, (0, 0), (10, 10)));

        Assert.Equal([1007], await CodesAsync(jaza, "2026-09-29", 1, 3, (10, 10), (20, 20))); // the next sequence is 2
        Assert.Equal([1010], await CodesAsync(jaza, "2026-09-29", 1, 2, (10, 10), (20, 20), "2026-09-29T09:30:00-03:00", "2026-09-29T11:00:00-03:00"));
        Assert.Equal([1008], await CodesAsync(jaza, "2026-09-29", 2, 2, (0, 0), (10, 10))); // a rectification starts again at sequence 1
        Assert.Equal([1009], await CodesAsync(jaza, "2026-09-29", 3, 1, (0, 0), (10, 10)));
        Assert.Equal([1012], await CodesAsync(jaza, "2026-09-30", 1, 1, (10, 9), (20, 20))); // coin-in does not continue from the day before
    }

    [Fact]
    public async Task A_rectification_older_than_30_days_is_refused()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        var day = new DateOnly(2026, 8, 20);
        await jaza.Store.PutAsync(JazaRules.Machines, $"{Caller}/00007/ME-01", new GamingMachine(Caller, 7, "ME-01", day, true));
        await jaza.Store.PutAsync(JazaRules.Days, $"{Caller}/00007/ME-01/{day:yyyyMMdd}",
            new MachineDay(Caller, 7, "ME-01", day, 1, [new MachineSequence(1, "2026-08-20T09:00:00-03:00", "2026-08-20T10:00:00-03:00", "0.01", new MachineCounters(0, 0, 0, 0), new MachineCounters(10, 10, 0, 0))]));

        Assert.Equal([1004], await CodesAsync(jaza, "2026-08-20", 2, 1, (0, 0), (12, 12)));
    }

    [Fact]
    public async Task A_lot_that_modifies_an_unknown_machine_ends_with_line_errors()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");

        var lot = (await jaza.CallAsync("informarLoteME", Auth(jaza) + "<ptoExplotacion>7</ptoExplotacion><arrayME><me><oper>3</oper><uid>NADIE</uid></me></arrayME>")).Valid();
        Assert.Equal("A", lot.Value("resultado"));

        var lots = (await jaza.CallAsync("consultarLoteME", Auth(jaza) + $"<ptoExplotacion>7</ptoExplotacion><nroLoteDesde>{lot.Value("nroLote")}</nroLoteDesde>")).Valid();
        Assert.Equal("ER", lots.Value("estado"));
        Assert.Equal("No se Registraron operaciones.\nCant. operaciones rechazadas: 1", lots.Value("observaciones"));
        Assert.Equal("301", lots.Value("codError"));
        Assert.Equal("No existe una maquina informada con el UID que desea modificar", lots.Value("descError"));
    }

    [Fact]
    public async Task A_counters_request_is_answered_once()
    {
        await using var sim = ArcaSimHarness.Start();
        var jaza = await ServiceProbe.StartAsync(sim, "wsjaza");
        await jaza.Store.PutAsync(JazaRules.Requests, $"{Caller}/{12:D10}", new CountersRequest(12, Caller, "ME-01", false, null));

        Assert.Equal("A", (await RespondAsync(jaza, 12)).Valid().Value("resultado"));
        Assert.Equal("7003", (await RespondAsync(jaza, 12)).Valid().Value("codigo"));
        var unknown = (await RespondAsync(jaza, 2)).Valid();
        Assert.Equal("7002", unknown.Value("codigo"));
        Assert.Equal("No registra solicitud de máquina electrónica para el idSolicitud: 2 idMaquina: ME-01", unknown.Value("descripcion"));
    }

    private static async Task DeclareAsync(ServiceProbe jaza, string uid) =>
        Assert.Equal("A", (await jaza.CallAsync("informarLoteME", Auth(jaza) + "<ptoExplotacion>7</ptoExplotacion><arrayME><me>" +
            $"<oper>1</oper><uid>{uid}</uid><codTipoMaquina>1</codTipoMaquina><codMarca>1</codMarca><codModelo>1</codModelo>" +
            "<codJuego>1</codJuego><nroSerie>SER-1</nroSerie><fecIniOp>2026-09-29</fecIniOp></me></arrayME>")).Valid().Value("resultado"));

    private static async Task<System.Xml.Linq.XElement> Pending(ServiceProbe jaza) =>
        (await jaza.CallAsync("consultarIdsMEPendientes", Auth(jaza) + "<nroPuntoExplotacion>7</nroPuntoExplotacion>")).Valid();

    private static Task<SoapAnswer> DayAsync(
        ServiceProbe jaza, string machine, string date, int presentation, int sequence, (long Games, long CoinIn) from, (long Games, long CoinIn) to,
        string? start = null, string? end = null) =>
        jaza.CallAsync("informarResumenDiaME", Auth(jaza) +
            $"<nroPuntoExplotacion>7</nroPuntoExplotacion><fechaPresentacion>{date}</fechaPresentacion><nroPresentacion>{presentation}</nroPresentacion>" +
            Detail(machine, date, sequence, from, to, start, end));

    /// <summary>The codes a summary is refused with, in the order the answer lists them; none when it is accepted.</summary>
    private static async Task<int[]> CodesAsync(
        ServiceProbe jaza, string date, int presentation, int sequence, (long Games, long CoinIn) from, (long Games, long CoinIn) to,
        string? start = null, string? end = null) =>
        (await DayAsync(jaza, "ME-01", date, presentation, sequence, from, to, start, end)).Valid().All("codigo").Select(c => int.Parse(c.Value)).ToArray();

    private static Task<SoapAnswer> RespondAsync(ServiceProbe jaza, long id) =>
        jaza.CallAsync("responderSolicitudME", Auth(jaza) + $"<idSolicitud>{id}</idSolicitud><estado>OK</estado>" +
                                               Detail("ME-01", "2026-09-30", 1, (0, 0), (5, 5)));

    private static string Detail(
        string machine, string date, int sequence, (long Games, long CoinIn) from, (long Games, long CoinIn) to, string? start = null, string? end = null) =>
        $"<detalleMaquinaElectronica><idMaquina>{machine}</idMaquina><secuencia>{sequence}</secuencia>" +
        $"<fechaHoraSecuenciaInicio>{start ?? $"{date}T{8 + sequence:D2}:00:00-03:00"}</fechaHoraSecuenciaInicio>" +
        $"<fechaHoraSecuenciaFin>{end ?? $"{date}T{9 + sequence:D2}:00:00-03:00"}</fechaHoraSecuenciaFin>" +
        "<denomContabilidad>0.01</denomContabilidad>" +
        $"<contadoresInicial><juegosJugados>{from.Games}</juegosJugados><coinIn>{from.CoinIn}</coinIn><coinOut>0</coinOut><jackpot>0</jackpot></contadoresInicial>" +
        $"<contadoresFinal><juegosJugados>{to.Games}</juegosJugados><coinIn>{to.CoinIn}</coinIn><coinOut>0</coinOut><jackpot>0</jackpot></contadoresFinal>" +
        "</detalleMaquinaElectronica>";

    private static string Auth(ServiceProbe jaza) =>
        $"<authRequest><token>{jaza.Token}</token><sign>{jaza.Sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada></authRequest>";
}
