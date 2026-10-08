using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// One liquidation or adjustment a sector liquidation service authorized
/// (wslsp, wslum, wsltv, wslca): its key (CUIT, point of sale, voucher type,
/// number), its CAE and dates, what it adjusts, its state, and the detail the
/// service answered, kept as XML so the queries return exactly what was issued.
/// </summary>
public sealed record Settlement(
    string Service,
    long Cuit,
    int PointOfSale,
    int VoucherType,
    long Number,
    long Cae,
    DateOnly Date,
    DateOnly ProcessedOn,
    DateOnly CaeExpiry,
    long ReceiverCuit,
    decimal Total,
    string Kind,
    bool IsAdjustment,
    List<string> Adjusts,
    string State,
    string Detail)
{
    public const string Active = "vigente";
    public const string Annulled = "anulada";
    public const string Adjusted = "ajustada";

    public string KeyOf() => SettlementLedger.Key(Cuit, PointOfSale, VoucherType, Number);

    public XElement DetailXml() => XElement.Parse(Detail);
}

/// <summary>The last number a sequence authorized and the date it carried.</summary>
public sealed record LastSettlement(long Number, DateOnly Date);

/// <summary>Where a CAE points: the settlement's key.</summary>
public sealed record SettlementByCae(string Key);

/// <summary>A business rejection: the code a manual gives and its text.</summary>
public readonly record struct SettlementProblem(long Code, string Text);

/// <summary>What the check of the number a client picked found.</summary>
public enum NumberCheck
{
    Ok,
    WrongNumber,
    EarlierDate,
}

