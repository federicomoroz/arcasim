using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using static ArcaSim.Application.Services.Fce.FceSpring;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// wsfecredagente, the Agentes de Depósito Colectivo's side of the FCE
/// (docs/arca/servicios/wsfecredagente.md): the agent opens and closes the
/// accounts sellers then pick in wsfecred, reads the invoices sellers reported
/// to those accounts (D turns P on the page that returns it) and accepts or
/// rejects them (P turns A or R), which wsfecred shows the seller in
/// infoAgtDptoCltv. Format errors go in erroresFormato, business errors in
/// errores, per item in each resultado.
///
/// Where the manual is silent (NO VERIFICADO), these are ArcaSim's choices:
/// - any taxpayer registered in ArcaSim may act as an agent; anyone else gets
///   4009 in errores;
/// - cuentaId must have 3 to 20 characters, as wsfecred's idCuenta (2005);
/// - opening an account that is already active is 4012 in its item; opening
///   one that was closed reopens it for the holder sent;
/// - closing an account with invoices in D or P is allowed;
/// - only the D invoices returned on the page turn P, and they come back
///   already in P; a later read keeps the first fechaHoraLecturaAgente;
/// - aceptada N without a codRechazo from obtenerMotivosRechazo, or S with
///   one, is 2006 for the whole batch; confirming an invoice in R is 4019;
/// - after a rejection the wsfecred account goes back to Aceptada, so the
///   seller may report the invoice again;
/// - a query with nothing to return answers the empty list, the page asked
///   and hayMas N, without observations;
/// - fechaEmision is the voucher's date; fechaVencimientoPago is the
///   invoice's, or its acceptance deadline when it has none.
/// obtenerCuitsEmisores, which homologación does not implement, and dummy keep
/// the contract's answer.
/// </summary>
public sealed class WsfecredagenteRules(FceLedger ledger, IClock clock) : IServiceBehavior
{
    private static readonly IReadOnlyDictionary<int, string> Texts = FceTexts.Agente;

    private readonly FceLedger _ledger = ledger;

    public string Service => "wsfecredagente";

