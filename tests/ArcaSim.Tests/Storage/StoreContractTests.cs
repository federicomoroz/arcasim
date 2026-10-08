using ArcaSim.Application;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.InMemory;
using ArcaSim.Infrastructure.Postgres;
using ArcaSim.Tests.Support;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArcaSim.Tests.Storage;

/// <summary>
/// What every store has to do, run against each provider so they cannot drift apart. The facts are
/// <see cref="DockerFactAttribute"/>: the in-memory run never skips, and the PostgreSQL one skips without Docker.
/// </summary>
public abstract class StoreContractTests
{
    protected abstract Task<ISimulatorStore> CreateAsync();

    [DockerFact]
    public async Task Taxpayers_keep_their_points_of_sale_and_state()
    {
        var store = await CreateAsync();
        var taxpayer = new Taxpayer(20111111112, "Empresa", VatCondition.ResponsableInscripto,
            [new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(2, PointOfSaleKind.WebServiceCaea, true, new DateOnly(2026, 1, 31))]);
        taxpayer.Update("Empresa SA", VatCondition.ResponsableInscripto, active: false);

        await store.SaveAsync(taxpayer);
        var found = await store.FindAsync(20111111112);

        Assert.NotNull(found);
        Assert.Equal("Empresa SA", found.Name);
        Assert.False(found.Active);
        Assert.Equal(new PointOfSale(2, PointOfSaleKind.WebServiceCaea, true, new DateOnly(2026, 1, 31)), found.FindPointOfSale(2));
        Assert.Null(await store.FindAsync(30000000007));
    }

    [DockerFact]
    public async Task A_taxpayer_changed_after_reading_it_changes_nothing_until_saved()
    {
        var store = await CreateAsync();
        await store.SaveAsync(new Taxpayer(20111111112, "Empresa", VatCondition.ResponsableInscripto, [new PointOfSale(1, PointOfSaleKind.WebServiceCae)]));

        var read = await store.FindAsync(20111111112);
        read!.AddPointOfSale(new PointOfSale(2, PointOfSaleKind.WebServiceCae));
        var unsaved = await store.FindAsync(20111111112);
        await store.SaveAsync(read);
        var saved = await store.FindAsync(20111111112);

        Assert.Null(unsaved!.FindPointOfSale(2));
        Assert.NotNull(saved!.FindPointOfSale(2));
    }

    [DockerFact]
    public async Task Documents_list_in_ordinal_key_order_whatever_the_culture()
    {
        var store = await CreateAsync();
        foreach (var key in new[] { "b", "B", "a1", "a-1", "A" }) await store.PutAsync("orden", key, new Liquidation(1, key, []));

        var keys = (await store.ListAsync<Liquidation>("orden")).Select(d => d.State).ToList();

        Assert.Equal(["A", "B", "a-1", "a1", "b"], keys);
    }

    private sealed record Liquidation(long Coe, string State, List<int> Items);

    [DockerFact]
    public async Task Documents_are_kept_as_written_listed_by_key_prefix_and_counted()
    {
        var store = await CreateAsync();
        await store.PutAsync("wslpg", "20111111112/0002", new Liquidation(2, "AC", [1, 2]));
        await store.PutAsync("wslpg", "20111111112/0001", new Liquidation(1, "AC", [1]));
        await store.PutAsync("wslpg", "20111111112/0001", new Liquidation(1, "AN", [1]));
        await store.PutAsync("wslpg", "30000000007/0001", new Liquidation(9, "AC", []));
        await store.PutAsync("wscpe", "20111111112/0001", new Liquidation(5, "AC", []));

        var one = await store.GetAsync<Liquidation>("wslpg", "20111111112/0001");
        var mine = await store.ListAsync<Liquidation>("wslpg", "20111111112/");
        var deleted = await store.DeleteAsync("wslpg", "30000000007/0001");
        var first = await store.NextAsync("wslpg.coe");
        var second = await store.NextAsync("wslpg.coe");

        Assert.Equal("AN", one!.State);
        Assert.Equal([1L, 2L], mine.Select(l => l.Coe));
        Assert.True(deleted);
        Assert.Null(await store.GetAsync<Liquidation>("wslpg", "30000000007/0001"));
        Assert.Equal((1L, 2L), (first, second));
    }

    [DockerFact]
    public async Task Authorizations_are_found_by_alias_and_service_regardless_of_case()
    {
        var store = await CreateAsync();
        await store.SaveAuthorizationAsync(new ServiceAuthorization(20111111112, "facturacion", 20111111112, "wsfe"));
        await store.SaveAuthorizationAsync(new ServiceAuthorization(20111111112, "facturacion", 20111111112, "wsfe"));

        var found = await store.AuthorizationsForAsync(20111111112, "FACTURACION", "WSFE");

        Assert.Single(found);
        Assert.Single(await store.ListAuthorizationsAsync());
    }

