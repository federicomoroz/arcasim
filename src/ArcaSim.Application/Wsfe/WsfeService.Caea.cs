using System.Globalization;
using ArcaSim.Application.Events;
using ArcaSim.Domain;

namespace ArcaSim.Application.Wsfe;

/// <summary>
/// The CAEA regime (docs/arca/wsfev1.md §6.3 to §6.5): ask for a code per
/// fortnight, issue offline with it, then report every voucher, or report the
/// points of sale that did not use it. Since RG 5782/2025 every CAEA point of
/// sale counts as a contingency one.
/// </summary>
public sealed partial class WsfeService
{
    public async Task<FECAEAGetResponse> FECAEASolicitarAsync(CaeaPeriodRequest request, CancellationToken ct = default)
    {
        const string method = "FECAEASolicitar";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECAEAGetResponse { Errors = [auth.Error!] };

        var errors = new List<Err>();
        var issuer = await IssuerAsync(auth.Cuit, null, PointOfSaleKind.WebServiceCaea, null, ct);
        if (issuer is not { Active: true }) errors.Add(catalog.For(method, 15000).ToErr());
        else if (!settings.OpenAccess)
        {
            var today = clock.Today();
            bool Active(PointOfSale p, PointOfSaleKind kind) => p.Kind == kind && !p.Blocked && !(p.DeactivatedOn <= today);
            if (!issuer.PointsOfSale.Any(p => Active(p, PointOfSaleKind.WebServiceCaea))) errors.Add(catalog.For(method, 15003).ToErr());
            if (!issuer.PointsOfSale.Any(p => Active(p, PointOfSaleKind.WebServiceCae))) errors.Add(catalog.For(method, 15016).ToErr());
        }
        if (!TryFortnight(request.Periodo, request.Orden, out var from, out var to, out var periodError))
            errors.Add(catalog.For(method, periodError).ToErr());
        if (errors.Count > 0) return new FECAEAGetResponse { Errors = [.. errors] };

        var day = clock.Today();
        var opens = from.AddDays(-5);
        if (day < opens || day > to)
        {
            var window = $"Fecha de envío podrá ser desde 5 días corridos anteriores al inicio hasta el último dia de cada quincena. Del {UsDate(opens)} hasta {UsDate(to)}";
            return new FECAEAGetResponse { Errors = [catalog.WithMessage(method, 15006, window).ToErr()] };
        }
        if (await caeas.FindAsync(auth.Cuit, request.Periodo, request.Orden, ct) is not null)
            return new FECAEAGetResponse { Errors = [catalog.For(method, 15008).ToErr()] };

        var caea = new IssuedCaea(auth.Cuit, request.Periodo, request.Orden, codes.NextCaea(), from, to, to.AddMonths(1), clock.Now);
        await caeas.AddAsync(caea, ct);
        events.Publish(new CaeaGranted(time.GetUtcNow(), auth.Cuit, caea.Period, caea.Fortnight, caea.Code));
        var result = ToGet(caea);
        result.Observaciones = [catalog.For(method, 15018).ToObs()];
        return new FECAEAGetResponse { ResultGet = result };
    }

    public async Task<FECAEAGetResponse> FECAEAConsultarAsync(CaeaPeriodRequest request, CancellationToken ct = default)
    {
        const string method = "FECAEAConsultar";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECAEAGetResponse { Errors = [auth.Error!] };

        // With an error ARCA still echoes Periodo and Orden, the schema's mandatory integers.
        var echo = new FECAEAGet { Periodo = request.Periodo, Orden = request.Orden };
        if (!TryFortnight(request.Periodo, request.Orden, out _, out _, out var periodError))
            return new FECAEAGetResponse { ResultGet = echo, Errors = [catalog.For(method, periodError).ToErr()] };

        var caea = await caeas.FindAsync(auth.Cuit, request.Periodo, request.Orden, ct);
        return caea is null
            ? new FECAEAGetResponse { ResultGet = echo, Errors = [NoData] }
            : new FECAEAGetResponse { ResultGet = ToGet(caea) };
    }

