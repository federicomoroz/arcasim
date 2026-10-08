using System.Net;
using System.Net.Http.Json;

namespace ArcaSim.Tests;

/// <summary>The admin API refuses what it cannot do before it changes anything, the same on both stores.</summary>
public class AdminApiTests
{
    [Fact]
    public async Task A_CSR_that_does_not_read_is_400_and_leaves_no_authorization_behind()
    {
        await using var sim = ArcaSimHarness.Start();

        var response = await sim.Http.PostAsJsonAsync("/arcasim/api/certificates", new
        {
            cuit = ArcaSimHarness.Issuer,
            alias = "roto",
            csr = "-----BEGIN CERTIFICATE REQUEST-----\nesto no es un CSR\n-----END CERTIFICATE REQUEST-----",
        });
        var authorizations = await sim.Http.GetStringAsync("/arcasim/api/authorizations");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CSR", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("roto", authorizations);
    }

    [Fact]
    public async Task A_negative_voucher_limit_is_400()
    {
        await using var sim = ArcaSimHarness.Start();

        var response = await sim.Http.GetAsync("/arcasim/api/vouchers?limit=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
