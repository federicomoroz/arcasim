using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Liquidaciones.SettlementXml;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// WSLTV, the green tobacco liquidation (docs/arca/servicios/wsltv.md): the
/// buyer authorizes the purchase of the bales of each romaneo with a CAE on
/// último + 1; a bale's traceability code is used once (1039). Credit and
/// debit adjustments (ajustarLiquidacion) cover one or more liquidations of the
/// same seller, priced by class after consultarTotalesDeClasesPorComprobantes-
/// ParaAjustar adds them up; a physical adjustment (generarAjusteFisico) takes
/// back one liquidation once (1135). Adjustments share the type and sequence
/// of what they adjust (1136). Queries by CAE or number, with the PDF on
/// request; errors in respuesta/errores, no metadata.
/// ArcaSim's choices where the manual is silent: a wrong number is 1071 (the
/// manual names no code); an empty sequence leaves nroComprobante out; type
/// 150 (A) adds 21% VAT and 151 (B) none; the net is the bales' amount minus
/// the bonus plus the freight, and retentions and taxes come off the total; a
/// physical adjustment leaves the liquidation "ajustada"; the CAE expires 10
/// days after the liquidation date (the examples show 10 to 15). Deposits, varieties
/// and classes are not checked (the tables are not in the manual), and the
/// parameter tables other than points of sale keep the contract's answer.
/// </summary>
public sealed class LtvRules(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
    : IServiceBehavior
{
    private readonly SettlementLedger _ledger = new(store, taxpayers, codes, locks, clock);

    public string Service => "wsltv";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "consultarUltimoComprobanteXPuntoVenta" => await LastAsync(call, ct),
        "generarLiquidacion" => await GenerateAsync(call, ct),
        "consultarLiquidacionXNroComprobante" => await ByNumberAsync(call, ct),
        "consultarLiquidacionXCAE" => await ByCaeAsync(call, ct),
        "consultarTotalesDeClasesPorComprobantesParaAjustar" => await TotalsAsync(call, ct),
        "ajustarLiquidacion" => await AdjustAsync(call, ct),
        "generarAjusteFisico" => await PhysicalAsync(call, ct),
        "consultarPuntosVentas" => await PointsAsync(call, ct),
        _ => null,
    };

    // ---- Numbering and queries -------------------------------------------------------

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"), ct);
        return Ok(call, last is null ? null : new XElement("nroComprobante", last.Number));
    }

    private async Task<ContractAnswer> ByNumberAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var found = await _ledger.FindAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"),
            request.Number("nroComprobante"), ct);
        return found is null ? Missing(call) : Answer(call, found, request.Value("pdf") is "true" or "1");
    }

    private async Task<ContractAnswer> ByCaeAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var found = await _ledger.FindByCaeAsync(Service, request.Number("cae"), ct);
        return found is null || found.Cuit != call.Cuit ? Missing(call) : Answer(call, found, request.Value("pdf") is "true" or "1");
    }

    private async Task<ContractAnswer> PointsAsync(ServiceCall call, CancellationToken ct) =>
        Ok(call, await _ledger.PointsAnswerAsync(call.Cuit, ct));

    private async Task<ContractAnswer> TotalsAsync(ServiceCall call, CancellationToken ct)
    {
        var originals = new List<Settlement>();
        foreach (var voucher in call.Request.Child("solicitud").Children("comprobante"))
        {
            if (await FindAsync(call, voucher, ct) is not { IsAdjustment: false } found) return NoData(call);
            originals.Add(found);
        }
        var classes = originals.SelectMany(o => o.DetailXml().Descendants("detalleClase"))
            .GroupBy(c => (Class: c.Value("codClase"), Price: c.Value("precioXKgFardo")))
            .Select(g => new XElement("detalleTotalClase",
                new XElement("codClase", g.Key.Class),
                new XElement("totalFardos", g.Sum(c => c.Number("cantidadFardos"))),
                new XElement("totalKilos", g.Sum(c => c.Number("pesoFardosKg"))),
                new XElement("precioXKilo", g.Key.Price)));
        return Ok(call, classes);
    }

    // ---- Liquidations ----------------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var liquidation = request.ChildOrEmpty("liquidacion");
        var receiver = request.ChildOrEmpty("receptor");
        var pointOfSale = (int)liquidation.Number("puntoVenta");
        var type = (int)liquidation.Number("tipoComprobante");
        var number = liquidation.Number("nroComprobante");
        var date = liquidation.Day("fechaLiquidacion") ?? _ledger.Today;

        if (await CommonProblemAsync(call, pointOfSale, date, ct) is { } problem) return problem;
        if (await _ledger.TaxpayerAsync(receiver.Number("cuit"), ct) is { } seller)
        {
            var classA = seller.VatCondition == VatCondition.ResponsableInscripto;
            if (classA && type != 150) return Fail(call, 1014, "Tipo de comprobante no válido para la cuit de receptor. La misma corresponde a tipo A.");
            if (!classA && type != 151) return Fail(call, 1015, "Tipo de comprobante no válido para la cuit de receptor. La misma corresponde a tipo B.");
        }
        var bales = request.Children("romaneo").SelectMany(r => r.Children("fardo")).Select(f => f.Value("codTrazabilidad") ?? "").ToList();

        // A bale goes in one liquidation of the service, whoever issues it and on whatever sequence, so the
        // check and the record that follows it share one hold, taken inside the sequence's.
        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, type, ct);
        using var unique = await _ledger.LockUniqueAsync(Service, "bales", ct);
        foreach (var bale in bales)
            if (bales.Count(b => b == bale) > 1 || await store.GetAsync<SettlementByCae>(Service, $"fardo/{bale}", ct) is not null)
                return Fail(call, 1039, "Un código de trazabilidad de un fardo que intenta agregar, ya fue utilizado en otra liquidación.");
        if (await SequenceProblemAsync(call, pointOfSale, type, number, date, ct) is { } wrong) return wrong;

        var prices = request.Children("precioClase").GroupBy(p => p.Value("claseTabaco")).ToDictionary(g => g.Key ?? "", g => g.First().Amount("precio"));
        var romaneos = request.Children("romaneo").Select(r => new XElement("romaneo",
            Maybe("nroRomaneo", r.Value("nroRomaneo")), Maybe("fechaRomaneo", r.Day("fechaRomaneo")),
            r.Children("fardo").GroupBy(f => f.Value("claseTabaco") ?? "").Select(g => ClassLine(g.Key, g.Count(),
                g.Sum(f => f.Number("peso")), prices.GetValueOrDefault(g.Key))))).ToList();
        var gross = romaneos.SelectMany(r => r.Elements("detalleClase")).Sum(c => c.Amount("importe"));
        var totals = Totals.Of(type, gross, request.Child("bonificacion")?.Amount("importe") ?? 0, request.Child("flete")?.Amount("importe"),
            request.Children("retencion").ToList(), request.Children("tributo").ToList());
        if (totals.Total <= 0) return Fail(call, TotalNotPositive);

        var cae = _ledger.NewCae();
        var detail = new XElement("liquidacion",
            Header(type, date, pointOfSale, liquidation.Number("codDepositoAcopio"), number, cae, null, await _ledger.TaxpayerAsync(call.Cuit, ct)),
            await IssuerAsync(call.Cuit, liquidation.Value("iibbEmisor"), liquidation.Day("fechaInicioActividad"), ct),
            await ReceiverAsync(receiver.Number("cuit"), receiver, ct),
            Copy(liquidation.Child("titularCompra")),
            new XElement("datosOperacion",
                Maybe("tipoCompra", liquidation.Value("tipoCompra")), Maybe("variedadTabaco", liquidation.Value("variedadTabaco")),
                Maybe("puerta", liquidation.Value("puerta")), Maybe("nroTarjeta", liquidation.Value("nroTarjeta")), Maybe("horas", liquidation.Value("horas")),
                Maybe("control", liquidation.Value("control")), Maybe("nroInterno", liquidation.Value("nroInterno")),
                Maybe("codProvinciaOrigenTabaco", liquidation.Value("codProvinciaOrigenTabaco")),
                liquidation.Children("condicionVenta").Select(c => Copy(c))),
            new XElement("detalleOperacion", romaneos,
                new XElement("cantidadTotalFardos", bales.Count),
                new XElement("pesoTotalFardosKg", request.Children("romaneo").SelectMany(r => r.Children("fardo")).Sum(f => f.Number("peso")))),
            totals.Elements(),
            Copy(request.Child("flete")), Copy(request.Child("bonificacion")));

        var settlement = new Settlement(Service, call.Cuit, pointOfSale, type, number, cae, date, _ledger.Today, date.AddDays(SettlementLedger.CaeDays),
            receiver.Number("cuit"), totals.Total, "tabaco", false, [], Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        foreach (var bale in bales) await store.PutAsync(Service, $"fardo/{bale}", new SettlementByCae(settlement.KeyOf()), ct);
        return Answer(call, settlement, withPdf: true);
    }

    // ---- Adjustments -----------------------------------------------------------------

    private async Task<ContractAnswer> AdjustAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var adjustment = request.ChildOrEmpty("liquidacionAjuste");
        var pointOfSale = (int)adjustment.Number("puntoVenta");
        var type = (int)adjustment.Number("tipoComprobante");
        var number = adjustment.Number("nroComprobante");
        var date = adjustment.Day("fechaAjusteLiquidacion") ?? _ledger.Today;
        var credit = adjustment.Value("tipoAjuste") == "C";

        var originals = new List<Settlement>();
        foreach (var voucher in adjustment.Children("comprobanteAAjustar"))
        {
            if (await FindAsync(call, voucher, ct) is not { } found) return Missing(call);
            if (found.IsAdjustment) return NoData(call);
            if (found.VoucherType != type) return Fail(call, WrongAdjustmentType);
            originals.Add(found);
        }
        if (originals.Count == 0)
            return Fail(call, 1117, "Para generar un comprobante de ajuste debe agregar al menos un comprobante correspondiente a una liquidación.");
        if (originals.Any(o => o.ReceiverCuit != adjustment.Number("cuitReceptor")))
            return Fail(call, 1118, "Uno o más comprobantes que intenta ajustar, pertenecen a liquidaciones emitidas para distintos vendedores.");
        if (await CommonProblemAsync(call, pointOfSale, date, ct) is { } problem) return problem;

        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, type, ct);
        if (await SequenceProblemAsync(call, pointOfSale, type, number, date, ct) is { } wrong) return wrong;

        var classes = request.Children("precioClase").Select(p => ClassLine(p.Value("claseTabaco") ?? "", p.Number("totalFardos"),
            p.Number("totalKilos"), p.Amount("precio"), "claseAjuste")).ToList();
        var totals = Totals.Of(type, classes.Sum(c => c.Amount("importe")), 0, null, request.Children("retencion").ToList(), request.Children("tributo").ToList());
        if (totals.Total <= 0) return Fail(call, TotalNotPositive);

        var cae = _ledger.NewCae();
        var first = originals[0].DetailXml();
        var detail = new XElement("liquidacion",
            Header(type, date, pointOfSale, adjustment.Number("codDepositoAcopio"), number, cae, credit ? "C" : "D", await _ledger.TaxpayerAsync(call.Cuit, ct)),
            await IssuerAsync(call.Cuit, adjustment.Value("iibbEmisor"), adjustment.Day("fechaInicioActividad"), ct),
            Copy(first.Child("receptor")), Copy(first.Child("datosOperacion")),
            new XElement("detalleOperacion", classes,
                new XElement("cantidadTotalFardos", classes.Sum(c => c.Number("cantidadFardos"))),
                new XElement("pesoTotalFardosKg", classes.Sum(c => c.Number("pesoFardosKg")))),
            totals.Elements(),
            originals.Select(o => new XElement("caeAjustado", o.Cae)));

        var settlement = new Settlement(Service, call.Cuit, pointOfSale, type, number, cae, date, _ledger.Today, date.AddDays(SettlementLedger.CaeDays),
            originals[0].ReceiverCuit, totals.Total, "tabaco", true, originals.Select(o => o.KeyOf()).ToList(), Settlement.Active,
            detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        return Answer(call, settlement, withPdf: true);
    }

    private async Task<ContractAnswer> PhysicalAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var pointOfSale = (int)request.Number("puntoVenta");
        var type = (int)request.Number("tipoComprobante");
        var number = request.Number("nroComprobante");
        var date = request.Day("fechaLiquidacion") ?? _ledger.Today;

        var target = request.ChildOrEmpty("comprobanteAAjustar");

        // The original is adjusted once, whoever asks and from whatever point of sale: the checks on it and
        // the mark that it was adjusted share one hold, taken inside the sequence's, and read it again inside.
        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, type, ct);
        using var document = await _ledger.LockDocumentAsync(KeyOf(call, target), ct);
        if (await FindAsync(call, target, ct) is not { } original) return Missing(call);
        if (original.IsAdjustment) return NoData(call);
        if (original.State == Settlement.Adjusted) return Fail(call, 1135, "El comprobante ingresado ya fue ajustado previamente.");
        if (original.VoucherType != type) return Fail(call, WrongAdjustmentType);
        if (await CommonProblemAsync(call, pointOfSale, date, ct) is { } problem) return problem;
        if (await SequenceProblemAsync(call, pointOfSale, type, number, date, ct) is { } wrong) return wrong;

        var cae = _ledger.NewCae();
        var issued = original.DetailXml();
        var classes = issued.Descendants("detalleClase")
            .GroupBy(c => (Class: c.Value("codClase") ?? "", Price: c.Amount("precioXKgFardo")))
            .Select(g => ClassLine(g.Key.Class, g.Sum(c => c.Number("cantidadFardos")), g.Sum(c => c.Number("pesoFardosKg")), g.Key.Price, "claseAjuste"))
            .ToList();
        var oldHeader = issued.ChildOrEmpty("cabecera");
        var detail = new XElement("liquidacion",
            Header(type, date, pointOfSale, oldHeader.Number("codDepositoAcopio"), number, cae, "F", await _ledger.TaxpayerAsync(call.Cuit, ct)),
            await IssuerAsync(call.Cuit, issued.ChildOrEmpty("emisor").Value("iibb"), request.Day("fechaInicioActividad"), ct),
            Copy(issued.Child("receptor")), Copy(issued.Child("titularCompra")), Copy(issued.Child("datosOperacion")),
            new XElement("detalleOperacion", classes, Copy(issued.ChildOrEmpty("detalleOperacion").Child("cantidadTotalFardos")),
                Copy(issued.ChildOrEmpty("detalleOperacion").Child("pesoTotalFardosKg"))),
            issued.Children("retencion").Select(r => Copy(r)), issued.Children("tributo").Select(t => Copy(t)), Copy(issued.Child("totalesOperacion")),
            new XElement("caeAjustado", original.Cae));

        var settlement = new Settlement(Service, call.Cuit, pointOfSale, type, number, cae, date, _ledger.Today, date.AddDays(SettlementLedger.CaeDays),
            original.ReceiverCuit, original.Total, "tabaco", true, [original.KeyOf()], Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        await _ledger.UpdateAsync(original with { State = Settlement.Adjusted }, ct);
        return Answer(call, settlement, withPdf: true);
    }

    // ---- Checks ----------------------------------------------------------------------

    private static readonly SettlementProblem NoPoints = new(1003, "No posee puntos de venta habilitados para el actual sistema de ingreso.");
    private static readonly SettlementProblem InvalidPoint = new(1006, "El punto de venta ingresado no es válido.");
    private static readonly SettlementProblem TotalNotPositive = new(1072, "El importe total de la liquidación o ajuste debe ser mayor a cero.");
    private static readonly SettlementProblem WrongAdjustmentType = new(1136, "El tipo de comprobante del ajuste debe ser el mismo que el del comprobante a ajustar.");

    private static string KeyOf(ServiceCall call, XElement voucher) =>
        SettlementLedger.Key(call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"), voucher.Number("nroComprobante"));

    private Task<Settlement?> FindAsync(ServiceCall call, XElement voucher, CancellationToken ct) =>
        _ledger.FindAsync(Service, call.Cuit, (int)voucher.Number("puntoVenta"), (int)voucher.Number("tipoComprobante"), voucher.Number("nroComprobante"), ct);

    private async Task<ContractAnswer?> CommonProblemAsync(ServiceCall call, int pointOfSale, DateOnly date, CancellationToken ct)
    {
        if (await _ledger.PointProblemAsync(call.Cuit, pointOfSale, NoPoints, InvalidPoint, ct) is { } point) return Fail(call, point);
        return Math.Abs(date.DayNumber - _ledger.Today.DayNumber) > 10
            ? Fail(call, 1012, "La fecha de liquidación no puede diferir en más de 10 días anteriores o posteriores a la fecha actual.")
            : null;
    }

    private async Task<ContractAnswer?> SequenceProblemAsync(ServiceCall call, int pointOfSale, int type, long number, DateOnly date, CancellationToken ct) =>
        await _ledger.CheckNumberAsync(Service, call.Cuit, pointOfSale, type, number, date, ct) switch
        {
            NumberCheck.WrongNumber => Fail(call, 1071, "Número de Comprobante no válido."),
            NumberCheck.EarlierDate => Fail(call, 1013, "La fecha de comprobante no puede ser anterior a la fecha del último comprobante generado para el mismo punto de venta."),
            _ => null,
        };

    // ---- Answers ---------------------------------------------------------------------

    private static XElement ClassLine(string tobaccoClass, long bales, long kilos, decimal price, string name = "detalleClase") => new(name,
        new XElement("codClase", tobaccoClass),
        new XElement("cantidadFardos", bales),
        new XElement("pesoFardosKg", kilos),
        new XElement("precioXKgFardo", price.ToString(CultureInfo.InvariantCulture)),
        new XElement("importe", Money(kilos * price)));

    private static XElement Header(int type, DateOnly date, int pointOfSale, long deposit, long number, long cae, string? adjustment, Taxpayer? issuer) => new("cabecera",
        new XElement("tipoComprobante", type),
        new XElement("fechaLiquidacion", Iso(date)),
        new XElement("fechaVencimiento", Iso(date.AddDays(SettlementLedger.CaeDays))),
        new XElement("puntoVenta", pointOfSale),
        new XElement("domicilioPuntoVenta", SettlementLedger.AddressOf(issuer)),
        new XElement("codDepositoAcopio", deposit),
        new XElement("nroComprobante", number),
        new XElement("cae", cae),
        Maybe("tipoAjuste", adjustment));

    private async Task<XElement> IssuerAsync(long cuit, string? iibb, DateOnly? started, CancellationToken ct)
    {
        var taxpayer = await _ledger.TaxpayerAsync(cuit, ct);
        var address = taxpayer?.Profile.Address ?? TaxAddress.Default;
        return new XElement("emisor",
            new XElement("cuit", cuit), Maybe("razonSocial", taxpayer?.Name.ToUpperInvariant()),
            Maybe("situacionIVA", taxpayer is null ? null : SettlementLedger.VatText(taxpayer.VatCondition)),
            new XElement("domicilio", address.Street), new XElement("nombreLocalidad", address.Locality), new XElement("codProvincia", address.ProvinceId),
            Maybe("iibb", iibb), Maybe("fechaInicioActividad", started));
    }

    private async Task<XElement> ReceiverAsync(long cuit, XElement receiver, CancellationToken ct)
    {
        var taxpayer = await _ledger.TaxpayerAsync(cuit, ct);
        var address = taxpayer?.Profile.Address;
        return new XElement("receptor",
            new XElement("cuit", cuit), Maybe("razonSocial", taxpayer?.Name.ToUpperInvariant()),
            Maybe("situacionIVA", taxpayer is null ? null : SettlementLedger.VatText(taxpayer.VatCondition)),
            Maybe("domicilio", address?.Street), Maybe("nombreLocalidad", address?.Locality), Maybe("codProvincia", address?.ProvinceId),
            Maybe("iibb", receiver.Value("iibb")), Maybe("nroSocio", receiver.Value("nroSocio")), Maybe("nroFET", receiver.Value("nroFET")));
    }

    private sealed record Totals(decimal Freight, decimal Bonus, decimal Net, decimal Rate, decimal Vat, decimal Retentions, decimal Taxes,
        List<XElement> RetentionLines, List<XElement> TaxLines)
    {
        public decimal Subtotal => Net + Vat;
        public decimal Total => Subtotal - Retentions - Taxes;

        public static Totals Of(int type, decimal gross, decimal bonus, decimal? freight, List<XElement> retentions, List<XElement> taxes)
        {
            var net = Round(gross - bonus + (freight ?? 0));
            var rate = type == 150 ? 21m : 0m;
            return new Totals(freight ?? 0, bonus, net, rate, Round(net * rate / 100),
                retentions.Sum(r => r.Amount("importe")), taxes.Sum(t => t.Amount("importe")),
                retentions.Select(r => new XElement("retencion", Maybe("codigo", r.Value("codRetencion")), Maybe("descripcion", r.Value("descripcion")),
                    new XElement("importe", Money(r.Amount("importe"))))).ToList(),
                taxes.Select(t => new XElement("tributo", Maybe("codigo", t.Value("codigoTributo")), Maybe("descripcion", t.Value("descripcion")),
                    new XElement("baseImponible", Money(t.Amount("baseImponible"))), new XElement("alicuota", Money(t.Amount("alicuota"))),
                    new XElement("importe", Money(t.Amount("importe"))))).ToList());
        }

        public IEnumerable<XElement> Elements() => RetentionLines.Concat(TaxLines).Append(new XElement("totalesOperacion",
            new XElement("importeFlete", Money(Freight)),
            new XElement("importeBonificacion", Money(Bonus)),
            new XElement("importeNeto", Money(Net)),
            new XElement("alicuotaIVA", Money(Rate)),
            new XElement("importeIVA", Money(Vat)),
            new XElement("subtotal", Money(Subtotal)),
            new XElement("totalRetenciones", Money(Retentions)),
            new XElement("totalTributos", Money(Taxes)),
            new XElement("total", Money(Total))));
    }

    private static ContractAnswer Answer(ServiceCall call, Settlement settlement, bool withPdf)
    {
        var liquidation = settlement.DetailXml();
        if (withPdf) liquidation.Add(PdfOf(settlement, "Liquidacion de Tabaco Verde", "acopiador", "productor", "Total"));
        return Ok(call, liquidation);
    }

    private static ContractAnswer Missing(ServiceCall call) => Fail(call, 1120, "El comprobante ingresado es inexistente.");

    private static ContractAnswer NoData(ServiceCall call) =>
        Fail(call, 1127, "No hay datos para los comprobantes ingresados, o bien, alguno de ellos corresponde a un ajuste.");

    private static ContractAnswer Fail(ServiceCall call, long code, string text) => Fail(call, new SettlementProblem(code, text));

    private static ContractAnswer Fail(ServiceCall call, SettlementProblem problem) => SettlementXml.Fail(call, null, problem);
}