    [DockerFact]
    public async Task Vouchers_keep_the_detail_sent_and_are_found_by_any_number_of_their_range()
    {
        var store = await CreateAsync();
        var detail = new FECAEDetRequest
        {
            Concepto = 1, DocTipo = 99, CbteDesde = 10, CbteHasta = 12, ImpTotal = 121.5, MonId = "PES", MonCotiz = 1, MonCotizSpecified = true,
            Iva = [new AlicIva { Id = 5, BaseImp = 100, Importe = 21.5 }],
        };
        var processed = TestTime.Reference;
        await store.AddAsync(new StoredVoucher(20111111112, 1, 6, 10, 12, new DateOnly(2026, 10, 1), EmissionType.Cae,
            "12345678901234", new DateOnly(2026, 10, 11), processed, detail, [new Obs { Code = 10245, Msg = "texto" }]));

        var last = await store.LastAsync(20111111112, 1, 6);
        var middle = await store.FindAsync(20111111112, 1, 6, 11);

        Assert.Equal(12, last!.To);
        Assert.NotNull(middle);
        Assert.Equal(121.5, middle.Detail.ImpTotal);
        Assert.True(middle.Detail.MonCotizSpecified);
        Assert.Equal(21.5, middle.Detail.Iva![0].Importe);
        Assert.Equal(10245, Assert.Single(middle.Observations).Code);
        Assert.Equal(processed.UtcDateTime, middle.ProcessedAt.UtcDateTime);
        Assert.Null(await store.FindAsync(20111111112, 1, 6, 13));
        Assert.Null(await store.LastAsync(20111111112, 1, 1));
    }

    [DockerFact]
    public async Task Vouchers_of_some_types_are_listed_newest_first_without_any_other_type()
    {
        var store = await CreateAsync();
        await store.AddAsync(Stored(6, 1, 1, TestTime.Reference));
        await store.AddAsync(Stored(201, 1, 1, TestTime.Reference.AddMinutes(1)));
        await store.AddAsync(Stored(203, 1, 1, TestTime.Reference.AddMinutes(3)));
        await store.AddAsync(Stored(201, 2, 2, TestTime.Reference.AddMinutes(2)));
        await store.AddAsync(Stored(6, 2, 2, TestTime.Reference.AddMinutes(4)));

        var listed = await store.ListOfTypesAsync([201, 203]);

        Assert.Equal([(203, 1L), (201, 2L), (201, 1L)], listed.Select(v => (v.VoucherType, v.From)));
    }

    private static StoredVoucher Stored(int type, long from, long to, DateTimeOffset processed) => new(
        20111111112, 1, type, from, to, new DateOnly(2026, 10, 1), EmissionType.Cae, "12345678901234", new DateOnly(2026, 10, 11),
        processed, new FECAEDetRequest { CbteDesde = from, CbteHasta = to }, []);

    [DockerFact]
    public async Task Exchange_rates_answer_with_the_last_day_on_or_before_the_one_asked()
    {
        var store = await CreateAsync();
        await store.SetAsync("DOL", new DateOnly(2026, 9, 29), 1380m);
        await store.SetAsync("DOL", new DateOnly(2026, 9, 30), 1385.5m);

        Assert.Equal((1385.5m, new DateOnly(2026, 9, 30)), await store.RateAsync("DOL", new DateOnly(2026, 10, 3)));
        Assert.Equal((1380m, new DateOnly(2026, 9, 29)), await store.RateAsync("DOL", new DateOnly(2026, 9, 29)));
        Assert.Null(await store.RateAsync("DOL", new DateOnly(2026, 9, 1)));
    }

    [DockerFact]
    public async Task Reset_leaves_nothing_behind()
    {
        var store = await CreateAsync();
        await store.SaveAsync(new Taxpayer(20111111112, "Empresa", VatCondition.Monotributo));
        await store.AddAsync(new IssuedCaea(20111111112, 202610, 1, "12345678901234", new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15), TestTime.Reference));

        await store.ResetAsync();

        Assert.Empty(await store.ListAsync());
        Assert.Null(await store.FindByCodeAsync("12345678901234"));
    }
}

public class InMemoryStoreTests : StoreContractTests
{
    protected override Task<ISimulatorStore> CreateAsync() => Task.FromResult<ISimulatorStore>(new InMemoryStore());
}

/// <summary>One PostgreSQL container for the whole run; each test gets a database of its own.</summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public async Task<string> NewDatabaseAsync()
    {
        var container = _container ?? throw new InvalidOperationException("The PostgreSQL container was not started.");
        var name = $"arcasim_{Guid.NewGuid():N}";
        await using (var admin = NpgsqlDataSource.Create(container.GetConnectionString()))
        await using (var command = admin.CreateCommand($"CREATE DATABASE {name}"))
            await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        // Without Docker (and not required) every test that needs it is skipped before it runs: there is nothing to start.
        if (DockerAvailability.SkipReason is not null) return;
        _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await _container.StartAsync();
    }

    public Task DisposeAsync() => _container is null ? Task.CompletedTask : _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresContainer>
{
    public const string Name = "PostgreSQL";
}

[Collection(PostgresCollection.Name)]
[RequiresDocker]
public class PostgresStoreTests(PostgresContainer postgres) : StoreContractTests
{
    protected override async Task<ISimulatorStore> CreateAsync()
    {
        var store = new PostgresStore(NpgsqlDataSource.Create(await postgres.NewDatabaseAsync()));
        await store.EnsureSchemaAsync();
        return store;
    }
}
