using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>A CUIT's rating in the AGR registry: the response code (ConsultaCodResp) and the observation (ConsultaObs).</summary>
public sealed record AgrRating(long Cuit, string? Period, string Rsp, string CodObs);

/// <summary>One rating a Consulta returned to a consultant, with its transaction code and date.</summary>
public sealed record AgrQuery(long Consultant, long Cuit, string Period, string Rsp, string CodObs, string RTran, string FTran, DateTimeOffset At);

/// <summary>
/// The AGR registry wsagr answers from, in the document store's "wsagr"
/// collection: ratings by CUIT (for one period, or for every period), the
/// consults made, and the most CUITs one Consulta takes. Tests and tools
/// write the ratings here; there is no admin endpoint for them yet.
/// </summary>
public static class AgrRegistry
{
    public const string Collection = "wsagr";

    /// <summary>The manual's example value for ConsultaCantCuit.</summary>
    public const int DefaultLimit = 100;

    private sealed record Limit(int Cantidad);

    /// <summary>Rates a CUIT for one period ("MM/AAAA"), or for every period when none is given.</summary>
    public static Task RateAsync(this IDocumentStore store, long cuit, string rsp, string? period = null, string codObs = "", CancellationToken ct = default) =>
        store.PutAsync(Collection, RatingKey(cuit, period), new AgrRating(cuit, period, rsp, codObs), ct);

    public static Task SetLimitAsync(this IDocumentStore store, int cantidad, CancellationToken ct = default) =>
        store.PutAsync(Collection, "parametros/cantidad", new Limit(cantidad), ct);

    public static async Task<int> LimitAsync(this IDocumentStore store, CancellationToken ct) =>
        (await store.GetAsync<Limit>(Collection, "parametros/cantidad", ct))?.Cantidad ?? DefaultLimit;

    public static async Task<AgrRating?> RatingAsync(this IDocumentStore store, long cuit, string period, CancellationToken ct) =>
        await store.GetAsync<AgrRating>(Collection, RatingKey(cuit, period), ct) ?? await store.GetAsync<AgrRating>(Collection, RatingKey(cuit, null), ct);

    public static async Task RecordAsync(this IDocumentStore store, AgrQuery query, CancellationToken ct)
    {
        var sequence = await store.NextAsync("wsagr.consulta", ct);
        await store.PutAsync(Collection, $"consulta/{query.Consultant}/{query.Cuit}/{PeriodKey(query.Period)}/{sequence:D12}", query, ct);
        await store.PutAsync(Collection, $"porcuit/{query.Cuit}/{PeriodKey(query.Period)}/{sequence:D12}", query, ct);
    }

    /// <summary>The consultant's own consults, oldest first.</summary>
    public static Task<IReadOnlyList<AgrQuery>> QueriesOfAsync(this IDocumentStore store, long consultant, CancellationToken ct) =>
        store.ListAsync<AgrQuery>(Collection, $"consulta/{consultant}/", ct);

    /// <summary>Anyone's consults of a CUIT for a period, oldest first.</summary>
    public static Task<IReadOnlyList<AgrQuery>> QueriesOfCuitAsync(this IDocumentStore store, long cuit, string period, CancellationToken ct) =>
        store.ListAsync<AgrQuery>(Collection, $"porcuit/{cuit}/{PeriodKey(period)}/", ct);

    /// <summary>"MM/AAAA" as AAAAMM, so keys sort by period.</summary>
    public static string PeriodKey(string period) => period.Length == 7 ? period[3..] + period[..2] : period;

    private static string RatingKey(long cuit, string? period) => period is null ? $"calificacion/{cuit}" : $"calificacion/{cuit}/{PeriodKey(period)}";
}

/// <summary>
/// WSAGR, Reproweb (docs/arca/servicios/wsagr.md): Consulta rates a batch of
/// CUITs for a period from the AGR registry and records each answer with a
/// transaction code; ConsultaHistorica reads the consultant's own records,
/// ConsultaCondRet anyone's, ConsultaRectificada the CUITs whose rating
/// changed since the consultant asked. The parameter consults serve the
/// manual's codes. Errors go in Respuesta/Err, and per CUIT in
/// DetalleCuits/Err, after the general checks (period, amount, duplicates).
/// ArcaSim's choices where the spec says NO VERIFICADO: a registered and
/// active CUIT with no rating in the registry gets Rsp 1 (general
/// retention); the period window (107) applies to Consulta only; every
/// Consulta makes a new record with a new RTran (21 digits); an empty
/// history answers Respuesta with an empty Det; ConsultaRectificada answers
/// the current rating without RTran, and 103 when nothing changed;
/// ConsultaCondRet does not show other consultants' RTran. The Msg texts of
/// 102, 104, 107, 111 and 113 are ArcaSim's wording of the manual's rules;
/// 103, 108 and 118 are the manual's.
/// </summary>
public sealed partial class WsagrRules(IDocumentStore store, IClock clock, PadronDirectory directory) : IServiceBehavior
{
    /// <summary>ConsultaCodResp: the manual's example (§2.6.5).</summary>
    private static readonly (string Code, string Text)[] Responses =
    [
        ("0", "RG 2226"),
        ("1", "Retención General vigente RG 2854, ..."),
        ("2", "Retención Sustitutiva 100% RG 2854 (AFIP)"),
        ("3", "CREDITO FISCAL NO COMPUTABLE"),
        ("4", "Retención Sustitutiva 100% RG 2854 (AFIP) - Irregularidades en la cadena de comercialización del proveedor"),
        ("5", "RETENCION SUSTITUTIVA 100% R.G. 1575 AFIP - HABILITADO FACTURA \"M\""),
    ];

