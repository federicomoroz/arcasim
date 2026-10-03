using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A gaming machine declared by lot at a point of exploitation.</summary>
public sealed record GamingMachine(long Cuit, int Point, string Uid, DateOnly? StartedOn, bool Active);

/// <summary>What a machine reported for one day: its presentation and sequences.</summary>
public sealed record MachineDay(long Cuit, int Point, string Machine, DateOnly Date, int Presentation, List<MachineSequence> Sequences);

public sealed record MachineSequence(
    int Number, string Start, string End, string Denomination, MachineCounters Initial, MachineCounters Final);

public sealed record MachineCounters(long Games, long CoinIn, long CoinOut, long Jackpot);

/// <summary>A lot of machine registrations, changes and removals, with the errors of its lines.</summary>
public sealed record MachineLot(long Number, long Cuit, int Point, DateOnly SentOn, string State, string Observations, List<LotLineError> Errors);

public sealed record LotLineError(int Line, int Code, string Description);

/// <summary>A request for a machine's counters, published to the operator in Ventanilla Electrónica.</summary>
public sealed record CountersRequest(long Id, long Cuit, string Machine, bool Answered, string? State);

/// <summary>
/// Juegos de azar (wsjaza, docs/arca/servicios/wsjaza.md): machines declared
/// by lot (informarLoteME, consultarLoteME with errors 301 by line), daily
/// summaries per machine with presentations, sequences and rectifications
/// (informarResumenDiaME with 1001 to 1014 and 1101 to 1105; consultarMEInformada,
/// consultarIdsMEInformadas, consultarIdsMEPendientes, removerResumenDiaME),
/// and answers to counter requests (responderSolicitudME, 7000 to 7003).
/// Codes are the manual's; its texts are verbatim only for 1003, 7002 and the
/// lot's 301 and observations, the rest say what the manual's tables describe.
/// ArcaSim's choices: every point of exploitation counts as registered; a lot
/// is processed when it arrives, so consultarLoteME already shows it in TE or
/// ER; an alta of a known uid updates it, and a baja or modificación of an
/// unknown one is line error 301; removing a summary that does not exist is
/// accepted; counter requests are not created by any operation, so tests put
/// them in wsjaza.solicitudes; summaries ("otros") keep the contract's answer.
/// </summary>
public sealed class JazaRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    public const string Machines = "wsjaza.maquinas";
    public const string Days = "wsjaza.resumenes";
    public const string Lots = "wsjaza.lotes";
    public const string Requests = "wsjaza.solicitudes";

    public string Service => "wsjaza";

    private DateOnly Today => DateOnly.FromDateTime(clock.Now.ToArgentina().DateTime);

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "informarLoteME" => await InformLotAsync(call, ct),
        "consultarLoteME" => await ListLotsAsync(call, ct),
        "informarResumenDiaME" => await InformDayAsync(call, ct),
        "consultarMEInformada" => await ReadDayAsync(call, ct),
        "consultarIdsMEInformadas" => await InformedAsync(call, ct),
        "consultarIdsMEPendientes" => await PendingAsync(call, ct),
        "removerResumenDiaME" => await RemoveDayAsync(call, ct),
        "responderSolicitudME" => await AnswerRequestAsync(call, ct),
        _ => null,
    };

    // ---- Lots ------------------------------------------------------------------------

    private async Task<ContractAnswer> InformLotAsync(ServiceCall call, CancellationToken ct)
    {
        var point = call.Request.Int("ptoExplotacion");
        var lines = call.Request.FindAll("me").ToList();
        var refused = new List<(int, string)>();
        foreach (var line in lines)
        {
            var operation = line.Int("oper");
            if (operation is < 1 or > 3)
            {
                refused.Add((8000, $"El campo oper debe ser 1 (alta), 2 (baja) o 3 (modificación). Se informó {line.Text("oper")}."));
                continue;
            }
            var required = operation switch
            {
                1 => new[] { "codTipoMaquina", "codMarca", "codModelo", "codJuego", "nroSerie", "fecIniOp" },
                2 => ["codBaja", "fechaBaja"],
                _ => [],
            };
            var name = operation switch { 1 => "alta", 2 => "baja", _ => "modificación" };
            refused.AddRange(required.Where(f => line.Optional(f) is null)
                .Select(f => (8001, $"Para una operación de {name} el campo {f} no puede ser nulo")));
        }
        if (refused.Count > 0)
            return call.Ok(new XElement(call.Operation.Output, new XElement("nroLote", 0), new XElement("resultado", "R"), ErrorList(refused)));

        var number = await store.NextAsync(Lots, ct);
        var errors = new List<LotLineError>();
        var registered = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var uid = line.Text("uid") ?? "";
            var key = MachineKey(call.Cuit, point, uid);
            var known = await store.GetAsync<GamingMachine>(Machines, key, ct);
            switch (line.Int("oper"))
            {
                case 1:
                    await store.PutAsync(Machines, key, new GamingMachine(call.Cuit, point, uid, line.Date("fecIniOp"), true), ct);
                    break;
                case 2 when known is { Active: true }:
                    await store.PutAsync(Machines, key, known with { Active = false }, ct);
                    break;
                case 3 when known is { Active: true }:
                    if (line.Date("fecIniOp") is { } start) await store.PutAsync(Machines, key, known with { StartedOn = start }, ct);
                    break;
                default:
                    errors.Add(new LotLineError(i + 1, 301, "No existe una maquina informada con el UID que desea modificar"));
                    continue;
            }
            registered++;
        }
        var rejected = lines.Count - registered;
        var observations = registered > 0
            ? $"Cant. operaciones registradas: {registered}" + (rejected > 0 ? $"\nCant. operaciones rechazadas: {rejected}" : "")
            : $"No se Registraron operaciones.\nCant. operaciones rechazadas: {rejected}";
        await store.PutAsync(Lots, LotKey(call.Cuit, number),
            new MachineLot(number, call.Cuit, point, Today, errors.Count == 0 ? "TE" : "ER", observations, errors), ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("nroLote", number), new XElement("resultado", "A")));
    }

    private async Task<ContractAnswer> ListLotsAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var point = request.Int("ptoExplotacion");
        var (low, high) = (request.OptionalLong("nroLoteDesde"), request.OptionalLong("nroLoteHasta"));
        var (from, to) = (request.Date("fechaDesde"), request.Date("fechaHasta"));
        var refused = new List<(int, string)>();
        if (low > high) refused.Add((9009, "El número de lote desde no puede ser mayor al número de lote hasta."));
        if (from > Today) refused.Add((9010, "La fecha desde no puede ser futura."));
        if (to > Today) refused.Add((9011, "La fecha hasta no puede ser futura."));
        if (from is { } f && to is { } t && t < f) refused.Add((9012, "La fecha hasta no puede ser anterior a la fecha desde."));
        else if (from is { } f2 && to is { } t2 && t2.DayNumber - f2.DayNumber > 30) refused.Add((9013, "El rango de fechas no puede superar los 30 días."));
        if (refused.Count > 0) return call.Ok(new XElement(call.Operation.Output, new XElement("resultado", "R"), ErrorList(refused)));

        var lots = (await store.ListAsync<MachineLot>(Lots, $"{call.Cuit}/", ct))
            .Where(l => l.Point == point && (low is null || l.Number >= low) && (high is null || l.Number <= high)
                        && (from is null || l.SentOn >= from) && (to is null || l.SentOn <= to))
            .OrderByDescending(l => l.Number).Take(20).OrderBy(l => l.Number);
        return call.Ok(new XElement(call.Operation.Output,
            new XElement("resultado", "A"),
            new XElement("arrayLotesME", lots.Select(l => new XElement("loteME",
                new XElement("nroLote", l.Number),
                new XElement("estado", l.State),
                new XElement("fechaEnvio", l.SentOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-03:00"),
                new XElement("origen", "WS"),
                new XElement("observaciones", l.Observations),
                l.Errors.Count == 0 ? null : new XElement("arrayErrores", l.Errors.Select(e => new XElement("errorME",
                    new XElement("nroLinea", e.Line),
                    new XElement("erroresLinea", new XElement("errorLineaME", new XElement("codError", e.Code), new XElement("descError", e.Description)))))))))));
    }

    // ---- Daily summaries ---------------------------------------------------------------

    private async Task<ContractAnswer> InformDayAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var point = request.Int("nroPuntoExplotacion");
        var date = request.Date("fechaPresentacion") ?? Today;
        var presentation = request.Int("nroPresentacion");
        var detail = request.Find("detalleMaquinaElectronica") ?? new XElement("detalleMaquinaElectronica");
        var machineId = detail.Text("idMaquina") ?? "";
        var sequence = Sequence(detail);
        var refused = new List<(int, string)>();

        if (date > Today) refused.Add((1020, "La fecha de presentación no puede ser futura."));
        var machine = await store.GetAsync<GamingMachine>(Machines, MachineKey(call.Cuit, point, machineId), ct);
        if (machine is not { Active: true })
            return Result(call, [.. refused, (1001, $"La máquina {machineId} no está declarada en JAzA para la CUIT, el punto de explotación {point} y la fecha {Show(date)}.")]);
        if (machine.StartedOn is { } started && date < started)
            refused.Add((1002, $"La fecha {Show(date)} es anterior al inicio de operaciones de la máquina, el {Show(started)}."));

        string[] names = ["juegos jugados", "coin-in", "coin-out", "jackpot"];
        var initial = Values(sequence.Initial);
        var final = Values(sequence.Final);
        for (var i = 0; i < 4; i++)
            if (final[i] < initial[i]) refused.Add((1101 + i, $"El contador final de {names[i]} debe ser mayor o igual al inicial."));
        if (Moment(sequence.End) < Moment(sequence.Start))
            refused.Add((1105, "La fecha y hora de fin de la secuencia debe ser posterior o igual a la de inicio."));

        var days = await store.ListAsync<MachineDay>(Days, $"{call.Cuit}/{point:D5}/{machineId}/", ct);
        var existing = days.FirstOrDefault(d => d.Date == date);
        var rectifies = false;
        if (existing is null)
        {
            var pending = days.Count > 0 ? days.Max(d => d.Date).AddDays(1) : machine.StartedOn;
            if (pending is { } first && first < date)
                refused.Add((1003, $"Antes de informar los datos para la fecha {Show(date)} debe informar los datos para la fecha {Show(first)}"));
            if (presentation != 1) refused.Add((1005, "En el primer envío de una fecha el número de presentación debe ser 1."));
            if (sequence.Number != 1) refused.Add((1006, "En el primer envío de una fecha la secuencia debe ser 1."));
        }
        else if (presentation == existing.Presentation)
        {
            var last = existing.Sequences[^1];
            if (sequence.Number != last.Number + 1)
                refused.Add((1007, $"Para informar una secuencia adicional se debe enviar la presentación {presentation} y la secuencia {last.Number + 1}."));
            else if (Moment(sequence.Start) < Moment(last.End))
                refused.Add((1010, "El inicio de la nueva secuencia debe ser posterior o igual al fin de la secuencia anterior."));
        }
        else if (presentation == existing.Presentation + 1)
        {
            rectifies = true;
            if (date < Today.AddDays(-30)) refused.Add((1004, "Solo se puede rectificar una presentación dentro de los 30 días."));
            if (sequence.Number != 1) refused.Add((1008, "Una presentación rectificativa debe informar la secuencia 1."));
        }
        else
        {
            refused.Add((1009, $"El número de presentación debe ser {existing.Presentation} para una nueva secuencia o {existing.Presentation + 1} para rectificar."));
        }

        if (sequence.Number == 1 && days.FirstOrDefault(d => d.Date == date.AddDays(-1)) is { } previous)
        {
            var before = Values(previous.Sequences[^1].Final);
            for (var i = 0; i < 4; i++)
                if (initial[i] != before[i])
                    refused.Add((1011 + i, $"El contador inicial de {names[i]} de la secuencia 1 debe coincidir con el final del día anterior ({before[i]})."));
        }
        if (refused.Count > 0) return Result(call, refused);

        var day = existing is null || rectifies
            ? new MachineDay(call.Cuit, point, machineId, date, presentation, [sequence])
            : existing with { Sequences = [.. existing.Sequences, sequence] };
        await store.PutAsync(Days, DayKey(call.Cuit, point, machineId, date), day, ct);
        if (rectifies)
            foreach (var later in days.Where(d => d.Date > date))
                await store.DeleteAsync(Days, DayKey(call.Cuit, point, machineId, later.Date), ct);
        return Result(call, []);
    }

    private async Task<ContractAnswer> ReadDayAsync(ServiceCall call, CancellationToken ct)
    {
        var point = call.Request.Int("nroPuntoExplotacion");
        var date = call.Request.Date("fechaPresentacion") ?? Today;
        var machineId = call.Request.Text("idMaquina") ?? "";
        if (date > Today) return Return(call, ErrorList([(3001, "La fecha de presentación no puede ser futura.")]));
        var day = await store.GetAsync<MachineDay>(Days, DayKey(call.Cuit, point, machineId, date), ct);
        if (day is null) return Return(call, ErrorList([(3003, $"No existe una presentación para la máquina {machineId} en la fecha {Show(date)}.")]));
        return Return(call, new XElement("arrayDetalleMaquinasElectronicas", day.Sequences.Select(s => new XElement("detalleMaquinaElectronica",
            new XElement("idMaquina", day.Machine),
            new XElement("secuencia", s.Number),
            new XElement("fechaHoraSecuenciaInicio", s.Start),
            new XElement("fechaHoraSecuenciaFin", s.End),
            new XElement("denomContabilidad", s.Denomination),
            Counters("contadoresInicial", s.Initial),
            Counters("contadoresFinal", s.Final)))));
    }

    private async Task<ContractAnswer> InformedAsync(ServiceCall call, CancellationToken ct)
    {
        var point = call.Request.Int("nroPuntoExplotacion");
        var date = call.Request.Date("fechaPresentacion") ?? Today;
        if (date > Today) return Return(call, ErrorList([(5002, "La fecha de presentación no puede ser futura.")]));
        var machines = (await store.ListAsync<MachineDay>(Days, $"{call.Cuit}/{point:D5}/", ct)).Where(d => d.Date == date).Select(d => d.Machine);
        return Return(call,
            new XElement("nroPuntoExplotacion", point),
            new XElement("fechaPresentacion", Iso(date)),
            new XElement("arrayIdsMaquinasElectronicas", machines.Select(m => new XElement("idMaquina", m))));
    }

    private async Task<ContractAnswer> PendingAsync(ServiceCall call, CancellationToken ct)
    {
        var point = call.Request.Int("nroPuntoExplotacion");
        var days = await store.ListAsync<MachineDay>(Days, $"{call.Cuit}/{point:D5}/", ct);
        var pending = new List<(string Machine, DateOnly Since)>();
        foreach (var machine in (await store.ListAsync<GamingMachine>(Machines, $"{call.Cuit}/{point:D5}/", ct)).Where(m => m.Active))
        {
            var informed = days.Where(d => d.Machine == machine.Uid).ToList();
            var since = informed.Count > 0 ? informed.Max(d => d.Date).AddDays(1) : machine.StartedOn;
            if (since is { } first && first < Today) pending.Add((machine.Uid, first));
        }
        return Return(call,
            new XElement("nroPuntoExplotacion", point),
            new XElement("arrayIdsFechasMaquinasElectronicas", pending.Select(p => new XElement("idFechaMaquina",
                new XElement("idMaquina", p.Machine), new XElement("fecha", Iso(p.Since))))));
    }

    private async Task<ContractAnswer> RemoveDayAsync(ServiceCall call, CancellationToken ct)
    {
        var date = call.Request.Date("fechaPresentacion") ?? Today;
        await store.DeleteAsync(Days, DayKey(call.Cuit, call.Request.Int("nroPuntoExplotacion"), call.Request.Text("idMaquina") ?? "", date), ct);
        return Result(call, []);
    }

    // ---- Counter requests ---------------------------------------------------------------

    private async Task<ContractAnswer> AnswerRequestAsync(ServiceCall call, CancellationToken ct)
    {
        var id = call.Request.Long("idSolicitud");
        var state = call.Request.Text("estado") ?? "";
        var detail = call.Request.Find("detalleMaquinaElectronica");
        var machineId = detail?.Text("idMaquina") ?? "";
        if (state is "ND" or "BA" && detail is not null) return Result(call, [(7000, "Con estado ND o BA no se debe informar el detalle de la máquina.")]);
        if (state == "OK" && detail is null) return Result(call, [(7001, "Con estado OK se debe informar el detalle de la máquina.")]);

        var key = $"{call.Cuit}/{id:D10}";
        var found = await store.GetAsync<CountersRequest>(Requests, key, ct);
        if (found is null || (detail is not null && found.Machine != machineId))
            return Result(call, [(7002, $"No registra solicitud de máquina electrónica para el idSolicitud: {id} idMaquina: {(detail is null ? found?.Machine : machineId)}")]);
        if (found.Answered) return Result(call, [(7003, $"La solicitud {id} ya fue respondida.")]);
        await store.PutAsync(Requests, key, found with { Answered = true, State = state }, ct);
        return Result(call, []);
    }

    // ---- Shapes --------------------------------------------------------------------------

    private static MachineSequence Sequence(XElement detail) => new(
        detail.Int("secuencia"),
        detail.Text("fechaHoraSecuenciaInicio") ?? "",
        detail.Text("fechaHoraSecuenciaFin") ?? "",
        detail.Text("denomContabilidad") ?? "0",
        CountersOf(detail.Find("contadoresInicial")),
        CountersOf(detail.Find("contadoresFinal")));

    private static MachineCounters CountersOf(XElement? counters) => counters is null
        ? new MachineCounters(0, 0, 0, 0)
        : new MachineCounters(counters.Long("juegosJugados"), counters.Long("coinIn"), counters.Long("coinOut"), counters.Long("jackpot"));

    private static DateTimeOffset Moment(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? moment : DateTimeOffset.MinValue;

    private static long[] Values(MachineCounters c) => [c.Games, c.CoinIn, c.CoinOut, c.Jackpot];

    private static XElement Counters(string name, MachineCounters c) => new(name,
        new XElement("juegosJugados", c.Games), new XElement("coinIn", c.CoinIn), new XElement("coinOut", c.CoinOut), new XElement("jackpot", c.Jackpot));

    /// <summary>The operation's *Return with resultado A, or R and the errors.</summary>
    private static ContractAnswer Result(ServiceCall call, IReadOnlyList<(int, string)> errors) =>
        Return(call, new XElement("resultado", errors.Count == 0 ? "A" : "R"), errors.Count == 0 ? null : ErrorList(errors));

    private static ContractAnswer Return(ServiceCall call, params object?[] content) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(call.Name + "Return", content)));

    private static XElement ErrorList(IEnumerable<(int Code, string Text)> errors) =>
        new("arrayErrores", errors.Select(e => new XElement("codigoDescripcion", new XElement("codigo", e.Code), new XElement("descripcion", e.Text))));

    private static string MachineKey(long cuit, int point, string uid) => $"{cuit}/{point:D5}/{uid}";

    private static string DayKey(long cuit, int point, string machine, DateOnly date) =>
        $"{cuit}/{point:D5}/{machine}/{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

    private static string LotKey(long cuit, long number) => $"{cuit}/{number:D10}";

    private static string Show(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
