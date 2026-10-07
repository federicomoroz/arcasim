using System.Globalization;
using ArcaSim.Application.Access;

namespace ArcaSim.Application.Setiws;

/// <summary>
/// The authentication gateway in front of SETIWS-PAGO-API (an OpenResty plugin,
/// "WSAA-AUTH-PROXY"): it reads the WSAA ticket from HTTP headers, checks it
/// whole and answers every problem at once in a single 401, with the texts
/// observed in homologación (docs/arca/servicios/SETIWS-PAGO-API.md).
/// WSAUTH's JWT is not published, so ArcaSim issues none and answers any
/// Authorization header as ARCA answers one it cannot read (ArcaSim's choice).
/// </summary>
public sealed class SetiwsGateway(TicketReader reader, IClock clock)
{
    public const string Service = "seti-setipago-api";
    public const string RepresentedHeader = "WSAA-AUTH-PROXY-REPRESENTADO";
    public const string TokenHeader = "WSAA-AUTH-PROXY-TOKEN";
    public const string SignHeader = "WSAA-AUTH-PROXY-SIGN";

    private const string MissingRepresented = "Falta header con representado seleccionado (\"WSAA-AUTH-PROXY-REPRESENTADO\").";
    private const string MissingCredentials = "Faltan headers requeridos de autenticación/autorización (\"Authorization\" o \"WSAA-AUTH-PROXY-TOKEN\" y \"WSAA-AUTH-PROXY-SIGN\").";
    private const string InvalidJwt = "El header \"Authorization\" no contiene un JWT válido.";
    private const string BadSignature = "La firma no es válida para el token.";
    private const string BadFormat = "Formato inválido del token.";

    /// <returns>The represented CUIT when the ticket holds, or the gateway's messages in the order it writes them.</returns>
    public (long Represented, IReadOnlyList<string> Errors) Check(string? authorization, string? token, string? sign, string? represented)
    {
        var proxy = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(sign);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(represented)) errors.Add(MissingRepresented);
        if (string.IsNullOrEmpty(authorization) && !proxy) errors.Add(MissingCredentials);
        if (errors.Count > 0) return (0, errors);
        if (!proxy) return (0, [InvalidJwt]);

        var facts = reader.Inspect(token!, sign!);
        if (!facts.Readable) return (0, facts.Signed ? [BadFormat] : [BadSignature, BadFormat]);

        if (facts.Service != Service)
            errors.Add($"El token no es válido para este servicio. (Servicio autorizado: \"{facts.Service}\". Servicios admitidos: \"{Service}\").");
        var now = clock.Now;
        if (now.ToUnixTimeSeconds() > facts.ExpirationTime)
            errors.Add($"El token está vencido. (Vencimiento: {Format(DateTimeOffset.FromUnixTimeSeconds(facts.ExpirationTime))}. Hora del servidor: {Format(now)}).");
        var cuit = long.TryParse(represented!.Trim(), out var value) ? value : 0;
        if (!facts.Relations.Contains(cuit))
            errors.Add($"La CUIT representada no está entre las autorizadas. (Seleccionada: {represented!.Trim()}. Autorizadas: {string.Join(", ", facts.Relations)}).");
        if (!facts.Signed) errors.Add(BadSignature);
        return errors.Count > 0 ? (0, errors) : (cuit, []);
    }

    public static string Format(DateTimeOffset moment) =>
        moment.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
