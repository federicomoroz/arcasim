namespace Arca.Client;

/// <summary>Something went wrong talking to ARCA. Rejected vouchers are not exceptions: they come back as results.</summary>
public abstract class ArcaException(string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Whether trying the same call again later can work.</summary>
    public abstract bool Retryable { get; }
}

/// <summary>
/// ARCA could not be reached or failed on its side: a timeout, an HTTP error,
/// a WSAA "unavailable" fault, or the 500/501/502 infrastructure errors of
/// WSFEv1. Safe to retry; for FECAESolicitar, check with FECompConsultar first.
/// </summary>
public sealed class ArcaUnavailableException(string message, Exception? inner = null) : ArcaException(message, inner)
{
    public override bool Retryable => true;
}

/// <summary>WSAA refused the login (wrong certificate, missing authorization, malformed TRA...).</summary>
public sealed class WsaaFaultException(string code, string message)
    : ArcaException($"WSAA {code}: {message}")
{
    public string Code { get; } = code;

    public string FaultMessage { get; } = message;

    public override bool Retryable => Code is "wsaa.unavailable" or "wsn.unavailable" or "wsaa.internalError";
}

/// <summary>WSFEv1 answered with Errors that are not about a single voucher, such as 600 for the token or 10005 for the point of sale.</summary>
public sealed class WsfeErrorException(IReadOnlyList<ArcaMessage> errors)
    : ArcaException("WSFEv1: " + string.Join("; ", errors.Select(e => $"{e.Code} {e.Message}")))
{
    public IReadOnlyList<ArcaMessage> Errors { get; } = errors;

    public override bool Retryable => Errors.All(e => e.Code is 500 or 501 or 502);
}

/// <summary>A code and its text, as ARCA sends them in Errors, Observaciones or Events.</summary>
public sealed record ArcaMessage(int Code, string Message);
