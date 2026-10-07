using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Services.Aduana;

/// <summary>
/// A document store that gives way before every operation, so the requests of a
/// test that run together interleave at each read and write the way they do
/// against a database. A read-modify-write that holds no lock then loses an
/// update every time, instead of once in a thousand runs.
/// </summary>
internal sealed class YieldingDocumentStore(IDocumentStore inner) : IDocumentStore
{
    public async Task<T?> GetAsync<T>(string collection, string key, CancellationToken ct = default) where T : class
    {
        await Task.Yield();
        return await inner.GetAsync<T>(collection, key, ct);
    }

    public async Task PutAsync<T>(string collection, string key, T document, CancellationToken ct = default) where T : class
    {
        await Task.Yield();
        await inner.PutAsync(collection, key, document, ct);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string collection, string keyPrefix = "", CancellationToken ct = default) where T : class
    {
        await Task.Yield();
        return await inner.ListAsync<T>(collection, keyPrefix, ct);
    }

    public async Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default)
    {
        await Task.Yield();
        return await inner.DeleteAsync(collection, key, ct);
    }

    public async Task<long> NextAsync(string counter, CancellationToken ct = default)
    {
        await Task.Yield();
        return await inner.NextAsync(counter, ct);
    }
}
