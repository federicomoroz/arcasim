using System.Globalization;
using System.Xml.Linq;

namespace ArcaSim.Application.Services.Mtxca;

public sealed record MtxcaItem(
    int? Units, string? Gtin, string? Code, string Description, decimal? Quantity, int Unit,
    decimal? Price, decimal? Discount, int VatCondition, decimal? Vat, decimal Amount);

public sealed record MtxcaAssociated(int Type, int PointOfSale, long Number, long? Cuit, DateOnly? Date);

public sealed record MtxcaOtherTax(int Code, string? Description, decimal Base, decimal Amount);

public sealed record MtxcaBuyer(int DocType, long DocNumber, decimal Percentage);

/// <summary>
/// A ComprobanteType as the request sent it, read by its direct children (the
/// associated vouchers have their own fechaEmision and codigoTipoComprobante).
/// Children may come qualified or not, as ARCA accepts both. An empty element
/// counts as not sent; a number that does not parse counts as not sent too.
/// </summary>
public sealed class MtxcaVoucherInput
{
    public required XElement Element { get; init; }
    public int Type { get; init; }
    public int PointOfSale { get; init; }
    public long Number { get; init; }
    public DateOnly? Date { get; init; }
    public string? AuthorizationType { get; init; }
    public long? AuthorizationCode { get; init; }
    public DateOnly? AuthorizationDue { get; init; }
    public int? DocType { get; init; }
    public long? DocNumber { get; init; }
    public int? ReceiverCondition { get; init; }
    public decimal? Net { get; init; }
    public decimal? NotTaxed { get; init; }
    public decimal? Exempt { get; init; }
    public decimal Subtotal { get; init; }
    public decimal? OtherTaxesTotal { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "";
    public decimal? Rate { get; init; }
    public string? SameCurrency { get; init; }
    public int Concept { get; init; }
    public DateOnly? ServiceFrom { get; init; }
    public DateOnly? ServiceTo { get; init; }
    public DateOnly? PaymentDue { get; init; }
    public string? GenerationTime { get; init; }
    public IReadOnlyList<MtxcaAssociated> Associated { get; init; } = [];
    public (DateOnly From, DateOnly To)? Period { get; init; }
    public IReadOnlyList<MtxcaOtherTax> OtherTaxes { get; init; } = [];
    public IReadOnlyList<MtxcaItem> Items { get; init; } = [];
    public bool HasSubtotals { get; init; }
    public IReadOnlyList<(int Code, decimal Amount)> Subtotals { get; init; } = [];
    public IReadOnlyList<int> ExtraData { get; init; } = [];
    public IReadOnlyList<MtxcaBuyer> Buyers { get; init; } = [];
    public IReadOnlyList<long> Activities { get; init; } = [];

    public static MtxcaVoucherInput Read(XElement voucher) => new()
    {
        Element = voucher,
        Type = (int)(Long(voucher, "codigoTipoComprobante") ?? 0),
        PointOfSale = (int)(Long(voucher, "numeroPuntoVenta") ?? 0),
        Number = Long(voucher, "numeroComprobante") ?? 0,
        Date = DateOf(voucher, "fechaEmision"),
        AuthorizationType = Text(voucher, "codigoTipoAutorizacion"),
        AuthorizationCode = Long(voucher, "codigoAutorizacion"),
        AuthorizationDue = DateOf(voucher, "fechaVencimiento"),
        DocType = (int?)Long(voucher, "codigoTipoDocumento"),
        DocNumber = Long(voucher, "numeroDocumento"),
        ReceiverCondition = (int?)Long(voucher, "condicionIVAReceptor"),
        Net = Decimal(voucher, "importeGravado"),
        NotTaxed = Decimal(voucher, "importeNoGravado"),
        Exempt = Decimal(voucher, "importeExento"),
        Subtotal = Decimal(voucher, "importeSubtotal") ?? 0,
        OtherTaxesTotal = Decimal(voucher, "importeOtrosTributos"),
        Total = Decimal(voucher, "importeTotal") ?? 0,
        Currency = Text(voucher, "codigoMoneda") ?? "",
        Rate = Decimal(voucher, "cotizacionMoneda"),
        SameCurrency = Text(voucher, "cancelaEnMismaMonedaExtranjera"),
        Concept = (int)(Long(voucher, "codigoConcepto") ?? 0),
        ServiceFrom = DateOf(voucher, "fechaServicioDesde"),
        ServiceTo = DateOf(voucher, "fechaServicioHasta"),
        PaymentDue = DateOf(voucher, "fechaVencimientoPago"),
        GenerationTime = Text(voucher, "fechaHoraGen"),
        Associated = List(voucher, "arrayComprobantesAsociados").Select(a => new MtxcaAssociated(
            (int)(Long(a, "codigoTipoComprobante") ?? 0), (int)(Long(a, "numeroPuntoVenta") ?? 0), Long(a, "numeroComprobante") ?? 0,
            Long(a, "cuit"), DateOf(a, "fechaEmision"))).ToList(),
        Period = Child(voucher, "periodoComprobantesAsociados") is { } period && DateOf(period, "fechaDesde") is { } from && DateOf(period, "fechaHasta") is { } to
            ? (from, to)
            : null,
        OtherTaxes = List(voucher, "arrayOtrosTributos").Select(t => new MtxcaOtherTax(
            (int)(Long(t, "codigo") ?? 0), Text(t, "descripcion"), Decimal(t, "baseImponible") ?? 0, Decimal(t, "importe") ?? 0)).ToList(),
        Items = List(voucher, "arrayItems").Select(i => new MtxcaItem(
            (int?)Long(i, "unidadesMtx"), Text(i, "codigoMtx"), Text(i, "codigo"), Child(i, "descripcion")?.Value ?? "",
            Decimal(i, "cantidad"), (int)(Long(i, "codigoUnidadMedida") ?? -1), Decimal(i, "precioUnitario"),
            Decimal(i, "importeBonificacion"), (int)(Long(i, "codigoCondicionIVA") ?? 0), Decimal(i, "importeIVA"),
            Decimal(i, "importeItem") ?? 0)).ToList(),
        HasSubtotals = Child(voucher, "arraySubtotalesIVA") is not null,
        Subtotals = List(voucher, "arraySubtotalesIVA").Select(s => ((int)(Long(s, "codigo") ?? 0), Decimal(s, "importe") ?? 0)).ToList(),
        ExtraData = List(voucher, "arrayDatosAdicionales").Select(d => (int)(Long(d, "t") ?? 0)).ToList(),
        Buyers = List(voucher, "arrayCompradores").Select(b => new MtxcaBuyer(
            (int)(Long(b, "codigoTipoDocumento") ?? 0), Long(b, "numeroDocumento") ?? 0, Decimal(b, "porcentaje") ?? 0)).ToList(),
        Activities = List(voucher, "arrayActividades").Select(a => Long(a, "codigo") ?? 0).ToList(),
    };

    /// <summary>The voucher with every element unqualified, as ComprobanteType travels, ready to keep and answer back.</summary>
    public static XElement Unqualified(XElement element) =>
        new(element.Name.LocalName, element.HasElements ? element.Elements().Select(Unqualified) : element.Value);

    public static XElement? Child(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> List(XElement parent, string array) =>
        Child(parent, array)?.Elements() ?? [];

    private static string? Text(XElement parent, string name) =>
        Child(parent, name)?.Value.Trim() is { Length: > 0 } text ? text : null;

    private static long? Long(XElement parent, string name) =>
        long.TryParse(Text(parent, name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static decimal? Decimal(XElement parent, string name) =>
        decimal.TryParse(Text(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>An xsd:date, with or without a time zone after it.</summary>
    private static DateOnly? DateOf(XElement parent, string name) =>
        Text(parent, name) is { Length: >= 10 } text
        && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
