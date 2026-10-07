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

    /// <summary>
    /// One scope seeds at a time in each store: requests that reach an empty scope together would each find it
    /// empty and each write defaults of their own.
    /// </summary>
    private static readonly KeyedLocks<(IDocumentStore Store, string Scope)> Seeding = new();

    /// <summary>The mark that a scope was seeded. It holds nothing: its existence is the mark.</summary>
    internal sealed record SeedMark;

    /// <summary>Writes the defaults of a scope the first time it is asked for, except the documents that are already there.</summary>
    public static Task SeedAsync<T>(
        this IDocumentStore store, string scope, string collection, IEnumerable<(string Key, T Document)> defaults, CancellationToken ct)
        where T : class =>
        store.SeedAsync(scope, collection, _ => Task.FromResult(defaults), ct);

    /// <summary>
    /// The same, for defaults that need the store to be made (ids from a counter): they are built inside the
    /// scope's turn and only when the scope still needs them, so requests that arrive together spend no ids on
    /// seeds nobody keeps.
    /// </summary>
    public static async Task SeedAsync<T>(
        this IDocumentStore store, string scope, string collection, Func<CancellationToken, Task<IEnumerable<(string Key, T Document)>>> defaults,
        CancellationToken ct)
        where T : class
    {
        using var turn = await Seeding.AcquireAsync((store, scope), ct);
        if (await store.GetAsync<SeedMark>(Seeds, scope, ct) is not null) return;
        foreach (var (key, document) in await defaults(ct))
            if (await store.GetAsync<T>(collection, key, ct) is null)
                await store.PutAsync(collection, key, document, ct);
        await store.PutAsync(Seeds, scope, new SeedMark(), ct);
    }

    /// <summary>Marks a scope as seeded without writing its defaults, so it holds only what was preloaded.</summary>
    public static async Task SkipSeedAsync(this IDocumentStore store, string scope, CancellationToken ct = default)
    {
        using var turn = await Seeding.AcquireAsync((store, scope), ct);
        await store.PutAsync(Seeds, scope, new SeedMark(), ct);
    }
}
