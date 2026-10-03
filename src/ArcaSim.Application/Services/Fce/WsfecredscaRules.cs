using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using static ArcaSim.Application.Services.Fce.FceSpring;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// wsfecredsca, the Sistema de Circulación Abierta's side of the FCE
/// (docs/arca/servicios/wsfecredsca.md): the invoices accepted in wsfecred with
/// the SCA option are available (D) from the instant of the acceptance; the
/// SCA reads them (D turns P on the page that returns it, which wsfecred shows
/// as fechaLecturaSCA) and confirms their reception (P turns R). Format errors
/// go in erroresFormato, business errors in errores, per item in each resultado.
///
/// Where the manual is silent (NO VERIFICADO), these are ArcaSim's choices:
/// - any taxpayer registered in ArcaSim may act as the SCA, and every one sees
///   every SCA invoice; anyone else gets 4009 in errores;
/// - tacitly accepted invoices reach the SCA with tipoAceptacion T and no
///   buyer CBU; a total cancellation (saldo 0) never reaches it;
/// - a buyer CBU always counts as validated (cbuValidada S), so
///   errorValidacionCbu is never sent;
/// - only the D invoices returned on the page turn P, and they come back
///   already in P; a later read keeps the first fechaHoraLecturaSCA;
/// - a query with nothing to return answers the empty list, the page asked
///   and hayMas N, without observations;
/// - fechaVencimientoPago is the invoice's, or its acceptance deadline when it
///   has none.
/// dummy keeps the contract's answer.
/// </summary>
public sealed class WsfecredscaRules(IDocumentStore store, IVoucherStore vouchers, ITaxpayerRepository taxpayers, IClock clock) : IServiceBehavior
{
    private static readonly IReadOnlyDictionary<int, string> Texts = FceTexts.Sca;

    private readonly FceLedger _ledger = new(store, vouchers, taxpayers, clock);

    public string Service => "wsfecredsca";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        Func<ServiceCall, CancellationToken, Task<XElement>>? operation = call.Name switch
        {
            "consultarFacturasAceptadas" => InvoicesAsync,
            "confirmarRecepcionFacturas" => ConfirmAsync,
            _ => null,
        };
        if (operation is null) return null;

