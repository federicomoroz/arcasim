using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// Reading the FCE requests and writing their answers. The three WSDLs leave
/// their children unqualified, so everything below the operation's element
/// travels without a namespace, and is read by local name (ContractXml's Child
/// family); what is here is what only the FCE needs: a field that is empty is
/// one that was not sent, and a date is exactly yyyy-MM-dd.
/// </summary>
public static class FceXml
{
    /// <summary>The direct child's text, trimmed; null when it is missing or empty.</summary>
    public static string? Value(this XElement? element, string name) => element.ChildText(name) is { Length: > 0 } text ? text : null;

    public static DateOnly? DateOf(this XElement? element, string name) =>
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

    /// <summary>
    /// The one builder of the codigo + descripcion lists every one of these
    /// services answers with (the parameter tables, the errors, the
    /// observations): one item per row under the block's name, which goes out
    /// empty when there are no rows. The item is codigoDescripcion, or
    /// codigoDescripcionString where the code travels as text. It is used by
    /// the FCE, MTXCA and WSCT rules alike; the shared place for it is
    /// ContractXml.
    /// </summary>
    public static XElement CodeList<TCode>(string block, IEnumerable<(TCode Code, string Text)> rows, string item = "codigoDescripcion") =>
        new(block, rows.Select(row => new XElement(item, new XElement("codigo", row.Code), new XElement("descripcion", row.Text))));

    /// <summary>The same list, or null when there are no rows: an errors or observations block goes out only with something to say.</summary>
    public static XElement? Codes(string block, IEnumerable<(long Code, string Text)> codes, string item = "codigoDescripcion") =>
        CodeList(block, codes, item) is { HasElements: true } list ? list : null;
}

/// <summary>An idCtaCte as the request sent it: the account code, or the invoice that opened it.</summary>
public sealed record AccountRef(long? Code, FceId? Invoice);
