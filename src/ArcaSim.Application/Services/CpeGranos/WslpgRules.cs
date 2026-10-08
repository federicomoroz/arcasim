using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using static ArcaSim.Application.Services.CpeGranos.WslpgTables;

namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>A document wslpg authorized: a liquidación, an adjustment, a secondary liquidación or a certificate, with its COE, state and what was asked and answered.</summary>
public sealed record StoredSettlement(
    long Coe,
    string Kind,
    long Cuit,
    long PointOfIssue,
    long OrderNumber,
    string State,
    DateTimeOffset AuthorizedAt,
    string Request,
    string Authorization,
    long? AdjustedCoe = null,
    long? UsedBy = null);

public sealed record SettlementCoe(long Coe);

public sealed record SettlementLast(long Number);

/// <summary>
/// Liquidación primaria de granos (docs/arca/servicios/wslpg.md): primary
/// liquidaciones with their COE, unified adjustments, the contradocumento,
/// secondary liquidaciones and grain certificates, each numbered "último + 1"
/// per CUIT and punto de emisión (1508 otherwise), read back by COE or by
/// number, voided with resultado A or R, and the parameter tables the manual
/// prints. Errors go in the return element's errores block.
/// Amounts follow the manual's examples (§2.4.2.4): price per ton = precioRefTn
/// x factorEnt / 100 + precioFleteTn, precioOperacion per kilo, IVA and
/// retenciones over their bases, net = with IVA - retenciones - deducciones,
/// IVA RG 4310 = IVA - retención RI.
/// ArcaSim's choices where the spec says NO VERIFICADO or says nothing: the
/// adjustments and contradocumentos share the LPG numbering; LSG and CG keep
/// their own; COE prefixes 3301 (LPG), 3302 (adjustments), 3310 (LSG) and
/// 3320 (CG); several active adjustments may exist over one COE (historial
/// v1.22), so 1909 is never sent; an adjustment's amounts are the importes it
/// declares; a contradocumento starts in PA and liquidacionXCoeConsultar
/// reads it; every retención counts as ARCA's (totalRetencionAfip).
/// </summary>
public sealed class WslpgRules(IDocumentStore store, IClock clock, SequenceLocks locks) : IServiceBehavior
{
    private const string Collection = "wslpg";
    private const string OrderCollection = "wslpg-orden";
    private const string LastCollection = "wslpg-ultimo";

    private const string Primary = "LPG";
    private const string Adjustment = "AJUSTE";
    private const string Counterdocument = "CONTRADOC";
    private const string Secondary = "LSG";
    private const string Certificate = "CG";

