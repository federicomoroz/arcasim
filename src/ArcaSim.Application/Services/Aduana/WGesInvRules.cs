using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>A wine export despacho blocked until the INV approves it: PEND, then APRO (with its secuencia) or DENE.</summary>
public sealed record InvDespacho(
    long Cuit, string Aduana, string Id, long Transaccion, DateTimeOffset Oficializacion, long CuitExportador, string Exportador,
    string Estado = "PEND", long NroSecuencia = 0);

/// <summary>A VUCEA form waiting for the INV to approve (A) or reject (R) it.</summary>
public sealed record VuceaForm(long Cuit, long NroTramite, long Transaccion, string IdDestinacion, long CuitRegistro, DateTimeOffset Registro, string Estado = "");

/// <summary>
/// WGesINV, the INV's service for wine exports (docs/arca/servicios/WGesINV.md):
/// despachos that show up pending, AprobarDespacho (with its NroSecuencia) or
/// DenegarDespacho, and the VUCEA forms approved or rejected with
/// AsignarEstadoVUCEA, with the manual's codes and 20304 "Procedimiento terminado
/// OK." on success. ArcaSim's choices where the manual is silent: argIdTransaccion
/// is a cursor (what is newer than it comes back); the first ConsultaDespachosPendientes
/// of a CUIT with no despachos finds three oficializaciones, the first with a VUCEA
/// form, since nothing else in ArcaSim makes wine despachos; and a resolved despacho
/// no longer shows as pending.
/// </summary>
public sealed class WGesInvRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Despachos = "WGesINV.despachos";
    private const string Forms = "WGesINV.vucea";
    private const string Ok = "Procedimiento terminado OK.";

    /// <summary>10121's text with the final period WGesINV.md prints, unlike the other customs services (Dia.NoData).</summary>
    private const string NoData = "No hay datos para los criterios ingresados.";

    /// <summary>The first query of a CUIT finds its despachos; requests that arrive together make them once.</summary>
    private readonly KeyedLocks<long> _seeding = new();

    public string Service => "WGesINV";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "ConsultaDespachosPendientes" => await PendingAsync(call, ct),
        "AprobarDespacho" => await ResolveAsync(call, call.Arg("argAprobarDespacho"), approve: true, ct),
        "DenegarDespacho" => await ResolveAsync(call, call.Arg("argDenegarDespacho"), approve: false, ct),
        "ConsultaIdTransaccionDespacho" => await TransactionAsync(call, ct),
        "ConsultaVUCEAPendientes" => await FormsAsync(call, ct),
        "AsignarEstadoVUCEA" => await AssignAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> PendingAsync(ServiceCall call, CancellationToken ct)
    {
        await SeedAsync(call.Cuit, ct);
        var after = call.Request.Long("argIdTransaccion");
        var pending = (await store.ListAsync<InvDespacho>(Despachos, $"{call.Cuit}/", ct))
            .Where(d => d.Estado == "PEND" && d.Transaccion > after).OrderBy(d => d.Transaccion).ToList();
        if (pending.Count == 0) return call.Fail(10121, NoData);

        var answer = call.Sample().Receipt(20304, Ok);
        answer.Repeat("Oficializacion", pending, (row, d) =>
        {
            row.Set("AduanaSalida", d.Aduana)
                .Set("IdDestinacion", d.Id)
                .Set("FechaOficializacion", d.Oficializacion)
                .Set("CuitExportador", d.CuitExportador)
                .Set("RazonSocial", d.Exportador)
                .Set("InvTransacExpo", d.Transaccion)
                .Drop("SubItems").Drop("Terceros");
            row.Find("Item")!
                .Set("NroItem", 1)
                .Set("PosicionArancelaria", "2204.21.00")
                .Set("CantidadUnidadDeclarada", 1200m)
                .Set("CantidadUnidadEstadistica", 1200m)
                .Set("Peso", 1500m)
                .Set("FobDivisa", 4800m)
                .Set("FobDolares", 4800m)
                .Set("CantidadMosto", 0m);
        });
        foreach (var list in new[] { "SinDiferencias", "PostEmbarques", "Reversiones", "Anulaciones", "Rectificaciones", "RectificacionesPaisDestino" })
            answer.Find(list)!.RemoveNodes();
        answer.Set("CantidadOficializaciones", pending.Count);
        foreach (var count in new[] { "CantidadSinDiferencias", "CantidadPostEmbarques", "CantidadReversiones", "CantidadAnulaciones", "CantidadRectificaciones", "CantidadRectificacionesPaisDestino" })
            answer.Set(count, 0);
        answer.Set("CantidadTotal", pending.Count);
        return call.Done(answer);
    }

    private async Task<ContractAnswer> ResolveAsync(ServiceCall call, XElement? arg, bool approve, CancellationToken ct)
    {
        var user = approve ? "IdUsuarioDesbloqueo" : "IdUsuarioDenegacion";
        if (Dia.FirstMissing(arg, "Aduana", "IdDestinacion", user) is { } missing) return call.Fail(42034, Dia.MissingText(missing));
        var aduana = arg.Field("Aduana");
        var id = arg.Field("IdDestinacion");
        if (aduana.Length != 3 || !aduana.All(char.IsDigit)) return call.Fail(10015, "Código de aduana no valido o inexistente");
        if (id.Length != 16) return call.Fail(10566, "Campo IdDestinacion longitud invalida.");
        if (await store.GetAsync<InvDespacho>(Despachos, $"{call.Cuit}/{id}", ct) is not { } despacho) return call.Fail(20150, "Destinación Inexistente.");
        if (despacho.Aduana != aduana) return call.Fail(10065, "Ese identificador no corresponde a ninguna declaración");

        if (approve)
        {
            if (despacho.Estado != "PEND") return call.Fail(30330, "Destinación no tiene el motivo de desbloqueo pendiente de desbloquear");
            var sequence = await store.NextAsync("WGesINV.secuencias", ct);
            await store.PutAsync(Despachos, $"{call.Cuit}/{id}", despacho with { Estado = "APRO", NroSecuencia = sequence }, ct);
            return call.Done(call.Sample().Receipt(20304, Ok).Set("NroSecuencia", sequence));
        }

        if (despacho.Estado == "APRO") return call.Fail(30687, $"Desbloqueo ya registrado {id}");
        if (despacho.Estado == "DENE") return call.Fail(30688, $"Denegacion de desbloqueo ya registrado {id}");
        await store.PutAsync(Despachos, $"{call.Cuit}/{id}", despacho with { Estado = "DENE" }, ct);
        return call.Done(call.Sample().Receipt(20304, Ok));
    }

    private async Task<ContractAnswer> TransactionAsync(ServiceCall call, CancellationToken ct)
    {
        var id = call.Request.Field("argIdDespacho");
        if (id == "") return call.Fail(42034, Dia.MissingText("IdDespacho"));
        if (await store.GetAsync<InvDespacho>(Despachos, $"{call.Cuit}/{id}", ct) is not { } despacho)
            return call.Fail(30349, $"Código Despacho {id} inexistente");
        return call.Done(call.Sample().Receipt(20304, Ok).Set("IdTransaccion", despacho.Transaccion));
    }

    private async Task<ContractAnswer> FormsAsync(ServiceCall call, CancellationToken ct)
    {
        await SeedAsync(call.Cuit, ct);
        var after = call.Request.Long("argIdTransaccion");
        var pending = (await store.ListAsync<VuceaForm>(Forms, $"{call.Cuit}/", ct))
            .Where(f => f.Estado == "" && f.Transaccion > after).ToList();
        if (pending.Count == 0) return call.Fail(10121, NoData);

        var answer = call.Sample().Receipt(20304, Ok);
        answer.Repeat("FormularioVUCEA", pending, (row, f) =>
        {
            row.Set("CuitRegistro", f.CuitRegistro)
                .Set("FechaRegistro", f.Registro)
                .Set("NroTramite", f.NroTramite)
                .Set("IdTransaccionTramite", f.Transaccion)
                .Set("IdDestinacion", f.IdDestinacion)
                .Drop("ItemsVUCEA");
            // Rubro 5001 is the form's header and campo 101 its registration date (manual, pp.44-45).
            row.Find("Campo")!.Set("IdRubro", 5001).Set("IdCampo", 101).Set("Valor", f.Registro.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        });
        return call.Done(answer.Set("CantidadFormulariosVUCEA", pending.Count));
    }

    private async Task<ContractAnswer> AssignAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argAsignarEstadoVUCEA");
        if (Dia.FirstMissing(arg, "IdDestinacion", "Estado") is { } missing) return call.Fail(42034, Dia.MissingText(missing));
        var state = arg.Field("Estado");
        if (state is not ("A" or "R")) return call.Fail(411, "Estado inválido");
        var number = arg?.Long("NroTramite") ?? 0;
        var key = $"{call.Cuit}/{number:D10}";
        if (await store.GetAsync<VuceaForm>(Forms, key, ct) is not { } form || form.Transaccion != (arg?.Long("IdTransaccionTramite") ?? 0))
            return call.Fail(30349, $"Código NroTramite {number} inexistente");
        if (form.Estado != "") return call.Fail(411, "Estado inválido");
        await store.PutAsync(Forms, key, form with { Estado = state }, ct);
        return call.Done(call.Sample().Receipt(20304, Ok));
    }

    /// <summary>Three oficializaciones from aduana 001, each with its transaction, the first with a VUCEA form.</summary>
    private async Task SeedAsync(long cuit, CancellationToken ct)
    {
        using var turn = await _seeding.AcquireAsync(cuit, ct);
        if ((await store.ListAsync<InvDespacho>(Despachos, $"{cuit}/", ct)).Count > 0) return;
        var now = clock.Now.ToArgentina();
        for (var i = 0; i < 3; i++)
        {
            var transaction = await store.NextAsync("WGesINV.transacciones", ct);
            var number = await store.NextAsync("WGesINV.destinaciones", ct);
            var id = Dia.DeclarationOf(now.ArgentinaDate(), "001", "EC01", number);
            await store.PutAsync(Despachos, $"{cuit}/{id}",
                new InvDespacho(cuit, "001", id, transaction, now.AddHours(-i - 1), Dia.SeededCompany, "BODEGA DEL SIMULADOR SA"), ct);
            if (i > 0) continue;
            var form = new VuceaForm(cuit, await store.NextAsync("WGesINV.tramites", ct), transaction, id, Dia.SeededCompany, now.AddHours(-1));
            await store.PutAsync(Forms, $"{cuit}/{form.NroTramite:D10}", form, ct);
        }
    }
}
