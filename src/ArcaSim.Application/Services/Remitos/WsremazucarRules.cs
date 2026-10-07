using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// Remito electrónico de azúcar (docs/arca/servicios/wsremazucar.md):
/// generarRemito issues at once when the issuer owns the goods (997, or 998
/// to a receiver abroad) or leaves it PAT for the holder, who authorizes (S)
/// or denies (N); the issuer issues, changes the driver within 24 hours,
/// informs contingencies (which cancel) and confirms exports; the receiver
/// confirms the reception, total or partial by the quantities it reports;
/// the issuer then validates (CON) or not (NCO) a partial or rejected one,
/// and can correct an NCO. While a receiver has remitos to validate, the
/// issuer cannot generate new ones to it (7014). Consults by codRemito,
/// request id or number, the history and the lists by role read what those
/// operations stored, paged with numeroPagina, maxPaginas and maxRegistros.
/// ArcaSim's choices where the spec says NO VERIFICADO: a repeated
/// idReqCliente gets 151; 998 is the type for a foreign receiver; a
/// contingency leaves ANU; a total export leaves EXT and a partial or refused
/// one ACP or NAC, to be validated like a reception; an unknown remito gets
/// 7015 and a role or state that does not allow the operation 3070; 140's
/// text is harina's; the texts of 7106, 7108 and 7109 are ArcaSim's wording
/// of the rules; validity is harina's table by distance; a list with no
/// remitos answers an empty page; the parties' standing in the registry is not
/// checked (the manual's 7000, 7001, 7006 and 7013 are not simulated, so no
/// party is refused for being unregistered or inactive); a repeated orden in a
/// reception counts once, as the manual documents no error for it.
/// </summary>
public sealed class WsremazucarRules(IDocumentStore store, SequenceLocks locks, IClock clock, PadronDirectory directory) : IServiceBehavior
{
    private const int DomesticType = 997;
    private const int ExportType = 998;

    private static readonly string[] OutputOrder =
    [
        "codRemito", "idRequest", "estado", "cuitEmisor", "esEntregaMostrador", "puntoEmision", "cuitTitularMercaderia", "tipoTitularMercaderia",
        "numeroMaquila", "cuitProductorContrato", "cuitAutorizadoRetirar", "receptor", "viaje", "arrayMercaderias", "arrayContingencias",
        "datosAutorizacion", "importeCot", "comentarios",
    ];

    private static readonly string[] TruckOrder = ["codPaisTransportista", "transporteNacional", "transporteExtranjero", "dominioVehiculo", "dominioAcoplado"];

    private static readonly RemitoProblem NotFound = new(7015, "Valor informado inválido");
    // "permitda" is the manual's own typo (wsremazucar manual 14, "Códigos generales"); harina and carne print "permitida".
    private static readonly RemitoProblem NotAllowed = new(3070, "Operación no permitda");
    private static readonly RemitoProblem TripBeforeToday = new(140, "La fecha de inicio del viaje no puede ser anterior a hoy");
    private static readonly RemitoCodes Codes = new(NotFound, NotAllowed, NotAllowed, NotAllowed);

    private readonly RemitoLedger _ledger = new("wsremazucar", store, locks, clock);

