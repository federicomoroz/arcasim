using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>
/// WSCT, Comprobantes clase T for tourism (RG 3971), following
/// docs/arca/servicios/wsct.md: autorizarComprobante gives a CAE to one
/// Factura, Nota de Débito or Nota de Crédito T after checking numbering
/// ("último + 1" per CUIT, point of sale and type, 302), the authorization
/// type, the date, the currency and rate, the receiver and the issuer-receiver
/// relation, every item, the VAT subtotals, the hotel VAT refund
/// (importeReintegro), the other taxes, the total, the payment forms and the
/// associated vouchers, with the manual's codes, all of them in arrayErrores;
/// a credit note above its associated vouchers is authorized with observation
/// 807. consultarUltimoComprobanteAutorizado and consultarComprobanteTipoPVentaNro
/// answer what was authorized; the parameter queries answer the documented
/// values. Every voucher is recorded for constatación (wscdc). A number or a
/// point of sale below 1 gets the validator's arrayErroresFormato, as observed.
/// In production (SimulationSettings.Environment) the issuer checks 100 and 103
/// and the point of sale (301, 1001, 2001, consultarPuntosVenta) apply; in
/// homologación they do not, as the manual says (pág. 17).
///
/// ArcaSim's choices on what the spec leaves NO VERIFICADO:
/// - idImpositivo codes are those of ARCA's receiver condition table: 1 IVA
///   Responsable Inscripto, 5 Consumidor Final, 9 Cliente del Exterior (the 9
///   PyAfipWs sends);
/// - payment forms are 1 Tarjeta de Débito and 2 Tarjeta de Crédito (as 1200
///   names them), 3 Transferencia Bancaria and 4 Cuenta Corriente; card types
///   1 Visa, 2 Mastercard, 3 American Express, 99 Otra; account types 1 Caja de
///   Ahorro, 2 Cuenta Corriente: their codes are ArcaSim's;
/// - currencies, countries and other taxes are WSFEv1's tables;
/// - the CAE expires SimulationSettings.CaeLifetimeDays after the voucher's date;
/// - a resent voucher is refused with 302, its number no longer being the next;
/// - every business error found is reported, not just the first;
/// - 413 takes importeItem as VAT included: importeIVA = importeItem × 21/121;
///   413, 504 and 369 use the margin of 361 (0.01 per item or 0.01 %);
/// - 306 accepts from 2 % to 5 times the latest rate ArcaSim has; with
///   cancelaEnMismaMonedaExtranjera S the rate may be left out when ArcaSim has
///   one for the business day before the reference day, and is then stored;
/// - codigoPais is required by 355 for every relation;
/// - 352 (hospedaje activity, agencies registry), 314 (CUIT país) and 900
///   (datos adicionales) are not checked: ArcaSim has no such registries;
/// - consultarComprobanteTipoPVentaNro answers 2002 for a number that was not
///   authorized, and the stored comprobante carries the CAE in
///   codigoAutorizacion and its expiry in fechaVencimiento;
/// - consultarPuntosVenta answers an empty Return in homologación;
/// - consultarCotizacion answers the latest rate on or before the date (PES is
///   1), and an empty Return when there is none.
/// consultarCUITsPaises and consultarTiposDatosAdicionales keep the contract's answer.
/// </summary>
public sealed class WsctRules(
    ParameterTables parameters,
    IDocumentStore documents,
    ITaxpayerRepository taxpayers,
    IExchangeRates rates,
    IAuthorizationCodes codes,
    SequenceLocks locks,
    IClock clock,
    SimulationSettings settings,
    EventManager events) : IServiceBehavior
{
    private readonly VoucherBook _book = new(documents, "wsct");
    private readonly ConcurrentDictionary<string, XName> _returns = new();

    public string Service => "wsct";

    private static readonly (int Code, string Description)[] VoucherTypes =
        [(195, "Factura T"), (196, "Nota de Débito T"), (197, "Nota de Crédito T")];

    private static readonly int[] DocumentTypes = [80, 91, 94, 96];

    private static readonly (string Code, string Description)[] Conditions =
        [("1", "IVA Responsable Inscripto"), ("5", "Consumidor Final"), ("9", "Cliente del Exterior")];

    private const string Registered = "1";

    private static readonly (int Code, string Description)[] Relations =
    [
        (1, "Alojamiento Directo a Turista No Residente"),
        (2, "Alojamiento a Agencia de Viaje Residente"),
        (3, "Alojamiento a Agencia de Viaje No Residente"),
        (4, "Agencia de Viaje Residente a Agencia de Viaje No Residente"),
        (5, "Agencia de Viaje Residente a Turista No Residente"),
        (6, "Agencia de Viaje Residente a Agencia de Viaje Residente"),
    ];

    private static readonly (int Code, string Description)[] ItemTypes = [(0, "Item general"), (97, "Anticipo"), (99, "Descuento General")];

    private static readonly (int Code, string Description)[] TourismCodes =
    [
        (1, "Servicio de hotelería - alojamiento sin desayuno"),
        (2, "Servicio de hotelería - alojamiento con desayuno"),
        (5, "Excedente"),
    ];

    private const int Vat21 = 5;

    private static readonly (int Code, string Description)[] PaymentForms =
        [(1, "Tarjeta de Débito"), (2, "Tarjeta de Crédito"), (3, "Transferencia Bancaria"), (4, "Cuenta Corriente")];

    private static readonly (int Code, string Description)[] CardTypes =
        [(1, "Visa"), (2, "Mastercard"), (3, "American Express"), (99, "Otra")];

    private static readonly (int Code, string Description)[] AccountTypes = [(1, "Caja de Ahorro"), (2, "Cuenta Corriente")];

    /// <summary>The arrays of ComprobanteType, their rows and the rows' fields in schema order.</summary>
    private static readonly (string Array, string Row, string[] Fields)[] Arrays =
    [
        ("arrayItems", "item", ["tipo", "codigoTurismo", "codigo", "descripcion", "codigoAlicuotaIVA", "importeIVA", "importeItem"]),
        ("arrayComprobantesAsociados", "comprobanteAsociado", ["codigoTipoComprobante", "numeroPuntoVenta", "numeroComprobante"]),
        ("arrayOtrosTributos", "otroTributo", ["codigo", "descripcion", "baseImponible", "importe"]),
        ("arraySubtotalesIVA", "subtotalIVA", ["codigo", "importe"]),
        ("arrayDatosAdicionales", "tipoDatoAdicional", ["t", "c1", "c2", "c3", "c4", "c5", "c6"]),
        ("arrayFormasPago", "formaPago", ["codigo", "tipoTarjeta", "numeroTarjeta", "swiftCode", "tipoCuenta", "numeroCuenta"]),
    ];

    private static readonly string[] Header =
    [
        "codigoTipoComprobante", "numeroPuntoVenta", "numeroComprobante", "fechaEmision", "codigoTipoAutorizacion", "codigoAutorizacion",
        "fechaVencimiento", "codigoTipoDocumento", "numeroDocumento", "idImpositivo", "codigoPais", "domicilioReceptor",
        "codigoRelacionEmisorReceptor", "importeGravado", "importeNoGravado", "importeExento", "importeOtrosTributos", "importeReintegro",
        "importeTotal", "codigoMoneda", "cotizacionMoneda", "cancelaEnMismaMonedaExtranjera", "observaciones",
    ];

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "autorizarComprobante" => await AuthorizeAsync(call, ct),
        "consultarUltimoComprobanteAutorizado" => await LastAsync(call, ct),
        "consultarComprobanteTipoPVentaNro" => await ConsultAsync(call, ct),
        "consultarPuntosVenta" => await PointsOfSaleAsync(call, ct),
        "consultarCotizacion" => await QuoteAsync(call, ct),
        "consultarTiposComprobantes" => Short(call, "arrayTiposComprobantes", VoucherTypes),
        "consultarTiposDocumento" => Short(call, "arrayTiposDocumento",
            parameters.DocumentTypes.Where(d => DocumentTypes.Contains(int.Parse(d.Id, CultureInfo.InvariantCulture))).Select(d => (int.Parse(d.Id, CultureInfo.InvariantCulture), d.Desc))),
        "consultarCondicionesIVA" => Strings(call, "arrayCondicionesIVA", Conditions),
        "consultarRelacionEmisorReceptor" => Short(call, "arrayRelacionesEmisorReceptor", Relations),
        "consultarTiposItem" => Short(call, "arrayTiposItem", ItemTypes),
        "consultarCodigosItemTurismo" => Short(call, "arrayCodigosItem", TourismCodes),
        "consultarTiposIVA" => Strings(call, "arrayTiposIVA", [("5", "21%")]),
        "consultarTiposTributo" => Strings(call, "arrayTiposTributo", parameters.Taxes.Select(t => (t.Id, t.Desc))),
        "consultarMonedas" => Strings(call, "arrayTiposMoneda", parameters.Currencies.Select(c => (c.Id, c.Desc))),
        "consultarPaises" => Strings(call, "arrayPaises", parameters.Countries.Select(c => (c.Id.ToString(CultureInfo.InvariantCulture), c.Desc))),
        "consultarFormasPago" => Short(call, "arrayFormasPago", PaymentForms),
        "consultarTiposCuenta" => Short(call, "arrayTiposCuenta", AccountTypes),
        "consultarTiposTarjeta" => call.Request.Whole("formaPago") is 1 or 2
            ? Short(call, "arrayTiposTarjeta", CardTypes)
            : Return(call, Errors([WsctCodes.Note(1200)])),
        "consultarNovedades" => Return(call),
        _ => null,
    };

    // ---- Answers ------------------------------------------------------------------------

    /// <summary>The response with its one …Return child, named as the schema names it (ConsultarNovedadesReturn, consultarComprobanteReturn).</summary>
    private ContractAnswer Return(ServiceCall call, params object?[] content) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(_returns.GetOrAdd(call.Name, _ => call.Sample().Elements().First().Name), content)));

    private ContractAnswer Short(ServiceCall call, string array, IEnumerable<(int Code, string Description)> rows) =>
        Return(call, new XElement(array, rows.Select(r => new XElement("codigoDescripcion", new XElement("codigo", r.Code), new XElement("descripcion", r.Description)))));

    private ContractAnswer Strings(ServiceCall call, string array, IEnumerable<(string Code, string Description)> rows) =>
        Return(call, new XElement(array, rows.Select(r => new XElement("codigoDescripcionString", new XElement("codigo", r.Code), new XElement("descripcion", r.Description)))));

    private static XElement Errors(IEnumerable<BookNote> notes, string name = "arrayErrores") =>
        new(name, notes.Select(n => new XElement("codigoDescripcion", new XElement("codigo", n.Code), new XElement("descripcion", n.Text))));

    /// <summary>
    /// What the schema validator reports for a number below its type's
    /// minimum: the facet and the element, with the leading space the real
    /// answer has. Null when the numbers are in range.
    /// </summary>
    private static XElement? FormatErrors(XElement scope)
    {
        var problems = new List<(string Code, string Text)>();
        foreach (var (field, type) in new[] { ("numeroPuntoVenta", "NumeroPuntoVentaSimpleType"), ("numeroComprobante", "NumeroComprobanteSimpleType") })
        {
            var value = scope.Field(field);
            if (value is null || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number >= 1) continue;
            problems.Add(("cvc-minInclusive-valid", $" El valor '{value}' no cumple con la restricción minInclusive '1' para el tipo '{type}'."));
            problems.Add(("cvc-type.3.1.3", $" El valor '{value}' del elemento '{field}' no es válido."));
        }
        return problems.Count == 0
            ? null
            : new XElement("arrayErroresFormato", problems.Select(p =>
                new XElement("codigoDescripcionString", new XElement("codigo", p.Code), new XElement("descripcion", p.Text))));
    }

    private bool Production => settings.Environment == ArcaEnvironment.Produccion;

    private async Task<bool> PointOfSaleUsableAsync(long cuit, int pointOfSale, CancellationToken ct) =>
        !Production || (await taxpayers.FindAsync(cuit, ct))?.PointsOfSale.Any(p =>
            p.Number == pointOfSale && p.Kind == PointOfSaleKind.WebServiceCae && !p.Blocked && p.DeactivatedOn is null) == true;

    // ---- Queries ------------------------------------------------------------------------

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        if (FormatErrors(call.Request) is { } format) return Return(call, format);
        var type = (int)(call.Request.Whole("codigoTipoComprobante") ?? 0);
        var point = (int)(call.Request.Whole("numeroPuntoVenta") ?? 0);
        if (VoucherTypes.All(t => t.Code != type)) return Return(call, Errors([WsctCodes.Note(1000)]));
        if (!await PointOfSaleUsableAsync(call.Cuit, point, ct)) return Return(call, Errors([WsctCodes.Note(1001)]));
        if (await _book.LastAsync(call.Cuit, point, type, ct) is not { } last) return Return(call, Errors([WsctCodes.Note(1002)]));
        return Return(call, new XElement("numeroComprobante", last.Number), new XElement("fechaEmision", Figures.IsoDay(last.Date)));
    }

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        if (FormatErrors(call.Request) is { } format) return Return(call, format);
        var type = (int)(call.Request.Whole("codigoTipoComprobante") ?? 0);
        var point = (int)(call.Request.Whole("numeroPuntoVenta") ?? 0);
        if (VoucherTypes.All(t => t.Code != type)) return Return(call, Errors([WsctCodes.Note(2000)]));
        if (!await PointOfSaleUsableAsync(call.Cuit, point, ct)) return Return(call, Errors([WsctCodes.Note(2001)]));
        var found = await _book.FindAsync(call.Cuit, point, type, call.Request.Whole("numeroComprobante") ?? 0, ct);
        if (found is null) return Return(call, Errors([WsctCodes.Note(2002)]));
        return Return(call, Comprobante(found), found.Notes.Count == 0 ? null : Errors(found.Notes, "arrayObservaciones"));
    }

    /// <summary>The voucher as it was sent, in ComprobanteType's order, with the date it got and its CAE.</summary>
    private static XElement Comprobante(BookedVoucher voucher)
    {
        var detail = XElement.Parse(voucher.Detail);
        var fields = new Dictionary<string, string?>
        {
            ["fechaEmision"] = Figures.IsoDay(voucher.Date),
            ["codigoTipoAutorizacion"] = "E",
            ["codigoAutorizacion"] = voucher.Cae,
            ["fechaVencimiento"] = Figures.IsoDay(voucher.CaeDue),
        };
        return new XElement("comprobante",
            Header.Select(name => fields.TryGetValue(name, out var value) ? new XElement(name, value)
                : detail.Child(name) is { } field ? new XElement(name, field.Value) : null),
            Arrays.Select(a => detail.Child(a.Array) is { } array
                ? new XElement(a.Array, array.Children(a.Row).Select(row =>
                    new XElement(a.Row, a.Fields.Select(f => row.Child(f) is { } value ? new XElement(f, value.Value) : null))))
                : null));
    }

    private async Task<ContractAnswer> PointsOfSaleAsync(ServiceCall call, CancellationToken ct)
    {
        if (!Production) return Return(call);
        var points = (await taxpayers.FindAsync(call.Cuit, ct))?.PointsOfSale
            .Where(p => p.Kind == PointOfSaleKind.WebServiceCae && p.Number is >= 1 and <= 9999).ToList() ?? [];
        if (points.Count == 0) return Return(call, Errors([WsctCodes.Note(1106)]));
        return Return(call, new XElement("arrayPuntosVenta", points.Select(p => new XElement("puntoVenta",
            new XElement("numeroPuntoVenta", p.Number),
            new XElement("bloqueado", p.Blocked ? "S" : "N"),
            p.DeactivatedOn is { } off ? new XElement("fechaBaja", Figures.IsoDay(off)) : null))));
    }

    private async Task<ContractAnswer> QuoteAsync(ServiceCall call, CancellationToken ct)
    {
        var currency = call.Request.Field("codigoMoneda");
        if (parameters.Currencies.All(c => c.Id != currency)) return Return(call, Errors([WsctCodes.Note(210)]));
        var day = Figures.ParseIsoDay(call.Request.Field("fechaCotizacion")) ?? clock.Today();
        var quote = currency == "PES" ? (1m, day) : await rates.RateAsync(currency!, day, ct);
        return quote is { } found ? Return(call, new XElement("cotizacionMoneda", Figures.Number(found.Rate))) : Return(call);
    }

    // ---- autorizarComprobante ---------------------------------------------------------------

    private sealed record Line(int Type, int? Tourism, string? Description, int Vat, decimal VatAmount, decimal Amount);

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("comprobanteRequest") ?? new XElement("comprobanteRequest");
        if (FormatErrors(request) is { } format) return Return(call, format, new XElement("resultado", "R"));

        var type = (int)(request.Whole("codigoTipoComprobante") ?? 0);
        var point = (int)(request.Whole("numeroPuntoVenta") ?? 0);
        var number = request.Whole("numeroComprobante") ?? 0;
        var today = clock.Today();
        var date = Figures.ParseIsoDay(request.Field("fechaEmision")) ?? today;
        var errors = new List<BookNote>();
        var observations = new List<BookNote>();
        void Fail(int code) => errors.Add(WsctCodes.Note(code));

        if (Production)
        {
            var issuer = await taxpayers.FindAsync(call.Cuit, ct);
            if (issuer is not { Active: true }) Fail(100);
            else if (issuer.VatCondition != VatCondition.ResponsableInscripto) Fail(103);
        }
        var authorization = request.Field("codigoTipoAutorizacion");
        if (string.IsNullOrEmpty(authorization)) Fail(200);
        else if (authorization != "E") Fail(201);
        if (request.Child("codigoAutorizacion") is not null) Fail(202);
        if (request.Child("fechaVencimiento") is not null) Fail(203);
        if (VoucherTypes.All(t => t.Code != type)) Fail(300);
        if (!await PointOfSaleUsableAsync(call.Cuit, point, ct)) Fail(301);
        if (request.Child("fechaEmision") is not null && (Figures.ParseIsoDay(request.Field("fechaEmision")) is not { } sent
                                                           || Math.Abs(sent.DayNumber - today.DayNumber) > 10)) Fail(303);

        var rate = await CheckCurrencyAsync(request, type, date, today, Fail, ct);
        await CheckReceiverAsync(request, call.Cuit, Fail, ct);
        var lines = Lines(request);
        CheckAmounts(request, type, lines, Fail);
        CheckPayments(request, Fail);
        await CheckAssociatedAsync(request, call.Cuit, type, date, observations, Fail, ct);

        using (await locks.AcquireAsync(Service, call.Cuit, point, type, ct))
        {
            var last = await _book.LastAsync(call.Cuit, point, type, ct);
            if (number != (last?.Number ?? 0) + 1) Fail(302);
            if (errors.Count > 0)
            {
                var unique = errors.DistinctBy(e => e.Code).ToList();
                events.Publish(new VoucherRejected(DateTimeOffset.UtcNow, call.Cuit, point, type, number, unique.Select(e => e.Code).ToList()));
                return Return(call, Errors(unique), new XElement("resultado", "R"));
            }

            var detail = Figures.Strip(request);
            detail.SetElementValue("fechaEmision", Figures.IsoDay(date));
            if (detail.Child("cotizacionMoneda") is null) detail.Add(new XElement("cotizacionMoneda", Figures.Number(rate)));
            var cae = codes.NextCae();
            var due = date.AddDays(settings.CaeLifetimeDays);
            var total = request.Amount("importeTotal") ?? 0;
            var voucher = new BookedVoucher(Service, call.Cuit, point, type, number, 0, date, request.Field("fechaEmision"), cae, due,
                observations.Count > 0 ? "O" : "A", observations, detail.ToString(SaveOptions.DisableFormatting), clock.Now);
            long.TryParse(request.Field("numeroDocumento"), NumberStyles.None, CultureInfo.InvariantCulture, out var receiver);
            await _book.AddAsync(voucher, new AuthorizedVoucher(Service, call.Cuit, point, type, number, date, total,
                (int)(request.Whole("codigoTipoDocumento") ?? 0), receiver, "CAE", cae, due), ct);
            events.Publish(new VoucherAuthorized(DateTimeOffset.UtcNow, call.Cuit, point, type, number, number, "CAE", cae));

            return Return(call,
                new XElement("comprobanteResponse",
                    new XElement("cuit", call.Cuit),
                    new XElement("codigoTipoComprobante", type),
                    new XElement("numeroPuntoVenta", point),
                    new XElement("numeroComprobante", number),
                    new XElement("fechaEmision", Figures.IsoDay(date)),
                    new XElement("CAE", cae),
                    new XElement("fechaVencimientoCAE", Figures.IsoDay(due))),
                observations.Count > 0 ? Errors(observations, "arrayObservaciones") : null,
                new XElement("resultado", voucher.Result));
        }
    }

    /// <summary>304-306, 317-320 and 322; the rate the voucher keeps (ARCA's when it may be left out).</summary>
    private async Task<decimal> CheckCurrencyAsync(XElement request, int type, DateOnly date, DateOnly today, Action<int> fail, CancellationToken ct)
    {
        var currency = request.Field("codigoMoneda");
        var rate = request.Amount("cotizacionMoneda");
        var same = request.Field("cancelaEnMismaMonedaExtranjera") == "S";
        if (parameters.Currencies.All(c => c.Id != currency))
        {
            fail(304);
            return rate ?? 1;
        }
        if (same && type != 195) fail(318);
        if (currency == "PES")
        {
            if (same) fail(319);
            if (rate is { } pesos && pesos != 1) fail(305);
            if (rate is null) fail(322);
            return 1;
        }
        if (rate <= 0) fail(317);

        var reference = date >= today ? today : date;
        var official = await rates.RateAsync(currency!, reference.AddDays(-1), ct);
        if (rate is null)
        {
            if (!same || official is null) fail(322);
            return official?.Rate ?? 0;
        }
        if (same && official is { } bna && rate != bna.Rate) fail(320);
        if (await rates.RateAsync(currency!, today, ct) is { } latest && (rate < latest.Rate * 0.02m || rate > latest.Rate * 5)) fail(306);
        return rate.Value;
    }

    /// <summary>307-316 and 350-356: who the receiver is and how it relates to the issuer.</summary>
    private async Task CheckReceiverAsync(XElement request, long issuer, Action<int> fail, CancellationToken ct)
    {
        var docType = (int)(request.Whole("codigoTipoDocumento") ?? 0);
        var document = request.Field("numeroDocumento") ?? "";
        var condition = request.Field("idImpositivo");
        var country = request.Whole("codigoPais");
        var relation = (int)(request.Whole("codigoRelacionEmisorReceptor") ?? 0);
        var address = request.Field("domicilioReceptor");
        var foreigner = new[] { 91, 94, 96 }.Contains(docType);

        if (country is { } code && parameters.Countries.All(c => c.Id != code)) fail(307);
        if (Conditions.All(c => c.Code != condition)) fail(308);
        else if (condition == Registered)
        {
            if (docType != 80) fail(309);
            else if (!long.TryParse(document, NumberStyles.None, CultureInfo.InvariantCulture, out var cuit)
                     || await taxpayers.FindAsync(cuit, ct) is not { Active: true } receiver) fail(310);
            else if (receiver.VatCondition != VatCondition.ResponsableInscripto) fail(316);
        }
        else
        {
            var finalConsumer = condition == "5";
            if (!DocumentTypes.Contains(docType)) fail(finalConsumer ? 311 : 313);
            if (document.Length == 0 || !document.All(char.IsLetterOrDigit)) fail(finalConsumer ? 312 : 315);
        }

        if (string.IsNullOrWhiteSpace(address) || address.Length > 300) fail(350);
        if (Relations.All(r => r.Code != relation))
        {
            fail(351);
            return;
        }
        if (relation is 1 or 5 ? !DocumentTypes.Contains(docType) : docType != 80) fail(353);
        if (document == issuer.ToString(CultureInfo.InvariantCulture)) fail(354);
        var argentine = country is 200 or 295 or 296 or (>= 250 and <= 265);
        if (relation is 2 or 6 ? country != 200 : country is null || argentine) fail(355);
        var expected = condition == Registered ? relation is 2 or 6
            : docType == 80 ? relation is 3 or 4
            : !foreigner || relation is 1 or 5;
        if (Conditions.Any(c => c.Code == condition) && !expected) fail(356);
    }

    private static List<Line> Lines(XElement request) =>
        request.Child("arrayItems")?.Children("item").Select(i => new Line(
            (int)(i.Whole("tipo") ?? -1),
            (int?)i.Whole("codigoTurismo"),
            i.Field("descripcion"),
            (int)(i.Whole("codigoAlicuotaIVA") ?? 0),
            i.Amount("importeIVA") ?? 0,
            i.Amount("importeItem") ?? 0)).ToList() ?? [];

    /// <summary>The items (400-415), the VAT subtotals (500-504), the other taxes (600-604) and the voucher's amounts (360-369).</summary>
    private void CheckAmounts(XElement request, int type, List<Line> lines, Action<int> fail)
    {
        foreach (var line in lines)
        {
            if (ItemTypes.All(t => t.Code != line.Type)) fail(400);
            if (line.Tourism is not { } tourism || TourismCodes.All(t => t.Code != tourism)) fail(401);
            if (line.Vat != Vat21) fail(403);
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 4000) fail(404);
            if (line.Type == 0 && line.Amount < 0) fail(406);
            if (line.Type == 99 && line.Amount >= 0) fail(408);
            if (line.Type == 0 && line.VatAmount < 0) fail(409);
            if (line.Type == 99 && line.VatAmount >= 0) fail(411);
            if (line.Vat == Vat21 && !Figures.Close(Math.Round(line.Amount * 21 / 121, 2, MidpointRounding.ToEven), line.VatAmount, 1)) fail(413);
            if (line.VatAmount > 0 && line.Amount < 0 || line.VatAmount < 0 && line.Amount > 0) fail(414);
        }
        if (type == 195 && lines.Count > 0 && lines.All(l => l.Tourism == 5)) fail(415);

        var subtotals = request.Child("arraySubtotalesIVA")?.Children("subtotalIVA")
            .Select(s => (Code: (int)(s.Whole("codigo") ?? 0), Amount: s.Amount("importe") ?? 0)).ToList();
        if (subtotals is null) fail(500);
        else
        {
            if (subtotals.Any(s => s.Code != Vat21)) fail(501);
            if (subtotals.GroupBy(s => s.Code).Any(g => g.Count() > 1)) fail(502);
            if (subtotals.Any(s => s.Amount < 0)) fail(503);
            foreach (var rate in lines.Select(l => l.Vat).Concat(subtotals.Select(s => s.Code)).Distinct())
            {
                var items = lines.Where(l => l.Vat == rate).ToList();
                if (!Figures.Close(items.Sum(l => l.VatAmount), subtotals.Where(s => s.Code == rate).Sum(s => s.Amount), items.Count)) fail(504);
            }
        }

        var taxes = request.Child("arrayOtrosTributos")?.Children("otroTributo").ToList() ?? [];
        foreach (var tax in taxes)
        {
            var code = (int)(tax.Whole("codigo") ?? 0);
            if (parameters.Taxes.All(t => t.Id != code.ToString(CultureInfo.InvariantCulture))) fail(600);
            var description = tax.Field("descripcion");
            if (code == 99 && string.IsNullOrWhiteSpace(description) || description?.Length > 50) fail(602);
            if (tax.Amount("baseImponible") < 0) fail(603);
            if ((tax.Amount("importe") ?? -1) < 0) fail(604);
        }

        var taxed = request.Amount("importeGravado");
        var untaxed = request.Amount("importeNoGravado") ?? 0;
        var exempt = request.Amount("importeExento") ?? 0;
        var others = request.Amount("importeOtrosTributos");
        var refund = request.Amount("importeReintegro");
        var total = request.Amount("importeTotal") ?? 0;
        if (taxed is null or < 0) fail(360);
        else if (!Figures.Close(lines.Sum(l => l.Amount - l.VatAmount), taxed.Value, lines.Count)) fail(361);
        if (untaxed != 0) fail(362);
        if (exempt != 0) fail(363);

        var hotel = lines.Where(l => l.Tourism is 1 or 2).ToList();
        if (hotel.Count > 0)
        {
            if (refund is null or > 0) fail(364);
            else if (!Figures.Close(hotel.Sum(l => l.VatAmount), -refund.Value, hotel.Count)) fail(366);
        }
        if (type is 196 or 197 && lines.Count > 0 && lines.All(l => l.Tourism == 5) && refund is { } excess && excess != 0) fail(365);

        if (others < 0) fail(367);
        if ((taxes.Count > 0 || others is not null) && !Figures.Close(taxes.Sum(t => t.Amount("importe") ?? 0), others ?? 0, taxes.Count)) fail(368);

        var sum = (taxed ?? 0) + untaxed + exempt + (refund ?? 0) + (others ?? 0) + (subtotals?.Sum(s => s.Amount) ?? 0);
        if (!Figures.Close(sum, total, lines.Count)) fail(369);
    }

    /// <summary>700-732: each payment form with the fields its kind asks for.</summary>
    private static void CheckPayments(XElement request, Action<int> fail)
    {
        var forms = request.Child("arrayFormasPago")?.Children("formaPago").ToList() ?? [];
        var relation = request.Whole("codigoRelacionEmisorReceptor");
        foreach (var form in forms)
        {
            var code = form.Whole("codigo");
            bool Has(string field) => form.Child(field) is not null;
            switch (code)
            {
                case 1 or 2:
                    if (Has("swiftCode")) fail(701);
                    if (Has("tipoCuenta")) fail(702);
                    if (Has("numeroCuenta")) fail(703);
                    if (CardTypes.All(c => c.Code != form.Whole("tipoTarjeta"))) fail(704);
                    if (!Has("numeroTarjeta")) fail(705);
                    break;
                case 3:
                    if (Has("tipoTarjeta")) fail(720);
                    if (Has("numeroTarjeta")) fail(721);
                    if (AccountTypes.All(a => a.Code != form.Whole("tipoCuenta"))) fail(722);
                    if (!Has("numeroCuenta")) fail(723);
                    if (form.Field("swiftCode") is { Length: >= 6 } swift && swift[4..6].Equals("AR", StringComparison.OrdinalIgnoreCase) != relation is 2 or 6)
                        fail(724);
                    break;
                case 4:
                    if (new[] { "tipoTarjeta", "numeroTarjeta", "swiftCode", "tipoCuenta", "numeroCuenta" }.Any(Has)) fail(730);
                    if (relation is not (2 or 6)) fail(732);
                    break;
                default:
                    fail(700);
                    break;
            }
        }
        if (forms.Count(f => f.Whole("codigo") == 4) > 1) fail(731);
    }

    /// <summary>800-807: which vouchers carry associated ones, and that those exist for the same issuer and receiver.</summary>
    private async Task CheckAssociatedAsync(XElement request, long cuit, int type, DateOnly date, List<BookNote> observations, Action<int> fail, CancellationToken ct)
    {
        var associated = request.Child("arrayComprobantesAsociados")?.Children("comprobanteAsociado").ToList() ?? [];
        if (type == 195 && associated.Count > 0 || type is 196 or 197 && associated.Count == 0) fail(800);
        var seen = new HashSet<(long, long, long)>();
        decimal adjusted = 0;
        foreach (var asoc in associated)
        {
            var asocType = asoc.Whole("codigoTipoComprobante") ?? 0;
            var asocPoint = asoc.Whole("numeroPuntoVenta") ?? 0;
            var asocNumber = asoc.Whole("numeroComprobante") ?? 0;
            if (VoucherTypes.All(t => t.Code != asocType)) fail(801);
            if (!seen.Add((asocType, asocPoint, asocNumber))) fail(804);
            if (await _book.FindAsync(cuit, (int)asocPoint, (int)asocType, asocNumber, ct) is not { } found)
            {
                fail(803);
                continue;
            }
            if (found.Date > date) fail(805);
            var original = XElement.Parse(found.Detail);
            if (original.Field("codigoTipoDocumento") != request.Field("codigoTipoDocumento")
                || original.Field("numeroDocumento") != request.Field("numeroDocumento")) fail(806);
            adjusted += original.Amount("importeTotal") ?? 0;
        }
        if (type == 197 && associated.Count > 0 && (request.Amount("importeTotal") ?? 0) > adjusted) observations.Add(WsctCodes.Note(807));
    }
}
