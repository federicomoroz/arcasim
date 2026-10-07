using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>A PEMA device in the prestador's padrón: HABI while it can be used, BAJA after a baja.</summary>
public sealed record PemaDevice(
    long Cuit, string Id, string Tipo, string Interno, string Modelo, string Estado, DateTimeOffset FechaEstado, DateTimeOffset FechaAlta);

/// <summary>What a device is doing: its state (ZGSA, PASA, ZGAR, DISP, PFER), the destinación and medio transportador it travels with, and the customs operation on it.</summary>
public sealed record PemaUse(
    long Cuit, string Id, string Destinacion, string Contenedor, string Estado, DateOnly FechaEstado,
    string Operacion, DateOnly FechaOperacion, string Salida, string Aduana);

/// <summary>
/// WDiaUtiDES, the PEMA prestadores' service (docs/arca/servicios/WDiaUtiDES.md):
/// the padrón of devices (NovedadDispositivo, ConsultaPemaPadron) and each
/// device's trip (InicioCargaSuelta and ActualizaDispositivo, read back by
/// ConsultaDispositivo and ConsultaContenedor), with the manual's state machine
/// (pp.12-14) and codes. Success is 20304 "Procedimiento terminado OK." in the
/// trip's methods and 0 in the padrón's, as the manual's tables list them;
/// ConsultaContenedor, whose table has no success code, answers 20304.
/// The customs operation moves by itself, ArcaSim's choice since ArcaSim has no
/// customs officer: ZGSA leaves it authorized to exit (SALI, with a salida
/// number), ZGAR leaves it arrived (ARRI). A baja leaves the device in BAJA,
/// and a device in BAJA or never given of alta is 12404 "Dispositivo INEXISTENTE".
/// </summary>
public sealed class WDiaUtiDesRules(IDocumentStore store, ITaxpayerRepository taxpayers, IClock clock) : IServiceBehavior
{
    private const string Padron = "WDiaUtiDES.padron";
    private const string Uses = "WDiaUtiDES.dispositivos";
    private const string TripOk = "Procedimiento terminado OK.";
    private static readonly string[] Assigned = ["ZGSA", "PASA", "ZGAR"];

    /// <summary>The states a device has to be in to move to each state (manual pp.12-14).</summary>
    private static readonly Dictionary<string, string[]> From = new()
    {
        ["ZGSA"] = ["", "DISP"],
        ["PASA"] = ["ZGSA"],
        ["ZGAR"] = ["PASA"],
        ["DISP"] = ["ZGSA", "ZGAR", "PFER"],
        ["PFER"] = ["", "ZGSA", "PASA", "ZGAR", "DISP"],
    };

