namespace ArcaSim.Application.Contracts;

/// <summary>
/// Where the services answered through their WSDL keep their state: documents
/// by collection and key, as JSON, and counters. One port for every service,
/// so a service's rules persist in memory or in PostgreSQL like wsfev1's do,
/// without a table per service.
/// </summary>
public interface IDocumentStore
{
    Task<T?> GetAsync<T>(string collection, string key, CancellationToken ct = default) where T : class;

    Task PutAsync<T>(string collection, string key, T document, CancellationToken ct = default) where T : class;

    /// <summary>The documents of a collection whose key starts with the prefix, in key order.</summary>
    Task<IReadOnlyList<T>> ListAsync<T>(string collection, string keyPrefix = "", CancellationToken ct = default) where T : class;

    Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default);

    /// <summary>The next value of a counter, starting at 1: an id that never repeats, even across restarts with PostgreSQL.</summary>
    Task<long> NextAsync(string counter, CancellationToken ct = default);
}
