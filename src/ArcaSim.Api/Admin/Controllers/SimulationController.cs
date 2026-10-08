using ArcaSim.Application;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>The environment, the manual version, the anti-replay window, open or strict access, and the reset.</summary>
[ApiController]
[Route(AdminRoutes.Prefix)]
public sealed class SimulationController(SimulationSettings settings, SimulatedClock clock) : ControllerBase
{
    [HttpGet("status")]
    public StatusView Status() => StatusView.Of(settings, clock);

    [HttpPut("settings")]
    public StatusView Update(SettingsBody body)
    {
        if (body.Environment is { } environment) settings.Environment = environment;
        if (body.FollowCalendar == true) settings.ManualVersionOverride = null;
        else if (body.ManualVersion is { } version) settings.ManualVersionOverride = version;
        if (body.ReplayWindowEnabled is { } replay) settings.ReplayWindowEnabled = replay;
        if (body.OpenAccess is { } open) settings.OpenAccess = open;
        if (body.FinalConsumerIdentificationThreshold is { } threshold) settings.FinalConsumerIdentificationThreshold = threshold;
        if (body.MaxRecordsPerRequest is { } max) settings.MaxRecordsPerRequest = max;
        if (body.CaeLifetimeDays is { } days) settings.CaeLifetimeDays = days;
        return StatusView.Of(settings, clock);
    }

    /// <summary>Back to nothing: no taxpayers, vouchers, failures, limits, activity or numbered sequences, and the real time.</summary>
    [HttpPost("reset")]
    public async Task<StatusView> Reset(
        [FromServices] IEnumerable<IResettable> stores,
        [FromServices] TrafficGate traffic,
        [FromServices] TrafficMeter meter,
        [FromServices] ActivityLog activity,
        [FromServices] PlaceholderCounters sequences,
        CancellationToken ct)
    {
        foreach (var store in stores) await store.ResetAsync(ct);
        traffic.Reset();
        meter.Reset();
        activity.Reset();
        sequences.Reset();
        settings.ResetChaos();
        clock.Reset();
        return StatusView.Of(settings, clock);
    }
}
