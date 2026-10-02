using System.Xml.Linq;

namespace ArcaSim.Application.Soap;

/// <summary>A SOAP fault as a service raises it: who is to blame, the text, and what goes in detail.</summary>
public sealed record SoapFault(string Code, string Message, XElement? Detail = null);

/// <summary>
/// What an operation answers in the services written with XML trees rather
/// than XmlSerializer: the response element for the Body, or a fault. The
/// dialect's writer puts either into the envelope.
/// </summary>
public sealed record SoapResult(XElement? Body, SoapFault? Fault)
{
    public static SoapResult Ok(XElement body) => new(body, null);

    public static SoapResult Fail(SoapFault fault) => new(null, fault);
}

/// <summary>Reading the unqualified children of a request element, as JAX-WS services declare them.</summary>
public static class SoapArgs
{
    public static string? Text(this XElement request, string name) =>
        request.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    public static IEnumerable<string> Texts(this XElement request, string name) =>
        request.Elements().Where(e => e.Name.LocalName == name).Select(e => e.Value);

    public static long Long(this XElement request, string name) =>
        long.TryParse(request.Text(name)?.Trim(), out var value) ? value : 0;
}
