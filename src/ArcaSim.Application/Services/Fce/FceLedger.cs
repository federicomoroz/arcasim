using System.Globalization;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// Everything the three FCE services know at one instant: every current
/// account and voucher, already caught up with what WSFEv1 authorized and
/// with the time that has passed.
/// </summary>
public sealed class FceBook(DateTimeOffset now)
{
    public DateTimeOffset Now { get; } = now;
    public DateOnly Today => Now.ArgentinaDate();
    public Dictionary<long, FceAccount> Accounts { get; } = [];
    public Dictionary<string, FceVoucher> Vouchers { get; } = [];
    internal Dictionary<long, string> Names { get; } = [];

    public FceVoucher? Find(FceId id) => Vouchers.GetValueOrDefault(id.Key);

    public FceVoucher InvoiceOf(FceAccount account) => Vouchers[account.Invoice.Key];

    public IEnumerable<FceVoucher> NotesOf(FceAccount account) => account.Notes.Select(key => Vouchers[key]);

    /// <summary>Notes that move the balance and were not rejected: debit notes add, credit notes subtract.</summary>
    public decimal NotesTotal(FceAccount account) =>
        NotesOf(account).Where(n => n.CountsInBalance && n.State.State != FceStates.Rejected).Sum(n => n.IsDebit ? n.Total : -n.Total);

    /// <summary>saldo: invoice + debit notes − credit notes not rejected (wsfecred.md, CuentaCorrienteType/saldo).</summary>
    public decimal Balance(FceAccount account) => account.Initial + NotesTotal(account);

    public string Name(long cuit) => Names.GetValueOrDefault(cuit) ?? "";
}

/// <summary>
/// The state behind wsfecred, wsfecredagente and wsfecredsca
/// (docs/arca/servicios/wsfecred.md "Comportamiento a simular"): one current
/// account per FCE invoice authorized by WSFEv1 (IVoucherStore) or by another
/// invoicing service (AuthorizedVouchers), the notes that join it, the agents'
/// accounts, and the deadlines that move states on their own.
///
/// The manual gives no figures for the deadlines, so these are ArcaSim's:
/// fechaPuestaDispo is the day WSFEv1 authorized the voucher; it turns
/// Recepcionado, and the buyer may operate (1106), from 00:00 two days later,
/// "la hora 24 del día siguiente"; the acceptance deadline (fechaVenAcep) is
/// 30 calendar days after fechaPuestaDispo, and the day after it an account
/// still Modificable is accepted tacitly with its whole balance. An invoice
/// issued without WSFEv1's optional 27 gets the ADC option. Notes issued once
/// the account left Modificable, and cancelling notes (optional 22 = S), do
/// not move the balance and stop at Recepcionado.
/// </summary>
public sealed class FceLedger(IDocumentStore store, IVoucherStore wsfe, ITaxpayerRepository taxpayers, IClock clock)
{
    private const string AccountsCollection = "wsfecred.ctasctes";
    private const string VouchersCollection = "wsfecred.comprobantes";
    private const string AgentAccountsCollection = "wsfecredagente.cuentas";
    private const string AccountCounter = "wsfecred.codCtaCte";

