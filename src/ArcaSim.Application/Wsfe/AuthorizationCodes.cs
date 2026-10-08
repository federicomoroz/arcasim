using System.Security.Cryptography;

namespace ArcaSim.Application.Wsfe;

/// <summary>Hands out CAE and CAEA numbers. A test can replace it to get predictable ones.</summary>
public interface IAuthorizationCodes
{
    string NextCae();
    string NextCaea();
}

/// <summary>
/// Fourteen random digits, like every CAE and CAEA seen. ARCA does not publish
/// whether the number has a structure (wsfev1.md §6.1), so ArcaSim gives it none.
/// </summary>
public sealed class RandomAuthorizationCodes : IAuthorizationCodes
{
    public string NextCae() => Next();

    public string NextCaea() => Next();

    private static string Next() => RandomNumberGenerator.GetInt32(1, 10).ToString() +
                                    string.Concat(Enumerable.Range(0, 13).Select(_ => RandomNumberGenerator.GetInt32(0, 10)));
}

/// <summary>
/// One lock per numbering sequence (CUIT, point of sale, voucher type) of one
/// service, so two requests for the same sequence cannot both take the same
/// number. The service keeps each one's sequences apart: a lock taken inside
/// another (a remito's request, then its number) never waits on itself, even
/// when the request names a type or point of sale another lock uses.
/// </summary>
public sealed class SequenceLocks
{
    private readonly KeyedLocks<(string Service, long Cuit, int PointOfSale, int VoucherType)> _locks = new();

    public Task<IDisposable> AcquireAsync(string service, long cuit, int pointOfSale, int voucherType, CancellationToken ct) =>
        _locks.AcquireAsync((service, cuit, pointOfSale, voucherType), ct);

    /// <summary>The sequences with a lock right now: held or waited for.</summary>
    public int Count => _locks.Count;
}
