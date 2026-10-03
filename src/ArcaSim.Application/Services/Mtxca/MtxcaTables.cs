using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Mtxca;

/// <summary>A voucher type wsmtxca authorizes, with what decides its rules: whether VAT goes per item (class A) and whether it is an FCE.</summary>
public sealed record MtxcaVoucherType(int Id, string Description)
{
    /// <summary>Class A, A with the retention legend and FCE A: the price goes without VAT and each item carries importeIVA.</summary>
    public bool ClassA => Id is 1 or 2 or 3 or 51 or 52 or 53 or 201 or 202 or 203;

    /// <summary>The types 129 and 128 ask a CUIT for: everything but plain class B.</summary>
    public bool NeedsCuit => Id is not (6 or 7 or 8);

    public bool Fce => Id >= 201;

    public bool Invoice => Id is 1 or 6 or 51 or 201 or 206;

    public bool CreditNote => Id is 3 or 8 or 53 or 203 or 208;

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
    public static readonly IReadOnlyList<MtxcaVoucherType> VoucherTypes =
    [
        new(1, "Factura A"),
        new(2, "Nota de Débito A"),
        new(3, "Nota de Crédito A"),
        new(6, "Factura B"),
        new(7, "Nota de Débito B"),
        new(8, "Nota de Crédito B"),
        new(51, "Factura A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        new(52, "Nota de Débito A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        new(53, "Nota de Crédito A con leyenda OPERACIÓN SUJETA A RETENCIÓN"),
        new(201, "Factura de Crédito Electrónica MiPyMEs (FCE) A"),
        new(202, "Nota de Débito Electrónica MiPyMEs (FCE) A"),
        new(203, "Nota de Crédito Electrónica MiPyMEs (FCE) A"),
        new(206, "Factura de Crédito Electrónica MiPyMEs (FCE) B"),
        new(207, "Nota de Débito Electrónica MiPyMEs (FCE) B"),
        new(208, "Nota de Crédito Electrónica MiPyMEs (FCE) B"),
    ];

    /// <summary>Condición de IVA of an item (consultarCondicionesIVA, pág. 285) and the rate it stands for.</summary>
    public static readonly IReadOnlyDictionary<int, decimal> ItemVatRates = new Dictionary<int, decimal>
    {
        [1] = 0m, [2] = 0m, [3] = 0m, [4] = 0.105m, [5] = 0.21m, [6] = 0.27m,
    };

    /// <summary>Remitos and other vouchers that may be associated (validations 200 and 203).</summary>
    public static readonly IReadOnlySet<int> AssociableTypes = new HashSet<int> { 1, 2, 3, 6, 7, 8, 51, 52, 53, 201, 202, 203, 206, 207, 208, 88, 91, 990, 995 };

    /// <summary>Currencies whose rate the Banco Nación publishes (Anexo, pág. 360-361), written with wsmtxca's three-character codes.</summary>
    public static readonly IReadOnlySet<string> BnaCurrencies = new HashSet<string> { "DOL", "002", "009", "014", "015", "016", "018", "019", "021", "026", "060", "064" };

    public static readonly IReadOnlyList<MtxcaRow> Units =
    [
        new(0, "SIN DESCRIPCION"), new(1, "KILOGRAMO"), new(2, "METROS"), new(3, "METRO CUADRADO"), new(4, "METRO CUBICO"),
        new(5, "LITROS"), new(6, "1000 KILOWATT HORA"), new(7, "UNIDAD"), new(8, "PAR"), new(9, "DOCENA"), new(10, "QUILATE"),
        new(11, "MILLAR"), new(12, "MEGA U. INTER. ACT. ANTIB"), new(13, "UNIDAD INT. ACT. INMUNG"), new(14, "GRAMO"),
        new(15, "MILIMETRO"), new(16, "MILIMETRO CUBICO"), new(17, "KILOMETRO"), new(18, "HECTOLITRO"),
        new(19, "MEGA UNIDAD INT. ACT. INMUNG"), new(20, "CENTIMETRO"), new(21, "KILOGRAMO ACTIVO"), new(22, "GRAMO ACTIVO"),
        new(23, "GRAMO BASE"), new(24, "UIACTHOR"), new(25, "JGO.PQT. MAZO NAIPES"), new(26, "MUIACTHOR"),
        new(27, "CENTIMETRO CUBICO"), new(28, "UIACTANT"), new(29, "TONELADA"), new(30, "DECAMETRO CUBICO"),
        new(31, "HECTOMETRO CUBICO"), new(32, "KILOMETRO CUBICO"), new(33, "MICROGRAMO"), new(34, "NANOGRAMO"),
        new(35, "PICOGRAMO"), new(36, "MUIACTANT"), new(37, "UIACTIG"), new(41, "MILIGRAMO"), new(47, "MILILITRO"),
        new(48, "CURIE"), new(49, "MILICURIE"), new(50, "MICROCURIE"), new(51, "U.INTER. ACT. HORMONAL"),
        new(52, "MEGA U. INTER. ACT. HOR."), new(53, "KILOGRAMO BASE"), new(54, "GRUESA"), new(55, "MUIACTIG"),
        new(61, "KILOGRAMO BRUTO"), new(62, "PACK"), new(63, "HORMA"), new(95, "ANULACIÓN/DEVOLUCIÓN"),
        new(97, "SEÑAS/ANTICIPOS"), new(98, "OTRAS UNIDADES"), new(99, "BONIFICACION"),
    ];

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

    public static MtxcaVoucherType? VoucherType(int id) => VoucherTypes.FirstOrDefault(t => t.Id == id);

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
