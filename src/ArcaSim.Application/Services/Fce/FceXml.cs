using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// Reading the FCE requests and writing their answers. The three WSDLs leave
/// their children unqualified, so everything below the operation's element
/// travels without a namespace, and is read by local name.
/// </summary>
public static class FceXml
{
    public static XElement? Child(this XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    public static IEnumerable<XElement> Children(this XElement? element, string name) =>
        element?.Elements().Where(e => e.Name.LocalName == name) ?? [];

    public static string? Value(this XElement element, string name) => element.Child(name)?.Value.Trim() is { Length: > 0 } text ? text : null;

    public static long? LongOf(this XElement element, string name) =>
        long.TryParse(element.Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static decimal? DecimalOf(this XElement element, string name) =>
        decimal.TryParse(element.Value(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static DateOnly? DateOf(this XElement element, string name) =>
        DateOnly.TryParseExact(element.Value(name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    /// <summary>xsd:date as the services write it: AAAA-MM-DD without a zone.</summary>
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Date(DateTimeOffset moment) => moment.ToArgentina().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>xsd:dateTime in Argentina's time without milliseconds or zone, as the real info header writes it (wsfecred.md, Contrato).</summary>
    public static string Moment(DateTimeOffset moment) => moment.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.ToEven).ToString("0.00", CultureInfo.InvariantCulture);

    public static string Rate(decimal rate) => Math.Round(rate, 6).ToString("0.######", CultureInfo.InvariantCulture);

    public static string YesNo(bool value) => value ? "S" : "N";

    /// <summary>A Texto250SimpleType: between 3 and 250 characters.</summary>
    public static string Text250(string text) => text.Length switch
    {
        < 3 => text.PadRight(3, '.'),
        > 250 => text[..250],
        _ => text,
    };

    /// <summary>codigo + descripcion items, under the block's name (arrayErrores, errores, observaciones...).</summary>
    public static XElement? Codes(string block, IEnumerable<(long Code, string Text)> codes, string item = "codigoDescripcion")
    {
        var list = codes.ToList();
        return list.Count == 0
            ? null
            : new XElement(block, list.Select(c => new XElement(item, new XElement("codigo", c.Code), new XElement("descripcion", c.Text))));
    }
}

/// <summary>An idCtaCte as the request sent it: the account code, or the invoice that opened it.</summary>
public sealed record AccountRef(long? Code, FceId? Invoice);
