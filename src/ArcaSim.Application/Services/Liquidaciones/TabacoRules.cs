using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using static ArcaSim.Application.Services.Liquidaciones.SettlementXml;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// WSTABACO, the tobacco regime's "Gestión de Hebras" (docs/arca/servicios/wstabaco.md):
/// the life of a CATHE. A depositor asks for CATHE for a deposit (within the
/// fortnight its productive parameters allow, and with 80% of the previous ones
/// linked) or for an import dispatch; links them to the tobacco they identify
/// (holder and kilos), using up the kilos of the CATHE and CATA it was made
/// from; and retires them by an elaboration report (with rectifications) or by
/// denaturation. A change of holder without moving the tobacco waits for the
/// seller and the buyer as the manual's state machine says (PC, PV, AP). A
/// pending request locks its CATHE. Business errors go in errores with
/// resultado R, observations with resultado O.
/// The manual gives no texts for its codes: every description here is
/// ArcaSim's summary of the validation the manual lists. ArcaSim's choices on
/// what the manual leaves open: a CATHE is the year and a 10-digit sequence;
/// the fortnight is 15 times the daily units; a refused change of holder ends
/// RC (buyer) or RV (seller), and AP reads "Aprobada"; the CATHE a
/// rectification leaves out go back to stock; SEFI is simulated by approving
/// (A) every denaturation once the simulated clock reaches its date; a request
/// someone else filed, or one that does not exist, answers 2200 or 2300. The
/// tables answer the rows the manual shows, which it cuts with "..."; the
/// product and CATHE state tables keep the contract's answer (the manual does
/// not document the second, and the first's schema demands an event).
/// </summary>
public sealed class TabacoRules(IDocumentStore store, ITaxpayerRepository taxpayers, IClock clock, SequenceLocks locks) : IServiceBehavior
{
    // The resultado of an answer (§1.3): accepted, accepted with an observation, rejected.
    private const string Accepted = "A";
    private const string AcceptedWithNote = "O";
    private const string Rejected = "R";

    /// <summary>The manual's SiNo type: S.</summary>
    private const string Yes = "S";

    // What SEFI decides about a denaturation: pending, or the removal of all the CATHE asked (the others are not simulated).
    private const string Pending = "P";
    private const string Approved = "A";

    // A change of holder waits for the buyer (PC) or the seller (PV) and ends approved (AP) or refused by one of them (RC, RV).
    private const string WaitingBuyer = "PC";
    private const string WaitingSeller = "PV";
    private const string ChangeApproved = "AP";
    private const string RefusedByBuyer = "RC";
    private const string RefusedBySeller = "RV";

    private static readonly string[] Waiting = [WaitingBuyer, WaitingSeller];
    private static readonly string[] Refused = [RefusedByBuyer, RefusedBySeller];

    /// <summary>A request for CATHE is capped at the units a fortnight of production needs: 15 days of the daily units the deposit declared.</summary>
    private const int FortnightDays = 15;

    /// <summary>Of the CATHE a deposit asked for before, at least this percent must be linked before it asks again (1202).</summary>
    private const int MinLinkedPercent = 80;

    /// <summary>A denaturation date less than this many days away is accepted with the observation 1701.</summary>
    private const int DenaturationLeadDays = 15;

    private readonly TabacoBook _book = new(store);

    public string Service => "wstabaco";

    private DateOnly Today => clock.Today();

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        switch (call.Name)
        {
            case "consultarTiposComprobante":
                return Table(call, "arrayTiposComprobante", ("1", "Factura A"), ("6", "Factura B"));
            case "consultarTiposMercaderia":
                return Table(call, "arrayTiposMercaderia", ("1", "Tabaco en Hebras"), ("2", "Tabaco reconstituido"));
            case "consultarTiposEstadoSolicitudCambioTitular":
                return Table(call, "arrayTiposEstados", (WaitingBuyer, "Pendiente aprobación del Comprador"), (WaitingSeller, "Pendiente aprobación del Vendedor"),
                    (ChangeApproved, "Aprobada"), (RefusedByBuyer, "Rechazada por el Comprador"), (RefusedBySeller, "Rechazada por el Vendedor"));
            case "consultarTiposResultadoDesnaturalizacion":
                return Table(call, "arrayTiposResultado", (Pending, "Pendiente de procesar en SEFI"),
                    (Approved, "Procedente la baja de la totalidad de los CATHE solicitados"),
                    ("B", "No procedente la baja de la totalidad de los CATHE solicitados"),
                    ("C", "Procedente la baja de los CATHE que se detallan"));
        }
        if (call.Name is "dummy" or "consultarTiposProductoElaborado" or "consultarTiposEstadoCathe") return null;

        if (await taxpayers.FindAsync(call.Cuit, ct) is { Active: false })
            return Reject(call, (101, "La CUIT representada debe encontrarse activa y sin limitaciones."));

