using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>
/// A CEMA precinto on a depósito fiscal's door: its place in the prestador's
/// padrón (HABI or BAJA, the depósito's acceptance) and its monitoring state
/// (CIDE, SOAC, ACTI, SODE, DESA) with the last alarms reported.
/// </summary>
public sealed record Cema(
    long Cuit, string Id, string Aduana, string LugarOperativo, string EstadoPrecinto, DateTimeOffset FechaEstado, string EstadoAcepDepo,
    string Estado, DateTimeOffset FUltEstado, string CodAlarma = "", DateTimeOffset? FUltEvento = null);

/// <summary>
/// wgesprecintosdepfis, the CEMA prestadores' service (docs/arca/servicios/wgesprecintosdepfis.md):
/// the padrón (NovedadPrecinto, ConsultaCemaPadron) and the monitoring cycle
/// SOAC → ACTI (IniciarMonitoreo) → events (InformarEstadoPrecintos) → SODE →
/// DESA (TerminarMonitoreo), polled with ConsultarPrecintosPendientes and read
/// back with ConsultarPrecintos, with the manual's codes and 0 "OK". ArcaSim's
/// choices where the manual is silent: an alta leaves the precinto accepted by
/// the depósito (ACEP) and already asked to activate (SOAC), the depositario's
/// and the guard's first steps; SODE, the request to deactivate that ARCA's
/// guards and depositarios make and ArcaSim has no service for, is a test or an
/// operator putting the precinto's document with that Estado; an array with one
/// bad item is refused whole, that item in DescAdicErr; NovedadPrecinto tells an alta
/// from an actualización by whether the precinto exists, and Aduana or
/// LugarOperativo of the wrong length are 70222 and 10782.
/// </summary>
public sealed class WgesPrecintosRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    public const string Collection = "wgesprecintosdepfis.precintos";
    private static readonly string[] Monitored = ["SOAC", "ACTI", "SODE"];

    public string Service => "wgesprecintosdepfis";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "NovedadPrecinto" => await NoveltyAsync(call, ct),
        "ConsultaCemaPadron" => await PadronAsync(call, ct),
        "ConsultarPrecintosPendientes" => await PendingAsync(call, ct),
        "IniciarMonitoreo" => await MoveAsync(call, "argIniciarMonitoreo", "SOAC", "ACTI", ct),
        "TerminarMonitoreo" => await MoveAsync(call, "argTerminarMonitoreo", "SODE", "DESA", ct),
        "InformarEstadoPrecintos" => await EventsAsync(call, ct),
        "ConsultarPrecintos" => await PrecintosAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> NoveltyAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argPrecinto");
        if (Dia.FirstMissing(arg, "IdPrecinto") is { } missing) return call.Fail(42034, Dia.MissingText(missing, article: true));
        var id = arg.Field("IdPrecinto");
        var aduana = arg.Field("Aduana");
        var place = arg.Field("LugarOperativo");
        var cema = await store.GetAsync<Cema>(Collection, id, ct);
        if (cema is not null && cema.Cuit != call.Cuit) return call.Fail(31167, $"Operación Prohibida {id}");
        var now = clock.Now.ToArgentina();

        if (aduana == "" && place == "")
        {
            if (cema is null || cema.EstadoPrecinto == "BAJA") return call.Fail(12404, "Dispositivo INEXISTENTE");
            if (Monitored.Contains(cema.Estado)) return call.Fail(30850, $"Puerta Deposito con dispositivo {id} asignado en estado {cema.Estado}");
            await store.PutAsync(Collection, id, cema with { EstadoPrecinto = "BAJA", FechaEstado = now }, ct);
            return Done(call);
        }
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo") is { } field) return call.Fail(42034, Dia.MissingText(field, article: true));
        if (aduana.Length != 3 || !aduana.All(char.IsDigit)) return call.Fail(70222, "Aduana INEXISTENTE o fuera de Vigencia");
        if (place.Length != 5) return call.Fail(10782, "Lugar Operativo INEXISTENTE o Fuera de Vigencia");

        if (cema is { EstadoPrecinto: "HABI" })
        {
            if (cema.Aduana == aduana && cema.LugarOperativo == place) return call.Fail(30846, $"Precinto {id} ya fue dado de alta");
            if (Monitored.Contains(cema.Estado)) return call.Fail(30850, $"Puerta Deposito con dispositivo {id} asignado en estado {cema.Estado}");
            await store.PutAsync(Collection, id, cema with { Aduana = aduana, LugarOperativo = place, FechaEstado = now }, ct);
            return Done(call);
        }
        await store.PutAsync(Collection, id, new Cema(call.Cuit, id, aduana, place, "HABI", now, "ACEP", "SOAC", now), ct);
        return Done(call);
    }

    private async Task<ContractAnswer> PadronAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsulta");
        var found = (await MineAsync(call, ct))
            .Where(c => arg.Matches("IdPrecinto", c.Id) && arg.Matches("Aduana", c.Aduana) && arg.Matches("LugarOperativo", c.LugarOperativo)
                        && arg.Matches("EstadoPrecinto", c.EstadoPrecinto) && arg.Matches("EstadoAcepDepo", c.EstadoAcepDepo))
            .ToList();
        if (found.Count == 0) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(0, "OK");
        answer.Repeat("Dispositivo", found, (row, c) => row
            .Set("IdPrecinto", c.Id)
            .Set("Aduana", c.Aduana)
            .Set("LugarOperativo", c.LugarOperativo)
            .Set("EstadoPrecinto", c.EstadoPrecinto)
            .Set("FechaEstado", c.FechaEstado)
            .Set("EstadoAcepDepo", c.EstadoAcepDepo));
        return call.Done(answer);
    }

    private async Task<ContractAnswer> PendingAsync(ServiceCall call, CancellationToken ct)
    {
        var found = (await MineAsync(call, ct)).Where(c => c.EstadoPrecinto == "HABI" && c.Estado is "SOAC" or "SODE").ToList();
        if (found.Count == 0) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(0, "OK");
        answer.Repeat("PrecintoPendiente", found, (row, c) => row.Set("IdPrecinto", c.Id).Set("Estado", c.Estado).Set("FechaEstado", c.FUltEstado));
        return call.Done(answer);
    }

    /// <summary>IniciarMonitoreo (SOAC → ACTI) and TerminarMonitoreo (SODE → DESA) over 1 to 250 precintos, all or none.</summary>
    private async Task<ContractAnswer> MoveAsync(ServiceCall call, string argName, string from, string to, CancellationToken ct)
    {
        var ids = call.Arg(argName)?.Find("IdPrecinto")?.Elements().Select(e => e.Value.Trim()).ToList() ?? [];
        if (ArrayProblem(call, ids) is { } refused) return refused;
        var batch = new List<Cema>();
        foreach (var id in ids)
        {
            var (cema, refusal) = await UsableAsync(call, id, from, ct);
            if (refusal is not null) return refusal;
            batch.Add(cema!);
        }
        var now = clock.Now.ToArgentina();
        foreach (var cema in batch) await store.PutAsync(Collection, cema.Id, cema with { Estado = to, FUltEstado = now }, ct);
        return Done(call);
    }

    private async Task<ContractAnswer> EventsAsync(ServiceCall call, CancellationToken ct)
    {
        var events = call.Arg("argInformarEstadoPrecintos")?.Find("EventoPrecintos")?.Elements().ToList() ?? [];
        if (ArrayProblem(call, events.Select(e => e.Field("IdPrecinto")).ToList()) is { } refused) return refused;
        var batch = new List<Cema>();
        foreach (var item in events)
        {
            var id = item.Field("IdPrecinto");
            var (cema, refusal) = await UsableAsync(call, id, "ACTI", ct);
            if (refusal is not null) return refusal;
            var alarms = item.Field("CodAlarma");
            if (alarms == "") return call.Fail(42034, Dia.MissingText("CodAlarma", article: true), id);
            if (alarms.Split('+').FirstOrDefault(a => !AduanaTables.Has("ESTMON_DESC", a)) is { } unknown)
                return call.Fail(30841, $"Codigo de alarma {unknown} inexistente", id);
            batch.Add(cema! with { CodAlarma = alarms, FUltEvento = Dia.Moment(item.Field("FechaEvento")) ?? clock.Now.ToArgentina() });
        }
        foreach (var cema in batch) await store.PutAsync(Collection, cema.Id, cema, ct);
        return Done(call);
    }

    private async Task<ContractAnswer> PrecintosAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultaPrecintos");
        var id = arg.Field("IdPrecinto");
        var state = arg.Field("Estado");
        if (id == "" && state == "") return call.Fail(30842, "Debe informarse precinto y/o estado");
        var found = (await MineAsync(call, ct)).Where(c => (id == "" || c.Id == id) && (state == "" || c.Estado == state)).ToList();
        if (found.Count == 0) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(0, "OK");
        answer.Repeat("Precinto", found, (row, c) =>
        {
            row.Set("IdPrecinto", c.Id)
                .Set("Estado", c.Estado)
                .Set("CodAlarma", c.CodAlarma)
                .Set("FUltEstado", c.FUltEstado)
                .Set("FUltEvento", c.FUltEvento ?? Dia.NoDate);
            if (c.CodAlarma == "") row.Drop("CodAlarma");
        });
        return call.Done(answer);
    }

    private static ContractAnswer? ArrayProblem(ServiceCall call, IReadOnlyList<string> ids)
    {
        if (ids.Count < 1) return call.Fail(31361, "El array IdPrecinto no debe tener menos de 1 datos");
        if (ids.Count > 250) return call.Fail(31362, "El array IdPrecinto no debe tener mas de 250 datos");
        if (ids.GroupBy(i => i).FirstOrDefault(g => g.Count() > 1) is { } repeated)
            return call.Fail(30839, "ERROR - Dispositivo informado mas de una vez", repeated.Key);
        return null;
    }

    /// <summary>The caller's precinto, enabled and in the state the operation needs, or the refusal naming it.</summary>
    private async Task<(Cema? Cema, ContractAnswer? Refusal)> UsableAsync(ServiceCall call, string id, string state, CancellationToken ct)
    {
        var cema = await store.GetAsync<Cema>(Collection, id, ct);
        if (cema is null || cema.Cuit != call.Cuit) return (null, call.Fail(12404, "Dispositivo INEXISTENTE", id));
        if (cema.EstadoPrecinto != "HABI") return (null, call.Fail(12591, "CEMA NO HABILITADO para su uso", id));
        if (cema.Estado != state) return (null, call.Fail(30840, $"El dispositivo no se encuentra en estado {state}", id));
        return (cema, null);
    }

    private async Task<List<Cema>> MineAsync(ServiceCall call, CancellationToken ct) =>
        (await store.ListAsync<Cema>(Collection, "", ct)).Where(c => c.Cuit == call.Cuit).ToList();

    private static ContractAnswer Done(ServiceCall call) => call.Done(call.Sample().Receipt(0, "OK"));
}
