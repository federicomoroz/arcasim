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
/// are over the net liters; VAT conditions print in ArcaSim's wording.
/// The parameter tables other than points of sale keep the contract's answer.
/// </summary>
public sealed class LumRules(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
    : IServiceBehavior
{
    private static readonly int[] AdjustmentTypes = [43, 44, 45, 46, 47, 48];

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
        var request = call.Request.Child("solicitud");
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"), ct);
        return last is null ? NotFound(call) : Ok(call, new XElement("nroComprobante", last.Number));
    }

    private async Task<ContractAnswer> ByNumberAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var found = await _ledger.FindAsync(Service, request.Number("cuitComprador"), (int)request.Number("puntoVenta"),
            (int)request.Number("tipoComprobante"), request.Number("nroComprobante"), ct);
        return found is null ? NotFound(call) : Answer(call, found, Wants(request));
    }

    private async Task<ContractAnswer> ByCaeAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var found = await _ledger.FindByCaeAsync(Service, request.Number("cae"), ct);
        return found is null || found.Cuit != call.Cuit ? NotFound(call) : Answer(call, found, Wants(request));
    }

    private async Task<ContractAnswer> PointsAsync(ServiceCall call, CancellationToken ct)
    {
        var address = SettlementLedger.AddressOf(await _ledger.TaxpayerAsync(call.Cuit, ct));
        var points = await _ledger.PointsOfSaleAsync(call.Cuit, ct);
        return Ok(call, points.Select(p => new XElement("puntoVenta", new XElement("codigo", p.Number), new XElement("descripcion", address))));
    }

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var liquidation = request.Child("liquidacion");
        var producer = request.Child("tambero");
        var dairy = request.Child("tambo");
        var balance = request.Optional("balanceLitrosPorcentajesSolidos");
        var domestic = request.Optional("conceptosBasicosMercadoInterno");
        var foreign = request.Optional("conceptosBasicosMercadoExterno");
        var pointOfSale = (int)liquidation.Number("puntoVenta");
        var type = (int)liquidation.Number("tipoComprobante");
        var number = liquidation.Number("nroComprobante");
        var period = liquidation.Value("periodo") ?? "";
        var adjustment = liquidation.Optional("ajuste");
        var today = _ledger.Today;

        switch (await _ledger.CheckPointOfSaleAsync(call.Cuit, pointOfSale, ct))
        {
            case PointCheck.NoPoints: return Fail(call, 2082, "La cuit representada, no tiene puntos de venta activos para emitir una liquidación.");
            case PointCheck.Invalid: return Fail(call, 2086, "El punto de venta informado no es válido.");
        }
        if (producer.Number("cuit") == call.Cuit)
            return Fail(call, 2126, "La cuit del productor tambero y la del adquiriente no pueden ser iguales.");
        if (await _ledger.TaxpayerAsync(producer.Number("cuit"), ct) is { Active: false })
            return Fail(call, 2103, "La cuit tambero, no se encuentra activa o es inexistente.");

        Settlement? adjusted = null;
        if (AdjustmentTypes.Contains(type) && adjustment is null) return Fail(call, 1000, "Solicitud incompleta: debe especificar el campo <ajuste>.");
        if (!AdjustmentTypes.Contains(type) && adjustment is not null) return Fail(call, 1003, "Si no es un ajuste, no debe enviar datos en la etiqueta <ajuste>.");
        if (adjustment is not null)
        {
            if ((adjustment.Optional("formularioPapel") is null) == (adjustment.Optional("caeAAjustar") is null))
                return Fail(call, 1001, "Solicitud incompleta: para ajustes debe especificar uno y solo uno de los siguientes campos: <formularioPapel>, <caeAAjustar>.");
            var monetary = adjustment.Value("tipoAjuste") == "MONETARIO";
            if (monetary && (balance is not null || domestic is not null || foreign is not null))
                return Fail(call, 1005, "Para ajustes monetarios los siguientes campos debe ser nulos: balanceLitrosPorcentajesSolidos, conceptosBasicosMercadoInterno y conceptosBasicosMercadoExterno.");
            if (!monetary && balance is null) return Fail(call, 1006, "Para ajustes físicos debe informar el campo balanceLitrosPorcentajesSolidos.");
            if (adjustment.Optional("caeAAjustar") is not null)
            {
                adjusted = await _ledger.FindByCaeAsync(Service, adjustment.Number("caeAAjustar"), ct);
                var issued = adjusted?.DetailXml();
                if (adjusted is null || issued is null || adjusted.Cuit != call.Cuit || adjusted.ReceiverCuit != producer.Number("cuit")
                    || issued.Child("encabezado").Value("periodo") != period || issued.Child("tambo").Value("nroRenspa") != dairy.Value("nroRenspa"))
                    return Fail(call, 2004, "No se puede ajustar la liquidación ya que no fue encontrada por los siguientes parámetros: su número de CAE, CUIT del productor, CUIT del comprador, período y número de RENSPA.");
            }
        }

        var rate = liquidation.OptionalAmount("alicuotaIVA");
        if (type == 27 && rate != 21 || type is 45 or 48 && rate is not (0 or 21))
            return Fail(call, 2114, "La alícuota IVA no se corresponde con la situación del tambero.");
        if (type is not (27 or 45 or 48) && rate is not null)
            return Fail(call, 2115, "No debe informar la alícuota IVA para el tipo de comprobante que intenta generar.");

        if (!PeriodOpen(period, today)) return Fail(call, 2055, "Error, el período seleccionado para el tipo de liquidación que se intenta realizar, no es válido.");
        var date = liquidation.Day("fechaComprobante") ?? today;
        if ($"{date:yyyy}/{date:MM}" != period)
            return Fail(call, 2121, "La fecha de comprobante de la liquidación debe pertenecer al año y al mes de la liquidación.");
        if (date > today) return Fail(call, 2131, "La fecha del comprobante no puede ser posterior a hoy.");
        if (today.DayNumber - date.DayNumber > 10)
            return Fail(call, 2130, "La fecha de comprobante de la liquidación no debe tener más de 10 días de diferencia hacia atrás con la fecha de hoy.");
        if (balance is not null && balance.Number("litrosDecomisados") == balance.Number("litrosRemitidos"))
            return Fail(call, 2016, "Error en balance de litros porcentaje de sólidos: El valor de Kg remitidos no puede ser igual al los Kg decomisados.");
        if (balance is not null && balance.Number("litrosDecomisados") > balance.Number("litrosRemitidos"))
            return Fail(call, 2024, "Error en balance de litros porcentaje de sólidos: El valor decomisados debe ser menor a remitidos.");
        foreach (var concept in request.Children("bonificacionPenalizacion"))
        {
            var commercial = concept.Number("codBonificacionPenalizacion") == 41;
            if (commercial && (concept.Optional("importe") is null || concept.Optional("porcentajeAAplicar") is not null))
                return Fail(call, 2122, "Para bonificaciones/penalizaciones con código igual a 41 debe informar el campo <importe> y no <porcentajeAAplicar>.");
            if (!commercial && (concept.Optional("porcentajeAAplicar") is null || concept.Optional("importe") is not null))
                return Fail(call, 2123, "Para bonificaciones/penalizaciones con código distinto a 41 debe informar el campo <porcentajeAAplicar> y no <importe>.");
        }

        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, type, ct);
        var last = await _ledger.LastAsync(Service, call.Cuit, pointOfSale, type, ct);
        if (number != (last?.Number ?? 0) + 1)
            return Fail(call, 2074, "N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados.");
        if (last is not null && date < last.Date)
            return Fail(call, 2132, "La fecha de comprobante ingresada, es anterior a la fecha de comprobante de una liquidación(activa) generada con anterioridad para la misma cuit, punto de venta y tipo de liquidacion.");
        if (adjustment is null && await DuplicateAsync(call.Cuit, period, producer.Number("cuit"), dairy.Value("nroRenspa"), ct))
            return Fail(call, 2078, "Error en la generación de la liquidación, la misma debe ser única por período, CUIT comprador, CUIT productor y número de RENSPA.");

        var basics = new[] { Basics("conceptoBasicoMercadoInterno", domestic), Basics("conceptoBasicoMercadoExterno", foreign) };
        var basic = basics.Sum(b => b.Amount);
        var concepts = request.Children("bonificacionPenalizacion").Select(c => Concept(c, basic)).ToList();
        var others = request.Children("otroImpuesto").Select(OtherTax).ToList();
        var qualityBonus = concepts.Where(c => c.Code is not (41 or 50)).Sum(c => c.Amount);
        var commercialBonus = concepts.Where(c => c.Code == 41).Sum(c => c.Amount);
        var commercialDebit = concepts.Where(c => c.Code == 50).Sum(c => c.Amount);
        var otherTaxes = others.Sum(o => o.Amount);
        var total = Round(basic + qualityBonus + commercialBonus - commercialDebit);
        var vat = Round(total * (rate ?? 0) / 100);
        var net = total - otherTaxes + vat;
        if (net <= 0) return Fail(call, 2113, "El importe total neto de la liquidación no puede ser cero o menor a cero.");

        var cae = _ledger.NewCae();
        var buyer = await _ledger.TaxpayerAsync(call.Cuit, ct);
        var producerTaxpayer = await _ledger.TaxpayerAsync(producer.Number("cuit"), ct);
        var producerAddress = producerTaxpayer?.Profile.Address ?? TaxAddress.Default;
        var detail = new XElement("liquidacion",
            new XElement("encabezado",
                new XElement("cae", cae), new XElement("tipoComprobante", type), new XElement("nroComprobante", number),
                new XElement("fechaComprobante", Iso(date)), new XElement("periodo", period),
                new XElement("fechaVencimiento", Iso(today.AddDays(SettlementLedger.CaeDays))), new XElement("fecha", Iso(today)),
                new XElement("puntoVenta", pointOfSale), Maybe("iibbAdquirente", liquidation.Value("iibbAdquirente")),
                new XElement("cuitComprador", call.Cuit), Maybe("razonSocialComprador", buyer?.Name.ToUpperInvariant()),
                Maybe("domicilioComprador", buyer is null ? null : SettlementLedger.AddressOf(buyer)),
                Maybe("domicilioSede", liquidation.Value("domicilioSede")), Maybe("inscripcionRegistroPublico", liquidation.Value("inscripcionRegistroPublico")),
                Maybe("situacionIVAComprador", buyer is null ? null : SettlementLedger.VatText(buyer.VatCondition)),
                liquidation.Children("condicionVenta").Select(c => Copy(c)),
                Maybe("datosAdicionales", liquidation.Value("datosAdicionales"))),
            Copy(adjustment),
            new XElement("tambero",
                Maybe("razonSocial", producerTaxpayer?.Name.ToUpperInvariant()),
                producerTaxpayer is null ? null : new XElement("domicilioFiscal", producerAddress.Street),
                producerTaxpayer is null ? null : new XElement("localidad", producerAddress.Locality),
                producerTaxpayer is null ? null : new XElement("codPostal", producerAddress.PostalCode),
                producerTaxpayer is null ? null : new XElement("provincia", producerAddress.Province),
                Maybe("situacionIVA", producerTaxpayer is null ? null : SettlementLedger.VatText(producerTaxpayer.VatCondition)),
                new XElement("cuit", producer.Number("cuit")), Maybe("iibb", producer.Value("iibb"))),
            Copy(dairy),
            Balance(balance),
            basics.Select(b => b.Element),
            concepts.Select(c => c.Element),
            others.Select(o => o.Element),
            new XElement("resumenTotales",
                new XElement("totalBasico", Money(basic)),
                new XElement("totalBonificacionesCalidad", Money(qualityBonus)),
                new XElement("totalPenalizacionesCalidad", Money(0)),
                new XElement("totalBonificacionesComerciales", Money(commercialBonus)),
                new XElement("totalDebitosComerciales", Money(commercialDebit)),
                new XElement("totalOtrosImpuestos", Money(otherTaxes)),
                new XElement("totalLiquidacion", Money(total)),
                Maybe("alicuotaIVA", rate),
                new XElement("importeIVA", Money(vat)),
                new XElement("totalNetoLiquidacion", Money(net))),
            request.Children("remito").Select(r => new XElement("remito", r.Value.Trim())));

        var settlement = new Settlement(Service, call.Cuit, pointOfSale, type, number, cae, date, today, today.AddDays(SettlementLedger.CaeDays),
            producer.Number("cuit"), net, "lecheria", adjustment is not null, adjusted is null ? [] : [adjusted.KeyOf()],
            Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        return Answer(call, settlement, withPdf: true);
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
            && s.DetailXml() is var detail && detail.Child("encabezado").Value("periodo") == period && detail.Child("tambo").Value("nroRenspa") == renspa);

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
        var amount = concept.OptionalAmount("importe") ?? Round(basic * concept.Amount("porcentajeAAplicar") / 100);
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
        if (withPdf)
            liquidation.Add(new XElement("pdf", Pdf($"ARCA - Liquidacion Unica Mensual Lecheria {settlement.VoucherType:D3}-{settlement.PointOfSale:D5}-{settlement.Number:D8}",
                [$"CUIT comprador: {settlement.Cuit}", $"CUIT tambero: {settlement.ReceiverCuit}", $"Fecha: {Iso(settlement.Date)}",
                    $"Total neto: {Money(settlement.Total)}", $"CAE: {settlement.Cae}", $"Vencimiento CAE: {Iso(settlement.CaeExpiry)}"])));
        return Ok(call, liquidation);
    }

    private static ContractAnswer NotFound(ServiceCall call) =>
        Fail(call, 2044, "La liquidación que intenta obtener no existe según los parámetros de búsqueda.");

    private static ContractAnswer Fail(ServiceCall call, long code, string text) => SettlementXml.Fail(call, null, (code, text));
}
