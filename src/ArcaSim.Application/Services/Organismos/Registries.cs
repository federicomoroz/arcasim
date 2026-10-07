using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>
/// The registries ARCA keeps and nobody writes through its web services (debts,
/// apócrifos, Ventanilla Electrónica, bank accounts...). Their documents live
/// in IDocumentStore and are seeded once per scope with plainly fictitious
/// defaults. A test or an operator that wants its own data puts documents in
/// the same collection: the seed never overwrites one, and SkipSeedAsync
/// leaves a scope with only what was preloaded.
/// </summary>
public static class Registries
{
    private const string Seeds = "organismos.seeds";

    internal sealed record SeedMark(DateTimeOffset At);

    public static async Task SeedAsync<T>(
        this IDocumentStore store, string scope, string collection, IEnumerable<(string Key, T Document)> defaults, CancellationToken ct)
        where T : class
    {
        if (await store.GetAsync<SeedMark>(Seeds, scope, ct) is not null) return;
        foreach (var (key, document) in defaults)
            if (await store.GetAsync<T>(collection, key, ct) is null)
                await store.PutAsync(collection, key, document, ct);
        await store.PutAsync(Seeds, scope, new SeedMark(DateTimeOffset.UtcNow), ct);
    }

    public static async Task<bool> IsSeededAsync(this IDocumentStore store, string scope, CancellationToken ct = default) =>
        await store.GetAsync<SeedMark>(Seeds, scope, ct) is not null;

    /// <summary>Marks a scope as seeded without writing its defaults, so it holds only what was preloaded.</summary>
    public static Task SkipSeedAsync(this IDocumentStore store, string scope, CancellationToken ct = default) =>
        store.PutAsync(Seeds, scope, new SeedMark(DateTimeOffset.UtcNow), ct);
}
