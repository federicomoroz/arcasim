using System.Collections.Concurrent;
using ArcaSim.Domain;

namespace ArcaSim.Application;

/// <summary>Which edition of the WSFEv1 manual ArcaSim follows. They differ in one rule (docs/arca/normativa.md §0).</summary>
public enum ManualVersion
{
    /// <summary>In production since 01/09/2026: a missing CondicionIVAReceptorId is observation 10245.</summary>
    V4_7,

    /// <summary>From 01/12/2026: a missing CondicionIVAReceptorId is rejected with 10246.</summary>
    V4_8,
}

/// <summary>
/// What ArcaSim adds on top of ARCA: the environment it impersonates and the
/// failures a test can switch on. Changed through the admin API; one instance
/// for the whole process.
/// </summary>
public sealed class SimulationSettings
{
    private readonly ConcurrentDictionary<string, ServiceChaos> _chaos = new(StringComparer.OrdinalIgnoreCase);

    public ArcaEnvironment Environment { get; set; } = ArcaEnvironment.Homologacion;

    public EnvironmentProfile Profile => EnvironmentProfile.For(Environment);

    /// <summary>Null follows the calendar: v4.8 from 01/12/2026, v4.7 before.</summary>
    public ManualVersion? ManualVersionOverride { get; set; }

    /// <summary>WSAA's anti-repeat window. Off by default for tests, which ask for tickets in a loop.</summary>
    public bool ReplayWindowEnabled { get; set; } = true;

    /// <summary>The amount from which a final consumer has to be identified (RG 1415, texto RG 5700/2025).</summary>
    public decimal FinalConsumerIdentificationThreshold { get; set; } = 10_000_000m;

    /// <summary>RegXReq in FECompTotXRequest: 250 in homologación (2021); production's value is not public.</summary>
    public int MaxRecordsPerRequest { get; set; } = 250;

    /// <summary>CAEFchVto is the voucher's date plus this many days: what every observed CAE shows (wsfev1.md §6.2).</summary>
    public int CaeLifetimeDays { get; set; } = 10;

    /// <summary>
    /// ArcaSim used only through ARCA's endpoints, with nothing set up first.
    /// Any certificate with a CUIT in its DN logs in (the one WSASS issued, or a
    /// self-signed one), its CUIT is authorized for every service, and an issuer
    /// or point of sale ArcaSim has not seen is created the first time it is used.
    /// Off, ArcaSim asks for what ARCA asks for: a certificate from its own
    /// authority, an authorization, and a registered issuer and point of sale.
    /// </summary>
    public bool OpenAccess { get; set; } = true;

    public ManualVersion ManualVersionOn(DateOnly day) =>
        ManualVersionOverride ?? (day >= new DateOnly(2026, 12, 1) ? ManualVersion.V4_8 : ManualVersion.V4_7);

    public ServiceChaos ChaosFor(string service) => _chaos.GetOrAdd(service, _ => new ServiceChaos());

    public IReadOnlyDictionary<string, ServiceChaos> Chaos => _chaos;

    public void ResetChaos() => _chaos.Clear();
}

/// <summary>Failures a test switches on for one service: down, slow, or a scripted answer for the next call.</summary>
public sealed class ServiceChaos
{
    private readonly ConcurrentQueue<int> _forcedObservations = new();

    public bool Down { get; set; }

    public TimeSpan Delay { get; set; }

    /// <summary>The next FECAESolicitar gets its CAE recorded but the connection is dropped before the answer.</summary>
    public bool DropNextResponse { get; set; }

    /// <summary>
    /// What fwshomo's F5 does in homologación: every answer that would go out
    /// with HTTP 500 (a fault) goes out as 200, without Content-Type, with the
    /// text "BL&lt;n&gt; &lt;yyyy-MM-dd HH:mm:ss&gt; 500" (wsmtxca.md, wsct.md, wsfecred.md).
    /// </summary>
    public bool BalancerMask { get; set; }

    public void ForceNextRejection(int code) => _forcedObservations.Enqueue(code);

    public bool TryTakeForcedRejection(out int code) => _forcedObservations.TryDequeue(out code);

    public int PendingForcedRejections => _forcedObservations.Count;
}
