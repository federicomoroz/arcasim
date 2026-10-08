namespace ArcaSim.Application.Wsfe;

/// <summary>How a voucher was authorized: online with a CAE, or reported afterwards under a CAEA.</summary>
public enum EmissionType
{
    Cae,
    Caea,
}

/// <summary>
/// An authorized voucher, or a range of them for a class B batch. It keeps the
/// detail exactly as it was sent, because FECompConsultar answers with it.
/// </summary>
public sealed record StoredVoucher(
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long From,
    long To,
    DateOnly Date,
    EmissionType EmissionType,
    string AuthorizationCode,
    DateOnly AuthorizationDue,
    DateTimeOffset ProcessedAt,
    FEDetRequest Detail,
    IReadOnlyList<Obs> Observations);

/// <summary>Every voucher WSFEv1 has authorized. Numbering runs per CUIT, point of sale and voucher type.</summary>
public interface IVoucherStore
{
    Task<StoredVoucher?> LastAsync(long cuit, int pointOfSale, int voucherType, CancellationToken ct = default);
    Task<StoredVoucher?> FindAsync(long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct = default);
    Task AddAsync(StoredVoucher voucher, CancellationToken ct = default);
    Task<IReadOnlyList<StoredVoucher>> ListAsync(long? cuit, int limit, CancellationToken ct = default);

    /// <summary>Every voucher of these types, newest first as ListAsync gives them, without reading the vouchers of any other type.</summary>
    Task<IReadOnlyList<StoredVoucher>> ListOfTypesAsync(IReadOnlyCollection<int> voucherTypes, CancellationToken ct = default);
    Task<bool> AnyWithCaeaAsync(long cuit, string caea, int pointOfSale, CancellationToken ct = default);
}

/// <summary>A CAEA granted for one fortnight ("orden" 1 is the 1st to the 15th, 2 the rest of the month).</summary>
public sealed record IssuedCaea(
    long Cuit,
    int Period,
    short Fortnight,
    string Code,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DateOnly ReportDeadline,
    DateTimeOffset ProcessedAt);

/// <summary>A point of sale declared as not having used a CAEA.</summary>
public sealed record CaeaWithoutMovement(long Cuit, string Caea, int PointOfSale, DateOnly ReportedOn);

public interface ICaeaStore
{
    Task<IssuedCaea?> FindAsync(long cuit, int period, short fortnight, CancellationToken ct = default);
    Task<IssuedCaea?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(IssuedCaea caea, CancellationToken ct = default);
    Task<IReadOnlyList<CaeaWithoutMovement>> WithoutMovementAsync(long cuit, string caea, CancellationToken ct = default);
    Task AddWithoutMovementAsync(CaeaWithoutMovement report, CancellationToken ct = default);
}

/// <summary>
/// ARCA's exchange rates. ArcaSim has no access to the real ones, so a test
/// loads the rates it needs; a currency without a rate skips the checks that
/// depend on it.
/// </summary>
public interface IExchangeRates
{
    /// <summary>The latest rate on or before the given day, and the day it is from.</summary>
    Task<(decimal Rate, DateOnly Day)?> RateAsync(string currency, DateOnly onOrBefore, CancellationToken ct = default);
    Task SetAsync(string currency, DateOnly day, decimal rate, CancellationToken ct = default);
}
