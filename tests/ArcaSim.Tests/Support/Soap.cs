using System.Xml.Linq;

namespace ArcaSim.Tests.Support;

/// <summary>The SOAP 1.1 envelopes the tests send, and the way to read the answers.</summary>
internal static class Soap
{
    public const string EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>
    /// An envelope around <paramref name="body"/>, the operation's element already written, declaring
    /// the prefixes the body uses (soapenv is always there).
    /// </summary>
    public static string Envelope(string body, params (string Prefix, string Namespace)[] namespaces) =>
        $"<soapenv:Envelope xmlns:soapenv=\"{EnvelopeNamespace}\"{string.Concat(namespaces.Select(n => $" xmlns:{n.Prefix}=\"{n.Namespace}\""))}>" +
        $"<soapenv:Header/><soapenv:Body>{body}</soapenv:Body></soapenv:Envelope>";

    /// <summary>An envelope around a request built from its schema; none at all gives an empty Body.</summary>
    public static string Envelope(XElement? request) => Envelope(request?.ToString(SaveOptions.DisableFormatting) ?? "");

    /// <summary>
    /// An operation's element with its children, in its namespace: the default one for the services that
    /// qualify their children (ASMX, Spring-WS), a prefix for the rest, whose children travel unqualified.
    /// </summary>
    public static string Operation(string name, string namespaceName, string inner, bool qualified = false) =>
        qualified
            ? $"<{name} xmlns=\"{namespaceName}\">{inner}</{name}>"
            : $"<x:{name} xmlns:x=\"{namespaceName}\">{inner}</x:{name}>";

    /// <summary>The SOAP envelope of an answer, out of its MTOM package when the service sends one.</summary>
    public static string Unwrap(string response)
    {
        if (!response.TrimStart().StartsWith("--", StringComparison.Ordinal)) return response;
        var start = response.IndexOf('<', response.IndexOf("\r\n\r\n", StringComparison.Ordinal));
        return response[start..(response.LastIndexOf('>') + 1)];
    }

    /// <summary>The first element of an answer's Body (a response, or the Fault).</summary>
    public static XElement Body(string response) =>
        XDocument.Parse(Unwrap(response)).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();

    /// <summary>
    /// Puts the ticket where the request carries it, and the caller's CUIT in the CUIT next to it (cuit,
    /// cuitRepresentada, CUITDelegado...). A null token takes the token and the sign out of the request,
    /// to ask the way a caller that sent none would.
    /// </summary>
    public static void Sign(XElement request, string? token, string? sign, long cuit)
    {
        if (request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("token", StringComparison.OrdinalIgnoreCase)) is not { } tokenElement) return;
        foreach (var element in tokenElement.Parent!.Elements().ToList())
        {
            var name = element.Name.LocalName.ToLowerInvariant();
            if (name is "token" or "sign" or "firma")
            {
                if (token is null) element.Remove();
                else element.Value = name == "token" ? token : sign ?? "";
            }
            else if (name.Contains("cuit")) element.Value = cuit.ToString();
        }
    }
}
