using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

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
        Type = (int)(voucher.ChildLong("codigoTipoComprobante") ?? 0),
        PointOfSale = (int)(voucher.ChildLong("numeroPuntoVenta") ?? 0),
        Number = voucher.ChildLong("numeroComprobante") ?? 0,
        Date = DateOf(voucher, "fechaEmision"),
        AuthorizationType = Text(voucher, "codigoTipoAutorizacion"),
        AuthorizationCode = voucher.ChildLong("codigoAutorizacion"),
        AuthorizationDue = DateOf(voucher, "fechaVencimiento"),
        DocType = (int?)voucher.ChildLong("codigoTipoDocumento"),
        DocNumber = voucher.ChildLong("numeroDocumento"),
        ReceiverCondition = (int?)voucher.ChildLong("condicionIVAReceptor"),
        Net = voucher.ChildDecimal("importeGravado"),
        NotTaxed = voucher.ChildDecimal("importeNoGravado"),
        Exempt = voucher.ChildDecimal("importeExento"),
        Subtotal = voucher.ChildDecimal("importeSubtotal") ?? 0,
        OtherTaxesTotal = voucher.ChildDecimal("importeOtrosTributos"),
        Total = voucher.ChildDecimal("importeTotal") ?? 0,
        Currency = Text(voucher, "codigoMoneda") ?? "",
        Rate = voucher.ChildDecimal("cotizacionMoneda"),
        SameCurrency = Text(voucher, "cancelaEnMismaMonedaExtranjera"),
        Concept = (int)(voucher.ChildLong("codigoConcepto") ?? 0),
        ServiceFrom = DateOf(voucher, "fechaServicioDesde"),
        ServiceTo = DateOf(voucher, "fechaServicioHasta"),
        PaymentDue = DateOf(voucher, "fechaVencimientoPago"),
        GenerationTime = Text(voucher, "fechaHoraGen"),
        Associated = ArrayItems(voucher, "arrayComprobantesAsociados").Select(a => new MtxcaAssociated(
            (int)(a.ChildLong("codigoTipoComprobante") ?? 0), (int)(a.ChildLong("numeroPuntoVenta") ?? 0), a.ChildLong("numeroComprobante") ?? 0,
            a.ChildLong("cuit"), DateOf(a, "fechaEmision"))).ToList(),
        Period = voucher.Child("periodoComprobantesAsociados") is { } period && DateOf(period, "fechaDesde") is { } from && DateOf(period, "fechaHasta") is { } to
            ? (from, to)
            : null,
        OtherTaxes = ArrayItems(voucher, "arrayOtrosTributos").Select(t => new MtxcaOtherTax(
            (int)(t.ChildLong("codigo") ?? 0), Text(t, "descripcion"), t.ChildDecimal("baseImponible") ?? 0, t.ChildDecimal("importe") ?? 0)).ToList(),
        Items = ArrayItems(voucher, "arrayItems").Select(i => new MtxcaItem(
            (int?)i.ChildLong("unidadesMtx"), Text(i, "codigoMtx"), Text(i, "codigo"), i.Child("descripcion")?.Value ?? "",
            i.ChildDecimal("cantidad"), (int)(i.ChildLong("codigoUnidadMedida") ?? -1), i.ChildDecimal("precioUnitario"),
            i.ChildDecimal("importeBonificacion"), (int)(i.ChildLong("codigoCondicionIVA") ?? 0), i.ChildDecimal("importeIVA"),
            i.ChildDecimal("importeItem") ?? 0)).ToList(),
        HasSubtotals = voucher.Child("arraySubtotalesIVA") is not null,
        Subtotals = ArrayItems(voucher, "arraySubtotalesIVA").Select(s => ((int)(s.ChildLong("codigo") ?? 0), s.ChildDecimal("importe") ?? 0)).ToList(),
        ExtraData = ArrayItems(voucher, "arrayDatosAdicionales").Select(d => (int)(d.ChildLong("t") ?? 0)).ToList(),
        Buyers = ArrayItems(voucher, "arrayCompradores").Select(b => new MtxcaBuyer(
            (int)(b.ChildLong("codigoTipoDocumento") ?? 0), b.ChildLong("numeroDocumento") ?? 0, b.ChildDecimal("porcentaje") ?? 0)).ToList(),
        Activities = ArrayItems(voucher, "arrayActividades").Select(a => a.ChildLong("codigo") ?? 0).ToList(),
    };

    /// <summary>The voucher with every element unqualified, as ComprobanteType travels, ready to keep and answer back.</summary>
    public static XElement Unqualified(XElement element) =>
        new(element.Name.LocalName, element.HasElements ? element.Elements().Select(Unqualified) : element.Value);

    /// <summary>The items of an array element, whatever they are called; none when the voucher did not send it.</summary>
    private static IEnumerable<XElement> ArrayItems(XElement parent, string array) =>
        parent.Child(array)?.Elements() ?? [];

    /// <summary>The direct child's text; null when it is missing or empty, which counts as not sent.</summary>
    private static string? Text(XElement parent, string name) =>
        parent.ChildText(name) is { Length: > 0 } text ? text : null;

    /// <summary>An xsd:date, with or without a time zone after it.</summary>
    private static DateOnly? DateOf(XElement parent, string name) =>
        Text(parent, name) is { Length: >= 10 } text
        && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
