using System.Xml;
using System.Xml.Linq;

namespace ArcaSim.Application.Soap;

/// <summary>
/// XML a client sends (an envelope, a ticket, a TRA), read the way ARCA's
/// servers read it: a DTD is refused, so there are no entities to expand and
/// nothing to fetch. Otherwise it reads as XDocument.Parse and
/// XmlDocument.LoadXml do, whitespace and error messages included.
/// </summary>
public static class SafeXml
{
    private static readonly XmlReaderSettings Settings = new() { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true };

    private static readonly XmlReaderSettings KeepWhitespace = new() { DtdProcessing = DtdProcessing.Prohibit };

    /// <summary>As XDocument.Parse(text), without a DTD.</summary>
    public static XDocument Parse(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), Settings);
        return XDocument.Load(reader);
    }

    /// <summary>As new XmlDocument().LoadXml(text), without a DTD.</summary>
    public static XmlDocument Document(string text)
    {
        var document = new XmlDocument();
        using var reader = XmlReader.Create(new StringReader(text), KeepWhitespace);
        document.Load(reader);
        return document;
    }
}
