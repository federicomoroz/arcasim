using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

[ApiController]
[Route(AdminRoutes.Prefix + "/rates")]
public sealed class RatesController(IExchangeRates rates) : ControllerBase
{
    [HttpPut]
    public async Task<RateBody> Set(RateBody body, CancellationToken ct)
    {
        await rates.SetAsync(body.Currency, body.Day, body.Rate, ct);
        return body;
    }
}
