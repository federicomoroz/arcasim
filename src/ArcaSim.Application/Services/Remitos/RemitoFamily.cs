using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>The codes one remito service answers the shared lifecycle's refusals with.</summary>
public sealed record RemitoCodes(RemitoProblem NotFound, RemitoProblem NotAllowed, RemitoProblem NotPending, RemitoProblem NotAuthorizer);

/// <summary>
/// The names carne and harina give the same things in consultarRemitos*:
/// the request's date range and point of emission, and in each item of the
/// answer its wrapper, its type and its request id.
/// </summary>
public sealed record RemitoListNames(string DateRange, string Point, string ItemName, string ItemType, string ItemRequestId);

/// <summary>
/// What the three remito services share: the authorizations a new remito
/// owes, the lifecycle (authorize, deny, cancel), the lists by role with
/// their paging, the parameter tables they answer alike, and the registral
/// checks on the parties
/// (wsremharina.md, wsremcarne.md, wsremazucar.md, "Máquina de estados").
/// </summary>
public static class RemitoFamily
{
    public const int PageSize = 2000;

    /// <summary>The top of a point of emission in the three WSDLs (PuntoEmisionSimpleType: 1 to 99999).</summary>
    public const int MaxPointOfEmission = 99_999;

    /// <summary>
    /// The top of a code that carne and harina can list: their WSDLs type
    /// CodigoDescripcionType.codigo as xsd:short, so a point of emission above
    /// it cannot be written in their answer. Azúcar's codigo is xsd:long.
    /// </summary>
    public const int MaxShortCode = short.MaxValue;

    public static readonly RemitoProblem NotRegistered = new(100, "CUIT debe encontrarse en el Sistema Registral");
    public static readonly RemitoProblem Inactive = new(101, "Debe encontrarse activa y sin limitaciones");

    /// <summary>
    /// The authorizations a new remito waits for: the holder's when the issuer
    /// is not the holder, the depositary's when the goods leave a third party's
    /// deposit. With none, the remito is issued at once.
    /// </summary>
    public static void Open(Remito remito, DateTimeOffset now)
    {
        if (remito.Holder != remito.Issuer) remito.Authorizations[RemitoStates.Holder] = "PE";
        if (remito.Depositary is { } depositary && depositary != remito.Issuer && depositary != remito.Holder)
            remito.Authorizations[RemitoStates.Depositary] = "PE";
        if (remito.Pending is { } role) remito.MoveTo(RemitoStates.PendingState(role), now, remito.Issuer);
    }

    /// <summary>The holder or the depositary authorizes (to the next pending, or PEM) or denies (DEN, final).</summary>
    public static RemitoProblem? Authorize(Remito remito, long cuit, bool approve, DateTimeOffset now, RemitoCodes codes)
    {
        if (remito.State is not (RemitoStates.PendingHolder or RemitoStates.PendingDepositary) || remito.Pending is not { } role)
            return codes.NotPending;
        if (cuit != (role == RemitoStates.Holder ? remito.Holder : remito.Depositary)) return codes.NotAuthorizer;
        remito.Authorizations[role] = approve ? "AU" : "RE";
        if (!approve)
        {
            remito.MoveTo(RemitoStates.Denied, now, cuit);
            return null;
        }
        remito.AuthorizedOn = now.ArgentinaDate();
        remito.MoveTo(remito.Pending is { } next ? RemitoStates.PendingState(next) : RemitoStates.PendingIssue, now, cuit);
        return null;
    }

    /// <summary>The issuer cancels a remito that was never issued: ANS, "Anulado sin emisión".</summary>
    public static RemitoProblem? Cancel(Remito remito, long cuit, DateTimeOffset now, RemitoCodes codes)
    {
        if (cuit != remito.Issuer) return codes.NotAllowed;
        if (remito.State is not (RemitoStates.PendingHolder or RemitoStates.PendingDepositary or RemitoStates.PendingIssue)) return codes.NotAllowed;
        remito.MoveTo(RemitoStates.CancelledUnissued, now, cuit);
        return null;
    }

