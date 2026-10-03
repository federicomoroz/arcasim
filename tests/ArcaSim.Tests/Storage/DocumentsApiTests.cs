using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ArcaSim.Tests.Storage;

/// <summary>The admin API over the services' documents: what an operator preloads is what the rules read.</summary>
public class DocumentsApiTests
{
    [Fact]
    public async Task A_document_is_put_listed_read_and_deleted_with_a_key_that_holds_slashes()
    {
        await using var sim = ArcaSimHarness.Start();
        const string url = "/arcasim/api/documents/sud_restricciones.deudas/20111111112/301";

        (await sim.Http.PutAsJsonAsync(url, new { impuesto = 301, periodo = "202609" })).EnsureSuccessStatusCode();

        var read = await sim.Http.GetFromJsonAsync<JsonObject>(url);
        var listed = await sim.Http.GetFromJsonAsync<JsonArray>("/arcasim/api/documents/sud_restricciones.deudas?prefix=20111111112/");
        Assert.Equal(301, (int)read!["impuesto"]!);
        Assert.Single(listed!);

        Assert.Equal(HttpStatusCode.NoContent, (await sim.Http.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sim.Http.GetAsync(url)).StatusCode);
    }
}
