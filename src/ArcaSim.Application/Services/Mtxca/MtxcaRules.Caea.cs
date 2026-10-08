using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Mtxca;

// wsmtxca's CAEA regime (docs/arca/servicios/wsmtxca.md, "CAEA (régimen y RG
// 5782/2025)"): solicitarCAEA grants one code per CUIT and fortnight,
// informarComprobanteCAEA takes each voucher issued with it, the two
// informarCAEANoUtilizado* declare it unused, and the queries answer from what
// was granted and reported.
public sealed partial class MtxcaRules
{
    private async Task<ContractAnswer> RequestCaeaAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request.Child("solicitudCAEA") ?? call.Request;
        var period = request.Int("periodo");
        var order = request.Int("orden") is >= short.MinValue and <= short.MaxValue and var asked ? (short)asked : short.MinValue;
        var today = clock.Today();
        var issuer = await IssuerAsync(call.Cuit, 0, PointOfSaleKind.WebServiceCaea, ct);

        var errors = new List<MtxcaFinding>();
        if (issuer is not { Active: true }) errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 10000));
        else
        {
            if (issuer.VatCondition != VatCondition.ResponsableInscripto) errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 10003));
            if (!settings.OpenAccess)
            {
                if (!issuer.PointsOfSale.Any(p => Usable(issuer, p.Number, PointOfSaleKind.WebServiceCaea, today)))
                    errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 10024));
                if (!issuer.PointsOfSale.Any(p => Usable(issuer, p.Number, PointOfSaleKind.WebServiceCae, today)))
                    errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 10027));
            }
        }
        var periodValid = period is >= 190_001 and <= 999_912 && period % 100 is >= 1 and <= 12;
        if (!periodValid) errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 600));
        if (order is not (1 or 2)) errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 601));
        (DateOnly From, DateOnly To)? fortnight = periodValid && order is 1 or 2 ? Fortnight(period, order) : null;
        if (fortnight is { } days && (today < days.From.AddDays(-5) || today > days.To)) errors.Add(MtxcaCodes.Error(MtxcaTable.Request, 602));
        if (errors.Count > 0 || fortnight is not { } granted) return CaeaErrors(call, errors);

        await _caeaGate.WaitAsync(ct);
        try
        {
            if (await _state.FindCaeaAsync(call.Cuit, period, order, ct) is not null)
                return CaeaErrors(call, [MtxcaCodes.Error(MtxcaTable.Request, 604)]);

            var code = long.Parse(codes.NextCaea(), CultureInfo.InvariantCulture);
            var caea = new MtxcaCaea(call.Cuit, period, order, code, granted.From, granted.To, granted.To.AddMonths(1), today,
                false, [], []);
            await _state.SaveCaeaAsync(caea, ct);
            events.Publish(new CaeaGranted(time.GetUtcNow(), call.Cuit, period, order, code.ToString(CultureInfo.InvariantCulture)));
            return call.Ok(new XElement(call.Operation.Output, CaeaElement(caea)));
        }
        finally
        {
            _caeaGate.Release();
        }
    }

    /// <summary>The days a valid fortnight covers: orden 1 is the 1st to the 15th, 2 the 16th to the end of the month.</summary>
    private static (DateOnly From, DateOnly To) Fortnight(int period, short order)
    {
        var first = new DateOnly(period / 100, period % 100, 1);
        return order == 1 ? (first, first.AddDays(14)) : (first.AddDays(15), first.AddMonths(1).AddDays(-1));
    }

    private static ContractAnswer CaeaErrors(ServiceCall call, IEnumerable<MtxcaFinding> errors) =>
        call.Ok(new XElement(call.Operation.Output, Codes("arrayErrores", errors)));

    private static XElement CaeaElement(MtxcaCaea caea) => new("CAEAResponse",
        new XElement("fechaProceso", Day(caea.ProcessedOn)),
        new XElement("CAEA", caea.Code),
        new XElement("periodo", caea.Period),
        new XElement("orden", caea.Order),
        new XElement("fechaDesde", Day(caea.From)),
        new XElement("fechaHasta", Day(caea.To)),
        new XElement("fechaTopeInforme", Day(caea.ReportDeadline)));

    /// <summary>The CAEA the request names, if it is the caller's; otherwise the query error 1300 or 1301.</summary>
    private async Task<(MtxcaCaea? Caea, int? Error)> OwnCaeaAsync(ServiceCall call, CancellationToken ct)
    {
        var caea = await _state.FindCaeaAsync(call.Request.Long("CAEA"), ct);
        if (caea is null) return (null, 1300);
        return caea.Cuit == call.Cuit ? (caea, null) : (null, 1301);
    }

    private async Task<ContractAnswer> ConsultCaeaAsync(ServiceCall call, CancellationToken ct)
    {
        var (caea, error) = await OwnCaeaAsync(call, ct);
        return caea is null
            ? QueryError(call, error!.Value)
            : call.Ok(new XElement(call.Operation.Output, CaeaElement(caea)));
    }

    private async Task<ContractAnswer> CaeasBetweenAsync(ServiceCall call, CancellationToken ct)
    {
        var from = call.Request.Date("fechaDesde") ?? DateOnly.MinValue;
        var to = call.Request.Date("fechaHasta") ?? DateOnly.MaxValue;
        if (from > to) return QueryError(call, 1400);

        var caeas = (await _state.CaeasOfAsync(call.Cuit, ct))
            .Where(c => c.From <= to && c.To >= from)
            .OrderBy(c => c.From);
        return call.Ok(new XElement(call.Operation.Output, new XElement("arrayCAEAResponse", caeas.Select(CaeaElement))));
    }

    private async Task<ContractAnswer> NotInformedAsync(ServiceCall call, CancellationToken ct)
    {
        var (caea, error) = await OwnCaeaAsync(call, ct);
        if (caea is null) return QueryError(call, error!.Value);

        var issuer = await taxpayers.FindAsync(call.Cuit, ct);
        var points = caea.Unused || issuer is null
            ? []
            : issuer.PointsOfSale
                .Where(p => p.Kind == PointOfSaleKind.WebServiceCaea && !(p.DeactivatedOn < caea.From))
                .Where(p => !caea.UsedPoints.Contains(p.Number) && !caea.UnusedPoints.Contains(p.Number))
                .OrderBy(p => p.Number)
                .ToList();
        return call.Ok(new XElement(call.Operation.Output, new XElement("arrayPuntosVenta", points.Select(PointOfSaleElement))));
    }

    /// <summary>informarCAEANoUtilizado, or informarCAEANoUtilizadoPtoVta when a point of sale is given.</summary>
    private async Task<ContractAnswer> UnusedAsync(ServiceCall call, int? pointOfSale, CancellationToken ct)
    {
        var code = call.Request.Long("CAEA");
        var today = clock.Today();
        var errors = new List<MtxcaFinding>();

        await _caeaGate.WaitAsync(ct);
        try
        {
            var caea = await _state.FindCaeaAsync(code, ct);
            if (caea is null) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1200));
            else if (caea.Cuit != call.Cuit) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1201));
            else
            {
                if (today <= caea.From) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1203));
                if (pointOfSale is { } number)
                {
                    var issuer = await IssuerAsync(call.Cuit, number, PointOfSaleKind.WebServiceCaea, ct);
                    if (issuer?.FindPointOfSale(number) is not { Kind: PointOfSaleKind.WebServiceCaea })
                        errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1204));
                    if (caea.UsedPoints.Contains(number)) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1206));
                    if (caea.Unused || caea.UnusedPoints.Contains(number)) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1207));
                }
                else
                {
                    if (caea.UsedPoints.Count > 0) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1202));
                    if (caea.Unused) errors.Add(MtxcaCodes.Error(MtxcaTable.Unused, 1208));
                }
            }

            if (errors.Count == 0)
            {
                var updated = pointOfSale is { } point
                    ? caea! with { UnusedPoints = [.. caea.UnusedPoints, point] }
                    : caea! with { Unused = true };
                await _state.SaveCaeaAsync(updated, ct);
            }
        }
        finally
        {
            _caeaGate.Release();
        }

        return call.Ok(new XElement(call.Operation.Output,
            new XElement("resultado", errors.Count == 0 ? "A" : "R"),
            new XElement("fechaProceso", Day(today)),
            new XElement("CAEA", code),
            pointOfSale is { } echoed ? new XElement("numeroPuntoVenta", Math.Clamp(echoed, 1, VoucherLimits.MaxPointOfSale)) : null,
            errors.Count > 0 ? Codes("arrayErrores", errors) : null));
    }

    private async Task<ContractAnswer> InformAsync(ServiceCall call, CancellationToken ct)
    {
        var element = call.Request.Child("comprobanteCAEARequest") ?? new XElement("comprobanteCAEARequest");
        var voucher = MtxcaVoucherInput.Read(element);
        var today = clock.Today();
        var date = voucher.Date ?? today;
        var type = _tables.VoucherType(voucher.Type);
        var issuer = await IssuerAsync(call.Cuit, voucher.PointOfSale, PointOfSaleKind.WebServiceCaea, ct, ClassOf(voucher.Type));
        var caea = voucher.AuthorizationCode is { } sent ? await _state.FindCaeaAsync(sent, ct) : null;

        var findings = new List<MtxcaFinding>();
        if (type is null) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 700));
        if (!Usable(issuer, voucher.PointOfSale, PointOfSaleKind.WebServiceCaea, date)) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 701));
        if (caea is null || caea.Cuit != call.Cuit) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 705));
        else
        {
            if (date < caea.From || date > caea.To) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 702));
            if (today <= caea.From) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 706));
            if (voucher.AuthorizationDue is { } due && due != caea.To) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 732));
            if (caea.Unused || caea.UnusedPoints.Contains(voucher.PointOfSale)) findings.Add(MtxcaCodes.Observation(MtxcaTable.Caea, 717));
        }
        if (issuer is { } known && known.VatCondition != VatCondition.ResponsableInscripto) findings.Add(MtxcaCodes.Observation(MtxcaTable.Caea, 750));
        if (type is not null)
            findings.AddRange(await Validator.ValidateAsync(true, voucher, type, call.Cuit, date, today, Activities(issuer), ct));

        var sequence = (call.Cuit, voucher.PointOfSale, voucher.Type);
        if (!_busy.TryAdd(sequence, 0)) return InformRejected(call, today, [MtxcaCodes.Error(MtxcaTable.Caea, 739)]);
        try
        {
            var last = await _state.LastAsync(call.Cuit, voucher.PointOfSale, voucher.Type, ct);
            if (voucher.Number != (last?.Number ?? 0) + 1) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 703));
            if (date < last?.Date) findings.Add(MtxcaCodes.Error(MtxcaTable.Caea, 704));
            if (findings.Any(f => f.Rejects))
            {
                events.Publish(new VoucherRejected(time.GetUtcNow(), call.Cuit, voucher.PointOfSale, voucher.Type, voucher.Number,
                    findings.Where(f => f.Rejects).Select(f => f.Code).ToList()));
                return InformRejected(call, today, findings.Where(f => f.Rejects));
            }

            await _caeaGate.WaitAsync(ct);
            try
            {
                var current = await _state.FindCaeaAsync(caea!.Code, ct) ?? caea;
                if (!current.UsedPoints.Contains(voucher.PointOfSale))
                    await _state.SaveCaeaAsync(current with { UsedPoints = [.. current.UsedPoints, voucher.PointOfSale] }, ct);
            }
            finally
            {
                _caeaGate.Release();
            }
            await _state.AddAsync(Stored(call.Cuit, voucher, date, "A", caea.Code, caea.To, findings),
                voucher.DocType ?? 0, voucher.DocNumber ?? 0, voucher.Total, ct);
            events.Publish(new VoucherAuthorized(time.GetUtcNow(), call.Cuit, voucher.PointOfSale, voucher.Type, voucher.Number,
                voucher.Number, "CAEA", caea.Code.ToString(CultureInfo.InvariantCulture)));

            return call.Ok(new XElement(call.Operation.Output,
                new XElement("resultado", findings.Count > 0 ? "O" : "A"),
                new XElement("fechaProceso", Day(today)),
                new XElement("comprobanteCAEAResponse",
                    new XElement("CAEA", caea.Code),
                    new XElement("codigoTipoComprobante", voucher.Type),
                    new XElement("numeroPuntoVenta", voucher.PointOfSale),
                    new XElement("numeroComprobante", voucher.Number)),
                findings.Count > 0 ? Codes("arrayObservaciones", findings) : null));
        }
        finally
        {
            _busy.TryRemove(sequence, out _);
        }
    }

    private static ContractAnswer InformRejected(ServiceCall call, DateOnly today, IEnumerable<MtxcaFinding> errors) =>
        call.Ok(new XElement(call.Operation.Output,
            new XElement("resultado", "R"),
            new XElement("fechaProceso", Day(today)),
            Codes("arrayErrores", errors)));
}
