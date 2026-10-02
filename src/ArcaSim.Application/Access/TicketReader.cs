using System.Text;
using System.Xml;

namespace ArcaSim.Application.Access;

/// <summary>Why a token and sign were not accepted, in the order every ARCA service checks them.</summary>
public enum TicketProblem
{
    None,
    MissingToken,
    MissingSign,
    Unreadable,
    OutOfDate,
    BadSignature,
    WrongService,
    CuitNotRelated,
}

/// <summary>
/// The outcome of reading an access ticket, with what each service needs to
/// word its own error: ARCA's services agree on the checks and their order,
/// but each dialect says it differently.
/// </summary>
public sealed record TicketCheck(
    TicketProblem Problem,
    long Cuit = 0,
    string? Detail = null,
    long GenerationTime = 0,
    long ExpirationTime = 0,
    long Now = 0,
    string? TokenService = null)
{
    public bool Failed => Problem != TicketProblem.None;
}

/// <summary>
/// Reads the token and sign WSAA handed out and checks them for one service:
/// present, readable, within its dates, signed by ArcaSim, issued for this
/// service, and the represented CUIT among its relations.
/// </summary>
public sealed class TicketReader(IClock clock, ITokenSigner signer)
{
    /// <param name="services">The WSAA ids that may call this service (wsfev1 takes "wsfe"; the constancia also its old name).</param>
    public TicketCheck Check(string? token, string? sign, long cuit, params string[] services)
    {
        if (string.IsNullOrEmpty(token)) return new(TicketProblem.MissingToken);
        if (string.IsNullOrEmpty(sign)) return new(TicketProblem.MissingSign);

        XmlDocument document;
        try
        {
            document = new XmlDocument();
            document.LoadXml(Decode(token));
        }
        catch (XmlException ex)
        {
            return new(TicketProblem.Unreadable, Detail: ex.Message);
        }

        var id = document.SelectSingleNode("/sso/id") as XmlElement;
        var generation = EpochAttribute(id, "gen_time");
        var expiration = EpochAttribute(id, "exp_time");
        var now = clock.Now.ToUnixTimeSeconds();
        if (now < generation || now > expiration)
            return new(TicketProblem.OutOfDate, GenerationTime: generation, ExpirationTime: expiration, Now: now);

        if (!signer.Verify(token, sign)) return new(TicketProblem.BadSignature);

        var login = document.SelectSingleNode("/sso/operation/login") as XmlElement;
        var service = login?.GetAttribute("service") ?? "";
        if (!services.Contains(service)) return new(TicketProblem.WrongService, TokenService: service);

        var relations = document.SelectNodes("/sso/operation/login/relations/relation")?.OfType<XmlElement>()
            .Select(r => r.GetAttribute("key")) ?? [];
        if (!relations.Contains(cuit.ToString())) return new(TicketProblem.CuitNotRelated, Cuit: cuit);

        return new(TicketProblem.None, Cuit: cuit);
    }

    /// <summary>
    /// ARCA's loaders read the token as base64 and then as XML; text that is not
    /// base64 ends up failing as XML at its first character.
    /// </summary>
    private static string Decode(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(token));
        }
        catch (FormatException)
        {
            return "?";
        }
    }

    private static long EpochAttribute(XmlElement? element, string name) =>
        long.TryParse(element?.GetAttribute(name), out var value) ? value : 0;
}
