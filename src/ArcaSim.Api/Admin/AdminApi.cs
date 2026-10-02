using ArcaSim.Application;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;

namespace ArcaSim.Api.Admin;

/// <summary>
/// What ArcaSim adds on top of ARCA, under /arcasim/api: the made-up
/// taxpayers, certificates and authorizations (WSASS's job), the clock, the
/// failures a test can switch on, the vouchers issued, and a reset.
/// </summary>
public static class AdminApi
{
    public sealed record SettingsBody(
        ArcaEnvironment? Environment,
        ManualVersion? ManualVersion,
        bool? FollowCalendar,
        bool? ReplayWindowEnabled,
        bool? OpenAccess,
        decimal? FinalConsumerIdentificationThreshold,
        int? MaxRecordsPerRequest,
        int? CaeLifetimeDays);

    public sealed record PointOfSaleBody(int Number, PointOfSaleKind Kind, bool Blocked = false, DateOnly? DeactivatedOn = null);

    public sealed record TaxpayerBody(string Name, VatCondition VatCondition, bool Active = true, List<PointOfSaleBody>? PointsOfSale = null);

    public sealed record CertificateBody(long Cuit, string Alias, string? Csr, string? Password, List<string>? Services);

    public sealed record ChaosBody(bool? Down, int? DelayMilliseconds, bool? DropNextResponse, int? ForceRejection);

    public sealed record ClockBody(DateTimeOffset? FreezeAt, double? AdvanceMinutes);

    public sealed record RateBody(string Currency, DateOnly Day, decimal Rate);

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/arcasim/api");

        api.MapGet("/status", (SimulationSettings settings, SimulatedClock clock) => Status(settings, clock));

        api.MapPut("/settings", (SettingsBody body, SimulationSettings settings, SimulatedClock clock) =>
        {
            if (body.Environment is { } environment) settings.Environment = environment;
            if (body.FollowCalendar == true) settings.ManualVersionOverride = null;
            else if (body.ManualVersion is { } version) settings.ManualVersionOverride = version;
            if (body.ReplayWindowEnabled is { } replay) settings.ReplayWindowEnabled = replay;
            if (body.OpenAccess is { } open) settings.OpenAccess = open;
            if (body.FinalConsumerIdentificationThreshold is { } threshold) settings.FinalConsumerIdentificationThreshold = threshold;
            if (body.MaxRecordsPerRequest is { } max) settings.MaxRecordsPerRequest = max;
            if (body.CaeLifetimeDays is { } days) settings.CaeLifetimeDays = days;
            return Status(settings, clock);
        });

        api.MapGet("/taxpayers", async (ITaxpayerRepository taxpayers, CancellationToken ct) =>
            (await taxpayers.ListAsync(ct)).Select(Describe));

        api.MapGet("/taxpayers/{cuit:long}", async (long cuit, ITaxpayerRepository taxpayers, CancellationToken ct) =>
            await taxpayers.FindAsync(cuit, ct) is { } taxpayer ? Results.Ok(Describe(taxpayer)) : Results.NotFound());

        api.MapPut("/taxpayers/{cuit:long}", async (long cuit, TaxpayerBody body, ITaxpayerRepository taxpayers, CancellationToken ct) =>
        {
            if (!Cuits.IsValid(cuit)) return Results.BadRequest(new { error = $"El CUIT {cuit} tiene mal el dígito verificador." });
            var points = (body.PointsOfSale ?? []).Select(p => new PointOfSale(p.Number, p.Kind, p.Blocked, p.DeactivatedOn));
            var taxpayer = await taxpayers.FindAsync(cuit, ct);
            if (taxpayer is null)
            {
                taxpayer = new Taxpayer(cuit, body.Name, body.VatCondition, points);
            }
            else
            {
                taxpayer.Update(body.Name, body.VatCondition, body.Active);
                foreach (var point in points) taxpayer.AddPointOfSale(point);
            }
            if (!body.Active) taxpayer.Update(body.Name, body.VatCondition, false);
            await taxpayers.SaveAsync(taxpayer, ct);
            return Results.Ok(Describe(taxpayer));
        });

        api.MapGet("/ca", (KeyMaterial keys) => Results.Text(keys.AuthorityPem, "application/x-pem-file"));

        api.MapPost("/certificates", async (CertificateBody body, KeyMaterial keys, IAccessRepository access, IClock clock, CancellationToken ct) =>
        {
            if (!Cuits.IsValid(body.Cuit)) return Results.BadRequest(new { error = $"El CUIT {body.Cuit} tiene mal el dígito verificador." });
            if (string.IsNullOrWhiteSpace(body.Alias)) return Results.BadRequest(new { error = "Falta el alias." });

            await access.SaveAliasAsync(new ClientAlias(body.Cuit, body.Alias), ct);
            foreach (var service in body.Services ?? [WsfeService.Name])
                await access.SaveAuthorizationAsync(new ServiceAuthorization(body.Cuit, body.Alias, body.Cuit, service), ct);

            if (!string.IsNullOrWhiteSpace(body.Csr))
                return Results.Ok(new { certificate = keys.IssueFromCsr(body.Csr, body.Cuit, body.Alias, clock.Now) });
            var pfx = keys.IssueWithKey(body.Cuit, body.Alias, body.Password ?? "", clock.Now);
            return Results.File(pfx, "application/x-pkcs12", $"arcasim-{body.Cuit}-{body.Alias}.pfx");
        });

