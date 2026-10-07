using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>An observation or a business error, with its code and text.</summary>
public sealed record BookNote(int Code, string Text);

/// <summary>
/// A voucher one of these services authorized: the request's detail exactly
/// as it came (without namespaces, so either service of a family can answer
/// it), the date it was given, the CAE and its expiry, and the observations.
/// RequestId is the Id of the requirement for wsbfe, wsbfev1 and wsseg, 0 for wsct.
/// </summary>
public sealed record BookedVoucher(
    string Service,
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long Number,
    long RequestId,
    DateOnly Date,
    string? SentDate,
    string Cae,
    DateOnly CaeDue,
    string Result,
    List<BookNote> Notes,
    string Detail,
    DateTimeOffset ProcessedAt);

/// <summary>The last number of a sequence (CUIT, point of sale, type) and its date.</summary>
public sealed record BookedLast(long Number, DateOnly Date);

/// <summary>Where a requirement Id was authorized, for the reproceso.</summary>
public sealed record BookedRequest(int PointOfSale, int VoucherType, long Number);

/// <summary>The highest requirement Id a CUIT got authorized.</summary>
public sealed record BookedLastId(long Id);

/// <summary>
/// The vouchers of one family of services in the document store, in
/// collections named after it: by number, the last of each sequence, the
/// requirement Ids already authorized and the highest of them. Every voucher
/// is also recorded in AuthorizedVouchers, for constatación.
/// </summary>
public sealed class VoucherBook(IDocumentStore store, string family, SequenceLocks locks)
{
    private string Vouchers => $"{family}-comprobantes";
    private string Lasts => $"{family}-ultimos";
    private string Requests => $"{family}-requerimientos";
    private string LastIds => $"{family}-ultimo-id";

    private static string Sequence(long cuit, int pointOfSale, int type) => $"{cuit}/{pointOfSale:D5}/{type:D3}";

    public Task<BookedVoucher?> FindAsync(long cuit, int pointOfSale, int type, long number, CancellationToken ct) =>
        store.GetAsync<BookedVoucher>(Vouchers, $"{Sequence(cuit, pointOfSale, type)}/{number:D8}", ct);

    public Task<BookedLast?> LastAsync(long cuit, int pointOfSale, int type, CancellationToken ct) =>
        store.GetAsync<BookedLast>(Lasts, Sequence(cuit, pointOfSale, type), ct);

    public async Task<BookedVoucher?> FindByRequestAsync(long cuit, long id, CancellationToken ct) =>
        await store.GetAsync<BookedRequest>(Requests, $"{cuit}/{id}", ct) is { } at
            ? await FindAsync(cuit, at.PointOfSale, at.VoucherType, at.Number, ct)
            : null;

    public async Task<long> LastRequestIdAsync(long cuit, CancellationToken ct) =>
        (await store.GetAsync<BookedLastId>(LastIds, cuit.ToString(), ct))?.Id ?? 0;

    /// <summary>Keeps the voucher, moves its sequence forward, files its requirement Id and records it for constatación.</summary>
    public async Task AddAsync(BookedVoucher voucher, AuthorizedVoucher authorized, CancellationToken ct)
    {
        var sequence = Sequence(voucher.Cuit, voucher.PointOfSale, voucher.VoucherType);
        await store.PutAsync(Vouchers, $"{sequence}/{voucher.Number:D8}", voucher, ct);
        await store.PutAsync(Lasts, sequence, new BookedLast(voucher.Number, voucher.Date), ct);
        if (voucher.RequestId > 0)
        {
            await store.PutAsync(Requests, $"{voucher.Cuit}/{voucher.RequestId}", new BookedRequest(voucher.PointOfSale, voucher.VoucherType, voucher.Number), ct);

            // The caller holds the lock of this point of sale and type; the CUIT's highest Id is shared by all of
            // them, so it is read and written under a lock of its own, apart from the sequences' (another key space).
            using (await locks.AcquireAsync(LastIds, voucher.Cuit, 0, 0, ct))
                if (voucher.RequestId > await LastRequestIdAsync(voucher.Cuit, ct))
                    await store.PutAsync(LastIds, voucher.Cuit.ToString(), new BookedLastId(voucher.RequestId), ct);
        }
        await store.PutAsync(authorized, ct);
    }
}
