namespace ArcaSim.Domain;

/// <summary>Which of ARCA's two environments ArcaSim impersonates. It changes texts and timings, never the contract.</summary>
public enum ArcaEnvironment
{
    Homologacion,
    Produccion,
}

/// <summary>
/// The values that differ between ARCA's environments, as observed or documented
/// (docs/arca/wsaa.md §2.2, §4.2, §4.4 and docs/arca/wsfev1.md §1.5). The node
/// names say "arcasim" where ARCA puts its own, so nobody mistakes one for the other.
/// </summary>
public sealed record EnvironmentProfile(
    ArcaEnvironment Environment,
    string WsaaDn,
    string WsaaDestinationDn,
    TimeSpan ReplayWindow,
    string FaultHostname,
    string WsfeAmbiente,
    string WsfeVersion)
{
    public static readonly EnvironmentProfile Homologacion = new(
        ArcaEnvironment.Homologacion,
        WsaaDn: "CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239",
        WsaaDestinationDn: "cn=wsaahomo,o=afip,c=ar,serialNumber=CUIT 33693450239",
        ReplayWindow: TimeSpan.FromMinutes(10),
        FaultHostname: "wsaaext0.homo.arcasim",
        WsfeAmbiente: "HomologacionExterno - arcasim",
        WsfeVersion: "7.0.0.53");

    public static readonly EnvironmentProfile Produccion = new(
        ArcaEnvironment.Produccion,
        WsaaDn: "CN=wsaa, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239",
        WsaaDestinationDn: "cn=wsaa,o=afip,c=ar,serialNumber=CUIT 33693450239",
        ReplayWindow: TimeSpan.FromMinutes(2),
        FaultHostname: "wsaaext0.arcasim",
        WsfeAmbiente: "Produccion - arcasim",
        WsfeVersion: "7.0.0.60");

    public static EnvironmentProfile For(ArcaEnvironment environment) =>
        environment == ArcaEnvironment.Produccion ? Produccion : Homologacion;
}
