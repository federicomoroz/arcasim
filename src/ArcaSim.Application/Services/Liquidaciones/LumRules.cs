using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Liquidaciones.SettlementXml;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// WSLUM, the monthly dairy liquidation (docs/arca/servicios/wslum.md): the
/// dairy buyer authorizes the month's liquidation to a producer with a CAE on
/// último + 1 (else 2074), once per period, buyer, producer and RENSPA (2078);
/// credit and debit notes (43-48) go through the same generarLiquidacion with
/// an ajuste block that points at the CAE they adjust or at a paper invoice.
/// Liquidations read back by number or by CAE, with the PDF when asked. The
/// period, date and VAT rules of §4.2.1 apply with their codes; errors go in
/// respuesta/errores, and there is no metadata.
/// ArcaSim's choices where the manual is silent: an empty sequence answers
/// 2044 (the schema does not allow a last number of 0); resending an
/// authorized number is 2074; code 41 is a commercial bonus, 50 a commercial
/// debit and every other code a quality bonus, since the sign of each concept
/// comes from a table the manual does not print; fat and protein percentages
/// are over the net liters; VAT conditions print in ArcaSim's wording; 2103
/// ("no se encuentra activa o es inexistente") is sent for a tambero the registry
/// holds as inactive, and one it does not hold at all is accepted, as the parties
/// of the other liquidation services are.
/// The parameter tables other than points of sale keep the contract's answer.
/// </summary>
public sealed class LumRules(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
    : IServiceBehavior
{
    private static readonly int[] AdjustmentTypes = [43, 44, 45, 46, 47, 48];

    private static readonly SettlementProblem NoPoints = new(2082, "La cuit representada, no tiene puntos de venta activos para emitir una liquidación.");
    private static readonly SettlementProblem InvalidPoint = new(2086, "El punto de venta informado no es válido.");

    private readonly SettlementLedger _ledger = new(store, taxpayers, codes, locks, clock);

    public string Service => "wslum";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "consultarUltimoNroComprobantePorPtoVta" => await LastAsync(call, ct),
        "generarLiquidacion" => await GenerateAsync(call, ct),
        "consultarLiquidacionPorNroComprobante" => await ByNumberAsync(call, ct),
        "consultarLiquidacionPorCae" => await ByCaeAsync(call, ct),
        "consultarPuntosVenta" => await PointsAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"), ct);
        return last is null ? NotFound(call) : Ok(call, new XElement("nroComprobante", last.Number));
    }

    private async Task<ContractAnswer> ByNumberAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var found = await _ledger.FindAsync(Service, request.Number("cuitComprador"), (int)request.Number("puntoVenta"),
            (int)request.Number("tipoComprobante"), request.Number("nroComprobante"), ct);
        return found is null ? NotFound(call) : Answer(call, found, Wants(request));
    }

    private async Task<ContractAnswer> ByCaeAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var found = await _ledger.FindByCaeAsync(Service, request.Number("cae"), ct);
        return found is null || found.Cuit != call.Cuit ? NotFound(call) : Answer(call, found, Wants(request));
    }

    private async Task<ContractAnswer> PointsAsync(ServiceCall call, CancellationToken ct) =>
        Ok(call, await _ledger.PointsAnswerAsync(call.Cuit, ct));

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var voucher = Voucher.Of(call.Request, _ledger.Today);
        var (refusal, adjusted) = await CheckAsync(call, voucher, ct);
        if (refusal is not null) return refusal;

        using var _ = await _ledger.LockAsync(Service, call.Cuit, voucher.PointOfSale, voucher.Type, ct);
        // A buyer makes one liquidation per period, producer and RENSPA on whatever sequence, so the check and
        // the voucher that follows it share one hold, taken inside the sequence's.
        using var unique = voucher.Adjustment is null ? await _ledger.LockUniqueAsync(Service, "periods", call.Cuit, ct) : null;
        if (await SequenceProblemAsync(call, voucher, ct) is { } wrong) return wrong;

        var amounts = Amounts.Of(voucher);
        if (amounts.Net <= 0) return Fail(call, 2113, "El importe total neto de la liquidación no puede ser cero o menor a cero.");

        var cae = _ledger.NewCae();
        var detail = Detail(call.Cuit, voucher, amounts, cae, await _ledger.TaxpayerAsync(call.Cuit, ct), await _ledger.TaxpayerAsync(voucher.ProducerCuit, ct));
        var settlement = new Settlement(Service, call.Cuit, voucher.PointOfSale, voucher.Type, voucher.Number, cae, voucher.Date, voucher.Today,
            voucher.Today.AddDays(SettlementLedger.CaeDays), voucher.ProducerCuit, amounts.Net, "lecheria", voucher.Adjustment is not null,
            adjusted is null ? [] : [adjusted.KeyOf()], Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        return Answer(call, settlement, withPdf: true);
    }

    /// <summary>What generarLiquidacion reads of its request, with the day it is processed.</summary>
    private sealed record Voucher(
        XElement Request, XElement Liquidation, XElement Producer, XElement Dairy,
        XElement? Balance, XElement? Domestic, XElement? Foreign, XElement? Adjustment,
        int PointOfSale, int Type, long Number, string Period, DateOnly Date, DateOnly Today)
    {
        public long ProducerCuit => Producer.Number("cuit");

        public string? Renspa => Dairy.Value("nroRenspa");

        public decimal? Rate => Liquidation.ChildDecimal("alicuotaIVA");

        public static Voucher Of(XElement envelope, DateOnly today)
        {
            var request = envelope.ChildOrEmpty("solicitud");
            var liquidation = request.ChildOrEmpty("liquidacion");
            return new Voucher(request, liquidation, request.ChildOrEmpty("tambero"), request.ChildOrEmpty("tambo"),
                request.Child("balanceLitrosPorcentajesSolidos"), request.Child("conceptosBasicosMercadoInterno"),
                request.Child("conceptosBasicosMercadoExterno"), liquidation.Child("ajuste"),
                (int)liquidation.Number("puntoVenta"), (int)liquidation.Number("tipoComprobante"), liquidation.Number("nroComprobante"),
                liquidation.Value("periodo") ?? "", liquidation.Day("fechaComprobante") ?? today, today);
        }
    }

    // ---- Checks ----------------------------------------------------------------------

    /// <summary>
    /// The rejections the request decides by itself, in the order their codes are checked, and the liquidation
    /// an adjustment names by its CAE.
    /// </summary>
    private async Task<(ContractAnswer? Refusal, Settlement? Adjusted)> CheckAsync(ServiceCall call, Voucher v, CancellationToken ct)
    {
        if (await _ledger.PointProblemAsync(call.Cuit, v.PointOfSale, NoPoints, InvalidPoint, ct) is { } point) return (Fail(call, point), null);
        if (v.ProducerCuit == call.Cuit)
            return (Fail(call, 2126, "La cuit del productor tambero y la del adquiriente no pueden ser iguales."), null);
        if (await _ledger.TaxpayerAsync(v.ProducerCuit, ct) is { Active: false })
            return (Fail(call, 2103, "La cuit tambero, no se encuentra activa o es inexistente."), null);

        var (refusal, adjusted) = await CheckAdjustmentAsync(call, v, ct);
        return refusal is not null ? (refusal, null) : (CheckFields(call, v), adjusted);
    }

    /// <summary>1000 to 1006 and 2004: the ajuste block against the type, the other blocks and the liquidation it names.</summary>
    private async Task<(ContractAnswer? Refusal, Settlement? Adjusted)> CheckAdjustmentAsync(ServiceCall call, Voucher v, CancellationToken ct)
    {
        var adjustment = v.Adjustment;
        if (AdjustmentTypes.Contains(v.Type) && adjustment is null) return (Fail(call, 1000, "Solicitud incompleta: debe especificar el campo <ajuste>."), null);
        if (!AdjustmentTypes.Contains(v.Type) && adjustment is not null) return (Fail(call, 1003, "Si no es un ajuste, no debe enviar datos en la etiqueta <ajuste>."), null);
        if (adjustment is null) return (null, null);

        if ((adjustment.Child("formularioPapel") is null) == (adjustment.Child("caeAAjustar") is null))
            return (Fail(call, 1001, "Solicitud incompleta: para ajustes debe especificar uno y solo uno de los siguientes campos: <formularioPapel>, <caeAAjustar>."), null);
        var monetary = adjustment.Value("tipoAjuste") == "MONETARIO";
        if (monetary && (v.Balance is not null || v.Domestic is not null || v.Foreign is not null))
            return (Fail(call, 1005, "Para ajustes monetarios los siguientes campos debe ser nulos: balanceLitrosPorcentajesSolidos, conceptosBasicosMercadoInterno y conceptosBasicosMercadoExterno."), null);
        if (!monetary && v.Balance is null) return (Fail(call, 1006, "Para ajustes físicos debe informar el campo balanceLitrosPorcentajesSolidos."), null);
        if (adjustment.Child("caeAAjustar") is null) return (null, null);

        var adjusted = await _ledger.FindByCaeAsync(Service, adjustment.Number("caeAAjustar"), ct);
        var issued = adjusted?.DetailXml();
        if (adjusted is null || issued is null || adjusted.Cuit != call.Cuit || adjusted.ReceiverCuit != v.ProducerCuit
            || issued.ChildOrEmpty("encabezado").Value("periodo") != v.Period || issued.ChildOrEmpty("tambo").Value("nroRenspa") != v.Renspa)
            return (Fail(call, 2004, "No se puede ajustar la liquidación ya que no fue encontrada por los siguientes parámetros: su número de CAE, CUIT del productor, CUIT del comprador, período y número de RENSPA."), null);
        return (null, adjusted);
    }

    /// <summary>The VAT rate (2114, 2115), the period and date (2055, 2121, 2131, 2130), the balance (2016, 2024) and the bonuses (2122, 2123).</summary>
    private ContractAnswer? CheckFields(ServiceCall call, Voucher v)
    {
        var rate = v.Rate;
        if (v.Type == 27 && rate != 21 || v.Type is 45 or 48 && rate is not (0 or 21))
            return Fail(call, 2114, "La alícuota IVA no se corresponde con la situación del tambero.");
        if (v.Type is not (27 or 45 or 48) && rate is not null)
            return Fail(call, 2115, "No debe informar la alícuota IVA para el tipo de comprobante que intenta generar.");

        if (!PeriodOpen(v.Period, v.Today)) return Fail(call, 2055, "Error, el período seleccionado para el tipo de liquidación que se intenta realizar, no es válido.");
        if (v.Date.ToString("yyyy/MM", CultureInfo.InvariantCulture) != v.Period)
            return Fail(call, 2121, "La fecha de comprobante de la liquidación debe pertenecer al año y al mes de la liquidación.");
        if (v.Date > v.Today) return Fail(call, 2131, "La fecha del comprobante no puede ser posterior a hoy.");
        if (v.Today.DayNumber - v.Date.DayNumber > 10)
            return Fail(call, 2130, "La fecha de comprobante de la liquidación no debe tener más de 10 días de diferencia hacia atrás con la fecha de hoy.");
        if (v.Balance is not null && v.Balance.Number("litrosDecomisados") == v.Balance.Number("litrosRemitidos"))
            return Fail(call, 2016, "Error en balance de litros porcentaje de sólidos: El valor de Kg remitidos no puede ser igual al los Kg decomisados.");
        if (v.Balance is not null && v.Balance.Number("litrosDecomisados") > v.Balance.Number("litrosRemitidos"))
            return Fail(call, 2024, "Error en balance de litros porcentaje de sólidos: El valor decomisados debe ser menor a remitidos.");
        foreach (var concept in v.Request.Children("bonificacionPenalizacion"))
        {
            var commercial = concept.Number("codBonificacionPenalizacion") == 41;
            if (commercial && (concept.Child("importe") is null || concept.Child("porcentajeAAplicar") is not null))
                return Fail(call, 2122, "Para bonificaciones/penalizaciones con código igual a 41 debe informar el campo <importe> y no <porcentajeAAplicar>.");
            if (!commercial && (concept.Child("porcentajeAAplicar") is null || concept.Child("importe") is not null))
                return Fail(call, 2123, "Para bonificaciones/penalizaciones con código distinto a 41 debe informar el campo <porcentajeAAplicar> y no <importe>.");
        }
        return null;
    }

    /// <summary>2074 and 2132 for the number and its date, and 2078 for a second liquidation of the same period, producer and RENSPA.</summary>
    private async Task<ContractAnswer?> SequenceProblemAsync(ServiceCall call, Voucher v, CancellationToken ct)
    {
        switch (await _ledger.CheckNumberAsync(Service, call.Cuit, v.PointOfSale, v.Type, v.Number, v.Date, ct))
        {
            case NumberCheck.WrongNumber: return Fail(call, 2074, SettlementLedger.WrongNumberText);
            case NumberCheck.EarlierDate:
                return Fail(call, 2132, "La fecha de comprobante ingresada, es anterior a la fecha de comprobante de una liquidación(activa) generada con anterioridad para la misma cuit, punto de venta y tipo de liquidacion.");
        }
        return v.Adjustment is null && await DuplicateAsync(call.Cuit, v.Period, v.ProducerCuit, v.Renspa, ct)
            ? Fail(call, 2078, "Error en la generación de la liquidación, la misma debe ser única por período, CUIT comprador, CUIT productor y número de RENSPA.")
            : null;
    }

    // ---- Amounts and answer ----------------------------------------------------------

    /// <summary>The concepts of the liquidation, each with its amount, and what they add up to.</summary>
    private sealed record Amounts(
        Part[] BasicLines, List<Part> ConceptLines, List<Part> OtherLines,
        decimal Basic, decimal QualityBonus, decimal CommercialBonus, decimal CommercialDebit, decimal OtherTaxes, decimal Total, decimal Vat, decimal Net)
    {
        public static Amounts Of(Voucher v)
        {
            var basics = new[] { Basics("conceptoBasicoMercadoInterno", v.Domestic), Basics("conceptoBasicoMercadoExterno", v.Foreign) };
            var basic = basics.Sum(b => b.Amount);
            var concepts = v.Request.Children("bonificacionPenalizacion").Select(c => Concept(c, basic)).ToList();
            var others = v.Request.Children("otroImpuesto").Select(OtherTax).ToList();
            var qualityBonus = concepts.Where(c => c.Code is not (41 or 50)).Sum(c => c.Amount);
            var commercialBonus = concepts.Where(c => c.Code == 41).Sum(c => c.Amount);
            var commercialDebit = concepts.Where(c => c.Code == 50).Sum(c => c.Amount);
            var otherTaxes = others.Sum(o => o.Amount);
            var total = Round(basic + qualityBonus + commercialBonus - commercialDebit);
            var vat = Round(total * (v.Rate ?? 0) / 100);
            return new Amounts(basics, concepts, others, basic, qualityBonus, commercialBonus, commercialDebit, otherTaxes, total, vat, total - otherTaxes + vat);
        }
    }

    private static XElement Detail(long buyerCuit, Voucher v, Amounts a, long cae, Taxpayer? buyer, Taxpayer? producerTaxpayer)
    {
        var producerAddress = producerTaxpayer?.Profile.Address ?? TaxAddress.Default;
        return new XElement("liquidacion",
            new XElement("encabezado",
                new XElement("cae", cae), new XElement("tipoComprobante", v.Type), new XElement("nroComprobante", v.Number),
                new XElement("fechaComprobante", Iso(v.Date)), new XElement("periodo", v.Period),
                new XElement("fechaVencimiento", Iso(v.Today.AddDays(SettlementLedger.CaeDays))), new XElement("fecha", Iso(v.Today)),
                new XElement("puntoVenta", v.PointOfSale), Maybe("iibbAdquirente", v.Liquidation.Value("iibbAdquirente")),
                new XElement("cuitComprador", buyerCuit), Maybe("razonSocialComprador", buyer?.Name.ToUpperInvariant()),
                Maybe("domicilioComprador", buyer is null ? null : SettlementLedger.AddressOf(buyer)),
                Maybe("domicilioSede", v.Liquidation.Value("domicilioSede")), Maybe("inscripcionRegistroPublico", v.Liquidation.Value("inscripcionRegistroPublico")),
                Maybe("situacionIVAComprador", buyer is null ? null : SettlementLedger.VatText(buyer.VatCondition)),
                v.Liquidation.Children("condicionVenta").Select(c => Copy(c)),
                Maybe("datosAdicionales", v.Liquidation.Value("datosAdicionales"))),
            Copy(v.Adjustment),
            new XElement("tambero",
                Maybe("razonSocial", producerTaxpayer?.Name.ToUpperInvariant()),
                producerTaxpayer is null ? null : new XElement("domicilioFiscal", producerAddress.Street),
                producerTaxpayer is null ? null : new XElement("localidad", producerAddress.Locality),
                producerTaxpayer is null ? null : new XElement("codPostal", producerAddress.PostalCode),
                producerTaxpayer is null ? null : new XElement("provincia", producerAddress.Province),
                Maybe("situacionIVA", producerTaxpayer is null ? null : SettlementLedger.VatText(producerTaxpayer.VatCondition)),
                new XElement("cuit", v.ProducerCuit), Maybe("iibb", v.Producer.Value("iibb"))),
            Copy(v.Dairy),
            Balance(v.Balance),
            a.BasicLines.Select(b => b.Element),
            a.ConceptLines.Select(c => c.Element),
            a.OtherLines.Select(o => o.Element),
            new XElement("resumenTotales",
                new XElement("totalBasico", Money(a.Basic)),
                new XElement("totalBonificacionesCalidad", Money(a.QualityBonus)),
                new XElement("totalPenalizacionesCalidad", Money(0)),
                new XElement("totalBonificacionesComerciales", Money(a.CommercialBonus)),
                new XElement("totalDebitosComerciales", Money(a.CommercialDebit)),
                new XElement("totalOtrosImpuestos", Money(a.OtherTaxes)),
                new XElement("totalLiquidacion", Money(a.Total)),
                Maybe("alicuotaIVA", v.Rate),
                new XElement("importeIVA", Money(a.Vat)),
                new XElement("totalNetoLiquidacion", Money(a.Net))),
            v.Request.Children("remito").Select(r => new XElement("remito", r.Value.Trim())));
    }

    /// <summary>§4.2.1: the current month, or the previous one until day 10.</summary>
    private static bool PeriodOpen(string period, DateOnly today)
    {
        if (!DateOnly.TryParseExact(period.Replace('\\', '/') + "/01", "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)) return false;
        var current = new DateOnly(today.Year, today.Month, 1);
        return month == current || month == current.AddMonths(-1) && today.Day <= 10;
    }

    private async Task<bool> DuplicateAsync(long cuit, string period, long producer, string? renspa, CancellationToken ct) =>
        (await _ledger.ListAsync(Service, cuit, ct)).Any(s => !s.IsAdjustment && s.State == Settlement.Active && s.ReceiverCuit == producer
            && s.DetailXml() is var detail && detail.ChildOrEmpty("encabezado").Value("periodo") == period && detail.ChildOrEmpty("tambo").Value("nroRenspa") == renspa);

    private static XElement? Balance(XElement? balance)
    {
        if (balance is null) return null;
        var net = balance.Number("litrosRemitidos") - balance.Number("litrosDecomisados");
        var fat = balance.Amount("kgGrasa");
        var protein = balance.Amount("kgProteina");
        string Share(decimal kilos) => Money(net > 0 ? kilos / net * 100 : 0);
        return new XElement("balanceLitrosPorcentajesSolidos",
            new XElement("litrosRemitidos", balance.Number("litrosRemitidos")),
            new XElement("litrosDecomisados", balance.Number("litrosDecomisados")),
            new XElement("litrosNetosLiquidados", net),
            new XElement("kgGrasa", Money(fat)),
            new XElement("kgProteina", Money(protein)),
            new XElement("kgSolidosUtiles", Money(fat + protein)),
            new XElement("porcentajeGrasa", Share(fat)),
            new XElement("porcentajeProteina", Share(protein)),
            new XElement("porcentajeSolidosUtiles", Share(fat + protein)));
    }

    private sealed record Part(XElement? Element, decimal Amount, int Code = 0);

    private static Part Basics(string name, XElement? market)
    {
        if (market is null) return new Part(null, 0);
        var fat = Round(market.Amount("kgProduccionGB") * market.Amount("precioPorKgProduccionGB"));
        var protein = Round(market.Amount("kgProduccionPR") * market.Amount("precioPorKgProduccionPR"));
        return new Part(new XElement(name,
            new XElement("kgProduccionGB", Money(market.Amount("kgProduccionGB"))),
            new XElement("precioPorKgProduccionGB", Money(market.Amount("precioPorKgProduccionGB"))),
            new XElement("importeProduccionGB", Money(fat)),
            new XElement("kgProduccionPR", Money(market.Amount("kgProduccionPR"))),
            new XElement("precioPorKgProduccionPR", Money(market.Amount("precioPorKgProduccionPR"))),
            new XElement("importeProduccionPR", Money(protein))), fat + protein);
    }

    private static Part Concept(XElement concept, decimal basic)
    {
        var code = (int)concept.Number("codBonificacionPenalizacion");
        var amount = concept.ChildDecimal("importe") ?? Round(basic * concept.Amount("porcentajeAAplicar") / 100);
        return new Part(new XElement("bonificacionPenalizacion",
            new XElement("codigo", code),
            Maybe("detalle", concept.Value("detalle")), Maybe("resultado", concept.Value("resultado")),
            Maybe("porcentaje", concept.Value("porcentajeAAplicar")), new XElement("importe", Money(amount))), amount, code);
    }

    private static Part OtherTax(XElement tax)
    {
        var amount = Round(tax.Amount("baseImponible") * tax.Amount("alicuota") / 100);
        return new Part(new XElement("otroImpuesto",
            new XElement("codigo", tax.Number("tipo")),
            new XElement("alicuota", Money(tax.Amount("alicuota"))), new XElement("baseImponible", Money(tax.Amount("baseImponible"))),
            Maybe("detalle", tax.Value("detalle")), new XElement("importe", Money(amount))), amount);
    }

    private static bool Wants(XElement request) => request.Value("pdf") is "true" or "1";

    private static ContractAnswer Answer(ServiceCall call, Settlement settlement, bool withPdf)
    {
        var liquidation = settlement.DetailXml();
        if (withPdf) liquidation.Add(PdfOf(settlement, "Liquidacion Unica Mensual Lecheria", "comprador", "tambero", "Total neto"));
        return Ok(call, liquidation);
    }

    private static ContractAnswer NotFound(ServiceCall call) =>
        Fail(call, 2044, "La liquidación que intenta obtener no existe según los parámetros de búsqueda.");

    private static ContractAnswer Fail(ServiceCall call, long code, string text) => Fail(call, new SettlementProblem(code, text));

    private static ContractAnswer Fail(ServiceCall call, SettlementProblem problem) => SettlementXml.Fail(call, null, problem);
}
