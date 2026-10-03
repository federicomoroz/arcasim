using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.FacturacionE;

/// <summary>
/// Constatación de comprobantes (docs/arca/servicios/wscdc.md): ComprobanteConstatar
/// checks a voucher against what ArcaSim really issued, WSFEv1's vouchers
/// (IVoucherStore) and every other service's (AuthorizedVouchers), and answers
/// A, or R with all the format causes in Errors and all the functional ones in
/// Observaciones. The parameter queries answer the tables the checks use.
///
/// ArcaSim's choices where the spec says NO VERIFICADO:
/// - every functional check runs and all failures are listed, not only the first;
/// - Errors and Observaciones use the manual's descriptions as texts, except 2, 108
///   and 200, whose real messages the manual's examples show;
/// - the voucher is looked up by its key and its code; a code registered for another
///   voucher yields 102 to 105 for each field that differs; a code nobody has, 100;
/// - a CAEA WSFEv1 granted whose voucher was not reported is A with observation 200
///   until the CAEA's report deadline, R with 106 after it;
/// - export vouchers (19, 20, 21, from wsfexv1) can be constatados; their receiver is
///   the country CUIT, as document type 80;
/// - no CAI is ever registered in ArcaSim, so mode CAI is always 100;
/// - ComprobantesTipoConsultar serves WSFEv1's types plus E (19-21) and T (195-197);
///   DocumentosTipoConsultar, WSFEv1's document types; ComprobantesModalidadConsultar,
///   CAE, CAEA and CAI with descriptions of ArcaSim's; OpcionalesTipoConsultar, none
///   ("reservado para usos futuros"), so any Opcional gets 151;
/// - Events carries Evt 0 in every answer, as seen live with a refused ticket.
/// </summary>
public sealed class WscdcRules(
    IClock clock, SimulationSettings settings, ParameterTables tables, IVoucherStore wsfeVouchers, ICaeaStore caeas,
    IDocumentStore documents, ITaxpayerRepository taxpayers) : IServiceBehavior
{
    public string Service => "wscdc";

    private static readonly IReadOnlyDictionary<int, string> Texts = new Dictionary<int, string>
    {
        [1] = "El modo indicado debe ser alfanumérico de 4 caracteres como máximo y debe ser alguno de los devueltos por el método ComprobantesModalidadConsultar()",
        [2] = "El campo CuitEmisor es invalido.",
        [3] = "Campo <PtoVta> debe ser numérico de 5 dígitos como máximo y debe estar comprendido entre 1 y 99998.",
        [4] = "El tipo de comprobante debe ser numérico de 3 dígitos como máximo y debe ser alguno de los definidos en el método ComprobantesTipoConsultar()",
        [5] = "Campo correspondiente al N° de comprobante, debe ser numérico de 8 dígitos como máximo y se debe encontrar entre 1 y 99999999.",
        [6] = "Campo correspondiente a la fecha del comprobante, debe tener el siguiente formato yyyymmdd",
        [7] = "Campo correspondiente al importe total del comprobante. Debe ser numérico mayor o igual a 0 de 13 enteros y 2 decimales.",
        [8] = "El tipo de documento del receptor debe ser numérico de 2 dígitos y debe ser alguno de los devueltos por el método DocumentosTipoConsultar().",
        [9] = "El número de documento del receptor, debe contener un valor numérico de 11 caracteres. Si el número del doucumento contiene letras no informarlas, solamente informar los caracteres numéricos.",
        [10] = "Código de autorización del comprobante, debe ser de 14 caracteres numéricos.",
        [100] = "Verificar que el CAE/CAI/CAEA exista registrado y autorizado en las bases del organismo.",
        [101] = "La fecha del comprobante <CbteFch> no podrá ser an terior a 20130101.",
        [102] = "Verifica que la CUIT del emisor informada se corresponda con la cuit registrada bajo el código de autorización <CodAutorizacion>.",
        [103] = "Verifica que el tipo de comprobante <CbteTipo> se corresponda con el registrado bajo el código de autorización informado <CodAutorizacion>",
        [104] = "Verifica que el punto de venta <PtoVta> se corresponda con el punto de venta registrado bajo el código de autorización informado <CodAutorizacion>",
        [105] = "Verifica que el Nº de comprobante <CbteNro> se corresponda con el Nº de comprobante registrado bajo el código de autorización informado <CodAutorizacion>",
        [106] = "Para modo <CbteModo> = “CAEA” , en caso de no encontrar el comprobante rendido, verifica que se encuentre vigente la rendición del mismo.",
        [107] = "Para modo <CbteModo> = “CAE” o <CbteModo> = “CAEA”, verifica que la fecha del comprobante <CbteFch> se corresponda con el código de autorización informado <CodAutorizacion>",
        [109] = "Para modo <CbteModo> = “CAEA”, verifica que el punto de venta sea un punto de venta habilitado para emitir comprobantes.",
        [110] = "Verificar que el importe de la operación informado se corresponda con lo registrado en las bases del organismo. Para los tipos de comprobantes sin ImpTotal se debe informar el campo en cero. Margen de error: Error relativo porcentual deberá ser <= 0.01% o el error absoluto <=1.",
        [111] = "Verifica que el tipo de documento del receptor <DocTipoReceptor> se corresponda con el registrado bajo el código de autorización informado <CodAutorizacion>",
        [112] = "Verifica que el número de documento del receptor <DocNroReceptor> se corresponda con el registrado bajo el código de autorización informado <CodAutorizacion>",
        [113] = "Para comprobantes tipo “A”, “A con leyenda operación sujeta a retención” o MiPyme el tipo de documento del receptor es obligatorio informarlo y debe ser CUIT (CbteTipo = 80).",
        [114] = "Para comprobantes tipo “A” o tipo “A con leyenda operación sujeta a retención”, el Nº de documento del receptor es obligatorio informarlo.",
        [115] = "Para comprobantes tipo B, C , R, 31, 30, 37, 38, 41 y 49 el tipo de documento del receptor solo es obligatorio informarlo cuando el importe es superior a 10.000.000 pesos.",
        [116] = "Para comprobantes tipo B, C , R, 31, 30, 37, 38, 41 y 49 , el número de documento del receptor solo es obligatorio informarlo cuando el importe es superior a 10.000.000 pesos.",
        [117] = "Si informa <DocTipoReceptor> o <DocNroReceptor> es obligatorio informar ambos.",
        [118] = "Si informa comprobante del tipo T (195, 196, 197), los tipos de documento válido son 80, 91, 94, 96.",
        [150] = "Si envía <Opcionales>, <Opcional> es obligatorio.",
        [151] = "El campo <Id> en <Opcionales> es obligatorio y debe ser alguno de los devueltos por el método OpcionalesTipoConsultar.",
        [152] = "El campo <Id> en <Opcionales> es obligatorio y no debe repetirse.\"",
        [153] = "El campo <Valor> en Opcionales es obligatorio",
        [200] = "Existe CAEA, no fue rendido o no coincide con los datos registrados.",
    };

    private static readonly IReadOnlyList<(string Code, string Desc)> Modes =
    [
        ("CAE", "Código de Autorización Electrónico"),
        ("CAEA", "Código de Autorización Electrónico Anticipado"),
        ("CAI", "Código de Autorización de Impresión"),
    ];

    private static readonly IReadOnlyList<(int Id, string Desc)> TouristVoucherTypes =
    [
        (195, "Factura T"),
        (196, "Nota de Débito T"),
        (197, "Nota de Crédito T"),
    ];

    /// <summary>Types the manual makes 115 and 116 apply to beyond classes B and C (R, 30, 31, 37, 38, 41 and 49).</summary>
    private static readonly IReadOnlySet<int> ThresholdTypes = new HashSet<int> { 30, 31, 37, 38, 41 };

    private IEnumerable<(int Id, string Desc)> VoucherTypes =>
        tables.VoucherTypes.Select(v => (v.Id, v.Desc)).Concat(Wsfexv1Tables.VoucherTypes).Concat(TouristVoucherTypes).OrderBy(v => v.Id);

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "ComprobanteConstatar" => await ConstatarAsync(call, ct),
        "ComprobantesModalidadConsultar" => Table(call, Modes, "FacModTipo", m => [("Cod", m.Code), ("Desc", m.Desc)]),
        "ComprobantesTipoConsultar" => Table(call, VoucherTypes, "CbteTipo", t => [("Id", t.Id), ("Desc", t.Desc)]),
        "DocumentosTipoConsultar" => Table(call, tables.DocumentTypes, "DocTipo", d => [("Id", d.Id), ("Desc", d.Desc)]),
        "OpcionalesTipoConsultar" => Table(call, Array.Empty<(string, string)>(), "OpcionalTipo", o => [("Id", o.Item1), ("Desc", o.Item2)]),
        "ComprobanteDummy" => call.Ok(new XElement(call.Operation.Output, new XElement(Ns(call) + "ComprobanteDummyResult",
            new XElement(Ns(call) + "AppServer", "OK"), new XElement(Ns(call) + "DbServer", "OK"), new XElement(Ns(call) + "AuthServer", "OK")))),
        _ => null,
    };

    /// <summary>A voucher as some invoicing service registered it, whichever it was.</summary>
    private sealed record Registered(
        long Cuit, int PointOfSale, int VoucherType, long From, long To, DateOnly Date, decimal Total,
        int DocType, long DocNumber, string Mode, string Code);

    private sealed record Query(
        string? Mode, long Cuit, int PointOfSale, int VoucherType, long Number, string? Date, decimal Total,
        string? Code, string? DocType, string? DocNumber, IReadOnlyList<ExportOptional>? Optionals);

    private async Task<ContractAnswer> ConstatarAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("CmpReq");
        var query = new Query(
            request.Str("CbteModo"), request.LongOf("CuitEmisor"), request.IntOf("PtoVta"), request.IntOf("CbteTipo"),
            request.LongOf("CbteNro"), request.Str("CbteFch"), request.DecimalOf("ImpTotal"), request.Str("CodAutorizacion"),
            Blank(request.Str("DocTipoReceptor")), Blank(request.Str("DocNroReceptor")),
            request.List("Opcionales", "Opcional", o => new ExportOptional(o.Str("Id"), o.Str("Valor"))));

        var errors = FormatErrors(query);
        var observations = errors.Count > 0 ? [] : await ObservationsAsync(query, ct);
        observations = observations.Distinct().ToList();
        var approved = errors.Count == 0 && observations.All(o => o == 200);
        return Constatado(call, query, approved, observations, errors);
    }

    /// <summary>Validations 1 to 10: the format of every field, all of them reported.</summary>
    private List<int> FormatErrors(Query q)
    {
        var errors = new List<int>();
        if (q.Mode is not ("CAE" or "CAEA" or "CAI")) errors.Add(1);
        if (!Cuits.IsValid(q.Cuit)) errors.Add(2);
        if (q.PointOfSale is < 1 or > 99_998) errors.Add(3);
        if (!VoucherTypes.Any(t => t.Id == q.VoucherType)) errors.Add(4);
        if (q.Number is < 1 or > 99_999_999) errors.Add(5);
        if (!Fev1Dates.TryParse(q.Date, out _)) errors.Add(6);
        if (q.Total < 0 || Math.Abs(q.Total) >= 10_000_000_000_000m || q.Total != Math.Round(q.Total, 2)) errors.Add(7);
        if (q.DocType is not null && (q.DocType.Length > 2 || !q.DocType.All(char.IsAsciiDigit) || !tables.HasDocumentType(int.Parse(q.DocType))))
            errors.Add(8);
        if (q.DocNumber is not null && (q.DocNumber.Length > 11 || !q.DocNumber.All(char.IsAsciiDigit))) errors.Add(9);
        if (q.Code is not { Length: 14 } || !q.Code.All(char.IsAsciiDigit)) errors.Add(10);
        return errors;
    }

    /// <summary>Validations 100 to 153 and 200: the receiver's rules, then the voucher against the registry.</summary>
    private async Task<List<int>> ObservationsAsync(Query q, CancellationToken ct)
    {
        var observations = new List<int>();
        var date = DateOnly.ParseExact(q.Date!, "yyyyMMdd", CultureInfo.InvariantCulture);
        if (date < new DateOnly(2013, 1, 1)) observations.Add(101);
        ReceiverRules(q, observations);
        OptionalRules(q, observations);

        if (q.Mode == "CAI")
        {
            observations.Add(100);
            return observations;
        }

        var byKey = await FindByKeyAsync(q.Cuit, q.PointOfSale, q.VoucherType, q.Number, ct);
        if (byKey is not null && byKey.Code == q.Code && byKey.Mode == q.Mode)
        {
            Compare(q, date, byKey, observations);
        }
        else if (await FindByCodeAsync(q.Code!, q.Mode!, ct) is { } registered)
        {
            if (registered.Cuit != q.Cuit) observations.Add(102);
            if (registered.VoucherType != q.VoucherType) observations.Add(103);
            if (registered.PointOfSale != q.PointOfSale) observations.Add(104);
            if (q.Number < registered.From || q.Number > registered.To) observations.Add(105);
            Compare(q, date, registered, observations);
        }
        else if (q.Mode == "CAEA" && await caeas.FindByCodeAsync(q.Code!, ct) is { } caea && caea.Cuit == q.Cuit)
        {
            var issuer = await taxpayers.FindAsync(q.Cuit, ct);
            if (!settings.OpenAccess && issuer?.FindPointOfSale(q.PointOfSale) is not { Kind: PointOfSaleKind.WebServiceCaea }) observations.Add(109);
            if (date < caea.ValidFrom || date > caea.ValidTo) observations.Add(107);
            observations.Add(clock.Today() <= caea.ReportDeadline ? 200 : 106);
        }
        else
        {
            observations.Add(100);
        }
        return observations;
    }

    private void Compare(Query q, DateOnly date, Registered registered, List<int> observations)
    {
        if (date != registered.Date) observations.Add(107);
        var error = Math.Abs(q.Total - registered.Total);
        if (error > 1 && (registered.Total == 0 || error / Math.Abs(registered.Total) > 0.0001m)) observations.Add(110);
        if (q.DocType is not null && int.Parse(q.DocType) != registered.DocType) observations.Add(111);
        if (q.DocNumber is not null && long.Parse(q.DocNumber) != registered.DocNumber) observations.Add(112);
    }

    /// <summary>113 to 118, which hold whatever was registered.</summary>
    private void ReceiverRules(Query q, List<int> observations)
    {
        if ((q.DocType is null) != (q.DocNumber is null)) observations.Add(117);
        var type = tables.VoucherType(q.VoucherType);
        if (type is { Class: VoucherClass.A or VoucherClass.ALey })
        {
            if (q.DocType != "80") observations.Add(113);
            if (q.DocNumber is null && !type.Fce) observations.Add(114);
        }
        var threshold = type is { Class: VoucherClass.B or VoucherClass.C or VoucherClass.UsedGoods } || ThresholdTypes.Contains(q.VoucherType);
        if (threshold && q.Total > settings.FinalConsumerIdentificationThreshold)
        {
            if (q.DocType is null) observations.Add(115);
            if (q.DocNumber is null) observations.Add(116);
        }
        if (q.VoucherType is >= 195 and <= 197 && q.DocType is not null and not ("80" or "91" or "94" or "96")) observations.Add(118);
    }

    private static void OptionalRules(Query q, List<int> observations)
    {
        if (q.Optionals is null) return;
        if (q.Optionals.Count == 0)
        {
            observations.Add(150);
            return;
        }
        var ids = new HashSet<string>();
        foreach (var optional in q.Optionals)
        {
            // OpcionalesTipoConsultar has no rows, so no Id is a valid one.
            observations.Add(!string.IsNullOrEmpty(optional.Id) && !ids.Add(optional.Id) ? 152 : 151);
            if (string.IsNullOrEmpty(optional.Value)) observations.Add(153);
        }
    }

    // ---- The registry: WSFEv1's vouchers and every other service's -----------------------

    private async Task<Registered?> FindByKeyAsync(long cuit, int pointOfSale, int voucherType, long number, CancellationToken ct)
    {
        if (await wsfeVouchers.FindAsync(cuit, pointOfSale, voucherType, number, ct) is { } wsfe) return FromWsfe(wsfe);
        return await documents.FindVoucherAsync(cuit, pointOfSale, voucherType, number, ct) is { } other ? FromOther(other) : null;
    }

    /// <summary>Where a code is registered, if anywhere. ARCA looks vouchers up by code; ArcaSim reads every one, which is fine for a simulator.</summary>
    private async Task<Registered?> FindByCodeAsync(string code, string mode, CancellationToken ct)
    {
        var wsfe = (await wsfeVouchers.ListAsync(null, int.MaxValue, ct)).Select(FromWsfe);
        var others = (await documents.ListAsync<AuthorizedVoucher>(AuthorizedVouchers.Collection, "", ct)).Select(FromOther);
        return wsfe.Concat(others).FirstOrDefault(r => r.Code == code && r.Mode == mode);
    }

    private static Registered FromWsfe(StoredVoucher v) => new(
        v.Cuit, v.PointOfSale, v.VoucherType, v.From, v.To, v.Date, (decimal)v.Detail.ImpTotal,
        v.Detail.DocTipo, v.Detail.DocNro, v.EmissionType == EmissionType.Cae ? "CAE" : "CAEA", v.AuthorizationCode);

    private static Registered FromOther(AuthorizedVoucher v) => new(
        v.Cuit, v.PointOfSale, v.VoucherType, v.Number, v.Number, v.Date, v.Total,
        v.ReceiverDocType, v.ReceiverDocNumber, v.EmissionType.ToUpperInvariant(), v.Code);

    // ---- Answers -----------------------------------------------------------------------

    private ContractAnswer Constatado(ServiceCall call, Query q, bool approved, List<int> observations, List<int> errors)
    {
        var ns = Ns(call);
        var request = call.Request.Child("CmpReq");
        XElement? Sent(string name) => request.Child(name) is { } element ? new XElement(ns + name, element.Value) : null;
        XElement Messages(string list, string item, IEnumerable<int> codes) =>
            new(ns + list, codes.Select(code => new XElement(ns + item, new XElement(ns + "Code", code), new XElement(ns + "Msg", Texts[code]))));

        return call.Ok(new XElement(call.Operation.Output, new XElement(ns + "ComprobanteConstatarResult",
            new XElement(ns + "CmpResp",
                Sent("CbteModo"),
                new XElement(ns + "CuitEmisor", q.Cuit),
                new XElement(ns + "PtoVta", q.PointOfSale),
                new XElement(ns + "CbteTipo", q.VoucherType),
                new XElement(ns + "CbteNro", q.Number),
                Sent("CbteFch"),
                new XElement(ns + "ImpTotal", ContractXml.Format((double)q.Total)),
                Sent("CodAutorizacion"),
                Sent("DocTipoReceptor"),
                Sent("DocNroReceptor"),
                q.Optionals is null ? null : new XElement(ns + "Opcionales", q.Optionals.Select(o => new XElement(ns + "Opcional",
                    o.Id is null ? null : new XElement(ns + "Id", o.Id), o.Value is null ? null : new XElement(ns + "Valor", o.Value))))),
            new XElement(ns + "Resultado", approved ? "A" : "R"),
            observations.Count == 0 ? null : Messages("Observaciones", "Obs", observations),
            new XElement(ns + "FchProceso", Fev1Dates.FormatProcessed(clock.Now)),
            Events(call),
            errors.Count == 0 ? null : Messages("Errors", "Err", errors))));
    }

    private static XNamespace Ns(ServiceCall call) => call.Operation.Output.Namespace;

    private static XElement Events(ServiceCall call) =>
        new(Ns(call) + "Events", new XElement(Ns(call) + "Evt", new XElement(Ns(call) + "Code", 0)));

    private static ContractAnswer Table<T>(ServiceCall call, IEnumerable<T> rows, string item, Func<T, (string Name, object Value)[]> fields)
    {
        var ns = Ns(call);
        return call.Ok(new XElement(call.Operation.Output, new XElement(ns + (call.Name + "Result"),
            new XElement(ns + "ResultGet", rows.Select(row =>
                new XElement(ns + item, fields(row).Select(f => new XElement(ns + f.Name, ContractXml.Format(f.Value)))))),
            Events(call))));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
