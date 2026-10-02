using System.Text;
using System.Xml;

namespace ArcaSim.Application.Wsfe;

/// <summary>The outcome of checking Auth: an error for the answer, or the CUIT that may be used.</summary>
public sealed record AuthCheck(Err? Error, long Cuit)
{
    public bool Failed => Error is not null;
}

/// <summary>
/// Checks WSFEv1's Auth block the way ARCA does, in the order observed against
/// homologación (docs/arca/wsfev1.md §1.4, wsfev1-codigos.md §2): Auth present,
/// token not empty, token readable, token dates, signature, service, CUIT
/// among the token's relations. The messages are ARCA's, word for word.
/// </summary>
public sealed class TokenValidator(IClock clock, ITokenSigner signer)
{
    public const string Service = "wsfe";

    public AuthCheck Validate(FEAuthRequest? auth)
    {
        if (auth is null) return Fail(500, "Campo Auth no fue ingresado o esta mal formado.");
        if (string.IsNullOrEmpty(auth.Token)) return Fail(600, "ValidacionDeToken: Parametro nulo o vacio (token)");
        if (string.IsNullOrEmpty(auth.Sign)) return Fail(600, "ValidacionDeToken: Parametro nulo o vacio (sign)");

        XmlDocument token;
        try
        {
            token = new XmlDocument();
            token.LoadXml(Decode(auth.Token));
        }
        catch (XmlException ex)
        {
            return Fail(600, "ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: " +
                             $"Error al cargar token XML. Excepcion: {ex.Message}");
        }

        var id = token.SelectSingleNode("/sso/id") as XmlElement;
        var generation = EpochAttribute(id, "gen_time");
        var expiration = EpochAttribute(id, "exp_time");
        var now = clock.Now.ToUnixTimeSeconds();
        if (now < generation || now > expiration)
            return Fail(600, $"ValidacionDeToken: No validaron las fechas del token. GenTime={generation}, ExpTime={expiration}, NowUTC={now}");

        if (!signer.Verify(auth.Token, auth.Sign)) return Fail(600, "ValidacionDeToken: Error al verificar hash: ");

        var login = token.SelectSingleNode("/sso/operation/login") as XmlElement;
        var service = login?.GetAttribute("service") ?? "";
        if (service != Service)
            return Fail(600, $"ValidacionDeToken: No valido Id Sistema: {Service}(Id Sistema de token es: {service})");

        var relations = token.SelectNodes("/sso/operation/login/relations/relation")?.OfType<XmlElement>()
            .Select(r => r.GetAttribute("key")) ?? [];
        if (!relations.Contains(auth.Cuit.ToString()))
            return Fail(600, $"ValidacionDeToken: No apareció CUIT en lista de relaciones: {auth.Cuit}");

        return new AuthCheck(null, auth.Cuit);
    }

    /// <summary>
    /// ARCA's loader reads the token as base64 and then as XML; text that is
    /// not base64 ends up failing as XML at its first character, which is the
    /// message homologación gives for a made-up token.
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

    private static AuthCheck Fail(int code, string message) => new(new Err { Code = code, Msg = message }, 0);
}
