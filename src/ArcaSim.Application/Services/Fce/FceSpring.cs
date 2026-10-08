using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Fce;

/// <summary>
/// What wsfecredagente and wsfecredsca share as Spring-WS services
/// (wsfecredagente.md "Validaciones y errores"): every answer is a single
/// resultado; format errors go alone in erroresFormato (codigoDescripcionString,
/// code as text) and stop the business checks; business errors go in errores.
/// Batch size and the absence of a maximum date range are ArcaSim's choices:
/// the manuals say only that ARCA tunes them internally.
/// </summary>
public static class FceSpring
{
    public const int BatchSize = 100;

    /// <summary>
    /// How both services answer an operation: one they do not implement keeps the
    /// contract's answer (null); a caller that is not registered in ArcaSim gets 4009
    /// in errores, in the shape of that operation's refusal; and the ledger is held
    /// while the operation runs, as the three FCE services share it.
    /// </summary>
    public static async Task<ContractAnswer?> RunAsync(
        ServiceCall call, FceLedger ledger, IReadOnlyDictionary<int, string> texts,
        Func<ServiceCall, CancellationToken, Task<XElement>>? operation,
        Func<ServiceCall, XElement?, XElement?, XElement> refused, CancellationToken ct)
    {
        if (operation is null) return null;
        if (!await ledger.IsRegisteredAsync(call.Cuit, ct)) return call.Ok(refused(call, Errors(texts, [4009]), null));
        using var _ = await ledger.LockAsync(ct);
        return call.Ok(await operation(call, ct));
    }

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

    /// <summary>
    /// The voucher an idFactura names; null when a part is missing or does not read, and also when the
    /// type or the point of sale is a number an int cannot hold (ChildInt's int.MinValue): the id is
    /// echoed back in the answer, where that marker would not fit the schema, so it is unreadable.
    /// </summary>
    public static FceId? IdOf(XElement? element) =>
        element is not null && element.ChildLong("cuitEmisor") is { } cuit && element.ChildInt("tipoCmp") is { } type
        && element.ChildInt("ptoVta") is { } point && type != int.MinValue && point != int.MinValue && element.ChildLong("nroCmp") is { } number
            ? new FceId(cuit, type, point, number)
            : null;

    /// <summary>A CUIT the request may leave out; when present it must carry its check digit (2002).</summary>
    public static bool BadCuit(long? cuit) => cuit is { } value && !Cuits.IsValid(value);

    /// <summary>
    /// The page and date range checks of the queries: 2004 for a page below 1 or one the
    /// schema's xsd:short cannot hold (the answer repeats it), 2003 for desde after hasta.
    /// </summary>
    public static List<int> CheckQuery(XElement request, out int page, out (string? Kind, DateOnly? From, DateOnly? To) range)
    {
        var errors = new List<int>();
        page = request.ChildInt("nroPagina") ?? 0;
        if (page is < 1 or > short.MaxValue) errors.Add(2004);
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
