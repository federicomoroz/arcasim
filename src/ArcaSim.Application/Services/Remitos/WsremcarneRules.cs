using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// Remito electrónico cárnico (docs/arca/servicios/wsremcarne.md): the four
/// generar variants (with importe COT, to a receiver without CUIT) create the
/// remito and leave it PAT, PAD or issued (type 995, number per point of
/// emission, CRE, qr); the holder and the depositary authorize; the issuer
/// issues, cancels, changes the trip within 24 hours and informs
/// contingencies, which cancel; the receiver chooses ACE, ACP or NAC; a
/// remito to a receiver without CUIT is accepted on its own once it expires.
/// Redestinos are generated with tipoMovimiento RED. The obligations by
/// movement (1205-1208, 1300, 1400, 1401, 1510) use the texts the spec
/// reconstructs from the manual's tables.
/// ArcaSim's choices where the spec says NO VERIFICADO: a repeated idReq
/// answers the remito it already created (manual 1.6: resend with the same
/// idReq); the state codes and the validity by distance are harina's; a
/// redestino is issued at once; codes harina documents and carne does not
/// (3022 not found, 3070 not allowed, 3034 no remitos, 2602 invalid receiver)
/// are taken from harina; 2201's text is ArcaSim's wording of the rule; an
/// orden sent twice in a reception counts once, the last informed, since the
/// manual documents no error for it.
/// </summary>
public sealed class WsremcarneRules(IDocumentStore store, SequenceLocks locks, IClock clock, PadronDirectory directory) : IServiceBehavior
{
    private const int VoucherType = 995;

    private static readonly string[] RemitoOrder =
    [
        "codRemito", "tipoComprobante", "tipoMovimiento", "categoriaEmisor", "puntoEmision", "cuitTitularMercaderia", "cuitDepositario",
        "tipoReceptor", "categoriaReceptor", "cuitReceptor", "codDomOrigen", "codDomDestino", "viaje", "arrayMercaderias", "estado",
        "datosEmision", "codRemRedestinado", "arrayContingencias",
    ];

    private static readonly string[] ImporteOrder = [.. RemitoOrder, "importeCot"];

    private static readonly string[] TripOrder = ["cuitTransportista", "cuitConductor", "fechaInicioViaje", "distanciaKm", "vehiculo"];

    private static readonly string[] GoodsOrder = ["orden", "codTipoProd", "tropa", "kilos", "unidades", "kilosRec", "unidadesRec"];

    private static readonly RemitoListNames ListNames = new("rangoFechas", "puntoEmision", "remitosConsulta", "tipoComprobante", "idReq");

    private static readonly RemitoProblem NotFound = new(3022, "Remito no encontrado");
    private static readonly RemitoProblem NotAllowed = new(3070, "Operación no permitida");
    private static readonly RemitoCodes Codes = new(NotFound, NotAllowed,
        new RemitoProblem(170, "El remito debe encontrarse pendiente de autorización"),
        new RemitoProblem(2201, "La CUIT no es un autorizador válido para el remito"));

    private readonly RemitoLedger _ledger = new("wsremcarne", store, locks, clock);

