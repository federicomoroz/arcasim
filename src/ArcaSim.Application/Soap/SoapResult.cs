using System.Xml.Linq;

namespace ArcaSim.Application.Soap;

/// <summary>A SOAP fault as a service raises it: who is to blame, the text, and what goes in detail.</summary>
/// <remarks>Status is the HTTP status it travels with: 500 almost always, 200 where a service sends its faults that way (DDJJ).</remarks>
public sealed record SoapFault(string Code, string Message, XElement? Detail = null, int Status = 500);

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
