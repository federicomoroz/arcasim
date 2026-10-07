using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// Remito electrónico harinero (docs/arca/servicios/wsremharina.md): generar
/// gives the codRemito and leaves the remito PAT, PAD or issued; the holder
/// and the depositary authorize; the issuer issues (number per type and point
/// of emission, CRE, validity by distance, qr), cancels, changes the trip,
/// informs contingencies, redirects and confirms exports; the receiver
/// registers the reception, total or partial by the weights it reports.
/// Consults by codRemito, request id or number, the last one issued, the
/// history and the lists by role read what those operations stored.
/// ArcaSim's choices where the spec says NO VERIFICADO: a repeated
/// idReqCliente gets 151; anularRemito leaves ANS; a contingency 9, 11 or 12
/// leaves ANU; registrarReingreso marks the remito reingresado without a new
/// state; registrarExportacion leaves EXT, EXP or EXR like a reception; a
/// redirected remito is issued at once; consultarUltimoRemitoEmitido with
/// nothing issued answers without remitoOutput. 3023's text and the
/// contingency descriptions are ArcaSim's wording; an orden sent twice in a
/// reception counts once, the last informed, since the manual (2.5.7.5)
/// documents no error for it.
/// </summary>
public sealed class WsremharinaRules(IDocumentStore store, SequenceLocks locks, IClock clock, PadronDirectory directory) : IServiceBehavior
{
    private static readonly string[] RemitoOrder =
    [
        "tipoMovimiento", "tipoCmp", "esEntregaMostrador", "esMercaderiaEnConsignacion", "tipoEmisor", "rucaEstEmisor", "puntoEmision",
        "cuitTitular", "depositario", "receptor", "viaje", "arrayMercaderia", "codRemRedestinar", "reingresado", "importeCot", "observaciones",
    ];

    private static readonly string[] GoodsOrder =
    [
        "orden", "codTipo", "codComer", "descComer", "codTipoEmb", "cantidadEmb", "codTipoUnidad", "cantidadUnidad",
        "pesoNetoKg", "pesoNetoRecKg", "pesoNetoPerKg", "pesoNetoRedKg", "pesoNetoReiKg",
    ];

    private static readonly RemitoProblem NotFound = new(3022, "Remito no encontrado");
    private static readonly RemitoProblem NotAllowed = new(3070, "Operación no permitida");
    private static readonly RemitoProblem Required = new(1000, "Debe informar este valor");
    private static readonly RemitoProblem TripBeforeToday = new(140, "La fecha de inicio del viaje no puede ser anterior a hoy");
    private static readonly RemitoCodes Codes = new(NotFound, NotAllowed, NotAllowed, NotAllowed);

    private readonly RemitoLedger _ledger = new("wsremharina", store, locks, clock);

