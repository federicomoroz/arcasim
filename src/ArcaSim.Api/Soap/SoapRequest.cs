using System.Xml;

namespace ArcaSim.Api.Soap;

public enum SoapVersion
{
    Soap11,
    Soap12,
}

/// <summary>
/// What a SOAP request carries before any service looks at it: its version
/// (from the Content-Type), its action (the SOAPAction header in 1.1, the
/// "action" parameter in 1.2) and the raw body.
/// </summary>
public sealed record SoapRequest(SoapVersion Version, string? Action, string Body)
{
    public const string Soap11Namespace = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string Soap12Namespace = "http://www.w3.org/2003/05/soap-envelope";

    public string EnvelopeNamespace => Version == SoapVersion.Soap12 ? Soap12Namespace : Soap11Namespace;

    public static async Task<SoapRequest?> ReadAsync(HttpRequest request)
    {
        var contentType = request.ContentType ?? "";
        SoapVersion version;
        string? action;
        if (contentType.StartsWith("application/soap+xml", StringComparison.OrdinalIgnoreCase))
        {
            version = SoapVersion.Soap12;
            action = ParameterOf(contentType, "action");
        }
        else if (contentType.StartsWith("text/xml", StringComparison.OrdinalIgnoreCase))
        {
            version = SoapVersion.Soap11;
            action = request.Headers.TryGetValue("SOAPAction", out var header) ? header.ToString().Trim().Trim('"') : null;
        }
        else
        {
            return null;
        }

        using var reader = new StreamReader(request.Body);
        return new SoapRequest(version, action, await reader.ReadToEndAsync());
    }

    /// <summary>
    /// A reader positioned on the first element inside soap:Body, over the whole
    /// document so line and column numbers in errors count from its start, as
    /// ARCA's do. Throws XmlException when the XML is not well formed.
    /// </summary>
    public XmlReader OpenBody()
    {
        var reader = XmlReader.Create(new StringReader(Body), new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit });
        reader.MoveToContent();
        if (reader.LocalName != "Envelope") throw new XmlException("The root element is not a SOAP envelope.");
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Body") continue;
            if (reader.IsEmptyElement) break;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element) return reader;
                if (reader.NodeType == XmlNodeType.EndElement) break;
            }
            break;
        }
        reader.Dispose();
        throw new SoapBodyEmptyException();
    }

    /// <summary>Reads the whole document once, to tell a malformed request apart before dispatching it.</summary>
    public void EnsureWellFormed()
    {
        using var reader = XmlReader.Create(new StringReader(Body), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        while (reader.Read())
        {
        }
    }

    private static string? ParameterOf(string contentType, string name)
    {
        foreach (var part in contentType.Split(';').Skip(1))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return pair[1].Trim().Trim('"');
        }
        return null;
    }
}

public sealed class SoapBodyEmptyException() : Exception("The SOAP body has no element.");
