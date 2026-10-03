using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Fce.FceXml;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// wsfecred, the register of Facturas de Crédito Electrónica MiPyMEs
/// (docs/arca/servicios/wsfecred.md): the current account each FCE invoice
/// opens, which the buyer accepts (with notes, withholdings, adjustments and
/// a partial or total cancellation), rejects with reasons, or cancels in full
/// afterwards; the notes the buyer rejects one by one; the seller's change of
/// transfer option and report to an Agente de Depósito Colectivo; the queries
/// by role with pagination, the state histories, the remitos, and whether a
/// CUIT must receive FCE. Business errors go in arrayErrores with the manual's
/// codes and texts, resultado R.
///
/// Where the manual is silent (NO VERIFICADO), these are ArcaSim's choices:
/// - deadlines, tacit acceptance and the default option: see FceLedger;
/// - permissions: the buyer's operations answer 1101 to the seller and 1100 to
///   anyone else, the seller's the other way round; a voucher of others is 1105;
/// - aceptarFECred checks state first (1106, then 1107, then 1108) and then
///   reports every business error together. Where codes overlap it uses the
///   12xxx series (12000, 12002, 12003, 12004, 12006, 12009, 12012) and keeps
///   2001, 2002 and 2003 (repeated jurisdiction) for what only they describe;
/// - saldoAceptado must be saldo + adjustments − partial cancellation −
///   withholdings − embargo (pesos converted at cotizacionMonedaUlt), or 0
///   with a total cancellation; each withholding must be its percentage of
///   saldo in pesos; both within 0.01;
/// - 2009 accepts a rate between 2 % and 400 % of WSFEv1's rate for the
///   currency, and is skipped when ArcaSim has no rate loaded;
/// - 2015: a CBU whose BCRA check digits fail; ArcaSim has no bank registry,
///   so 2018 and the observation 9000 never happen and a valid CBU counts as
///   validated;
/// - informarFacturaAgtDptoCltv answers 6007, 6001 or 6000 while the agent has
///   the report in D, P or A, and lets the seller report again after the
///   agent rejected it; 6004 to 6006 are not simulated;
/// - informarCancelacionTotalFECred cancels an accepted ADC account with an
///   amount of at least saldoAceptado (4000 otherwise);
/// - a page holds 100 items; without nroPagina, page 1; no results is the
///   observation 32767 with nroPagina and hayMas N;
/// - historiales answer in chronological order; on error the schema still
///   asks for one estadoHistorico, so it carries the error's instant;
/// - consultarMontoObligadoRecepcion: a registered, active Responsable
///   Inscripto must receive FCE from FceTables.MinimumAmount on; anyone else
///   need not, without montoDesde.
/// The deprecated consultarObligadoRecepcion and dummy keep the contract's answer.
/// </summary>
public sealed class WsfecredRules(
    IDocumentStore store, IVoucherStore vouchers, ITaxpayerRepository taxpayers, IClock clock, IExchangeRates rates) : IServiceBehavior
{
    public const int PageSize = 100;

    private readonly FceLedger _ledger = new(store, vouchers, taxpayers, clock);

    public string Service => "wsfecred";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        switch (call.Name)
        {
            case "consultarTiposRetenciones":
                return call.Ok(Answer(call, "consultarTiposRetencionesReturn", new XElement("arrayTiposRetenciones",
                    FceTables.Withholdings.Select(w => new XElement("tipoRetencion",
                        new XElement("codigoJurisdiccion", w.Code),
                        new XElement("descripcionJurisdiccion", w.Jurisdiction),
                        new XElement("porcentajeRetencion", Money(w.Rate)))))));
            case "consultarTiposMotivosRechazo":
                return call.Ok(Table(call, FceTables.RejectionReasons.Select(r => ((long)r.Code, r.Description))));
            case "consultarTiposFormasCancelacion":
                return call.Ok(Table(call, FceTables.CancellationForms.Select(f => ((long)f.Code, f.Description))));
            case "consultarTiposAjustesOperacion":
                return call.Ok(Table(call, FceTables.Adjustments.Select(a => ((long)a.Code, a.Description))));
            case "consultarMontoObligadoRecepcion":
                return call.Ok(await ObligationAsync(call, ct));
        }

        Func<ServiceCall, FceBook, CancellationToken, Task<XElement>>? operation = call.Name switch
        {
            "consultarComprobantes" => (c, b, _) => Task.FromResult(Vouchers(c, b)),
            "consultarCtasCtes" => (c, b, _) => Task.FromResult(Accounts(c, b)),
            "consultarCtaCte" => (c, b, _) => Task.FromResult(Account(c, b)),
            "consultarHistorialEstadosCtaCte" => (c, b, _) => Task.FromResult(AccountHistory(c, b)),
            "consultarHistorialEstadosComprobante" => (c, b, _) => Task.FromResult(VoucherHistory(c, b)),
            "obtenerRemitos" => (c, b, _) => Task.FromResult(DeliveryNotes(c, b)),
            "aceptarFECred" => AcceptAsync,
            "rechazarFECred" => RejectAsync,
            "rechazarNotaDC" => RejectNoteAsync,
            "informarCancelacionTotalFECred" => CancelAsync,
            "modificarOpcionTransferencia" => ChangeOptionAsync,
            "informarFacturaAgtDptoCltv" => ReportToAgentAsync,
            "consultarCuentasEnAgtDptoCltv" => AgentAccountsAsync,
            "consultarFacturasAgtDptoCltv" => (c, b, _) => Task.FromResult(Reported(c, b)),
            _ => null,
        };
        if (operation is null) return null;

        using var _ = await _ledger.LockAsync(ct);
        var book = await _ledger.OpenAsync(ct);
        return call.Ok(await operation(call, book, ct));
    }

    // ---- Answers ---------------------------------------------------------------------

    private static XElement Answer(ServiceCall call, string result, params object?[] content) =>
        new(call.Operation.Output, new XElement(result, content));

    private static XElement Table(ServiceCall call, IEnumerable<(long Code, string Text)> rows) =>
        Answer(call, "codigoDescripcionReturn", Codes("arrayCodigoDescripcion", rows));

    private static IEnumerable<(long, string)> Texts(IEnumerable<int> codes) => codes.Select(c => ((long)c, FceTexts.Fecred[c]));

    private static XElement? Errors(params int[] codes) => Codes("arrayErrores", Texts(codes));

    private static XElement? Errors(IEnumerable<int> codes) => Codes("arrayErrores", Texts(codes));

    /// <summary>OperacionFECredReturnType: resultado, the idCtaCte the request sent, and the errors when it failed.</summary>
    private static XElement Operation(ServiceCall call, AccountRef reference, IReadOnlyCollection<int> errors) =>
        Answer(call, "operacionFECredReturn",
            new XElement("resultado", errors.Count == 0 ? "A" : "R"),
            Echo(reference),
            Errors(errors));

    private static XElement Operation(ServiceCall call, AccountRef reference, int error) => Operation(call, reference, [error]);

    private static XElement Echo(AccountRef reference, string name = "idCtaCte") =>
        reference.Invoice is { } invoice
            ? new XElement(name, IdComprobante("idFactura", invoice))
            : new XElement(name, new XElement("codCtaCte", reference.Code ?? 0));

    public static XElement IdComprobante(string name, FceId id) => new(name,
        new XElement("CUITEmisor", id.Cuit),
        new XElement("codTipoCmp", id.Type),
        new XElement("ptoVta", id.PointOfSale),
        new XElement("nroCmp", id.Number));

    private static FceId? IdOf(XElement? element) =>
        element is not null && element.LongOf("CUITEmisor") is { } cuit && element.LongOf("codTipoCmp") is { } type
        && element.LongOf("ptoVta") is { } point && element.LongOf("nroCmp") is { } number
            ? new FceId(cuit, (int)type, (int)point, number)
            : null;

    private static AccountRef ReferenceOf(XElement request)
    {
        var id = request.Child("idCtaCte");
        return new AccountRef(id?.LongOf("codCtaCte"), IdOf(id?.Child("idFactura")));
    }

    private static FceAccount? Find(FceBook book, AccountRef reference) =>
        reference.Code is { } code ? book.Accounts.GetValueOrDefault(code)
        : reference.Invoice is { } invoice && book.Find(invoice) is { IsInvoice: true } voucher ? book.Accounts[voucher.Account]
        : null;

    /// <summary>1102, or who may act: the buyer's operations refuse the seller with 1101 and anyone else with 1100, and the other way round.</summary>
    private static int? Refusal(FceAccount? account, long caller, bool buyer)
    {
        if (account is null) return 1102;
        var (own, other) = buyer ? (account.Receiver, account.Issuer) : (account.Issuer, account.Receiver);
        return caller == own ? null : caller == other ? 1101 : 1100;
    }

    private static bool IsParty(FceAccount account, long caller) => caller == account.Issuer || caller == account.Receiver;

    // ---- Views -----------------------------------------------------------------------

    private static XElement State(string name, FceState state) =>
        new(name, new XElement("estado", state.State), new XElement("fechaHoraEstado", Moment(state.Since)));

    private static XElement Reasons(IEnumerable<FceReason> reasons) => new("arrayMotivosRechazo", reasons.Select(r =>
        new XElement("motivoRechazo",
            new XElement("codMotivo", r.Code),
            new XElement("descMotivo", Text250(r.Description)),
            new XElement("justificacion", Text250(r.Justification)))));

    /// <summary>ComprobanteType: the voucher as WSFEv1 authorized it, its account, its state and, for an invoice, its transfer.</summary>
    private static XElement Voucher(FceBook book, FceVoucher v, string name = "comprobante")
    {
        var account = book.Accounts[v.Account];
        return new XElement(name,
            new XElement("cuitEmisor", v.Id.Cuit),
            new XElement("razonSocialEmi", book.Name(v.Id.Cuit)),
            new XElement("codTipoCmp", v.Id.Type),
            new XElement("ptovta", v.Id.PointOfSale),
            new XElement("nroCmp", v.Id.Number),
            new XElement("cuitReceptor", v.Receiver),
            new XElement("razonSocialRecep", book.Name(v.Receiver)),
            new XElement("tipoCodAuto", v.AuthorizationKind),
            new XElement("codAutorizacion", v.AuthorizationCode),
            new XElement("fechaEmision", Date(v.Date)),
            new XElement("fechaPuestaDispo", Date(v.AvailableOn)),
            v.PaymentDue is { } due ? new XElement("fechaVenPago", Date(due)) : null,
            v.CountsInBalance ? new XElement("fechaVenAcep", Date(account.AcceptanceDue)) : null,
            new XElement("importeTotal", Money(v.Total)),
            new XElement("codMoneda", v.Currency),
            new XElement("cotizacionMoneda", Rate(v.Rate)),
            v.Cbu is { } cbu ? new XElement("CBUEmisor", cbu) : null,
            v.Alias is { } alias ? new XElement("AliasEmisor", alias) : null,
            v.IsInvoice ? null : new XElement("esAnulacion", YesNo(v.Cancels)),
            v.IsInvoice ? null : new XElement("esPostAceptacion", YesNo(v.PostAcceptance)),
            v.Associated is { } associated ? IdComprobante("idComprobanteAsociado", associated) : null,
            v.References.Count == 0 ? null : new XElement("referenciasComerciales", v.References.Select(r => new XElement("texto", r))),
            v.Vat.Count == 0 ? null : new XElement("arraySubtotalesIVA", v.Vat.Select(a => new XElement("subtotalIVA",
                new XElement("codigo", a.Code), new XElement("baseImponible", Money(a.Base)), new XElement("importe", Money(a.Amount))))),
            v.Taxes.Count == 0 ? null : new XElement("arrayOtrosTributos", v.Taxes.Select(t => new XElement("otroTributo",
                new XElement("codigo", t.Code),
                t.Detail is null ? null : new XElement("detalle", t.Detail),
                new XElement("baseImponible", Money(t.Base)),
                new XElement("importe", Money(t.Amount))))),
            new XElement("codCtaCte", v.Account),
            State("estado", v.State),
            v.AcceptanceKind is { } kind ? new XElement("tipoAcep", kind) : null,
            v.DecidedAt is { } decided ? new XElement("fechaHoraAcep", Moment(decided)) : null,
            v.Rejection.Count == 0 ? null : Reasons(v.Rejection),
            v.IsInvoice ? new XElement("opcionTransferencia", account.Option) : null,
            v.IsInvoice ? Transfer(book, account) : null);
    }

    private static XElement? Transfer(FceBook book, FceAccount account)
    {
        if (account.Agent is { } report)
            return new XElement("infoTransferencia", new XElement("infoAgtDptoCltv",
                new XElement("fechaInfo", Date(report.AvailableAt)),
                AgentAccount("ctaAgente", book.Name(report.Agent), report.Agent, report.AccountId, report.Denomination),
                new XElement("recibida", YesNo(report.ConfirmedAt is not null)),
                report.ReadAt is { } read ? new XElement("fechaLectura", Date(read)) : null,
                report.ConfirmedAt is { } confirmed ? new XElement("fechaRecep", Date(confirmed)) : null,
                report.State is "A" or "R" ? new XElement("aceptada", YesNo(report.State == "A")) : null,
                report.RejectionReason is { } reason ? new XElement("motivoRechazo", reason) : null));
        if (account.Sca is { } sca)
            return new XElement("infoTransferencia", new XElement("infoSCA",
                new XElement("fechaAceptacionFactura", Date(sca.AvailableAt)),
                new XElement("informaCBUReceptor", YesNo(sca.InformsCbu)),
                sca.Cbu is { } cbu ? new XElement("CBUReceptor", cbu) : null,
                sca.CbuValidated is { } validated ? new XElement("CBUValidada", YesNo(validated)) : null,
                sca.ReadAt is { } read ? new XElement("fechaLecturaSCA", Moment(read)) : null));
        return null;
    }

    private static XElement AgentAccount(string name, string agentName, long agent, string accountId, string? denomination) => new(name,
        new XElement("cuitAgente", agent),
        agentName.Length > 0 ? new XElement("razonSocialAgente", agentName) : null,
        new XElement("idCuenta", accountId),
        denomination is { Length: >= 3 } ? new XElement("denominacion", Text250(denomination)) : null);

    /// <summary>CuentaCorrienteType: the invoice, its notes, what the acceptance informed and the balances.</summary>
    private static XElement AccountView(FceBook book, FceAccount account)
    {
        var notes = book.NotesOf(account).ToList();
        return new XElement("ctaCte",
            new XElement("codCtaCte", account.Code),
            State("estadoCtaCte", account.State),
            Voucher(book, book.InvoiceOf(account), "factura"),
            notes.Count == 0 ? null : new XElement("arrayNotasDCAsociadas", notes.Select(n => Voucher(book, n))),
            Codes("arrayFormasCancelacion", account.Forms.Select(f => ((long)f.Code, f.Description))),
            account.Withholdings.Count == 0 ? null : new XElement("arrayRetenciones", account.Withholdings.Select(w => new XElement("retencion",
                new XElement("codTipo", w.Code),
                new XElement("importe", Money(w.Amount)),
                new XElement("porcentaje", Money(w.Rate)),
                w.Reason is { Length: >= 3 } reason ? new XElement("descMotivo", Text250(reason)) : null))),
            account.Adjustments.Count == 0 ? null : new XElement("arrayAjustesOperacion", account.Adjustments.Select(a => new XElement("ajuste",
                new XElement("codigo", a.Code), new XElement("importe", Money(a.Amount))))),
            new XElement("importeInicial", Money(account.Initial)),
            notes.Count == 0 ? null : new XElement("importeTotalNotasDC", Money(book.NotesTotal(account))),
            account.Cancelled is { } cancelled ? new XElement("importeCancelado", Money(cancelled)) : null,
            account.WithholdingsTotal is { } withheld ? new XElement("importeTotalRetPesos", Money(withheld)) : null,
            account.Embargo is { } embargo ? new XElement("importeEmbargoPesos", Money(embargo)) : null,
            account.AcceptedBalance is { } accepted ? new XElement("saldoAceptado", Money(accepted)) : null,
            new XElement("saldo", Money(book.Balance(account))),
            new XElement("codMoneda", account.Currency),
            new XElement("cotizacionMonedaUlt", Rate(account.LastRate)));
    }

    /// <summary>InfoCtaCteType, the summary consultarCtasCtes lists.</summary>
    private static XElement AccountInfo(FceBook book, FceAccount account) => new("infoCtaCte",
        new XElement("codCtaCte", account.Code),
        State("estadoCtaCte", account.State),
        IdComprobante("idFacturaCredito", account.Invoice),
        new XElement("importeTotalFC", Money(account.Initial)),
        new XElement("saldo", Money(book.Balance(account))),
        account.AcceptedBalance is { } accepted ? new XElement("saldoAceptado", Money(accepted)) : null,
        new XElement("codMoneda", account.Currency),
        new XElement("opcionTransferencia", account.Option));

    // ---- Queries ---------------------------------------------------------------------

    private static (IReadOnlyList<T> Items, bool More) Page<T>(IReadOnlyList<T> all, int page) =>
        (all.Skip((page - 1) * PageSize).Take(PageSize).ToList(), all.Count > page * PageSize);

    private static XElement NoResults(string block) => Codes(block, [(32767L, FceTexts.Fecred[32767])])!;

    /// <summary>The day a FiltroFechaType's tipo looks at, for a voucher.</summary>
    private static DateOnly? DayOf(FceBook book, FceVoucher voucher, string? kind)
    {
        var account = book.Accounts[voucher.Account];
        return kind switch
        {
            "Emision" => voucher.Date,
            "PuestaDispo" => voucher.AvailableOn,
            "VenPago" => voucher.PaymentDue,
            "VenAcep" => voucher.CountsInBalance ? account.AcceptanceDue : null,
            "Acep" => voucher.AcceptanceKind is not null && voucher.DecidedAt is { } at ? DateOnly.FromDateTime(at.ToArgentina().DateTime) : null,
            "InfoAgDptoCltv" => account.Agent is { } report ? DateOnly.FromDateTime(report.AvailableAt.ToArgentina().DateTime) : null,
            _ => null,
        };
    }

    private static Func<FceVoucher, bool> DateFilter(FceBook book, XElement? filter)
    {
        if (filter is null) return _ => true;
        var kind = filter.Value("tipo");
        var from = filter.DateOf("desde");
        var to = filter.DateOf("hasta");
        return v => DayOf(book, v, kind) is { } day && (from is null || day >= from) && (to is null || day <= to);
    }

    private static XElement Vouchers(ServiceCall call, FceBook book)
    {
        var request = call.Request;
        var page = (int)(request.LongOf("nroPagina") ?? 1);
        if (page <= 0) return Answer(call, "consultarCmpReturn", Errors(12011));

        var issuer = request.Value("rolCUITRepresentada") == "Emisor";
        var counterpart = request.LongOf("CUITContraparte");
        var type = request.LongOf("codTipoCmp");
        var state = request.Value("estadoCmp");
        var code = request.LongOf("codCtaCte");
        var accountState = request.Value("estadoCtaCte");
        var inRange = DateFilter(book, request.Child("fecha"));

        var all = book.Vouchers.Values
            .Where(v => issuer ? v.Id.Cuit == call.Cuit : v.Receiver == call.Cuit)
            .Where(v => counterpart is null || (issuer ? v.Receiver : v.Id.Cuit) == counterpart)
            .Where(v => type is null || v.Id.Type == type)
            .Where(v => state is null || v.State.State == state)
            .Where(v => code is null || v.Account == code)
            .Where(v => accountState is null || book.Accounts[v.Account].State.State == accountState)
            .Where(inRange)
            .OrderBy(v => v.Account).ThenBy(v => v.IsInvoice ? 0 : 1).ThenBy(v => v.Id.Key, StringComparer.Ordinal)
            .ToList();
        var (items, more) = Page(all, page);
        return Answer(call, "consultarCmpReturn",
            items.Count == 0 ? null : new XElement("arrayComprobantes", items.Select(v => Voucher(book, v))),
            new XElement("nroPagina", page),
            new XElement("hayMas", YesNo(more)),
            items.Count == 0 ? NoResults("arrayObservaciones") : null);
    }

    private static XElement Accounts(ServiceCall call, FceBook book)
    {
        var request = call.Request;
        var page = (int)(request.LongOf("nroPagina") ?? 1);
        if (page <= 0) return Answer(call, "consultarCtasCtesReturn", Errors(12011));

        var issuer = request.Value("rolCUITRepresentada") == "Emisor";
        var counterpart = request.LongOf("CUITContraparte");
        var state = request.Value("estadoCtaCte");
        var option = request.Value("opcionTransferencia");
        var inRange = DateFilter(book, request.Child("fecha"));

        var all = book.Accounts.Values
            .Where(a => issuer ? a.Issuer == call.Cuit : a.Receiver == call.Cuit)
            .Where(a => counterpart is null || (issuer ? a.Receiver : a.Issuer) == counterpart)
            .Where(a => state is null || a.State.State == state)
            .Where(a => option is null || a.Option == option)
            .Where(a => inRange(book.InvoiceOf(a)))
            .OrderBy(a => a.Code)
            .ToList();
        var (items, more) = Page(all, page);
        return Answer(call, "consultarCtasCtesReturn",
            items.Count == 0 ? null : new XElement("arrayInfosCtaCte", items.Select(a => AccountInfo(book, a))),
            new XElement("nroPagina", page),
            new XElement("hayMas", YesNo(more)),
            items.Count == 0 ? NoResults("arrayObservaciones") : null);
    }

    private static XElement Account(ServiceCall call, FceBook book)
    {
        var account = Find(book, ReferenceOf(call.Request));
        if (account is null) return Answer(call, "consultarCtaCteReturn", Errors(1102));
        if (!IsParty(account, call.Cuit)) return Answer(call, "consultarCtaCteReturn", Errors(1100));
        return Answer(call, "consultarCtaCteReturn", AccountView(book, account));
    }

    private static XElement AccountHistory(ServiceCall call, FceBook book)
    {
        var reference = ReferenceOf(call.Request);
        var account = Find(book, reference);
        int? error = account is null ? 1102 : IsParty(account, call.Cuit) ? null : 1100;
        var history = error is null ? account!.History : [new FceState(FceStates.Modifiable, book.Now)];
        return Answer(call, "consultarHistorialEstadosCtaCteReturn",
            Echo(reference),
            new XElement("arrayHistorialEstados", history.Select(h => State("estadoHistorico", h))),
            error is { } code ? Errors(code) : null);
    }

    /// <summary>A voucher the caller issued or received; anyone else's is "No existe el comprobante indicado".</summary>
    private static FceVoucher? VoucherFor(ServiceCall call, FceBook book, FceId? id) =>
        id is not null && book.Find(id) is { } voucher && (voucher.Id.Cuit == call.Cuit || voucher.Receiver == call.Cuit) ? voucher : null;

    private static XElement VoucherHistory(ServiceCall call, FceBook book)
    {
        var id = IdOf(call.Request.Child("idComprobante"));
        var voucher = VoucherFor(call, book, id);
        var history = voucher?.History ?? [new FceState(FceStates.PendingReception, book.Now)];
        return Answer(call, "consultarHistorialEstadosComprobanteReturn",
            IdComprobante("idComprobante", id ?? new FceId(call.Cuit, 201, 1, 1)),
            new XElement("arrayHistorialEstados", history.Select(h => State("estadoHistorico", h))),
            voucher is null ? Errors(1105) : null);
    }

    private static XElement DeliveryNotes(ServiceCall call, FceBook book)
    {
        var voucher = VoucherFor(call, book, IdOf(call.Request.Child("idComprobante")));
        if (voucher is null) return Answer(call, "obtenerRemitosReturn", Errors(1105));
        return Answer(call, "obtenerRemitosReturn", voucher.DeliveryNotes.Count == 0
            ? null
            : new XElement("arrayIdsRemitos", voucher.DeliveryNotes.Select(r => IdComprobante("idsComprobantes", r))));
    }

    private async Task<XElement> AgentAccountsAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var accounts = (await _ledger.AgentAccountsAsync(ct)).Where(a => a.Holder == call.Cuit && a.State == "A").ToList();
        var rows = new List<XElement>();
        foreach (var account in accounts)
            rows.Add(AgentAccount("cuentaEnAgente", await _ledger.NameAsync(book, account.Agent, ct), account.Agent, account.AccountId, account.Denomination));
        return Answer(call, "consultarCuentasEnAgtDptoCltvReturn",
            rows.Count == 0 ? null : new XElement("arrayCuentasEnAgente", rows),
            rows.Count == 0 ? NoResults("arrayObservacion") : null);
    }

    private static XElement Reported(ServiceCall call, FceBook book)
    {
        var request = call.Request;
        IEnumerable<FceAccount> accounts = book.Accounts.Values;
        if (request.Child("idCtaCte") is not null)
        {
            var account = Find(book, ReferenceOf(request));
            if (account is null) return Answer(call, "consultarFacturasAgtDptoCltvReturn", Errors(1102));
            if (!IsParty(account, call.Cuit)) return Answer(call, "consultarFacturasAgtDptoCltvReturn", Errors(1100));
            accounts = [account];
        }
        var inRange = DateFilter(book, request.Child("filtroFecha"));
        var reported = accounts
            .Where(a => a.Agent is not null && IsParty(a, call.Cuit) && inRange(book.InvoiceOf(a)))
            .OrderBy(a => a.Code)
            .ToList();
        return Answer(call, "consultarFacturasAgtDptoCltvReturn",
            reported.Count == 0 ? null : new XElement("arrayFacturasAgtDptoCltv", reported.Select(a => new XElement("facturaInformada",
                IdComprobante("idFactura", a.Invoice),
                Transfer(book, a)!.Elements().Single()))),
            reported.Count == 0 ? NoResults("arrayObservaciones") : null);
    }

    private async Task<XElement> ObligationAsync(ServiceCall call, CancellationToken ct)
    {
        var taxpayer = await taxpayers.FindAsync(call.Request.LongOf("cuitConsultada") ?? 0, ct);
        var obliged = taxpayer is { Active: true, VatCondition: VatCondition.ResponsableInscripto };
        return Answer(call, "consultarMontoObligadoRecepcionReturn",
            new XElement("obligado", YesNo(obliged)),
            obliged ? new XElement("montoDesde", Money(FceTables.MinimumAmount)) : null);
    }

    // ---- The buyer -------------------------------------------------------------------

    /// <summary>The checks before any of the buyer's decisions: who, 1106, 1107 and 1108.</summary>
    private static int? BuyerRefusal(FceBook book, FceAccount? account, long caller, params string[] states)
    {
        if (Refusal(account, caller, buyer: true) is { } refusal) return refusal;
        if (book.Now < FceLedger.OperableFrom(book.InvoiceOf(account!))) return 1106;
        if (book.Today > account!.AcceptanceDue) return 1107;
        if (!states.Contains(account.State.State)) return 1108;
        return null;
    }

    private static List<FceReason> ReasonsOf(XElement request) =>
        request.Child("arrayMotivosRechazo").Children("motivoRechazo")
            .Select(m => new FceReason((short)(m.LongOf("codMotivo") ?? 0), m.Value("descMotivo") ?? "", m.Value("justificacion") ?? ""))
            .ToList();

    /// <summary>3001 for a code not in the table or repeated, 3000 for a reason without justification.</summary>
    private static List<int> CheckReasons(IReadOnlyList<FceReason> reasons)
    {
        var errors = new List<int>();
        if (reasons.Count == 0 || reasons.Any(r => FceTables.RejectionReasons.All(t => t.Code != r.Code)) || reasons.DistinctBy(r => r.Code).Count() != reasons.Count)
            errors.Add(3001);
        if (reasons.Any(r => string.IsNullOrWhiteSpace(r.Justification))) errors.Add(3000);
        return errors;
    }

    private async Task<XElement> RejectAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var reference = ReferenceOf(call.Request);
        var account = Find(book, reference);
        if (BuyerRefusal(book, account, call.Cuit, FceStates.Modifiable) is { } refusal) return Operation(call, reference, refusal);
        var reasons = ReasonsOf(call.Request);
        var errors = CheckReasons(reasons);
        if (errors.Count > 0) return Operation(call, reference, errors);

        var now = book.Now;
        account!.MoveTo(FceStates.AccountRejected, now);
        var invoice = book.InvoiceOf(account);
        invoice.Rejection = reasons;
        foreach (var voucher in new[] { invoice }.Concat(book.NotesOf(account)).Where(v => v.CountsInBalance && v.State.State != FceStates.Rejected))
        {
            voucher.MoveTo(FceStates.Rejected, now);
            voucher.DecidedAt = now;
        }
        await _ledger.SaveAsync(book, account, ct);
        return Operation(call, reference, []);
    }

    private async Task<XElement> RejectNoteAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var id = IdOf(call.Request.Child("idComprobante"));
        XElement Result(params int[] errors) => Answer(call, "rechazarNotaDCReturn",
            IdComprobante("idComprobante", id ?? new FceId(call.Cuit, 202, 1, 1)),
            new XElement("resultado", errors.Length == 0 ? "A" : "R"),
            Errors(errors));

        if (id is null || book.Find(id) is not { } note) return Result(1105);
        var account = book.Accounts[note.Account];
        if (Refusal(account, call.Cuit, buyer: true) is { } refusal) return Result(refusal);
        if (note.IsInvoice || !note.CountsInBalance || note.State.State == FceStates.Rejected || account.State.State != FceStates.Modifiable)
            return Result(15000);
        if (book.Now < FceLedger.OperableFrom(note)) return Result(1106);
        if (book.Today > account.AcceptanceDue) return Result(1107);
        var reasons = ReasonsOf(call.Request);
        if (CheckReasons(reasons) is { Count: > 0 } errors) return Result([.. errors]);
        if (book.Balance(account) - (note.IsDebit ? note.Total : -note.Total) <= 0) return Result(5000);

        note.MoveTo(FceStates.Rejected, book.Now);
        note.DecidedAt = book.Now;
        note.Rejection = reasons;
        await _ledger.SaveAsync(book, account, ct);
        return Result();
    }

    private sealed record Confirmation(bool Accepts, FceId? Id);

    private async Task<XElement> AcceptAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var request = call.Request;
        var reference = ReferenceOf(request);
        var account = Find(book, reference);
        if (BuyerRefusal(book, account, call.Cuit, FceStates.Modifiable) is { } refusal) return Operation(call, reference, refusal);

        var confirmations = request.Child("arrayConfirmarNotasDC").Children("confirmarNota")
            .Select(n => new Confirmation(n.Value("acepta") == "S", IdOf(n.Child("idNota")))).ToList();
        var forms = request.Child("arrayFormasCancelacion").Children("codigoDescripcion")
            .Select(f => new FceCodeText((short)(f.LongOf("codigo") ?? 0), f.Value("descripcion") ?? "")).ToList();
        var withholdings = request.Child("arrayRetenciones").Children("retencion")
            .Select(r => new FceWithholding((short)(r.LongOf("codTipo") ?? 0), r.DecimalOf("importe") ?? 0, r.DecimalOf("porcentaje") ?? 0, r.Value("descMotivo"))).ToList();
        var adjustments = request.Child("arrayAjustesOperacion").Children("ajuste")
            .Select(a => new FceAdjustment((short)(a.LongOf("codigo") ?? 0), a.DecimalOf("importe") ?? 0)).ToList();
        var cancellation = request.Value("tipoCancelacion");
        var cancelled = request.DecimalOf("importeCancelado");
        var withheld = request.DecimalOf("importeTotalRetPesos");
        var embargo = request.DecimalOf("importeEmbargoPesos");
        var accepted = request.DecimalOf("saldoAceptado") ?? 0;
        var currency = request.Value("codMoneda");
        var rate = request.DecimalOf("cotizacionMonedaUlt") ?? 0;
        var informsCbu = request.Value("informaCBU");
        var cbu = request.Value("CBUComprador");

        var errors = new List<int>();
        void Add(int code)
        {
            if (!errors.Contains(code)) errors.Add(code);
        }

        // Notes: every note that moves the balance, confirmed; the rejected ones only as rejected.
        var notes = book.NotesOf(account!).Where(n => n.CountsInBalance).ToList();
        foreach (var confirmation in confirmations)
        {
            var note = notes.FirstOrDefault(n => n.Id == confirmation.Id);
            if (note is null) Add(12004);
            else if (note.State.State == FceStates.Rejected) { if (confirmation.Accepts) Add(2002); }
            else if (!confirmation.Accepts) Add(2001);
        }
        if (notes.Any(n => n.State.State != FceStates.Rejected && confirmations.All(c => c.Id != n.Id))) Add(12003);

        // Currency and rate.
        var pesos = account!.Currency == "PES";
        if (currency != account.Currency) Add(12001);
        if (rate <= 0) Add(2006);
        else if (pesos && rate != 1) Add(12000);
        else if (!pesos && await rates.RateAsync(account.Currency, book.Today, ct) is { } official
                 && (rate < official.Rate * 0.02m || rate > official.Rate * 4m)) Add(2009);

        if (new[] { cancelled, withheld, embargo }.Any(a => a < 0)) Add(2010);

        // Adjustments, only in a foreign currency.
        if (adjustments.Count > 0)
        {
            if (pesos) Add(12010);
            if (adjustments.Any(a => FceTables.Adjustments.All(t => t.Code != a.Code)) || adjustments.DistinctBy(a => a.Code).Count() != adjustments.Count) Add(2013);
            if (adjustments.Any(a => a.Code == FceTables.ExchangeRateAdjustment) && rate == book.InvoiceOf(account).Rate) Add(2012);
        }

        // Withholdings, in pesos, each its percentage of the balance.
        var balance = book.Balance(account);
        var toPesos = pesos || rate <= 0 ? 1 : rate;
        if (withholdings.Count > 0 || withheld is not null)
        {
            if (withholdings.Count == 0 || withheld is not > 0) Add(12007);
            foreach (var withholding in withholdings)
            {
                if (FceTables.Withholdings.FirstOrDefault(t => t.Code == withholding.Code) is not { } type) { Add(12009); continue; }
                if (withholding.Rate != type.Rate && string.IsNullOrWhiteSpace(withholding.Reason)) Add(12006);
                if (!Amounts.WithinMargin(Math.Round(balance * toPesos * withholding.Rate / 100, 2), withholding.Amount, 1)) Add(2005);
            }
            if (withholdings.DistinctBy(w => w.Code).Count() != withholdings.Count) Add(2003);
            if (withholdings.Count > 0 && withheld is { } total && !Amounts.WithinMargin(withholdings.Sum(w => w.Amount), total, withholdings.Count)) Add(12005);
        }

        // Cancellation: forms, type and amount together.
        var cancelsAll = cancellation == "TOT";
        if (forms.Count > 0 || cancellation is not null || cancelled is not null)
        {
            if (forms.Count == 0 || cancellation is null || cancelled is not > 0) Add(12008);
            if (forms.Any(f => FceTables.CancellationForms.All(t => t.Code != f.Code)) || forms.DistinctBy(f => f.Code).Count() != forms.Count) Add(4002);
            if (forms.Any(f => FceTables.CancellationForms.Any(t => t.Code == f.Code && t.TotalOnly)) && (!cancelsAll || forms.Count > 1)) Add(4003);
            if (cancelsAll && cancelled is { } amount && Math.Abs(amount - balance) > 0.01m) Add(4000);
        }

        // The buyer's CBU, only for the SCA.
        var sca = account.Option == "SCA";
        if (sca && informsCbu is null) Add(12014);
        if (!sca && informsCbu is not null) Add(12015);
        if (cbu is not null && !sca) Add(2014);
        if (informsCbu is not null && (informsCbu == "S") != (cbu is not null)) Add(12013);
        if (cbu is not null && cancelsAll) Add(12012);
        if (cbu is not null && account.Currency is not ("PES" or "DOL")) Add(2017);
        if (cbu is not null && !IsValidCbu(cbu)) Add(2015);

        var expected = cancelsAll
            ? 0
            : balance + adjustments.Sum(a => a.Amount) - (cancellation == "PAR" ? cancelled ?? 0 : 0) - ((withheld ?? 0) + (embargo ?? 0)) / toPesos;
        if (Math.Abs(Math.Round(expected, 2) - accepted) > 0.01m) Add(12002);

        if (errors.Count > 0) return Operation(call, reference, errors);

        var now = book.Now;
        account.MoveTo(cancelsAll ? FceStates.Cancelled : FceStates.AccountAccepted, now);
        account.AcceptanceKind = "Expresa";
        account.AcceptedAt = now;
        account.Forms = forms;
        account.Withholdings = withholdings;
        account.Adjustments = adjustments;
        account.Cancelled = cancelled;
        account.WithholdingsTotal = withheld;
        account.Embargo = embargo;
        account.AcceptedBalance = accepted;
        account.LastRate = rate;
        foreach (var voucher in new[] { book.InvoiceOf(account) }.Concat(book.NotesOf(account))
                     .Where(v => v.CountsInBalance && v.State.State != FceStates.Rejected))
        {
            voucher.MoveTo(FceStates.Accepted, now);
            voucher.AcceptanceKind = "Expresa";
            voucher.DecidedAt = now;
        }
        if (sca && !cancelsAll)
            account.Sca = new FceSca { AvailableAt = now, AcceptanceKind = "E", InformsCbu = informsCbu == "S", Cbu = cbu, CbuValidated = cbu is null ? null : true };
        await _ledger.SaveAsync(book, account, ct);
        return Operation(call, reference, []);
    }

    private async Task<XElement> CancelAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var reference = ReferenceOf(call.Request);
        var account = Find(book, reference);
        if (Refusal(account, call.Cuit, buyer: true) is { } refusal) return Operation(call, reference, refusal);
        if (book.Today > account!.AcceptanceDue) return Operation(call, reference, 1107);
        if (account.State.State != FceStates.AccountAccepted || account.Option != "ADC") return Operation(call, reference, 1108);

        var forms = call.Request.Child("arrayFormasCancelacion").Children("codigoDescripcion")
            .Select(f => new FceCodeText((short)(f.LongOf("codigo") ?? 0), f.Value("descripcion") ?? "")).ToList();
        var amount = call.Request.DecimalOf("importeCancelacion") ?? 0;
        var errors = new List<int>();
        if (forms.Count == 0) errors.Add(4001);
        else if (forms.Any(f => FceTables.CancellationForms.All(t => t.Code != f.Code)) || forms.DistinctBy(f => f.Code).Count() != forms.Count) errors.Add(4002);
        if (amount < (account.AcceptedBalance ?? book.Balance(account)) - 0.01m) errors.Add(4000);
        if (errors.Count > 0) return Operation(call, reference, errors);

        account.MoveTo(FceStates.Cancelled, book.Now);
        account.Forms = [.. account.Forms, .. forms];
        account.Cancelled = amount;
        await _ledger.SaveAsync(book, account, ct);
        return Operation(call, reference, []);
    }

    // ---- The seller ------------------------------------------------------------------

    private async Task<XElement> ChangeOptionAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var reference = ReferenceOf(call.Request);
        var account = Find(book, reference);
        if (Refusal(account, call.Cuit, buyer: false) is { } refusal) return Operation(call, reference, refusal);
        if (account!.State.State != FceStates.Modifiable) return Operation(call, reference, 1108);
        var option = call.Request.Value("opcionTransferencia");
        if (option == account.Option) return Operation(call, reference, 7000);

        account.Option = option ?? account.Option;
        await _ledger.SaveAsync(book, account, ct);
        return Operation(call, reference, []);
    }

    private async Task<XElement> ReportToAgentAsync(ServiceCall call, FceBook book, CancellationToken ct)
    {
        var reference = ReferenceOf(call.Request);
        var account = Find(book, reference);
        if (Refusal(account, call.Cuit, buyer: false) is { } refusal) return Operation(call, reference, refusal);
        switch (account!.Agent?.State)
        {
            case "D": return Operation(call, reference, 6007);
            case "P": return Operation(call, reference, 6001);
            case "A": return Operation(call, reference, 6000);
            case null when account.State.State != FceStates.AccountAccepted || account.Option != "ADC" || account.AcceptedBalance is not > 0:
                return Operation(call, reference, 1108);
        }

        var target = call.Request.Child("ctaAgente");
        var agent = target?.LongOf("cuitAgente") ?? 0;
        var agentAccount = await _ledger.AgentAccountAsync(agent, target?.Value("idCuenta") ?? "", ct);
        if (agentAccount is not { State: "A" } || agentAccount.Holder != call.Cuit) return Operation(call, reference, 6002);

        await _ledger.NameAsync(book, agent, ct);
        account.Agent = new FceAgentReport
        {
            Agent = agent,
            AccountId = agentAccount.AccountId,
            Denomination = agentAccount.Denomination,
            AvailableAt = book.Now,
        };
        account.MoveTo(FceStates.Reported, book.Now);
        book.InvoiceOf(account).MoveTo(FceStates.Reported, book.Now);
        await _ledger.SaveAsync(book, account, ct);
        return Operation(call, reference, []);
    }

    /// <summary>A CBU's two BCRA check digits: the bank and branch block, and the account block.</summary>
    public static bool IsValidCbu(string cbu)
    {
        if (cbu.Length != 22 || !cbu.All(char.IsAsciiDigit)) return false;
        static bool Block(string digits, int[] weights)
        {
            var sum = 0;
            for (var i = 0; i < weights.Length; i++) sum += (digits[i] - '0') * weights[i];
            return (10 - sum % 10) % 10 == digits[^1] - '0';
        }
        return Block(cbu[..8], [7, 1, 3, 9, 7, 1, 3]) && Block(cbu[8..], [3, 9, 7, 1, 3, 9, 7, 1, 3, 9, 7, 1, 3]);
    }
}
