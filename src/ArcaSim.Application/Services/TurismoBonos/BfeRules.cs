using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>
/// WSBFEv1, Bonos Fiscales Electrónicos (RG 5427/2023 and RG 2861), following
/// docs/arca/servicios/wsbfev1.md: the shared flow of AsmxVoucherRules plus
/// the Factura de Crédito Electrónica MiPyMEs rules (Fecha_vto_pago, Opcionales,
/// CbtesAsoc with Cuit and date) and BFEGetPARAM_Tipo_Opc.
/// </summary>
public sealed class WsbfeV1Rules(
    ParameterTables parameters, IDocumentStore documents, IExchangeRates rates, IAuthorizationCodes codes,
    SequenceLocks locks, IClock clock, SimulationSettings settings, EventManager events)
    : BfeRules(parameters, documents, rates, codes, locks, clock, settings, events)
{
    public override string Service => "wsbfev1";

    protected override bool CreditInvoices => true;
}

/// <summary>
/// WSBFE, the previous version of the Bonos Fiscales Electrónicos service,
/// following docs/arca/servicios/wsbfe.md: wsbfev1 without Factura de Crédito
/// Electrónica (types 1, 2, 3, 6, 7 and 8 only; Opcionales and Fecha_vto_pago
/// do not exist and are ignored if they come). Its manual (V1.1) predates the
/// RG 5616 fields; ArcaSim checks them with wsbfev1's codes (4957-4967), the
/// spec's most plausible reading. In homologación every answer carries event
/// 102, the RG 5616 notice the service shows today.
/// </summary>
public sealed class WsbfeRules(
    ParameterTables parameters, IDocumentStore documents, IExchangeRates rates, IAuthorizationCodes codes,
    SequenceLocks locks, IClock clock, SimulationSettings settings, EventManager events)
    : BfeRules(parameters, documents, rates, codes, locks, clock, settings, events)
{
    public const string Rg5616Notice =
        "IMPORTANTE: El dia 9 de junio de 2025 se actualizo la version del Web Service (WS) en el ambiente de Homologacion Externa en la cual " +
        "se establece como obligatorio el campo Condicion Frente al IVA del receptor. Cabe destacar que la Resolucion General Nro 5616 indica " +
        "que ese dato debe enviarse de manera obligatoria. Para mas informacion, consultar el manual en: https://www.arca.gob.ar/fe/ayuda/webservice.asp, " +
        "https://www.arca.gob.ar/ws/documentacion/ws-factura-electronica.asp";

    public override string Service => "wsbfe";

    protected override bool CreditInvoices => false;

    protected override AsmxEvent Event(ServiceCall call) =>
        Settings.Environment == ArcaEnvironment.Homologacion ? new AsmxEvent(102, Rg5616Notice) : base.Event(call);
}

