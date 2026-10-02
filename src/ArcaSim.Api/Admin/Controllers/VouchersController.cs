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
    [HttpGet]
    public async Task<IEnumerable<VoucherView>> List(long? cuit, int? limit, CancellationToken ct) =>
        (await vouchers.ListAsync(cuit, limit ?? 100, ct)).Select(VoucherView.Of);
}
