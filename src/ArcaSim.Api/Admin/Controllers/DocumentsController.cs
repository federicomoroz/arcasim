using System.Text.Json.Nodes;
using ArcaSim.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>
/// The state the services answered through their WSDL keep, by collection and
/// key: read what a service stored, or load what nobody can write through
/// ARCA's API (debts, apócrifos, a Ventanilla inbox, wsagr's ratings) before a
/// test. Keys may hold '/', as the vouchers' do.
/// </summary>
[ApiController]
[Route(AdminRoutes.Prefix + "/documents/{collection}")]
public sealed class DocumentsController(IDocumentStore documents) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<JsonNode>> List(string collection, [FromQuery] string? prefix, CancellationToken ct) =>
        documents.ListAsync<JsonNode>(collection, prefix ?? "", ct);

    [HttpGet("{*key}")]
    public async Task<IActionResult> Get(string collection, string key, CancellationToken ct) =>
        await documents.GetAsync<JsonNode>(collection, key, ct) is { } document ? Ok(document) : NotFound();

    [HttpPut("{*key}")]
    public async Task<JsonNode> Put(string collection, string key, JsonNode document, CancellationToken ct)
    {
        await documents.PutAsync(collection, key, document, ct);
        return document;
    }

    [HttpDelete("{*key}")]
    public async Task<IActionResult> Delete(string collection, string key, CancellationToken ct) =>
        await documents.DeleteAsync(collection, key, ct) ? NoContent() : NotFound();
}
