using System.Security.Cryptography.X509Certificates;

namespace Arca.Client;

/// <summary>
/// Where ARCA is and who is calling. Moving from ArcaSim to homologación or
/// production changes these values and nothing else.
/// </summary>
public sealed class ArcaOptions
{
    /// <summary>LoginCms of WSAA, e.g. https://wsaahomo.afip.gov.ar/ws/services/LoginCms.</summary>
    public required Uri WsaaUrl { get; init; }

    /// <summary>WSFEv1, e.g. https://wswhomo.afip.gov.ar/wsfev1/service.asmx.</summary>
    public required Uri WsfeUrl { get; init; }

    /// <summary>The certificate WSASS (or ArcaSim) issued, with its private key.</summary>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>The CUIT the vouchers are issued for. It has to be among the ticket's relations.</summary>
    public required long Cuit { get; init; }

    /// <summary>
    /// Where to keep the access ticket between runs. WSAA refuses a new ticket
    /// while the last one is valid (coe.alreadyAuthenticated), so a process that
    /// restarts needs the one it already had. Null keeps it in memory only.
    /// The file name carries the certificate, the WSAA address with its port and
    /// the service. The cache is best effort: a directory that cannot be read or
    /// written is skipped, and the client logs in as it would without one.
    /// </summary>
    public string? TicketCacheDirectory { get; init; }

    /// <summary>How long before its expiration a ticket is renewed.</summary>
    public TimeSpan TicketRenewalMargin { get; init; } = TimeSpan.FromMinutes(10);

    public static X509Certificate2 LoadCertificate(string pfxPath, string? password = null) =>
        new(pfxPath, password, X509KeyStorageFlags.EphemeralKeySet);
}
