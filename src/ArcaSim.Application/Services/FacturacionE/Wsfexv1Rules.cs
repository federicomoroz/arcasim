using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.FacturacionE;

/// <summary>
/// Factura de exportación, class E (docs/arca/servicios/wsfexv1.md): FEXAuthorize
/// numbers vouchers 19, 20 and 21 as "último + 1" per CUIT, point of sale and
/// type, gives a CAE, and answers a repeated request Id with what it already
/// granted and Reproceso S; FEXGetLast_ID, FEXGetLast_CMP and FEXGetCMP read
/// that back; the FEXGetPARAM_* tables answer the values the spec documents.
/// Every answer carries FEXErr (0 when there was no error) and FEXEvents 0/Ok,
/// as production does.
///
/// ArcaSim's choices where the spec says NO VERIFICADO:
/// - a business error answers FEXErr alone, without FEXResultAuth, as a refused ticket does;
/// - "OK" as FEXErr's text when there is no error; Resultado A on approval;
/// - Fch_venc_Cae is the voucher's date plus the CAE lifetime WSFEv1 uses (10 days);
/// - an Id is registered only when approved, so a refused one can be sent again, and a
///   repeated Id gets back what was stored whatever the rest of the request says;
/// - FEXGetLast_ID is the highest approved Id, 0 before the first; FEXGetLast_CMP
///   answers number 0 without a date before the first voucher;
/// - an export point of sale is a web service CAE one (ArcaSim's taxpayers have no
///   FEEWS kind); with open access an unknown one is created on first use, as WSFEv1 does;
/// - FEXGetPARAM_DST_pais and FEXGetPARAM_MON serve WSFEv1's countries and currencies,
///   which are ARCA's generic tables; FEXGetPARAM_DST_CUIT the manual's two example CUITs;
///   validation 1570 accepts those and any CUIT with a right check digit (the manual's
///   50000000016 does not have one);
/// - FEXGetPARAM_Actividades answers the issuer's padrón activity;
/// - FEXCheck_Permiso answers OK for any permit with the right format and a known country:
///   ArcaSim has no customs registry;
/// - FEXGetPARAM_Ctz's errors other than 1003 go out as 1014 with ArcaSim's own text,
///   as the manual says unlisted errors do.
/// </summary>
public sealed class Wsfexv1Rules : IServiceBehavior
{
    private readonly IClock _clock;
    private readonly SimulationSettings _settings;
    private readonly ITaxpayerRepository _taxpayers;
    private readonly IAuthorizationCodes _codes;
    private readonly SequenceLocks _locks;
    private readonly ParameterTables _tables;
    private readonly IExchangeRates _rates;
    private readonly ExportStore _store;
    private readonly ExportVoucherValidator _validator;

    public Wsfexv1Rules(
        IClock clock, SimulationSettings settings, ITaxpayerRepository taxpayers, IAuthorizationCodes codes,
        SequenceLocks locks, ParameterTables tables, IExchangeRates rates, IDocumentStore documents)
    {
        _clock = clock;
        _settings = settings;
        _taxpayers = taxpayers;
        _codes = codes;
        _locks = locks;
        _tables = tables;
        _rates = rates;
        _store = new ExportStore(documents, locks);
        _validator = new ExportVoucherValidator(tables, rates, _store);
    }

