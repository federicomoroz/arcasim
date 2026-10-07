using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.FacturacionE;

/// <summary>
/// WSFEXv1's state in the document store: the authorized vouchers by CUIT,
/// point of sale, type and number; each approved request Id pointing at its
/// voucher, for Reproceso; and the highest approved Id per CUIT. Every voucher
/// also goes to AuthorizedVouchers, where constatación finds it.
/// </summary>
internal sealed class ExportStore(IDocumentStore documents, SequenceLocks locks)
{
    public const string Service = "wsfexv1";
    private const string Vouchers = "wsfexv1.comprobantes";
    private const string Requests = "wsfexv1.ids";
    private const string LastIds = "wsfexv1.ultimo_id";

    private sealed record RequestRef(string Voucher);

    private sealed record LastId(long Id);

    public Task<AuthorizedExport?> FindAsync(long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct) =>
        documents.GetAsync<AuthorizedExport>(Vouchers, AuthorizedVouchers.Key(cuit, pointOfSale, voucherType, number), ct);

    public async Task<AuthorizedExport?> LastAsync(long cuit, int pointOfSale, int voucherType, CancellationToken ct) =>
        (await documents.ListAsync<AuthorizedExport>(Vouchers, $"{cuit}/{pointOfSale:D5}/{voucherType:D3}/", ct)).LastOrDefault();

    public async Task<AuthorizedExport?> ByRequestAsync(long cuit, long id, CancellationToken ct) =>
        await documents.GetAsync<RequestRef>(Requests, $"{cuit}/{id}", ct) is { } reference
            ? await documents.GetAsync<AuthorizedExport>(Vouchers, reference.Voucher, ct)
            : null;

    public async Task<long> LastIdAsync(long cuit, CancellationToken ct) =>
        (await documents.GetAsync<LastId>(LastIds, cuit.ToString(), ct))?.Id ?? 0;

    public async Task AddAsync(AuthorizedExport authorized, DateOnly date, DateOnly due, CancellationToken ct)
    {
        var voucher = authorized.Voucher;
        var key = AuthorizedVouchers.Key(authorized.Cuit, voucher.PointOfSale, voucher.VoucherType, voucher.Number);
        await documents.PutAsync(Vouchers, key, authorized, ct);
        await documents.PutAsync(Requests, $"{authorized.Cuit}/{voucher.Id}", new RequestRef(key), ct);

        // The CUIT's last Id has a lock of its own, apart from the sequence lock the caller holds.
        using (await locks.AcquireAsync(LastIds, authorized.Cuit, 0, 0, ct))
            if (voucher.Id > await LastIdAsync(authorized.Cuit, ct))
                await documents.PutAsync(LastIds, authorized.Cuit.ToString(), new LastId(voucher.Id), ct);

        var receiver = voucher.ClientCountryCuit;
        await documents.PutAsync(new AuthorizedVoucher(
            Service, authorized.Cuit, voucher.PointOfSale, voucher.VoucherType, voucher.Number, date, voucher.Total,
            receiver == 0 ? 0 : 80, receiver, "CAE", authorized.Cae, due), ct);
    }
}
