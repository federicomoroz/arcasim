using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcaSim.Api.Soap;
using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Setiws;

namespace ArcaSim.Api.Rest;

/// <summary>
/// SETIWS-PAGO-API, the one REST service in ARCA's catalog: a gateway that
/// checks the WSAA ticket from headers before routing anything, then a Spring
/// Boot app that creates and finds VEPs (docs/arca/servicios/SETIWS-PAGO-API.md).
/// ARCA serves it at the root of its own host; ArcaSim serves it under Base,
/// so an application points its base URL there.
/// Where the manual and the OpenAPI disagree ArcaSim follows the manual: the
/// POST answers 201 with nroVEP, and the GET takes owner-cuit with nro-vep or
/// owner-transaction-id. It answers JSON only (the XML shapes are unverified),
/// and the Spring texts of the framework errors are ArcaSim's.
/// </summary>
public static class SetiwsEndpoint
{
    public const string Base = "/setiws-pago-api";
    private const string Veps = "api/v1/veps";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Map(WebApplication app)
    {
        app.Services.GetRequiredService<ServiceDirectory>().Route($"{Base}/{Veps}", SetiwsGateway.Service);
        app.MapGet($"{Base}/dummy", () => Results.Json(new { appserver = "OK", dbserver = "OK" }));
        app.Map(Base + "/{**path}", (HttpContext context, string? path) => AnswerAsync(context, path ?? ""));
    }