    /// <summary>Reports vouchers issued offline under a CAEA. Same batch logic as FECAESolicitar, with the CAEA's own codes.</summary>
    public async Task<FECAEAResponse> FECAEARegInformativoAsync(FECAEARegInformativoRequest request, CancellationToken ct = default)
    {
        const string method = RuleCodes.Caea;
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECAEAResponse { Errors = [auth.Error!] };

        var header = request.FeCAEARegInfReq?.FeCabReq ?? new FECAEACabRequest();
        var details = request.FeCAEARegInfReq?.FeDetReq ?? [];
        var today = clock.Today();
        var type = tables.VoucherType(header.CbteTipo);
        var issuer = await IssuerAsync(auth.Cuit, header.PtoVta, PointOfSaleKind.WebServiceCaea, type?.Class, ct);
        var errors = HeaderErrors(method, header, details.Length, type, issuer, today, PointOfSaleKind.WebServiceCaea);

        var response = new FECAEAResponse
        {
            FeCabResp = new FECAEACabResponse
            {
                Cuit = auth.Cuit,
                PtoVta = header.PtoVta,
                CbteTipo = type is null ? 0 : header.CbteTipo,
                FchProceso = Fev1Dates.FormatProcessed(clock.Now),
                CantReg = header.CantReg,
                Reproceso = "N",
            },
            FeDetResp = details.Select(d => new FECAEADetResponse
            {
                Concepto = d.Concepto,
                DocTipo = d.DocTipo,
                DocNro = d.DocNro,
                CbteDesde = d.CbteDesde,
                CbteHasta = d.CbteHasta,
                CbteFch = d.CbteFch,
                Resultado = "R",
                CAEA = d.CAEA ?? "",
            }).ToArray(),
        };
        if (errors.Count > 0)
        {
            response.FeCabResp.Resultado = "R";
            response.Errors = [.. errors];
            return response;
        }

        using (await locks.AcquireAsync(auth.Cuit, header.PtoVta, header.CbteTipo, ct))
        {
            var last = await vouchers.LastAsync(auth.Cuit, header.PtoVta, header.CbteTipo, ct);
            var next = (last?.To ?? 0) + 1;
            var lastDate = last?.Date;

            for (var i = 0; i < details.Length; i++)
            {
                var detail = details[i];
                var answer = response.FeDetResp[i];

                if (detail.CAEA is not { Length: 14 } code || !code.All(char.IsAsciiDigit))
                {
                    errors.Add(catalog.For(method, 782).ToErr());
                    break;
                }
                var caea = await caeas.FindByCodeAsync(code, ct);
                if (caea is null)
                {
                    errors.Add(catalog.For(method, 780).ToErr());
                    break;
                }
                if (caea.Cuit != auth.Cuit)
                {
                    errors.Add(catalog.For(method, 705).ToErr());
                    break;
                }

                var findings = (await validator.ValidateAsync(method, detail, type!, auth.Cuit, today, ct)).ToList();
                var dated = Fev1Dates.TryParse(Fev1Dates.Blank(detail.CbteFch), out var date);
                if (!dated) findings.Add(catalog.For(method, 783));
                else if (date < caea.ValidFrom || date > caea.ValidTo) findings.Add(catalog.For(method, 702));
                var generated = Fev1Dates.Blank(detail.CbteFchHsGen);
                if (generated is null) findings.Add(catalog.For(method, 1440));
                else if (!DateTime.TryParseExact(generated, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    findings.Add(catalog.For(method, 1441));
                if (detail.Compradores is { Length: > 0 }) findings.Add(catalog.For(method, 1432));
                if ((await caeas.WithoutMovementAsync(auth.Cuit, code, ct)).Any(r => r.PointOfSale == header.PtoVta))
                    findings.Add(catalog.For(method, 1424));
                if (findings.Count > 0) answer.Observaciones = findings.Select(f => f.ToObs()).ToArray();

                var rejected = findings.Any(f => f.Rejects);
                if (!rejected && detail.CbteDesde != next)
                {
                    errors.Add(catalog.For(method, 703).ToErr());
                    rejected = true;
                }
                if (!rejected && date < lastDate)
                {
                    errors.Add(catalog.For(method, 704).ToErr());
                    rejected = true;
                }
                if (rejected) break;

                await vouchers.AddAsync(new StoredVoucher(
                    auth.Cuit, header.PtoVta, header.CbteTipo, detail.CbteDesde, detail.CbteHasta, date,
                    EmissionType.Caea, code, caea.ValidTo, clock.Now, Normalized(detail, date),
                    answer.Observaciones ?? []), ct);
                events.Publish(new VoucherAuthorized(time.GetUtcNow(), auth.Cuit, header.PtoVta, header.CbteTipo,
                    detail.CbteDesde, detail.CbteHasta, "CAEA", code));
                answer.Resultado = "A";
                next = detail.CbteHasta + 1;
                lastDate = date;
            }
        }

        var approved = response.FeDetResp.Count(d => d.Resultado == "A");
        response.FeCabResp.Resultado = approved == details.Length ? "A" : approved == 0 ? "R" : "P";
        response.Errors = errors.Count > 0 ? [.. errors] : null;
        return response;
    }

    public async Task<FECAEASinMovResponse> FECAEASinMovimientoInformarAsync(FECAEASinMovimientoInformarRequest request, CancellationToken ct = default)
    {
        const string method = "FECAEASinMovimientoInformar";
        var auth = tokens.Validate(request.Auth);
        var response = new FECAEASinMovResponse
        {
            CAEA = request.CAEA,
            FchProceso = Fev1Dates.Format(clock.Today()),
            PtoVta = request.PtoVta,
        };
        if (auth.Failed)
        {
            response.Resultado = "R";
            response.Errors = [auth.Error!];
            return response;
        }

        var errors = new List<Err>();
        if (request.PtoVta is < 1 or > 99_998) errors.Add(catalog.For(method, 1206).ToErr());
        var caea = request.CAEA is { Length: 14 } code && code.All(char.IsAsciiDigit)
            ? await caeas.FindByCodeAsync(code, ct)
            : null;
        if (request.CAEA is not { Length: 14 }) errors.Add(catalog.For(method, 1207).ToErr());
        else if (caea is null) errors.Add(catalog.For(method, 1200).ToErr());
        else if (caea.Cuit != auth.Cuit) errors.Add(catalog.For(method, 1201).ToErr());
        else
        {
            var issuer = await IssuerAsync(auth.Cuit, request.PtoVta, PointOfSaleKind.WebServiceCaea, null, ct);
            if (issuer?.FindPointOfSale(request.PtoVta) is not { Kind: PointOfSaleKind.WebServiceCaea }) errors.Add(catalog.For(method, 1204).ToErr());
            if (clock.Today() <= caea.ValidFrom) errors.Add(catalog.For(method, 1203).ToErr());
            if (await vouchers.AnyWithCaeaAsync(auth.Cuit, caea.Code, request.PtoVta, ct)) errors.Add(catalog.For(method, 1202).ToErr());
            if ((await caeas.WithoutMovementAsync(auth.Cuit, caea.Code, ct)).Any(r => r.PointOfSale == request.PtoVta))
                errors.Add(catalog.For(method, 1209).ToErr());
        }

        if (errors.Count == 0)
            await caeas.AddWithoutMovementAsync(new CaeaWithoutMovement(auth.Cuit, caea!.Code, request.PtoVta, clock.Today()), ct);
        response.Resultado = errors.Count == 0 ? "A" : "R";
        response.Errors = errors.Count > 0 ? [.. errors] : null;
        return response;
    }

    public async Task<FECAEASinMovConsResponse> FECAEASinMovimientoConsultarAsync(FECAEASinMovimientoConsultarRequest request, CancellationToken ct = default)
    {
        const string method = "FECAEASinMovimientoConsultar";
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECAEASinMovConsResponse { Errors = [auth.Error!] };

        if (request.CAEA is not { Length: 14 } code || !code.All(char.IsAsciiDigit))
            return new FECAEASinMovConsResponse { Errors = [catalog.For(method, 10100).ToErr()] };
        if (request.PtoVta is < 0 or > 99_998)
            return new FECAEASinMovConsResponse { Errors = [catalog.For(method, 10101).ToErr()] };

        var reports = (await caeas.WithoutMovementAsync(auth.Cuit, code, ct))
            .Where(r => request.PtoVta == 0 || r.PointOfSale == request.PtoVta)
            .Select(r => new FECAEASinMov { CAEA = r.Caea, FchProceso = Fev1Dates.Format(r.ReportedOn), PtoVta = r.PointOfSale })
            .ToArray();
        return reports.Length == 0
            ? new FECAEASinMovConsResponse { Errors = [catalog.For(method, 10102).ToErr()] }
            : new FECAEASinMovConsResponse { ResultGet = reports };
    }

    /// <summary>The days a fortnight covers: "orden" 1 is the 1st to the 15th, 2 the 16th to the end of the month.</summary>
    private static bool TryFortnight(int period, short fortnight, out DateOnly from, out DateOnly to, out int error)
    {
        from = to = default;
        error = 0;
        if (period is < 190001 or > 999912 || period % 100 is < 1 or > 12)
        {
            error = 15004;
            return false;
        }
        if (fortnight is not (1 or 2))
        {
            error = 15005;
            return false;
        }
        var first = new DateOnly(period / 100, period % 100, 1);
        from = fortnight == 1 ? first : first.AddDays(15);
        to = fortnight == 1 ? first.AddDays(14) : first.AddMonths(1).AddDays(-1);
        return true;
    }

    private static FECAEAGet ToGet(IssuedCaea caea) => new()
    {
        CAEA = caea.Code,
        Periodo = caea.Period,
        Orden = caea.Fortnight,
        FchVigDesde = Fev1Dates.Format(caea.ValidFrom),
        FchVigHasta = Fev1Dates.Format(caea.ValidTo),
        FchTopeInf = Fev1Dates.Format(caea.ReportDeadline),
        FchProceso = Fev1Dates.FormatProcessed(caea.ProcessedAt),
    };

    /// <summary>ARCA writes the window of 15006 as M/d/yyyy ("Del 6/26/2021 hasta 7/15/2021").</summary>
    private static string UsDate(DateOnly day) => day.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
}