    public Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        Func<ServiceCall, CancellationToken, Task<XElement>>? operation = call.Name switch
        {
            "obtenerMotivosRechazo" => (c, _) => Task.FromResult(Reasons(c)),
            "altaCuentasAgente" => OpenAsync,
            "bajaCuentasAgente" => CloseAsync,
            "consultarCuentasAgente" => AccountsAsync,
            "consultarFacturasInformadas" => InvoicesAsync,
            "confirmarFacturasInformadas" => ConfirmAsync,
            _ => null,
        };
        return RunAsync(call, _ledger, Texts, operation, Refused, ct);
    }

    /// <summary>The operation's answer with only errors: queries keep their empty list, batches their empty resultados.</summary>
    private static XElement Refused(ServiceCall call, XElement? errors, XElement? formatErrors) => call.Name switch
    {
        "consultarCuentasAgente" => Result(call.Operation.Output, FailedQuery("cuentasAgente", errors, formatErrors)),
        "consultarFacturasInformadas" => Result(call.Operation.Output, FailedQuery("facturasInformadas", errors, formatErrors)),
        "obtenerMotivosRechazo" => Result(call.Operation.Output, errors, formatErrors),
        _ => Result(call.Operation.Output, errors, formatErrors, new XElement("resultados")),
    };

    private static XElement Reasons(ServiceCall call) => Result(call.Operation.Output,
        new XElement("parametros", FceXml.Codes("parametrosTipoCodigosDescripciones",
            FceTables.AgentRejectionReasons.Select(r => ((long)r.Code, r.Description)))));

    private static XElement Account(string name, long holder, string id, string? denomination) => new(name,
        new XElement("cuitTitular", holder),
        new XElement("cuentaId", id),
        denomination is { Length: >= 3 } ? new XElement("denominacion", FceXml.Text250(denomination)) : null);

    // ---- Accounts --------------------------------------------------------------------

    private sealed record AccountItem(long? Holder, string Id, string? Denomination);

    private static List<AccountItem> AccountItems(XElement request) =>
        request.Child("cuentas").Children("cuenta")
            .Select(c => new AccountItem(c.ChildLong("cuitTitular"), c.ChildText("cuentaId") ?? "", c.Value("denominacion")))
            .ToList();

    /// <summary>2009 for too many accounts, 2002 for a holder without its check digit, 2005 for a cuentaId out of 3 to 20 characters.</summary>
    private static List<int> CheckItems(IReadOnlyList<AccountItem> items)
    {
        var errors = new List<int>();
        if (items.Count > BatchSize) errors.Add(2009);
        if (items.Any(i => i.Holder is null || BadCuit(i.Holder))) errors.Add(2002);
        if (items.Any(i => i.Id.Length is < 3 or > 20)) errors.Add(2005);
        return errors;
    }

    private static XElement ItemResult(AccountItem item, params int[] errors) => new("resultado",
        Account("cuentaAgente", item.Holder ?? 0, item.Id, item.Denomination),
        new XElement("resultado", errors.Length == 0 ? "A" : "R"),
        Errors(Texts, errors));

    private async Task<XElement> OpenAsync(ServiceCall call, CancellationToken ct)
    {
        var items = AccountItems(call.Request);
        if (CheckItems(items) is { Count: > 0 } format) return Refused(call, null, FormatErrors(Texts, format));

        var today = clock.Today();
        var results = new List<XElement>();
        foreach (var item in items)
        {
            var account = await _ledger.AgentAccountAsync(call.Cuit, item.Id, ct);
            if (account is { State: "A" })
            {
                results.Add(ItemResult(item, 4012));
                continue;
            }
            await _ledger.SaveAsync(new FceAgentAccount
            {
                Agent = call.Cuit,
                AccountId = item.Id,
                Holder = item.Holder!.Value,
                Denomination = item.Denomination,
                OpenedOn = today,
            }, ct);
            results.Add(ItemResult(item));
        }
        return Result(call.Operation.Output, new XElement("resultados", results));
    }

    private async Task<XElement> CloseAsync(ServiceCall call, CancellationToken ct)
    {
        var items = AccountItems(call.Request);
        if (CheckItems(items) is { Count: > 0 } format) return Refused(call, null, FormatErrors(Texts, format));

        var today = clock.Today();
        var results = new List<XElement>();
        foreach (var item in items)
        {
            var account = await _ledger.AgentAccountAsync(call.Cuit, item.Id, ct);
            if (account is not { State: "A" }) results.Add(ItemResult(item, 4014));
            else if (account.Holder != item.Holder) results.Add(ItemResult(item, 4015));
            else
            {
                account.State = "B";
                account.ClosedOn = today;
                await _ledger.SaveAsync(account, ct);
                results.Add(ItemResult(item with { Denomination = item.Denomination ?? account.Denomination }));
            }
        }
        return Result(call.Operation.Output, new XElement("resultados", results));
    }

    private async Task<XElement> AccountsAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var holder = request.ChildLong("cuitTitular");
        var format = CheckQuery(request, out var page, out var range);
        if (BadCuit(holder)) format.Insert(0, 2002);
        if (format.Count > 0) return Refused(call, null, FormatErrors(Texts, format));

        var state = request.Value("estadoCuenta");
        var all = (await _ledger.AgentAccountsOfAsync(call.Cuit, ct))
            .Where(a => state is null || a.State == state)
            .Where(a => holder is null || a.Holder == holder)
            .Where(a => (range.Kind == "Baja" ? a.ClosedOn : a.OpenedOn) is { } day && day >= range.From && day <= range.To)
            .ToList();
        var (items, more) = FceXml.Page(all, page);
        return Result(call.Operation.Output,
            new XElement("cuentasAgente", items.Select(a => Account("cuenta", a.Holder, a.AccountId, a.Denomination))),
            new XElement("nroPagina", page),
            new XElement("hayMas", FceXml.YesNo(more)));
    }

    // ---- Reported invoices -----------------------------------------------------------

    private static DateTimeOffset? MomentOf(FceAgentReport report, string? kind) => kind switch
    {
        "Disponible" => report.AvailableAt,
        "Consultada" => report.ReadAt,
        "Confirmada" => report.ConfirmedAt,
        _ => null,
    };

    private async Task<XElement> InvoicesAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var issuer = request.ChildLong("cuitEmisor");
        var format = CheckQuery(request, out var page, out var range);
        if (BadCuit(issuer)) format.Insert(0, 2002);
        if (format.Count > 0) return Refused(call, null, FormatErrors(Texts, format));

        var book = await _ledger.OpenAsync(ct);
        var state = request.Value("estadoInforme");
        var all = book.Accounts.Values
            .Where(a => a.Agent is { } report && report.Agent == call.Cuit)
            .Where(a => state is null || a.Agent!.State == state)
            .Where(a => issuer is null || a.Issuer == issuer)
            .Where(a => InRange(MomentOf(a.Agent!, range.Kind), range))
            .OrderBy(a => a.Agent!.AvailableAt).ThenBy(a => a.Code)
            .ToList();
        var (items, more) = FceXml.Page(all, page);

        foreach (var account in items.Where(a => a.Agent!.State == "D"))
        {
            account.Agent!.State = "P";
            account.Agent.ReadAt = book.Now;
            await _ledger.SaveAsync(book, account, ct);
        }

        var rows = new List<XElement>();
        foreach (var account in items)
        {
            var invoice = book.InvoiceOf(account);
            var report = account.Agent!;
            rows.Add(new XElement("facturaInformada",
                IdFactura(account.Invoice),
                new XElement("cuitComprador", account.Receiver),
                new XElement("razonSocialComprador", FceXml.Text250(await _ledger.NameAsync(book, account.Receiver, ct))),
                new XElement("fechaEmision", FceXml.Date(invoice.Date)),
                new XElement("fechaVencimientoPago", FceXml.Date(invoice.PaymentDue ?? account.AcceptanceDue)),
                new XElement("saldoNegociable", FceXml.Money(account.AcceptedBalance ?? book.Balance(account))),
                new XElement("codMoneda", account.Currency),
                Account("cuentaAgente", account.Issuer, report.AccountId, report.Denomination),
                new XElement("estadoInforme", report.State),
                new XElement("fechaHoraDisponible", FceXml.Moment(report.AvailableAt)),
                report.ReadAt is { } read ? new XElement("fechaHoraLecturaAgente", FceXml.Moment(read)) : null,
                report.ConfirmedAt is { } confirmed ? new XElement("fechaHoraConfirmacionAgente", FceXml.Moment(confirmed)) : null));
        }
        return Result(call.Operation.Output,
            new XElement("facturasInformadas", rows),
            new XElement("nroPagina", page),
            new XElement("hayMas", FceXml.YesNo(more)));
    }

    private sealed record Confirmation(FceId? Id, bool Accepts, short? Reason);

    private async Task<XElement> ConfirmAsync(ServiceCall call, CancellationToken ct)
    {
        var items = call.Request.Child("facturas").Children("factura")
            .Select(f => new Confirmation(IdOf(f.Child("idFactura")), f.Value("aceptada") == "S", (short?)f.ChildLong("codRechazo")))
            .ToList();
        var format = new List<int>();
        if (items.Count > BatchSize) format.Add(2009);
        if (items.Any(i => i.Id is null || BadCuit(i.Id.Cuit))) format.Add(2002);
        if (items.Any(i => i.Accepts ? i.Reason is not null : FceTables.AgentRejectionReasons.All(r => r.Code != i.Reason))) format.Add(2006);
        if (format.Count > 0) return Refused(call, null, FormatErrors(Texts, format));

        var book = await _ledger.OpenAsync(ct);
        var results = new List<XElement>();
        foreach (var item in items)
        {
            XElement Item(params int[] errors) => new("resultado",
                IdFactura(item.Id!),
                new XElement("resultado", errors.Length == 0 ? "A" : "R"),
                Errors(Texts, errors));

            var invoice = book.Find(item.Id!);
            var account = invoice is { IsInvoice: true } ? book.Accounts[invoice.Account] : null;
            var report = account?.Agent;
            int? refusal = report is null || report.Agent != call.Cuit || report.State == "R" ? 4019
                : report.State == "D" ? 4021
                : report.State == "A" ? 4020
                : null;
            if (refusal is { } code)
            {
                results.Add(Item(code));
                continue;
            }

            report!.ConfirmedAt = book.Now;
            if (item.Accepts) report.State = "A";
            else
            {
                report.State = "R";
                report.RejectionCode = item.Reason;
                report.RejectionReason = FceTables.AgentRejectionReasons.First(r => r.Code == item.Reason).Description;
                account!.MoveTo(FceStates.AccountAccepted, book.Now);
                invoice!.MoveTo(FceStates.Accepted, book.Now);
            }
            await _ledger.SaveAsync(book, account!, ct);
            results.Add(Item());
        }
        return Result(call.Operation.Output, new XElement("resultados", results));
    }
}
