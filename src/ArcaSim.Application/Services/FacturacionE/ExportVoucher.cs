using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.FacturacionE;

public sealed record ExportPermit(string? Id, int Destination);

public sealed record ExportAssociated(int Type, int PointOfSale, long Number, long Cuit);

public sealed record ExportItem(string? Code, string? Description, decimal Quantity, int Unit, decimal UnitPrice, decimal Discount, decimal Total);

public sealed record ExportOptional(string? Id, string? Value);

/// <summary>
/// A FEXAuthorize request's Cmp (ClsFEXRequest), field by field as .NET reads
/// it: a missing number is 0, a missing string is null, and an array is null
/// when its element was not sent, empty when it came without items.
/// </summary>
public sealed record ExportVoucher(
    long Id,
    string? Date,
    int VoucherType,
    int PointOfSale,
    long Number,
    int ExportType,
    string? PermitExists,
    IReadOnlyList<ExportPermit>? Permits,
    int Destination,
    string? Client,
    long ClientCountryCuit,
    string? ClientAddress,
    string? ClientTaxId,
    string? Currency,
    decimal? Rate,
    string? SameCurrency,
    string? CommercialNotes,
    decimal Total,
    string? Notes,
    IReadOnlyList<ExportAssociated>? Associated,
    string? PaymentTerms,
    string? Incoterms,
    string? IncotermsText,
    int Language,
    IReadOnlyList<ExportItem>? Items,
    IReadOnlyList<ExportOptional>? Optionals,
    string? PaymentDate,
    IReadOnlyList<long>? Activities)
{
    public static ExportVoucher Read(XElement? cmp) => new(
        cmp.LongOf("Id"),
        cmp.Str("Fecha_cbte"),
        cmp.IntOf("Cbte_Tipo"),
        cmp.IntOf("Punto_vta"),
        cmp.LongOf("Cbte_nro"),
        cmp.IntOf("Tipo_expo"),
        cmp.Str("Permiso_existente"),
        cmp.List("Permisos", "Permiso", p => new ExportPermit(p.Str("Id_permiso"), p.IntOf("Dst_merc"))),
        cmp.IntOf("Dst_cmp"),
        cmp.Str("Cliente"),
        cmp.LongOf("Cuit_pais_cliente"),
        cmp.Str("Domicilio_cliente"),
        cmp.Str("Id_impositivo"),
        cmp.Str("Moneda_Id"),
        cmp.Child("Moneda_ctz") is null ? null : cmp.DecimalOf("Moneda_ctz"),
        cmp.Str("CanMisMonExt"),
        cmp.Str("Obs_comerciales"),
        cmp.DecimalOf("Imp_total"),
        cmp.Str("Obs"),
        cmp.List("Cmps_asoc", "Cmp_asoc", a => new ExportAssociated(a.IntOf("Cbte_tipo"), a.IntOf("Cbte_punto_vta"), a.LongOf("Cbte_nro"), a.LongOf("Cbte_cuit"))),
        cmp.Str("Forma_pago"),
        cmp.Str("Incoterms"),
        cmp.Str("Incoterms_Ds"),
        cmp.IntOf("Idioma_cbte"),
        cmp.List("Items", "Item", i => new ExportItem(
            i.Str("Pro_codigo"), i.Str("Pro_ds"), i.DecimalOf("Pro_qty"), i.IntOf("Pro_umed"),
            i.DecimalOf("Pro_precio_uni"), i.DecimalOf("Pro_bonificacion"), i.DecimalOf("Pro_total_item"))),
        cmp.List("Opcionales", "Opcional", o => new ExportOptional(o.Str("Id"), o.Str("Valor"))),
        cmp.Str("Fecha_pago"),
        cmp.List("Actividades", "Actividad", a => a.LongOf("Id")));
}

/// <summary>An export voucher with its CAE, as FEXAuthorize answered it and FEXGetCMP gives it back.</summary>
public sealed record AuthorizedExport(
    long Cuit,
    ExportVoucher Voucher,
    string Date,
    string Cae,
    string CaeDue,
    string AuthorizedOn,
    decimal Rate);

/// <summary>
/// Reading WSFEXv1's requests by exact element name (ContractXml's Child), as
/// the .NET deserializer does: Cbte_Tipo and Cbte_tipo are different fields. What
/// is here is what only this service needs: an element with xsi:nil is a
/// missing one, and a number that does not read is 0.
/// </summary>
internal static class FexXml
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public static string? Str(this XElement? element, string name) => element.Child(name) is { } child && !IsNil(child) ? child.Value : null;

    public static long LongOf(this XElement? element, string name) =>
        long.TryParse(element.Str(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static int IntOf(this XElement? element, string name) =>
        int.TryParse(element.Str(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static decimal DecimalOf(this XElement? element, string name) =>
        decimal.TryParse(element.Str(name)?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>The items of an array element: null when the array was not sent; nil items are left out.</summary>
    public static IReadOnlyList<T>? List<T>(this XElement? element, string array, string item, Func<XElement, T> read) =>
        element.Child(array) is { } holder
            ? holder.Elements().Where(e => e.Name.LocalName == item && !IsNil(e)).Select(read).ToList()
            : null;

    private static bool IsNil(XElement element) => (string?)element.Attribute(Xsi + "nil") == "true";
}
