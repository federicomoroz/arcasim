using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>What WSASS does at ARCA: certificates with ARCA's DN, issued by ArcaSim's own authority.</summary>
[ApiController]
[Route(AdminRoutes.Prefix)]
public sealed class CertificatesController(KeyMaterial keys, IAccessRepository access, IClock clock) : ControllerBase
{
    [HttpGet("ca")]
    public ContentResult Authority() => Content(keys.AuthorityPem, "application/x-pem-file");

    /// <summary>With a CSR, the signed certificate as PEM; without one, a PFX with a new key. The alias is authorized for the services given (wsfe by default).</summary>
    [HttpPost("certificates")]
    public async Task<IActionResult> Issue(CertificateBody body, CancellationToken ct)
    {
        if (!Cuits.IsValid(body.Cuit)) return BadRequest(new ErrorView($"El CUIT {body.Cuit} tiene mal el dígito verificador."));
        if (string.IsNullOrWhiteSpace(body.Alias)) return BadRequest(new ErrorView("Falta el alias."));

        await access.SaveAliasAsync(new ClientAlias(body.Cuit, body.Alias), ct);
        foreach (var service in body.Services ?? [WsfeService.Name])
            await access.SaveAuthorizationAsync(new ServiceAuthorization(body.Cuit, body.Alias, body.Cuit, service), ct);

        if (!string.IsNullOrWhiteSpace(body.Csr))
            return Ok(new { certificate = keys.IssueFromCsr(body.Csr, body.Cuit, body.Alias, clock.Now) });
        var pfx = keys.IssueWithKey(body.Cuit, body.Alias, body.Password ?? "", clock.Now);
        return File(pfx, "application/x-pkcs12", $"arcasim-{body.Cuit}-{body.Alias}.pfx");
    }
}
