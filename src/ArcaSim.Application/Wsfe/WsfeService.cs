using ArcaSim.Domain;

namespace ArcaSim.Application.Wsfe;

/// <summary>
/// WSFEv1's 22 operations. Each takes the operation's request as ARCA's WSDL
/// defines it and returns its result; the SOAP layer only reads and writes
/// them. Business errors never throw: they go back inside Errors, as ARCA does.
/// </summary>
public sealed partial class WsfeService(
    IClock clock,
    SimulationSettings settings,
    TokenValidator tokens,
    VoucherValidator validator,
    ValidationCatalog catalog,
    ParameterTables tables,
    ITaxpayerRepository taxpayers,
    IVoucherStore vouchers,
    ICaeaStore caeas,
    IExchangeRates rates,
    IAuthorizationCodes codes,
    SequenceLocks locks)
{
    public const string Name = "wsfe";

    /// <summary>"No existen datos en nuestros registros para los parametros ingresados." as ARCA sends it.</summary>
    private Err NoData => new() { Code = 602, Msg = catalog.Message(602) };

    public DummyResponse FEDummy() => new() { AppServer = "OK", DbServer = "OK", AuthServer = "OK" };

    public FERegXReqResponse FECompTotXRequest(AuthOnlyRequest request)
    {
        var auth = tokens.Validate(request.Auth);
        return auth.Failed
            ? new FERegXReqResponse { Errors = [auth.Error!] }
            : new FERegXReqResponse { RegXReq = settings.MaxRecordsPerRequest };
    }

    public async Task<FERecuperaLastCbteResponse> FECompUltimoAutorizadoAsync(FECompUltimoAutorizadoRequest request, CancellationToken ct = default)
    {
        const string method = "FECompUltimoAutorizado";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FERecuperaLastCbteResponse { Errors = [auth.Error!] };

        var errors = new List<Err>();
        if (request.PtoVta is < 1 or > 99_998) errors.Add(catalog.For(method, 11000).ToErr());
        if (tables.VoucherType(request.CbteTipo) is null) errors.Add(catalog.For(method, 11001).ToErr());
        if (errors.Count == 0)
        {
            var issuer = await IssuerAsync(auth.Cuit, request.PtoVta, PointOfSaleKind.WebServiceCae, tables.VoucherType(request.CbteTipo)?.Class, ct);
            if (issuer?.FindPointOfSale(request.PtoVta) is not { Kind: PointOfSaleKind.WebServiceCae or PointOfSaleKind.WebServiceCaea })
                errors.Add(catalog.For(method, 11002).ToErr());
        }
        if (errors.Count > 0) return new FERecuperaLastCbteResponse { Errors = [.. errors] };

        var last = await vouchers.LastAsync(auth.Cuit, request.PtoVta, request.CbteTipo, ct);
        return new FERecuperaLastCbteResponse
        {
            PtoVta = request.PtoVta,
            CbteTipo = request.CbteTipo,
            CbteNro = (int)(last?.To ?? 0),
        };
    }

    public async Task<FECompConsultaResponse> FECompConsultarAsync(FECompConsultarRequest request, CancellationToken ct = default)
    {
        const string method = "FECompConsultar";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECompConsultaResponse { Errors = [auth.Error!] };

        var query = request.FeCompConsReq ?? new FECompConsultaReq();
        var errors = new List<Err>();
        if (query.PtoVta is < 1 or > 99_998) errors.Add(catalog.For(method, 10200).ToErr());
        if (tables.VoucherType(query.CbteTipo) is null) errors.Add(catalog.For(method, 10201).ToErr());
        if (query.CbteNro is < 1 or > 99_999_999) errors.Add(catalog.For(method, 10202).ToErr());
        if (errors.Count == 0 && !settings.OpenAccess && (await taxpayers.FindAsync(auth.Cuit, ct))?.FindPointOfSale(query.PtoVta) is null)
            errors.Add(catalog.For(method, 10104).ToErr());
        if (errors.Count > 0) return new FECompConsultaResponse { Errors = [.. errors] };

        var stored = await vouchers.FindAsync(auth.Cuit, query.PtoVta, query.CbteTipo, query.CbteNro, ct);
        if (stored is null) return new FECompConsultaResponse { Errors = [NoData] };

        var result = new FECompConsResponse();
        stored.Detail.CopyTo(result);
        result.Resultado = "A";
        result.CodAutorizacion = stored.AuthorizationCode;
        result.EmisionTipo = stored.EmissionType == EmissionType.Cae ? "CAE" : "CAEA";
        result.FchVto = Fev1Dates.Format(stored.AuthorizationDue);
        result.FchProceso = Fev1Dates.FormatProcessed(stored.ProcessedAt);
        result.Observaciones = stored.Observations.Count > 0 ? stored.Observations.ToArray() : null;
        result.PtoVta = stored.PointOfSale;
        result.CbteTipo = stored.VoucherType;
        return new FECompConsultaResponse { ResultGet = result };
    }

    // ---- Parameter tables ------------------------------------------------------

    public CbteTipoResponse FEParamGetTiposCbte(AuthOnlyRequest request) =>
        Table<CbteTipoResponse, CbteTipo>(request, () => tables.VoucherTypes
            .Select(v => new CbteTipo { Id = v.Id, Desc = v.Desc, FchDesde = v.From, FchHasta = v.To }));

    public ConceptoTipoResponse FEParamGetTiposConcepto(AuthOnlyRequest request) =>
        Table<ConceptoTipoResponse, ConceptoTipo>(request, () => tables.Concepts
            .Select(c => new ConceptoTipo { Id = int.Parse(c.Id), Desc = c.Desc, FchDesde = c.From, FchHasta = c.To }));

    public DocTipoResponse FEParamGetTiposDoc(AuthOnlyRequest request) =>
        Table<DocTipoResponse, DocTipo>(request, () => tables.DocumentTypes
            .Select(d => new DocTipo { Id = int.Parse(d.Id), Desc = d.Desc, FchDesde = d.From, FchHasta = d.To }));

    public IvaTipoResponse FEParamGetTiposIva(AuthOnlyRequest request) =>
        Table<IvaTipoResponse, IvaTipo>(request, () => tables.VatRates
            .Select(v => new IvaTipo { Id = v.Id, Desc = v.Desc, FchDesde = v.From, FchHasta = v.To }));

    public MonedaResponse FEParamGetTiposMonedas(AuthOnlyRequest request) =>
        Table<MonedaResponse, Moneda>(request, () => tables.Currencies
            .Select(m => new Moneda { Id = m.Id, Desc = m.Desc, FchDesde = m.From, FchHasta = m.To }));

    public OpcionalTipoResponse FEParamGetTiposOpcional(AuthOnlyRequest request) =>
        Table<OpcionalTipoResponse, OpcionalTipo>(request, () => tables.Optionals
            .Select(o => new OpcionalTipo { Id = o.Id, Desc = o.Desc, FchDesde = o.From, FchHasta = o.To }));

    public FETributoResponse FEParamGetTiposTributos(AuthOnlyRequest request) =>
        Table<FETributoResponse, TributoTipo>(request, () => tables.Taxes
            .Select(t => new TributoTipo { Id = short.Parse(t.Id), Desc = t.Desc, FchDesde = t.From, FchHasta = t.To }));

    public FEPaisResponse FEParamGetTiposPaises(AuthOnlyRequest request) =>
        Table<FEPaisResponse, PaisTipo>(request, () => tables.Countries
            .Select(c => new PaisTipo { Id = (short)c.Id, Desc = c.Desc }));

    /// <summary>
    /// The issuer's web service points of sale. Only "CAE - Ri Iva" was ever
    /// seen as EmisionTipo; the texts for other cases follow its pattern.
    /// </summary>
    public async Task<FEPtoVentaResponse> FEParamGetPtosVentaAsync(AuthOnlyRequest request, CancellationToken ct = default)
    {
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FEPtoVentaResponse { Errors = [auth.Error!] };

        var issuer = await taxpayers.FindAsync(auth.Cuit, ct);
        var points = (issuer?.PointsOfSale ?? [])
            .Where(p => p.Kind != PointOfSaleKind.Other)
            .OrderBy(p => p.Number)
            .Select(p => new PtoVenta
            {
                Nro = p.Number,
                EmisionTipo = EmissionTypeText(p.Kind, issuer!.VatCondition),
                Bloqueado = p.Blocked ? "S" : "N",
                FchBaja = p.DeactivatedOn is { } day ? Fev1Dates.Format(day) : "NULL",
            })
            .ToArray();
        return points.Length == 0
            ? new FEPtoVentaResponse { Errors = [NoData] }
            : new FEPtoVentaResponse { ResultGet = points };
    }

    /// <summary>ArcaSim keeps no activities per taxpayer, so this answers as ARCA does for an empty result.</summary>
    public FEActividadesResponse FEParamGetActividades(AuthOnlyRequest request)
    {
        var auth = tokens.Validate(request.Auth);
        return new FEActividadesResponse { Errors = [auth.Error ?? NoData] };
    }

    public async Task<FECotizacionResponse> FEParamGetCotizacionAsync(FEParamGetCotizacionRequest request, CancellationToken ct = default)
    {
        const string method = "FEParamGetCotizacion";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECotizacionResponse { Errors = [auth.Error!] };

        if (string.IsNullOrEmpty(request.MonId)) return Fail(12001);
        if (!tables.HasCurrency(request.MonId)) return Fail(12000);
        var askedFor = Fev1Dates.Blank(request.FchCotiz);
        DateOnly day = default;
        if (askedFor is not null && !Fev1Dates.TryParse(askedFor, out day)) return Fail(12002);

        var today = clock.Today();
        if (request.MonId == "PES")
            return new FECotizacionResponse { ResultGet = new Cotizacion { MonId = "PES", MonCotiz = 1, FchCotiz = Fev1Dates.Format(today) } };

        var found = await rates.RateAsync(request.MonId, askedFor is null ? Fev1Dates.PreviousBusinessDay(today) : day, ct);
        return found is { } rate
            ? new FECotizacionResponse { ResultGet = new Cotizacion { MonId = request.MonId, MonCotiz = (double)rate.Rate, FchCotiz = Fev1Dates.Format(rate.Day) } }
            : new FECotizacionResponse { Errors = [NoData] };

        FECotizacionResponse Fail(int code) => new() { Errors = [catalog.For(method, code).ToErr()] };
    }

    /// <summary>
    /// One row per condition and voucher class it may go on, or only the rows of
    /// the class asked for. How ARCA lays out the rows was never captured
    /// (wsfev1.md §7.8); this follows the manual's "all the combinations".
    /// </summary>
    public CondicionIvaReceptorResponse FEParamGetCondicionIvaReceptor(FEParamGetCondicionIvaReceptorRequest request)
    {
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new CondicionIvaReceptorResponse { Errors = [auth.Error!] };

        var asked = Fev1Dates.Blank(request.ClaseCmp);
        string[] classes = asked switch
        {
            null => ["A", "B", "C", "49"],
            "A" or "ALEY" or "B" or "C" or "49" => [asked],
            _ => [],
        };
        if (classes.Length == 0)
            return new CondicionIvaReceptorResponse { Errors = [catalog.For("FEParamGetCondicionIvaReceptor", 10244).ToErr()] };

        var rows = classes
            .SelectMany(cls => tables.ReceiverVatConditions
                .Where(c => c.Classes.Contains(cls == "ALEY" ? "A" : cls))
                .Select(c => new CondicionIvaReceptor { Id = c.Id, Desc = c.Desc, Cmp_Clase = cls }))
            .ToArray();
        return new CondicionIvaReceptorResponse { ResultGet = rows };
    }

    private TResponse Table<TResponse, TItem>(AuthOnlyRequest request, Func<IEnumerable<TItem>> rows)
        where TResponse : IParameterResponse<TItem>, new()
    {
        var auth = tokens.Validate(request.Auth);
        var response = new TResponse();
        if (auth.Failed) response.Errors = [auth.Error!];
        else response.ResultGet = rows().ToArray();
        return response;
    }

    private static string EmissionTypeText(PointOfSaleKind kind, VatCondition condition)
    {
        var regime = condition switch
        {
            VatCondition.ResponsableInscripto => "Ri Iva",
            VatCondition.Exento => "Exento",
            _ => "Monotributo",
        };
        return kind == PointOfSaleKind.WebServiceCaea ? $"CAEA - {regime}" : $"CAE - {regime}";
    }

    /// <summary>
    /// The issuer, as ArcaSim knows it. With open access, a CUIT it has not seen
    /// is created on the spot (Monotributo if its first voucher is class C,
    /// Responsable Inscripto otherwise), and so is the point of sale it uses,
    /// so an application needs nothing but ARCA's endpoints.
    /// </summary>
    private async Task<Taxpayer?> IssuerAsync(long cuit, int? pointOfSale, PointOfSaleKind kind, VoucherClass? firstClass, CancellationToken ct)
    {
        var issuer = await taxpayers.FindAsync(cuit, ct);
        if (!settings.OpenAccess) return issuer;

        var changed = false;
        if (issuer is null)
        {
            if (!Cuits.IsValid(cuit)) return null;
            var condition = firstClass == VoucherClass.C ? VatCondition.Monotributo : VatCondition.ResponsableInscripto;
            issuer = new Taxpayer(cuit, $"Contribuyente {cuit}", condition);
            changed = true;
        }
        if (pointOfSale is >= 1 and <= 99_998 && issuer.FindPointOfSale(pointOfSale.Value) is null)
        {
            issuer.AddPointOfSale(new PointOfSale(pointOfSale.Value, kind));
            changed = true;
        }
        if (changed) await taxpayers.SaveAsync(issuer, ct);
        return issuer;
    }

    /// <summary>The issuer's checks behind code 10000 (wsfev1-codigos.md §4.1), with the manual's numbered messages.</summary>
    private static string? IssuerProblem(Taxpayer? issuer, VoucherTypeInfo? type)
    {
        if (issuer is not { Active: true })
            return "EL CUIT INFORMADO COMO EMISOR NO SE ENCUENTRA REGISTRADO DE FORMA ACTIVA EN LAS BASES DE LA ADMINISTRACIÓN.";
        if (type is { Class: VoucherClass.A or VoucherClass.B or VoucherClass.ALey }
            && issuer.VatCondition != VatCondition.ResponsableInscripto)
            return "LA CUIT INFORMADA NO CORRESPONDE A UN RESPONSABLE INSCRIPTO EN EL IMPUESTO";
        return null;
    }
}
