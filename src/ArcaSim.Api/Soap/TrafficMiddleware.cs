using ArcaSim.Application.Traffic;

namespace ArcaSim.Api.Soap;

/// <summary>
/// Puts the traffic gate in front of ARCA's endpoints. A refused request gets
/// what a saturated load balancer answers, HTTP 503 with no SOAP inside: the
/// client sees ARCA unavailable and has to retry. The WSDL pages are not gated.
/// </summary>
public static class TrafficMiddleware
{
    public static string? ServiceOf(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) ? ServiceRoutes.ServiceOf(request.Path) : null;

    public static void Use(WebApplication app) => app.Use(async (context, next) =>
    {
        var service = ServiceOf(context.Request);
        if (service is null)
        {
            await next(context);
            return;
        }

        var gate = context.RequestServices.GetRequiredService<TrafficGate>();
        await using var admission = await gate.EnterAsync(service, context.RequestAborted);
        if (admission is null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "1";
            await context.Response.WriteAsync("Service Unavailable");
            return;
        }

        await admission.ServeAsync(context.RequestAborted);
        await next(context);
    });
}