    /// <summary>
    /// autorizarRemito of carne and harina: the holder or the depositary says A
    /// to authorize and anything else to deny.
    /// </summary>
    public static async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, RemitoLedger ledger, RemitoCodes codes, string wrapper, CancellationToken ct)
    {
        var code = call.Request.Long("codRemito");
        using var hold = await ledger.HoldAsync(code, call.Cuit, ct);
        if (hold.Remito is not { } remito) return OperationAnswer(call, wrapper, code, codes.NotFound);
        var problem = Authorize(remito, call.Cuit, call.Request.Text("estado") == "A", ledger.Now, codes);
        if (problem is null) await ledger.SaveAsync(remito, ct);
        return OperationAnswer(call, wrapper, code, problem);
    }

    /// <summary>anularRemito of carne and harina: the issuer cancels a remito that was never issued.</summary>
    public static async Task<ContractAnswer> CancelAsync(ServiceCall call, RemitoLedger ledger, RemitoCodes codes, string wrapper, CancellationToken ct)
    {
        var code = call.Request.Long("codRemito");
        using var hold = await ledger.HoldAsync(code, call.Cuit, ct);
        if (hold.Remito is not { } remito) return OperationAnswer(call, wrapper, code, codes.NotFound);
        var problem = Cancel(remito, call.Cuit, ledger.Now, codes);
        if (problem is null) await ledger.SaveAsync(remito, ct);
        return OperationAnswer(call, wrapper, code, problem);
    }

    /// <summary>A party must be in the registry and active (codes 100 and 101).</summary>
    public static async Task<RemitoProblem?> CheckPartyAsync(PadronDirectory directory, long? cuit, CancellationToken ct)
    {
        if (cuit is not { } number) return null;
        var taxpayer = await directory.FindAsync(number, ct);
        return taxpayer is null ? NotRegistered : taxpayer.Active ? null : Inactive;
    }

    public static async Task<string> NameOfAsync(PadronDirectory directory, long cuit, CancellationToken ct) =>
        (await directory.FindAsync(cuit, ct))?.Name.ToUpperInvariant() is { Length: >= 3 } name ? name : $"CUIT {cuit}";

    // ---- Lists ------------------------------------------------------------------------

    public static bool Within(DateOnly date, DateOnly? from, DateOnly? to) => (from is null || date >= from) && (to is null || date <= to);

    public static IEnumerable<Remito> ForIssuer(IEnumerable<Remito> all, long cuit, int point, int? type, string? state, DateOnly? from, DateOnly? to) =>
        all.Where(r => r.Issuer == cuit && r.Point == point && (type is null || r.Type == type)
                       && (string.IsNullOrEmpty(state) || r.State == state) && Within(r.CreatedOn, from, to));

    /// <summary>As holder (TIT) or depositary (DEP): PE pending, AU authorized, RE refused. The range only filters what was decided.</summary>
    public static IEnumerable<Remito> ForAuthorizer(IEnumerable<Remito> all, long cuit, string role, string status, long? issuer, DateOnly? from, DateOnly? to) =>
        all.Where(r => (role == RemitoStates.Holder ? r.Holder : r.Depositary) == cuit
                       && r.Authorizations.TryGetValue(role, out var answer) && answer == status
                       && (status != "PE" || r.Pending == role)
                       && (issuer is null || r.Issuer == issuer)
                       && (status == "PE" || Within(r.AuthorizedOn ?? r.CreatedOn, from, to)));

    /// <summary>As receiver: PEN for issued and not yet received, else the reception's state. The range only filters what was received.</summary>
    public static IEnumerable<Remito> ForReceiver(IEnumerable<Remito> all, long cuit, string status, long? issuer, DateOnly? from, DateOnly? to) =>
        all.Where(r => r.Receiver == cuit && r.State == (status == "PEN" ? RemitoStates.Issued : status)
                       && (issuer is null || r.Issuer == issuer)
                       && (status == "PEN" || Within(r.ReceivedOn ?? r.CreatedOn, from, to)));

    /// <summary>How many pages <paramref name="total"/> remitos fill.</summary>
    public static int PageCount(int total) => Paging.Count(total, PageSize);

    /// <summary>
    /// One page of the remitos in code order, and whether more follow. A page below 1 is the first; one the client
    /// counts past the last is empty and has no more: the number is a long because a page times
    /// <see cref="PageSize"/> overflows an int from page 1,073,743.
    /// </summary>
    public static (List<Remito> Items, bool More) Page(IEnumerable<Remito> remitos, long page) =>
        Paging.Page(remitos.OrderBy(r => r.Code).ToList(), Math.Max(page, 1), PageSize);

    /// <summary>
    /// The consultarRemitos* answer carne and harina share: the remitos the
    /// caller has as issuer, as holder or depositary, or as receiver (the date
    /// range only filters what was decided or received), one page of them with
    /// its number and whether more follow, or 3034 when nothing matched.
    /// </summary>
    public static ContractAnswer ListAnswer(ServiceCall call, IReadOnlyList<Remito> all, RemitoListNames names)
    {
        var request = RemitoXml.Plain(call.Request);
        var range = request.Element(names.DateRange);
        var (from, to) = (range.ChildDate("fechaDesde"), range.ChildDate("fechaHasta"));
        var issuer = request.ChildLong("cuitEmisor");
        var found = call.Name switch
        {
            "consultarRemitosEmisor" => ForIssuer(all, call.Cuit, (int)(request.ChildLong(names.Point) ?? 0),
                (int?)request.ChildLong("tipoComprobante"), request.ChildText("estado"), from, to),
            "consultarRemitosAutorizador" => ForAuthorizer(all, call.Cuit, request.ChildText("rolAutorizador") ?? "",
                request.ChildText("estadoAutorizacion") ?? "", issuer, from, to),
            _ => ForReceiver(all, call.Cuit, request.ChildText("estadoRecepcion") ?? "", issuer, from, to),
        };

        var page = Math.Max(call.Request.Long("nroPagina"), 1);
        var (items, more) = Page(found, page);
        var body = items.Count == 0
            ? new XElement("consultarRemitosReturn", RemitoXml.Errors([new RemitoProblem(3034, "Remitos no encontrados")]))
            : new XElement("consultarRemitosReturn",
                new XElement("arrayRemitos", items.Select(r => ListItem(r, names))),
                new XElement("nroPagina", page),
                new XElement("hayMas", more ? "S" : "N"));
        return call.Ok(new XElement(call.Operation.Output, body));
    }

    private static XElement ListItem(Remito r, RemitoListNames names) => new(names.ItemName,
        new XElement("cuitEmisor", r.Issuer),
        new XElement("codRemito", r.Code),
        new XElement("puntoEmision", r.Point),
        new XElement(names.ItemType, r.Type),
        r.Number is { } number ? new XElement("nroRemito", number) : null,
        new XElement(names.ItemRequestId, r.RequestId),
        new XElement("estadoActual", r.State),
        new XElement("fechaOper", RemitoXml.Date(r.History[^1].At)));

    /// <summary>The short answer of the operations that only change state: codRemito, resultado and the errors.</summary>
    public static ContractAnswer OperationAnswer(ServiceCall call, string wrapper, long code, IReadOnlyCollection<RemitoProblem> problems) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            new XElement("codRemito", code),
            new XElement("resultado", problems.Count == 0 ? "A" : "R"),
            RemitoXml.Errors(problems))));

    public static ContractAnswer OperationAnswer(ServiceCall call, string wrapper, long code, RemitoProblem? problem) =>
        OperationAnswer(call, wrapper, code, problem is null ? [] : [problem]);

    /// <summary>The history of states with who moved it and their name, as consultarEstadosRemito lists it.</summary>
    public static async Task<XElement> HistoryAsync(PadronDirectory directory, Remito remito, string userText, CancellationToken ct)
    {
        var steps = new List<XElement>();
        foreach (var step in remito.History)
            steps.Add(new XElement("estados",
                new XElement("estado", step.State),
                new XElement("fecha", RemitoXml.Date(step.At)),
                new XElement("cuitUsuario", step.Cuit),
                new XElement(userText, await NameOfAsync(directory, step.Cuit, ct))));
        return new XElement("arrayEstados", steps);
    }

    /// <summary>
    /// The issuer's points of sale as points of emission (ArcaSim's choice: the
    /// remitos have no registry of their own), those a code of the service's
    /// answer can hold.
    /// </summary>
    private static async Task<IEnumerable<(object Code, string Text)>> PointsAsync(PadronDirectory directory, long cuit, int maxCode, CancellationToken ct) =>
        (await directory.FindAsync(cuit, ct))?.PointsOfSale
            .Where(p => !p.Blocked && p.Number <= maxCode)
            .OrderBy(p => p.Number)
            .Select(p => ((object)p.Number, $"PUNTO DE EMISION {p.Number}")) ?? [];

    /// <summary>consultarPuntosEmision: the issuer's points of emission that a code of the service can hold.</summary>
    public static async Task<ContractAnswer> PointsAnswerAsync(ServiceCall call, PadronDirectory directory, int maxCode, CancellationToken ct) =>
        Table(call, "consultarPuntosEmisionReturn", "arrayPuntosEmision", "codigoDescripcion", await PointsAsync(directory, call.Cuit, maxCode, ct));

    /// <summary>consultarCodigosDomicilio: the fiscal address of the CUIT asked about, or 100 when the registry does not have it.</summary>
    public static async Task<ContractAnswer> AddressesAnswerAsync(ServiceCall call, PadronDirectory directory, CancellationToken ct)
    {
        var address = await FiscalAddressAsync(directory, call.Request.Long("cuitTitularDomicilio"), ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarCodigosDomicilioReturn",
            address is { } found
                ? RemitoXml.Codes("arrayDomicilios", "codigoDescripcion", [found])
                : RemitoXml.Errors([NotRegistered]))));
    }

    /// <summary>A parameter table: its rows as codigo and descripcion, under the wrapper, array and item names the service gives them.</summary>
    public static ContractAnswer Table(ServiceCall call, string wrapper, string array, string item, IEnumerable<(object Code, string Text)> rows) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper, RemitoXml.Codes(array, item, rows))));

    /// <summary>Code 0, the fiscal address, the one every carne manual example uses as destination.</summary>
    public static async Task<(object Code, string Text)?> FiscalAddressAsync(PadronDirectory directory, long cuit, CancellationToken ct)
    {
        if (await directory.FindAsync(cuit, ct) is not { } taxpayer) return null;
        var address = PadronDirectory.AddressOf(taxpayer);
        return (0, $"{address.Street} - {address.Locality} ({address.PostalCode}) - {address.Province}");
    }

    /// <summary>
    /// 2602 for a CUIT that cannot receive remitos: not in the registry or not
    /// active (wsremharina manual, example 2.5.29.6).
    /// </summary>
    public static async Task<ContractAnswer> ValidReceiversAsync(ServiceCall call, PadronDirectory directory, CancellationToken ct)
    {
        var problems = new List<RemitoProblem>();
        foreach (var cuit in RemitoXml.Plain(call.Request).Descendants("cuitReceptor").Select(e => e.Value.Trim()))
            if (!long.TryParse(cuit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || await directory.FindAsync(number, ct) is not { Active: true })
                problems.Add(new RemitoProblem(2602, $"Actualmente, la CUIT {cuit} NO está habilitada para ser destinatario de nuevos remitos de mercadería"));
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarReceptoresValidosReturn",
            new XElement("resultado", problems.Count == 0 ? "A" : "R"),
            RemitoXml.Errors(problems))));
    }
}
