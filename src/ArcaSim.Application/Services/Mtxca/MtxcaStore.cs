using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Mtxca;

public sealed record MtxcaNote(int Code, string Text);

/// <summary>
/// A voucher wsmtxca authorized with a CAE ("E") or took under a CAEA ("A"),
/// with the ComprobanteType exactly as it was sent, because
/// consultarComprobante answers with it.
/// </summary>
public sealed record MtxcaVoucher(
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long Number,
    DateOnly Date,
    string AuthorizationType,
    long AuthorizationCode,
    DateOnly AuthorizationDue,
    string Voucher,
    List<MtxcaNote> Observations);

/// <summary>The last number of a sequence (CUIT, point of sale, type) and its date, for "último + 1" and validation 104.</summary>
public sealed record MtxcaLast(long Number, DateOnly Date);

/// <summary>
/// A CAEA granted for a fortnight, with what was reported against it: the
/// points of sale that informed vouchers, the ones declared as not having used
/// it, and whether the whole CAEA was declared unused.
/// </summary>
public sealed record MtxcaCaea(
    long Cuit,
    int Period,
    short Order,
    long Code,
    DateOnly From,
    DateOnly To,
    DateOnly ReportDeadline,
    DateOnly ProcessedOn,
    bool Unused,
    List<int> UsedPoints,
    List<int> UnusedPoints);

/// <summary>Where a CAEA is filed, to find it by its code.</summary>
public sealed record MtxcaCaeaIndex(long Cuit, int Period, short Order);

/// <summary>wsmtxca's state in the document store, in collections named after the service.</summary>
public sealed class MtxcaStore(IDocumentStore store)
{
    private const string Vouchers = "wsmtxca-comprobantes";
    private const string Lasts = "wsmtxca-ultimos";
    private const string Caeas = "wsmtxca-caea";
    private const string CaeaCodes = "wsmtxca-caea-codigos";

    /// <summary>A sequence's key (CUIT, point of sale, type): the front of AuthorizedVouchers.Key, which names the vouchers.</summary>
    private static string Sequence(long cuit, int pointOfSale, int type) => $"{cuit}/{pointOfSale:D5}/{type:D3}";

    public Task<MtxcaLast?> LastAsync(long cuit, int pointOfSale, int type, CancellationToken ct) =>
        store.GetAsync<MtxcaLast>(Lasts, Sequence(cuit, pointOfSale, type), ct);

    public Task<MtxcaVoucher?> FindAsync(long cuit, int pointOfSale, int type, long number, CancellationToken ct) =>
        store.GetAsync<MtxcaVoucher>(Vouchers, AuthorizedVouchers.Key(cuit, pointOfSale, type, number), ct);

    /// <summary>Keeps the voucher, moves its sequence forward and records it for constatación.</summary>
    public async Task AddAsync(MtxcaVoucher voucher, int receiverDocType, long receiverDocNumber, decimal total, CancellationToken ct)
    {
        await store.PutAsync(Vouchers, AuthorizedVouchers.Key(voucher.Cuit, voucher.PointOfSale, voucher.VoucherType, voucher.Number), voucher, ct);
        await store.PutAsync(Lasts, Sequence(voucher.Cuit, voucher.PointOfSale, voucher.VoucherType), new MtxcaLast(voucher.Number, voucher.Date), ct);
        await store.PutAsync(new AuthorizedVoucher("wsmtxca", voucher.Cuit, voucher.PointOfSale, voucher.VoucherType, voucher.Number,
            voucher.Date, total, receiverDocType, receiverDocNumber, voucher.AuthorizationType == "E" ? "CAE" : "CAEA",
            voucher.AuthorizationCode.ToString(), voucher.AuthorizationDue), ct);
    }

    private static string CaeaKey(long cuit, int period, short order) => $"{cuit}/{period}/{order}";

    public Task<MtxcaCaea?> FindCaeaAsync(long cuit, int period, short order, CancellationToken ct) =>
        store.GetAsync<MtxcaCaea>(Caeas, CaeaKey(cuit, period, order), ct);

    public async Task<MtxcaCaea?> FindCaeaAsync(long code, CancellationToken ct) =>
        await store.GetAsync<MtxcaCaeaIndex>(CaeaCodes, code.ToString(), ct) is { } index
            ? await FindCaeaAsync(index.Cuit, index.Period, index.Order, ct)
            : null;

    public Task<IReadOnlyList<MtxcaCaea>> CaeasOfAsync(long cuit, CancellationToken ct) =>
        store.ListAsync<MtxcaCaea>(Caeas, $"{cuit}/", ct);

    public async Task SaveCaeaAsync(MtxcaCaea caea, CancellationToken ct)
    {
        await store.PutAsync(Caeas, CaeaKey(caea.Cuit, caea.Period, caea.Order), caea, ct);
        await store.PutAsync(CaeaCodes, caea.Code.ToString(), new MtxcaCaeaIndex(caea.Cuit, caea.Period, caea.Order), ct);
    }
}
