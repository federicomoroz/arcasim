using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>Failures on demand: a service down, slow, rejecting the next voucher, dropping the next answer, or behind fwshomo's F5 mask.</summary>
[ApiController]
[Route(AdminRoutes.Prefix + "/chaos")]
public sealed class ChaosController(SimulationSettings settings, SimulatedClock clock) : ControllerBase
{
    [HttpPut("{service}")]
    public StatusView Set(string service, ChaosBody body)
    {
        var chaos = settings.ChaosFor(service);
        if (body.Down is { } down) chaos.Down = down;
        if (body.DelayMilliseconds is { } delay) chaos.Delay = TimeSpan.FromMilliseconds(delay);
        if (body.DropNextResponse is { } drop) chaos.DropNextResponse = drop;
        if (body.ForceRejection is { } code) chaos.ForceNextRejection(code);
        if (body.BalancerMask is { } mask) chaos.BalancerMask = mask;
        return StatusView.Of(settings, clock);
    }
}
