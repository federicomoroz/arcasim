using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>
/// WSSEG, Seguros de Caución (RG 2668), following docs/arca/servicios/wsseg.md
/// and its manual 1.0 (9 June 2025): the shared flow of AsmxVoucherRules for
/// vouchers 1, 2, 3, 6, 7 and 8 with their policy and endorsement per item.
/// SEGAuthorize checks, in this order, and answers the first that fails: the
/// Id, type, point of sale and number ranges and the document of a class A
/// voucher (1014); the date (1014); the currency (1014), CanMisMonExt (1033,
/// 1035) and the rate (1014, 1034); the receiver's VAT condition (1032, 1030,
/// 1031); the items (1014). A receiver CUIT that cannot exist (wrong check
/// digit) is authorized with observation 22. SEGGetPARAM_Ctz (1003-1006) and
/// SEGGetCondicionIvaReceptor (1002) answer the rates and the annex.
///
/// In production every answer carries event 47, the notice that RG 5866/2026
/// retires the service on 01/01/2027 in favor of WSFEv1, as production shows
/// it today. In homologación it carries the catalog's event 39, the maintenance
/// notice seen live with every error (taken for the successful answers too, which
/// are inferred).
///
/// ArcaSim's choices on what the spec leaves NO VERIFICADO:
/// - a refused SEGAuthorize carries SEGResultAuth with Id and Cuit 0, as
///   wsbfev1 does; a successful answer carries SEGErr 0 "OK" and the event above;
/// - numbers run "último + 1" per CUIT, point of sale and type (1014 otherwise);
/// - the CAE has 14 digits and expires SimulationSettings.CaeLifetimeDays
///   after the voucher's date, as in WSFEv1;
/// - Imp_moneda_ctz may be left out only when CanMisMonExt is S and ArcaSim has
///   a rate for the currency; "no podrá superar en 1" (1034) is one unit above
///   that rate; the 20 %-200 % band is not checked;
/// - SEGGetPARAM_Tipo_Cbte, _Tipo_doc, _Tipo_IVA and _MON answer WSFEv1's tables
///   (types 1, 2, 3, 6, 7 and 8 only): the manual publishes none of them, and
///   no rate of that table is exempt, so the Imp_op_ex rule never applies;
/// - SEGGetPARAM_Ctz answers the latest rate on or before the date asked, PES is
///   1, and a missing FchCotiz is today;
/// - Imp_otrib_prov, Imp_valor_aseg and Imp_moneda_vaseg are kept and answered
///   back, but not checked.
/// </summary>
public sealed class WssegRules(
    ParameterTables parameters, IDocumentStore documents, IExchangeRates rates, IAuthorizationCodes codes,
    SequenceLocks locks, IClock clock, SimulationSettings settings, EventManager events, TimeProvider time)
    : AsmxVoucherRules(parameters, documents, rates, codes, locks, clock, settings, events, time)
{
    public const string RetirementNotice =
        "El servicio WSSEG sera dado de baja. Cabe destacar que la Resolución General Nro 5866/2026 indica que este servicio sera reemplazado por " +
        "WSFEV1 a partir del 01/01/2027. En caso de querer emitir comprobantes electrónicos de Seguros de Caución, el contribuyente podrá " +
        "informarlos utilizando un Punto de Venta de tipo 'Comprobantes de Seguros de Caución - Webservices' por medio del servicio WSFEV1. " +
        "En caso de requerir consultar comprobantes emitidos vía WSSEG, para tal fin se adecuó el método FECompConsultar para obtener ademas " +
        "la póliza y el endoso";

    private static readonly int[] TypeIds = [1, 2, 3, 6, 7, 8];

    public override string Service => "wsseg";

    protected override string Prefix => "SEG";

    protected override string Family => "wsseg";

    protected override AsmxEvent Event(ServiceCall call) =>
        Settings.Environment == ArcaEnvironment.Produccion ? new AsmxEvent(47, RetirementNotice) : base.Event(call);

    private static readonly AsmxRefusal BadClass = new(1002,
        "El valor ingresado para la clase de comprobante no es valido. La clase de Comprobante es opcional, de ingresar un valor solo puede ser A o B,");

    private static readonly QuoteRefusals Quote = new(
        MissingCurrency: new AsmxRefusal(1004, "No ingreso el código de moneda. Ingresar un valor valido. Ver método SEGGetPARAM_MON"),
        UnknownCurrency: new AsmxRefusal(1003, "El código de moneda ingresado es invalido. Verificar los codigos mediante el método SEGGetPARAM_MON"),
        BadDate: new AsmxRefusal(1005, "Campo FchCotiz No corresponde a una fecha valida con formato YYYYMMDD"),
        NoRate: new AsmxRefusal(1006, "Sin Resultados: - Método SEGGetPARAM_Ctz"));

    private static readonly CurrencyRefusals Currency = new(
        Unknown: new AsmxRefusal(1014, Text1014.InvalidValue("Imp_moneda_Id", "la moneda no existe. Consultar método SEGGetPARAM_MON.")),
        BadFlag: new AsmxRefusal(1033, "Si informa el campo CanMisMonExt, los valores posibles son S o N y no debe quedar vacío,"),
        PesFlag: new AsmxRefusal(1035, "Si informa Imp_moneda_Id = PES, el campo CanMisMonExt no debe informarse o informarse con el valor N."),
        RateRequired: new AsmxRefusal(1014, "El campo <Imp_moneda_ctz> es obligatorio si no informa el campo CanMisMonExt = S, y de informarse debe ser mayor a 0. " +
            "Si se indica que el pago del comprobante se realiza en la misma moneda extranjera que la factura, la cotización de la moneda provista debe " +
            "coincidir exactamente con la registrada en las bases de ARCA para el día hábil anterior a la fecha de emisión del comprobante, si esta es " +
            "anterior a la fecha actual, o bien con la registrada para el día hábil anterior a la fecha actual, si la fecha de emisión es posterior a esta. " +
            "En caso contrario, se puede omitir el campo de Cotización de Moneda."),
        RateAbove: new AsmxRefusal(1034, "Si informa el campo Imp_moneda_ctz, el mismo no podrá superar en 1 a la cotizacion oficial. Ver Método SEGGetPARAM_Ctz.."));

    private static readonly ReceiverRefusals Receiver = new(
        Missing: new AsmxRefusal(1032, "El campo Condición Frente al IVA del receptor resultara obligatorio conforme lo reglamentado por la Resolución General N° 5616. Para mas información consular método SEGGetCondicionIvaReceptor"),
        Unknown: new AsmxRefusal(1030, "El campo Condición IVA receptor no es un valor permitido. Para mas información consular método SEGGetCondicionIvaReceptor"),
        WrongClass: new AsmxRefusal(1031, "El campo Condición IVA receptor no es valido para la clase de comprobante informado. Para mas información consular método SEGGetCondicionIvaReceptor"));

    protected override Task<ContractAnswer?> OtherAsync(ServiceCall call, CancellationToken ct)
    {
        var ns = Ns(call);
        return call.Name switch
        {
            "SEGGetPARAM_Tipo_Cbte" => Done(Table(call, Parameters.VoucherTypes.Where(t => TypeIds.Contains(t.Id))
                .Select(t => Row(ns, "ClsSEGResponse_Tipo_Cbte", "Cbte", t.Id, t.Desc, t.From, t.To)))),
            "SEGGetPARAM_Tipo_doc" => Done(Table(call, Parameters.DocumentTypes.Select(d => Row(ns, "ClsSEGResponse_Tipo_doc", "Doc", d.Id, d.Desc, d.From, d.To)))),
            "SEGGetPARAM_Tipo_IVA" => Done(Table(call, Parameters.VatRates.Select(r => Row(ns, "ClsSEGResponse_Tipo_IVA", "IVA", r.Id, r.Desc, r.From, r.To)))),
            "SEGGetPARAM_MON" => Done(Table(call, Parameters.Currencies.Select(c => Row(ns, "ClsSEGResponse_Mon", "Mon", c.Id, c.Desc, c.From, c.To)))),
            "SEGGetPARAM_Ctz" => QuoteAsync(call, Quote, ct),
            "SEGGetCondicionIvaReceptor" => Done(Conditions(call, BadClass)),
            _ => Done(),
        };
    }

    // ---- SEGAuthorize ----------------------------------------------------------------------

    protected override async Task<AsmxRefusal?> CheckAsync(ServiceCall call, AsmxCmp cmp, DateOnly today, CancellationToken ct)
    {
        var (header, type) = CheckHeader(cmp, TypeIds);
        if (header is not null || type is null) return header;

        if (CheckDate(cmp.DateText, today) is { } date) return date;
        if (await CheckCurrencyAsync(cmp, today, rateMayBeOmitted: true, Currency, ct) is { } currency) return currency;
        if (CheckReceiver(cmp, type.Class, Receiver) is { } receiver) return receiver;

        var items = Items(cmp);
        if (CheckItemsSent(items) is { } none) return none;
        foreach (var item in items)
        {
            var vat = item.ChildText("Iva_id") ?? "";
            if (Parameters.VatRates.All(r => r.Id != vat))
                return new AsmxRefusal(1014, Text1014.InvalidValue("Iva_id", $"la alícuota {vat} no existe. Consultar método SEGGetPARAM_Tipo_IVA."));
        }
        var raw = cmp.Raw;
        if (new[] { "Imp_op_ex", "Imp_perc", "Imp_iibb", "Imp_perc_mun", "Imp_internos" }.Any(f => (raw.Amount(f) ?? 0) > cmp.Total))
            return new AsmxRefusal(1014, Text1014.Items);
        return CheckItemsTotal(cmp, items);
    }

    /// <summary>Observation 22 when the receiver's CUIT cannot exist; the rest of the padrón checks (21, apócrifos) need ARCA's data.</summary>
    protected override List<BookNote> Observe(AsmxCmp cmp) =>
        cmp.DocType == 80 && !Cuits.IsValid(cmp.DocNumber)
            ? [new BookNote(22, "La CUIT RECEPTORA INGRESADA NO EXISTE. Para el caso de Facturas y Notas de Débito, emitir una Nota de Crédito o anular la operación, según corresponda.")]
            : [];

    // ---- SEGGetCMP -------------------------------------------------------------------------

    protected override IEnumerable<XElement> Detail(XNamespace ns, BookedVoucher voucher)
    {
        var detail = XElement.Parse(voucher.Detail);
        foreach (var head in Head(ns, voucher, detail)) yield return head;
        foreach (var field in Figures.Ordered(ns, detail, ["Imp_otrib_prov!"])) yield return field;
        yield return new XElement(ns + "Fecha_cbte_orig", voucher.SentDate ?? "");
        yield return new XElement(ns + "Fecha_cbte_cae", Figures.Day(voucher.Date));
        yield return new XElement(ns + "Fch_venc_Cae", Figures.Day(voucher.CaeDue));
        yield return new XElement(ns + "Cae", voucher.Cae);
        yield return new XElement(ns + "Resultado", voucher.Result);
        yield return new XElement(ns + "Obs", Observations(voucher));
        foreach (var field in Figures.Ordered(ns, detail, ["CanMisMonExt", "CondicionIVAReceptorId#"])) yield return field;
        if (detail.Child("Items") is { } items)
            yield return new XElement(ns + "Items", items.Children("Item").Select(i => new XElement(ns + "Item", Figures.Ordered(ns, i,
                ["Poliza", "Endoso", "Ds", "Qty!", "Precio_uni!", "Imp_bonif!", "Imp_total!", "Imp_valor_aseg!", "Imp_moneda_vaseg", "Iva_id!"]))));
    }
}
