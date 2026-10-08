using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Liquidaciones.SettlementXml;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// WSLCA, the sugar cane purchase liquidation (docs/arca/servicios/wslca.md),
/// on the CXF stack: auth/cuitRepresentada and the https namespace. The mill,
/// a VAT registered issuer (1002), authorizes the purchase from a grower with
/// a CAE on último + 1 (else 1500), listing the cane delivery notes, each
/// liquidated once (1303) and adding up to the kilos of the detail (1304).
/// Price adjustments (2 for the issuer, 3 for the buyer) point at items of one
/// or more liquidations; a physical adjustment annuls the liquidation (1604
/// after). Both take the adjusted liquidation's type and sequence (1600).
/// Queries by number; errors in respuesta/errores and metadata on every answer.
/// ArcaSim's choices where the manual is silent: the CAE has 14 digits like the
/// rest of the family (the manual's examples show 7, 8 and 9); resending an
/// authorized number is 1500; an empty sequence answers 0; dates go out with
/// the -03:00 offset of the manual's examples; any well-formed delivery note
/// exists (where they come from is not documented); the voucher type
/// descriptions are ArcaSim's wording of §2.7.6.3's "Clase A" and "Clase B".
/// The other parameter tables keep the contract's answer.
/// </summary>
public sealed class LcaRules(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
    : IServiceBehavior
{
    private readonly SettlementLedger _ledger = new(store, taxpayers, codes, locks, clock);

    public string Service => "wslca";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "consultarUltimoNroComprobantePorPtoVta" => await LastAsync(call, ct),
        "generarLiquidacion" => await GenerateAsync(call, ct),
        "consultarLiquidacionPorNroComprobante" => await ConsultAsync(call, ct),
        "generarAjustePrecio" => await PriceAsync(call, ct),
        "generarAjusteFisico" => await PhysicalAsync(call, ct),
        "consultarPuntosVenta" => await PointsAsync(call, ct),
        "consultarTiposComprobante" => Ok(call,
            new XElement("tipoComprobante", new XElement("codigo", 171), new XElement("descripcion", "Liquidación de compra de caña de azúcar - Clase A")),
            new XElement("tipoComprobante", new XElement("codigo", 172), new XElement("descripcion", "Liquidación de compra de caña de azúcar - Clase B")),
            Metadata()),
        _ => null,
    };

    // ---- Numbering and queries -------------------------------------------------------

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"), ct);
        return Ok(call, new XElement("nroComprobante", last?.Number ?? 0), Metadata());
    }

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct) =>
        await FindAsync(call, call.Request.Child("solicitud").Child("comprobante"), ct) is { } found
            ? Answer(call, found)
            : Fail(call, 800, "No se encontraron resultados según los parámetros de búsqueda informados.");

    private async Task<ContractAnswer> PointsAsync(ServiceCall call, CancellationToken ct) =>
        Ok(call, await _ledger.PointsAnswerAsync(call.Cuit, ct), Metadata());

    // ---- Liquidations ----------------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var voucher = request.Child("emisor").Child("comprobante");
        var receiver = request.Child("receptor");
        var general = request.Child("datosGenerales");
        var type = (int)voucher.Number("tipoComprobante");
        var date = general.Day("fechaComprobante") ?? _ledger.Today;

        if (type is not (171 or 172)) return Fail(call, 1206, $"El tipo de comprobante informado es inexistente: {type}.");
        if (await IssuerProblemAsync(call, (int)voucher.Number("puntoVenta"), date, ct) is { } problem) return problem;
        if (receiver.Number("cuit") == call.Cuit) return Fail(call, 1100, "Emisor y receptor no pueden ser iguales.");
        if (await _ledger.TaxpayerAsync(receiver.Number("cuit"), ct) is { } grower
            && grower.VatCondition == VatCondition.ResponsableInscripto != (type == 171))
            return Fail(call, 1207, "Tipo de comprobante inexistente o incorrecto según la situación frente al IVA de los actores.");
        if (Repeated(general.Children("condicionVenta"))) return Fail(call, 1204, "No puede informar condiciones de venta repetidas en una misma liquidación.");
        if (Repeated(general.Children("medioPago"))) return Fail(call, 1210, "No puede informar medios de pago repetidos en una misma liquidación.");
        var notes = request.Children("remito").ToList();
        if (notes.Select(n => n.Value("nroRemito")).Distinct().Count() != notes.Count)
            return Fail(call, 1302, "No puede informar remitos repetidos en una misma liquidación.");
        var items = request.Children("detalle").ToList();

        // A delivery note is liquidated once in the service, whoever issues it and on whatever sequence, so the
        // check and the record that follows it share one hold, taken inside the sequence's.
        using var _ = await _ledger.LockAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), type, ct);
        using var unique = await _ledger.LockUniqueAsync(Service, "remitos", ct);
        foreach (var note in notes)
            if (await store.GetAsync<SettlementByCae>(Service, $"remito/{note.Value("nroRemito")}", ct) is not null)
                return Fail(call, 1303, $"Remito #{note.Value("nroRemito")}: El remito que desea agregar ya se encuentra liquidado.");
        if (notes.Sum(n => n.Number("kilos")) != items.Sum(i => i.Number("cantidad")))
            return Fail(call, 1304, "La cantidad de kilos informada en los remitos debe ser igual a la cantidad de kilos en el detalle de la liquidación.");
        if (await SequenceProblemAsync(call, voucher, date, ct) is { } wrong) return wrong;

        var lines = items.Select((item, i) => Item(i + 1, item, item.Amount("precioUnitario"), type, null)).ToList();
        var concepts = request.Children("otroConcepto").Select(c => Concept(c, type)).ToList();
        var taxes = request.Children("tributo").Select(Tax).ToList();
        var cae = _ledger.NewCae();
        var detail = new XElement("respuesta",
            Authorization(cae),
            await IssuerAsync(call.Cuit, voucher, request.Child("emisor"), ct),
            await ReceiverAsync(receiver, ct),
            General(general),
            notes.Select(n => new XElement("remito", new XElement("nroRemito", n.Value("nroRemito")), new XElement("kilos", n.Number("kilos")))),
            lines.Select(l => l.Element), concepts.Select(c => c.Element), taxes.Select(t => t.Element),
            Summary(lines, concepts, taxes, out var total));

        var settlement = await IssueAsync(call, voucher, cae, date, receiver.Number("cuit"), total, false, [], detail, ct);
        foreach (var note in notes) await store.PutAsync(Service, $"remito/{note.Value("nroRemito")}", new SettlementByCae(settlement.KeyOf()), ct);
        return Answer(call, settlement);
    }

    // ---- Adjustments -----------------------------------------------------------------

    private async Task<ContractAnswer> PriceAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var issuer = request.Child("emisor");
        var voucher = issuer.Child("comprobante");
        var general = request.Child("datosGenerales");
        var type = (int)voucher.Number("tipoComprobante");
        var kind = issuer.Number("tipoAjuste");
        var date = general.Day("fechaComprobante") ?? _ledger.Today;

        if (kind is not (2 or 3)) return Fail(call, 1603, $"El tipo de ajuste informado es inexistente: {kind}.");
        var lines = new List<Line>();
        Settlement? first = null;
        foreach (var (item, i) in request.Children("detalle").Select((d, i) => (d, i)))
        {
            var target = item.Child("comprobanteAjustado");
            var original = await FindAsync(call, target, ct);
            if (AdjustableProblem(call, target, original, type) is { } problem) return problem;
            var order = item.Number("nroOrdenItemAjustado");
            var source = original!.DetailXml().Children("detalle").FirstOrDefault(d => d.Number("nroOrden") == order);
            if (source is null)
                return Fail(call, 1602, $"El ítem {order} de la liquidación {target.Number("nroComprobante")} que intenta ajustar es inexistente.");
            first ??= original;
            lines.Add(Item(i + 1, source, item.Amount("diferenciaPrecio"), type, new XElement("detalleAjuste", Copy(target, "comprobanteAjustado"),
                new XElement("nroOrdenAjustado", order))));
        }
        if (first is null) return Fail(call, 1601, "La liquidación que intenta ajustar es inexistente: 0.");
        if (await IssuerProblemAsync(call, (int)voucher.Number("puntoVenta"), date, ct) is { } issuerProblem) return issuerProblem;

        using var _ = await _ledger.LockAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), type, ct);
        if (await SequenceProblemAsync(call, voucher, date, ct) is { } wrong) return wrong;

        var concepts = request.Children("otroConcepto").Select(c => Concept(c, type)).ToList();
        var taxes = request.Children("tributo").Select(Tax).ToList();
        var cae = _ledger.NewCae();
        var issued = first.DetailXml();
        var detail = new XElement("respuesta",
            Authorization(cae),
            new XElement("ajuste", new XElement("tipoAjuste", kind)),
            await IssuerAsync(call.Cuit, voucher, issued.Child("emisor"), ct),
            Copy(issued.Optional("receptor")),
            General(general),
            lines.Select(l => l.Element), concepts.Select(c => c.Element), taxes.Select(t => t.Element),
            Summary(lines, concepts, taxes, out var total));

        var settlement = await IssueAsync(call, voucher, cae, date, first.ReceiverCuit, total, true, [first.KeyOf()], detail, ct);
        return Answer(call, settlement);
    }

    private async Task<ContractAnswer> PhysicalAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitud");
        var issuer = request.Child("emisor");
        var voucher = issuer.Child("comprobante");
        var target = issuer.Child("comprobanteAjustado");
        var type = (int)voucher.Number("tipoComprobante");
        var date = request.Day("fechaComprobante") ?? _ledger.Today;
        var returned = request.Value("devolucionMercaderia") is "true" or "1";

        // The original is annulled once, whoever asks and from whatever point of sale: the checks on it and
        // the mark that it was annulled share one hold, taken inside the sequence's, and read it again inside.
        using var _ = await _ledger.LockAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), type, ct);
        using var document = await _ledger.LockDocumentAsync(KeyOf(call, target), ct);
        var original = await FindAsync(call, target, ct);
        if (AdjustableProblem(call, target, original, type) is { } problem) return problem;
        if (await IssuerProblemAsync(call, (int)voucher.Number("puntoVenta"), date, ct) is { } issuerProblem) return issuerProblem;
        if (await SequenceProblemAsync(call, voucher, date, ct) is { } wrong) return wrong;

        var cae = _ledger.NewCae();
        var issued = original!.DetailXml();
        var general = Copy(issued.Child("datosGenerales"))!;
        general.Child("fechaComprobante").Value = Stamp(date);
        var detail = new XElement("respuesta",
            Authorization(cae),
            new XElement("ajuste", new XElement("tipoAjuste", 1), new XElement("esDevolucionMercaderia", returned ? "true" : "false")),
            await IssuerAsync(call.Cuit, voucher, issued.Child("emisor"), ct),
            Copy(issued.Optional("receptor")),
            general,
            issued.Children("remito").Select(r => Copy(r)),
            issued.Children("detalle").Select(d =>
            {
                var item = Copy(d)!;
                item.Elements("detalleAjuste").Remove();
                item.Add(new XElement("detalleAjuste", Copy(target, "comprobanteAjustado"), new XElement("nroOrdenAjustado", d.Number("nroOrden"))));
                return item;
            }),
            issued.Children("otroConcepto").Select(c => Copy(c)), issued.Children("tributo").Select(t => Copy(t)),
            Copy(issued.Optional("resumenTotales")));

        var settlement = await IssueAsync(call, voucher, cae, date, original.ReceiverCuit, original.Total, true, [original.KeyOf()], detail, ct);
        await _ledger.UpdateAsync(original with { State = Settlement.Annulled }, ct);
        return Answer(call, settlement);
    }

    // ---- Checks ----------------------------------------------------------------------

    private static readonly SettlementProblem NoPoints = new(910, "No posee puntos de venta habilitados para Comprobantes en Línea.");
    private static readonly SettlementProblem InvalidPoint = new(1001, "El punto de venta informado es inválido.");

    private static string KeyOf(ServiceCall call, XElement voucher) =>
        SettlementLedger.Key(call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"), voucher.Number("nroComprobante"));

    private Task<Settlement?> FindAsync(ServiceCall call, XElement voucher, CancellationToken ct) =>
        _ledger.FindAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"), voucher.Number("nroComprobante"), ct);

    private ContractAnswer? AdjustableProblem(ServiceCall call, XElement target, Settlement? original, int type)
    {
        var number = target.Number("nroComprobante");
        if (original is null) return Fail(call, 1601, $"La liquidación que intenta ajustar es inexistente: {number}.");
        if (original.VoucherType != type)
            return Fail(call, 1600, "El tipo de comprobante informado para el ajuste debe ser igual al tipo de la liquidación que intenta ajustar.");
        return original.State == Settlement.Annulled ? Fail(call, 1604, $"La liquidación que intenta ajustar se encuentra anulada: {number}.") : null;
    }

    private async Task<ContractAnswer?> IssuerProblemAsync(ServiceCall call, int pointOfSale, DateOnly date, CancellationToken ct)
    {
        if (await _ledger.PointProblemAsync(call.Cuit, pointOfSale, NoPoints, InvalidPoint, ct) is { } point) return Fail(call, point);
        if (await _ledger.TaxpayerAsync(call.Cuit, ct) is { VatCondition: not VatCondition.ResponsableInscripto })
            return Fail(call, 1002, "El emisor no corresponde a un contribuyente inscripto en el Impuesto al Valor Agregado.");
        var today = _ledger.Today;
        if (Math.Abs(date.DayNumber - today.DayNumber) > 5)
            return Fail(call, 1200, "La fecha de liquidación debe ser cinco días anteriores o posteriores a la fecha de generación del comprobante.");
        return date > today && (date.Year != today.Year || date.Month != today.Month)
            ? Fail(call, 1212, "Si la fecha de comprobante es mayor a la fecha actual, ambas deben pertenecer al mismo mes calendario.")
            : null;
    }

    private async Task<ContractAnswer?> SequenceProblemAsync(ServiceCall call, XElement voucher, DateOnly date, CancellationToken ct)
    {
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"), ct);
        if (voucher.Number("nroComprobante") != (last?.Number ?? 0) + 1)
            return Fail(call, 1500, "N° de comprobante incorrecto para el tipo de comprobante y punto de venta ingresados.");
        return last is not null && date < last.Date
            ? Fail(call, 1201, "La fecha de liquidación debe ser mayor o igual a la fecha de la última liquidación autorizada para el mismo tipo de comprobante.")
            : null;
    }

    private static bool Repeated(IEnumerable<XElement> entries)
    {
        var list = entries.Select(e => e.Value("codigo")).ToList();
        return list.Distinct().Count() != list.Count;
    }

    // ---- Answers ---------------------------------------------------------------------

    private async Task<Settlement> IssueAsync(ServiceCall call, XElement voucher, long cae, DateOnly date, long receiver, decimal total,
        bool adjustment, List<string> adjusts, XElement detail, CancellationToken ct)
    {
        var today = _ledger.Today;
        var settlement = new Settlement(Service, call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"),
            voucher.Number("nroComprobante"), cae, date, today, today.AddDays(SettlementLedger.CaeDays), receiver, total, "cana", adjustment, adjusts,
            Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        return settlement;
    }

    private ContractAnswer Answer(ServiceCall call, Settlement settlement)
    {
        var answer = settlement.DetailXml();
        answer.Add(new XElement("pdf", Pdf($"ARCA - Liquidacion de compra de cana de azucar {settlement.VoucherType:D3}-{settlement.PointOfSale:D5}-{settlement.Number:D8}",
            [$"CUIT emisor: {settlement.Cuit}", $"CUIT receptor: {settlement.ReceiverCuit}", $"Fecha: {Iso(settlement.Date)}",
                $"Total: {Money(settlement.Total)}", $"CAE: {settlement.Cae}", $"Vencimiento CAE: {Iso(settlement.CaeExpiry)}"])));
        answer.Add(new XElement("errores"));
        answer.Add(Metadata());
        return call.Ok(new XElement(call.Operation.Output, answer));
    }

    /// <summary>xsd:date with the zone, as the manual's answers show them (2019-05-06-03:00).</summary>
    private static string Stamp(DateOnly date) => ArgentinaTime.DateWithOffset(date);

    private XElement Authorization(long cae) => new("autorizacion",
        new XElement("cae", cae),
        new XElement("fechaVencimientoCae", Stamp(_ledger.Today.AddDays(SettlementLedger.CaeDays))),
        new XElement("fechaProcesoAFIP", Stamp(_ledger.Today)));

    private async Task<XElement> IssuerAsync(long cuit, XElement voucher, XElement source, CancellationToken ct)
    {
        var taxpayer = await _ledger.TaxpayerAsync(cuit, ct);
        return new XElement("emisor",
            new XElement("comprobante",
                new XElement("puntoVenta", voucher.Number("puntoVenta")),
                new XElement("tipoComprobante", voucher.Number("tipoComprobante")),
                new XElement("nroComprobante", voucher.Number("nroComprobante"))),
            source.Day("fechaInicioActividades") is { } started ? new XElement("fechaInicioActividades", Stamp(started)) : null,
            Maybe("razonSocial", taxpayer?.Name.ToUpperInvariant()), Maybe("iibb", source.Value("iibb")), Maybe("leyenda", source.Value("leyenda")),
            Maybe("situacionIVA", taxpayer is null ? null : SettlementLedger.VatText(taxpayer.VatCondition)),
            new XElement("domicilioPuntoVenta", SettlementLedger.AddressOf(taxpayer)));
    }

    private async Task<XElement> ReceiverAsync(XElement receiver, CancellationToken ct)
    {
        var taxpayer = await _ledger.TaxpayerAsync(receiver.Number("cuit"), ct);
        return new XElement("receptor",
            new XElement("cuit", receiver.Number("cuit")), Maybe("razonSocial", taxpayer?.Name.ToUpperInvariant()), Maybe("iibb", receiver.Value("iibb")),
            Maybe("situacionIVA", taxpayer is null ? null : SettlementLedger.VatText(taxpayer.VatCondition)),
            Maybe("domicilio", taxpayer?.Profile.Address?.Street),
            new XElement("localidad", receiver.Number("localidad")), new XElement("provincia", receiver.Number("provincia")));
    }

    private static XElement General(XElement general) => new("datosGenerales",
        new XElement("fechaComprobante", Stamp(general.Day("fechaComprobante") ?? DateOnly.MinValue)),
        general.Children("condicionVenta").Select(c => new XElement("condicionVenta", new XElement("codigo", c.Number("codigo")), Maybe("detalle", c.Value("detalle")))),
        general.Children("medioPago").Select(m => new XElement("medioPago", new XElement("codigo", m.Number("codigo")), Maybe("detalle", m.Value("detalle")))));

    private sealed record Line(XElement Element, decimal Subtotal, decimal Vat);

    /// <summary>§4.1: subtotal = quantity × price; VAT only on class A (171).</summary>
    private static Line Item(int order, XElement source, decimal price, int type, XElement? link)
    {
        var quantity = source.Amount("cantidad");
        var rate = source.Amount("alicuotaIVA");
        var subtotal = Round(quantity * price);
        var vat = type == 171 ? Round(subtotal * rate / 100) : 0;
        return new Line(new XElement("detalle",
            new XElement("nroOrden", order),
            Maybe("producto", source.Value("producto")),
            new XElement("cantidad", Money(quantity)),
            Maybe("unidadMedida", source.Value("unidadMedida")),
            new XElement("precioUnitario", Money(price)),
            new XElement("importeSubtotal", Money(subtotal)),
            new XElement("alicuotaIVA", Money(rate)),
            new XElement("importeIVA", Money(vat)),
            new XElement("importeTotal", Money(subtotal + vat)),
            link), subtotal, vat);
    }

    private static Line Concept(XElement concept, int type)
    {
        var amount = concept.OptionalAmount("importe") ?? Round(concept.Amount("baseImponible") * concept.Amount("alicuota") / 100);
        var rate = concept.Amount("alicuotaIVA");
        var vat = type == 171 ? Round(amount * rate / 100) : 0;
        return new Line(new XElement("otroConcepto",
            new XElement("codConcepto", concept.Number("codConcepto")), Maybe("detalle", concept.Value("detalle")),
            concept.OptionalAmount("baseImponible") is { } basis ? new XElement("baseImponible", Money(basis)) : null,
            concept.OptionalAmount("alicuota") is { } share ? new XElement("alicuota", Money(share)) : null,
            new XElement("alicuotaIVA", Money(rate)), new XElement("importe", Money(amount)), new XElement("importeIVA", Money(vat))), amount, vat);
    }

    private static Line Tax(XElement tax)
    {
        var amount = tax.OptionalAmount("importe") ?? Round(tax.Amount("baseImponible") * tax.Amount("alicuota") / 100);
        return new Line(new XElement("tributo",
            new XElement("codTributo", tax.Number("codTributo")), Maybe("detalle", tax.Value("detalle")),
            tax.OptionalAmount("baseImponible") is { } basis ? new XElement("baseImponible", Money(basis)) : null,
            tax.OptionalAmount("alicuota") is { } share ? new XElement("alicuota", Money(share)) : null,
            new XElement("importe", Money(amount))), amount, 0);
    }

    /// <summary>§4.1: net taxed = subtotal + other concepts; general subtotal = net taxed + VAT; total = general subtotal + taxes.</summary>
    private static XElement Summary(List<Line> items, List<Line> concepts, List<Line> taxes, out decimal total)
    {
        var subtotal = items.Sum(i => i.Subtotal);
        var others = concepts.Sum(c => c.Subtotal);
        var vat = items.Sum(i => i.Vat) + concepts.Sum(c => c.Vat);
        var general = subtotal + others + vat;
        total = general + taxes.Sum(t => t.Subtotal);
        return new XElement("resumenTotales",
            new XElement("importeSubtotal", Money(subtotal)),
            new XElement("importeOtrosConceptos", Money(others)),
            new XElement("importeNetoGravado", Money(subtotal + others)),
            new XElement("importeIVA", Money(vat)),
            new XElement("importeSubtotalGeneral", Money(general)),
            new XElement("importeTributos", Money(taxes.Sum(t => t.Subtotal))),
            new XElement("importeTotal", Money(total)));
    }

    private ContractAnswer Fail(ServiceCall call, long code, string text) => Fail(call, new SettlementProblem(code, text));

    private ContractAnswer Fail(ServiceCall call, SettlementProblem problem) => SettlementXml.Fail(call, Metadata(), problem);

    private ContractAnswer Ok(ServiceCall call, params object?[] content) => SettlementXml.Ok(call, content);

    /// <summary>§2.5: the server and its time, with milliseconds and offset as in the manual's examples.</summary>
    private XElement Metadata() => new("metadata",
        new XElement("servidor", "arcasim"),
        new XElement("fechaHora", clock.Now.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)));
}