/// <summary>
/// The numbering and authorization the four sector liquidation services share
/// (docs/arca/servicios/wslsp.md, wslum.md, wsltv.md and wslca.md §1.3 of each
/// manual): one sequence per CUIT, point of sale and voucher type that starts
/// at 1, the client picks the number and a rejected one is not consumed, a CAE
/// of 14 digits, and every settlement recorded for constatación. State lives
/// in the document store, in a collection named after the service.
/// The CAE expiry is not documented by any of the four manuals (the examples
/// fall 6 to 36 days after processing): ArcaSim gives 10 days.
/// </summary>
public sealed class SettlementLedger(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
{
    public const int CaeDays = 10;

    /// <summary>
    /// The text wslum (2074), wslca (1500) and wslsp (1009) give for a number out of sequence:
    /// the one sentence of their manuals under each service's own code.
    /// </summary>
    public const string WrongNumberText = "N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados.";

    private readonly KeyedLocks<string> _documents = new();

    public IClock Clock => clock;

    public DateOnly Today => clock.Today();

    public static string Key(long cuit, int pointOfSale, int voucherType, long number) =>
        $"{cuit}/{pointOfSale:D5}/{voucherType:D3}/{number:D8}";

    public Task<IDisposable> LockAsync(string service, long cuit, int pointOfSale, int voucherType, CancellationToken ct) =>
        locks.AcquireAsync(service, cuit, pointOfSale, voucherType, ct);

    /// <summary>
    /// One operation at a time on one voucher of the service, by its key: what
    /// annuls or adjusts an original reads it again inside this lock, checks its
    /// state and writes it, so no two do it to the same one. Take it inside the sequence's.
    /// </summary>
    public Task<IDisposable> LockDocumentAsync(string key, CancellationToken ct) => _documents.AcquireAsync(key, ct);

    /// <summary>
    /// One at a time for what must be unique in the whole service (a bale, a
    /// delivery note): the check that nothing uses it yet and the record that
    /// something does share one hold, since two vouchers on different
    /// sequences would each find it free. Take it inside the sequence's.
    /// </summary>
    public Task<IDisposable> LockUniqueAsync(string service, string what, CancellationToken ct) => LockUniqueAsync(service, what, 0, ct);

    /// <summary>The same for what is unique among one CUIT's vouchers (a buyer's period).</summary>
    public Task<IDisposable> LockUniqueAsync(string service, string what, long cuit, CancellationToken ct) =>
        locks.AcquireAsync($"{service}.{what}", cuit, 0, 0, ct);

    public Task<LastSettlement?> LastAsync(string service, long cuit, int pointOfSale, int voucherType, CancellationToken ct) =>
        store.GetAsync<LastSettlement>(service, $"ultimo/{cuit}/{pointOfSale:D5}/{voucherType:D3}", ct);

    /// <summary>
    /// The client picks the number of a voucher: it must be the last + 1 of its sequence (1 when the
    /// sequence is empty) and, in the services that ask for it (<paramref name="date"/> given), must not
    /// carry a date earlier than the last one's. Take the sequence's lock before, and keep it until the voucher is issued.
    /// </summary>
    public async Task<NumberCheck> CheckNumberAsync(string service, long cuit, int pointOfSale, int voucherType, long number, DateOnly? date, CancellationToken ct)
    {
        var last = await LastAsync(service, cuit, pointOfSale, voucherType, ct);
        if (number != (last?.Number ?? 0) + 1) return NumberCheck.WrongNumber;
        return date is { } day && last is not null && day < last.Date ? NumberCheck.EarlierDate : NumberCheck.Ok;
    }

    public Task<Settlement?> FindAsync(string service, long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct) =>
        FindAsync(service, Key(cuit, pointOfSale, voucherType, number), ct);

    public Task<Settlement?> FindAsync(string service, string key, CancellationToken ct) =>
        store.GetAsync<Settlement>(service, $"liq/{key}", ct);

    public async Task<Settlement?> FindByCaeAsync(string service, long cae, CancellationToken ct) =>
        await store.GetAsync<SettlementByCae>(service, $"cae/{cae}", ct) is { } index ? await FindAsync(service, index.Key, ct) : null;

    public async Task<IReadOnlyList<Settlement>> ListAsync(string service, long cuit, CancellationToken ct) =>
        await store.ListAsync<Settlement>(service, $"liq/{cuit}/", ct);

    public long NewCae() => long.Parse(codes.NextCae(), CultureInfo.InvariantCulture);

    /// <summary>Keeps a new settlement, moves its sequence and records the voucher for constatación (wscdc).</summary>
    public async Task IssueAsync(Settlement settlement, CancellationToken ct)
    {
        var key = settlement.KeyOf();
        await store.PutAsync(settlement.Service, $"liq/{key}", settlement, ct);
        await store.PutAsync(settlement.Service, $"cae/{settlement.Cae}", new SettlementByCae(key), ct);
        await store.PutAsync(settlement.Service, $"ultimo/{settlement.Cuit}/{settlement.PointOfSale:D5}/{settlement.VoucherType:D3}",
            new LastSettlement(settlement.Number, settlement.Date), ct);
        await store.PutAsync(new AuthorizedVoucher(settlement.Service, settlement.Cuit, settlement.PointOfSale, settlement.VoucherType,
            settlement.Number, settlement.Date, settlement.Total, 80, settlement.ReceiverCuit, "CAE",
            settlement.Cae.ToString(CultureInfo.InvariantCulture), settlement.CaeExpiry), ct);
    }

    public Task UpdateAsync(Settlement settlement, CancellationToken ct) =>
        store.PutAsync(settlement.Service, $"liq/{settlement.KeyOf()}", settlement, ct);

    public Task<Taxpayer?> TaxpayerAsync(long cuit, CancellationToken ct) => taxpayers.FindAsync(cuit, ct);

    /// <summary>
    /// The issuer's web service points of sale (RECE): <paramref name="noPoints"/> when it has none at all,
    /// <paramref name="invalid"/> when it has not this one, nothing when it is fine. Each service
    /// gives its own code and text for the two.
    /// </summary>
    public async Task<SettlementProblem?> PointProblemAsync(long cuit, int pointOfSale, SettlementProblem noPoints, SettlementProblem invalid, CancellationToken ct)
    {
        var points = await PointsOfSaleAsync(cuit, ct);
        if (points.Count == 0) return noPoints;
        return points.Any(p => p.Number == pointOfSale) ? null : invalid;
    }

    /// <summary>The answer to a query of points of sale, the same in the four services: a puntoVenta per point, each with the issuer's address.</summary>
    public async Task<List<XElement>> PointsAnswerAsync(long cuit, CancellationToken ct)
    {
        var address = AddressOf(await taxpayers.FindAsync(cuit, ct));
        return (await PointsOfSaleAsync(cuit, ct))
            .Select(p => new XElement("puntoVenta", new XElement("codigo", p.Number), new XElement("descripcion", address))).ToList();
    }

    private async Task<IReadOnlyList<PointOfSale>> PointsOfSaleAsync(long cuit, CancellationToken ct) =>
        (await taxpayers.FindAsync(cuit, ct))?.PointsOfSale
            .Where(p => p.Kind == PointOfSaleKind.WebServiceCae && !p.Blocked && (p.DeactivatedOn is null || p.DeactivatedOn > Today))
            .OrderBy(p => p.Number).ToList() ?? [];

    /// <summary>A VAT condition as the liquidations print it; the manuals show no list, so the wording is ArcaSim's.</summary>
    public static string VatText(VatCondition condition) => condition switch
    {
        VatCondition.ResponsableInscripto => "IVA Responsable Inscripto",
        VatCondition.Exento => "IVA Sujeto Exento",
        VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido => "Responsable Monotributo",
        _ => "IVA No Alcanzado",
    };

    /// <summary>The address a point of sale shows: the taxpayer's fiscal address, the only one ArcaSim knows.</summary>
    public static string AddressOf(Taxpayer? taxpayer)
    {
        var address = taxpayer?.Profile.Address ?? TaxAddress.Default;
        return $"{address.Street} - {address.Locality}";
    }
}

/// <summary>Reading the requests and writing the answers of the sector liquidation services.</summary>
public static class SettlementXml
{
    /// <summary>A child by local name, or an empty element when it is missing, so a chain of lookups never fails.</summary>
    public static XElement Child(this XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name) ?? new XElement(name);

    public static XElement? Optional(this XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    public static IEnumerable<XElement> Children(this XElement element, string name) =>
        element.Elements().Where(e => e.Name.LocalName == name);

    public static string? Value(this XElement element, string name) =>
        element.Optional(name)?.Value.Trim() is { Length: > 0 } text ? text : null;

    public static long Number(this XElement element, string name) =>
        long.TryParse(element.Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static decimal Amount(this XElement element, string name) =>
        decimal.TryParse(element.Value(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static decimal? OptionalAmount(this XElement element, string name) =>
        decimal.TryParse(element.Value(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>An xsd:date, with or without the zone some stacks add (2019-05-06-03:00).</summary>
    public static DateOnly? Day(this XElement element, string name)
    {
        var text = element.Value(name);
        if (text is null) return null;
        return DateOnly.TryParseExact(text[..Math.Min(10, text.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>xsd:date the way the JAX-WS services write it: without a zone.</summary>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Round Half Even to 2 decimals, as every manual of the family asks.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);

    public static string Money(decimal value) => Round(value).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>An element only when there is a value: every field of these answers is optional.</summary>
    public static XElement? Maybe(string name, object? value) =>
        value switch
        {
            null => null,
            string text when text.Length == 0 => null,
            DateOnly date => new XElement(name, Iso(date)),
            decimal amount => new XElement(name, amount.ToString(CultureInfo.InvariantCulture)),
            _ => new XElement(name, ContractXml.Format(value)),
        };

    /// <summary>A copy of a request element under its own name, children unqualified as these schemas want them.</summary>
    public static XElement? Copy(XElement? element, string? name = null) =>
        element is null ? null : new XElement(name ?? element.Name.LocalName, element.Elements().Select(e => Copy(e)), element.HasElements ? null : element.Value);

    /// <summary>
    /// A business rejection: the service's own error block (respuesta/errores/error)
    /// with the error, and the metadata the service sends with every answer when it has one.
    /// </summary>
    public static ContractAnswer Fail(ServiceCall call, XElement? metadata, SettlementProblem problem)
    {
        var answer = call.Error(problem.Code, problem.Text);
        if (answer.Body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "errores") is not { } block) return answer;
        block.RemoveNodes();
        block.Add(new XElement("error",
            new XElement("codigo", problem.Code.ToString(CultureInfo.InvariantCulture)),
            new XElement("descripcion", problem.Text)));
        if (metadata is not null && block.Parent is { } holder && holder.Optional("metadata") is null) block.AddAfterSelf(metadata);
        return answer;
    }

    public static ContractAnswer Ok(ServiceCall call, params object?[] content) =>
        call.Ok(new XElement(call.Operation.Output, new XElement("respuesta", content)));

    /// <summary>
    /// The pdf element of a settlement's answer (§2.5/§2.6 of the manuals: "el mismo archivo que se imprime
    /// por la aplicación web"): the voucher, both parties, the date, the total and the CAE, in the words each service uses for them.
    /// </summary>
    public static XElement PdfOf(Settlement settlement, string title, string issuer, string receiver, string total) =>
        new("pdf", SimplePdf.Lines($"ARCA - {title} {settlement.VoucherType:D3}-{settlement.PointOfSale:D5}-{settlement.Number:D8}",
        [
            $"CUIT {issuer}: {settlement.Cuit}", $"CUIT {receiver}: {settlement.ReceiverCuit}", $"Fecha: {Iso(settlement.Date)}",
            $"{total}: {Money(settlement.Total)}", $"CAE: {settlement.Cae}", $"Vencimiento CAE: {Iso(settlement.CaeExpiry)}",
        ]));
}
