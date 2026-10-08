namespace ArcaSim.Application.Contracts;

/// <summary>
/// A voucher one of the invoicing services authorized (wsmtxca, wsfexv1, wsct,
/// wsbfev1, wsseg...), as constatación (wscdc) checks it: who issued it, its
/// number, date, total, receiver and the CAE or CAEA it got. WSFEv1's vouchers
/// live in IVoucherStore; the rest write here when they authorize one.
/// </summary>
public sealed record AuthorizedVoucher(
    string Service,
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long Number,
    DateOnly Date,
    decimal Total,
    int ReceiverDocType,
    long ReceiverDocNumber,
    string EmissionType,
    string Code,
    DateOnly? CodeExpiry);

public static class AuthorizedVouchers
{
    public const string Collection = "comprobantes";

    public static string Key(long cuit, int pointOfSale, int voucherType, long number) =>
        $"{SequenceKey(cuit, pointOfSale, voucherType)}/{number:D8}";

    /// <summary>
    /// A sequence's key (CUIT, point of sale, type): the front of <see cref="Key"/>, for a
    /// service that keeps its last number apart from its vouchers or lists one sequence's
    /// vouchers by key prefix.
    /// </summary>
    public static string SequenceKey(long cuit, int pointOfSale, int voucherType) =>
        $"{cuit}/{pointOfSale:D5}/{voucherType:D3}";

    public static Task PutAsync(this IDocumentStore store, AuthorizedVoucher voucher, CancellationToken ct = default) =>
        store.PutAsync(Collection, Key(voucher.Cuit, voucher.PointOfSale, voucher.VoucherType, voucher.Number), voucher, ct);

    public static Task<AuthorizedVoucher?> FindVoucherAsync(this IDocumentStore store, long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct = default) =>
        store.GetAsync<AuthorizedVoucher>(Collection, Key(cuit, pointOfSale, voucherType, number), ct);
}