        api.MapGet("/authorizations", async (IAccessRepository access, CancellationToken ct) => await access.ListAuthorizationsAsync(ct));

        api.MapPost("/authorizations", async (ServiceAuthorization authorization, IAccessRepository access, CancellationToken ct) =>
        {
            await access.SaveAuthorizationAsync(authorization, ct);
            return Results.Ok(authorization);
        });

        api.MapDelete("/authorizations", async (long clientCuit, string alias, long representedCuit, string service, IAccessRepository access, CancellationToken ct) =>
        {
            await access.DeleteAuthorizationAsync(new ServiceAuthorization(clientCuit, alias, representedCuit, service), ct);
            return Results.NoContent();
        });

        api.MapPut("/chaos/{service}", (string service, ChaosBody body, SimulationSettings settings, SimulatedClock clock) =>
        {
            var chaos = settings.ChaosFor(service);
            if (body.Down is { } down) chaos.Down = down;
            if (body.DelayMilliseconds is { } delay) chaos.Delay = TimeSpan.FromMilliseconds(delay);
            if (body.DropNextResponse is { } drop) chaos.DropNextResponse = drop;
            if (body.ForceRejection is { } code) chaos.ForceNextRejection(code);
            return Status(settings, clock);
        });

        api.MapGet("/traffic", (TrafficGate gate) => gate.Snapshot().Select(t => new
        {
            t.Service,
            t.Limits,
            t.InFlight,
            t.Queued,
            LastMinute = new { t.Requests, t.Admitted, t.Refused, t.AverageMilliseconds, t.P95Milliseconds },
            t.SaturationPercent,
            t.LoadPercent,
        }));

        api.MapPut("/traffic/{service}", (string service, TrafficLimits limits, TrafficGate gate) =>
        {
            if (limits.RequestsPerMinute < 0 || limits.Capacity < 0 || limits.ServiceTimeMilliseconds < 0 || limits.QueueLimit < 0)
                return Results.BadRequest(new { error = "Los límites no pueden ser negativos." });
            gate.SetLimits(service, limits);
            return Results.Ok(limits);
        });

        api.MapPost("/clock", (ClockBody body, SimulationSettings settings, SimulatedClock clock) =>
        {
            if (body.FreezeAt is { } at) clock.Freeze(at);
            if (body.AdvanceMinutes is { } minutes) clock.Advance(TimeSpan.FromMinutes(minutes));
            return Status(settings, clock);
        });

        api.MapDelete("/clock", (SimulationSettings settings, SimulatedClock clock) =>
        {
            clock.Reset();
            return Status(settings, clock);
        });

        api.MapGet("/vouchers", async (long? cuit, int? limit, IVoucherStore vouchers, CancellationToken ct) =>
            (await vouchers.ListAsync(cuit, limit ?? 100, ct)).Select(v => new
            {
                v.Cuit,
                v.PointOfSale,
                v.VoucherType,
                v.From,
                v.To,
                Date = v.Date,
                EmissionType = v.EmissionType.ToString().ToUpperInvariant(),
                v.AuthorizationCode,
                v.AuthorizationDue,
                v.ProcessedAt,
                Total = v.Detail.ImpTotal,
                Currency = v.Detail.MonId,
                Receiver = new { v.Detail.DocTipo, v.Detail.DocNro },
                Observations = v.Observations.Select(o => new { o.Code, o.Msg }),
            }));

        api.MapPut("/rates", async (RateBody body, IExchangeRates rates, CancellationToken ct) =>
        {
            await rates.SetAsync(body.Currency, body.Day, body.Rate, ct);
            return Results.Ok(body);
        });

        api.MapPost("/reset", async (IEnumerable<IResettable> stores, SimulationSettings settings, SimulatedClock clock, TrafficGate traffic, CancellationToken ct) =>
        {
            traffic.Reset();
            foreach (var store in stores) await store.ResetAsync(ct);
            settings.ResetChaos();
            clock.Reset();
            return Status(settings, clock);
        });
    }

    private static object Status(SimulationSettings settings, SimulatedClock clock)
    {
        var now = clock.Now.ToArgentina();
        return new
        {
            Environment = settings.Environment.ToString(),
            ManualVersion = settings.ManualVersionOn(DateOnly.FromDateTime(now.DateTime)) == ManualVersion.V4_8 ? "4.8" : "4.7",
            FollowsCalendar = settings.ManualVersionOverride is null,
            settings.ReplayWindowEnabled,
            settings.OpenAccess,
            settings.FinalConsumerIdentificationThreshold,
            settings.MaxRecordsPerRequest,
            settings.CaeLifetimeDays,
            Clock = new { Now = now, clock.Frozen },
            Chaos = settings.Chaos.ToDictionary(c => c.Key, c => new
            {
                c.Value.Down,
                DelayMilliseconds = (int)c.Value.Delay.TotalMilliseconds,
                c.Value.DropNextResponse,
                c.Value.PendingForcedRejections,
            }),
        };
    }

    private static object Describe(Taxpayer taxpayer) => new
    {
        taxpayer.Cuit,
        taxpayer.Name,
        VatCondition = taxpayer.VatCondition.ToString(),
        taxpayer.Active,
        PointsOfSale = taxpayer.PointsOfSale.OrderBy(p => p.Number).Select(p => new
        {
            p.Number,
            Kind = p.Kind.ToString(),
            p.Blocked,
            p.DeactivatedOn,
        }),
    };
}
