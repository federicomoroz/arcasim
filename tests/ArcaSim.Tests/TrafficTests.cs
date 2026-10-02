using System.Net.Http.Json;
using System.Text.Json;
using Arca.Client;

namespace ArcaSim.Tests;

/// <summary>Saturation and bottlenecks on ARCA's endpoints, and the numbers the meter shows.</summary>
public class TrafficTests
{
    [Fact]
    public async Task Past_the_rate_limit_requests_get_a_503_and_the_meter_counts_them()
    {
        await using var sim = ArcaSimHarness.Start();
        await SetLimitsAsync(sim, "wsfe", new { requestsPerMinute = 2 });

        var statuses = new List<int>();
        for (var i = 0; i < 3; i++) statuses.Add((await sim.PostWsfeAsync("FEDummy", "")).Status);
        var wsfe = await TrafficOfAsync(sim, "wsfe");

        Assert.Equal([200, 200, 503], statuses);
        Assert.Equal(3, wsfe.GetProperty("lastMinute").GetProperty("requests").GetInt32());
        Assert.Equal(1, wsfe.GetProperty("lastMinute").GetProperty("refused").GetInt32());
        Assert.Equal(33.3, Math.Round(wsfe.GetProperty("saturationPercent").GetDouble(), 1));
    }

    [Fact]
    public async Task With_one_slot_and_one_place_in_the_queue_the_third_concurrent_request_is_refused()
    {
        await using var sim = ArcaSimHarness.Start();
        await SetLimitsAsync(sim, "wsfe", new { capacity = 1, serviceTimeMilliseconds = 300, queueLimit = 1 });

        var calls = Enumerable.Range(0, 3).Select(_ => sim.PostWsfeAsync("FEDummy", "")).ToList();
        var statuses = (await Task.WhenAll(calls)).Select(r => r.Status).Order().ToList();
        var wsfe = await TrafficOfAsync(sim, "wsfe");

        Assert.Equal([200, 200, 503], statuses);
        // The queued one waited for the first to finish: its time includes the first one's service time.
        Assert.True(wsfe.GetProperty("lastMinute").GetProperty("p95Milliseconds").GetDouble() >= 550);
    }

    [Fact]
    public async Task For_the_client_a_saturated_ARCA_is_a_failure_worth_retrying()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        await wsfe.LastAuthorizedAsync(1, 6);
        await SetLimitsAsync(sim, "wsfe", new { requestsPerMinute = 1 });

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => wsfe.LastAuthorizedAsync(1, 6));

        Assert.True(failure.Retryable);
        Assert.Contains("503", failure.Message);
    }

    private static async Task SetLimitsAsync(ArcaSimHarness sim, string service, object limits) =>
        (await sim.Http.PutAsJsonAsync($"/arcasim/api/traffic/{service}", limits)).EnsureSuccessStatusCode();

    private static async Task<JsonElement> TrafficOfAsync(ArcaSimHarness sim, string service) =>
        (await sim.Http.GetFromJsonAsync<JsonElement[]>("/arcasim/api/traffic"))!
        .Single(t => t.GetProperty("service").GetString() == service);
}

/// <summary>The live log is fed by the event bus: whoever acts publishes, the log only listens.</summary>
public class ActivityTests
{
    [Fact]
    public async Task The_activity_log_tells_the_ticket_the_CAE_and_the_rejection()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var approved = await wsfe.AuthorizeNextAsync(1, 6, new Voucher
        {
            Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 121, Net = 100, Vat = 21,
            ReceiverVatCondition = 5, VatLines = [new VatLine(5, 100, 21)],
        });
        await wsfe.AuthorizeNextAsync(1, 6, new Voucher { Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 999, Net = 100, Vat = 21, ReceiverVatCondition = 5, VatLines = [new VatLine(5, 100, 21)] });
        var log = await sim.Http.GetFromJsonAsync<JsonElement[]>("/arcasim/api/activity");
        var texts = log!.Select(e => e.GetProperty("text").GetString()!).ToList();

        Assert.Contains(texts, t => t.StartsWith("Ticket para wsfe"));
        Assert.Contains(texts, t => t.EndsWith($"→ CAE {approved.Cae}"));
        Assert.Contains(texts, t => t.Contains("2 rechazado (10048)"));
        Assert.Contains("rechazado", texts[0]);
    }
}