        if (!await _ledger.IsRegisteredAsync(call.Cuit, ct)) return call.Ok(Refused(call, Errors(Texts, [4009]), null));
        using var _ = await _ledger.LockAsync(ct);
        return call.Ok(await operation(call, ct));
    }

    private static XElement Refused(ServiceCall call, XElement? errors, XElement? formatErrors) =>
        call.Name == "consultarFacturasAceptadas"
            ? Result(call.Operation.Output, FailedQuery("facturas", errors, formatErrors))
            : Result(call.Operation.Output, errors, formatErrors, new XElement("resultados"));

    private static DateTimeOffset? MomentOf(FceSca sca, string? kind) => kind switch
    {
        "Disponible" => sca.AvailableAt,
        "Consultada" => sca.ReadAt,
        "Recibida" => sca.ConfirmedAt,
        _ => null,
    };

    private async Task<XElement> InvoicesAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var issuer = request.LongOf("cuitEmisor");
        var receiver = request.LongOf("cuitReceptor");
        var format = CheckQuery(request, out var page, out var range);
        if (BadCuit(issuer) || BadCuit(receiver)) format.Insert(0, 2002);
        if (format.Count > 0) return Refused(call, null, FormatErrors(Texts, format));

        var book = await _ledger.OpenAsync(ct);
        var state = request.Value("estadoFactura");
        var all = book.Accounts.Values
            .Where(a => a.Sca is not null)
            .Where(a => state is null || a.Sca!.State == state)
            .Where(a => issuer is null || a.Issuer == issuer)
            .Where(a => receiver is null || a.Receiver == receiver)
            .Where(a => InRange(MomentOf(a.Sca!, range.Kind), range))
            .OrderBy(a => a.Sca!.AvailableAt).ThenBy(a => a.Code)
            .ToList();
        var items = all.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        foreach (var account in items.Where(a => a.Sca!.State == "D"))
        {
            account.Sca!.State = "P";
            account.Sca.ReadAt = book.Now;
            await _ledger.SaveAsync(book, account, ct);
        }

        var rows = new List<XElement>();
        foreach (var account in items)
        {
            var invoice = book.InvoiceOf(account);
            var sca = account.Sca!;
            rows.Add(new XElement("factura",
                IdFactura(account.Invoice),
                new XElement("tipoAceptacion", sca.AcceptanceKind),
                new XElement("razonSocialEmisor", FceXml.Text250(await _ledger.NameAsync(book, account.Issuer, ct))),
                invoice.Cbu is { } cbu ? new XElement("cbuEmisor", cbu) : null,
                invoice.Alias is { } alias ? new XElement("aliasCBUEmisor", alias) : null,
                new XElement("cuitComprador", account.Receiver),
                new XElement("razonSocialComprador", FceXml.Text250(await _ledger.NameAsync(book, account.Receiver, ct))),
                new XElement("informaCbuComprador", FceXml.YesNo(sca.InformsCbu)),
                sca.InformsCbu && sca.CbuValidated is { } validated ? new XElement("cbuValidada", FceXml.YesNo(validated)) : null,
                sca.InformsCbu && sca.Cbu is { } buyerCbu ? new XElement("cbuComprador", buyerCbu) : null,
                new XElement("fechaEmision", FceXml.Date(invoice.Date)),
                new XElement("fechaVencimientoPago", FceXml.Date(invoice.PaymentDue ?? account.AcceptanceDue)),
                new XElement("saldoAceptado", FceXml.Money(account.AcceptedBalance ?? 0)),
                new XElement("codMoneda", account.Currency),
                new XElement("estadoFactura", sca.State),
                new XElement("fechaHoraDisponible", FceXml.Moment(sca.AvailableAt)),
                sca.ReadAt is { } read ? new XElement("fechaHoraLecturaSCA", FceXml.Moment(read)) : null,
                sca.ConfirmedAt is { } confirmed ? new XElement("fechaHoraConfirmacionSCA", FceXml.Moment(confirmed)) : null));
        }
        return Result(call.Operation.Output,
            new XElement("facturas", rows),
            new XElement("nroPagina", page),
            new XElement("hayMas", FceXml.YesNo(all.Count > page * PageSize)));
    }

    private async Task<XElement> ConfirmAsync(ServiceCall call, CancellationToken ct)
    {
        var ids = call.Request.Child("facturas").Children("factura").Select(f => IdOf(f.Child("idFactura"))).ToList();
        var format = new List<int>();
        if (ids.Count > BatchSize) format.Add(2006);
        if (ids.Any(id => id is null || BadCuit(id.Cuit))) format.Add(2002);
        if (format.Count > 0) return Refused(call, null, FormatErrors(Texts, format));

        var book = await _ledger.OpenAsync(ct);
        var results = new List<XElement>();
        foreach (var id in ids)
        {
            var invoice = book.Find(id!);
            var account = invoice is { IsInvoice: true } ? book.Accounts[invoice.Account] : null;
            int? refusal = account?.Sca switch
            {
                null => 4012,
                { State: "D" } => 4014,
                { State: "R" } => 4013,
                _ => null,
            };
            if (refusal is null)
            {
                account!.Sca!.State = "R";
                account.Sca.ConfirmedAt = book.Now;
                await _ledger.SaveAsync(book, account, ct);
            }
            results.Add(new XElement("resultado",
                IdFactura(id!),
                new XElement("resultado", refusal is null ? "A" : "R"),
                refusal is { } code ? Errors(Texts, [code]) : null));
        }
        return Result(call.Operation.Output, new XElement("resultados", results));
    }
}