    public string Service => "wslpg";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "liquidacionUltimoNroOrdenConsultar" => await LastOrderAsync(call, Primary, ct),
        "lsgConsultarUltimoNroOrden" => await LastOrderAsync(call, Secondary, ct),
        "cgConsultarUltimoNroOrden" => await LastOrderAsync(call, Certificate, ct),
        "liquidacionAutorizar" => await AuthorizePrimaryAsync(call, ct),
        "liquidacionXCoeConsultar" => await PrimaryByCoeAsync(call, ct),
        "liquidacionXNroOrdenConsultar" => await PrimaryByNumberAsync(call, ct),
        "liquidacionAnular" => await VoidAsync(call, Primary, Codes.VoidOnlyOwn, ct),
        "lpgAnularContraDocumento" => await CounterdocumentAsync(call, ct),
        "liquidacionAjustarUnificado" => await AdjustAsync(call, ct),
        "ajusteXCoeConsultar" => await AdjustmentByCoeAsync(call, ct),
        "ajusteXNroOrdenConsultar" => await AdjustmentByNumberAsync(call, ct),
        "lsgAutorizar" => await AuthorizeSecondaryAsync(call, ct),
        "lsgConsultarXCoe" => await SecondaryAsync(call, await ByCoeAsync(call.Request.Long("coe"), ct), ct),
        "lsgConsultarXNroOrden" => await SecondaryAsync(call, await ByNumberAsync(Secondary, call, ct), ct),
        "lsgAnular" => await VoidAsync(call, Secondary, Codes.OtherCuit, ct),
        "cgAutorizar" => await AuthorizeCertificateAsync(call, ct),
        "cgConsultarXCoe" => CertificateAnswer(call, await ByCoeAsync(call.Request.Long("coe"), ct)),
        "cgConsultarXNroOrden" => CertificateAnswer(call, await ByNumberAsync(Certificate, call, ct)),
        "cgSolicitarAnulacion" => await CertificateVoidAsync(call, confirm: false, ct),
        "cgConfirmarAnulacion" => await CertificateVoidAsync(call, confirm: true, ct),
        "campaniasConsultar" => Table(call, "campanias", Campaigns(clock.Now.ToArgentina())),
        "provinciasConsultar" => Table(call, "provincias", WscpeTables.Provinces.Select(p => (p.Code.ToString(CultureInfo.InvariantCulture), p.Name))),
        "codigoGradoEntregadoXTipoGranoConsultar" => DeliveredGrades(call),
        _ when Lists.TryGetValue(call.Name, out var list) => Table(call, list.List, list.Rows),
        _ => null,
    };

    // ---- Numbering -------------------------------------------------------------------

    private async Task<ContractAnswer> LastOrderAsync(ServiceCall call, string kind, CancellationToken ct)
    {
        var answer = call.Sample();
        answer.Set("nroOrden", await LastAsync(kind, call.Cuit, call.Request.Long("ptoEmision"), ct));
        return call.Ok(answer);
    }

    /// <summary>
    /// One operation at a time on one document, by its COE: a void, an
    /// adjustment, a counterdocument or the use of a certificate reads the
    /// document as the last one left it. The lock lives apart from the series'
    /// (its own name), and they nest in that order: the document, then the series.
    /// </summary>
    private Task<IDisposable> LockAsync(long coe, CancellationToken ct) => locks.AcquireAsync($"{Service}.coe", coe, 0, 0, ct);

    /// <summary>
    /// Numbers and stores a new document: the order number must be the last
    /// + 1 of its series, the COE is new, and nothing is kept when it fails.
    /// </summary>
    private async Task<ContractAnswer> IssueAsync(
        ServiceCall call, string kind, long pointOfIssue, long order, XElement request,
        Func<long, DateTimeOffset, (XElement Answer, XElement Authorization)> build, CancellationToken ct, long? adjusted = null)
    {
        using var _ = await locks.AcquireAsync(Service, call.Cuit, (int)Math.Clamp(pointOfIssue, 0, int.MaxValue), SeriesCode(kind), ct);
        if (order != await LastAsync(kind, call.Cuit, pointOfIssue, ct) + 1) return call.Error(1508, Codes.NotConsecutive);
        var now = clock.Now;
        var coe = long.Parse(CoePrefix(kind) + (await store.NextAsync("wslpg-coe", ct) % 100_000_000).ToString("D8", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var (answer, authorization) = build(coe, now);
        var document = new StoredSettlement(coe, kind, call.Cuit, pointOfIssue, order, kind == Counterdocument ? "PA" : "AC", now,
            Strip(request).ToString(SaveOptions.DisableFormatting), authorization.ToString(SaveOptions.DisableFormatting), adjusted);
        await SaveAsync(document, ct);
        await store.PutAsync(OrderCollection, OrderKey(kind, call.Cuit, pointOfIssue, order), new SettlementCoe(coe), ct);
        await store.PutAsync(LastCollection, LastKey(kind, call.Cuit, pointOfIssue), new SettlementLast(order), ct);
        return call.Ok(answer);
    }

    // ---- Primary liquidación ---------------------------------------------------------

    private async Task<ContractAnswer> AuthorizePrimaryAsync(ServiceCall call, CancellationToken ct)
    {
        var liquidation = call.Request.Child("liquidacion") ?? new XElement("liquidacion");
        if (liquidation.Text("esLiquidacionPropia") == "N" && liquidation.Text("actuaCorredor") == "S" && liquidation.Long("cuitCorredor") == 0)
            return call.Error(1618, Codes.BrokerRequired);
        var pointOfIssue = liquidation.Long("ptoEmision");
        var order = liquidation.Long("nroOrden");
        return await IssueAsync(call, Primary, pointOfIssue, order, call.Request, (coe, now) =>
        {
            var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
            var authorization = Return(fill.Root).Child("autorizacion")!;
            var totals = Settlement.ForPrimary(call.Request, liquidation);
            Put(fill, authorization, "ptoEmision", pointOfIssue);
            Put(fill, authorization, "nroOrden", order);
            Put(fill, authorization, "codTipoOperacion", liquidation.Text("codTipoOperacion"));
            Put(fill, authorization, "nroOpComercial", 0);
            Put(fill, authorization, "fechaLiquidacion", GrainsFormat.Date(now));
            Put(fill, authorization, "precioOperacion", GrainsFormat.Amount(totals.Price, 3));
            Put(fill, authorization, "totalPesoNeto", totals.Weight);
            Settlement.Write(fill, authorization, totals);
            Put(fill, authorization, "coe", coe);
            if (liquidation.Long("numeroContrato") > 0) Put(fill, authorization, "numeroContrato", liquidation.Long("numeroContrato"));
            Put(fill, authorization, "estado", "AC");
            fill.Done();
            return (fill.Root, authorization);
        }, ct);
    }

    private async Task<ContractAnswer> PrimaryByCoeAsync(ServiceCall call, CancellationToken ct)
    {
        var document = await ByCoeAsync(call.Request.Long("coe"), ct);
        if (document is null) return call.Error(600, Codes.NoData);
        if (document.Cuit != call.Cuit) return call.Error(1510, Codes.OtherCuit);
        if (document.Kind == Adjustment) return call.Error(1861, Codes.OriginalOnly);
        if (document.Kind is not (Primary or Counterdocument)) return call.Error(1723, Codes.NotPrimary);
        return call.Ok(PrimaryAnswer(call, document, Wants(call)));
    }

    private async Task<ContractAnswer> PrimaryByNumberAsync(ServiceCall call, CancellationToken ct)
    {
        var document = await ByNumberAsync(Primary, call, ct);
        if (document is null) return call.Error(600, Codes.NoData);
        if (document.Kind == Adjustment) return call.Error(1861, Codes.OriginalOnly);
        return call.Ok(PrimaryAnswer(call, document, Wants(call)));
    }

    /// <summary>LpgLiqConsReturnType: the liquidación as it was sent and its autorización as it is now.</summary>
    private XElement PrimaryAnswer(ServiceCall call, StoredSettlement document, bool pdf)
    {
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        fill.Merge(result.Child("liquidacion"), XElement.Parse(document.Request).Child("liquidacion"));
        if (result.Child("autorizacion") is { } authorization)
        {
            fill.Merge(authorization, XElement.Parse(document.Authorization));
            Put(fill, authorization, "estado", document.State);
        }
        Pdf(fill, result, pdf, document);
        return fill.Done();
    }

    /// <summary>
    /// liquidacionAnular and lsgAnular: AC to AN, answered with resultado A,
    /// or R with the reason in errores (§2.4.31.5). It cannot be voided after
    /// the 15th of the month after it was authorized, nor with an active
    /// adjustment over it (1519).
    /// </summary>
    private async Task<ContractAnswer> VoidAsync(ServiceCall call, string kind, string otherCuit, CancellationToken ct)
    {
        var coe = call.Request.Long("coe");
        using var gate = await LockAsync(coe, ct);
        var document = await ByCoeAsync(coe, ct);
        (long Code, string Text)? refusal = document switch
        {
            null => (600, Codes.NoData),
            _ when document.Cuit != call.Cuit => (1510, otherCuit),
            _ when document.Kind != kind => (1519, Codes.CannotVoid),
            _ when document.State == "AN" => (1527, Codes.AlreadyVoided),
            _ when clock.Now.ToArgentina() > VoidDeadline(document.AuthorizedAt) => (1519, Codes.CannotVoid),
            _ => null,
        };
        if (refusal is null && (await store.ListAsync<StoredSettlement>(Collection, "", ct))
            .Any(d => d.AdjustedCoe == coe && d.State is "AC" or "PA"))
            refusal = (1519, Codes.CannotVoid);

        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        Put(fill, result, "coe", coe);
        Put(fill, result, "resultado", refusal is null ? "A" : "R");
        if (refusal is { } r)
        {
            result.Child("pdf")?.Remove();
            fill.Done();
            var rejection = call.Error(r.Code, r.Text);
            // The refusal's error block goes into the answer built here; a response with none is refused with the fault.
            if (rejection.Body?.Descendants("errores").FirstOrDefault() is not { } errors) return rejection;
            result.Add(new XElement(errors));
            return call.Ok(fill.Root);
        }
        var voided = document! with { State = "AN" };
        await SaveAsync(voided, ct);
        Pdf(fill, result, call.Request.Text("pdf") != "N", voided);
        return call.Ok(fill.Done());
    }

    private static DateTimeOffset VoidDeadline(DateTimeOffset authorizedAt)
    {
        var local = authorizedAt.ToArgentina();
        var next = new DateTime(local.Year, local.Month, 1).AddMonths(1).AddDays(15);
        return new DateTimeOffset(next, ArgentinaTime.Offset).AddTicks(-1);
    }

    /// <summary>
    /// A new document with its own number and COE that cancels the original
    /// "like a unified adjustment" (§2.4.6.2): it starts in PA, and the
    /// original only turns AN when the seller accepts it on the web, which no
    /// operation of the service does.
    /// </summary>
    private async Task<ContractAnswer> CounterdocumentAsync(ServiceCall call, CancellationToken ct)
    {
        var basis = call.Request.Child("anulacionBase") ?? new XElement("anulacionBase");
        using var gate = await LockAsync(basis.Long("coeAnular"), ct);
        var original = await ByCoeAsync(basis.Long("coeAnular"), ct);
        if (original is null) return call.Error(600, Codes.NoData);
        if (original.Cuit != call.Cuit) return call.Error(1510, Codes.VoidOnlyOwn);
        if (original.Kind != Primary) return call.Error(1723, Codes.NotPrimary);
        if (original.State == "AN" || (await store.ListAsync<StoredSettlement>(Collection, "", ct))
                .Any(d => d.Kind == Counterdocument && d.AdjustedCoe == original.Coe))
            return call.Error(1527, Codes.AlreadyVoided);

        var pointOfIssue = basis.Long("puntoEmision");
        var order = basis.Long("nroOrden");
        var request = XElement.Parse(original.Request);
        if (request.Child("liquidacion") is { } liquidation)
        {
            liquidation.Child("ptoEmision")?.SetValue(pointOfIssue);
            liquidation.Child("nroOrden")?.SetValue(order);
        }
        return await IssueAsync(call, Counterdocument, pointOfIssue, order, request, (coe, now) =>
        {
            var authorization = XElement.Parse(original.Authorization);
            authorization.Child("ptoEmision")?.SetValue(pointOfIssue);
            authorization.Child("nroOrden")?.SetValue(order);
            authorization.Child("fechaLiquidacion")?.SetValue(GrainsFormat.Date(now));
            authorization.Child("coe")?.SetValue(coe);
            authorization.Child("coe")?.AddAfterSelf(new XElement("coeAjustado", original.Coe));
            var document = original with { Coe = coe, Kind = Counterdocument, State = "PA", Request = request.ToString(), Authorization = authorization.ToString() };
            var answer = PrimaryAnswer(call, document, pdf: true);
            return (answer, authorization);
        }, ct, adjusted: original.Coe);
    }

    // ---- Adjustments -----------------------------------------------------------------

    private async Task<ContractAnswer> AdjustAsync(ServiceCall call, CancellationToken ct)
    {
        var basis = call.Request.Child("ajusteBase") ?? new XElement("ajusteBase");
        using var gate = await LockAsync(basis.Long("coeAjustado"), ct);
        var adjusted = await ByCoeAsync(basis.Long("coeAjustado"), ct);
        if (adjusted is null || adjusted.State != "AC" || adjusted.Kind is Secondary or Certificate)
            return call.Error(1908, Codes.AdjustedMustExist);
        if (adjusted.Kind != Primary) return call.Error(1911, Codes.AdjustedNotAdjustment);
        if (adjusted.Cuit != call.Cuit) return call.Error(1510, Codes.AdjustedSameCuit);

        var pointOfIssue = basis.Long("ptoEmision");
        var order = basis.Long("nroOrden");
        var original = XElement.Parse(adjusted.Request).Child("liquidacion") ?? new XElement("liquidacion");
        return await IssueAsync(call, Adjustment, pointOfIssue, order, call.Request, (coe, now) =>
        {
            var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
            var unified = Return(fill.Root).Child("ajusteUnificado")!;
            Put(fill, unified, "ptoEmision", pointOfIssue);
            Put(fill, unified, "nroOrden", order);
            if (original.Long("numeroContrato") > 0) Put(fill, unified, "nroContrato", original.Long("numeroContrato"));
            Put(fill, unified, "coeAjustado", adjusted.Coe);
            Put(fill, unified, "codTipoOperacion", original.Text("codTipoOperacion"));
            var credit = Side(fill, unified, call.Request.Child("ajusteCredito"), "ajusteCredito", now);
            var debit = Side(fill, unified, call.Request.Child("ajusteDebito"), "ajusteDebito", now);
            if (unified.Child("totalesUnificados") is { } totals) Settlement.WriteUnified(fill, totals, debit, credit);
            Put(fill, unified, "coe", coe);
            Put(fill, unified, "estado", "AC");
            fill.Done();
            return (fill.Root, unified);
        }, ct, adjusted: adjusted.Coe);
    }

    /// <summary>One side of the adjustment; a side the request leaves out comes back in zeros, as the schema requires both.</summary>
    private static Settlement.Totals Side(AnswerFill fill, XElement unified, XElement? request, string name, DateTimeOffset now)
    {
        request ??= new XElement(name);
        var totals = Settlement.ForAdjustment(request);
        if (unified.Child(name) is not { } side) return totals;
        Put(fill, side, "nroOpComercial", 0);
        Put(fill, side, "fechaLiquidacion", GrainsFormat.Date(now));
        Put(fill, side, "precioOperacion", GrainsFormat.Amount(request.Decimal("diferenciaPrecioOperacion"), 3));
        Put(fill, side, "totalPesoNeto", (long)request.Decimal("diferenciaPesoNeto"));
        Settlement.Rows(side.Child("importes"), "importeReturn", totals.Importes, (row, line) =>
        {
            Put(fill, row, "importe", GrainsFormat.Amount(line.Amount));
            Put(fill, row, "concepto", line.Source.Value.Trim());
            Put(fill, row, "alicuota", line.Base);
            Put(fill, row, "ivaCalculado", GrainsFormat.Amount(line.Vat));
        });
        Settlement.Write(fill, side, totals);
        return totals;
    }

    private async Task<ContractAnswer> AdjustmentByCoeAsync(ServiceCall call, CancellationToken ct)
    {
        var document = await ByCoeAsync(call.Request.Long("coe"), ct);
        if (document is null) return call.Error(600, Codes.NoData);
        if (document.Cuit != call.Cuit) return call.Error(1510, Codes.OtherCuit);
        if (document.Kind != Adjustment) return call.Error(1649, Codes.AdjustmentOnly);
        return call.Ok(AdjustmentAnswer(call, document, Wants(call)));
    }

    private async Task<ContractAnswer> AdjustmentByNumberAsync(ServiceCall call, CancellationToken ct)
    {
        var document = await ByNumberAsync(Primary, call, ct);
        if (document is null) return call.Error(600, Codes.NoData);
        if (document.Kind != Adjustment) return call.Error(1649, Codes.AdjustmentOnly);
        return call.Ok(AdjustmentAnswer(call, document, pdf: false));
    }

    private XElement AdjustmentAnswer(ServiceCall call, StoredSettlement document, bool pdf)
    {
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        if (result.Child("ajusteUnificado") is { } unified)
        {
            fill.Merge(unified, XElement.Parse(document.Authorization));
            Put(fill, unified, "estado", document.State);
        }
        Pdf(fill, result, pdf, document);
        return fill.Done();
    }

    // ---- Secondary liquidación ---------------------------------------------------------

    private async Task<ContractAnswer> AuthorizeSecondaryAsync(ServiceCall call, CancellationToken ct)
    {
        var basis = call.Request.Child("liqSecundariaBase") ?? new XElement("liqSecundariaBase");
        if (basis.Text("actuaCorredor") == "S" && basis.Long("cuitCorredor") == 0) return call.Error(1618, Codes.BrokerRequired);
        var pointOfIssue = basis.Long("ptoEmision");
        var order = basis.Long("nroOrden");
        return await IssueAsync(call, Secondary, pointOfIssue, order, call.Request, (coe, now) =>
        {
            var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
            var authorization = Return(fill.Root).Child("autorizacion")!;
            var subtotal = GrainsFormat.Round(basis.Decimal("cantidadTn") * basis.Decimal("precioOperacion"));
            var vat = GrainsFormat.Round(subtotal * basis.Decimal("alicIvaOperacion") / 100);
            var deductions = basis.Elements("deduccion").Sum(d => GrainsFormat.Round(d.Decimal("baseCalculo") * (1 + d.Decimal("alicuotaIVA") / 100)));
            var perceptions = basis.Elements("percepcion").Sum(p => GrainsFormat.Round(p.Decimal("baseCalculo") * p.Decimal("alicuota") / 100));
            Put(fill, authorization, "ptoEmision", pointOfIssue);
            Put(fill, authorization, "nroOrden", order);
            Put(fill, authorization, "fechaLiquidacion", GrainsFormat.Date(now));
            Put(fill, authorization, "subTotal", GrainsFormat.Amount(subtotal));
            Put(fill, authorization, "importeIva", GrainsFormat.Amount(vat));
            Put(fill, authorization, "operacionConIva", GrainsFormat.Amount(subtotal + vat));
            Put(fill, authorization, "coe", coe);
            Put(fill, authorization, "totalDeducciones", GrainsFormat.Amount(deductions));
            Put(fill, authorization, "totalPercepciones", GrainsFormat.Amount(perceptions));
            fill.Done();
            return (fill.Root, authorization);
        }, ct);
    }

    /// <summary>The names an LSG keeps in the consult that differ from the ones it was authorized with.</summary>
    private static readonly Dictionary<string, string> SecondaryNames = new()
    {
        ["cantidadTn"] = "pesoNetoEnTn",
        ["campaniaPPal"] = "campania",
        ["desPuertoLocalidad"] = "descripcionPuertoLocalidad",
        ["precioRefTn"] = "precioReferenciaTn",
        ["precioOperacion"] = "precioOperacionTn",
        ["alicIvaOperacion"] = "alicuotaIvaOperacion",
        ["codProvincia"] = "codProvinciaOperacion",
        ["codLocalidad"] = "codLocalidadOperacion",
        ["numeroContrato"] = "nroContrato",
    };

    private Task<ContractAnswer> SecondaryAsync(ServiceCall call, StoredSettlement? document, CancellationToken ct)
    {
        if (document is null || document.Kind != Secondary) return Task.FromResult(call.Error(600, Codes.NoData));
        if (document.Cuit != call.Cuit) return Task.FromResult(call.Error(1510, Codes.OtherCuit));

        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        var request = XElement.Parse(document.Request);
        var basis = request.Child("liqSecundariaBase") ?? new XElement("liqSecundariaBase");
        var stored = XElement.Parse(document.Authorization);
        if (result.Child("liquidaciones") is { } entry)
        {
            if (entry.Child("liquidacion") is { } liquidation)
            {
                var renamed = new XElement("liquidacion", basis.Elements().Select(e =>
                    SecondaryNames.TryGetValue(e.Name.LocalName, out var name) ? new XElement(name, e.Nodes()) : new XElement(e)));
                fill.Merge(liquidation, renamed);
                fill.Merge(liquidation.Child("facturaPapel"), request.Child("facturaPapel"));
                Put(fill, liquidation, "totalDeducciones", stored.Text("totalDeducciones"));
                Put(fill, liquidation, "todalPercepciones", stored.Text("totalPercepciones"));
                Put(fill, liquidation, "estado", document.State);
            }
            if (entry.Child("autorizacion") is { } authorization)
            {
                fill.Merge(authorization, stored);
                Put(fill, authorization, "subtotal", stored.Text("subTotal"));
                Put(fill, authorization, "precioOperacion", basis.Text("precioOperacion"));
            }
            entry.Child("ajuste")?.Remove();
        }
        Pdf(fill, result, Wants(call), document);
        return Task.FromResult(call.Ok(fill.Done()));
    }

    // ---- Certificates ----------------------------------------------------------------

    private async Task<ContractAnswer> AuthorizeCertificateAsync(ServiceCall call, CancellationToken ct)
    {
        var header = call.Request.Child("cabecera") ?? new XElement("cabecera");
        var pointOfIssue = header.Long("ptoEmision");
        var order = header.Long("nroOrden");
        var answer = await IssueAsync(call, Certificate, pointOfIssue, order, call.Request, (coe, now) =>
        {
            var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
            var authorization = Return(fill.Root).Child("autorizacion")!;
            Put(fill, authorization, "ptoEmision", pointOfIssue);
            Put(fill, authorization, "nroOrden", order);
            Put(fill, authorization, "coe", coe);
            Put(fill, authorization, "estado", "AC");
            Put(fill, authorization, "fechaCertificacion", GrainsFormat.Date(now));
            if (call.Request.Child("primaria") is { } primary && authorization.Child("pesosResumen") is { } weights)
            {
                var loads = primary.Elements().Where(e => e.Name.LocalName is "ctg" or "cartaPorteFerroviaria").ToList();
                var gross = loads.Sum(l => l.Decimal("pesoNetoConfirmadoDefinitivo"));
                var drying = loads.Sum(l => l.Decimal("pesoNetoMermaSecado"));
                var sifting = loads.Sum(l => l.Decimal("pesoNetoMermaZarandeo"));
                var volatile_ = primary.Decimal("pesoNetoMermaVolatil");
                Put(fill, weights, "pesoBrutoCertificado", gross);
                Put(fill, weights, "pesoMermaVolatil", volatile_);
                Put(fill, weights, "pesoMermaSecado", drying);
                Put(fill, weights, "pesoMermaZarandeo", sifting);
                Put(fill, weights, "pesoNetoCertificado", gross - drying - sifting - volatile_);
            }
            fill.Done();
            return (fill.Root, authorization);
        }, ct);

        // A retiro or transferencia uses the deposit certificates it names: they can no longer be voided (3500).
        if (answer.Body?.Descendants("coe").FirstOrDefault() is { } issued && answer.Body.Descendants("errores").FirstOrDefault() is null)
            foreach (var used in call.Request.Child("retiroTransferencia")?.Elements("certificadoDeposito") ?? [])
            {
                var coe = used.Long("coeCertificadoDeposito");
                using var gate = await LockAsync(coe, ct);
                if (await ByCoeAsync(coe, ct) is { Kind: Certificate } deposit)
                    await SaveAsync(deposit with { UsedBy = long.Parse(issued.Value, CultureInfo.InvariantCulture) }, ct);
            }
        return answer;
    }

    private ContractAnswer CertificateAnswer(ServiceCall call, StoredSettlement? document)
    {
        if (document is null || document.Kind != Certificate) return call.Error(600, Codes.NoData);
        if (document.Cuit != call.Cuit && Depositor(document) != call.Cuit) return call.Error(1510, Codes.OtherCuit);
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        var request = XElement.Parse(document.Request);
        fill.Merge(result.Child("autorizacion"), XElement.Parse(document.Authorization));
        if (result.Child("autorizacion") is { } authorization) Put(fill, authorization, "estado", document.State);
        foreach (var block in new[] { "cabecera", "primaria", "retiroTransferencia", "preexistente" })
            fill.Merge(result.Child(block), request.Child(block));
        Put(fill, result, "cuitDepositario", document.Cuit);
        Pdf(fill, result, Wants(call), document);
        return call.Ok(fill.Done());
    }

    /// <summary>
    /// cgSolicitarAnulacion (the depositario): AC to AN up to the 15th of the
    /// month after the certification, to PA after it. cgConfirmarAnulacion
    /// (the depositante): PA to AN (§2.4.40, §2.4.41).
    /// </summary>
    private async Task<ContractAnswer> CertificateVoidAsync(ServiceCall call, bool confirm, CancellationToken ct)
    {
        var coe = call.Request.Long("coe");
        using var gate = await LockAsync(coe, ct);
        var document = await ByCoeAsync(coe, ct);
        if (document is null || document.Kind != Certificate) return call.Error(600, Codes.NoData);
        if (call.Cuit != (confirm ? Depositor(document) : document.Cuit)) return call.Error(3502, Codes.CertificatePermission);
        if (!confirm && document.UsedBy is not null) return call.Error(3500, Codes.CertificateInUse);
        if (document.State != (confirm ? "PA" : "AC")) return call.Error(3501, Codes.CertificateTransition);

        var state = confirm || clock.Now.ToArgentina() <= VoidDeadline(document.AuthorizedAt) ? "AN" : "PA";
        var changed = document with { State = state };
        await SaveAsync(changed, ct);
        var fill = new AnswerFill(call.Sample(), call.Contract.Schemas);
        var result = Return(fill.Root);
        Put(fill, result, "estadoCertificado", state);
        Pdf(fill, result, Wants(call), changed);
        return call.Ok(fill.Done());
    }

    private static long Depositor(StoredSettlement document) =>
        XElement.Parse(document.Request).Child("cabecera")?.Long("cuitDepositante") ?? 0;

    // ---- Parameter tables ------------------------------------------------------------

    private static ContractAnswer Table(ServiceCall call, string list, IEnumerable<(string Code, string Description)> rows)
    {
        var answer = call.Sample();
        Return(answer).Child(list)?.Repeat("codigoDescripcion", rows, (e, row) => e.Set("codigo", row.Code).Set("descripcion", row.Description));
        return call.Ok(answer);
    }

    private static ContractAnswer DeliveredGrades(ServiceCall call)
    {
        var answer = call.Sample();
        Return(answer).Child("gradoEnt")?.Child("gradoEnt")?.Repeat("gradoEnt", WslpgTables.DeliveredGrades, (e, grade) => e
            .Set("codigo", grade.Code).Set("descripcion", grade.Description).Set("valor", grade.Value));
        return call.Ok(answer);
    }

    // ---- Store -----------------------------------------------------------------------

    private async Task<long> LastAsync(string kind, long cuit, long pointOfIssue, CancellationToken ct) =>
        (await store.GetAsync<SettlementLast>(LastCollection, LastKey(kind, cuit, pointOfIssue), ct))?.Number ?? 0;

    private Task<StoredSettlement?> ByCoeAsync(long coe, CancellationToken ct) =>
        store.GetAsync<StoredSettlement>(Collection, coe.ToString(CultureInfo.InvariantCulture), ct);

    private async Task<StoredSettlement?> ByNumberAsync(string kind, ServiceCall call, CancellationToken ct) =>
        await store.GetAsync<SettlementCoe>(OrderCollection, OrderKey(kind, call.Cuit, call.Request.Long("ptoEmision"), call.Request.Long("nroOrden")), ct) is { } index
            ? await ByCoeAsync(index.Coe, ct)
            : null;

    private Task SaveAsync(StoredSettlement document, CancellationToken ct) =>
        store.PutAsync(Collection, document.Coe.ToString(CultureInfo.InvariantCulture), document, ct);

    /// <summary>The series a document numbers in: adjustments and contradocumentos share the LPG one (§1.9.1).</summary>
    private static string Series(string kind) => kind switch { Secondary => "lsg", Certificate => "cg", _ => "lpg" };

    private static int SeriesCode(string kind) => kind switch { Secondary => 2, Certificate => 3, _ => 1 };

    private static string CoePrefix(string kind) => kind switch
    {
        Primary => "3301",
        Secondary => "3310",
        Certificate => "3320",
        _ => "3302",
    };

    private static string OrderKey(string kind, long cuit, long pointOfIssue, long order) =>
        $"{Series(kind)}/{cuit}/{pointOfIssue:D4}/{order:D8}";

    private static string LastKey(string kind, long cuit, long pointOfIssue) => $"{Series(kind)}/{cuit}/{pointOfIssue:D4}";

    // ---- Answers ---------------------------------------------------------------------

    /// <summary>The return element every answer wraps its data in (liqReturn, oReturn, liqConsReturn...).</summary>
    private static XElement Return(XElement answer) => answer.Elements().First();

    private static bool Wants(ServiceCall call) => string.Equals(call.Request.Text("pdf"), "S", StringComparison.OrdinalIgnoreCase);

    private static void Pdf(AnswerFill fill, XElement result, bool wanted, StoredSettlement document)
    {
        if (wanted && result.Child("pdf") is { } pdf)
            fill.Set(pdf, "pdf", GrainsFormat.Pdf($"Liquidacion de granos - COE {document.Coe} - {document.State}"));
    }

    internal static void Put(AnswerFill fill, XElement parent, string name, object? value)
    {
        if (parent.Child(name) is { } target) fill.Set(target, name, value);
    }

    private static XElement Strip(XElement request) =>
        new(request.Name.LocalName, request.Elements().Where(e => e.Name.LocalName != "auth").Select(GrainsFormat.Unqualified));
}