    private static async Task AnswerAsync(HttpContext context, string path)
    {
        var services = context.RequestServices;
        // The admin API's failures, as for every other service; the GET dummy stays up, as the ASMX ones do.
        if (await ChaosGate.RefusedAsync(context, services.GetRequiredService<SimulationSettings>().ChaosOf(SetiwsGateway.Service))) return;
        var headers = context.Request.Headers;
        var (represented, problems) = services.GetRequiredService<SetiwsGateway>().Check(
            headers.Authorization, headers[SetiwsGateway.TokenHeader], headers[SetiwsGateway.SignHeader], headers[SetiwsGateway.RepresentedHeader]);
        if (problems.Count > 0)
        {
            await GatewayRefusalAsync(context, problems, services.GetRequiredService<IClock>());
            return;
        }

        if (!string.Equals(path.TrimEnd('/'), Veps, StringComparison.OrdinalIgnoreCase))
        {
            await ErrorAsync(context, new VepError(404, "NoResourceFoundException", $"No static resource {path}."));
            return;
        }

        var operation = HttpMethods.IsPost(context.Request.Method) ? "createVep"
            : HttpMethods.IsGet(context.Request.Method) ? "findMyVEPByTransactionId" : null;
        VepError? error;
        try
        {
            error = operation switch
            {
                "createVep" => await CreateAsync(context, represented),
                "findMyVEPByTransactionId" => await FindAsync(context, represented),
                _ => new VepError(400, "HttpRequestMethodNotSupportedException", $"Request method '{context.Request.Method}' is not supported"),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            // What Spring Boot answers for an exception nobody handled: a 500 with the error body, never a bare one.
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SetiwsEndpoint)).LogError(ex, "SETIWS {Operation} failed", operation);
            error = new VepError(StatusCodes.Status500InternalServerError, ex.GetType().Name, ex.Message);
        }
        if (error is not null) await ErrorAsync(context, error);
        services.GetRequiredService<EventManager>().Publish(new ServiceCalled(DateTimeOffset.UtcNow, SetiwsGateway.Service,
            operation ?? context.Request.Method, represented, error is null ? "ok" : "error", error?.Message ?? ""));
    }

    private static async Task<VepError?> CreateAsync(HttpContext context, long represented)
    {
        if (context.Request.ContentType is not { } type || !type.Contains("json", StringComparison.OrdinalIgnoreCase))
            return new VepError(400, "HttpMediaTypeNotSupportedException", $"Content-Type '{context.Request.ContentType}' is not supported");

        EdpVep? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<EdpVep>(context.Request.Body, Json, context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return new VepError(400, "HttpMessageNotReadableException", $"JSON parse error: {ex.Message}");
        }
        if (request is null) return new VepError(400, "HttpMessageNotReadableException", "Required request body is missing");

        var (stored, error) = await context.RequestServices.GetRequiredService<VepService>().CreateAsync(request, represented, context.RequestAborted);
        if (error is not null) return error;

        var (qr, url) = VepService.PendingPayment(stored!, WithQr(context));
        context.Response.StatusCode = StatusCodes.Status201Created;
        await context.Response.WriteAsJsonAsync(new Created(stored!.Vep.NroVEP!.Value, stored.Vep.FechaExpiracion!, qr, url), Json);
        return null;
    }

    private static async Task<VepError?> FindAsync(HttpContext context, long represented)
    {
        var query = context.Request.Query;
        if (Number(query["owner-cuit"], "owner-cuit", out var ownerCuit) is { } ownerError) return ownerError;
        if (Number(query["nro-vep"], "nro-vep", out var number, required: false) is { } numberError) return numberError;
        string? transaction = query["owner-transaction-id"];
        if (number is null && string.IsNullOrWhiteSpace(transaction))
            return new VepError(400, "MissingServletRequestParameterException", "Required request parameter 'nro-vep' for method parameter type Long is not present");
        if (ownerCuit != represented) return new VepError(404, "OwnerCuitException", "El VEP pertenece a otro ownerCuit");

        var (stored, error) = await context.RequestServices.GetRequiredService<VepService>().FindAsync(ownerCuit!.Value, number, transaction, context.RequestAborted);
        if (error is not null) return error;

        var (qr, url) = VepService.PendingPayment(stored!, WithQr(context));
        await context.Response.WriteAsJsonAsync(new Found(stored!.Vep, stored.Cp, qr, url), Json);
        return null;
    }

    private static VepError? Number(string? text, string name, out long? value, bool required = true)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return required ? new VepError(400, "MissingServletRequestParameterException", $"Required request parameter '{name}' for method parameter type Long is not present") : null;
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            return new VepError(400, "MethodArgumentTypeMismatchException",
                $"Method parameter '{name}': Failed to convert value of type 'java.lang.String' to required type 'java.lang.Long'; For input string: \"{text}\"");
        value = parsed;
        return null;
    }

    private static bool WithQr(HttpContext context) =>
        bool.TryParse(context.Request.Query["with-qr"], out var withQr) && withQr;

    /// <summary>The gateway's 401: one message with every problem, '/' escaped as Lua's cjson writes it.</summary>
    private static async Task GatewayRefusalAsync(HttpContext context, IReadOnlyList<string> problems, IClock clock)
    {
        var body = JsonSerializer.Serialize(new
        {
            error = new { message = string.Join("\n", problems), code = "401", date = SetiwsGateway.Format(clock.Now) },
            status = "error",
        }, Json).Replace("/", "\\/");
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(body);
    }

    /// <summary>The application's error body, as the manual's "Manejo de errores" describes it.</summary>
    private static async Task ErrorAsync(HttpContext context, VepError error)
    {
        var request = context.Request;
        var now = context.RequestServices.GetRequiredService<IClock>().Now.ToArgentina();
        context.Response.StatusCode = error.Status;
        await context.Response.WriteAsJsonAsync(new
        {
            id = Guid.NewGuid(),
            timestamp = now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            message = error.Message,
            url = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}",
            method = request.Method,
            status = error.Status,
            type = error.Type,
        }, Json);
    }

    private sealed record Created(
        [property: JsonPropertyName("nroVEP")] long NroVep,
        string FechaExpiracion,
        string? QrBase64,
        string? EntidadDePagoUrl);

    private sealed record Found(
        [property: JsonPropertyName("VEP")] Vep Vep,
        [property: JsonPropertyName("CP")] Cp? Cp,
        string? QrBase64,
        string? EntidadDePagoUrl);
}