    public string Service => ExportStore.Service;

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "FEXAuthorize" => await AuthorizeAsync(call, ct),
        "FEXGetCMP" => await GetVoucherAsync(call, ct),
        "FEXGetLast_ID" => Answer(call, new XElement(Ns(call) + "FEXResultGet",
            new XElement(Ns(call) + "Id", await _store.LastIdAsync(call.Cuit, ct)))),
        "FEXGetLast_CMP" => await LastVoucherAsync(call, ct),
        "FEXGetPARAM_Cbte_Tipo" => Table(call, Wsfexv1Tables.VoucherTypes, "ClsFEXResponse_Cbte_Tipo", t => [("Cbte_Id", t.Id), ("Cbte_Ds", t.Desc)]),
        "FEXGetPARAM_Tipo_Expo" => Table(call, Wsfexv1Tables.ExportTypes, "ClsFEXResponse_Tex", t => [("Tex_Id", t.Id), ("Tex_Ds", t.Desc)]),
        "FEXGetPARAM_Incoterms" => Table(call, Wsfexv1Tables.Incoterms, "ClsFEXResponse_Inc", t => [("Inc_Id", t.Id), ("Inc_Ds", t.Desc)]),
        "FEXGetPARAM_Idiomas" => Table(call, Wsfexv1Tables.Languages, "ClsFEXResponse_Idi", t => [("Idi_Id", t.Id), ("Idi_Ds", t.Desc)]),
        "FEXGetPARAM_UMed" => Table(call, Wsfexv1Tables.Units, "ClsFEXResponse_UMed", t => [("Umed_Id", t.Id), ("Umed_Ds", t.Desc)]),
        "FEXGetPARAM_DST_pais" => Table(call, _tables.Countries, "ClsFEXResponse_DST_pais", c => [("DST_Codigo", c.Id), ("DST_Ds", c.Desc)]),
        "FEXGetPARAM_DST_CUIT" => Table(call, Wsfexv1Tables.CountryCuits, "ClsFEXResponse_DST_cuit", c => [("DST_CUIT", c.Cuit), ("DST_Ds", c.Desc)]),
        "FEXGetPARAM_MON" => Table(call, _tables.Currencies, "ClsFEXResponse_Mon",
            m => [("Mon_Id", m.Id), ("Mon_Ds", m.Desc), ("Mon_vig_desde", m.From), ("Mon_vig_hasta", m.To)]),
        "FEXGetPARAM_Opcionales" => Table(call, Wsfexv1Tables.Optionals, "ClsFEXResponse_Opc", o => [("Opc_Id", o.Id), ("Opc_Ds", o.Desc)]),
        "FEXGetPARAM_MON_CON_COTIZACION" => await RatedCurrenciesAsync(call, ct),
        "FEXGetPARAM_Ctz" => await RateAsync(call, ct),
        "FEXGetPARAM_PtoVenta" => await PointsOfSaleAsync(call, ct),
        "FEXGetPARAM_Actividades" => await ActivitiesAsync(call, ct),
        "FEXCheck_Permiso" => CheckPermit(call),
        "FEXDummy" => call.Ok(new XElement(call.Operation.Output, new XElement(Ns(call) + "FEXDummyResult",
            new XElement(Ns(call) + "AppServer", "OK"), new XElement(Ns(call) + "DbServer", "OK"), new XElement(Ns(call) + "AuthServer", "OK")))),
        _ => null,
    };

    // ---- Authorization ---------------------------------------------------------------

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CancellationToken ct)
    {
        var voucher = ExportVoucher.Read(call.Request.Child("Cmp"));
        if (voucher.Id < 0) return Fail(call, 1014);

        using var _ = await _locks.AcquireAsync(Service, call.Cuit, voucher.PointOfSale, voucher.VoucherType, ct);
        if (await _store.ByRequestAsync(call.Cuit, voucher.Id, ct) is { } granted) return Approved(call, granted, reprocessed: true);

        var today = _clock.Today();
        var enabled = voucher.VoucherType is 19 or 20 or 21 && await ExportPointOfSaleAsync(call.Cuit, voucher.PointOfSale, ct);
        var code = await _validator.ValidateAsync(call.Cuit, voucher, today, enabled, ct);
        if (code != 0) return Fail(call, code);

        var date = string.IsNullOrEmpty(voucher.Date) ? today : DateOnly.ParseExact(voucher.Date, "yyyyMMdd", CultureInfo.InvariantCulture);
        var due = date.AddDays(_settings.CaeLifetimeDays);
        var authorized = new AuthorizedExport(call.Cuit, voucher, Fev1Dates.Format(date), _codes.NextCae(), Fev1Dates.Format(due),
            Fev1Dates.Format(today), await RateOfAsync(voucher, date, ct));
        await _store.AddAsync(authorized, date, due, ct);
        return Approved(call, authorized, reprocessed: false);
    }

    /// <summary>The rate as sent; with CanMisMonExt S and none sent, the official one ArcaSim has, or 0.</summary>
    private async Task<decimal> RateOfAsync(ExportVoucher voucher, DateOnly date, CancellationToken ct)
    {
        if (voucher.Rate is > 0) return voucher.Rate.Value;
        if (voucher.Currency == "PES") return 1;
        return voucher.Currency is { } currency && await _rates.RateAsync(currency, Fev1Dates.PreviousBusinessDay(date), ct) is { } official
            ? official.Rate
            : 0;
    }

    private ContractAnswer Approved(ServiceCall call, AuthorizedExport authorized, bool reprocessed)
    {
        var ns = Ns(call);
        var voucher = authorized.Voucher;
        return Answer(call, new XElement(ns + "FEXResultAuth",
            new XElement(ns + "Id", voucher.Id),
            new XElement(ns + "Cuit", authorized.Cuit),
            new XElement(ns + "Cbte_tipo", voucher.VoucherType),
            new XElement(ns + "Punto_vta", voucher.PointOfSale),
            new XElement(ns + "Cbte_nro", voucher.Number),
            new XElement(ns + "Cae", authorized.Cae),
            new XElement(ns + "Fch_venc_Cae", authorized.CaeDue),
            new XElement(ns + "Fch_cbte", authorized.Date),
            new XElement(ns + "Resultado", "A"),
            new XElement(ns + "Reproceso", reprocessed ? "S" : "N")));
    }

    /// <summary>
    /// Whether the issuer has the point of sale for export web services. With
    /// open access, an issuer or point of sale ArcaSim has not seen is created.
    /// </summary>
    private async Task<bool> ExportPointOfSaleAsync(long cuit, int number, CancellationToken ct)
    {
        if (number is < 1 or > VoucherLimits.MaxPointOfSale) return false;
        var issuer = await _taxpayers.FindAsync(cuit, ct);
        if (_settings.OpenAccess && (issuer is null ? Cuits.IsValid(cuit) : issuer.FindPointOfSale(number) is null))
        {
            issuer ??= new Taxpayer(cuit, $"Contribuyente {cuit}", VatCondition.ResponsableInscripto);
            issuer.AddPointOfSale(new PointOfSale(number, PointOfSaleKind.WebServiceCae));
            await _taxpayers.SaveAsync(issuer, ct);
        }
        return issuer is { Active: true } && issuer.FindPointOfSale(number) is { Kind: PointOfSaleKind.WebServiceCae, Blocked: false, DeactivatedOn: null };
    }

    // ---- Queries ---------------------------------------------------------------------

    private async Task<ContractAnswer> GetVoucherAsync(ServiceCall call, CancellationToken ct)
    {
        var key = call.Request.Child("Cmp");
        var found = await _store.FindAsync(call.Cuit, key.IntOf("Punto_vta"), key.IntOf("Cbte_tipo"), key.LongOf("Cbte_nro"), ct);
        if (found is null) return Fail(call, 1020);

        var ns = Ns(call);
        var v = found.Voucher;
        XElement? Optional(string name, object? value) => value is null ? null : new XElement(ns + name, value);
        return Answer(call, new XElement(ns + "FEXResultGet",
            new XElement(ns + "Id", v.Id),
            new XElement(ns + "Fecha_cbte", found.Date),
            new XElement(ns + "Cbte_tipo", v.VoucherType),
            new XElement(ns + "Punto_vta", v.PointOfSale),
            new XElement(ns + "Cbte_nro", v.Number),
            new XElement(ns + "Tipo_expo", v.ExportType),
            Optional("Permiso_existente", v.PermitExists),
            v.Permits is null ? null : new XElement(ns + "Permisos", v.Permits.Select(p => new XElement(ns + "Permiso",
                Optional("Id_permiso", p.Id), new XElement(ns + "Dst_merc", p.Destination)))),
            new XElement(ns + "Dst_cmp", v.Destination),
            Optional("Cliente", v.Client),
            new XElement(ns + "Cuit_pais_cliente", v.ClientCountryCuit),
            Optional("Domicilio_cliente", v.ClientAddress),
            Optional("Id_impositivo", v.ClientTaxId),
            Optional("Moneda_Id", v.Currency),
            new XElement(ns + "Moneda_ctz", found.Rate),
            Optional("CanMisMonExt", v.SameCurrency),
            Optional("Obs_comerciales", v.CommercialNotes),
            new XElement(ns + "Imp_total", v.Total),
            Optional("Obs", v.Notes),
            v.Associated is null ? null : new XElement(ns + "Cmps_asoc", v.Associated.Select(a => new XElement(ns + "Cmp_asoc",
                new XElement(ns + "Cbte_tipo", a.Type), new XElement(ns + "Cbte_punto_vta", a.PointOfSale),
                new XElement(ns + "Cbte_nro", a.Number), new XElement(ns + "Cbte_cuit", a.Cuit)))),
            Optional("Forma_pago", v.PaymentTerms),
            Optional("Incoterms", v.Incoterms),
            Optional("Incoterms_Ds", v.IncotermsText),
            new XElement(ns + "Idioma_cbte", v.Language),
            v.Items is null ? null : new XElement(ns + "Items", v.Items.Select(i => new XElement(ns + "Item",
                Optional("Pro_codigo", i.Code), Optional("Pro_ds", i.Description),
                new XElement(ns + "Pro_qty", i.Quantity), new XElement(ns + "Pro_umed", i.Unit),
                new XElement(ns + "Pro_precio_uni", i.UnitPrice), new XElement(ns + "Pro_bonificacion", i.Discount),
                new XElement(ns + "Pro_total_item", i.Total)))),
            new XElement(ns + "Fecha_cbte_cae", found.AuthorizedOn),
            new XElement(ns + "Fch_venc_Cae", found.CaeDue),
            new XElement(ns + "Cae", found.Cae),
            new XElement(ns + "Resultado", "A"),
            v.Optionals is null ? null : new XElement(ns + "Opcionales", v.Optionals.Select(o => new XElement(ns + "Opcional",
                Optional("Id", o.Id), Optional("Valor", o.Value)))),
            Optional("Fecha_pago", v.PaymentDate),
            v.Activities is null ? null : new XElement(ns + "Actividades", v.Activities.Select(a => new XElement(ns + "Actividad",
                new XElement(ns + "Id", a))))));
    }

    private async Task<ContractAnswer> LastVoucherAsync(ServiceCall call, CancellationToken ct)
    {
        var auth = call.Request.Child("Auth");
        var type = auth.IntOf("Cbte_Tipo");
        var pointOfSale = auth.IntOf("Pto_venta");
        if (type is not (19 or 20 or 21)) return Fail(call, 1606);
        if (!await ExportPointOfSaleAsync(call.Cuit, pointOfSale, ct)) return Fail(call, 1607);

        var last = await _store.LastAsync(call.Cuit, pointOfSale, type, ct);
        var ns = Ns(call);
        return Answer(call, new XElement(ns + "FEXResult_LastCMP",
            new XElement(ns + "Cbte_nro", last?.Voucher.Number ?? 0),
            last is null ? null : new XElement(ns + "Cbte_fecha", last.Date)));
    }

    private async Task<ContractAnswer> PointsOfSaleAsync(ServiceCall call, CancellationToken ct)
    {
        var issuer = await _taxpayers.FindAsync(call.Cuit, ct);
        var points = (issuer?.PointsOfSale ?? []).Where(p => p.Kind == PointOfSaleKind.WebServiceCae).OrderBy(p => p.Number);
        return Table(call, points, "ClsFEXResponse_PtoVenta", p =>
            [("Pve_Nro", p.Number), ("Pve_Bloqueado", p.Blocked ? "S" : "N"), ("Pve_FchBaja", p.DeactivatedOn is { } day ? Fev1Dates.Format(day) : "NULL")]);
    }

    private async Task<ContractAnswer> ActivitiesAsync(ServiceCall call, CancellationToken ct)
    {
        var issuer = await _taxpayers.FindAsync(call.Cuit, ct);
        var activities = issuer is null ? [] : new[] { PadronDirectory.ActivityOf(issuer) };
        return Table(call, activities, "ClsFEXResponse_ActividadTipo", a => [("Id", a.Id), ("Orden", 1), ("Desc", a.Description)]);
    }

    /// <summary>The currencies with an official rate on the day asked for: those ArcaSim has (IExchangeRates), and pesos at 1.</summary>
    private async Task<ContractAnswer> RatedCurrenciesAsync(ServiceCall call, CancellationToken ct)
    {
        if (!Fev1Dates.TryParse(call.Request.Str("Fecha_CTZ"), out var day)) return Fail(call, 2054);
        var rated = new List<(string Id, string Desc, decimal Rate, DateOnly Day)>();
        foreach (var currency in _tables.Currencies)
        {
            if (currency.Id == "PES") rated.Add((currency.Id, currency.Desc, 1, day));
            else if (await _rates.RateAsync(currency.Id, day, ct) is { } rate) rated.Add((currency.Id, currency.Desc, rate.Rate, rate.Day));
        }
        return Table(call, rated, "ClsFEXResponse_Mon_CON_Cotizacion",
            r => [("Mon_Id", r.Id), ("Mon_Ds", r.Desc), ("Mon_ctz", r.Rate), ("Fecha_ctz", Fev1Dates.Format(r.Day))]);
    }

    private async Task<ContractAnswer> RateAsync(ServiceCall call, CancellationToken ct)
    {
        var currency = call.Request.Str("Mon_id");
        var asked = call.Request.Str("FchCotiz");
        DateOnly day = Fev1Dates.PreviousBusinessDay(_clock.Today());
        if (!string.IsNullOrEmpty(asked) && !DateOnly.TryParseExact(asked, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
            return Fail(call, 1003);
        if (string.IsNullOrEmpty(currency) || !_tables.HasCurrency(currency))
            return Fail(call, 1014, "El campo Mon_id no es valido.");

        var ns = Ns(call);
        var rate = currency == "PES" ? (1m, day) : await _rates.RateAsync(currency, day, ct);
        if (rate is not { } found) return Fail(call, 1014, "No existe cotizacion para la moneda y la fecha informadas.");
        return Answer(call, new XElement(ns + "FEXResultGet",
            new XElement(ns + "Mon_ctz", found.Item1),
            new XElement(ns + "Mon_fecha", Fev1Dates.Format(found.Item2))));
    }

    private ContractAnswer CheckPermit(ServiceCall call)
    {
        var permit = call.Request.Str("ID_Permiso");
        var destination = call.Request.IntOf("Dst_merc");
        if (string.IsNullOrEmpty(permit) || !_tables.Countries.Any(c => c.Id == destination))
            return Fail(call, 1810, Wsfexv1Tables.PermitCheckText);
        var exists = System.Text.RegularExpressions.Regex.IsMatch(permit, "^[0-9]{5}[A-Z]{2}[A-Z0-9]{2}[0-9]{6}[A-Z]$");
        return Answer(call, new XElement(Ns(call) + "FEXResultGet", new XElement(Ns(call) + "Status", exists ? "OK" : "NO")));
    }

    // ---- Answers ---------------------------------------------------------------------

    private static XNamespace Ns(ServiceCall call) => call.Operation.Output.Namespace;

    /// <summary>{Op}Result with the data, FEXErr 0 and FEXEvents 0.</summary>
    private static ContractAnswer Answer(ServiceCall call, XElement data) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(Ns(call) + (call.Name + "Result"), data, Err(call, 0, "OK"), Events(call))));

    private static ContractAnswer Fail(ServiceCall call, int code, string? text = null) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(Ns(call) + (call.Name + "Result"),
            Err(call, code, text ?? Wsfexv1Tables.Text(code)), Events(call))));

    private static XElement Err(ServiceCall call, int code, string text) =>
        new(Ns(call) + "FEXErr", new XElement(Ns(call) + "ErrCode", code), new XElement(Ns(call) + "ErrMsg", text));

    private static XElement Events(ServiceCall call) =>
        new(Ns(call) + "FEXEvents", new XElement(Ns(call) + "EventCode", 0), new XElement(Ns(call) + "EventMsg", "Ok"));

    /// <summary>A FEXGetPARAM_* list: one item per row, its fields in schema order.</summary>
    private static ContractAnswer Table<T>(ServiceCall call, IEnumerable<T> rows, string item, Func<T, (string Name, object Value)[]> fields)
    {
        var ns = Ns(call);
        return Answer(call, new XElement(ns + "FEXResultGet", rows.Select(row =>
            new XElement(ns + item, fields(row).Select(f => new XElement(ns + f.Name, ContractXml.Format(f.Value)))))));
    }
}