    public string Service => "wsremazucar";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "generarRemito" => await GenerateAsync(call, ct),
        "autorizarRemitoTitular" => await AuthorizeAsync(call, ct),
        "emitirRemito" => await IssueAsync(call, ct),
        "confirmarRecepcionMercaderia" => await ReceiveAsync(call, export: false, ct),
        "confirmarExportacionMercaderia" => await ReceiveAsync(call, export: true, ct),
        "convalidarEmisor" => await ValidateAsync(call, ct),
        "corregirConvalidacionEmisor" => await CorrectAsync(call, ct),
        "modificarConductor" => await ChangeDriverAsync(call, ct),
        "informarContingencia" => await ContingencyAsync(call, ct),
        "consultarRemito" => await ConsultAsync(call, ct),
        "consultarEstadosRemito" => await HistoryAsync(call, ct),
        "consultarRemitosEmisor" or "consultarRemitosTitular" or "consultarRemitosReceptor" => await ListAsync(call, ct),
        "consultarTiposComprobante" => RemitoFamily.Table(call, "consultarTiposComprobanteReturn", "arrayTiposComprobante", "codigoDescripcion", RemitoTables.SugarVoucherTypes),
        "consultarTiposEstado" => RemitoFamily.Table(call, "consultarTiposEstadoReturn", "arrayTiposEstado", "codigoDescripcionString", RemitoTables.SugarStates),
        "consultarTiposTitular" => RemitoFamily.Table(call, "codigoDescripcionReturn", "arrayCodigoDescripcion", "codigoDescripcion", RemitoTables.SugarHolderTypes),
        "consultarPuntosEmision" => await RemitoFamily.PointsAnswerAsync(call, directory, RemitoFamily.MaxPointOfEmission, ct),
        "consultarCodigosDomicilio" => await RemitoFamily.AddressesAnswerAsync(call, directory, ct),
        _ => null,
    };

    // ---- Generation and lifecycle ------------------------------------------------------

    private async Task<ContractAnswer> GenerateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var sent = request.Element("remito") ?? new XElement("remito");
        var requestId = request.ChildLong("idReqCliente") ?? 0;
        var point = (int)(sent.ChildLong("puntoEmision") ?? 0);
        var receiver = sent.Element("receptor")?.Element("receptorNacional").ChildLong("cuitReceptor");
        var year = _ledger.Today.Year;

        using var requests = await _ledger.LockRequestsAsync(call.Cuit, point, ct);
        var problems = new List<RemitoProblem>();
        if (await _ledger.FindByRequestAsync(call.Cuit, point, requestId, ct) is not null)
            problems.Add(new RemitoProblem(151, $"El ID de request {requestId} ya existe para el punto de emisión {point}"));
        if (sent.Element("viaje").ChildDate("fechaInicioViaje") < _ledger.Today) problems.Add(TripBeforeToday);
        if (sent.Element("arrayMercaderias")?.Elements("mercaderia").Any(m => m.ChildLong("anioZafra") is { } harvest && (harvest > year || harvest < year - 10)) == true)
            problems.Add(new RemitoProblem(7102, "El año no puede ser posterior al actual, ni anterior a 10 años"));
        if (receiver is { } cuit && (await _ledger.AllAsync(ct)).Any(r => r.Issuer == call.Cuit && r.Receiver == cuit
                && r.State is RemitoStates.PartlyAccepted or RemitoStates.NotAccepted or RemitoStates.NotValidated))
            problems.Add(new RemitoProblem(7014, "Usted posee remitos electrónicos pendientes de convalidación o \"No convalidados\" con el receptor de la mercadería"));
        if (problems.Count > 0) return RemitoAnswer(call, "generarRemitoReturn", null, problems);

        var foreign = sent.Element("receptor")?.Element("receptorExtranjero") is not null;
        var remito = new Remito
        {
            Code = await _ledger.NextCodeAsync(ct),
            Issuer = call.Cuit,
            RequestId = requestId,
            Point = point,
            Type = foreign ? ExportType : DomesticType,
            Movement = "ENV",
            Holder = sent.ChildLong("cuitTitularMercaderia") ?? call.Cuit,
            Receiver = receiver,
            Foreign = foreign,
            DistanceKm = sent.Element("viaje").ChildDecimal("kmDistancia") ?? 0,
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
        var request = Wrapped(call, "autorizarRemitoTitular");
        if (await FindAsync(request.ChildLong("codigoRemito") ?? 0, call.Cuit, ct) is not { } remito) return Simple(call, "autorizarRemitoReturn", NotFound);
        var problem = RemitoFamily.Authorize(remito, call.Cuit, request.ChildText("autorizar") == "S", _ledger.Now, Codes);
        if (problem is null) await _ledger.SaveAsync(remito, ct);
        return Simple(call, "autorizarRemitoReturn", problem);
    }

    private async Task<ContractAnswer> IssueAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Wrapped(call, "emitirRemito");
        if (await FindAsync(request.ChildLong("codigoRemito") ?? 0, call.Cuit, ct) is not { } remito)
            return RemitoAnswer(call, "emitirRemitoReturn", null, [NotFound]);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.PendingIssue) return RemitoAnswer(call, "emitirRemitoReturn", null, [NotAllowed]);
        if (request.ChildDate("fechaInicioViaje") is { } start)
        {
            if (start < _ledger.Today) return RemitoAnswer(call, "emitirRemitoReturn", null, [TripBeforeToday]);
            RemitoXml.Edit(remito, document => document.Element("viaje")?.Element("fechaInicioViaje")?.SetValue(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        if (request.Element("transporte") is { } transport)
            RemitoXml.Edit(remito, document => ChangeTruck(document, transport.Element("conductor"), transport.ChildText("dominioVehiculo"), transport.ChildText("dominioAcoplado")));
        await _ledger.IssueAsync(remito, call.Cuit, ct);
        return RemitoAnswer(call, "emitirRemitoReturn", remito, []);
    }

    /// <summary>
    /// The receiver confirms what arrived; quantities cannot exceed what was
    /// sent. An export is confirmed by the issuer: all of it leaves EXT.
    /// </summary>
    private async Task<ContractAnswer> ReceiveAsync(ServiceCall call, bool export, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        if (await FindAsync(request.ChildLong("codigoRemito") ?? 0, call.Cuit, ct) is not { } remito)
            return Simple(call, "confirmarRecepcionMercaderiaReturn", NotFound);
        var allowed = remito.State == RemitoStates.Issued && (export ? remito.Foreign && remito.Issuer == call.Cuit : remito.Receiver == call.Cuit);
        if (!allowed) return Simple(call, "confirmarRecepcionMercaderiaReturn", NotAllowed);

        var sent = Goods(remito);
        var accepted = request.ChildText("aceptaRecepcion") == "S";
        var reported = request.Element("arrayMercaderiaRecibida")?.Elements("mercaderia").ToList();
        var received = new Dictionary<int, long>();
        if (accepted && reported is null)
            foreach (var (order, quantity) in sent) received[order] = quantity;
        foreach (var line in accepted && reported is not null ? reported : [])
        {
            var order = (int)(line.ChildLong("orden") ?? 0);
            var quantity = line.ChildLong("cantidad") ?? 0;
            if (!sent.TryGetValue(order, out var limit)) return Simple(call, "confirmarRecepcionMercaderiaReturn", new RemitoProblem(7106, "No existe mercadería para recibir"));
            if (quantity > limit)
                return Simple(call, "confirmarRecepcionMercaderiaReturn", new RemitoProblem(7108, "La cantidad recibida no debe superar la cantidad emitida"));
            received[order] = quantity;
        }

        var total = received.Values.Sum();
        var state = total == 0 ? RemitoStates.NotAccepted
            : total >= sent.Values.Sum() ? (export ? RemitoStates.Exported : RemitoStates.Accepted)
            : RemitoStates.PartlyAccepted;
        remito.Received = received;
        remito.ReceivedOn = _ledger.Today;
        remito.MoveTo(state, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return Simple(call, "confirmarRecepcionMercaderiaReturn", null);
    }

    /// <summary>The issuer validates (CON) or not (NCO) a partial or rejected reception.</summary>
    private async Task<ContractAnswer> ValidateAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Wrapped(call, "convalidaRechazoReceptor");
        if (await FindAsync(request.ChildLong("codigoRemito") ?? 0, call.Cuit, ct) is not { } remito) return Simple(call, "convalidarEmisorReturn", NotFound);
        if (remito.Issuer != call.Cuit || remito.State is not (RemitoStates.PartlyAccepted or RemitoStates.NotAccepted))
            return Simple(call, "convalidarEmisorReturn", NotAllowed);
        remito.MoveTo(request.ChildText("convalida") == "S" ? RemitoStates.Validated : RemitoStates.NotValidated, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return Simple(call, "convalidarEmisorReturn", null);
    }

    private async Task<ContractAnswer> CorrectAsync(ServiceCall call, CancellationToken ct)
    {
        if (await FindAsync(call.Request.Long("codRemito"), call.Cuit, ct) is not { } remito) return Simple(call, "convalidarEmisorReturn", NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.NotValidated) return Simple(call, "convalidarEmisorReturn", NotAllowed);
        remito.MoveTo(RemitoStates.Validated, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return Simple(call, "convalidarEmisorReturn", null);
    }

    /// <summary>Driver, vehicle or trailer of an issued remito, within 24 hours of issue and before it is received.</summary>
    private async Task<ContractAnswer> ChangeDriverAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Wrapped(call, "modificarConductor");
        if (await FindAsync(request.ChildLong("codRemito") ?? 0, call.Cuit, ct) is not { } remito) return Simple(call, "modificarConductorReturn", NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.Issued || _ledger.Now > remito.IssuedAt!.Value.AddHours(RemitoTerms.FlatChangeHours))
            return Simple(call, "modificarConductorReturn", NotAllowed);
        RemitoXml.Edit(remito, document => ChangeTruck(document, request.Element("conductor"), request.ChildText("dominioVehiculo"), request.ChildText("dominioAcoplado")));
        await _ledger.SaveAsync(remito, ct);
        return Simple(call, "modificarConductorReturn", null);
    }

    /// <summary>"Realiza la anulación del remito" (manual 6): ANU, with what was lost on record.</summary>
    private async Task<ContractAnswer> ContingencyAsync(ServiceCall call, CancellationToken ct)
    {
        var request = Wrapped(call, "informarContingencia");
        if (await FindAsync(request.ChildLong("codigoRemito") ?? 0, call.Cuit, ct) is not { } remito) return Simple(call, "informarContingenciaReturn", NotFound);
        if (remito.Issuer != call.Cuit || remito.State != RemitoStates.Issued) return Simple(call, "informarContingenciaReturn", NotAllowed);
        var sent = Goods(remito);
        var lost = new Dictionary<int, long>();
        foreach (var line in request.Element("arrayMercaderiaPerdida")?.Elements("mercaderia") ?? [])
        {
            var order = (int)(line.ChildLong("orden") ?? 0);
            var quantity = line.ChildLong("cantidad") ?? 0;
            if (!sent.TryGetValue(order, out var limit) || quantity > limit)
                return Simple(call, "informarContingenciaReturn", new RemitoProblem(7109, "La cantidad perdida no puede superar la cantidad emitida"));
            lost[order] = quantity;
        }
        remito.Lost = lost;
        remito.Contingencies.Add(new XElement("contingencia",
            new XElement("tipoContingencia", request.ChildText("tipoContingencia")),
            lost.Select(l => new XElement("arrayMercaderiaPerdida", new XElement("orden", l.Key), new XElement("cantidad", l.Value))),
            new XElement("observaciones", request.ChildText("observaciones"))).ToString(SaveOptions.DisableFormatting));
        remito.MoveTo(RemitoStates.Cancelled, _ledger.Now, call.Cuit);
        await _ledger.SaveAsync(remito, ct);
        return Simple(call, "informarContingenciaReturn", null);
    }

    // ---- Consults ----------------------------------------------------------------------

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        var remito = await RemitoQueries.FindAsync(_ledger, RemitoXml.Plain(call.Request), call.Cuit, "idReqCliente", ct);
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarRemitoReturn",
            new XElement("resultado", remito is null ? "R" : "A"),
            remito is null ? RemitoXml.Errors([NotFound]) : Output(remito))));
    }

    private async Task<ContractAnswer> HistoryAsync(ServiceCall call, CancellationToken ct)
    {
        var remito = await FindAsync(call.Request.Long("codRemito"), call.Cuit, ct);
        var steps = remito?.History ?? [];
        return call.Ok(new XElement(call.Operation.Output, new XElement("consultarEstadosRemitoReturn",
            new XElement("arrayEstadosRemito", steps.Select((step, i) => new XElement("historialAcciones",
                new XElement("fecha", RemitoXml.Date(step.At)),
                new XElement("estado", step.State),
                new XElement("activo", i == steps.Count - 1 ? "S" : "N")))),
            remito is null ? RemitoXml.Errors([NotFound]) : null)));
    }

    /// <summary>The issuer's, the holder's or the receiver's remitos generated in the range, 2000 per page.</summary>
    private async Task<ContractAnswer> ListAsync(ServiceCall call, CancellationToken ct)
    {
        var request = RemitoXml.Plain(call.Request);
        var (from, to) = (request.ChildDate("fechaDesde"), request.ChildDate("fechaHasta"));
        var state = request.ChildText("estado");
        var type = request.ChildLong("tipoComprobante");
        var (issuer, holder, receiver) = (request.ChildLong("cuitEmisor"), request.ChildLong("cuitTitular"), request.ChildLong("cuitReceptor"));
        var mine = call.Name switch
        {
            "consultarRemitosEmisor" => (Func<Remito, bool>)(r => r.Issuer == call.Cuit && (holder is null || r.Holder == holder)),
            "consultarRemitosTitular" => r => r.Holder == call.Cuit && (issuer is null || r.Issuer == issuer),
            _ => r => r.Receiver == call.Cuit && r.Issued && (issuer is null || r.Issuer == issuer),
        };
        var found = (await _ledger.AllAsync(ct)).Where(r => mine(r) && RemitoFamily.Within(r.CreatedOn, from, to)
            && (string.IsNullOrEmpty(state) || r.State == state) && (type is null || r.Type == type)
            && (receiver is null || call.Name == "consultarRemitosReceptor" || r.Receiver == receiver)).ToList();

        var page = Math.Max(request.ChildLong("numeroPagina") ?? 1, 1);
        var (items, _) = RemitoFamily.Page(found, (int)page);
        var wrapper = call.Name + "Return";
        return call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            new XElement("resultado", "A"),
            items.Count == 0 ? null : new XElement("arrayRemitos", items.Select(r => new XElement("remito",
                new XElement("codigoRemito", r.Code),
                new XElement("estadoRemito", r.State),
                new XElement("puntoEmision", r.Point),
                r.Receiver is { } receiverCuit ? new XElement("cuitReceptor", receiverCuit) : null,
                new XElement("cuitTitularMercaderia", r.Holder),
                new XElement("idTipoComprobante", r.Type)))),
            new XElement("numeroPagina", page),
            new XElement("maxPaginas", (found.Count + RemitoFamily.PageSize - 1) / RemitoFamily.PageSize),
            new XElement("maxRegistros", found.Count))));
    }

    // ---- Shapes ------------------------------------------------------------------------

    private Task<Remito?> FindAsync(long code, long cuit, CancellationToken ct) => _ledger.FindForAsync(code, cuit, ct);

    /// <summary>The element azúcar wraps a write operation's data in (emitirRemito, autorizarRemitoTitular...).</summary>
    private static XElement Wrapped(ServiceCall call, string name) => RemitoXml.Plain(call.Request).Element(name) ?? new XElement(name);

    private static Dictionary<int, long> Goods(Remito remito) =>
        XElement.Parse(remito.Xml).Element("arrayMercaderias")?.Elements("mercaderia")
            .GroupBy(m => (int)(m.ChildLong("orden") ?? 0))
            .ToDictionary(g => g.Key, g => g.Sum(m => m.ChildLong("cantidad") ?? 0)) ?? [];

    /// <summary>A new driver, vehicle or trailer on the remito's truck legs.</summary>
    private static void ChangeTruck(XElement document, XElement? driver, string? vehicle, string? trailer)
    {
        foreach (var truck in document.Element("viaje")?.Elements("tramo").Select(t => t.Element("automotor")).OfType<XElement>() ?? [])
        {
            if (driver?.Element("conductorNacional").ChildLong("cuitConductor") is { } cuit)
                truck.Element("transporteNacional")?.Element("cuitConductor")?.SetValue(cuit);
            if (driver?.Element("conductorExtranjero") is { } foreign && truck.Element("transporteExtranjero") is { } abroad)
                foreach (var field in new[] { "cedulaConductor", "nombreConductor", "apellidoConductor" })
                    abroad.Element(field)?.SetValue(foreign.ChildText(field) ?? "");
            if (!string.IsNullOrEmpty(vehicle)) RemitoXml.Put(truck, "dominioVehiculo", vehicle, TruckOrder);
            if (!string.IsNullOrEmpty(trailer)) RemitoXml.Put(truck, "dominioAcoplado", trailer, TruckOrder);
        }
    }

    /// <summary>The answers with only resultado and the errors.</summary>
    private static ContractAnswer Simple(ServiceCall call, string wrapper, RemitoProblem? problem) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            new XElement("resultado", problem is null ? "A" : "R"),
            problem is null ? null : RemitoXml.Errors([problem]))));

    /// <summary>RemitoReturnType: resultado first, then what ARCA assigned. Before issue, fechaVencimiento (required) is what it would get issued today.</summary>
    private ContractAnswer RemitoAnswer(ServiceCall call, string wrapper, Remito? remito, IReadOnlyCollection<RemitoProblem> problems) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(wrapper,
            new XElement("resultado", problems.Count == 0 ? "A" : "R"),
            remito is null ? null : new XElement("remitoDatosAutorizacion",
                new XElement("codigoRemito", remito.Code),
                remito.Number is { } number ? new XElement("nroComprobante", number) : null,
                new XElement("idTipoComprobante", remito.Type),
                remito.AuthorizationCode is { } cre ? new XElement("codigoAutorizacion", cre) : null,
                remito.IssuedAt is { } issued ? new XElement("fechaEmision", RemitoXml.Date(issued)) : null,
                new XElement("fechaVencimiento", RemitoXml.Date(remito.ExpiresOn ?? _ledger.Today.AddDays(RemitoTerms.ValidityDays(remito.DistanceKm)))),
                new XElement("estado", remito.State)),
            RemitoXml.Errors(problems))));

    /// <summary>RemitoOutputType: the remito as generated, with each item's quantities sent, received and lost, and what ARCA assigned.</summary>
    private static XElement Output(Remito remito)
    {
        var document = XElement.Parse(remito.Xml);
        var goods = (document.Element("arrayMercaderias")?.Elements("mercaderia") ?? []).Select(m =>
        {
            var order = (int)(m.ChildLong("orden") ?? 0);
            return new XElement("mercaderia",
                new XElement("orden", order),
                new XElement("anioZafra", m.ChildText("anioZafra")),
                new XElement("cantidadEnviada", m.ChildText("cantidad")),
                remito.Received.GetValueOrDefault(order) is > 0 and var received ? new XElement("cantidadRecibida", received) : null,
                remito.Lost.GetValueOrDefault(order) is > 0 and var lost ? new XElement("cantidadPerdida", lost) : null,
                new XElement("tipoProducto", m.ChildText("tipoProducto")),
                new XElement("unidadMedida", m.ChildText("unidadMedida")),
                new XElement("tipoEmbalaje", m.ChildText("tipoEmbalaje")));
        });
        return RemitoXml.Shape("remito", document, OutputOrder, new Dictionary<string, object?>
        {
            ["codRemito"] = remito.Code,
            ["idRequest"] = remito.RequestId,
            ["estado"] = remito.State,
            ["cuitEmisor"] = remito.Issuer,
            ["cuitProductorContrato"] = null,
            ["arrayMercaderias"] = new XElement("arrayMercaderias", goods),
            ["arrayContingencias"] = remito.Contingencies.Count == 0 ? null : new XElement("arrayContingencias", remito.Contingencies.Select(xml => XElement.Parse(xml))),
            ["datosAutorizacion"] = remito.Issued
                ? new XElement("datosAutorizacion",
                    new XElement("nroComprobante", remito.Number),
                    new XElement("idTipoComprobante", remito.Type),
                    new XElement("codigoAutorizacion", remito.AuthorizationCode),
                    new XElement("fechaEmision", RemitoXml.Date(remito.IssuedAt!.Value)),
                    new XElement("fechaVencimiento", RemitoXml.Date(remito.ExpiresOn!.Value)))
                : null,
        });
    }
}