    public string Service => "wsremharina";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "dummy" => RemitoFamily.Dummy(call),
        "generarRemito" => await GenerateAsync(call, ct),
        "autorizarRemito" => await AuthorizeAsync(call, ct),
        "anularRemito" => await CancelAsync(call, ct),
        "emitirRemito" => await IssueAsync(call, ct),
        "registrarRecepcion" => await ReceiveAsync(call, export: false, ct),
        "registrarExportacion" => await ReceiveAsync(call, export: true, ct),
        "modificarViaje" => await ChangeTripAsync(call, ct),
        "informarContingencia" => await ContingencyAsync(call, ct),
        "registrarRedestino" => await RedirectAsync(call, ct),
        "registrarReingreso" => await ReenterAsync(call, ct),
        "consultarRemito" => await ConsultAsync(call, ct),
        "consultarEstadosRemito" => await HistoryAsync(call, ct),
        "consultarUltimoRemitoEmitido" => await LastAsync(call, ct),
        "consultarRemitosEmisor" => await ListAsync(call, ct),
        "consultarRemitosAutorizador" => await ListAsync(call, ct),
        "consultarRemitosReceptor" => await ListAsync(call, ct),
        "consultarReceptoresValidos" => await RemitoFamily.ValidReceiversAsync(call, directory, ct),
        "consultarTiposComprobante" => Table(call, RemitoTables.FlourVoucherTypes),
        "consultarTiposContingencia" => Table(call, RemitoTables.FlourContingencies),
        "consultarUnidadesVenta" => Table(call, RemitoTables.SaleUnits),
        "consultarTiposEstado" => call.Ok(new XElement(call.Operation.Output, new XElement("codigoDescripcionReturn",
            RemitoXml.Codes("arrayCodigoDescripcion", "codigoDescripcionString", RemitoTables.States)))),
        "consultarPuntosEmision" => call.Ok(new XElement(call.Operation.Output, new XElement("consultarPuntosEmisionReturn",
            RemitoXml.Codes("arrayPuntosEmision", "codigoDescripcion", await RemitoFamily.PointsAsync(directory, call.Cuit, RemitoFamily.MaxShortCode, ct))))),
        "consultarCodigosDomicilio" => await AddressesAsync(call, ct),
        _ => null,
    };

    // ---- Generation and lifecycle ------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var sent = request.Element("remito") ?? new XElement("remito");
        var requestId = request.ChildLong("idReqCliente") ?? 0;
        var point = (int)(sent.ChildLong("puntoEmision") ?? 0);
        var holder = sent.ChildLong("cuitTitular") ?? call.Cuit;
        var depositary = sent.Element("depositario").ChildLong("cuitDepositario");
        var receiver = sent.Element("receptor")?.Element("receptorNacional").ChildLong("cuitReceptor");

        using var requests = await _ledger.LockRequestsAsync(call.Cuit, point, ct);
        var problems = new List<RemitoProblem>();
        foreach (var party in new[] { call.Cuit, holder, depositary, receiver }.Distinct())
            if (await RemitoFamily.CheckPartyAsync(directory, party, ct) is { } problem && !problems.Contains(problem)) problems.Add(problem);
        if (await _ledger.FindByRequestAsync(call.Cuit, point, requestId, ct) is not null)
            problems.Add(new RemitoProblem(151, $"El ID de request {requestId} ya existe para el punto de emisión {point}"));
        if (sent.Element("viaje").ChildDate("fechaInicioViaje") < _ledger.Today) problems.Add(TripBeforeToday);
        if (problems.Count > 0) return RemitoAnswer(call, "generarRemitoReturn", null, problems);

        if (sent.Element("tipoCmp") is null)
            RemitoXml.Put(sent, "tipoCmp", sent.Element("viaje")?.Element("vehiculo")?.Element("ferroviario") is null ? 993 : 994, RemitoOrder);
        var remito = new Remito
        {
            Code = await _ledger.NextCodeAsync(ct),
            Issuer = call.Cuit,
            RequestId = requestId,
            Point = point,
            Type = (int)(sent.ChildLong("tipoCmp") ?? 993),
            Movement = sent.Child("tipoMovimiento") ?? "ENV",
            Holder = holder,
            Depositary = depositary,
            Receiver = receiver,
            Foreign = sent.Element("receptor")?.Element("receptorExtranjero") is not null,
            DistanceKm = sent.Element("viaje").ChildDecimal("distanciaKm") ?? 0,
            CreatedOn = _ledger.Today,
            Xml = sent.ToString(SaveOptions.DisableFormatting),
        };
        RemitoFamily.Open(remito, _ledger.Now);
        await _ledger.AddAsync(remito, ct);
        if (remito.Pending is null) await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "generarRemitoReturn", remito, []);
    }

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CancellationToken ct)
    {
        var code = call.Request.Long("codRemito");
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        var problem = RemitoFamily.Authorize(remito, call.Cuit, call.Request.Text("estado") == "A", _ledger.Now, Codes);
        if (problem is null) await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, problem);
    }

    private async Task<ContractAnswer> CancelAsync(ServiceCall call, CancellationToken ct)
    {
        var code = call.Request.Long("codRemito");
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        var problem = RemitoFamily.Cancel(remito, call.Cuit, _ledger.Now, Codes);
        if (problem is null) await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, problem);
    }

    /// <summary>PEM to EMI; a trip sent here replaces the one the remito was generated with.</summary>
    private async Task<ContractAnswer> IssueAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        if (await FindAsync(request.ChildLong("codRemito") ?? 0, call.Cuit, ct) is not { } remito)
            return RemitoAnswer(call, "emitirRemitoReturn", null, [NotFound]);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.PendingIssue)
            return RemitoAnswer(call, "emitirRemitoReturn", null, [NotAllowed]);
        if (request.Element("viaje") is { } trip)
        {
            if (trip.ChildDate("fechaInicioViaje") < _ledger.Today) return RemitoAnswer(call, "emitirRemitoReturn", null, [TripBeforeToday]);
            ReplaceTrip(remito, trip);
        }
        await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "emitirRemitoReturn", remito, []);
    }

    /// <summary>
    /// The receiver accepts or rejects; the system works out total or partial
    /// from the weights, which cannot exceed what was sent (3023). An export
    /// is confirmed by the issuer the same way, to EXT, EXP or EXR.
    /// </summary>
    private async Task<ContractAnswer> ReceiveAsync(ServiceCall call, bool export, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        var allowed = remito.State == RemitoStates.Issued && (export ? remito.Foreign && remito.Issuer == call.Cuit : remito.Receiver == call.Cuit);
        if (!allowed) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotAllowed);

        var document = RemitoXml.Parse(remito.Xml);
        var goods = document.Element("arrayMercaderia")?.Elements("mercaderia").ToList() ?? [];
        var accepted = request.Child("aceptado") == "S";
        var reported = request.Element("arrayRecepcionMercaderia") is { } list
            ? RemitoXml.ByOrder(list.Elements("recepcionMercaderia"), e => e.ChildDecimal("pesoNetoKG") ?? 0)
            : null;
        decimal sentTotal = 0, receivedTotal = 0;
        foreach (var item in goods)
        {
            var sent = item.ChildDecimal("pesoNetoKg");
            var received = !accepted ? 0 : reported is null ? sent ?? 0 : reported.GetValueOrDefault(item.ChildLong("orden") ?? 0);
            if (received > sent)
                return RemitoFamily.OperationAnswer(call, "operacionReturn", code,
                    new RemitoProblem(3023, $"El peso neto recibido del ítem {item.Child("orden")} no puede superar el peso neto enviado"));
            RemitoXml.Put(item, "pesoNetoRecKg", RemitoXml.Number(received), GoodsOrder);
            sentTotal += sent ?? 0;
            receivedTotal += received;
        }

        var state = !accepted || receivedTotal == 0 ? (export ? RemitoStates.ExportRefused : RemitoStates.NotAccepted)
            : receivedTotal >= sentTotal ? (export ? RemitoStates.Exported : RemitoStates.Accepted)
            : (export ? RemitoStates.PartlyExported : RemitoStates.PartlyAccepted);
        remito.Xml = document.ToString(SaveOptions.DisableFormatting);
        remito.ReceivedOn = request.ChildDate("fecha") ?? _ledger.Today;
        remito.MoveTo(state, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, []);
    }

    /// <summary>Changing the carrier or vehicle of an issued remito, within 24 to 240 hours of issue by distance.</summary>
    private async Task<ContractAnswer> ChangeTripAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.Issued
            || _ledger.Now > remito.IssuedAt!.Value.AddHours(RemitoTerms.ChangeHours(remito.DistanceKm)))
            return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotAllowed);
        if (request.Element("viaje") is { } trip) ReplaceTrip(remito, trip);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, []);
    }

    /// <summary>9, 11 and 12 cancel the remito (ANU); 13, a delay, adds a day of validity; 10 and 14 only stay on record.</summary>
    private async Task<ContractAnswer> ContingencyAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var code = request.ChildLong("codRemito") ?? 0;
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        var contingency = request.Element("contingencia") ?? new XElement("contingencia");
        var kind = (int)(contingency.ChildLong("codTipoContingencia") ?? 0);
        if (remito.State != RemitoStates.Issued || !RemitoTables.FlourContingencies.Any(c => (int)c.Code == kind))
            return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotAllowed);

        remito.Contingencies.Add(contingency.ToString(SaveOptions.DisableFormatting));
        if (RemitoTables.CancellingContingencies.Contains(kind)) remito.MoveTo(RemitoStates.Cancelled, _ledger.Now, call.Cuit);
        else if (kind == RemitoTables.DelayContingency) remito.ExpiresOn = remito.ExpiresOn?.AddDays(1);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, []);
    }

    /// <summary>
    /// What a receiver did not accept goes to another receiver: a new remito,
    /// tipoMovimiento RED, with its own codRemito and the weights given.
    /// </summary>
    private async Task<ContractAnswer> RedirectAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        if (await FindAsync(request.ChildLong("codRemito") ?? 0, call.Cuit, ct) is not { } original)
            return RemitoAnswer(call, "registrarRedestinoReturn", null, [NotFound]);
        if (original.Issuer != call.Cuit || original.State is not (RemitoStates.PartlyAccepted or RemitoStates.NotAccepted))
            return RemitoAnswer(call, "registrarRedestinoReturn", null, [NotAllowed]);
        var requestId = request.ChildLong("idReqCliente") ?? 0;
        using var requests = await _ledger.LockRequestsAsync(call.Cuit, original.Point, ct);
        if (await _ledger.FindByRequestAsync(call.Cuit, original.Point, requestId, ct) is not null)
            return RemitoAnswer(call, "registrarRedestinoReturn", null,
                [new RemitoProblem(151, $"El ID de request {requestId} ya existe para el punto de emisión {original.Point}")]);
        var receiver = request.ChildLong("cuitReceptor") ?? 0;
        if (await RemitoFamily.CheckPartyAsync(directory, receiver, ct) is { } problem) return RemitoAnswer(call, "registrarRedestinoReturn", null, [problem]);

        var document = RemitoXml.Parse(original.Xml);
        var goods = document.Element("arrayMercaderia")?.Elements("mercaderia").ToList() ?? [];
        var weights = request.Element("arrayRedestinoMercaderia")?.Elements("recepcionMercaderia").ToList() ?? [];
        var redirected = weights
            .Select(w => (Item: goods.FirstOrDefault(g => g.ChildLong("orden") == w.ChildLong("orden")), Weight: w.ChildDecimal("pesoNetoKG") ?? 0))
            .Where(w => w.Item is not null)
            .Select(w =>
            {
                var item = new XElement(w.Item!);
                foreach (var name in GoodsOrder.Skip(9)) item.Element(name)?.Remove();
                RemitoXml.Put(item, "pesoNetoKg", RemitoXml.Number(w.Weight), GoodsOrder);
                return item;
            }).ToList();
        if (redirected.Count == 0) return RemitoAnswer(call, "registrarRedestinoReturn", null, [Required]);

        RemitoXml.Put(document, "tipoMovimiento", "RED", RemitoOrder);
        RemitoXml.Put(document, new XElement("arrayMercaderia", redirected), RemitoOrder);
        RemitoXml.Put(document, "codRemRedestinar", original.Code, RemitoOrder);
        var cuitPais = document.Element("receptor")?.Child("cuitPaisReceptor");
        RemitoXml.Put(document, new XElement("receptor",
            cuitPais is null ? null : new XElement("cuitPaisReceptor", cuitPais),
            new XElement("receptorNacional",
                new XElement("cuitReceptor", receiver),
                new XElement("tipoDomReceptor", request.Child("tipoDomReceptor")),
                new XElement("codDomReceptor", request.Child("codDomReceptor")))), RemitoOrder);

        var remito = new Remito
        {
            Code = await _ledger.NextCodeAsync(ct),
            Issuer = call.Cuit,
            RequestId = requestId,
            Point = original.Point,
            Type = original.Type,
            Movement = "RED",
            Holder = original.Holder,
            Depositary = original.Depositary,
            Receiver = receiver,
            DistanceKm = original.DistanceKm,
            CreatedOn = _ledger.Today,
            Redirects = original.Code,
            Xml = document.ToString(SaveOptions.DisableFormatting),
        };
        await _ledger.AddAsync(remito, ct);
        await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "registrarRedestinoReturn", remito, []);
    }

    private async Task<ContractAnswer> ReenterAsync(ServiceCall call, CancellationToken ct)
    {
        var code = call.Request.Long("codRemito");
        if (await FindAsync(code, call.Cuit, ct) is not { } remito) return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotFound);
        var delivery = remito.Movement == "REP" && remito.State == RemitoStates.Issued;
        if (remito.Issuer != call.Cuit || !(delivery || remito.State is RemitoStates.PartlyAccepted or RemitoStates.NotAccepted))
            return RemitoFamily.OperationAnswer(call, "operacionReturn", code, NotAllowed);
        var document = RemitoXml.Parse(remito.Xml);
        RemitoXml.Put(document, "reingresado", "S", RemitoOrder);
        remito.Xml = document.ToString(SaveOptions.DisableFormatting);
        await _ledger.SaveAsync(remito, ct);
        return RemitoFamily.OperationAnswer(call, "operacionReturn", code, []);
    }

    // ---- Consults ----------------------------------------------------------------------

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var remito = await RemitoQueries.FindAsync(_ledger, request, call.Cuit, "idReqCliente", ct);
        var problem = !RemitoQueries.AsksForOne(request, "idReqCliente") ? Required : remito is null ? NotFound : null;
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarRemitoReturn",
            remito is null ? null : Output(remito),
            problem is null ? null : RemitoXml.Errors([problem]))));
    }

    private async Task<ContractAnswer> HistoryAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var remito = await RemitoQueries.FindAsync(_ledger, request, call.Cuit, "idReqCliente", ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("estadosRemitoReturn",
            remito is null
                ? RemitoXml.Errors([RemitoQueries.AsksForOne(request, "idReqCliente") ? NotFound : Required])
                : new object[] { new XElement("codRemito", remito.Code), await RemitoFamily.HistoryAsync(directory, remito, "cuitDesc", ct) })));
    }

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var last = await _ledger.LastIssuedAsync(call.Cuit, call.Request.Int("tipoComprobante"), call.Request.Int("puntoEmision"), ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarUltimoRemitoReturn", last is null ? null : Output(last))));
    }

    private async Task<ContractAnswer> ListAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var range = request.Element("rangoFecha");
        var (from, to) = (range.ChildDate("fechaDesde"), range.ChildDate("fechaHasta"));
        var issuer = request.ChildLong("cuitEmisor");
        var all = await _ledger.AllAsync(ct);
        var found = call.Name switch
        {
            "consultarRemitosEmisor" => RemitoFamily.ForIssuer(all, call.Cuit, (int)(request.ChildLong("ptoEmision") ?? 0),
                (int?)request.ChildLong("tipoComprobante"), request.Child("estado"), from, to),
            "consultarRemitosAutorizador" => RemitoFamily.ForAuthorizer(all, call.Cuit, request.Child("rolAutorizador") ?? "",
                request.Child("estadoAutorizacion") ?? "", issuer, from, to),
            _ => RemitoFamily.ForReceiver(all, call.Cuit, request.Child("estadoRecepcion") ?? "", issuer, from, to),
        };
        return RemitoFamily.ListAnswer(call, found, r => new XElement("item",
            new XElement("cuitEmisor", r.Issuer),
            new XElement("codRemito", r.Code),
            new XElement("puntoEmision", r.Point),
            new XElement("tipoCmp", r.Type),
            r.Number is { } number ? new XElement("nroRemito", number) : null,
            new XElement("idReqCliente", r.RequestId),
            new XElement("estadoActual", r.State),
            new XElement("fechaOper", RemitoXml.Date(DateOnly.FromDateTime(r.History[^1].At.DateTime)))), "infoRemito");
    }

    private async Task<ContractAnswer> AddressesAsync(ServiceCall call, CancellationToken ct)
    {
        var address = await RemitoFamily.FiscalAddressAsync(directory, call.Request.Long("cuitTitularDomicilio"), ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarCodigosDomicilioReturn",
            address is { } found
                ? RemitoXml.Codes("arrayDomicilios", "codigoDescripcion", [found])
                : RemitoXml.Errors([RemitoFamily.NotRegistered]))));
    }

    private static ContractAnswer Table(ServiceCall call, IEnumerable<(object Code, string Text)> rows) =>
        call.Ok(new XElement(call.Operation.Output, new XElement("codigoDescripcionReturn",
            RemitoXml.Codes("arrayCodigoDescripcion", "codigoDescripcion", rows))));

    // ---- Shapes ------------------------------------------------------------------------

    private async Task<Remito?> FindAsync(long code, long cuit, CancellationToken ct) =>
        await _ledger.FindAsync(code, ct) is { } remito && remito.Involves(cuit) ? remito : null;

    private static void ReplaceTrip(Remito remito, XElement trip)
    {
        var document = RemitoXml.Parse(remito.Xml);
        RemitoXml.Put(document, new XElement(trip), RemitoOrder);
        remito.Xml = document.ToString(SaveOptions.DisableFormatting);
        remito.DistanceKm = trip.ChildDecimal("distanciaKm") ?? remito.DistanceKm;
    }

    /// <summary>RemitoReturnType: the remito when there is one, resultado, and the errors.</summary>
    private static ContractAnswer RemitoAnswer(ServiceCall call, string wrapper, Remito? remito, IReadOnlyCollection<RemitoProblem> problems) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            remito is null ? null : Output(remito),
            new XElement("resultado", problems.Count == 0 ? "A" : "R"),
            RemitoXml.Errors(problems))));

    /// <summary>RemitoOutputType: the remito as generated, what ARCA assigned and its state.</summary>
    private static XElement Output(Remito remito) => new("remitoOutput",
        new XElement("codRemito", remito.Code),
        new XElement("idReqCliente", remito.RequestId),
        new XElement("cuitEmisor", remito.Issuer),
        RemitoXml.Parse(remito.Xml),
        remito.Issued
            ? new XElement("datosAutAFIP",
                new XElement("nroRemito", remito.Number),
                new XElement("codAutorizacion", remito.AuthorizationCode),
                new XElement("fechaEmision", RemitoXml.Date(DateOnly.FromDateTime(remito.IssuedAt!.Value.DateTime))),
                new XElement("fechaVencimiento", RemitoXml.Date(remito.ExpiresOn!.Value)))
            : null,
        new XElement("estadoRemito", remito.State),
        remito.Issued ? new XElement("qr", RemitoTerms.Qr(remito)) : null,
        remito.Contingencies.Count == 0 ? null : new XElement("arrayContingencias", remito.Contingencies.Select(RemitoXml.Parse)),
        remito.AuthorizedOn is { } authorized ? new XElement("fechaAut", RemitoXml.Date(authorized)) : null,
        remito.ReceivedOn is { } received ? new XElement("fechaRec", RemitoXml.Date(received)) : null);
}
