using ArcaSim.Infrastructure.Postgres;
using ArcaSim.Tests.Support;
using Npgsql;

namespace ArcaSim.Tests.Storage;

/// <summary>What the PostgreSQL schema does for the queries, beyond what the store contract checks.</summary>
[Collection(PostgresCollection.Name)]
[RequiresDocker]
public class PostgresSchemaTests(PostgresContainer postgres)
{
    private sealed record Note(string Text);

    [DockerFact]
    public async Task A_documents_table_from_before_gets_its_key_collated_C_and_keeps_its_documents()
    {
        await using var db = NpgsqlDataSource.Create(await postgres.NewDatabaseAsync());
        await ExecuteAsync(db, "CREATE TABLE documents (collection text NOT NULL, key text NOT NULL, body jsonb NOT NULL, PRIMARY KEY (collection, key))");
        await ExecuteAsync(db, """INSERT INTO documents VALUES ('notas', 'b', '{"text":"two"}'), ('notas', 'a', '{"text":"one"}')""");
        var store = new PostgresStore(db);

        await store.EnsureSchemaAsync();
        await store.EnsureSchemaAsync();

        Assert.Equal("C", await ScalarAsync(db,
            "SELECT c.collname FROM pg_attribute a JOIN pg_collation c ON c.oid = a.attcollation WHERE a.attrelid = 'documents'::regclass AND a.attname = 'key'"));
        Assert.Equal(["one", "two"], (await store.ListAsync<Note>("notas")).Select(n => n.Text));
    }

    [DockerFact]
    public async Task A_key_prefix_is_read_as_a_range_of_the_primary_key_already_in_order()
    {
        await using var db = NpgsqlDataSource.Create(await postgres.NewDatabaseAsync());
        await new PostgresStore(db).EnsureSchemaAsync();
        await using var connection = await db.OpenConnectionAsync();
        await using (var off = new NpgsqlCommand("SET enable_seqscan = off", connection)) await off.ExecuteNonQueryAsync();

        // The listing's own query, with the collection and the prefix it is given: the index has to serve both, and the order.
        await using var explain = new NpgsqlCommand(
            "EXPLAIN SELECT body::text FROM documents WHERE collection = 'remitos' AND starts_with(key, '20111111112/') ORDER BY key COLLATE \"C\"",
            connection);
        var plan = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
            while (await reader.ReadAsync()) plan.Add(reader.GetString(0));

        var text = string.Join(Environment.NewLine, plan);
        Assert.Contains("key >= '20111111112/'", text);
        Assert.DoesNotContain("Sort", text);
    }

    private static async Task ExecuteAsync(NpgsqlDataSource db, string sql)
    {
        await using var command = db.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource db, string sql)
    {
        await using var command = db.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }
}
