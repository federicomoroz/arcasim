using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

[ApiController]
[Route(AdminRoutes.Prefix + "/clock")]
public sealed class ClockController(SimulationSettings settings, SimulatedClock clock) : ControllerBase
{
    /// <summary>Freezes the clock at a moment, moves it forward, or both.</summary>
    [HttpPost]
    public StatusView Move(ClockBody body)
    {
        if (body.FreezeAt is { } at) clock.Freeze(at);
        if (body.AdvanceMinutes is { } minutes) clock.Advance(TimeSpan.FromMinutes(minutes));
        return StatusView.Of(settings, clock);
    }

    [HttpDelete]
    public StatusView Release()
    {
        clock.Reset();
        return StatusView.Of(settings, clock);
    }
}
