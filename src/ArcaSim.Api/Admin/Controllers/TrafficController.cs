using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>Saturation and bottlenecks on ARCA's endpoints, and the meter of the last minute.</summary>
[ApiController]
[Route(AdminRoutes.Prefix + "/traffic")]
public sealed class TrafficController(TrafficGate gate, TrafficMeter meter, ServiceDirectory directory) : ControllerBase
{
    [HttpGet]
    public IEnumerable<TrafficView> Meter() => meter.Snapshot().Select(TrafficView.Of);

    /// <summary>A name ArcaSim does not serve answers 404: limits nobody's requests go through would never apply.</summary>
    [HttpPut("{service}")]
    public ActionResult<TrafficLimits> Limit(string service, TrafficLimits limits)
    {
        if (!directory.Serves(service)) return NotFound(new ErrorView($"ArcaSim no sirve el servicio {service}."));
        if (limits.RequestsPerMinute < 0 || limits.Capacity < 0 || limits.ServiceTimeMilliseconds < 0 || limits.QueueLimit < 0)
            return BadRequest(new ErrorView("Los límites no pueden ser negativos."));
        gate.SetLimits(service, limits);
        return limits;
    }
}
