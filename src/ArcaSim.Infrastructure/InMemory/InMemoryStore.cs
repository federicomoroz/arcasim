using ArcaSim.Application;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Infrastructure.InMemory;

/// <summary>
/// Every port in memory, behind one lock. It starts in milliseconds and is
/// thrown away with the process: what an application's tests want.
/// </summary>
public sealed partial class InMemoryStore :
    ISimulatorStore
{
    private readonly object _gate = new();
    private readonly List<ClientAlias> _aliases = [];
    private readonly List<ServiceAuthorization> _authorizations = [];
    private readonly List<IssuedTicket> _tickets = [];
    private readonly Dictionary<long, Taxpayer> _taxpayers = [];
    private readonly List<StoredVoucher> _vouchers = [];
    private readonly List<IssuedCaea> _caeas = [];
    private readonly List<CaeaWithoutMovement> _withoutMovement = [];
    private readonly Dictionary<(string, DateOnly), decimal> _rates = [];

    public Task ResetAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            _aliases.Clear();
            _authorizations.Clear();
            _tickets.Clear();
            _taxpayers.Clear();
            _vouchers.Clear();
            _caeas.Clear();
            _withoutMovement.Clear();
            _rates.Clear();
            ClearDocuments();
        }
        return Task.CompletedTask;
    }

    // ---- Access ------------------------------------------------------------

    public Task<IReadOnlyList<ServiceAuthorization>> AuthorizationsForAsync(long clientCuit, string alias, string service, CancellationToken ct = default) =>
        Read<IReadOnlyList<ServiceAuthorization>>(() => _authorizations
            .Where(a => a.ClientCuit == clientCuit
                        && string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(a.Service, service, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task<IReadOnlyList<ServiceAuthorization>> ListAuthorizationsAsync(CancellationToken ct = default) =>
        Read<IReadOnlyList<ServiceAuthorization>>(() => _authorizations.ToList());

    public Task SaveAliasAsync(ClientAlias alias, CancellationToken ct = default) => Write(() =>
    {
        _aliases.RemoveAll(a => a.Cuit == alias.Cuit && string.Equals(a.Alias, alias.Alias, StringComparison.OrdinalIgnoreCase));
        _aliases.Add(alias);
    });

    public Task SaveAuthorizationAsync(ServiceAuthorization authorization, CancellationToken ct = default) => Write(() =>
    {
        _authorizations.Remove(authorization);
        _authorizations.Add(authorization);
    });

    public Task DeleteAuthorizationAsync(ServiceAuthorization authorization, CancellationToken ct = default) =>
        Write(() => _authorizations.Remove(authorization));

    // ---- Tickets -----------------------------------------------------------

    public Task<IssuedTicket?> LatestAsync(string clientDn, string service, CancellationToken ct = default) =>
        Read(() => _tickets.LastOrDefault(t => t.ClientDn == clientDn && t.Service == service));

    public Task AddAsync(IssuedTicket ticket, CancellationToken ct = default) => Write(() => _tickets.Add(ticket));

    // ---- Taxpayers ---------------------------------------------------------
    // Copies in and out, as PostgreSQL's rows are: a caller that changes a taxpayer
    // changes its own copy until it saves, and two requests never share one.

    public Task<Taxpayer?> FindAsync(long cuit, CancellationToken ct = default) =>
        Read(() => _taxpayers.GetValueOrDefault(cuit)?.Copy());

    public Task<IReadOnlyList<Taxpayer>> ListAsync(CancellationToken ct = default) =>
        Read<IReadOnlyList<Taxpayer>>(() => _taxpayers.Values.OrderBy(t => t.Cuit).Select(t => t.Copy()).ToList());

    public Task SaveAsync(Taxpayer taxpayer, CancellationToken ct = default) => Write(() => _taxpayers[taxpayer.Cuit] = taxpayer.Copy());

    // ---- Vouchers ----------------------------------------------------------

    public Task<StoredVoucher?> LastAsync(long cuit, int pointOfSale, int voucherType, CancellationToken ct = default) =>
        Read(() => _vouchers
            .Where(v => v.Cuit == cuit && v.PointOfSale == pointOfSale && v.VoucherType == voucherType)
            .MaxBy(v => v.To));

    public Task<StoredVoucher?> FindAsync(long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct = default) =>
        Read(() => _vouchers.FirstOrDefault(v =>
            v.Cuit == cuit && v.PointOfSale == pointOfSale && v.VoucherType == voucherType && v.From <= number && number <= v.To));

    public Task AddAsync(StoredVoucher voucher, CancellationToken ct = default) => Write(() => _vouchers.Add(voucher));

    public Task<IReadOnlyList<StoredVoucher>> ListAsync(long? cuit, int limit, CancellationToken ct = default) =>
        Read<IReadOnlyList<StoredVoucher>>(() => _vouchers
            .Where(v => cuit is null || v.Cuit == cuit)
            .OrderByDescending(v => v.ProcessedAt)
            .Take(limit)
            .ToList());

    public Task<bool> AnyWithCaeaAsync(long cuit, string caea, int pointOfSale, CancellationToken ct = default) =>
        Read(() => _vouchers.Any(v =>
            v.Cuit == cuit && v.PointOfSale == pointOfSale && v.EmissionType == EmissionType.Caea && v.AuthorizationCode == caea));

    // ---- CAEA --------------------------------------------------------------

    public Task<IssuedCaea?> FindAsync(long cuit, int period, short fortnight, CancellationToken ct = default) =>
        Read(() => _caeas.FirstOrDefault(c => c.Cuit == cuit && c.Period == period && c.Fortnight == fortnight));

    public Task<IssuedCaea?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        Read(() => _caeas.FirstOrDefault(c => c.Code == code));

    public Task AddAsync(IssuedCaea caea, CancellationToken ct = default) => Write(() => _caeas.Add(caea));

    public Task<IReadOnlyList<CaeaWithoutMovement>> WithoutMovementAsync(long cuit, string caea, CancellationToken ct = default) =>
        Read<IReadOnlyList<CaeaWithoutMovement>>(() => _withoutMovement.Where(r => r.Cuit == cuit && r.Caea == caea).ToList());

    public Task AddWithoutMovementAsync(CaeaWithoutMovement report, CancellationToken ct = default) =>
        Write(() => _withoutMovement.Add(report));

    // ---- Exchange rates ----------------------------------------------------

    public Task<(decimal Rate, DateOnly Day)?> RateAsync(string currency, DateOnly onOrBefore, CancellationToken ct = default) =>
        Read<(decimal, DateOnly)?>(() =>
        {
            var match = _rates
                .Where(r => r.Key.Item1 == currency && r.Key.Item2 <= onOrBefore)
                .OrderByDescending(r => r.Key.Item2)
                .Select(r => ((decimal, DateOnly)?)(r.Value, r.Key.Item2))
                .FirstOrDefault();
            return match;
        });

    public Task SetAsync(string currency, DateOnly day, decimal rate, CancellationToken ct = default) =>
        Write(() => _rates[(currency, day)] = rate);

    private Task<T> Read<T>(Func<T> read)
    {
        lock (_gate) return Task.FromResult(read());
    }

    private Task Write(Action write)
    {
        lock (_gate) write();
        return Task.CompletedTask;
    }
}
