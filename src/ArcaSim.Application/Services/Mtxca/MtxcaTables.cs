using ArcaSim.Application.Services.FacturacionE;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Mtxca;

/// <summary>
/// A voucher type wsmtxca authorizes: WSFEv1's row of the table for it (so both
/// services agree on its class, kind and FCE flag) with the description wsmtxca
/// gives it. What decides its rules is read from that row.
/// </summary>
public sealed record MtxcaVoucherType(VoucherTypeInfo Info, string Description)
{
    public int Id => Info.Id;

    /// <summary>Class A, A with the retention legend and FCE A: the price goes without VAT and each item carries importeIVA.</summary>
    public bool ClassA => Info.Class is VoucherClass.A or VoucherClass.ALey;

    /// <summary>Class B and not an FCE: the types 6, 7 and 8, which identify the receiver by the amount instead of always asking for a CUIT.</summary>
    public bool PlainB => Info.Class == VoucherClass.B && !Info.Fce;

    /// <summary>The types 129 and 128 ask a CUIT for: everything but plain class B (an FCE B asks for one).</summary>
    public bool NeedsCuit => !PlainB;

    public bool Fce => Info.Fce;

    public bool Invoice => Info.Kind == VoucherKind.Invoice;

    public bool Note => !Invoice;
}

public sealed record MtxcaRow(int Code, string Description);

