using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

[ApiController]
[Route(AdminRoutes.Prefix + "/taxpayers")]
public sealed class TaxpayersController(ITaxpayerRepository taxpayers) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<TaxpayerView>> List(CancellationToken ct) => (await taxpayers.ListAsync(ct)).Select(TaxpayerView.Of);

    [HttpGet("{cuit:long}")]
    public async Task<ActionResult<TaxpayerView>> Get(long cuit, CancellationToken ct) =>
        await taxpayers.FindAsync(cuit, ct) is { } taxpayer ? TaxpayerView.Of(taxpayer) : NotFound();

    /// <summary>Creates or updates a taxpayer; the points of sale sent are added or replaced, and a profile sent replaces the old one.</summary>
    [HttpPut("{cuit:long}")]
    public async Task<ActionResult<TaxpayerView>> Put(long cuit, TaxpayerBody body, CancellationToken ct)
    {
        if (!Cuits.IsValid(cuit)) return BadRequest(new ErrorView($"El CUIT {cuit} tiene mal el dígito verificador."));
        var points = (body.PointsOfSale ?? []).Select(p => new PointOfSale(p.Number, p.Kind, p.Blocked, p.DeactivatedOn));
        var taxpayer = await taxpayers.FindAsync(cuit, ct);
        if (taxpayer is null) taxpayer = new Taxpayer(cuit, body.Name, body.VatCondition, points);
        else foreach (var point in points) taxpayer.AddPointOfSale(point);
        taxpayer.Update(body.Name, body.VatCondition, body.Active);
        if (body.Profile is not null) taxpayer.SetProfile(body.Profile);
        await taxpayers.SaveAsync(taxpayer, ct);
        return TaxpayerView.Of(taxpayer);
    }
}