        // One operation at a time on the whole book: every one reads CATHE, CATA and requests, decides from
        // their state and writes them, and a request that locks a CATHE does it by reading it free first.
        using var gate = await locks.AcquireAsync($"{Service}.book", 0, 0, 0, ct);
        await SimulateSefiAsync(call.Cuit, ct);

        return call.Name switch
        {
            "informarParametrosProductivos" => await InformParametersAsync(call, ct),
            "consultarParametrosProductivos" => await ParametersAsync(call, ct),
            "informarExistenciaInicialCata" => await InformCatasAsync(call, ct),
            "consultarExistenciaInicialCata" => await CatasAsync(call, ct),
            "solicitarCathesTabacoElaborado" => await RequestElaboratedAsync(call, ct),
            "solicitarCathesTabacoImportado" => await RequestImportedAsync(call, ct),
            "consultarCathesSolicitados" => await ListCathesAsync(call, linked: false, ct),
            "consultarCathesVinculados" => await ListCathesAsync(call, linked: true, ct),
            "vincularCathesTabacoElaborado" => await LinkElaboratedAsync(call, ct),
            "vincularCathesTabacoImportado" => await LinkImportedAsync(call, ct),
            "informarElaboracionProductos" => await InformElaborationAsync(call, ct),
            "consultarElaboracionProductos" => await ElaborationAsync(call, ct),
            "solicitarDesnaturalizacion" => await DenatureAsync(call, ct),
            "consultarSolicitudDesnaturalizacion" => await DenaturationAsync(call, ct),
            "consultarSolicDesnatPendientes" => await DenaturationsAsync(call, processed: false, ct),
            "consultarSolicDesnatProcesadas" => await DenaturationsAsync(call, processed: true, ct),
            "solicitarCambioTitularSinMovFisico" => await RequestChangeAsync(call, ct),
            "confirmarCambioTitularSinMovFisico" => await ConfirmChangeAsync(call, ct),
            "consultarSolicitudCambioTitular" => await ChangeAsync(call, ct),
            "consultarSolicCambioTitularPendientes" => await ChangesAsync(call, Waiting, ct),
            "consultarSolicCambioTitularAprobadas" => await ChangesAsync(call, [ChangeApproved], ct),
            "consultarSolicCambioTitularRechazadas" => await ChangesAsync(call, Refused, ct),
            _ => null,
        };
    }

    // ---- Productive parameters and initial CATA --------------------------------------

    private async Task<ContractAnswer> InformParametersAsync(ServiceCall call, CancellationToken ct)
    {
        var rows = call.Request.Child("arrayParametros").Children("parametros").ToList();
        var create = call.Request.Value("operacion") != "M";
        if (rows.Select(r => r.Number("deposito")).Distinct().Count() != rows.Count)
            return Reject(call, (1000, "No se puede informar más de una vez el mismo depósito."));
        foreach (var row in rows)
        {
            var existing = await _book.ParametersAsync(call.Cuit, row.Number("deposito"), ct);
            if (create && existing is not null)
                return Reject(call, (1001, $"El depósito {row.Number("deposito")} ya tiene parámetros productivos informados: debe usar la operación M."));
            if (!create && existing is null)
                return Reject(call, (1002, $"El depósito {row.Number("deposito")} no tiene parámetros productivos informados: debe usar la operación A."));
        }
        foreach (var row in rows)
            await _book.PutAsync(call.Cuit, new ProductionParameters(row.Number("deposito"), row.Amount("kilos"), row.Number("cantidad")), ct);
        return Accept(call);
    }

    private async Task<ContractAnswer> ParametersAsync(ServiceCall call, CancellationToken ct)
    {
        var rows = await _book.AllParametersAsync(call.Cuit, ct);
        return Reply(call, rows.Count == 0 ? null : new XElement("arrayParametros", rows.Select(r => new XElement("parametros",
            new XElement("deposito", r.Deposit), new XElement("kilos", Kilos(r.Kilos)), new XElement("cantidad", r.Quantity)))));
    }

    private async Task<ContractAnswer> InformCatasAsync(ServiceCall call, CancellationToken ct)
    {
        var rows = call.Request.Child("arrayExistenciasCata").Children("datosCata").ToList();
        if (rows.Select(r => r.Number("cata")).Distinct().Count() != rows.Count) return Reject(call, (1100, "No se puede informar más de una vez el mismo CATA."));
        foreach (var row in rows)
            if (await _book.CataAsync(call.Cuit, row.Number("cata"), ct) is not null)
                return Reject(call, (1103, $"El CATA {row.Number("cata")} ya fue informado como existencia inicial."));
        foreach (var row in rows)
            await _book.PutAsync(new Cata(row.Number("cata"), call.Cuit, row.Number("deposito"), row.Amount("kilos"), row.Amount("kilos")), ct);
        return Reply(call, new XElement("resultado", Accepted));
    }

    private async Task<ContractAnswer> CatasAsync(ServiceCall call, CancellationToken ct)
    {
        var deposit = call.Request.Number("deposito");
        var catas = (await _book.CatasAsync(call.Cuit, ct)).Where(c => c.Deposit == deposit && c.Available > 0).ToList();
        return Reply(call, catas.Count == 0 ? null : new XElement("arrayExistenciasCata", catas.Select(c => new XElement("datosCata",
            new XElement("cata", c.Code), new XElement("deposito", c.Deposit), new XElement("kilos", Kilos(c.Available))))));
    }

    // ---- Requesting CATHE ------------------------------------------------------------

    /// <summary>
    /// The most CATHE one request can ask for: CantidadSimpleType in the WSDL (cantidad, cantBultos), 1 to
    /// 999999. ARCA checks a request against its schema and refuses what does not fit with a fault (manual
    /// 1.3.1, "errores de tipos de datos"). ArcaSim refuses only what is above the maximum, so that no
    /// request can make it issue CATHE without end, and it does so before any business rule.
    /// </summary>
    private const long MaxQuantity = 999_999;

    /// <summary>The fault for a request that asks for more than <see cref="MaxQuantity"/>, in the wording of a schema range error.</summary>
    private static ContractAnswer? TooMany(ServiceCall call, long quantity) => quantity <= MaxQuantity
        ? null
        : call.Fault($"cvc-maxInclusive-valid: El valor '{quantity}' no cumple con la restricción maxInclusive '{MaxQuantity}' para el tipo 'CantidadSimpleType'.");

    /// <summary>
    /// A quantity read from a request, held to one past the maximum in either direction: whether it passes
    /// the maximum is all that matters, and the sums that follow cannot overflow.
    /// </summary>
    private static long Bounded(long quantity) => Math.Clamp(quantity, -MaxQuantity - 1, MaxQuantity + 1);

    private async Task<ContractAnswer> RequestElaboratedAsync(ServiceCall call, CancellationToken ct)
    {
        var deposit = call.Request.Number("deposito");
        var quantity = call.Request.Number("cantidad");
        if (TooMany(call, quantity) is { } tooMany) return tooMany;
        if (call.Request.Value("inicial") != Yes)
        {
            if (await _book.ParametersAsync(call.Cuit, deposit, ct) is not { } parameters)
                return Reject(call, (1200, "El depósito debe tener informados sus parámetros productivos."));
            if (quantity > parameters.Quantity * FortnightDays)
                return Reject(call, (1201, $"La cantidad solicitada supera la necesaria para una producción quincenal ({parameters.Quantity * FortnightDays} CATHE)."));
            var previous = (await _book.CathesAsync(call.Cuit, ct)).Where(c => c.Deposit == deposit && c.Dispatch is null).ToList();
            if (previous.Count > 0 && previous.Count(c => c.State != Cathe.Requested) * 100 < previous.Count * MinLinkedPercent)
                return Reject(call, (1202, "Deben estar vinculados al menos el 80 % de los CATHE solicitados anteriormente para el depósito."));
        }
        var codes = new List<long>();
        for (var i = 0; i < quantity; i++)
        {
            var cathe = new Cathe(await _book.NewCatheAsync(Today.Year, ct), call.Cuit, deposit, null, Today, Cathe.Requested);
            await _book.PutAsync(cathe, ct);
            codes.Add(cathe.Code);
        }
        return Accept(call, new XElement("arrayCathes", codes.Select(c => new XElement("cathe", c))));
    }

    private async Task<ContractAnswer> RequestImportedAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var rows = request.Child("arrayCantSolicitadas").Children("cantPorDeposito")
            .Select(r => (Deposit: r.Number("deposito"), Quantity: Bounded(r.Number("cantidad")))).ToList();
        // What the loop below issues: a negative row asks for nothing, though it lowers the sum rule 1302 compares.
        var asked = rows.Where(r => r.Quantity > 0).Sum(r => r.Quantity);
        if (TooMany(call, Math.Max(request.Number("cantBultos"), asked)) is { } tooMany) return tooMany;
        if (!Cuits.IsValid(request.Number("cuitDespachante"))) return Reject(call, (1300, "La CUIT del despachante no es válida."));
        if (rows.Select(r => r.Deposit).Distinct().Count() != rows.Count) return Reject(call, (1301, "No se puede informar más de una vez el mismo depósito."));
        if (rows.Sum(r => r.Quantity) > request.Number("cantBultos"))
            return Reject(call, (1302, "La suma de las cantidades solicitadas no puede superar la cantidad de bultos del despacho."));
        var dispatch = request.Value("nroDespachoImp");
        var issued = new List<Cathe>();
        foreach (var (deposit, quantity) in rows)
            for (var i = 0; i < quantity; i++)
            {
                var cathe = new Cathe(await _book.NewCatheAsync(Today.Year, ct), call.Cuit, deposit, dispatch, Today, Cathe.Requested);
                await _book.PutAsync(cathe, ct);
                issued.Add(cathe);
            }
        return Accept(call, new XElement("arrayCathes", issued.Select(c => new XElement("datosCathes",
            new XElement("cathe", c.Code), new XElement("deposito", c.Deposit)))));
    }

    private async Task<ContractAnswer> ListCathesAsync(ServiceCall call, bool linked, CancellationToken ct)
    {
        var request = call.Request;
        var deposit = request.Child("deposito") is null ? (long?)null : request.Number("deposito");
        var dispatch = request.Value("nroDespachoImp");
        var from = request.Day("fechaDesde") ?? DateOnly.MinValue;
        var to = request.Day("fechaHasta") ?? DateOnly.MaxValue;
        var cathes = (await _book.CathesAsync(call.Cuit, ct))
            .Where(c => linked ? c.InStock : c.State == Cathe.Requested)
            .Where(c => deposit is null || c.Deposit == deposit)
            .Where(c => dispatch is null || c.Dispatch == dispatch)
            .Where(c => (linked ? c.LinkedOn ?? c.RequestedOn : c.RequestedOn) is var day && day >= from && day <= to)
            .ToList();
        return Accept(call, cathes.Count == 0 ? null : new XElement("arrayCathes", cathes.Select(c => new XElement("datosCathe",
            new XElement("cathe", c.Code), new XElement("deposito", c.Deposit), Maybe("nroDespachoImp", c.Dispatch)))));
    }

    // ---- Linking ---------------------------------------------------------------------

    private async Task<ContractAnswer> LinkElaboratedAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var recovery = request.Value("recupero") == Yes;
        var goods = (int)request.Number("tipoMercaderia");
        var made = request.Child("arrayCathesElaborados").Children("datosTabacoElaborado").ToList();
        var used = request.Child("arrayCathesUsados").Children("datosCatheUsado").ToList();
        var catas = request.Child("arrayCatasUsados").Children("datosCataUsado").ToList();

        if (goods is 1 or 2 && request.Value("nroOrdenProduccion") is null)
            return Reject(call, (1401, "El número de orden de producción es obligatorio para los tipos de mercadería 1 y 2."));
        if (!recovery && used.Count == 0 && catas.Count == 0)
            return Reject(call, (1420, "Debe informar los CATHE o los CATA utilizados para la elaboración."));

        var linked = new List<Cathe>();
        foreach (var row in made)
        {
            var code = row.Number("cathe");
            if (await _book.CatheAsync(call.Cuit, code, ct) is not { State: Cathe.Requested, Dispatch: null } cathe)
                return Reject(call, (1402, $"El CATHE {code} no es válido, no fue solicitado o ya fue vinculado."));
            if (linked.Any(c => c.Code == code)) return Reject(call, (1403, $"El CATHE {code} está repetido."));
            if (linked.Count > 0 && linked[0].Deposit != cathe.Deposit) return Reject(call, (1404, "Todos los CATHE elaborados deben ser del mismo depósito."));
            if (await HolderProblemAsync(call, row.Number("cuitTitular"), ct) is { } holder) return holder;
            linked.Add(cathe with
            {
                State = Cathe.Linked, Holder = row.Number("cuitTitular"), GrossKilos = row.Amount("kilosBrutos"), NetKilos = row.Amount("kilosNetos"),
                Available = row.Amount("kilosNetos"), Goods = goods, LinkedOn = Today,
            });
        }

        var consumed = new List<Cathe>();
        foreach (var row in used)
        {
            var code = row.Number("cathe");
            if (recovery || consumed.Any(c => c.Code == code))
                return Reject(call, (1406, $"El CATHE utilizado {code} no puede informarse en un recupero ni repetirse."));
            if (await _book.CatheAsync(call.Cuit, code, ct) is not { } source) return Reject(call, (1407, $"El CATHE utilizado {code} no existe."));
            if (!source.InStock) return Reject(call, (1408, $"El CATHE utilizado {code} no se encuentra VINCULADO."));
            if (linked.Count > 0 && source.Deposit != linked[0].Deposit)
                return Reject(call, (1409, $"El CATHE utilizado {code} no se encuentra en el depósito de los CATHE elaborados."));
            if (row.Amount("kilosNetos") > source.Available)
                return Reject(call, (1410, $"Los kilos utilizados del CATHE {code} superan sus kilos disponibles ({Kilos(source.Available)})."));
            var left = source.Available - row.Amount("kilosNetos");
            consumed.Add(source with { Available = left, State = left == 0 ? Cathe.Retired : source.State, Retirement = left == 0 ? "utilizado" : null });
        }

        var spent = new List<Cata>();
        foreach (var row in catas)
        {
            var code = row.Number("cata");
            if (recovery || spent.Any(c => c.Code == code))
                return Reject(call, (1411, $"El CATA utilizado {code} no puede informarse en un recupero ni repetirse."));
            if (await _book.CataAsync(call.Cuit, code, ct) is not { } cata || cata.Available <= 0)
                return Reject(call, (1412, $"El CATA utilizado {code} no existe o no tiene kilos disponibles."));
            if (linked.Count > 0 && cata.Deposit != linked[0].Deposit)
                return Reject(call, (1414, $"El CATA utilizado {code} no se encuentra en el depósito de los CATHE elaborados."));
            if (row.Amount("kilosNetos") > cata.Available)
                return Reject(call, (1415, $"Los kilos utilizados del CATA {code} superan sus kilos disponibles ({Kilos(cata.Available)})."));
            spent.Add(cata with { Available = cata.Available - row.Amount("kilosNetos") });
        }

        foreach (var cathe in linked.Concat(consumed)) await _book.PutAsync(cathe, ct);
        foreach (var cata in spent) await _book.PutAsync(cata, ct);
        return Accept(call);
    }

    private async Task<ContractAnswer> LinkImportedAsync(ServiceCall call, CancellationToken ct)
    {
        var linked = new List<Cathe>();
        foreach (var row in call.Request.Child("arrayCathesImp").Children("datosTabacoImportado"))
        {
            var code = row.Number("cathe");
            if (linked.Any(c => c.Code == code)) return Reject(call, (1501, $"El CATHE {code} está repetido."));
            if (await _book.CatheAsync(call.Cuit, code, ct) is not { State: Cathe.Requested, Dispatch: not null } cathe)
                return Reject(call, (1502, $"El CATHE {code} no fue solicitado para un despacho de importación o ya fue vinculado."));
            if (await HolderProblemAsync(call, row.Number("cuitTitular"), ct) is { } holder) return holder;
            linked.Add(cathe with
            {
                State = Cathe.Linked, Holder = row.Number("cuitTitular"), GrossKilos = row.Amount("kilosBrutos"), NetKilos = row.Amount("kilosNetos"),
                Available = row.Amount("kilosNetos"), Goods = (int)row.Number("tipoMercaderia"), LinkedOn = Today,
            });
        }
        foreach (var cathe in linked) await _book.PutAsync(cathe, ct);
        return Accept(call);
    }

    private async Task<ContractAnswer?> HolderProblemAsync(ServiceCall call, long holder, CancellationToken ct) =>
        await taxpayers.FindAsync(holder, ct) is { Active: false }
            ? Reject(call, (301, $"La CUIT titular {holder} debe encontrarse activa y sin limitaciones."))
            : null;

    // ---- Elaboration -----------------------------------------------------------------

    private async Task<ContractAnswer> InformElaborationAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var deposit = request.Number("deposito");
        var date = request.Day("fechaElaboracion") ?? Today;
        var rectification = (int)request.Number("rectificativa");
        var rows = request.Child("arrayCathes").Children("datosCatheProducto").ToList();

        if (date > Today) return Reject(call, (1600, "La fecha de elaboración no puede ser posterior a la fecha actual."));
        var previous = await _book.ReportAsync(call.Cuit, deposit, date, ct);
        if (rectification == 0 && previous is not null)
            return Reject(call, (1601, "Ya existe un informe de elaboración para el depósito y la fecha: debe enviar una rectificativa."));
        if (rectification > 0 && previous is null)
            return Reject(call, (1602, "No existe un informe de elaboración previo para el depósito y la fecha a rectificar."));
        if (previous is not null && rectification != previous.Rectification + 1)
            return Reject(call, (1603, $"La rectificativa debe ser la siguiente a la última informada ({previous.Rectification + 1})."));

        var before = previous?.Lines.Select(l => l.Cathe).ToHashSet() ?? [];
        var retired = new List<Cathe>();
        foreach (var row in rows)
        {
            var code = row.Number("cathe");
            if (retired.Any(c => c.Code == code)) return Reject(call, (1604, $"El CATHE {code} está repetido."));
            var cathe = await _book.CatheAsync(call.Cuit, code, ct);
            if (cathe is null || !cathe.InStock && !before.Contains(code)) return Reject(call, (1605, $"El CATHE {code} no se encuentra VINCULADO."));
            if (cathe.Deposit != deposit) return Reject(call, (1606, $"El CATHE {code} no se encuentra en el depósito indicado."));
            if (row.Number("tipoProducto") == 4 && row.Value("descripcionOtroProducto") is null)
                return Reject(call, (1608, "Para el tipo de producto 4 (Otro) debe informar la descripción del producto."));
            retired.Add(cathe with { State = Cathe.Retired, Retirement = "elaboracion" });
        }

        foreach (var code in before.Where(c => retired.All(r => r.Code != c)))
            if (await _book.CatheAsync(call.Cuit, code, ct) is { } back)
                await _book.PutAsync(back with { State = Cathe.Linked, Retirement = null }, ct);
        foreach (var cathe in retired) await _book.PutAsync(cathe, ct);
        await _book.PutAsync(call.Cuit, new ElaborationReport(deposit, date, rectification, Today,
            rows.Select(r => new ProductLine(r.Number("cathe"), (int)r.Number("tipoProducto"), r.Value("descripcionOtroProducto"))).ToList()), ct);
        return Accept(call);
    }

    private async Task<ContractAnswer> ElaborationAsync(ServiceCall call, CancellationToken ct)
    {
        var deposit = call.Request.Number("deposito");
        var date = call.Request.Day("fecha") ?? Today;
        if (await _book.ReportAsync(call.Cuit, deposit, date, ct) is not { } report) return Accept(call);
        return Accept(call,
            new XElement("deposito", report.Deposit),
            new XElement("fechaElaboracion", Iso(report.Date)),
            new XElement("fechaInformacion", Iso(report.InformedOn)),
            new XElement("arrayCathes", report.Lines.Select(l => new XElement("datosCatheProducto",
                new XElement("cathe", l.Cathe), new XElement("tipoProducto", l.Product), Maybe("descripcionOtroProducto", l.OtherProduct)))));
    }

    // ---- Denaturation ----------------------------------------------------------------

    /// <summary>
    /// The CATHE codes of a request's arrayCathes. A code that is not a number is deliberately not refused here:
    /// the schema says xsd:long, so the request is not one ARCA would have unmarshalled, and the exception is
    /// answered by the engine as the fault the service gives for what its rules did not foresee
    /// (UnexpectedErrorTests pins it).
    /// </summary>
    private static List<long> CatheCodes(XElement request) =>
        request.Child("arrayCathes").Children("cathe").Select(c => long.Parse(c.Value, CultureInfo.InvariantCulture)).ToList();

    private async Task<ContractAnswer> DenatureAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var deposit = request.Number("deposito");
        var date = request.Day("fecha") ?? Today;
        var codes = CatheCodes(request);

        if (date <= Today) return Reject(call, (1700, "La fecha de desnaturalización debe ser posterior a la fecha actual."));
        var cathes = new List<Cathe>();
        foreach (var code in codes)
        {
            if (cathes.Any(c => c.Code == code)) return Reject(call, (1702, $"El CATHE {code} está repetido."));
            if (await _book.CatheAsync(call.Cuit, code, ct) is not { InStock: true } cathe)
                return Reject(call, (1703, $"El CATHE {code} no es válido o no se encuentra VINCULADO."));
            if (cathe.Deposit != deposit) return Reject(call, (1704, $"El CATHE {code} no se encuentra en el depósito indicado."));
            cathes.Add(cathe);
        }

        var id = await _book.NewRequestIdAsync(ct);
        await _book.PutAsync(new Denaturation(id, call.Cuit, deposit, date, request.Value("motivo") ?? "",
            codes.ToDictionary(c => c.ToString(CultureInfo.InvariantCulture), _ => Pending), Pending, null), ct);
        foreach (var cathe in cathes) await _book.PutAsync(cathe with { Lock = $"desnat/{id}" }, ct);
        var early = date.DayNumber - Today.DayNumber < DenaturationLeadDays;
        return Respond(call, early ? AcceptedWithNote : Accepted, new XElement("idSolicitud", id),
            early ? Codes("observaciones", (1701, $"La fecha de desnaturalización debería ser al menos {DenaturationLeadDays} días corridos posterior a la fecha actual.")) : null);
    }

    private async Task<ContractAnswer> DenaturationAsync(ServiceCall call, CancellationToken ct)
    {
        if (await _book.DenaturationAsync(call.Request.Number("idSolicitud"), ct) is not { } request || request.Owner != call.Cuit)
            return Reject(call, (2300, "La CUIT representada no presentó la solicitud de desnaturalización consultada."));
        return call.Ok(new XElement(call.Operation.Output,
            new XElement("idSolicitud", request.Id),
            new XElement("deposito", request.Deposit),
            new XElement("fechaDesnat", Iso(request.Date)),
            new XElement("motivo", request.Reason),
            new XElement("arrayCathesDesnat", request.Cathes.Select(c => new XElement("estadoCathe", new XElement("cathe", c.Key), new XElement("estado", c.Value)))),
            new XElement("resultadoSolicitud", request.Result),
            Maybe("fechaResultado", request.ResolvedOn)));
    }

    private async Task<ContractAnswer> DenaturationsAsync(ServiceCall call, bool processed, CancellationToken ct)
    {
        if (Range(call) is not { } range) return Reject(call, (2100, "La fecha desde debe ser anterior o igual a la fecha hasta."));
        var (from, to) = range;
        var found = (await _book.DenaturationsAsync(ct))
            .Where(d => d.Owner == call.Cuit && (d.Result != Pending) == processed && d.Date >= from && d.Date <= to).ToList();
        if (found.Count == 0) return Respond(call, AcceptedWithNote, Codes("observaciones", (2101, "No se encontraron solicitudes para los parámetros informados.")));
        return Respond(call, Accepted, new XElement("arraySolicitudes", found.Select(d => new XElement("datosSolicitud",
            new XElement("idSolicitud", d.Id), new XElement("deposito", d.Deposit), new XElement("fechaDesnat", Iso(d.Date)), new XElement("motivo", d.Reason),
            processed ? new XElement("resultado", d.Result) : null,
            processed ? new XElement("fechaResultado", Iso(d.ResolvedOn ?? d.Date)) : null))));
    }

    /// <summary>
    /// SEFI checks the tobacco outside the web service; ArcaSim approves a request (A) once its date arrives.
    /// It does it for the CUIT that calls, when it calls, which is when that CUIT can first see the difference:
    /// nobody reads another's requests or CATHE.
    /// </summary>
    private async Task SimulateSefiAsync(long owner, CancellationToken ct)
    {
        foreach (var request in (await _book.DenaturationsAsync(ct)).Where(d => d.Owner == owner && d.Result == Pending && d.Date <= Today))
        {
            foreach (var code in request.Cathes.Keys)
                if (await _book.CatheAsync(request.Owner, long.Parse(code, CultureInfo.InvariantCulture), ct) is { } cathe)
                    await _book.PutAsync(cathe with { State = Cathe.Retired, Lock = null, Retirement = "desnaturalizacion" }, ct);
            await _book.PutAsync(request with { Result = Approved, ResolvedOn = request.Date, Cathes = request.Cathes.ToDictionary(c => c.Key, _ => Yes) }, ct);
        }
    }

    // ---- Change of holder ------------------------------------------------------------

    private async Task<ContractAnswer> RequestChangeAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var seller = request.Number("cuitVendedor");
        var buyer = request.Number("cuitComprador");
        var codes = CatheCodes(request);

        if (await taxpayers.FindAsync(buyer, ct) is { Active: false }) return Reject(call, (105, $"La CUIT compradora {buyer} registra inconsistencias."));
        var cathes = new List<Cathe>();
        foreach (var code in codes)
        {
            if (cathes.Any(c => c.Code == code)) return Reject(call, (1801, $"El CATHE {code} está repetido."));
            if (await _book.CatheAsync(call.Cuit, code, ct) is not { } cathe)
                return Reject(call, (201, $"El CATHE {code} no se encuentra en un depósito de la CUIT solicitante."));
            if (!cathe.InStock) return Reject(call, (1802, $"El CATHE {code} no se encuentra VINCULADO."));
            if (cathe.Holder != seller) return Reject(call, (1803, $"El vendedor no es el titular actual del CATHE {code}."));
            cathes.Add(cathe);
        }

        var id = await _book.NewRequestIdAsync(ct);
        var state = call.Cuit == seller ? WaitingBuyer : WaitingSeller;
        await _book.PutAsync(new TitleChange(id, call.Cuit, seller, buyer, request.Day("fechaCambio") ?? Today, request.Value("modoFactura") ?? "",
            request.Value("tipoComprobante") ?? "", request.Number("puntoVenta"), request.Number("numeroComprobante"),
            request.Amount("importeNetoGravado"), request.Amount("importeTotal"), codes, state), ct);
        foreach (var cathe in cathes) await _book.PutAsync(cathe with { Lock = $"cambio/{id}" }, ct);
        return Respond(call, Accepted, new XElement("idSolicitud", id), new XElement("estado", state));
    }

    private async Task<ContractAnswer> ConfirmChangeAsync(ServiceCall call, CancellationToken ct)
    {
        if (await _book.TitleChangeAsync(call.Request.Number("idSolicitud"), ct) is not { } change)
            return Reject(call, (1900, "La solicitud de cambio de titular no existe."));
        if (!Waiting.Contains(change.State)) return Reject(call, (1901, "La solicitud de cambio de titular no está pendiente de aprobación."));
        var buyerTurn = change.State == WaitingBuyer;
        if (call.Cuit != (buyerTurn ? change.Buyer : change.Seller))
            return Reject(call, (1902, $"La solicitud debe ser confirmada por el {(buyerTurn ? "comprador" : "vendedor")}."));

        var state = call.Request.Value("confirma") != Yes
            ? (buyerTurn ? RefusedByBuyer : RefusedBySeller)
            : (buyerTurn || change.Informant == change.Buyer ? ChangeApproved : WaitingBuyer);
        await _book.PutAsync(change with { State = state }, ct);
        if (state != WaitingBuyer)
            foreach (var code in change.Cathes)
                if (await _book.CatheAsync(change.Informant, code, ct) is { } cathe)
                    await _book.PutAsync(cathe with { Lock = null, Holder = state == ChangeApproved ? change.Buyer : cathe.Holder }, ct);
        return Accept(call);
    }

    private async Task<ContractAnswer> ChangeAsync(ServiceCall call, CancellationToken ct)
    {
        if (await _book.TitleChangeAsync(call.Request.Number("idSolicitud"), ct) is not { } change || !Takes(change, call.Cuit))
            return Reject(call, (2200, "La CUIT representada no participa de la solicitud de cambio de titular consultada."));
        return call.Ok(new XElement(call.Operation.Output,
            new XElement("idSolicitud", change.Id),
            new XElement("cuitInformante", change.Informant), new XElement("cuitVendedor", change.Seller), new XElement("cuitComprador", change.Buyer),
            new XElement("fechaCambio", Iso(change.Date)), new XElement("modoFactura", change.InvoiceMode), new XElement("tipoComprobante", change.VoucherType),
            new XElement("puntoVenta", change.PointOfSale), new XElement("numeroComprobante", change.Number),
            new XElement("importeNetoGravado", Money(change.NetAmount)), new XElement("importeTotal", Money(change.Total)),
            new XElement("arrayCathes", change.Cathes.Select(c => new XElement("cathe", c))),
            new XElement("estado", change.State)));
    }

    private async Task<ContractAnswer> ChangesAsync(ServiceCall call, string[] states, CancellationToken ct)
    {
        if (Range(call) is not { } range) return Reject(call, (2000, "La fecha desde debe ser anterior o igual a la fecha hasta."));
        var (from, to) = range;
        var found = (await _book.TitleChangesAsync(ct))
            .Where(c => Takes(c, call.Cuit) && states.Contains(c.State) && c.Date >= from && c.Date <= to).ToList();
        if (found.Count == 0) return Respond(call, AcceptedWithNote, Codes("observaciones", (2001, "No se encontraron solicitudes para los parámetros informados.")));
        return Respond(call, Accepted, new XElement("arraySolicitudes", found.Select(c => new XElement("datosSolicitud",
            new XElement("idSolicitud", c.Id), new XElement("cuitInformante", c.Informant), new XElement("cuitComprador", c.Buyer),
            new XElement("cuitVendedor", c.Seller), new XElement("fechaCambio", Iso(c.Date)), new XElement("tipoComprobante", c.VoucherType),
            new XElement("puntoVenta", c.PointOfSale), new XElement("numeroComprobante", c.Number), new XElement("estado", c.State)))));
    }

    private static bool Takes(TitleChange change, long cuit) => change.Informant == cuit || change.Seller == cuit || change.Buyer == cuit;

    // ---- Answers ---------------------------------------------------------------------

    private static (DateOnly From, DateOnly To)? Range(ServiceCall call)
    {
        var from = call.Request.Day("fechaDesde") ?? DateOnly.MinValue;
        var to = call.Request.Day("fechaHasta") ?? DateOnly.MaxValue;
        return from <= to ? (from, to) : null;
    }

    private static string Kilos(decimal kilos) => kilos.ToString("0.00", CultureInfo.InvariantCulture);

    private static XElement Codes(string name, params (int Code, string Text)[] entries) => new(name, entries.Select(e => new XElement("codigoDescripcion",
        new XElement("codigo", e.Code.ToString(CultureInfo.InvariantCulture)), new XElement("descripcion", e.Text))));

    private static ContractAnswer Table(ServiceCall call, string array, params (string Code, string Text)[] rows) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(array, rows.Select(r => new XElement("codigoDescripcion",
            new XElement("codigo", r.Code), new XElement("descripcion", r.Text))))));

    /// <summary>An answer with resultado first and the given content after it, in schema order.</summary>
    private static ContractAnswer Respond(ServiceCall call, string result, params object?[] content) =>
        call.Ok(new XElement(call.Operation.Output, new XElement("resultado", result), content));

    private static ContractAnswer Accept(ServiceCall call, params object?[] content) => Respond(call, Accepted, content);

    /// <summary>The answers without resultado (parameters, initial CATA).</summary>
    private static ContractAnswer Reply(ServiceCall call, params object?[] content) => call.Ok(new XElement(call.Operation.Output, content));

    /// <summary>§1.3: resultado R and the errors in errores/codigoDescripcion; an answer without resultado keeps only its required fields.</summary>
    private static ContractAnswer Reject(ServiceCall call, (int Code, string Text) error)
    {
        var answer = call.Error(error.Code, error.Text);
        if (answer.Body is not { } body) return answer;
        body.Element("resultado")?.SetValue(Rejected);
        body.Element("idSolicitud")?.SetValue(call.Request.Number("idSolicitud"));
        body.Element("errores")?.ReplaceWith(Codes("errores", error));
        return answer;
    }
}
