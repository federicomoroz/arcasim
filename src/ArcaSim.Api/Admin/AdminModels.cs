using ArcaSim.Application;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Api.Admin;

// What the admin API reads and writes: request bodies, and the views it answers with.

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

public sealed record TaxpayerBody(string Name, VatCondition VatCondition, bool Active = true, List<PointOfSaleBody>? PointsOfSale = null, TaxpayerProfile? Profile = null);

public sealed record CertificateBody(long Cuit, string Alias, string? Csr, string? Password, List<string>? Services);

public sealed record ChaosBody(bool? Down, int? DelayMilliseconds, bool? DropNextResponse, int? ForceRejection)
{
    /// <summary>fwshomo's F5 mask over every fault (ServiceChaos.BalancerMask).</summary>
    public bool? BalancerMask { get; init; }
}

public sealed record ClockBody(DateTimeOffset? FreezeAt, double? AdvanceMinutes);

public sealed record RateBody(string Currency, DateOnly Day, decimal Rate);

public sealed record ErrorView(string Error);

public sealed record ChaosView(bool Down, int DelayMilliseconds, bool DropNextResponse, int PendingForcedRejections)
{
    public bool BalancerMask { get; init; }
}

public sealed record ClockView(DateTimeOffset Now, bool Frozen);

/// <summary>The simulation as it stands: what /status answers and what every settings change returns.</summary>
public sealed record StatusView(
    string Environment,
    string ManualVersion,
    bool FollowsCalendar,
    bool ReplayWindowEnabled,
    bool OpenAccess,
    decimal FinalConsumerIdentificationThreshold,
    int MaxRecordsPerRequest,
    int CaeLifetimeDays,
    ClockView Clock,
    IReadOnlyDictionary<string, ChaosView> Chaos)
{
    public static StatusView Of(SimulationSettings settings, SimulatedClock clock)
    {
        var now = clock.Now.ToArgentina();
        return new StatusView(
            settings.Environment.ToString(),
            settings.ManualVersionOn(DateOnly.FromDateTime(now.DateTime)) == Application.ManualVersion.V4_8 ? "4.8" : "4.7",
            settings.ManualVersionOverride is null,
            settings.ReplayWindowEnabled,
            settings.OpenAccess,
            settings.FinalConsumerIdentificationThreshold,
            settings.MaxRecordsPerRequest,
            settings.CaeLifetimeDays,
            new ClockView(now, clock.Frozen),
            settings.Chaos.ToDictionary(c => c.Key, c => new ChaosView(
                c.Value.Down, (int)c.Value.Delay.TotalMilliseconds, c.Value.DropNextResponse, c.Value.PendingForcedRejections) { BalancerMask = c.Value.BalancerMask }));
    }
}

public sealed record PointOfSaleView(int Number, string Kind, bool Blocked, DateOnly? DeactivatedOn);

public sealed record TaxpayerView(long Cuit, string Name, string VatCondition, bool Active, IReadOnlyList<PointOfSaleView> PointsOfSale, TaxpayerProfile Profile)
{
    public static TaxpayerView Of(Taxpayer taxpayer) => new(
        taxpayer.Cuit,
        taxpayer.Name,
        taxpayer.VatCondition.ToString(),
        taxpayer.Active,
        taxpayer.PointsOfSale.OrderBy(p => p.Number)
            .Select(p => new PointOfSaleView(p.Number, p.Kind.ToString(), p.Blocked, p.DeactivatedOn)).ToList(),
        taxpayer.Profile);
}

public sealed record ReceiverView(int DocTipo, long DocNro);

public sealed record ObservationView(int Code, string? Msg);

public sealed record VoucherView(
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long From,
    long To,
    DateOnly Date,
    string EmissionType,
    string AuthorizationCode,
    DateOnly AuthorizationDue,
    DateTimeOffset ProcessedAt,
    double Total,
    string? Currency,
    ReceiverView Receiver,
    IReadOnlyList<ObservationView> Observations)
{
    public static VoucherView Of(StoredVoucher v) => new(
        v.Cuit, v.PointOfSale, v.VoucherType, v.From, v.To, v.Date, v.EmissionType.ToString().ToUpperInvariant(),
        v.AuthorizationCode, v.AuthorizationDue, v.ProcessedAt, v.Detail.ImpTotal, v.Detail.MonId,
        new ReceiverView(v.Detail.DocTipo, v.Detail.DocNro),
        v.Observations.Select(o => new ObservationView(o.Code, o.Msg)).ToList());
}

public sealed record LastMinuteView(int Requests, int Admitted, int Refused, double AverageMilliseconds, double P95Milliseconds);

public sealed record TrafficView(
    string Service, TrafficLimits Limits, int InFlight, int Queued, LastMinuteView LastMinute, double SaturationPercent, double LoadPercent)
{
    public static TrafficView Of(TrafficSnapshot t) => new(
        t.Service, t.Limits, t.InFlight, t.Queued,
        new LastMinuteView(t.Requests, t.Admitted, t.Refused, t.AverageMilliseconds, t.P95Milliseconds),
        t.SaturationPercent, t.LoadPercent);
}
