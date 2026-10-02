using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

[ApiController]
[Route(AdminRoutes.Prefix + "/authorizations")]
public sealed class AuthorizationsController(IAccessRepository access) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ServiceAuthorization>> List(CancellationToken ct) => access.ListAuthorizationsAsync(ct);

    [HttpPost]
    public async Task<ServiceAuthorization> Add(ServiceAuthorization authorization, CancellationToken ct)
    {
        await access.SaveAuthorizationAsync(authorization, ct);
        return authorization;
    }

    [HttpDelete]
    public async Task<NoContentResult> Remove(long clientCuit, string alias, long representedCuit, string service, CancellationToken ct)
    {
        await access.DeleteAuthorizationAsync(new ServiceAuthorization(clientCuit, alias, representedCuit, service), ct);
        return NoContent();
    }
}
