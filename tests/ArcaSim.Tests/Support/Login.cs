using Arca.Client;

namespace ArcaSim.Tests.Support;

/// <summary>
/// What a test does before it calls a service: get a certificate from ArcaSim's authority and
/// a ticket for the service, as an application would from WSASS and WSAA.
/// </summary>
internal static class Login
{
    /// <summary>
    /// A ticket from ArcaSim's WSAA for the CUIT and the service, with a new certificate authorized
    /// for that service and for <paramref name="alsoAuthorized"/> (one certificate may serve several).
    /// </summary>
    public static async Task<AccessTicket> TicketAsync(this ArcaSimHarness sim, long cuit, string service, params string[] alsoAuthorized)
    {
        var certificate = await sim.IssueCertificateAsync(cuit, "test", [service, .. alsoAuthorized]);
        return await sim.Wsaa(cuit, certificate).LoginAsync(service);
    }

    /// <summary>WSFEv1's Auth element for the CUIT, with a ticket of its own, ready to go in front of an operation's parameters.</summary>
    public static async Task<string> WsfeAuthAsync(this ArcaSimHarness sim, long cuit = ArcaSimHarness.Issuer) =>
        ArcaSimHarness.AuthXml(await sim.TicketAsync(cuit, "wsfe"), cuit);

    /// <summary>The ticket and the represented CUIT as three loose elements, the way the padrón and most of the Java services take them.</summary>
    public static string Credentials(AccessTicket ticket, long cuit, string cuitElement = "cuitRepresentada") =>
        $"<token>{ticket.Token}</token><sign>{ticket.Sign}</sign><{cuitElement}>{cuit}</{cuitElement}>";
}