    public string Service => "WDiaUtiDES";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "NovedadDispositivo" => await NoveltyAsync(call, ct),
        "ConsultaPemaPadron" => await PadronAsync(call, ct),
        "ActualizaDispositivo" => await MoveAsync(call, call.Arg("argDispositivo"), "Contenedor", "Estado", "FechaEstado", ct),
        "InicioCargaSuelta" => await MoveAsync(call, call.Arg("argInicioCargaSuelta"), "PaisPatente", null, "Fecha", ct),
        "ConsultaDispositivo" => await DeviceAsync(call, ct),
        "ConsultaContenedor" => await ContainersAsync(call, ct),
        "ConsultaDatosATA" => await AtaAsync(call, ct),
        _ => null,
    };

    // ---- Padrón ------------------------------------------------------------------------

    private async Task<ContractAnswer> NoveltyAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argNovedadDispositivo");
        if (Dia.FirstMissing(arg, "IdentificadorDispositivo", "Novedad") is { } missing) return call.Fail(42034, Dia.MissingText(missing));
        var id = arg.Field("IdentificadorDispositivo");
        var novelty = arg.Field("Novedad");
        if (novelty is not ("A" or "B" or "M"))
            return call.Fail(31353, "El campo Novedad tiene un formato erroneo. Debe ser A, B o M");

        var device = await store.GetAsync<PemaDevice>(Padron, id, ct);
        if (device is not null && device.Cuit != call.Cuit)
            return call.Fail(11891, "El identificador PEMA no corresponde a la empresa de conexion");
        var now = clock.Now.ToArgentina();

        if (novelty == "A")
        {
            if (Dia.FirstMissing(arg, "TipoDispositivo", "IdentificadorDispositivoInterno") is { } field) return call.Fail(42034, Dia.MissingText(field));
            if (device is { Estado: "HABI" }) return call.Fail(30838, "El dispositivo se encuentra en estado HABI");
            var interno = arg.Field("IdentificadorDispositivoInterno");
            var mine = await store.ListAsync<PemaDevice>(Padron, "", ct);
            if (mine.Any(d => d.Cuit == call.Cuit && d.Id != id && d.Estado == "HABI" && d.Interno == interno))
                return call.Fail(11895, "Numero interno de dispositivo ya informado");
            await store.PutAsync(Padron, id, new PemaDevice(call.Cuit, id, arg.Field("TipoDispositivo"), interno, arg.Field("ModeloDispositivo"), "HABI", now, now), ct);
            return Padronned(call);
        }

        if (device is null) return call.Fail(12404, "Dispositivo INEXISTENTE");
        if (device.Estado == "BAJA") return call.Fail(30838, "El dispositivo se encuentra en estado BAJA");
        if (novelty == "B")
        {
            if (await store.GetAsync<PemaUse>(Uses, id, ct) is { } use && Assigned.Contains(use.Estado))
                return call.Fail(12409, "El dispositivo se encuentra asignado.");
            await store.PutAsync(Padron, id, device with { Estado = "BAJA", FechaEstado = now }, ct);
            return Padronned(call);
        }

        await store.PutAsync(Padron, id, device with
        {
            Tipo = Or(arg.Field("TipoDispositivo"), device.Tipo),
            Interno = Or(arg.Field("IdentificadorDispositivoInterno"), device.Interno),
            Modelo = Or(arg.Field("ModeloDispositivo"), device.Modelo),
        }, ct);
        return Padronned(call);
    }

    private static ContractAnswer Padronned(ServiceCall call) => call.Done(call.Sample().Receipt(0, "Procedimiento terminado OK"));

    private async Task<ContractAnswer> PadronAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultaPemaPadron");
        var id = arg.Field("IdentificadorDispositivo");
        var state = arg.Field("EstadoDispositivo");
        var type = arg.Field("TipoDispositivo");
        var found = (await store.ListAsync<PemaDevice>(Padron, "", ct))
            .Where(d => d.Cuit == call.Cuit && (id == "" || d.Id == id) && (state == "" || d.Estado == state) && (type == "" || d.Tipo == type))
            .ToList();
        if (found.Count == 0) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(0, "Procedimiento terminado OK");
        answer.Repeat("Pema", found, (row, d) => row
            .Set("Identificador", d.Id)
            .Set("Tipo", d.Tipo)
            .Set("Estado", d.Estado)
            .Set("FechaEstado", d.FechaEstado)
            .Set("IdentificadorInterno", d.Interno)
            .Set("IdentificadorModelo", d.Modelo)
            .Set("FechaAlta", d.FechaAlta));
        return call.Done(answer);
    }

    // ---- The trip -------------------------------------------------------------------

    /// <summary>
    /// ActualizaDispositivo, or InicioCargaSuelta when there is no state field:
    /// carga suelta starts the trip in ZGSA with the country and plate as the
    /// medio transportador.
    /// </summary>
    private async Task<ContractAnswer> MoveAsync(ServiceCall call, XElement? arg, string carrierField, string? stateField, string dateField, CancellationToken ct)
    {
        string[] required = stateField is null
            ? ["IdentificadorDispositivo", "IdentificadorDestinacion", carrierField, dateField]
            : ["IdentificadorDispositivo", "IdentificadorDestinacion", carrierField, stateField, dateField];
        if (Dia.FirstMissing(arg, required) is { } missing) return call.Fail(42034, Dia.MissingText(missing));
        if (!Dia.TryDayMonthYear(arg.Field(dateField), out var date)) return call.Fail(10238, "Formato fecha inválido");
        var target = stateField is null ? "ZGSA" : arg.Field(stateField);
        if (!AduanaTables.Has("ETAPEMA_DESC", target))
            return call.Fail(31353, "El campo Estado tiene un formato erroneo. Debe ser ZGSA, PASA, ZGAR, DISP o PFER");

        var id = arg.Field("IdentificadorDispositivo");
        var destination = arg.Field("IdentificadorDestinacion");
        var carrier = arg.Field(carrierField);
        if (await store.GetAsync<PemaDevice>(Padron, id, ct) is not { Estado: "HABI" } device || device.Cuit != call.Cuit)
            return call.Fail(12404, "Dispositivo INEXISTENTE");

        var use = await store.GetAsync<PemaUse>(Uses, id, ct);
        var current = use?.Estado ?? "";
        if (Assigned.Contains(current) && (use!.Destinacion != destination || use.Contenedor != carrier))
            return use.Destinacion == destination
                ? call.Fail(12623, "Dispositivo asignado a otro Medio Transportador")
                : call.Fail(12409, "El dispositivo se encuentra asignado.");
        if (!From[target].Contains(current))
            return call.Fail(12403, "El dispositivo está en estado incorrecto debe ser : " + string.Join(" o ", From[target].Where(s => s != "")));

        var next = target switch
        {
            "ZGSA" => new PemaUse(call.Cuit, id, destination, carrier, target, date, "SALI", date,
                $"{date:yy}{AduanaOf(destination)}SALI{await store.NextAsync("WDiaUtiDES.salidas", ct):D6}", AduanaOf(destination)),
            "ZGAR" => use! with { Estado = target, FechaEstado = date, Operacion = "ARRI", FechaOperacion = date },
            _ => (use ?? new PemaUse(call.Cuit, id, destination, carrier, "", date, "", date, "", AduanaOf(destination)))
                with { Estado = target, FechaEstado = date },
        };
        await store.PutAsync(Uses, id, next, ct);
        return call.Done(call.Sample().Receipt(20304, TripOk));
    }

    private async Task<ContractAnswer> DeviceAsync(ServiceCall call, CancellationToken ct)
    {
        var id = call.Request.Field("argIdentificadorDispositivo");
        if (id == "") return call.Fail(42034, Dia.MissingText("IdentificadorDispositivo"));
        if (await store.GetAsync<PemaUse>(Uses, id, ct) is not { } use || use.Cuit != call.Cuit) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(20304, TripOk);
        answer.Repeat("Dispositivo", [use], (row, u) => row
            .Set("CuitPrestador", u.Cuit)
            .Set("IdentificadorDispositivo", u.Id)
            .Set("EstadoOperacion", u.Operacion)
            .Set("FechaEstadoOperacion", Day(u.FechaOperacion))
            .Set("IdentificadorDestinacion", u.Destinacion)
            .Set("IdentificadorContenedor", u.Contenedor)
            .Set("IdentificadorAduana", u.Aduana)
            .Set("IdentificadorSalida", u.Salida));
        return call.Done(answer);
    }

    private async Task<ContractAnswer> ContainersAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argContenedor");
        var found = (await store.ListAsync<PemaUse>(Uses, "", ct))
            .Where(u => u.Cuit == call.Cuit && u.Estado != ""
                        && arg.Matches("IdentificadorDestinacion", u.Destinacion) && arg.Matches("IdentificadorDispositivo", u.Id)
                        && arg.Matches("IdentificadorContenedor", u.Contenedor) && arg.Matches("EstadoOperacion", u.Operacion)
                        && arg.Matches("AduanaOrigen", u.Aduana))
            .ToList();
        if (found.Count == 0) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(20304, TripOk);
        answer.Repeat("Contenedor", found, (row, u) => row
            .Set("IdentificadorDestinacion", u.Destinacion)
            .Set("IdentificadorDispositivo", u.Id)
            .Set("IdentificadorContenedor", u.Contenedor)
            .Set("EstadoContenedor", u.Estado)
            .Set("IdentificadorSalida", u.Salida)
            .Set("IdentificadorAduana", u.Aduana)
            .Set("IndUsaDES", "S")
            .Set("CUITPrestador", u.Cuit)
            .Set("EstadoOperacion", u.Operacion)
            .Set("FechaEstadoOperacion", Day(u.FechaOperacion))
            .Set("FechaEstadoContenedor", Day(u.FechaEstado)));
        return call.Done(answer);
    }

    /// <summary>An ATA's data from the taxpayers ArcaSim knows: HABI while active, BAJA when not (states of p.31).</summary>
    private async Task<ContractAnswer> AtaAsync(ServiceCall call, CancellationToken ct)
    {
        var text = call.Request.Field("argCuitATA");
        if (text == "") return call.Fail(42034, Dia.MissingText("CuitATA"));
        if (text.Length != 11 || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var cuit))
            return call.Fail(27045, "Formato de CUIT Invalido");
        if (!Cuits.IsValid(cuit)) return call.Fail(20714, "El CUIT ingresado es inválido");
        if (await taxpayers.FindAsync(cuit, ct) is not { } ata) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(20304, TripOk)
            .Set("CuitATA", cuit)
            .Set("CodigoEstado", ata.Active ? "HABI" : "BAJA")
            .Set("RazonSocial", ata.Name.ToUpperInvariant());
        return call.Done(answer);
    }

    /// <summary>The aduana inside a destinación's number (AA BBB ...): its third to fifth characters.</summary>
    private static string AduanaOf(string destination) => destination.Length >= 5 ? destination[2..5] : "";

    private static string Day(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Or(string value, string fallback) => value == "" ? fallback : value;
}
