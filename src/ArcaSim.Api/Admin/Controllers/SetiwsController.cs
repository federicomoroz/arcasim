using ArcaSim.Application.Setiws;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>
/// What SETIWS-PAGO-API has no endpoint for: the payment entity reporting that
/// a VEP was paid. After it, the VEP's GET carries the CP and no longer offers QR.
/// </summary>
[ApiController]
[Route(AdminRoutes.Prefix + "/setiws/veps")]
public sealed class SetiwsController(VepService veps) : ControllerBase
{
    /// <param name="body">TIPO_SUCURSAL (8, HomeBanking, by default), FORMA_PAGO (91, dinero en cuenta) and the paying bank.</param>
    [HttpPost("{number:long}/payment")]
    public async Task<IActionResult> Pay(long number, VepPaymentBody? body, CancellationToken ct)
    {
        var branchType = body?.BranchType ?? 8;
        var paymentForm = body?.PaymentForm ?? 91;
        if (!VepService.BranchTypes.Contains(branchType)) return BadRequest(new { error = $"Unknown TIPO_SUCURSAL {branchType}." });
        if (!VepService.PaymentForms.Contains(paymentForm)) return BadRequest(new { error = $"Unknown FORMA_PAGO {paymentForm}." });

        var paid = await veps.PayAsync(number, branchType, paymentForm, body?.Bank ?? 11, ct);
        return paid is null ? NotFound(new { error = $"VEP {number} does not exist." }) : Ok(paid);
    }
}

public sealed record VepPaymentBody(int? BranchType, int? PaymentForm, int? Bank);