    private const int AcceptanceDays = 30;
    private const int OperableAfterDays = 2;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>One operation at a time: the three services read and write the same accounts, through this one ledger.</summary>
    public async Task<IDisposable> LockAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        return new Release(_gate);
    }

    /// <summary>When the buyer may first operate on a voucher (1106), and when it turns Recepcionado.</summary>
    public static DateTimeOffset OperableFrom(FceVoucher voucher) => ArgentinaTime.StartOf(voucher.AvailableOn.AddDays(OperableAfterDays));

    public static DateTimeOffset TacitAcceptanceAt(FceAccount account) => ArgentinaTime.StartOf(account.AcceptanceDue.AddDays(1));

    /// <summary>The accounts and vouchers as they stand now: new FCE vouchers registered, deadlines applied, changes saved.</summary>
    public async Task<FceBook> OpenAsync(CancellationToken ct)
    {
        var book = new FceBook(clock.Now);
        foreach (var account in await store.ListAsync<FceAccount>(AccountsCollection, "", ct)) book.Accounts[account.Code] = account;
        foreach (var voucher in await store.ListAsync<FceVoucher>(VouchersCollection, "", ct)) book.Vouchers[voucher.Id.Key] = voucher;

        var changed = new HashSet<long>();
        await RegisterNewAsync(book, changed, ct);
        foreach (var account in book.Accounts.Values)
            if (Settle(book, account, book.Now)) changed.Add(account.Code);
        foreach (var code in changed) await SaveAsync(book, book.Accounts[code], ct);

        foreach (var cuit in book.Vouchers.Values.SelectMany(v => new[] { v.Id.Cuit, v.Receiver }).Distinct())
            await NameAsync(book, cuit, ct);
        return book;
    }

    /// <summary>Saves an account with its invoice and notes.</summary>
    public async Task SaveAsync(FceBook book, FceAccount account, CancellationToken ct)
    {
        await store.PutAsync(AccountsCollection, account.Code.ToString("D12", CultureInfo.InvariantCulture), account, ct);
        await store.PutAsync(VouchersCollection, account.Invoice.Key, book.InvoiceOf(account), ct);
        foreach (var note in book.NotesOf(account)) await store.PutAsync(VouchersCollection, note.Id.Key, note, ct);
    }

    /// <summary>The razón social ARCA would show: the registered taxpayer's name, upper case.</summary>
    public async Task<string> NameAsync(FceBook book, long cuit, CancellationToken ct)
    {
        if (book.Names.TryGetValue(cuit, out var name)) return name;
        name = (await taxpayers.FindAsync(cuit, ct))?.Name.ToUpperInvariant() ?? "";
        book.Names[cuit] = name;
        return name;
    }

    public async Task<bool> IsRegisteredAsync(long cuit, CancellationToken ct) => await taxpayers.FindAsync(cuit, ct) is not null;

    public Task<FceAgentAccount?> AgentAccountAsync(long agent, string accountId, CancellationToken ct) =>
        store.GetAsync<FceAgentAccount>(AgentAccountsCollection, $"{agent}/{accountId}", ct);

    /// <summary>Every agent's accounts, for a seller looking for the ones opened in its name.</summary>
    public Task<IReadOnlyList<FceAgentAccount>> AgentAccountsAsync(CancellationToken ct) =>
        store.ListAsync<FceAgentAccount>(AgentAccountsCollection, "", ct);

    /// <summary>One agent's accounts, read by their key prefix (the agent's CUIT) instead of looking through everyone's.</summary>
    public Task<IReadOnlyList<FceAgentAccount>> AgentAccountsOfAsync(long agent, CancellationToken ct) =>
        store.ListAsync<FceAgentAccount>(AgentAccountsCollection, $"{agent}/", ct);

    public Task SaveAsync(FceAgentAccount account, CancellationToken ct) =>
        store.PutAsync(AgentAccountsCollection, account.Key, account, ct);

    // ---- Deadlines -------------------------------------------------------------------

    /// <summary>
    /// Applies what time does on its own up to an instant: vouchers reach
    /// Recepcionado, and an account still Modificable after its deadline is
    /// accepted tacitly (going to the SCA when that is its option).
    /// </summary>
    public static bool Settle(FceBook book, FceAccount account, DateTimeOffset until)
    {
        var count = account.History.Count + (account.Sca is null ? 0 : 1);
        var vouchers = new[] { book.InvoiceOf(account) }.Concat(book.NotesOf(account)).ToList();
        var voucherCount = vouchers.Sum(v => v.History.Count);

        foreach (var voucher in vouchers)
            if (voucher.State.State == FceStates.PendingReception && OperableFrom(voucher) <= until)
                voucher.MoveTo(FceStates.Received, OperableFrom(voucher));

        var tacit = TacitAcceptanceAt(account);
        if (account.State.State == FceStates.Modifiable && tacit <= until)
        {
            account.MoveTo(FceStates.AccountAccepted, tacit);
            account.AcceptedBalance = book.Balance(account);
            foreach (var voucher in vouchers.Where(v => v.CountsInBalance && v.State.State != FceStates.Rejected))
            {
                voucher.MoveTo(FceStates.Accepted, tacit);
                voucher.AcceptanceKind = "Tacita";
                voucher.DecidedAt = tacit;
            }
            if (account.Option == "SCA" && account.AcceptedBalance > 0)
                account.Sca = new FceSca { AvailableAt = tacit, AcceptanceKind = "T" };
        }

        return account.History.Count + (account.Sca is null ? 0 : 1) != count || vouchers.Sum(v => v.History.Count) != voucherCount;
    }

    // ---- Registration ----------------------------------------------------------------

    private sealed record Arrival(FceVoucher Voucher, DateTimeOffset At);

    /// <summary>
    /// FCE vouchers authorized since the last look: an invoice opens an
    /// account; a note joins the account of the voucher it references, and
    /// waits outside until that voucher is known. Every voucher WSFEv1 has
    /// authorized is looked at: a cap would leave the oldest ones without an account.
    /// </summary>
    private async Task RegisterNewAsync(FceBook book, HashSet<long> changed, CancellationToken ct)
    {
        var arrivals = new List<Arrival>();
        foreach (var stored in await wsfe.ListOfTypesAsync(FceTypes.All, ct))
            if (!book.Vouchers.ContainsKey(new FceId(stored.Cuit, stored.VoucherType, stored.PointOfSale, stored.From).Key))
                arrivals.Add(new Arrival(FromWsfe(stored), stored.ProcessedAt));

        // Other services record no CbtesAsoc, so only their invoices can open an account.
        foreach (var other in await store.ListAsync<AuthorizedVoucher>(AuthorizedVouchers.Collection, "", ct))
            if (FceTypes.IsInvoice(other.VoucherType)
                && !book.Vouchers.ContainsKey(new FceId(other.Cuit, other.VoucherType, other.PointOfSale, other.Number).Key))
                arrivals.Add(new Arrival(FromOther(other), ArgentinaTime.StartOf(other.Date)));

        foreach (var arrival in arrivals.OrderBy(a => a.Voucher.IsInvoice ? 0 : 1).ThenBy(a => a.At))
        {
            var voucher = arrival.Voucher;
            voucher.History = [new FceState(FceStates.PendingReception, arrival.At)];
            if (voucher.IsInvoice)
            {
                var account = new FceAccount
                {
                    Code = await store.NextAsync(AccountCounter, ct),
                    Invoice = voucher.Id,
                    Issuer = voucher.Id.Cuit,
                    Receiver = voucher.Receiver,
                    Option = voucher.Option ?? "ADC",
                    Currency = voucher.Currency,
                    Initial = voucher.Total,
                    LastRate = voucher.Rate,
                    AcceptanceDue = voucher.AvailableOn.AddDays(AcceptanceDays),
                    History = [new FceState(FceStates.Modifiable, arrival.At)],
                };
                voucher.Account = account.Code;
                book.Vouchers[voucher.Id.Key] = voucher;
                book.Accounts[account.Code] = account;
                changed.Add(account.Code);
                continue;
            }

            if (voucher.Associated is null || book.Find(voucher.Associated) is not { } referenced) continue;
            var target = book.Accounts[referenced.Account];
            Settle(book, target, arrival.At);
            voucher.Account = target.Code;
            voucher.PostAcceptance = target.StateAt(arrival.At) != FceStates.Modifiable;
            book.Vouchers[voucher.Id.Key] = voucher;
            target.Notes.Add(voucher.Id.Key);
            target.LastRate = voucher.Rate;
            changed.Add(target.Code);
        }
    }

    private static FceVoucher FromWsfe(StoredVoucher stored)
    {
        var detail = stored.Detail;
        string? Optional(string id) => detail.Opcionales?.FirstOrDefault(o => o.Id?.Trim() == id)?.Valor?.Trim();
        var associated = detail.CbtesAsoc ?? [];
        FceId IdOf(CbteAsoc a) =>
            new(long.TryParse(a.Cuit, NumberStyles.None, CultureInfo.InvariantCulture, out var cuit) && cuit > 0 ? cuit : stored.Cuit, a.Tipo, a.PtoVta, a.Nro);

        return new FceVoucher
        {
            Id = new FceId(stored.Cuit, stored.VoucherType, stored.PointOfSale, stored.From),
            Receiver = detail.DocNro,
            AuthorizationKind = stored.EmissionType == EmissionType.Cae ? "E" : "A",
            AuthorizationCode = long.TryParse(stored.AuthorizationCode, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : 0,
            Date = stored.Date,
            AvailableOn = stored.ProcessedAt.ArgentinaDate(),
            PaymentDue = Fev1Dates.TryParse(detail.FchVtoPago, out var due) ? due : null,
            Total = Money(detail.ImpTotal),
            Currency = string.IsNullOrWhiteSpace(detail.MonId) ? "PES" : detail.MonId.Trim(),
            Rate = detail.MonCotiz > 0 ? Math.Round((decimal)detail.MonCotiz, 6) : 1,
            Cbu = Optional("2101") is { Length: 22 } cbu ? cbu : null,
            Alias = Optional("2102") is { Length: >= 3 and <= 250 } alias ? alias : null,
            Option = Optional("27") is "SCA" or "ADC" ? Optional("27") : null,
            Cancels = Optional("22") == "S",
            References = (detail.Opcionales ?? []).Where(o => o.Id?.Trim() == "23" && o.Valor?.Trim().Length is >= 3 and <= 250)
                .Select(o => o.Valor!.Trim()).ToList(),
            Vat = (detail.Iva ?? []).Select(a => new FceVat((short)a.Id, Money(a.BaseImp), Money(a.Importe))).ToList(),
            Taxes = (detail.Tributos ?? []).Select(t => new FceTax(t.Id, t.Desc?.Trim() is { Length: >= 3 } d ? d[..Math.Min(d.Length, 250)] : null,
                Money(t.BaseImp), Money(t.Importe))).ToList(),
            DeliveryNotes = associated.Where(a => FceTypes.IsDeliveryNote(a.Tipo)).Select(IdOf).ToList(),
            Associated = associated.Where(a => FceTypes.IsFce(a.Tipo)).Select(IdOf).FirstOrDefault(),
        };
    }

    private static FceVoucher FromOther(AuthorizedVoucher other) => new()
    {
        Id = new FceId(other.Cuit, other.VoucherType, other.PointOfSale, other.Number),
        Receiver = other.ReceiverDocNumber,
        AuthorizationKind = other.EmissionType.Equals("CAEA", StringComparison.OrdinalIgnoreCase) ? "A" : "E",
        AuthorizationCode = long.TryParse(other.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : 0,
        Date = other.Date,
        AvailableOn = other.Date,
        Total = other.Total,
    };

    private static decimal Money(double value) => Math.Round((decimal)value, 2);

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
