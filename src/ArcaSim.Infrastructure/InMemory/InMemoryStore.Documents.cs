using System.Text.Json;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Infrastructure.InMemory;

public sealed partial class InMemoryStore : IDocumentStore
{
    // Kept as JSON, as PostgreSQL keeps them: what a caller changes after Put does not leak in.
    private readonly SortedDictionary<(string Collection, string Key), string> _documents = [];
    private readonly Dictionary<string, long> _counters = [];

    public Task<T?> GetAsync<T>(string collection, string key, CancellationToken ct = default) where T : class
    {
        lock (_gate)
            return Task.FromResult(_documents.TryGetValue((collection, key), out var json) ? JsonSerializer.Deserialize<T>(json, Json) : null);
    }

    public Task PutAsync<T>(string collection, string key, T document, CancellationToken ct = default) where T : class
    {
        lock (_gate) _documents[(collection, key)] = JsonSerializer.Serialize(document, Json);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<T>> ListAsync<T>(string collection, string keyPrefix = "", CancellationToken ct = default) where T : class
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<T>>(_documents
                .Where(d => d.Key.Collection == collection && d.Key.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                .Select(d => JsonSerializer.Deserialize<T>(d.Value, Json)!)
                .ToList());
    }

    public Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_documents.Remove((collection, key)));
    }

    public Task<long> NextAsync(string counter, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _counters[counter] = _counters.GetValueOrDefault(counter) + 1;
            return Task.FromResult(_counters[counter]);
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private void ClearDocuments()
    {
        _documents.Clear();
        _counters.Clear();
    }
}