    /// <summary>ConsultaObs: the manual's example (§2.7).</summary>
    private static readonly (string Code, string Text)[] Observations = [("10", "factura M")];

    private const string DefaultRating = "1";

    // ASCII digits only: \d would let in the digits of other scripts, which no parse below reads.
    [GeneratedRegex(@"^(0[1-9]|1[0-2])/[0-9]{4}$")]
    private static partial Regex PeriodFormat();

    public string Service => "wsagr";

    private DateTimeOffset Now => clock.Now.ToArgentina();

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "Consulta" => await ConsultAsync(call, ct),
        "ConsultaHistorica" => await HistoryAsync(call, ct),
        "ConsultaRectificada" => await RectifiedAsync(call, ct),
        "ConsultaCondRet" => await OthersAsync(call, ct),
        "ConsultaCodResp" => Answer(call, Table(call, "Rsp", "WsResp", Responses)),
        "ConsultaObs" => Answer(call, Table(call, "Obs", "WsObs", Observations)),
        "ConsultaCantCuit" => Answer(call, Q(call, "Cantidad", await store.LimitAsync(ct))),
        _ => null,
    };

    /// <summary>Rates each CUIT for the period and records it; a CUIT not in the registry gets its own 108.</summary>
    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        var period = Period(call);
        var cuits = Cuits(call);
        if ((CheckPeriod(period, window: true) ?? await CheckCuitsAsync(cuits, required: true, ct)) is { } problem) return Failed(call, problem);

        var details = new List<XElement>();
        var fTran = Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        foreach (var cuit in cuits)
        {
            if (await RateAsync(cuit, period!, ct) is not { } rating)
            {
                details.Add(Detail(call, cuit, period!, error: (108, "El CUIT es invalido o inexistente")));
                continue;
            }
            var query = new AgrQuery(call.Cuit, cuit, period!, rating.Rsp, rating.CodObs, NewTransaction(), fTran, Now);
            await store.RecordAsync(query, ct);
            details.Add(Detail(call, query, withTransaction: true));
        }
        return Answer(call, Q(call, "Det", details));
    }

    /// <summary>The consultant's own records, by CUIT, by period or both; nothing found is an empty answer, not an error.</summary>
    private async Task<ContractAnswer> HistoryAsync(ServiceCall call, CancellationToken ct)
    {
        var period = Period(call);
        var cuits = Cuits(call);
        if (((period is null ? null : CheckPeriod(period, window: false)) ?? await CheckCuitsAsync(cuits, required: false, ct)) is { } problem)
            return Failed(call, problem);
        if (period is null && cuits.Count == 0) return Answer(call, Q(call, "Det"));
        var found = (await store.QueriesOfAsync(call.Cuit, ct))
            .Where(q => (period is null || q.Period == period) && (cuits.Count == 0 || cuits.Contains(q.Cuit)))
            .OrderBy(q => q.At);
        return Answer(call, Q(call, "Det", found.Select(q => Detail(call, q, withTransaction: true))));
    }

    /// <summary>The CUITs the consultant asked about for the period whose rating is no longer the one it got.</summary>
    private async Task<ContractAnswer> RectifiedAsync(ServiceCall call, CancellationToken ct)
    {
        var period = Period(call);
        if (CheckPeriod(period, window: false) is { } problem) return Failed(call, problem);
        var changed = new List<XElement>();
        foreach (var last in (await store.QueriesOfAsync(call.Cuit, ct)).Where(q => q.Period == period).GroupBy(q => q.Cuit).Select(g => g.MaxBy(q => q.At)!))
            if (await RateAsync(last.Cuit, period!, ct) is { } now && (now.Rsp != last.Rsp || now.CodObs != last.CodObs))
                changed.Add(Detail(call, last with { Rsp = now.Rsp, CodObs = now.CodObs }, withTransaction: false));
        return changed.Count == 0 ? Failed(call, (103, "No se encontraron registros para dicha consulta")) : Answer(call, Q(call, "Det", changed));
    }

    /// <summary>What anyone was last told about each CUIT for the period.</summary>
    private async Task<ContractAnswer> OthersAsync(ServiceCall call, CancellationToken ct)
    {
        var period = Period(call);
        var cuits = Cuits(call);
        if (string.IsNullOrEmpty(period)) return Failed(call, (118, "Es obligatorio el ingreso de un periodo (formato MM/AAAA)"));
        if ((CheckPeriod(period, window: false) ?? await CheckCuitsAsync(cuits, required: true, ct)) is { } problem) return Failed(call, problem);
        var details = new List<XElement>();
        foreach (var cuit in cuits)
            if ((await store.QueriesOfCuitAsync(cuit, period, ct)).MaxBy(q => q.At) is { } last)
                details.Add(Detail(call, last, withTransaction: false));
        return Answer(call, Q(call, "Det", details));
    }

    // ---- Checks ------------------------------------------------------------------------

    /// <summary>113 for a period not written MM/AAAA; with the window, 107 unless it is this month, or next month from the 16th.</summary>
    private (int, string)? CheckPeriod(string? period, bool window)
    {
        if (period is null || !PeriodFormat().IsMatch(period)) return (113, "El periodo debe tener el formato MM/AAAA");
        if (!window) return null;
        var today = DateOnly.FromDateTime(Now.DateTime);
        // There is no year 0000: it is read as the first year, which is no current month either (107).
        var asked = new DateOnly(Math.Max(1, int.Parse(period[3..], CultureInfo.InvariantCulture)), int.Parse(period[..2], CultureInfo.InvariantCulture), 1);
        var current = new DateOnly(today.Year, today.Month, 1);
        return asked == current || (asked == current.AddMonths(1) && today.Day > 15)
            ? null
            : (107, "Solo se puede consultar el mes corriente, o el mes siguiente a partir del día 16");
    }

    private async Task<(int, string)?> CheckCuitsAsync(IReadOnlyList<long> cuits, bool required, CancellationToken ct)
    {
        if (required && cuits.Count == 0) return (111, "Debe ingresar como mínimo una CUIT");
        var limit = await store.LimitAsync(ct);
        if (cuits.Count > limit) return (102, $"La cantidad de CUITs supera el máximo permitido ({limit})");
        if (cuits.Distinct().Count() != cuits.Count) return (104, "Existen CUITs duplicadas en la consulta");
        return null;
    }

    /// <summary>The registry's rating for the period, else for every period, else Rsp 1 for an active registered taxpayer.</summary>
    private async Task<AgrRating?> RateAsync(long cuit, string period, CancellationToken ct)
    {
        if (await store.RatingAsync(cuit, period, ct) is { } rating) return rating;
        return await directory.FindAsync(cuit, ct) is { Active: true } ? new AgrRating(cuit, period, DefaultRating, "") : null;
    }

    // ---- Shapes ------------------------------------------------------------------------

    private static string? Period(ServiceCall call) => call.Request.Element(call.Request.Name.Namespace + "Periodo")?.Value.Trim();

    /// <summary>The CUITs to consult: Cuit repeated right under the operation, not the one in Auth.</summary>
    private static List<long> Cuits(ServiceCall call) =>
        call.Request.Elements(call.Request.Name.Namespace + "Cuit")
            .Select(e => long.TryParse(e.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var cuit) ? cuit : 0)
            .ToList();

    private static string NewTransaction() =>
        RandomNumberGenerator.GetInt32(1, 10).ToString(CultureInfo.InvariantCulture)
        + string.Concat(Enumerable.Range(0, 20).Select(_ => RandomNumberGenerator.GetInt32(0, 10)));

    /// <summary>The service's qualified element: every element of wsagr is in its namespace.</summary>
    private static XElement Q(ServiceCall call, string name, params object?[] content) => new(call.Operation.Output.Namespace + name, content);

    private static XElement Detail(ServiceCall call, AgrQuery query, bool withTransaction) => Q(call, "DetalleCuits",
        Q(call, "Cuit", query.Cuit),
        Q(call, "Pdo", query.Period),
        Q(call, "Rsp", query.Rsp),
        withTransaction ? Q(call, "RTran", query.RTran) : null,
        Q(call, "FTran", query.FTran),
        Q(call, "CodObs", query.CodObs));

    private static XElement Detail(ServiceCall call, long cuit, string period, (int Code, string Msg) error) => Q(call, "DetalleCuits",
        Q(call, "Cuit", cuit),
        Q(call, "Pdo", period),
        Error(call, error));

    private static XElement Error(ServiceCall call, (int Code, string Msg) error) => Q(call, "Err", Q(call, "Code", error.Code), Q(call, "Msg", error.Msg));

    private static XElement Table(ServiceCall call, string list, string item, IEnumerable<(string Code, string Text)> rows) =>
        Q(call, list, rows.Select(r => Q(call, item, Q(call, "Code", r.Code), Q(call, "Msg", r.Text))));

    private static ContractAnswer Answer(ServiceCall call, params object?[] content) =>
        call.Ok(Q(call, call.Operation.Output.LocalName, Q(call, "Respuesta", content)));

    /// <summary>A general error: Respuesta/Err alone, no Det.</summary>
    private static ContractAnswer Failed(ServiceCall call, (int Code, string Msg) error) => Answer(call, Error(call, error));
}
