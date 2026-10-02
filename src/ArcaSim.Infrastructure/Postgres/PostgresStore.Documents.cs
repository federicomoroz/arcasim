using System.Text.Json;
using ArcaSim.Application.Contracts;
using NpgsqlTypes;

namespace ArcaSim.Infrastructure.Postgres;

/// <summary>The services answered through their WSDL keep their documents in two tables, whatever the service.</summary>
public sealed partial class PostgresStore : IDocumentStore
{
    private const string DocumentsSchema = """
        CREATE TABLE IF NOT EXISTS documents (
            collection text NOT NULL,
            key text NOT NULL,
            body jsonb NOT NULL,
            PRIMARY KEY (collection, key));
        CREATE TABLE IF NOT EXISTS counters (
            name text PRIMARY KEY,
            value bigint NOT NULL);
        """;

    private static readonly JsonSerializerOptions DocumentJson = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string collection, string key, CancellationToken ct = default) where T : class
    {
        await using var command = db.CreateCommand("SELECT body::text FROM documents WHERE collection = $1 AND key = $2");
        command.Parameters.AddWithValue(collection);
        command.Parameters.AddWithValue(key);
        return await command.ExecuteScalarAsync(ct) is string json ? JsonSerializer.Deserialize<T>(json, DocumentJson) : null;
    }

    public async Task PutAsync<T>(string collection, string key, T document, CancellationToken ct = default) where T : class
    {
        await using var command = db.CreateCommand(
            "INSERT INTO documents (collection, key, body) VALUES ($1, $2, $3) ON CONFLICT (collection, key) DO UPDATE SET body = EXCLUDED.body");
        command.Parameters.AddWithValue(collection);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(document, DocumentJson));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string collection, string keyPrefix = "", CancellationToken ct = default) where T : class
    {
        await using var command = db.CreateCommand(
            "SELECT body::text FROM documents WHERE collection = $1 AND starts_with(key, $2) ORDER BY key COLLATE \"C\"");
        command.Parameters.AddWithValue(collection);
        command.Parameters.AddWithValue(keyPrefix);
        var list = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), DocumentJson)!);
        return list;
    }

    public async Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default)
    {
        await using var command = db.CreateCommand("DELETE FROM documents WHERE collection = $1 AND key = $2");
        command.Parameters.AddWithValue(collection);
        command.Parameters.AddWithValue(key);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<long> NextAsync(string counter, CancellationToken ct = default)
    {
        await using var command = db.CreateCommand(
            "INSERT INTO counters (name, value) VALUES ($1, 1) ON CONFLICT (name) DO UPDATE SET value = counters.value + 1 RETURNING value");
        command.Parameters.AddWithValue(counter);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }
}
