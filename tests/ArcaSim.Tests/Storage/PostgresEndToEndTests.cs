using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Storage;

[Collection(PostgresCollection.Name)]
[RequiresDocker]
public class PostgresEndToEndTests(PostgresContainer postgres)
{
    [DockerFact]
    public async Task On_PostgreSQL_an_invoice_is_authorized_and_found_again_after_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync(postgres: database);
        var issued = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice());
        await sim.DisposeAsync();

        var (restarted, again) = await ArcaSimHarness.StartWithIssuerAsync(postgres: database);
        await using var _ = restarted;
        var found = await again.QueryAsync(1, 6, issued.Number);

        Assert.True(issued.Approved);
        Assert.Equal(issued.Cae, found?.AuthorizationCode);
        Assert.Equal(1, await again.LastAuthorizedAsync(1, 6));
    }
}
