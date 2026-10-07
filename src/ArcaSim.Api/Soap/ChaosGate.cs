using ArcaSim.Application;

namespace ArcaSim.Api.Soap;

/// <summary>The failures the admin API switches on for a service, applied by each endpoint before the service runs.</summary>
public static class ChaosGate
{
    /// <summary>
    /// Waits the service's delay and, when the service is down, answers what a
    /// load balancer with nothing behind it answers: HTTP 503 and no body.
    /// True when the request was answered that way.
    /// </summary>
    public static async Task<bool> RefusedAsync(HttpContext context, ServiceChaos chaos)
    {
        if (chaos.Delay > TimeSpan.Zero) await Task.Delay(chaos.Delay, context.RequestAborted);
        if (!chaos.Down) return false;
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return true;
    }
}