/// <summary>
/// The tables wsmtxca answers its consultar* operations with and validates
/// against (docs/arca/servicios/wsmtxca.md, "Tablas y datos"). Where wsmtxca
/// uses ARCA's generic tables (documents, currencies, taxes, VAT rates,
/// receiver conditions) they come from WSFEv1's ParameterTables, so both
/// services agree. The manual only shows the first rows of most lists, so the
/// full lists are ArcaSim's choice:
/// - voucher types: the 15 that validation 100 accepts, FCE included, although
///   the manual's example of consultarTiposComprobante shows only nine;
/// - units: the generic table (wsfexv1.md) plus 95, which the validations use
///   for cancellations and returns; its description is ArcaSim's;
/// - additional data: the types validations 320 to 340 name, described with
///   the manual's words; 23's description is wsfev1's "Referencia Comercial";
/// - receiver conditions for class B: the manual's example (4, 5, 7, 10, 15),
///   which is wsfev1's B list without the foreign ones (8, 9).
/// </summary>
public sealed class MtxcaTables(ParameterTables tables)
{
    /// <summary>The voucher types wsmtxca authorizes and the descriptions it gives them.</summary>
    private static readonly (int Id, string Description)[] VoucherTypeDescriptions =
    [
        (1, "Factura A"),
        (2, "Nota de Débito A"),
        (3, "Nota de Crédito A"),
        (6, "Factura B"),
        (7, "Nota de Débito B"),
        (8, "Nota de Crédito B"),
        (51, "Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        (52, "Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        (53, "Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        (201, "Factura de Crédito Electrónica MiPyMEs (FCE) A"),
        (202, "Nota de Débito Electrónica MiPyMEs (FCE) A"),
        (203, "Nota de Crédito Electrónica MiPyMEs (FCE) A"),
        (206, "Factura de Crédito Electrónica MiPyMEs (FCE) B"),
        (207, "Nota de Débito Electrónica MiPyMEs (FCE) B"),
        (208, "Nota de Crédito Electrónica MiPyMEs (FCE) B"),
    ];

    public IReadOnlyList<MtxcaVoucherType> VoucherTypes { get; } = VoucherTypeDescriptions
        .Select(t => new MtxcaVoucherType(
            tables.VoucherType(t.Id) ?? throw new InvalidOperationException($"WSFEv1's table has no voucher type {t.Id}."), t.Description))
        .ToList();

    /// <summary>Condición de IVA of an item (consultarCondicionesIVA, pág. 285) and the rate it stands for.</summary>
    public static readonly IReadOnlyDictionary<int, decimal> ItemVatRates = new Dictionary<int, decimal>
    {
        [1] = 0m, [2] = 0m, [3] = 0m, [4] = 0.105m, [5] = 0.21m, [6] = 0.27m,
    };

    /// <summary>Remitos and other vouchers that may be associated (validations 200 and 203).</summary>
    public static readonly IReadOnlySet<int> AssociableTypes = new HashSet<int> { 1, 2, 3, 6, 7, 8, 51, 52, 53, 201, 202, 203, 206, 207, 208, 88, 91, 990, 995 };

    /// <summary>Currencies whose rate the Banco Nación publishes (Anexo, pág. 360-361), written with wsmtxca's three-character codes.</summary>
    public static readonly IReadOnlySet<string> BnaCurrencies = new HashSet<string> { "DOL", "002", "009", "014", "015", "016", "018", "019", "021", "026", "060", "064" };

    /// <summary>The generic units table WSFEXv1 answers (Wsfexv1Tables.Units) plus 95, which the validations use for cancellations and returns.</summary>
    public static readonly IReadOnlyList<MtxcaRow> Units =
        Wsfexv1Tables.Units.Append((Id: 95, Desc: "ANULACIÓN/DEVOLUCIÓN")).OrderBy(u => u.Id).Select(u => new MtxcaRow(u.Id, u.Desc)).ToList();

    public static readonly IReadOnlyList<MtxcaRow> ExtraDataTypes =
    [
        new(1, "Entes Reguladores"),
        new(2, "Dato Adicional para Empresas Promovidas"),
        new(5, "Motivo de Excepcion - Cómputo IVA Crédito Fiscal"),
        new(10, "Dato Adicional para Educación Pública de Gestión Privada"),
        new(11, "Dato Adicional para Operaciones Económicas Relacionadas con Bienes Inmuebles"),
        new(12, "Dato Adicional para Locacion temporaria de Inmuebles con fines Turisticos"),
        new(13, "Dato Adicional para Representantes de Modelos"),
        new(14, "Dato Adicional para Agencias de Publicidad"),
        new(15, "Dato Adicional para Personas Físicas que desarrollen actividad de Modelaje"),
        new(21, "CBU y Alias del Emisor"),
        new(22, "Anulación"),
        new(23, "Referencia Comercial"),
        new(27, "Opción de Transferencia"),
    ];

    public MtxcaVoucherType? VoucherType(int id) => VoucherTypes.FirstOrDefault(t => t.Id == id);

    public static bool HasUnit(int code) => Units.Any(u => u.Code == code);

    public static bool HasExtraDataType(int code) => ExtraDataTypes.Any(t => t.Code == code);

    public IEnumerable<MtxcaRow> DocumentTypes => tables.DocumentTypes.Select(d => new MtxcaRow(int.Parse(d.Id), d.Desc));

    public bool HasDocumentType(int id) => tables.HasDocumentType(id);

    /// <summary>The VAT rates a subtotal may carry (consultarAlicuotasIVA, pág. 280): 0, 10.5, 21 and 27 %.</summary>
    public IEnumerable<MtxcaRow> VatRates => new[] { 3, 4, 5, 6 }.Select(id => new MtxcaRow(id, tables.Vat(id)?.Desc ?? ""));

    public IEnumerable<MtxcaRow> ItemVatConditions =>
        new[] { new MtxcaRow(1, "No gravado"), new MtxcaRow(2, "Exento") }.Concat(VatRates);

    public IEnumerable<(string Code, string Description)> Currencies => tables.Currencies.Select(c => (c.Id, c.Desc));

    public bool HasCurrency(string code) => tables.HasCurrency(code);

    public IEnumerable<MtxcaRow> Taxes => tables.Taxes.Select(t => new MtxcaRow(int.Parse(t.Id), t.Desc));

    public bool HasTax(int id) => tables.HasTax(id);

    /// <summary>The receiver conditions that may go on a voucher of this type (consultarCondicionesIVAReceptor).</summary>
    public IEnumerable<MtxcaRow> ReceiverConditions(MtxcaVoucherType type) =>
        tables.ReceiverVatConditions.Where(c => Allows(c, type.ClassA)).Select(c => new MtxcaRow(c.Id, c.Desc));

    /// <summary>A receiver condition the service knows for some voucher type (190).</summary>
    public bool IsReceiverCondition(int id) =>
        tables.ReceiverVatCondition(id) is { } condition && (Allows(condition, classA: true) || Allows(condition, classA: false));

    private static bool Allows(ReceiverVatConditionRow condition, bool classA) =>
        classA ? condition.Classes.Contains("A") : condition.Classes.Contains("B") && condition.Id is not (8 or 9);
}
