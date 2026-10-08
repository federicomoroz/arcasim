using Arca.Client;
using ArcaSim.Application;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests;

/// <summary>
/// Requests that arrive together, against a store slow enough to let them
/// overlap between reading and writing: what ARCA allows once happens once.
/// </summary>
public class ConcurrencyTests
{
    private const long Cuit = ArcaSimHarness.Issuer;
    private const int AtOnce = 6;

    [Fact]
    public async Task With_the_window_on_logins_at_once_get_one_ticket()
    {
        await using var sim = ArcaSimHarness.Start(services: s =>
            s.AddSingleton<ITicketLog>(sp => new SlowTicketLog(sp.GetRequiredService<InMemoryStore>())));
        sim.Settings.ReplayWindowEnabled = true;
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");

        var refusals = await Task.WhenAll(Enumerable.Range(0, AtOnce).Select(_ => RefusalAsync(sim.Wsaa(Cuit, certificate))));

        Assert.Single(refusals, code => code is null);
        Assert.All(refusals.Where(code => code is not null), code => Assert.Equal("coe.alreadyAuthenticated", code));
    }

    [Fact]
    public async Task CAEA_requests_at_once_get_one_CAEA()
    {
        await using var sim = ArcaSimHarness.Start(services: s =>
            s.AddSingleton<ICaeaStore>(sp => new SlowCaeaStore(sp.GetRequiredService<InMemoryStore>())));
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(Cuit, "Empresa de Prueba SA", VatCondition.ResponsableInscripto,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(900, PointOfSaleKind.WebServiceCaea));
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");
        var auth = ArcaSimHarness.AuthXml(await sim.Wsaa(Cuit, certificate).LoginAsync("wsfe"), Cuit);

        var answers = await Task.WhenAll(Enumerable.Range(0, AtOnce)
            .Select(_ => sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>")));

        Assert.Single(answers, answer => answer.Body.Contains("<CAEA>", StringComparison.Ordinal));
        Assert.Equal(AtOnce - 1, answers.Count(answer => answer.Body.Contains("<Code>15008</Code>", StringComparison.Ordinal)));
    }

    private static async Task<string?> RefusalAsync(WsaaClient client)
    {
        try
        {
            await client.LoginAsync("wsfe");
            return null;
        }
        catch (WsaaFaultException failure)
        {
            return failure.Code;
        }
    }

    /// <summary>Reads late, the way a database far away would: every request reads before any writes.</summary>
    private sealed class SlowTicketLog(ITicketLog inner) : ITicketLog
    {
        public async Task<IssuedTicket?> LatestAsync(string clientDn, string service, CancellationToken ct = default)
        {
            var latest = await inner.LatestAsync(clientDn, service, ct);
            await Task.Delay(100, ct);
            return latest;
        }

        public Task AddAsync(IssuedTicket ticket, CancellationToken ct = default) => inner.AddAsync(ticket, ct);
    }

    private sealed class SlowCaeaStore(ICaeaStore inner) : ICaeaStore
    {
        public async Task<IssuedCaea?> FindAsync(long cuit, int period, short fortnight, CancellationToken ct = default)
        {
            var found = await inner.FindAsync(cuit, period, fortnight, ct);
            await Task.Delay(100, ct);
            return found;
        }

        public Task<IssuedCaea?> FindByCodeAsync(string code, CancellationToken ct = default) => inner.FindByCodeAsync(code, ct);

        public Task AddAsync(IssuedCaea caea, CancellationToken ct = default) => inner.AddAsync(caea, ct);

        public Task<IReadOnlyList<CaeaWithoutMovement>> WithoutMovementAsync(long cuit, string caea, CancellationToken ct = default) =>
            inner.WithoutMovementAsync(cuit, caea, ct);

        public Task AddWithoutMovementAsync(CaeaWithoutMovement report, CancellationToken ct = default) => inner.AddWithoutMovementAsync(report, ct);
    }
}
