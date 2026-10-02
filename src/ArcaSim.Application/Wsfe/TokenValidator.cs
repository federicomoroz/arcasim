using ArcaSim.Application.Access;

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
public sealed class TokenValidator(TicketReader tickets)
{
    public const string Service = "wsfe";

    public AuthCheck Validate(FEAuthRequest? auth)
    {
        if (auth is null) return Fail(500, "Campo Auth no fue ingresado o esta mal formado.");
        var check = tickets.Check(auth.Token, auth.Sign, auth.Cuit, Service);
        return check.Problem switch
        {
            TicketProblem.None => new AuthCheck(null, auth.Cuit),
            TicketProblem.MissingToken => Fail(600, "ValidacionDeToken: Parametro nulo o vacio (token)"),
            TicketProblem.MissingSign => Fail(600, "ValidacionDeToken: Parametro nulo o vacio (sign)"),
            TicketProblem.Unreadable => Fail(600, "ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: " +
                                                  $"Error al cargar token XML. Excepcion: {check.Detail}"),
            TicketProblem.OutOfDate => Fail(600,
                $"ValidacionDeToken: No validaron las fechas del token. GenTime={check.GenerationTime}, ExpTime={check.ExpirationTime}, NowUTC={check.Now}"),
            TicketProblem.BadSignature => Fail(600, "ValidacionDeToken: Error al verificar hash: "),
            TicketProblem.WrongService => Fail(600, $"ValidacionDeToken: No valido Id Sistema: {Service}(Id Sistema de token es: {check.TokenService})"),
            _ => Fail(600, $"ValidacionDeToken: No apareció CUIT en lista de relaciones: {auth.Cuit}"),
        };
    }

    private static AuthCheck Fail(int code, string message) => new(new Err { Code = code, Msg = message }, 0);
}
