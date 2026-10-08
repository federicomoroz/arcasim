using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

[ApiController]
[Route(AdminRoutes.Prefix + "/vouchers")]
public sealed class VouchersController(IVoucherStore vouchers) : ControllerBase
{
    /// <summary>A negative limit answers 400 on both stores, rather than an empty list in memory and an error on PostgreSQL.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VoucherView>>> List(long? cuit, int? limit, CancellationToken ct) =>
        limit is < 0
            ? BadRequest(new ErrorView("El límite no puede ser negativo."))
            : Ok((await vouchers.ListAsync(cuit, limit ?? 100, ct)).Select(VoucherView.Of));
}
