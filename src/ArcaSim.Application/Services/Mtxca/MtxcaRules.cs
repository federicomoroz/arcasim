using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Services.Fce;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Mtxca;

/// <summary>
/// WSMTXCA, factura electrónica con detalle de ítems (RG 2904), following
/// docs/arca/servicios/wsmtxca.md: autorizarComprobante gives a CAE to one
/// voucher with its items after checking numbering ("último + 1" per CUIT,
/// point of sale and type), the issuer, the receiver, the currency, every item
/// and the VAT subtotals and totals; consultarUltimoComprobanteAutorizado and
/// consultarComprobante answer from what was authorized; the CAEA regime asks
/// for a code per fortnight, takes the vouchers issued with it and the points
/// of sale that did not use it; and the parameter queries answer the
/// documented tables. Every voucher is recorded for constatación (wscdc).
/// autorizarAjusteIVA and informarAjusteIVACAEA keep the contract's answer.
///
/// ArcaSim's choices on what the spec leaves NO VERIFICADO:
/// - a CAE expires SimulationSettings.CaeLifetimeDays after the voucher's date,
///   as in WSFEv1 (the manual's two examples show 15 and 10 days);
/// - a CAEA's fechaTopeInforme is the end of its fortnight plus one month, as
///   in the manual's example;
/// - "mayor a la fecha de inicio" (706, 1203) is read literally: a CAEA's
///   vouchers and unused points are reported from the day after it starts;
/// - consultarCAEAEntreFechas lists the CAEAs whose fortnight overlaps the range;
/// - informarComprobanteCAEA answers 705 for a CAEA that does not exist as for
///   one of another CUIT; a point of sale of a CAEA declared unused as a whole
///   answers 1207 to informarCAEANoUtilizadoPtoVta;
/// - consultarPtosVtaCAEANoInformados lists the issuer's CAEA points of sale not
///   deactivated before the fortnight starts, and none once the CAEA was
///   declared unused;
/// - solicitarCAEA checks 10024 and 10027 only with strict access, as WSFEv1
///   does, and never observes: 10025, 10026 and 10028 need data ArcaSim lacks;
/// - points of sale answer bloqueado with the WSDL's S and N, not the
///   examples' Si and No;
/// - the issuer's checks (10000, 10003) apply to autorizarComprobante and
///   solicitarCAEA: an issuer ArcaSim does not know, or that is not
///   Responsable Inscripto, is rejected;
/// - a request that arrives while another one for the same point of sale and
///   type is being processed is rejected with 135 (739 for CAEA), as the manual
///   says, instead of waiting its turn as WSFEv1's SequenceLocks do.
/// </summary>
public sealed partial class MtxcaRules(
    ParameterTables parameters,
    IDocumentStore documents,
    ITaxpayerRepository taxpayers,
    IExchangeRates rates,
    IAuthorizationCodes codes,
    IClock clock,
    SimulationSettings settings,
    EventManager events,
    TimeProvider time) : IServiceBehavior
{
    private readonly MtxcaTables _tables = new(parameters);
    private readonly MtxcaStore _state = new(documents);
    private MtxcaValidator? _validator;
    private readonly ConcurrentDictionary<(long, int, int), byte> _busy = new();
    private readonly SemaphoreSlim _caeaGate = new(1, 1);

    public string Service => "wsmtxca";

    public Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "autorizarComprobante" => Some(AuthorizeAsync(call, ct)),
        "consultarUltimoComprobanteAutorizado" => Some(LastAsync(call, ct)),
        "consultarComprobante" => Some(ConsultAsync(call, ct)),
        "solicitarCAEA" => Some(RequestCaeaAsync(call, ct)),
        "consultarCAEA" => Some(ConsultCaeaAsync(call, ct)),
        "consultarCAEAEntreFechas" => Some(CaeasBetweenAsync(call, ct)),
        "informarComprobanteCAEA" => Some(InformAsync(call, ct)),
        "informarCAEANoUtilizado" => Some(UnusedAsync(call, null, ct)),
        "informarCAEANoUtilizadoPtoVta" => Some(UnusedAsync(call, call.Request.Int("numeroPuntoVenta"), ct)),
        "consultarPtosVtaCAEANoInformados" => Some(NotInformedAsync(call, ct)),
        "consultarTiposComprobante" => Done(Table(call, "arrayTiposComprobante", MtxcaTables.VoucherTypes.Select(t => new MtxcaRow(t.Id, t.Description)))),
        "consultarTiposDocumento" => Done(Table(call, "arrayTiposDocumento", _tables.DocumentTypes)),
        "consultarAlicuotasIVA" => Done(Table(call, "arrayAlicuotasIVA", _tables.VatRates)),
        "consultarCondicionesIVA" => Done(Table(call, "arrayCondicionesIVA", _tables.ItemVatConditions)),
        "consultarCondicionesIVAReceptor" => Done(ReceiverConditions(call)),
        "consultarMonedas" => Done(call.Ok(new XElement(call.Operation.Output, FceXml.CodeList("arrayMonedas", _tables.Currencies)))),
        "consultarCotizacionMoneda" => Some(QuoteAsync(call, ct)),
        "consultarUnidadesMedida" => Done(Table(call, "arrayUnidadesMedida", MtxcaTables.Units)),
        "consultarTiposTributo" => Done(Table(call, "arrayTiposTributo", _tables.Taxes)),
        "consultarTiposDatosAdicionales" => Done(Table(call, "arrayTiposDatosAdicionales", MtxcaTables.ExtraDataTypes)),
        "consultarPuntosVenta" => Some(PointsOfSaleAsync(call, PointOfSaleKind.WebServiceCae, PointOfSaleKind.WebServiceCaea, ct)),
        "consultarPuntosVentaCAE" => Some(PointsOfSaleAsync(call, PointOfSaleKind.WebServiceCae, null, ct)),
        "consultarPuntosVentaCAEA" => Some(PointsOfSaleAsync(call, PointOfSaleKind.WebServiceCaea, null, ct)),
        "consultarActividadesVigentes" => Some(ActivitiesAsync(call, ct)),
        _ => Task.FromResult<ContractAnswer?>(null),
    };

    /// <summary>The checks of autorizarComprobante and informarComprobanteCAEA, over the same tables as the queries.</summary>
    private MtxcaValidator Validator => _validator ??= new MtxcaValidator(_tables, taxpayers, rates, settings);

    private static async Task<ContractAnswer?> Some(Task<ContractAnswer> answer) => await answer;

    private static Task<ContractAnswer?> Done(ContractAnswer answer) => Task.FromResult<ContractAnswer?>(answer);

    // ---- CAE ----------------------------------------------------------------------------

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CancellationToken ct)
    {
        var element = call.Request.Child("comprobanteCAERequest") ?? new XElement("comprobanteCAERequest");
        var voucher = MtxcaVoucherInput.Read(element);
        var today = clock.Today();
        var date = voucher.Date ?? today;
        var type = MtxcaTables.VoucherType(voucher.Type);
        var issuer = await IssuerAsync(call.Cuit, voucher.PointOfSale, PointOfSaleKind.WebServiceCae, ct, ClassOf(voucher.Type));

        var findings = new List<MtxcaFinding>();
        if (issuer is not { Active: true }) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 10000));
        else if (issuer.VatCondition != VatCondition.ResponsableInscripto) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 10003));
        if (type is null) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 100));
        if (!Usable(issuer, voucher.PointOfSale, PointOfSaleKind.WebServiceCae, today)) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 101));
        if (type is not null)
            findings.AddRange(await Validator.ValidateAsync(false, voucher, type, call.Cuit, date, today, Activities(issuer), ct));

        var sequence = (call.Cuit, voucher.PointOfSale, voucher.Type);
        if (!_busy.TryAdd(sequence, 0)) return Rejected(call, [MtxcaCodes.Error(MtxcaTable.Cae, 135)]);
        try
        {
            var last = await _state.LastAsync(call.Cuit, voucher.PointOfSale, voucher.Type, ct);
            if (voucher.Number != (last?.Number ?? 0) + 1) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 102));
            if (date < last?.Date) findings.Add(MtxcaCodes.Error(MtxcaTable.Cae, 104));
            if (findings.Any(f => f.Rejects))
            {
                events.Publish(new VoucherRejected(time.GetUtcNow(), call.Cuit, voucher.PointOfSale, voucher.Type, voucher.Number,
                    findings.Where(f => f.Rejects).Select(f => f.Code).ToList()));
                return Rejected(call, findings.Where(f => f.Rejects));
            }

            var cae = long.Parse(codes.NextCae(), CultureInfo.InvariantCulture);
            var due = date.AddDays(settings.CaeLifetimeDays);
            await _state.AddAsync(Stored(call.Cuit, voucher, date, "E", cae, due, findings), voucher.DocType ?? 0, voucher.DocNumber ?? 0, voucher.Total, ct);
            events.Publish(new VoucherAuthorized(time.GetUtcNow(), call.Cuit, voucher.PointOfSale, voucher.Type, voucher.Number,
                voucher.Number, "CAE", cae.ToString(CultureInfo.InvariantCulture)));

            return call.Ok(new XElement(call.Operation.Output,
                new XElement("resultado", findings.Count > 0 ? "O" : "A"),
                new XElement("comprobanteResponse",
                    new XElement("cuit", call.Cuit),
                    new XElement("codigoTipoComprobante", voucher.Type),
                    new XElement("numeroPuntoVenta", voucher.PointOfSale),
                    new XElement("numeroComprobante", voucher.Number),
                    new XElement("fechaEmision", Day(date)),
                    new XElement("CAE", cae),
                    new XElement("fechaVencimientoCAE", Day(due))),
                findings.Count > 0 ? Codes("arrayObservaciones", findings) : null));
        }
        finally
        {
            _busy.TryRemove(sequence, out _);
        }
    }

    private static ContractAnswer Rejected(ServiceCall call, IEnumerable<MtxcaFinding> errors) =>
        call.Ok(new XElement(call.Operation.Output, new XElement("resultado", "R"), Codes("arrayErrores", errors)));

    /// <summary>The voucher as consultarComprobante gives it back: as sent, with its date and authorization in place.</summary>
    private static MtxcaVoucher Stored(long cuit, MtxcaVoucherInput voucher, DateOnly date, string authorizationType, long code, DateOnly due,
        IEnumerable<MtxcaFinding> observations)
    {
        var copy = MtxcaVoucherInput.Unqualified(voucher.Element);
        copy.Name = "comprobante";
        foreach (var name in new[] { "fechaEmision", "codigoTipoAutorizacion", "codigoAutorizacion", "fechaVencimiento" })
            copy.Child(name)?.Remove();
        var anchor = copy.Child("numeroComprobante");
        XElement[] authorization =
        [
            new("fechaEmision", Day(date)),
            new("codigoTipoAutorizacion", authorizationType),
            new("codigoAutorizacion", code),
            new("fechaVencimiento", Day(due)),
        ];
        if (anchor is not null) anchor.AddAfterSelf(authorization);
        else copy.AddFirst(authorization);

        return new MtxcaVoucher(cuit, voucher.PointOfSale, voucher.Type, voucher.Number, date, authorizationType, code, due,
            copy.ToString(SaveOptions.DisableFormatting),
            observations.Where(o => !o.Rejects).Select(o => new MtxcaNote(o.Code, o.Text)).ToList());
    }

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var query = call.Request.Child("consultaUltimoComprobanteAutorizadoRequest") ?? call.Request;
        var type = query.Int("codigoTipoComprobante");
        var pointOfSale = query.Int("numeroPuntoVenta");
        if (await QueryProblemAsync(call, type, pointOfSale, ct) is { } problem) return QueryError(call, problem);

        var last = await _state.LastAsync(call.Cuit, pointOfSale, type, ct);
        return last is null
            ? QueryError(call, 1502)
            : call.Ok(new XElement(call.Operation.Output, new XElement("numeroComprobante", last.Number)));
    }

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        var query = call.Request.Child("consultaComprobanteRequest") ?? call.Request;
        var type = query.Int("codigoTipoComprobante");
        var pointOfSale = query.Int("numeroPuntoVenta");
        if (await QueryProblemAsync(call, type, pointOfSale, ct) is { } problem) return QueryError(call, problem);

        var stored = await _state.FindAsync(call.Cuit, pointOfSale, type, query.Long("numeroComprobante"), ct);
        if (stored is null) return QueryError(call, 1503);
        return call.Ok(new XElement(call.Operation.Output,
            XElement.Parse(stored.Voucher),
            stored.Observations.Count > 0
                ? Codes("arrayObservaciones", stored.Observations.Select(o => new MtxcaFinding(o.Code, o.Text, false)))
                : null));
    }

    /// <summary>1500 for a type the service does not authorize; 1501 for a point of sale that is not the issuer's CAE or CAEA one.</summary>
    private async Task<int?> QueryProblemAsync(ServiceCall call, int type, int pointOfSale, CancellationToken ct)
    {
        if (MtxcaTables.VoucherType(type) is null) return 1500;
        if (settings.OpenAccess) return null;
        var kind = (await taxpayers.FindAsync(call.Cuit, ct))?.FindPointOfSale(pointOfSale)?.Kind;
        return kind is PointOfSaleKind.WebServiceCae or PointOfSaleKind.WebServiceCaea ? null : 1501;
    }

    private static ContractAnswer QueryError(ServiceCall call, int code) =>
        call.Ok(new XElement(call.Operation.Output, Codes("arrayErrores", [MtxcaCodes.Error(MtxcaTable.Query, code)])));

    // ---- Parameters ------------------------------------------------------------------

    private static ContractAnswer Table(ServiceCall call, string array, IEnumerable<MtxcaRow> rows) =>
        call.Ok(new XElement(call.Operation.Output, FceXml.CodeList(array, rows.Select(r => (r.Code, r.Description)))));

    private ContractAnswer ReceiverConditions(ServiceCall call)
    {
        var type = MtxcaTables.VoucherType(call.Request.Int("codigoTipoComprobante"));
        if (type is null) return QueryError(call, 196);
        return call.Ok(new XElement(call.Operation.Output,
            FceXml.CodeList("arrayCondicionesIVAReceptor", _tables.ReceiverConditions(type).Select(r => (r.Code, r.Description)))));
    }

    /// <summary>The rate ArcaSim was given for that day or the last one before it; PES is always 1; none, no value.</summary>
    private async Task<ContractAnswer> QuoteAsync(ServiceCall call, CancellationToken ct)
    {
        var currency = call.Request.Text("codigoMoneda") ?? "";
        if (!_tables.HasCurrency(currency)) return QueryError(call, 1600);
        var day = call.Request.Date("fechaCotizacion") ?? clock.Today();
        var rate = (await rates.QuoteAsync(currency, day, ct))?.Rate;
        return call.Ok(new XElement(call.Operation.Output, rate is { } value ? new XElement("cotizacionMoneda", value) : null));
    }

    private async Task<ContractAnswer> PointsOfSaleAsync(ServiceCall call, PointOfSaleKind kind, PointOfSaleKind? also, CancellationToken ct)
    {
        var issuer = await taxpayers.FindAsync(call.Cuit, ct);
        var points = issuer?.PointsOfSale.Where(p => p.Kind == kind || p.Kind == also).OrderBy(p => p.Number) ?? Enumerable.Empty<PointOfSale>();
        return call.Ok(new XElement(call.Operation.Output, new XElement("arrayPuntosVenta", points.Select(PointOfSaleElement))));
    }

    private static XElement PointOfSaleElement(PointOfSale point) => new("puntoVenta",
        new XElement("numeroPuntoVenta", point.Number),
        new XElement("bloqueado", point.Blocked ? "S" : "N"),
        point.DeactivatedOn is { } off ? new XElement("fechaBaja", Day(off)) : null);

    private async Task<ContractAnswer> ActivitiesAsync(ServiceCall call, CancellationToken ct)
    {
        var issuer = await taxpayers.FindAsync(call.Cuit, ct)
                     ?? (settings.OpenAccess && Cuits.IsValid(call.Cuit) ? new Taxpayer(call.Cuit, "", VatCondition.ResponsableInscripto) : null);
        if (issuer is null) return call.Ok(new XElement(call.Operation.Output));
        var (id, description) = PadronDirectory.ActivityOf(issuer);
        return call.Ok(new XElement(call.Operation.Output, new XElement("arrayActividades",
            new XElement("actividad", new XElement("codigo", id), new XElement("orden", 1), new XElement("descripcion", description)))));
    }

    // ---- Shared ----------------------------------------------------------------------

    /// <summary>
    /// The issuer; with open access, created with the point of sale on first use,
    /// as WSFEv1 does: a Monotributo when its first voucher is class C, a
    /// Responsable Inscripto otherwise.
    /// </summary>
    private Task<Taxpayer?> IssuerAsync(long cuit, int pointOfSale, PointOfSaleKind kind, CancellationToken ct, VoucherClass? firstClass = null) =>
        taxpayers.FindOrOpenAsync(settings, cuit, pointOfSale, kind, firstClass, ct);

    /// <summary>
    /// The class of a voucher type MTXCA authorizes, which is A, B or A with the
    /// retention legend; none for any other type (class C is not MTXCA's: error
    /// 100), so a wrong first request does not open a Monotributo that 10003
    /// would then reject.
    /// </summary>
    private VoucherClass? ClassOf(int voucherType) =>
        MtxcaTables.VoucherType(voucherType) is null ? null : parameters.VoucherType(voucherType)?.Class;

    private static bool Usable(Taxpayer? issuer, int number, PointOfSaleKind kind, DateOnly today) =>
        issuer?.CanIssueFrom(number, kind, today) == true;

    /// <summary>The issuer's current activities, from the padrón; unknown with open access, where any activity goes.</summary>
    private IReadOnlyCollection<long>? Activities(Taxpayer? issuer) =>
        settings.OpenAccess || issuer is null ? null : [PadronDirectory.ActivityOf(issuer).Id];

    private static XElement Codes(string array, IEnumerable<MtxcaFinding> findings) =>
        FceXml.CodeList(array, findings.Select(f => (f.Code, f.Text)));

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
