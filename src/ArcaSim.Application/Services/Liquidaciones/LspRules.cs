using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Liquidaciones.SettlementXml;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// WSLSP, the cattle, pig and poultry liquidations (docs/arca/servicios/wslsp.md):
/// generarLiquidacion and generarLiquidacionAvicola authorize a liquidation
/// with a CAE on the number the client picks (último + 1, else 1009), the
/// queries by number return what was issued, and generarAjuste /
/// generarAjusteAvicola issue credit or debit adjustments with their own CAE
/// on the adjusted voucher's sequence. An adjustment that takes back every
/// item in full is an annulment (until day 6 of the next month, never twice,
/// never of an adjustment). Errors go in respuesta/errores with the manual's
/// codes, and every answer carries metadata.
/// ArcaSim's choices where the manual is silent: the last number of an empty
/// sequence is 0; resending an authorized number is 1009; adjustments take the
/// adjusted voucher's type and sequence (inferred from the examples); the
/// adjustment mode of a purely financial adjustment reads "Financiero";
/// poultry liquidations allow 10 days around the processing date for all
/// operations; the net total is gross + VAT − expenses − their VAT − taxes;
/// poultry bonuses and penalties are listed but left out of the totals, since
/// whether a concept adds or takes away comes from a table the manual does not
/// print; the voucher type descriptions are ArcaSim's wording of the codes in §4.1.
/// Parameter tables other than points of sale and voucher types keep the
/// contract's answer.
/// </summary>
public sealed class LspRules(IDocumentStore store, ITaxpayerRepository taxpayers, IAuthorizationCodes codes, SequenceLocks locks, IClock clock)
    : IServiceBehavior
{
    private const string Bovine = "bovina";
    private const string Avian = "avicola";

    /// <summary>How far from the processing date a voucher's date may fall (2200): 5 days, 10 in poultry.</summary>
    private const int WindowDays = 5;
    private const int PoultryWindowDays = 10;

    private static readonly Dictionary<int, int[]> TypesByOperation = new()
    {
        [1] = [180, 182], [2] = [180, 182], [3] = [180, 182], [4] = [183, 185], [5] = [186, 188, 189], [6] = [190, 191],
        [101] = [180, 182], [102] = [180, 182], [103] = [180, 182], [104] = [183, 185], [105] = [186, 188, 189], [106] = [190, 191],
    };

    private static readonly (int Code, string Description)[] VoucherTypes =
    [
        (180, "Cuenta de venta y líquido producto A - Hacienda"),
        (182, "Cuenta de venta y líquido producto B - Hacienda"),
        (183, "Liquidación de compra A - Hacienda"),
        (185, "Liquidación de compra B - Hacienda"),
        (186, "Liquidación de compra directa A - Hacienda"),
        (188, "Liquidación de compra directa B - Hacienda"),
        (189, "Liquidación de compra directa C - Hacienda"),
        (190, "Liquidación de venta directa A - Hacienda"),
        (191, "Liquidación de venta directa B - Hacienda"),
    ];

    private readonly SettlementLedger _ledger = new(store, taxpayers, codes, locks, clock);

    public string Service => "wslsp";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "consultarUltimoNroComprobantePorPtoVta" => await LastAsync(call, ct),
        "generarLiquidacion" => await GenerateAsync(call, avian: false, ct),
        "generarLiquidacionAvicola" => await GenerateAsync(call, avian: true, ct),
        "consultarLiquidacionPorNroComprobante" => await ConsultAsync(call, avian: false, ct),
        "consultarLiquidacionAvicolaPorNroComp" => await ConsultAsync(call, avian: true, ct),
        "generarAjuste" => await AdjustAsync(call, avian: false, ct),
        "generarAjusteAvicola" => await AdjustAsync(call, avian: true, ct),
        "consultarPuntosVenta" => await PointsAsync(call, ct),
        "consultarTiposComprobante" => Ok(call, VoucherTypes.Select(t => new XElement("tipoComprobante",
            new XElement("codigo", t.Code), new XElement("descripcion", t.Description))), Metadata()),
        _ => null,
    };

    // ---- Numbering and queries -------------------------------------------------------

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var last = await _ledger.LastAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"), ct);
        return Ok(call, new XElement("nroComprobante", last?.Number ?? 0), Metadata());
    }

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, bool avian, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var found = await _ledger.FindAsync(Service, call.Cuit, (int)request.Number("puntoVenta"), (int)request.Number("tipoComprobante"),
            request.Number("nroComprobante"), ct);
        if (found is null || found.Kind != (avian ? Avian : Bovine))
            return Fail(call, 1000, "No se encontraron resultados según los parámetros de búsqueda informados.");
        return Answer(call, found);
    }

    private async Task<ContractAnswer> PointsAsync(ServiceCall call, CancellationToken ct) =>
        Ok(call, await _ledger.PointsAnswerAsync(call.Cuit, ct), Metadata());

    // ---- Liquidations ----------------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, bool avian, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var issuer = request.ChildOrEmpty("emisor");
        var operation = (int)request.Number("codOperacion");
        var pointOfSale = (int)issuer.Number("puntoVenta");
        var type = (int)issuer.Number("tipoComprobante");
        var number = issuer.Number("nroComprobante");

        if (!TypeFits(operation, type, avian))
            return Fail(call, 2006, "Emisor: El tipo de comprobante no es válido para el tipo de operación que intenta realizar.");
        if (await PointProblemAsync(call, pointOfSale, ct) is { } pointProblem) return pointProblem;
        var data = request.ChildOrEmpty("datosLiquidacion");
        var date = data.Day("fechaComprobante") ?? _ledger.Today;
        if (OutOfWindow(date, avian) is { } window) return Fail(call, window);

        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, type, ct);
        if (await WrongNumberAsync(call, pointOfSale, type, number, ct) is { } wrong) return wrong;

        var lines = request.Children("itemDetalleLiquidacion").Select((item, i) => new Line(i + 1, item.Number("cantidad"),
            item.Amount("precioUnitario"), item.ChildDecimal("alicuotaIVA"), item)).ToList();
        var expenses = request.Children("gasto").Select(Expense).ToList();
        var taxes = request.Children("tributo").Select(Tax).ToList();
        var totals = Totals.Of(lines, expenses, taxes);
        if (totals.Net < 0) return Fail(call, 4000, "El importe neto de la operación no puede ser negativo.");

        var cae = _ledger.NewCae();
        var today = _ledger.Today;
        var taxpayer = await _ledger.TaxpayerAsync(call.Cuit, ct);
        var detail = avian
            ? new XElement("respuesta",
                Header(operation, cae, today),
                Issuer(issuer, taxpayer, type, avian),
                AvianReceiver(request.ChildOrEmpty("receptor")),
                new XElement("datosLiquidacion",
                    Maybe("fechaComprobante", date), Maybe("fechaOperacion", data.Day("fechaOperacion")), Maybe("codMotivo", data.Value("codMotivo")),
                    data.Children("condicionVenta").Select(c => Copy(c)), Copy(data.Child("granja"))),
                request.Children("dte").Select(d => Copy(d)),
                request.Children("remito").Select(r => Copy(r)),
                lines.Select(l => Item(l, avian)),
                Production(request.Child("resultadoProductivo")),
                request.Children("bonificacionesPenalizaciones").Select(b => Copy(b, "bonificacionPenalizacion")),
                expenses.Select(e => e.Element), taxes.Select(t => t.Element),
                Maybe("datosAdicionales", request.Value("datosAdicionales")),
                totals.Element())
            : new XElement("respuesta",
                Header(operation, cae, today),
                Issuer(issuer, taxpayer, type, avian),
                await ReceiverAsync(request.ChildOrEmpty("receptor"), ct),
                new XElement("datosLiquidacion",
                    Maybe("fechaComprobante", date), Maybe("fechaOperacion", data.Day("fechaOperacion")),
                    Maybe("lugarRealizacion", data.Value("lugarRealizacion")), Maybe("codMotivo", data.Value("codMotivo")),
                    Maybe("fechaRecepcion", data.Day("fechaRecepcion")), Maybe("fechaFaena", data.Day("fechaFaena")),
                    data.Child("frigorifico") is { } plant
                        ? new XElement("frigorifico", Maybe("cuit", plant.Value("cuit")), Maybe("nroPlanta", plant.Value("nroPlanta")))
                        : null),
                request.Children("guia").Select(g => Copy(g)),
                request.Children("dte").Select(d => Copy(d)),
                request.Children("remito").Select(r => Copy(r)),
                lines.Select(l => Item(l, avian)),
                expenses.Select(e => e.Element), taxes.Select(t => t.Element),
                Maybe("datosAdicionales", request.Value("datosAdicionales")),
                totals.Element());

        var receiver = avian ? request.ChildOrEmpty("receptor").Number("nroDoc") : request.ChildOrEmpty("receptor").ChildOrEmpty("operador").Number("cuit");
        var settlement = new Settlement(Service, call.Cuit, pointOfSale, type, number, cae, date, today, today.AddDays(SettlementLedger.CaeDays),
            receiver, totals.Net, avian ? Avian : Bovine, false, [], Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        return Answer(call, settlement);
    }

    // ---- Adjustments -----------------------------------------------------------------

    private async Task<ContractAnswer> AdjustAsync(ServiceCall call, bool avian, CancellationToken ct)
    {
        var request = call.Request.ChildOrEmpty("solicitud");
        var issuer = request.ChildOrEmpty("emisor");
        var target = issuer.ChildOrEmpty("comprobanteAAjustar");
        var credit = request.Value("tipoAjuste") == "C";
        var date = request.Day("fechaComprobante") ?? _ledger.Today;
        var pointOfSale = (int)issuer.Number("puntoVenta");
        var number = issuer.Number("nroComprobante");
        var items = request.Children("itemDetalleAjusteLiquidacion").ToList();
        var financial = request.ChildOrEmpty("ajusteFinanciero");
        var physical = items.Any(i => i.Child("ajusteFisico") is not null);
        var monetary = items.Any(i => i.Child("ajusteMonetario") is not null);

        var named = await _ledger.FindAsync(Service, call.Cuit, (int)target.Number("puntoVenta"), (int)target.Number("tipoComprobante"),
            target.Number("nroComprobante"), ct);
        if (named is null) return Fail(call, 3000, "La liquidación que intenta ajustar es inexistente.");

        // The original is annulled once, whoever asks and from whatever point of sale: the checks on it and
        // the mark that it was annulled share one hold, taken inside the sequence's (which the original's type
        // names), and read it again inside.
        using var _ = await _ledger.LockAsync(Service, call.Cuit, pointOfSale, named.VoucherType, ct);
        using var document = await _ledger.LockDocumentAsync(named.KeyOf(), ct);
        var original = await _ledger.FindAsync(Service, named.KeyOf(), ct) ?? named;
        var originalItems = original.DetailXml().Children("itemDetalleLiquidacion").ToDictionary(i => (int)i.Number("nroItem"));
        var annulment = credit && physical && !monetary && originalItems.Count > 0 && items.Count == originalItems.Count
                        && items.All(i => originalItems.TryGetValue((int)i.Number("nroItemAjustar"), out var item)
                                          && i.ChildOrEmpty("ajusteFisico").Number("cantidad") == item.Number("cantidad"));
        if (original.IsAdjustment)
            return annulment ? Fail(call, 5001, "No se puede anular un ajuste.") : Fail(call, 3007, "No se puede realizar un ajuste sobre otro ajuste.");
        if (original.Kind != (avian ? Avian : Bovine))
            return Fail(call, 3011, "El ajuste debe ser de la misma especie que la liquidación que intenta ajustar.");
        if (original.State == Settlement.Annulled) return Fail(call, 5002, "La liquidación ya se encuentra anulada.");
        if (date < original.Date || date.DayNumber - original.Date.DayNumber > 180)
            return Fail(call, 3001, "La fecha de comprobante de la liquidación que intenta ajustar debe estar dentro de los 180 días previos a la fecha de comprobante del ajuste.");
        if (physical && monetary) return Fail(call, 3002, "No se pueden realizar ajustes físicos y monetario en un mismo comprobante.");
        if (!physical && !monetary && !financial.HasElements)
            return Fail(call, 3006, "No se determinó ningún tipo de ajuste (físico, monetario o financiero).");
        if (annulment && _ledger.Today > new DateOnly(original.Date.Year, original.Date.Month, 1).AddMonths(1).AddDays(5))
            return Fail(call, 5000, "Solo se puede anular un comprobante hasta el día 6 inclusive del mes siguiente al de la liquidación.");
        if (await PointProblemAsync(call, pointOfSale, ct) is { } pointProblem) return pointProblem;
        if (OutOfWindow(date, avian) is { } window) return Fail(call, window);

        if (await WrongNumberAsync(call, pointOfSale, original.VoucherType, number, ct) is { } wrong) return wrong;

        var lines = items.Select((item, i) =>
        {
            var source = originalItems.GetValueOrDefault((int)item.Number("nroItemAjustar")) ?? new XElement("itemDetalleLiquidacion");
            return item.Child("ajusteFisico") is { } quantity
                ? new Line(i + 1, quantity.Number("cantidad"), source.Amount("precioUnitario"), source.ChildDecimal("alicuotaIVA"), source)
                : new Line(i + 1, source.Number("cantidad"), item.ChildOrEmpty("ajusteMonetario").Amount("precioUnitario"), source.ChildDecimal("alicuotaIVA"), source);
        }).ToList();
        var expenses = financial.Children("gasto").Select(Expense).ToList();
        var taxes = financial.Children("tributo").Select(Tax).ToList();
        var totals = Totals.Of(lines, expenses, taxes);

        var cae = _ledger.NewCae();
        var today = _ledger.Today;
        var issued = original.DetailXml();
        var operation = (int)issued.ChildOrEmpty("cabecera").Number("codOperacion");
        var adjustedIssuer = Copy(issued.ChildOrEmpty("emisor"))!;
        adjustedIssuer.ChildOrEmpty("puntoVenta").Value = pointOfSale.ToString(CultureInfo.InvariantCulture);
        adjustedIssuer.ChildOrEmpty("nroComprobante").Value = number.ToString(CultureInfo.InvariantCulture);
        var data = Copy(issued.ChildOrEmpty("datosLiquidacion"))!;
        data.ChildOrEmpty("fechaComprobante").Value = Iso(date);
        var detail = avian
            ? new XElement("respuesta",
                Header(operation, cae, today), adjustedIssuer, Copy(issued.Child("receptor")), data,
                lines.Select(l => Item(l, avian)),
                request.Children("bonificacionesPenalizaciones").Select(b => Copy(b, "bonificacionPenalizacion")),
                expenses.Select(e => e.Element), taxes.Select(t => t.Element),
                Maybe("datosAdicionales", request.Value("datosAdicionales")),
                totals.Element())
            : new XElement("respuesta",
                Header(operation, cae, today),
                new XElement("ajuste",
                    new XElement("tipoAjuste", credit ? "C" : "D"),
                    new XElement("modoAjuste", physical ? "Fisico" : monetary ? "Monetario" : "Financiero"),
                    new XElement("comprobanteAjustado",
                        new XElement("tipoComprobante", original.VoucherType),
                        new XElement("puntoVenta", original.PointOfSale),
                        new XElement("nroComprobante", original.Number))),
                adjustedIssuer, Copy(issued.Child("receptor")), data,
                lines.Select(l => Item(l, avian)),
                expenses.Select(e => e.Element), taxes.Select(t => t.Element),
                Maybe("datosAdicionales", request.Value("datosAdicionales")),
                totals.Element());

        var settlement = new Settlement(Service, call.Cuit, pointOfSale, original.VoucherType, number, cae, date, today,
            today.AddDays(SettlementLedger.CaeDays), original.ReceiverCuit, totals.Net, original.Kind, true, [original.KeyOf()],
            Settlement.Active, detail.ToString(SaveOptions.DisableFormatting));
        await _ledger.IssueAsync(settlement, ct);
        if (annulment) await _ledger.UpdateAsync(original with { State = Settlement.Annulled }, ct);
        return Answer(call, settlement);
    }

    // ---- Checks ----------------------------------------------------------------------

    private static bool TypeFits(int operation, int type, bool avian) =>
        avian
            ? operation is >= 201 and <= 208 && type is >= 157 and <= 170
            : TypesByOperation.TryGetValue(operation, out var types) && types.Contains(type);

    private static readonly SettlementProblem NoPoints = new(1007, "La CUIT representada no tiene puntos de venta activos para emitir una liquidación.");
    private static readonly SettlementProblem InvalidPoint = new(1008, "El punto de venta informado no es válido.");

    private async Task<ContractAnswer?> PointProblemAsync(ServiceCall call, int pointOfSale, CancellationToken ct) =>
        await _ledger.PointProblemAsync(call.Cuit, pointOfSale, NoPoints, InvalidPoint, ct) is { } problem ? Fail(call, problem) : null;

    /// <summary>The manual has no rule on the date against the last voucher's, so only the number is checked.</summary>
    private async Task<ContractAnswer?> WrongNumberAsync(ServiceCall call, int pointOfSale, int type, long number, CancellationToken ct) =>
        await _ledger.CheckNumberAsync(Service, call.Cuit, pointOfSale, type, number, date: null, ct) is NumberCheck.WrongNumber
            ? Fail(call, 1009, SettlementLedger.WrongNumberText)
            : null;

    /// <summary>2200: the voucher's date within N days of the processing date, N = 5 in sales and 10 in poultry (§4.9).</summary>
    private SettlementProblem? OutOfWindow(DateOnly date, bool avian)
    {
        var days = avian ? PoultryWindowDays : WindowDays;
        return Math.Abs(date.DayNumber - _ledger.Today.DayNumber) <= days
            ? null
            : new SettlementProblem(2200, $"Liquidación: La fecha de comprobante debe estar comprendida entre los {days} días próximos o anteriores a la fecha de proceso.");
    }

    // ---- Answers ---------------------------------------------------------------------

    private ContractAnswer Answer(ServiceCall call, Settlement settlement)
    {
        var answer = settlement.DetailXml();
        answer.Add(PdfOf(settlement, "Liquidacion sector pecuario", "emisor", "receptor", "Importe neto"));
        answer.Add(Metadata());
        return call.Ok(new XElement(call.Operation.Output, answer));
    }

    private ContractAnswer Fail(ServiceCall call, long code, string text) => Fail(call, new SettlementProblem(code, text));

    private ContractAnswer Fail(ServiceCall call, SettlementProblem problem) => SettlementXml.Fail(call, Metadata(), problem);

    /// <summary>§2.5: the server and its local time, without a zone (§4.10).</summary>
    private XElement Metadata() => new("metadata",
        new XElement("servidor", "arcasim"),
        new XElement("fechaHora", clock.Now.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)));

    /// <summary>nroCodigoBarras stays out: "ya no se retornará valor" since v1.3 (§4.12.3).</summary>
    private static XElement Header(int operation, long cae, DateOnly processed) => new("cabecera",
        new XElement("codOperacion", operation),
        new XElement("cae", cae),
        new XElement("fechaVencimientoCae", Iso(processed.AddDays(SettlementLedger.CaeDays))),
        new XElement("fechaProcesoAFIP", Iso(processed)));

    /// <summary>§1.5: homologación skips the issuer's checks and answers RI for A and B, MO for C (189).</summary>
    private static string VatOf(int type) => type == 189 ? "MO" : "RI";

    /// <summary>The emisor block of the answer; cuitAutorizado is only in the bovine schema.</summary>
    private static XElement Issuer(XElement issuer, Taxpayer? taxpayer, int type, bool avian) => new("emisor",
        Maybe("puntoVenta", issuer.Value("puntoVenta")), Maybe("tipoComprobante", type), Maybe("nroComprobante", issuer.Value("nroComprobante")),
        Maybe("codCaracter", issuer.Value("codCaracter")), Maybe("fechaInicioActividades", issuer.Day("fechaInicioActividades")),
        Maybe("razonSocial", taxpayer?.Name.ToUpperInvariant()), Maybe("iibb", issuer.Value("iibb")),
        Maybe("domicilioPuntoVenta", SettlementLedger.AddressOf(taxpayer)), Maybe("situacionIVA", VatOf(type)),
        Maybe("nroRUCA", issuer.Value("nroRUCA")), Maybe("nroRenspa", issuer.Value("nroRenspa")),
        avian ? null : Maybe("cuitAutorizado", issuer.Value("cuitAutorizado")));

    private async Task<XElement> ReceiverAsync(XElement receiver, CancellationToken ct)
    {
        var operator_ = receiver.ChildOrEmpty("operador");
        var taxpayer = await _ledger.TaxpayerAsync(operator_.Number("cuit"), ct);
        var address = taxpayer?.Profile.Address;
        return new XElement("receptor",
            Maybe("cuit", operator_.Value("cuit")), Maybe("nombre", taxpayer?.Name.ToUpperInvariant()), Maybe("codCaracter", receiver.Value("codCaracter")),
            Maybe("iibb", operator_.Value("iibb")), Maybe("nroRenspa", operator_.Value("nroRenspa")), Maybe("nroRUCA", operator_.Value("nroRUCA")),
            Maybe("cuitAutorizado", operator_.Value("cuitAutorizado")),
            taxpayer is null ? null : new XElement("domicilio", (address ?? TaxAddress.Default).Street),
            taxpayer is null ? null : new XElement("nombreLocalidad", (address ?? TaxAddress.Default).Locality),
            taxpayer is null ? null : new XElement("codProvincia", (address ?? TaxAddress.Default).ProvinceId));
    }

    private static XElement AvianReceiver(XElement receiver) => new("receptor",
        Maybe("codCaracter", receiver.Value("codCaracter")), Maybe("tipoDoc", receiver.Value("tipoDoc")), Maybe("nroDoc", receiver.Value("nroDoc")),
        Maybe("nombre", receiver.Value("nombreApellido")), Maybe("iibb", receiver.Value("iibb")), Maybe("domicilio", receiver.Value("domicilio")),
        Maybe("nombreLocalidad", receiver.Value("nombreLocalidad")), Maybe("codProvincia", receiver.Value("codProvincia")),
        Maybe("codigoPostal", receiver.Value("codigoPostal")), Maybe("nroRUCA", receiver.Value("nroRUCA")), Maybe("nroRenspa", receiver.Value("nroRenspa")));

    private static XElement? Production(XElement? result)
    {
        if (result is null) return null;
        var mortality = result.Number("bbIngresados") - result.Number("cantidadCabezasAvesVivasSalidas") - result.Number("cantidadCabezasAvesFaenadas");
        return new XElement("resultadoProductivo",
            Maybe("bbIngresados", result.Value("bbIngresados")),
            Maybe("cantidadCabezasAvesVivasSalidas", result.Value("cantidadCabezasAvesVivasSalidas")),
            Maybe("cantidadKilosAvesVivasSalidas", result.Value("cantidadKilosAvesVivasSalidas")),
            Maybe("cantidadCabezasAvesFaenadas", result.Value("cantidadCabezasAvesFaenadas")),
            Maybe("cantidadKilosAvesFaenadas", result.Value("cantidadKilosAvesFaenadas")),
            mortality >= 0 ? new XElement("cantidadCabezasMortandad", mortality) : null,
            Maybe("edad", result.Value("edad")),
            Maybe("kilosConsumoAlimentoBalanceado", result.Value("kilosConsumoAlimentoBalanceado")));
    }

    private sealed record Line(int Item, long Quantity, decimal Price, decimal? Vat, XElement Source)
    {
        public decimal Gross => Round(Quantity * Price);
        public decimal VatAmount => Vat is { } rate ? Round(Gross * rate / 100) : 0;
    }

    /// <summary>
    /// An itemDetalleLiquidacion of the answer, with the fields of the species' schema in its order: the raza, the
    /// tipoIVANulo, the tropa and the cuts are bovine; the kind of meat is avian.
    /// </summary>
    private static XElement Item(Line line, bool avian)
    {
        var item = line.Source;
        return new XElement("itemDetalleLiquidacion",
            new XElement("nroItem", line.Item),
            Maybe("cuitCliente", item.Value("cuitCliente")), Maybe("codCategoria", item.Value("codCategoria")),
            new XElement("cantidad", line.Quantity), Maybe("cantidadCabezas", item.Value("cantidadCabezas")),
            avian ? null : Copy(item.Child("raza")),
            Maybe("tipoLiquidacion", item.Value("tipoLiquidacion")), new XElement("precioUnitario", Money(line.Price)),
            Maybe("alicuotaIVA", line.Vat),
            avian ? null : Maybe("tipoIVANulo", item.Value("tipoIVANulo")), avian ? null : Maybe("nroTropa", item.Value("nroTropa")),
            Maybe("cantidadKgVivo", item.Value("cantidadKgVivo")),
            avian
                ? Maybe("tipoCarneAviar", item.Value("tipoCarneAviar"))
                : new[]
                {
                    Maybe("cantidadPorCorte", item.Value("cantidadPorCorte")),
                    item.ChildDecimal("precioRecupero") is { } recovery ? new XElement("precioRecupero", Money(recovery)) : null,
                    Maybe("codCorte", item.Value("codCorte")),
                },
            new XElement("importeBruto", Money(line.Gross)), new XElement("importeIVA", Money(line.VatAmount)),
            new XElement("importeTotal", Money(line.Gross + line.VatAmount)),
            item.Children("liquidacionCompraAsociada").Select(a => Copy(a)));
    }

    private sealed record Charge(XElement Element, decimal Amount, decimal Vat);

    private static Charge Expense(XElement expense)
    {
        var amount = expense.ChildDecimal("importe") ?? Round(expense.Amount("baseImponible") * expense.Amount("alicuota") / 100);
        var rate = expense.ChildDecimal("alicuotaIVA");
        var vat = rate is { } r ? Round(amount * r / 100) : 0;
        return new Charge(new XElement("gasto",
            Maybe("codGasto", expense.Value("codGasto")), Maybe("descripcion", expense.Value("descripcion")),
            expense.ChildDecimal("baseImponible") is { } basis ? new XElement("baseImponible", Money(basis)) : null,
            Maybe("alicuota", expense.Value("alicuota")), new XElement("importe", Money(amount)),
            Maybe("alicuotaIVA", expense.Value("alicuotaIVA")), rate is null ? null : new XElement("importeIVA", Money(vat)),
            Maybe("tipoIVANulo", expense.Value("tipoIVANulo"))), amount, vat);
    }

    private static Charge Tax(XElement tax)
    {
        var amount = tax.ChildDecimal("importe") ?? Round(tax.Amount("baseImponible") * tax.Amount("alicuota") / 100);
        return new Charge(new XElement("tributo",
            Maybe("codTributo", tax.Value("codTributo")), Maybe("descripcion", tax.Value("descripcion")),
            tax.ChildDecimal("baseImponible") is { } basis ? new XElement("baseImponible", Money(basis)) : null,
            Maybe("alicuota", tax.Value("alicuota")), new XElement("importe", Money(amount))), amount, 0);
    }

    private sealed record Totals(decimal Gross, decimal GrossVat, decimal Expenses, decimal ExpensesVat, decimal Taxes)
    {
        public decimal Net => Gross + GrossVat - Expenses - ExpensesVat - Taxes;

        public static Totals Of(List<Line> lines, List<Charge> expenses, List<Charge> taxes) => new(
            lines.Sum(l => l.Gross), lines.Sum(l => l.VatAmount), expenses.Sum(e => e.Amount), expenses.Sum(e => e.Vat), taxes.Sum(t => t.Amount));

        public XElement Element() => new("resumenTotales",
            new XElement("importeBruto", Money(Gross)),
            new XElement("importeIVASobreBruto", Money(GrossVat)),
            new XElement("importeTotalGastos", Money(Expenses)),
            new XElement("importeIVASobreGastos", Money(ExpensesVat)),
            new XElement("importeTotalTributos", Money(Taxes)),
            new XElement("importeTotalNeto", Money(Net)));
    }
}
