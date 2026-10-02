using System.Collections.Concurrent;
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
/// One lock per numbering sequence (CUIT, point of sale, voucher type), so two
/// requests for the same sequence cannot both take the same number.
/// </summary>
public sealed class SequenceLocks
{
    private readonly ConcurrentDictionary<(long, int, int), SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(long cuit, int pointOfSale, int voucherType, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd((cuit, pointOfSale, voucherType), _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Release(semaphore);
    }

    private sealed class Release(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