/// <summary>
/// What wsbfev1 and wsbfe share (docs/arca/servicios/wsbfev1.md and wsbfe.md).
/// BFEAuthorize checks, in this order, and answers the first that fails:
/// the Id, type, point of sale and number ranges and the document of a class A
/// voucher (1014); the date (1014, or 4897/4898 for FCE); the currency (1014),
/// CanMisMonExt (4959, 4958) and the rate (4957, 4960); the receiver's VAT
/// condition (4963, 4961, 4962); the items' VAT rate and unit (1014), the
/// exempt amount and the items' total (1014); the associated vouchers
/// (1030-1039; 4886, 4888 for FCE); and, in wsbfev1, the FCE rules on
/// Fecha_vto_pago and Opcionales (4900, 4902, 4905, 4954, 4955, 4916, 1015-1019,
/// 4906-4908, 4953). The parameter queries answer the 2021 homologación tables.
///
/// ArcaSim's choices on what the specs leave NO VERIFICADO:
/// - wsbfe and wsbfev1 share one book: the same WSAA id (wsbfe), the same
///   business service and the same data, so a voucher authorized by one is
///   numbered after the other's and BFEGetCMP and BFEGetLast_* see it from both.
/// - RG 5616 is in force (manual V3.2): a missing CondicionIVAReceptorId is
///   rejected with 4963, not observed with 26.
/// - Imp_moneda_ctz may be left out only when CanMisMonExt is S, the voucher is
///   an invoice and ArcaSim has a rate for the currency (4957); "no podrá
///   superar en 1 a la cotización oficial" (4960) is read as one unit above the
///   latest rate ArcaSim has. The 1014 band against the reference rate is not
///   checked: three manuals give three bands.
/// - The NCM code is not checked: ArcaSim does not have the 3209-row table, and
///   BFEGetPARAM_NCM answers only the codes seen in approved vouchers.
/// - Zona is not checked (whether 0 is accepted is unknown).
/// - Rules that need ARCA's data (CBU registry, FCE current account and
///   thresholds, Promoción Industrial projects, categorization) are not applied.
/// - BFEGetCotizacion answers the latest rate on or before the date asked
///   (FchCotiz is that rate's day); PES is 1.
/// - Opc_Ds of BFEGetPARAM_Tipo_Opc are wsfev1's descriptions of the same ids.
/// </summary>
public abstract class BfeRules(
    ParameterTables parameters, IDocumentStore documents, IExchangeRates rates, IAuthorizationCodes codes,
    SequenceLocks locks, IClock clock, SimulationSettings settings, EventManager events)
    : AsmxVoucherRules(documents, codes, locks, clock, settings, events)
{
    protected override string Prefix => "BFE";

    protected override string Family => "wsbfe";

    /// <summary>Whether the service takes Factura de Crédito Electrónica MiPyMEs (wsbfev1) or not (wsbfe).</summary>
    protected abstract bool CreditInvoices { get; }

    /// <summary>BFEGetPARAM_Tipo_Cbte [CAS 2021]: id, description, validity start and class.</summary>
    private static readonly (int Id, string Description, string From, string Class)[] AllTypes =
    [
        (1, "Factura A", "20090620", "A"), (2, "Nota de Débito A", "20090620", "A"), (3, "Nota de Crédito A", "20090620", "A"),
        (6, "Factura B", "20090620", "B"), (7, "Nota de Débito B", "20090620", "B"), (8, "Nota de Crédito B", "20090620", "B"),
        (201, "Factura de Crédito electrónica MiPyMEs (FCE) A", "20190112", "A"),
        (202, "Nota de Débito electrónica MiPyMEs (FCE) A", "20190112", "A"),
        (203, "Nota de Crédito electrónica MiPyMEs (FCE) A", "20190112", "A"),
        (206, "Factura de Crédito electrónica MiPyMEs (FCE) B", "20190112", "B"),
        (207, "Nota de Débito electrónica MiPyMEs (FCE) B", "20190112", "B"),
        (208, "Nota de Crédito electrónica MiPyMEs (FCE) B", "20190112", "B"),
    ];

    /// <summary>BFEGetPARAM_Tipo_IVA [CAS 2021]; 2 is the exempt rate of the Imp_op_ex rule.</summary>
    private static readonly (int Id, string Description)[] VatRates =
        [(1, "No gravado"), (2, "Exento"), (3, "0%"), (4, "10.5%"), (5, "21%"), (6, "27%")];

    /// <summary>BFEGetPARAM_UMed [CAS 2021]: 0 and 98 have their own start, the rest 20080704.</summary>
    private static readonly (int Id, string Description)[] Units =
    [
        (41, "miligramos"), (14, "gramos"), (1, "kilogramos"), (29, "toneladas"), (10, "quilates"), (47, "mililitros"), (5, "litros"),
        (27, "cm cúbicos"), (15, "milimetros"), (20, "centímetros"), (17, "kilómetros"), (7, "unidades"), (8, "pares"), (9, "docenas"),
        (11, "millares"), (96, "packs"), (97, "hormas"), (2, "metros"), (3, "metros cuadrados"), (4, "metros cúbicos"), (6, "1000 kWh"),
        (99, "otras unidades"), (16, "mm cúbicos"), (18, "hectolitros"), (25, "jgo. pqt. mazo naipes"), (30, "dam cúbicos"),
        (31, "hm cúbicos"), (32, "km cúbicos"), (33, "microgramos"), (34, "nanogramos"), (35, "picogramos"), (48, "curie"),
        (49, "milicurie"), (50, "microcurie"), (51, "uiacthor"), (52, "muiacthor"), (53, "kg base"), (54, "gruesa"), (61, "kg bruto"),
        (62, "uiactant"), (63, "muiactant"), (64, "uiactig"), (65, "muiactig"), (66, "kg activo"), (67, "gramo activo"), (68, "gramo base"),
        (0, ""), (98, "otras unidades"),
    ];

    /// <summary>The NCM codes of the approved vouchers seen in homologación, and the wildcard (wsbfev1.md, Productos NCM).</summary>
    private static readonly (string Code, string Description)[] Products =
        [("2101.11.10", "NULL"), ("7308.10.00", "NULL"), ("7308.20.00", "NULL"), ("9999.99.99", "(item no incluído en el Beneficio Fiscal)")];

    /// <summary>The optional ids the manual names (wsbfev1.md, Opcionales).</summary>
    private static readonly string[] OptionalIds = ["2", "22", "23", "27", "2101", "2102"];

    private static readonly string[] FceOptionals = ["2101", "2102", "22", "27"];

    private IEnumerable<(int Id, string Description, string From, string Class)> Types =>
        CreditInvoices ? AllTypes : AllTypes.Where(t => t.Id < 200);

    private static bool IsFce(int type) => type > 200;

    private static bool IsFceInvoice(int type) => type is 201 or 206;

    private static bool IsInvoice(int type) => type is 1 or 6 or 201 or 206;

    protected override Task<ContractAnswer?> OtherAsync(ServiceCall call, CancellationToken ct)
    {
        var ns = Ns(call);
        return call.Name switch
        {
            "BFEGetPARAM_Tipo_Cbte" => Done(Table(call, Types.Select(t => Row(ns, "ClsBFEResponse_Tipo_Cbte", "Cbte", t.Id, t.Description, t.From)))),
            "BFEGetPARAM_Tipo_doc" => Done(Table(call, [Row(ns, "ClsBFEResponse_Tipo_doc", "Doc", 80, "CUIT", "20090620")])),
            "BFEGetPARAM_Tipo_IVA" => Done(Table(call, VatRates.Select(r => Row(ns, "ClsBFEResponse_Tipo_IVA", "IVA", r.Id, r.Description, "20090220")))),
            "BFEGetPARAM_Zonas" => Done(Table(call, [Row(ns, "ClsBFEResponse_Zon", "Zon", 1, "Nacional", "20090215")])),
            "BFEGetPARAM_UMed" => Done(Table(call, Units.Select(u => Row(ns, "ClsBFEResponse_UMed", "Umed", u.Id, u.Description,
                u.Id switch { 0 => "20091211", 98 => "20201022", _ => "20080704" })))),
            "BFEGetPARAM_MON" => Done(Table(call, parameters.Currencies.Select(c => Row(ns, "ClsBFEResponse_Mon", "Mon", c.Id, c.Desc, c.From, c.To)))),
            "BFEGetPARAM_NCM" => Done(Table(call, Products.Select(p => new XElement(ns + "ClsBFEResponse_NCM",
                new XElement(ns + "NCM_Codigo", p.Code), new XElement(ns + "NCM_Ds", p.Description), new XElement(ns + "NCM_Nota", "Bonos Fisc."),
                new XElement(ns + "NCM_vig_desde", "20090215"), new XElement(ns + "NCM_vig_hasta", "NULL"))))),
            "BFEGetPARAM_Tipo_Opc" => Done(Table(call, parameters.Optionals.Where(o => OptionalIds.Contains(o.Id))
                .Select(o => Row(ns, "ClsBFEResponse_Opc", "Opc", o.Id, o.Desc, o.From, o.To)))),
            "BFEGetPARAM_CondicionIvaReceptor" => Done(Conditions(call)),
            "BFEGetCotizacion" => QuoteAsync(call, ct),
            _ => Task.FromResult<ContractAnswer?>(null),
        };
    }

    private static Task<ContractAnswer?> Done(ContractAnswer answer) => Task.FromResult<ContractAnswer?>(answer);

    /// <summary>The annex, all of it or the class asked; another class is 4967 (manual V3.2).</summary>
    private ContractAnswer Conditions(ServiceCall call)
    {
        var ns = Ns(call);
        var wanted = call.Request.Field("ClaseCmp");
        if (!string.IsNullOrEmpty(wanted) && wanted is not ("A" or "B"))
            return Refuse(call, new AsmxRefusal(4967,
                "El valor ingresado para la clase de comprobante no es valido. La clase de Comprobante es opcional, de ingresar un valor solo puede ser A o B"));
        return Table(call, ReceiverConditions.Annex.Where(c => string.IsNullOrEmpty(wanted) || c.Class == wanted)
            .Select(c => new XElement(ns + "ClsBFEResponse_CondicionIvaReceptor",
                new XElement(ns + "Id", c.Id), new XElement(ns + "Desc", c.Description), new XElement(ns + "Cmp_Clase", c.Class))));
    }

    private async Task<ContractAnswer?> QuoteAsync(ServiceCall call, CancellationToken ct)
    {
        var currency = call.Request.Field("MonId");
        if (string.IsNullOrEmpty(currency) || parameters.Currencies.All(c => c.Id != currency))
            return Refuse(call, new AsmxRefusal(4965,
                "El identificador de moneda (MonId) ingresado es invalido. Este campo es obligatorio y no puede quedar vacío. Verificar los códigos mediante el metodo BFEGetPARAM_MON."));
        var asked = call.Request.Field("FchCotiz");
        DateOnly day;
        if (string.IsNullOrEmpty(asked)) day = Clock.Today();
        else if (Figures.ParseDay(asked) is { } parsed && asked.Length == 8) day = parsed;
        else
            return Refuse(call, new AsmxRefusal(4966,
                "Campo FchCotiz no corresponde a una fecha valida con formato YYYYMMDD. Este campo es opcional, de informarlo la fecha debe tener el formato YYYYMMDD donde YYYY corresponde al año, MM al mes y DD al día solicitado. De no informarlo se tomara la fecha del día actual como valor por default."));

        var quote = currency == "PES" ? (1m, day) : await rates.RateAsync(currency, day, ct);
        if (quote is not { } found)
            return Refuse(call, new AsmxRefusal(4964, "Sin Resultados. A la fecha consultada no se registran valores de cotización para la moneda indicada"));
        var ns = Ns(call);
        return Answer(call, new XElement(ns + "BFEResultGet",
            new XElement(ns + "MonId", currency), new XElement(ns + "MonCotiz", Figures.Number(found.Rate)), new XElement(ns + "FchCotiz", Figures.Day(found.Day))));
    }

    // ---- BFEAuthorize ----------------------------------------------------------------------

    protected override async Task<AsmxRefusal?> CheckAsync(ServiceCall call, AsmxCmp cmp, DateOnly today, CancellationToken ct)
    {
        var type = Types.FirstOrDefault(t => t.Id == cmp.VoucherType);
        if (cmp.Id <= 0) return new AsmxRefusal(1014, Text1014.Id);
        if (type.Id == 0) return new AsmxRefusal(1014, Text1014.VoucherType);
        if (cmp.PointOfSale is < 1 or > 99998) return new AsmxRefusal(1014, Text1014.PointOfSale);
        if (cmp.Number is < 1 or > 99999999) return new AsmxRefusal(1014, Text1014.Number);
        if (type.Class == "A" && cmp.DocType != 80) return new AsmxRefusal(1014, Text1014.DocType);

        if ((IsFce(cmp.VoucherType) ? CheckFceDate(cmp.DateText, today) : CheckDate(cmp.DateText, today)) is { } date) return date;
        if (await CheckCurrencyAsync(cmp, today, ct) is { } currency) return currency;
        if (CheckReceiver(cmp, type.Class) is { } receiver) return receiver;
        if (CheckItems(cmp) is { } items) return items;
        if (await CheckAssociatedAsync(call, cmp, ct) is { } associated) return associated;
        return CreditInvoices ? CheckCredit(cmp) : null;
    }

    private static AsmxRefusal? CheckFceDate(string? text, DateOnly today)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length != 8 || Figures.ParseDay(text) is not { } date) return new AsmxRefusal(1014, Text1014.DateFormat);
        if (date < today.AddDays(-5) || date > today.AddDays(1))
            return new AsmxRefusal(4897, "Para comprobantes MiPyMEs (FCE), el campo <Fecha_cbte> podrá estar comprendido en el rango N-5 y N+1 siendo N la fecha de envío del pedido de autorización. (<Cbte_nro>/<Fecha_cbte>/<Cmp>/<Fecha_cbte>)");
        if (date > today && date.Month != today.Month)
            return new AsmxRefusal(4898, "Si informa fecha de comprobante <Fecha_cbte> para comprobante del tipo MiPyMEs (FCE) con fecha superior a la fecha de envío de autorización, el mes de la fecha del comprobante <Fecha_cbte> debe coincidir con el mes de la fecha de envío de autorización.");
        return null;
    }

    private async Task<AsmxRefusal?> CheckCurrencyAsync(AsmxCmp cmp, DateOnly today, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cmp.Currency) || parameters.Currencies.All(c => c.Id != cmp.Currency))
            return new AsmxRefusal(1014, Text1014.InvalidValue("Imp_moneda_Id", "la moneda no existe. Consultar método BFEGetPARAM_MON."));
        if (cmp.SameCurrency is { } same && same is not ("S" or "N"))
            return new AsmxRefusal(4959, "Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío.");
        if (cmp.Currency == "PES" && cmp.SameCurrency == "S")
            return new AsmxRefusal(4958, "Si informa Imp_moneda_Id = PES, el campo CanMisMonExt NO debe informarse (o informarse con el valor N)");

        var official = cmp.Currency == "PES" ? (1m, today) : await rates.RateAsync(cmp.Currency, Figures.ParseDay(cmp.DateText) ?? today, ct);
        var mayOmit = cmp.SameCurrency == "S" && IsInvoice(cmp.VoucherType) && official is not null;
        if (cmp.Rate is null && !mayOmit || cmp.Rate <= 0)
            return new AsmxRefusal(4957, "El campo Imp_moneda_ctz es obligatorio si no informa el campo CanMisMonExt con valor S o si la moneda del comprobante no tiene cotización en Banco Nación o el comprobante no es del tipo factura. El mismo debe ser mayor a 0.");
        if (cmp.Rate is { } rate && official is { } known && cmp.Currency != "PES" && rate > known.Rate + 1)
            return new AsmxRefusal(4960, "Si informa el campo Imp_moneda_ctz, el mismo no podra superar en 1 a la cotización oficial. Ver Método BFEGetCotizacion.");
        return null;
    }

    private static AsmxRefusal? CheckReceiver(AsmxCmp cmp, string voucherClass)
    {
        if (string.IsNullOrEmpty(cmp.ReceiverConditionText))
            return new AsmxRefusal(4963, "Campo Condición Frente al IVA del receptor es obligatorio conforme a lo reglamentado por la Resolución General N° 5616. Para mas información consular método BFEGetPARAM_CondicionIvaReceptor");
        if (!int.TryParse(cmp.ReceiverConditionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var condition)
            || ReceiverConditions.ClassOf(condition) is not { } conditionClass)
            return new AsmxRefusal(4961, "El campo Condición IVA receptor no es un valor permitido. Consular método BFEGetPARAM_CondicionIvaReceptor.");
        if (conditionClass != voucherClass)
            return new AsmxRefusal(4962, "El campo Condición IVA receptor no es valido para la clase de comprobante informado. Consular método BFEGetPARAM_CondicionIvaReceptor.");
        return null;
    }

    private static AsmxRefusal? CheckItems(AsmxCmp cmp)
    {
        var items = Items(cmp);
        if (items.Count == 0) return new AsmxRefusal(1014, Text1014.InvalidValue("Items", "el comprobante debe informar al menos un ítem."));
        foreach (var item in items)
        {
            var vat = item.Whole("Iva_id") ?? 0;
            if (VatRates.All(r => r.Id != vat))
                return new AsmxRefusal(1014, Text1014.InvalidValue("Iva_id", $"la alícuota {vat} no existe. Consultar método BFEGetPARAM_Tipo_IVA."));
            var unit = item.Whole("Pro_umed") ?? 0;
            if (Units.All(u => u.Id != unit))
                return new AsmxRefusal(1014, Text1014.InvalidValue("Pro_umed", $"la unidad de medida {unit} no existe. Consultar método BFEGetPARAM_UMed."));
        }
        if (items.Any(i => i.Whole("Iva_id") == 2) && cmp.Exempt <= 0) return new AsmxRefusal(1014, Text1014.Exempt);
        return CheckItemsTotal(cmp, items);
    }

    /// <summary>What each voucher type may have associated (1035-1038, and 4888 for the FCE invoices).</summary>
    private int[]? Associable(int type) => type switch
    {
        1 or 6 => [91],
        2 or 3 => [1, 2, 3, 91],
        7 or 8 => [6, 7, 8, 91],
        201 or 206 => [91],
        202 or 203 => [201, 202, 203, 91],
        207 or 208 => [206, 207, 208, 91],
        _ => null,
    };

    private async Task<AsmxRefusal?> CheckAssociatedAsync(ServiceCall call, AsmxCmp cmp, CancellationToken ct)
    {
        var block = cmp.Raw.Child("CbtesAsoc");
        if (block is null)
            return CreditInvoices && IsFce(cmp.VoucherType) && !IsFceInvoice(cmp.VoucherType)
                ? new AsmxRefusal(4886, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE) y corresponde a un comprobante de débito o crédito, es obligatorio informar comprobantes asociados. (<BFEAuthorize><Cmp><Tipo_cbte>/ <BFEAuthorize><Cmp> /<CbtesAsoc>)")
                : null;
        var associated = block.Children("CbteAsoc").ToList();
        if (associated.Count == 0) return new AsmxRefusal(1030, "Si envía CbtesAsoc, CbteAsoc es obligatorio y no puede estar vacío");
        if (Associable(cmp.VoucherType) is not { } allowed)
            return new AsmxRefusal(1035, CreditInvoices
                ? "De enviarse el tag <CbtesAsoc>, entonces el campo tipo de comprobante <Cmp><Tipo_cbte> a autorizar tiene que ser 01, 02, 03, 06, 07, 08, 91, 201, 202, 203, 206, 207, 208"
                : "De enviarse el tag <CbtesAsoc>, entonces el campo tipo de comprobante <Cmp><Tipo_cbte> a autorizar tiene que ser 01, 02, 03, 06, 07, 08");

        var seen = new HashSet<(long, long, long)>();
        foreach (var asoc in associated)
        {
            var type = asoc.Whole("Tipo_cbte") ?? 0;
            var point = asoc.Whole("Punto_vta") ?? 0;
            var number = asoc.Whole("Cbte_nro") ?? 0;
            if (type <= 0) return new AsmxRefusal(1031, "De enviarse el tag CbteAsoc debe enviarse <CbteAsoc><Tipo>mayor a 0");
            if (point is <= 0 or >= 99998) return new AsmxRefusal(1032, "De enviarse el tag CbteAsoc debe enviarse <CbteAsoc><PtoVta> mayor a 0 y menor a 99998.");
            if (number is <= 0 or >= 99999999) return new AsmxRefusal(1033, "De enviarse el tag CbteAsoc debe enviarse <CbteAsoc><Nro> > a 0 y < a 99999999.");
            if (!seen.Add((type, point, number))) return new AsmxRefusal(1034, "De enviarse el tag CbteAsoc, los comprobantes no deben repetirse.");
            if (!allowed.Contains((int)type)) return AssociationRefusal(cmp.VoucherType);
            if (type != 91 && await Book.FindAsync(call.Cuit, (int)point, (int)type, number, ct) is null)
                return new AsmxRefusal(1039, "Si el punto de venta del comprobante asociado (CbtesAsoc.Punto_vta) es electrónico y del tipo Bonos, el número de comprobante debe obrar en las bases del organismo para el punto de venta y tipo de comprobante informado.");
        }
        return null;
    }

    private static AsmxRefusal AssociationRefusal(int type) => type switch
    {
        1 or 6 => new(1036, "Para <Cmp><Tipo_cbte>01 o 06 solo puede asociarse el tipo de comprobante <CbtesAsoc><Tipo_cbte> 91."),
        2 or 3 => new(1037, "Para <Cmp><Tipo_cbte> 02 o 03 pueden asociarse los tipos de comprobante <CbtesAsoc><Tipo_cbte> 01, 02, 03, 91."),
        7 or 8 => new(1038, "Para <Cmp><Tipo_cbte> 07 u 08 pueden asociarse los tipos de comprobante <CbtesAsoc><Tipo_cbte> 06, 07, 08, 91."),
        201 or 206 => new(4888, "De enviarse el tag <CbtesAsoc>, para <Tipo_cbte> comprobantes MiPyMEs (FCE) 201 o 206, solo puede asociarse comprobante 91."),
        202 or 203 => new(4890, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo A, sin código de Anulación, solo puede asociar: Para comprobantes A, asociar 201 o 91 (<BFEAuthorize><Cmp><Tipo_cbte >/ <BFEAuthorize><Cmp><CbteAsoc><Tipo>)"),
        _ => new(4893, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Débito o Crédito tipo B, sin código de Anulación, solo puede asociar: Para comprobantes B, asociar 206 (<BFEAuthorize><Cmp><Tipo_cbte >/ <BFEAuthorize><Cmp><CbteAsoc><Tipo>) . De forma complementaria se puede asociar el código 91."),
    };

    /// <summary>Fecha_vto_pago and Opcionales, which only wsbfev1 has (4900-4955, 1015-1019).</summary>
    private static AsmxRefusal? CheckCredit(AsmxCmp cmp)
    {
        var type = cmp.VoucherType;
        var fce = IsFce(type);
        var due = cmp.Raw.Field("Fecha_vto_pago");
        var optionals = cmp.Raw.Child("Opcionales")?.Children("Opcional").ToList();
        var ids = optionals?.Select(o => o.Field("Id") ?? "").ToList() ?? [];

        if (fce && cmp.Total < 0)
            return new AsmxRefusal(4955, "Si el tipo de comprobante que está autorizando es Factura del tipo MiPyMEs (201, 202, 203, 206, 207, 208), el campo <Cmp>.<Imp_total> (Importe total de la operación) deber ser igual o mayor a 0 (cero).");
        if (IsFceInvoice(type) && string.IsNullOrEmpty(due))
            return new AsmxRefusal(4900, "Para Factura de Credito, es obligatorio informar el campo Fecha_vto_pago.");
        if (!fce && !string.IsNullOrEmpty(due))
            return new AsmxRefusal(4902, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), el campo “fecha de vencimiento para el pago” <Fecha_vto_pago> no debe informarse si NO es Factura de Crédito (Cbte_tipo 201 / 206). En el caso de ser Nota de Débito o Crédito, solo puede informarse si es de Anulación. (<Fecha_vto_pago>)");

        if (optionals is not null)
        {
            if (optionals.Count == 0 || ids.Any(string.IsNullOrEmpty))
                return new AsmxRefusal(1015, "Opcionales ->Opcional : de informar <Opcionales> debe informar de forma completa la estructura <Opcionales><Opcional><Id>");
            if (ids.FirstOrDefault(id => !OptionalIds.Contains(id)) is not null)
                return new AsmxRefusal(1016, "El valor ingresado en <Id> debe ser alguno permitido. Consultar método BFEGetPARAM_Tipo_Opc.");
            if (ids.Where(id => id != "23").GroupBy(id => id).Any(g => g.Count() > 1))
                return new AsmxRefusal(1017, "El campo <Id> en <Opcionales> es obligatorio y no debe repetirse.");
            foreach (var optional in optionals)
            {
                var value = optional.Field("Valor");
                if (string.IsNullOrEmpty(value)) return new AsmxRefusal(1018, "El campo <Valor> en Opcionales es obligatorio");
                if (OptionalValueRefusal(optional.Field("Id")!, value) is { } refusal) return refusal;
            }
            if (!fce && ids.Any(FceOptionals.Contains))
                return new AsmxRefusal(4916, "Si el tipo de comprobante que está autorizando NO es MiPyMEs (FCE), no informar los códigos 2101, 2102, 22, 27. (<Opcionales><Id><Valor>)");
        }
        if (IsFceInvoice(type))
        {
            if (optionals is null)
                return new AsmxRefusal(4905, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA DE CREDITO ELECTRONICA MiPyMEs (FCE) B, es obligatorio informar <Opcionales>");
            if (!ids.Contains("27"))
                return new AsmxRefusal(4954, "Si informa comprobante MiPyMEs (FCE) del tipo Factura, es obligatorio informar opcional por RG con ID 27 y su valor correspondiente. Valores esperados SCA = 'TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA' o ADC = 'AGENTE DE DEPOSITO COLECTIVO'");
        }
        return null;
    }

    private static AsmxRefusal? OptionalValueRefusal(string id, string value) => id switch
    {
        "2" when value.Length != 8 || !value.All(char.IsAsciiDigit) =>
            new(1019, "<Opcionales><Id><Valor>. Si selecciona Id = 2 el valor ingresado debe ser un numérico de 8 (ocho) dígitos mayor o igual a 0 (cero)."),
        "22" when value is not ("S" or "N") =>
            new(4908, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 22 es “S” o “N”: S = Es de Anulación N = No es de Anulación <Opcionales><Id><Valor>"),
        "2101" when value.Length != 22 || !value.All(char.IsAsciiDigit) =>
            new(4906, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2101 es un CBU numérico de 22 caracteres. (<Opcionales><Id><Valor>)"),
        "2102" when !Alias.IsMatch(value) =>
            new(4907, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales, el valor correcto para el código 2102 es un ALIAS alfanumérico de 6 a 20 caracteres. (<Opcionales><Id><Valor>)"),
        "23" when value.Length > 50 =>
            new(4931, "Puede identificar una o varias Referencias Comerciales según corresponda. Informar bajo el código 23. Campo alfanumérico de 50 caracteres como máximo."),
        "27" when value is not ("SCA" or "ADC") =>
            new(4953, "Si el tipo de comprobante que está autorizando es MiPyMEs (FCE), informa opcionales y el código es 27, los valores posibles son: SCA = \"TRANSFERENCIA AL SISTEMA DE CIRCULACION ABIERTA\" ADC = \"AGENTE DE DEPOSITO COLECTIVO\""),
        _ => null,
    };

    private static readonly Regex Alias = new("^[A-Za-z0-9.\\-]{6,20}$");

    // ---- BFEGetCMP -------------------------------------------------------------------------

    protected override IEnumerable<XElement> Detail(XNamespace ns, BookedVoucher voucher)
    {
        var detail = XElement.Parse(voucher.Detail);
        foreach (var head in Head(ns, voucher, detail)) yield return head;
        yield return new XElement(ns + "Fecha_cbte_orig", voucher.SentDate ?? "");
        yield return new XElement(ns + "Fecha_cbte_cae", Figures.Day(voucher.Date));
        if (CreditInvoices && detail.Field("Fecha_vto_pago") is { } due) yield return new XElement(ns + "Fecha_vto_pago", due);
        yield return new XElement(ns + "Fch_venc_Cae", Figures.Day(voucher.CaeDue));
        yield return new XElement(ns + "Cae", voucher.Cae);
        yield return new XElement(ns + "Resultado", voucher.Result);
        yield return new XElement(ns + "Obs", Observations(voucher));
        foreach (var field in Figures.Ordered(ns, detail, ["CondicionIVAReceptorId#", "CanMisMonExt"])) yield return field;
        if (CreditInvoices && detail.Child("Opcionales") is { } optionals)
            yield return new XElement(ns + "Opcionales", optionals.Children("Opcional").Select(o =>
                new XElement(ns + "Opcional", Figures.Ordered(ns, o, ["Id", "Valor"]))));
        if (detail.Child("Items") is { } items)
            yield return new XElement(ns + "Items", items.Children("Item").Select(i => new XElement(ns + "Item", Figures.Ordered(ns, i,
                ["Pro_codigo_ncm", "Pro_codigo_sec", "Pro_ds", "Pro_qty!", "Pro_umed!", "Pro_precio_uni!", "Imp_bonif!", "Imp_total!", "Iva_id!"]))));
        if (detail.Child("CbtesAsoc") is { } associated)
            yield return new XElement(ns + "CbtesAsoc", associated.Children("CbteAsoc").Select(a => new XElement(ns + "CbteAsoc", Figures.Ordered(ns, a,
                CreditInvoices ? ["Tipo_cbte!", "Punto_vta!", "Cbte_nro!", "Cuit", "Fecha_cbte"] : ["Tipo_cbte!", "Punto_vta!", "Cbte_nro!"]))));
    }
}
