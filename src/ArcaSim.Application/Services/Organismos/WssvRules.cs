using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A transfer followed by a PEMA device, with the positions and alarms it reported.</summary>
public sealed record VehicleTransfer(
    long Cuit, string Id, string Device, string? Route, string? Container, string? Exit, bool Active, List<TransferPosition> Positions);

public sealed record TransferPosition(DateTimeOffset At, double Lat, double Lng, List<string> Alarms);

/// <summary>
/// Seguimiento vehicular (wssv, docs/arca/servicios/wssv.md): a provider (the
/// CUIT of the ticket) starts transfers, reports positions and alarms, and
/// ends them; ListarTrasladosActivos, ListarTraslado and ListarAlarmas read it
/// back, with positions in UTC as the manual shows. An IdTras starting with
/// TEST skips the state checks, as the manual allows in homologación: it
/// restarts, reports or ends whatever its state. The manual lists no ErrNum
/// nor ErrMsg, so ArcaSim's own are: 1 transfer already started, 2 transfer
/// that does not exist, 3 transfer already ended, 4 device that is not the
/// transfer's, 5 missing data. Success is ErrNum 0. Of the alarms only PTA
/// "Puerta Abierta" is documented; NPM and NPG are documented codes with
/// ArcaSim's wording.
/// </summary>
public sealed class WssvRules(IDocumentStore store) : IServiceBehavior
{
    public const string Transfers = "wssv.traslados";

    private static readonly (string Id, string Description)[] Alarms =
        [("PTA", "Puerta Abierta"), ("NPM", "Sin datos del PEMA"), ("NPG", "PEMA sin posicion GPS")];

    public string Service => "wssv";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        XNamespace ns = call.Operation.Output.Namespace;
        var id = call.Request.Text("IdTras") ?? "";
        var device = call.Request.Text("IdDES") ?? "";
        var test = id.StartsWith("TEST", StringComparison.Ordinal);
        var key = $"{call.Cuit}/{id}";
        var transfer = id.Length == 0 ? null : await store.GetAsync<VehicleTransfer>(Transfers, key, ct);

        switch (call.Name)
        {
            case "TrasladoBegin":
                if (id.Length == 0 || device.Length == 0) return Result(call, 5, "Faltan IdTras o IdDES.");
                if (transfer is not null && !test) return Result(call, 1, $"El traslado {id} ya fue iniciado.");
                await store.PutAsync(Transfers, key, new VehicleTransfer(call.Cuit, id, device, call.Request.Optional("IdRuta"),
                    call.Request.Optional("IdCont"), call.Request.Optional("IdSalida"), true, []), ct);
                return Result(call, 0, null);
            case "Reporte":
                if (id.Length == 0 || device.Length == 0) return Result(call, 5, "Faltan IdTras o IdDES.");
                if (!test && Refuse(transfer, id, device) is { } problem) return Result(call, problem.Code, problem.Text);
                transfer ??= new VehicleTransfer(call.Cuit, id, device, null, null, null, true, []);
                var at = DateTimeOffset.TryParse(call.Request.Text("FHDES"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? moment : DateTimeOffset.UtcNow;
                var alarms = (call.Request.Find("Alarmas")?.Elements().Select(e => e.Value.Trim()).Where(a => a.Length > 0) ?? []).ToList();
                transfer.Positions.Add(new TransferPosition(at, Number(call.Request, "Lat"), Number(call.Request, "Lng"), alarms));
                await store.PutAsync(Transfers, key, transfer, ct);
                return Result(call, 0, null);
            case "TrasladoEnd":
                if (id.Length == 0) return Result(call, 5, "Falta IdTras.");
                if (!test && Refuse(transfer, id, null) is { } ended) return Result(call, ended.Code, ended.Text);
                if (transfer is not null) await store.PutAsync(Transfers, key, transfer with { Active = false }, ct);
                return Result(call, 0, null);
            case "ListarTrasladosActivos":
                var active = (await store.ListAsync<VehicleTransfer>(Transfers, $"{call.Cuit}/", ct)).Where(t => t.Active);
                return Answer(call, new XElement(ns + "Traslados", active.Select(t => new XElement(ns + "Traslado",
                    new XElement(ns + "IdTras", t.Id), new XElement(ns + "IdDES", t.Device), t.Route is null ? null : new XElement(ns + "IdRuta", t.Route)))), 0, null);
            case "ListarTraslado":
                if (transfer is null) return Answer(call, null, 2, $"No existe el traslado {id}.");
                if (transfer.Device != device) return Answer(call, null, 4, $"El dispositivo {device} no corresponde al traslado {id}.");
                return Answer(call, new XElement(ns + "Coordenadas", transfer.Positions.Select(p => new XElement(ns + "Coordenada",
                    new XElement(ns + "Fecha", p.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                    new XElement(ns + "Lat", p.Lat.ToString(CultureInfo.InvariantCulture)),
                    new XElement(ns + "Lng", p.Lng.ToString(CultureInfo.InvariantCulture))))), 0, null);
            case "ListarAlarmas":
                return Answer(call, new XElement(ns + "Alarmas", Alarms.Select(a => new XElement(ns + "Alarma",
                    new XElement(ns + "Id", a.Id), new XElement(ns + "Des", a.Description)))), 0, null);
            default:
                return null;
        }
    }

    private static (int Code, string Text)? Refuse(VehicleTransfer? transfer, string id, string? device) =>
        transfer is null ? (2, $"No existe el traslado {id}.")
        : !transfer.Active ? (3, $"El traslado {id} ya fue finalizado.")
        : device is not null && transfer.Device != device ? (4, $"El dispositivo {device} no corresponde al traslado {id}.")
        : null;

    private static double Number(XElement request, string name) =>
        double.TryParse(request.Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>A *Result that only carries RError.</summary>
    private static ContractAnswer Result(ServiceCall call, int code, string? message) => Answer(call, null, code, message);

    private static ContractAnswer Answer(ServiceCall call, XElement? content, int code, string? message)
    {
        XNamespace ns = call.Operation.Output.Namespace;
        return call.Ok(new XElement(call.Operation.Output, new XElement(ns + (call.Name + "Result"),
            content,
            new XElement(ns + "RError", new XElement(ns + "ErrNum", code), message is null ? null : new XElement(ns + "ErrMsg", message)))));
    }
}
