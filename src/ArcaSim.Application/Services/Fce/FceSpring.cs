using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// What wsfecredagente and wsfecredsca share as Spring-WS services
/// (wsfecredagente.md "Validaciones y errores"): every answer is a single
/// resultado; format errors go alone in erroresFormato (codigoDescripcionString,
/// code as text) and stop the business checks; business errors go in errores.
/// Page size, batch size and the absence of a maximum date range are ArcaSim's
/// choices: the manuals say only that ARCA tunes them internally.
/// </summary>
public static class FceSpring
{
    public const int PageSize = 100;
    public const int BatchSize = 100;

    public static XElement Result(XName output, params object?[] content) => new(output, new XElement("resultado", content));

    public static XElement? Errors(IReadOnlyDictionary<int, string> texts, IEnumerable<int> codes) =>
        FceXml.Codes("errores", codes.Select(c => ((long)c, texts[c])));

    public static XElement? FormatErrors(IReadOnlyDictionary<int, string> texts, IEnumerable<int> codes) =>
        FceXml.Codes("erroresFormato", codes.Distinct().Select(c => ((long)c, texts[c])), "codigoDescripcionString");

    public static XElement IdFactura(FceId id) => new("idFactura",
        new XElement("cuitEmisor", id.Cuit),
        new XElement("tipoCmp", id.Type),
        new XElement("ptoVta", id.PointOfSale),
        new XElement("nroCmp", id.Number));

    public static FceId? IdOf(XElement? element) =>
        element is not null && element.ChildLong("cuitEmisor") is { } cuit && element.ChildLong("tipoCmp") is { } type
        && element.ChildLong("ptoVta") is { } point && element.ChildLong("nroCmp") is { } number
            ? new FceId(cuit, (int)type, (int)point, number)
            : null;

    /// <summary>A CUIT the request may leave out; when present it must carry its check digit (2002).</summary>
    public static bool BadCuit(long? cuit) => cuit is { } value && !Cuits.IsValid(value);

    /// <summary>The page and date range checks of the queries: 2004 for a page below 1, 2003 for desde after hasta.</summary>
    public static List<int> CheckQuery(XElement request, out int page, out (string? Kind, DateOnly? From, DateOnly? To) range)
    {
        var errors = new List<int>();
        page = (int)(request.ChildLong("nroPagina") ?? 0);
        if (page <= 0) errors.Add(2004);
        var filter = request.Child("filtroFechas");
        range = (filter?.Value("tipo"), filter?.DateOf("desde"), filter?.DateOf("hasta"));
        if (range.From is null || range.To is null || range.From > range.To) errors.Add(2003);
        return errors;
    }

    public static bool InRange(DateTimeOffset? moment, (string? Kind, DateOnly? From, DateOnly? To) range) =>
        moment is { } at && at.ArgentinaDate() is var day && day >= range.From && day <= range.To;

    /// <summary>The empty answer of a query that failed: the list empty, page 0 and hayMas N, as the real one sends it.</summary>
    public static object?[] FailedQuery(string list, XElement? errors, XElement? formatErrors) =>
        [new XElement(list), new XElement("nroPagina", 0), new XElement("hayMas", "N"), errors, formatErrors];
}