    public string Service => "wsremcarne";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "generarRemito" or "generarRemitoImporte" or "generarRemitoRecNoCateg" or "generarRemitoRecNoCategImporte" => await GenerateAsync(call, ct),
        "autorizarRemito" => await RemitoFamily.AuthorizeAsync(call, _ledger, Codes, "autorizarRemitoReturn", ct),
        "anularRemito" => await RemitoFamily.CancelAsync(call, _ledger, Codes, "anularRemitoReturn", ct),
        "emitirRemito" => await IssueAsync(call, ct),
        "registrarRecepcion" => await ReceiveAsync(call, ct),
        "modificarViaje" => await ChangeTripAsync(call, ct),
        "informarContingencia" => await ContingencyAsync(call, ct),
        "consultarRemito" => await ConsultAsync(call, "consultarRemitoReturn", RemitoOrder, ct),
        "consultarRemitoImporte" => await ConsultAsync(call, "consultarRemitoImporteReturn", ImporteOrder, ct),
        "consultarEstadosRemito" => await HistoryAsync(call, ct),
        "consultarUltimoRemitoEmitido" => await LastAsync(call, ct),
        "consultarRemitosEmisor" or "consultarRemitosAutorizador" or "consultarRemitosReceptor" => RemitoFamily.ListAnswer(call, await AllRefreshedAsync(ct), ListNames),
        "consultarReceptoresValidos" => await RemitoFamily.ValidReceiversAsync(call, directory, ct),
        "consultarTiposComprobante" => RemitoFamily.Table(call, "consultarTiposComprobanteReturn", "arrayTiposComprobante", "codigoDescripcion", RemitoTables.MeatVoucherTypes),
        "consultarTiposEstado" => RemitoFamily.Table(call, "consultarTiposEstadoReturn", "arrayTiposEstado", "codigoDescripcionString", RemitoTables.States),
        "consultarPuntosEmision" => await RemitoFamily.PointsAnswerAsync(call, directory, RemitoFamily.MaxShortCode, ct),
        "consultarCodigosDomicilio" => await RemitoFamily.AddressesAnswerAsync(call, directory, ct),
        _ => null,
    };

    // ---- Generation and lifecycle ------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var sent = request.Element("remito") ?? new XElement("remito");
        var requestId = request.ChildLong("idReq") ?? 0;
        var movement = sent.ChildText("tipoMovimiento") ?? "ENV";
        var uncategorized = call.Name.Contains("RecNoCateg", StringComparison.Ordinal);
        var trip = sent.Element("viaje");

        Remito? redirected = null;
        var problems = new List<RemitoProblem>();
        if (movement == "RED")
        {
            if (sent.ChildLong("codRemRedestinado") is not { } original)
                problems.Add(new RemitoProblem(1300, "En un movimiento de REDESTINO debe informar el código de remito que está redestinando"));
            else if ((redirected = await FindAsync(original, call.Cuit, ct)) is null)
                problems.Add(NotFound);
            else if (redirected.Issuer != call.Cuit || !(redirected.State is RemitoStates.PartlyAccepted or RemitoStates.NotAccepted
                                                         || (redirected.Movement == "REP" && redirected.State == RemitoStates.Issued)))
                problems.Add(NotAllowed);
        }
        else
        {
            if (sent.Element("puntoEmision") is null) problems.Add(new RemitoProblem(1205, "En un movimiento que no sea RED debe informar punto de emisión"));
            if (trip is null)
            {
                problems.Add(new RemitoProblem(1206, "En un movimiento que no sea RED debe informar fecha de inicio de viaje"));
                problems.Add(new RemitoProblem(1207, "En un movimiento que no sea RED debe informar km"));
            }
            if (sent.Element("categoriaEmisor") is null) problems.Add(new RemitoProblem(1208, "En un movimiento que no sea RED debe informar categoría del emisor"));
            if (trip is not null && trip.Element("cuitTransportista") is null) problems.Add(new RemitoProblem(1400, "Debe informar la CUIT del transportista"));
            if (trip is not null && trip.Element("vehiculo")?.Element("dominioVehiculo") is null) problems.Add(new RemitoProblem(1401, "Debe informar el dominio del vehículo"));
        }
        if (trip.ChildDate("fechaInicioViaje") < _ledger.Today)
            problems.Add(new RemitoProblem(140, "La fecha de inicio del viaje no puede ser anterior a la fecha de proceso"));

        var point = (int)(sent.ChildLong("puntoEmision") ?? redirected?.Point ?? 0);
        var holder = sent.ChildLong("cuitTitularMercaderia") ?? call.Cuit;
        var depositary = sent.ChildLong("cuitDepositario");
        var receiver = uncategorized ? null : sent.ChildLong("cuitReceptor");
        foreach (var party in new[] { call.Cuit, holder, depositary, receiver }.Distinct())
            if (await RemitoFamily.CheckPartyAsync(directory, party, ct) is { } problem && !problems.Contains(problem)) problems.Add(problem);
        if (problems.Count > 0) return RemitoAnswer(call, "generarRemitoReturn", null, problems);

        using var requests = await _ledger.LockRequestsAsync(call.Cuit, point, ct);
        if (await _ledger.FindByRequestAsync(call.Cuit, point, requestId, ct) is { } already)
            return RemitoAnswer(call, "generarRemitoReturn", already, []);

        var remito = new Remito
        {
            Code = await _ledger.NextCodeAsync(ct),
            Issuer = call.Cuit,
            RequestId = requestId,
            Point = point,
            Type = (int)(sent.ChildLong("tipoComprobante") ?? VoucherType),
            Movement = movement,
            Holder = holder,
            Depositary = depositary,
            Receiver = receiver,
            Uncategorized = uncategorized,
            DistanceKm = trip.ChildDecimal("distanciaKm") ?? 0,
            CreatedOn = _ledger.Today,
            Xml = sent.ToString(SaveOptions.DisableFormatting),
        };
        if (redirected is null) RemitoFamily.Open(remito, _ledger.Now);
        await _ledger.AddAsync(remito, ct);
        if (remito.Pending is null) await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "generarRemitoReturn", remito, []);
    }

    private async Task<ContractAnswer> IssueAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        using var hold = await _ledger.HoldAsync(request.ChildLong("codRemito") ?? 0, call.Cuit, ct);
        if (hold.Remito is not { } remito)
            return RemitoAnswer(call, "emitirRemitoReturn", null, [NotFound]);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.PendingIssue) return RemitoAnswer(call, "emitirRemitoReturn", null, [NotAllowed]);
        if (request.Element("viaje") is { } trip)
        {
            var problems = new List<RemitoProblem>();
            if (trip.Element("cuitTransportista") is null)
                problems.Add(new RemitoProblem(1400, "Si modifica algún dato del viaje debe informar la cuit del transportista"));
            if (trip.Element("vehiculo") is null)
                problems.Add(new RemitoProblem(1510, "Si modifica algún dato del viaje debe completar también los datos del vehículo"));
            if (problems.Count > 0) return RemitoAnswer(call, "emitirRemitoReturn", null, problems);
            RemitoXml.Edit(remito, document => RemitoXml.Put(document, new XElement(trip), RemitoOrder));
            remito.DistanceKm = trip.ChildDecimal("distanciaKm") ?? remito.DistanceKm;
        }
        await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "emitirRemitoReturn", remito, []);
    }

    /// <summary>The receiver says ACE, ACP or NAC; with a partial acceptance the items carry what arrived.</summary>
    private async Task<ContractAnswer> ReceiveAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        using var hold = await _ledger.HoldAsync(code, call.Cuit, ct);
        if (hold.Remito is not { } remito) return RemitoFamily.OperationAnswer(call, "registrarRecepcionReturn", code, NotFound);
        var state = request.ChildText("estado");
        if (remito.Receiver != call.Cuit || remito.State != RemitoStates.Issued
            || state is not (RemitoStates.Accepted or RemitoStates.PartlyAccepted or RemitoStates.NotAccepted))
            return RemitoFamily.OperationAnswer(call, "registrarRecepcionReturn", code, NotAllowed);

        var reported = request.Element("arrayRecepcionMercaderia") is { } list ? RemitoXml.ByOrder(list.Elements("recepcionMercaderia"), e => e) : null;
        RemitoXml.Edit(remito, document =>
        {
            foreach (var item in document.Element("arrayMercaderias")?.Elements("mercaderia") ?? [])
            {
                var line = reported?.GetValueOrDefault(item.ChildLong("orden") ?? 0);
                var (kilos, units) = state == RemitoStates.NotAccepted ? (0m, 0m)
                    : line is not null ? (line.ChildDecimal("kilos") ?? 0, line.ChildDecimal("unidades") ?? 0)
                    : reported is null && state == RemitoStates.Accepted ? (item.ChildDecimal("kilos") ?? 0, item.ChildDecimal("unidades") ?? 0)
                    : (0m, 0m);
                if (item.Element("kilos") is not null) RemitoXml.Put(item, "kilosRec", RemitoXml.Number(kilos), GoodsOrder);
                if (item.Element("unidades") is not null) RemitoXml.Put(item, "unidadesRec", RemitoXml.Number(units), GoodsOrder);
            }
        });
        remito.ReceivedOn = _ledger.Today;
        remito.MoveTo(state, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "registrarRecepcionReturn", code, []);
    }

    private async Task<ContractAnswer> ChangeTripAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        using var hold = await _ledger.HoldAsync(code, call.Cuit, ct);
        if (hold.Remito is not { } remito) return RemitoFamily.OperationAnswer(call, "modificarViajeReturn", code, NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.Issued || _ledger.Now > remito.IssuedAt!.Value.AddHours(RemitoTerms.FlatChangeHours))
            return RemitoFamily.OperationAnswer(call, "modificarViajeReturn", code, NotAllowed);
        RemitoXml.Edit(remito, document =>
        {
            var trip = document.Element("viaje");
            if (trip is null) return;
            RemitoXml.Put(trip, "cuitTransportista", request.ChildText("cuitTransportista") ?? "", TripOrder);
            if (request.ChildText("cuitConductor") is { } driver) RemitoXml.Put(trip, "cuitConductor", driver, TripOrder);
            if (request.Element("vehiculo") is { } vehicle) RemitoXml.Put(trip, new XElement(vehicle), TripOrder);
        });
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "modificarViajeReturn", code, []);
    }

    /// <summary>"Realiza la anulación del remito" (manual 2.5.9): ANU, with the contingency on record.</summary>
    private async Task<ContractAnswer> ContingencyAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        using var hold = await _ledger.HoldAsync(code, call.Cuit, ct);
        if (hold.Remito is not { } remito) return RemitoFamily.OperationAnswer(call, "informarContingenciaReturn", code, NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.Issued)
            return RemitoFamily.OperationAnswer(call, "informarContingenciaReturn", code, NotAllowed);
        remito.Contingencies.Add((request.Element("contingencia") ?? new XElement("contingencia")).ToString(SaveOptions.DisableFormatting));
        remito.MoveTo(RemitoStates.Cancelled, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "informarContingenciaReturn", code, []);
    }

    // ---- Consults ----------------------------------------------------------------------

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, string wrapper, string[] order, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var remito = await _ledger.WithDeadlinesAsync(await RemitoQueries.FindAsync(_ledger, request, call.Cuit, "idReq", ct), ct);
        return call.Ok(new XElement(call.Operation.Output, remito is null
            ? new XElement(wrapper, RemitoXml.Errors([NotFound]))
            : Consulted(wrapper, remito, order)));
    }

    private async Task<ContractAnswer> HistoryAsync(ServiceCall call, CancellationToken ct)
    {
        var remito = await _ledger.WithDeadlinesAsync(await RemitoQueries.FindAsync(_ledger, RemitoXml.Plain(call.Request), call.Cuit, "idReq", ct), ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("estadosRemitosReturn",
            remito is null
                ? RemitoXml.Errors([NotFound])
                : new object[] { new XElement("codRemito", remito.Code), await RemitoFamily.HistoryAsync(directory, remito, "descUsuario", ct) })));
    }

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var last = await _ledger.WithDeadlinesAsync(await _ledger.LastIssuedAsync(call.Cuit, call.Request.Int("tipoComprobante"), call.Request.Int("puntoEmision"), ct), ct);
        return call.Ok(new XElement(call.Operation.Output,
            last is null ? new XElement("consultarUltimoRemitoReturn") : Consulted("consultarUltimoRemitoReturn", last, RemitoOrder)));
    }

    /// <summary>Every remito, each with its deadline applied, for the lists.</summary>
    private async Task<List<Remito>> AllRefreshedAsync(CancellationToken ct)
    {
        var all = new List<Remito>();
        foreach (var remito in await _ledger.AllAsync(ct)) all.Add((await _ledger.WithDeadlinesAsync(remito, ct))!);
        return all;
    }

    // ---- Shapes ------------------------------------------------------------------------

    /// <summary>The remito a redirection starts from, as a consult sees it: only a party to the remito finds it.</summary>
    private async Task<Remito?> FindAsync(long code, long cuit, CancellationToken ct) =>
        await _ledger.WithDeadlinesAsync(await _ledger.FindAsync(code, ct), ct) is { } remito && remito.Involves(cuit) ? remito : null;

    /// <summary>RemitoReturnType: what ARCA assigned and the state, resultado, and the errors.</summary>
    private static ContractAnswer RemitoAnswer(ServiceCall call, string wrapper, Remito? remito, IReadOnlyCollection<RemitoProblem> problems) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            remito is null ? null : new object?[]
            {
                new XElement("codRemito", remito.Code),
                new XElement("tipoComprobante", remito.Type),
                remito.Point > 0 ? new XElement("puntoEmision", remito.Point) : null,
                Emission(remito),
                new XElement("estado", remito.State),
                remito.Issued ? new XElement("qr", RemitoTerms.Qr(remito)) : null,
            },
            new XElement("resultado", problems.Count == 0 ? "A" : "R"),
            RemitoXml.Errors(problems))));

    /// <summary>ConsultarRemitoReturnType: idReq, the remito in RemitoType (or RemitoImporteType) with ARCA's fields filled, and the qr.</summary>
    private static XElement Consulted(string wrapper, Remito remito, string[] order) => new(wrapper,
        new XElement("idReq", remito.RequestId),
        RemitoXml.Shape("remito", XElement.Parse(remito.Xml), order, new Dictionary<string, object?>
        {
            ["codRemito"] = remito.Code,
            ["tipoComprobante"] = remito.Type,
            ["estado"] = remito.State,
            ["datosEmision"] = Emission(remito),
            ["arrayContingencias"] = remito.Contingencies.Count == 0 ? null : new XElement("arrayContingencias", remito.Contingencies.Select(xml => XElement.Parse(xml))),
        }),
        remito.Issued ? new XElement("qr", RemitoTerms.Qr(remito)) : null);

    private static XElement? Emission(Remito remito) => remito.Issued
        ? new XElement("datosEmision",
            new XElement("nroRemito", remito.Number),
            new XElement("codAutorizacion", remito.AuthorizationCode),
            new XElement("fechaEmision", RemitoXml.Date(remito.IssuedAt!.Value)),
            new XElement("fechaVencimiento", RemitoXml.Date(remito.ExpiresOn!.Value)))
        : null;
}
