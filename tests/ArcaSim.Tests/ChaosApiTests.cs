using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ArcaSim.Tests;

/// <summary>The admin API's failures: only for services ArcaSim serves, listed while they are on, and applied to SETIWS too.</summary>
public class ChaosApiTests
{
    [Fact]
    public async Task A_service_ArcaSim_does_not_serve_is_404_for_failures_and_limits()
    {
        await using var sim = ArcaSimHarness.Start();

        var chaos = await sim.Http.PutAsJsonAsync("/arcasim/api/chaos/wsfev1", new { down = true });
        var traffic = await sim.Http.PutAsJsonAsync("/arcasim/api/traffic/wsfev1", new { capacity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, chaos.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, traffic.StatusCode);
        Assert.Contains("wsfev1", await chaos.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_status_lists_a_failure_while_it_is_on_and_requests_do_not_add_entries()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PostWsfeAsync("FEDummy", "");

        var calm = await StatusChaosAsync(sim);
        var on = await PutChaosAsync(sim, "wsfe", new { down = true });
        var off = await PutChaosAsync(sim, "wsfe", new { down = false });

        Assert.Empty(calm.EnumerateObject());
        Assert.True(on.GetProperty("wsfe").GetProperty("down").GetBoolean());
        Assert.Empty(off.EnumerateObject());
    }

    [Fact]
    public async Task SETIWS_down_answers_503_like_every_other_service()
    {
        await using var sim = ArcaSimHarness.Start();
        await PutChaosAsync(sim, "seti-setipago-api", new { down = true });

        var down = await sim.Http.GetAsync("/setiws-pago-api/api/v1/veps?owner-cuit=20111111112&nro-vep=1");
        var dummy = await sim.Http.GetAsync("/setiws-pago-api/dummy");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dummy.StatusCode);
    }

    private static async Task<JsonElement> PutChaosAsync(ArcaSimHarness sim, string service, object body)
    {
        var response = await sim.Http.PutAsJsonAsync($"/arcasim/api/chaos/{service}", body);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("chaos");
    }

    private static async Task<JsonElement> StatusChaosAsync(ArcaSimHarness sim) =>
        JsonDocument.Parse(await sim.Http.GetStringAsync("/arcasim/api/status")).RootElement.GetProperty("chaos");
}
