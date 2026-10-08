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
    /// <summary>
    /// Items per page in the queries of the three services: ArcaSim's choice, the
    /// manuals say only that ARCA tunes it internally.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// A page of a list (the first is 1) and whether more follows it. A page past
    /// the end is empty. The arithmetic is long, so no page number can wrap it
    /// into the middle of the list.
    /// </summary>
    public static (IReadOnlyList<T> Items, bool More) Page<T>(IReadOnlyList<T> all, long page)
    {
        var skip = (page - 1) * PageSize;
        IReadOnlyList<T> items = skip < 0 || skip >= all.Count ? [] : all.Skip((int)skip).Take(PageSize).ToList();
        return (items, skip >= 0 && all.Count > skip + PageSize);
    }

    /// <summary>
    /// The direct child's number as an int: null when it is missing or does not read, and
    /// int.MinValue, which no code, type or point of sale is, when it is a number an int
    /// cannot hold. A rule then refuses 4294967297 as the invalid value it is, instead of
    /// reading it as 1. The shared place for this is ContractXml.
    /// </summary>
    public static int? ChildInt(this XElement? element, string name) =>
        element.ChildLong(name) is { } value ? (value is >= int.MinValue and <= int.MaxValue ? (int)value : int.MinValue) : null;

    /// <summary>The same for a field the schema types as xsd:short: short.MinValue when the number does not fit one.</summary>
    public static short? ChildShort(this XElement? element, string name) =>
        element.ChildLong(name) is { } value ? (value is >= short.MinValue and <= short.MaxValue ? (short)value : short.MinValue) : null;

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
