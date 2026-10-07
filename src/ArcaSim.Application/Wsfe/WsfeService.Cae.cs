using ArcaSim.Application.Events;
using ArcaSim.Domain;

namespace ArcaSim.Application.Wsfe;

public sealed partial class WsfeService
{
    /// <summary>
    /// Authorizes a voucher or a batch (docs/arca/wsfev1.md §3, §4.1, §5):
    /// header problems reject everything; then each voucher is validated in
    /// order, takes the next number and gets its CAE, until one is rejected and
    /// the rest go back unprocessed. Sending the same voucher twice is not
    /// idempotent: the second one fails numbering, as in ARCA.
    /// </summary>
    public async Task<FECAEResponse> FECAESolicitarAsync(FECAESolicitarRequest request, CancellationToken ct = default)
    {
        var auth = tokens.Validate(request.Auth);
        if (auth.Failed) return new FECAEResponse { Errors = [auth.Error!] };

        var header = request.FeCAEReq?.FeCabReq ?? new FECAECabRequest();
        var details = request.FeCAEReq?.FeDetReq ?? [];
        var today = clock.Today();
        var type = tables.VoucherType(header.CbteTipo);
        var issuer = await IssuerAsync(auth.Cuit, header.PtoVta, PointOfSaleKind.WebServiceCae, type?.Class, ct);
        var forced = settings.ChaosOf(Name).TryTakeForcedRejection(out var forcedCode) ? forcedCode : (int?)null;

        var errors = HeaderErrors(RuleCodes.Cae, header, details.Length, type, issuer, today, PointOfSaleKind.WebServiceCae);
        if (forced is { } code && IsHeaderCode(code))
        {
            errors.Add(Forced(code).ToErr());
            forced = null;
        }

        var response = new FECAEResponse
        {
            FeCabResp = new FECAECabResponse
            {
                Cuit = auth.Cuit,
                PtoVta = header.PtoVta,
                CbteTipo = header.CbteTipo,
                FchProceso = Fev1Dates.FormatProcessed(clock.Now),
                CantReg = header.CantReg,
                Reproceso = "N",
            },
            FeDetResp = details.Select(d => Unprocessed(d, today)).ToArray(),
        };
        if (errors.Count > 0)
        {
            response.FeCabResp.Resultado = "R";
            response.Errors = [.. errors];
            return response;
        }

        using (await locks.AcquireAsync(Name, auth.Cuit, header.PtoVta, header.CbteTipo, ct))
        {
            var last = await vouchers.LastAsync(auth.Cuit, header.PtoVta, header.CbteTipo, ct);
            var next = (last?.To ?? 0) + 1;
            var lastDate = last?.Date;

            for (var i = 0; i < details.Length; i++)
            {
                var detail = details[i];
                var answer = response.FeDetResp[i];
                var findings = (await validator.ValidateAsync(RuleCodes.Cae, detail, type!, auth.Cuit, today, ct)).ToList();
                if (i == 0 && forced is { } rejection) findings.Insert(0, Forced(rejection));
                if (findings.Count > 0) answer.Observaciones = findings.Select(f => f.ToObs()).ToArray();

                var date = VoucherValidator.DateOf(detail, today);
                var rejected = findings.Any(f => f.Rejects);
                if (!rejected && (detail.CbteDesde != next || date < lastDate))
                {
                    errors.Add(catalog.For(RuleCodes.Cae, 10016, "Err").ToErr());
                    findings.Add(catalog.For(RuleCodes.Cae, 10016, "Err"));
                    rejected = true;
                }
                if (rejected)
                {
                    events.Publish(new VoucherRejected(time.GetUtcNow(), auth.Cuit, header.PtoVta, header.CbteTipo, detail.CbteDesde,
                        findings.Where(f => f.Rejects).Select(f => f.Code).ToList()));
                    break;
                }

                var cae = codes.NextCae();
                var due = date.AddDays(settings.CaeLifetimeDays);
                var observations = answer.Observaciones ?? [];
                await vouchers.AddAsync(new StoredVoucher(
                    auth.Cuit, header.PtoVta, header.CbteTipo, detail.CbteDesde, detail.CbteHasta, date,
                    EmissionType.Cae, cae, due, clock.Now, Normalized(detail, date), observations), ct);

                events.Publish(new VoucherAuthorized(time.GetUtcNow(), auth.Cuit, header.PtoVta, header.CbteTipo,
                    detail.CbteDesde, detail.CbteHasta, "CAE", cae));
                answer.Resultado = "A";
                answer.CAE = cae;
                answer.CAEFchVto = Fev1Dates.Format(due);
                next = detail.CbteHasta + 1;
                lastDate = date;
            }
        }

        var approved = response.FeDetResp.Count(d => d.Resultado == "A");
        response.FeCabResp.Resultado = approved == details.Length ? "A" : approved == 0 ? "R" : "P";
        response.Errors = errors.Count > 0 ? [.. errors] : null;
        return response;
    }

    /// <summary>
    /// The header checks shared by FECAESolicitar and FECAEARegInformativo:
    /// the issuer (10000), the batch size, the point of sale and the voucher type.
    /// </summary>
    private List<Err> HeaderErrors(
        string method, FECabRequest header, int sent, VoucherTypeInfo? type, Taxpayer? issuer, DateOnly today, PointOfSaleKind expectedKind)
    {
        var caea = method == RuleCodes.Caea;
        var errors = new List<Err>();
        if (IssuerProblem(issuer, type) is { } problem) errors.Add(catalog.WithMessage(method, 10000, problem).ToErr());
        if (header.CantReg is < 1 or > 9_998) errors.Add(catalog.For(method, 10001).ToErr());
        if (header.CantReg != sent)
            errors.Add(catalog.WithMessage(method, 10002, $"Campo CantReg debe ser igual a lo informado en detalle. Informado: {header.CantReg}, Enviado:{sent}").ToErr());
        if (sent > settings.MaxRecordsPerRequest || (type is { Fce: true } && sent > 1)) errors.Add(catalog.For(method, 10003).ToErr());
        if (header.PtoVta is < 1 or > VoucherLimits.MaxPointOfSale) errors.Add(catalog.For(method, caea ? 1300 : 10004).ToErr());
        if (caea)
        {
            if (type is null) errors.Add(catalog.For(method, 700).ToErr());
        }
        else
        {
            if (header.CbteTipo <= 0) errors.Add(catalog.For(method, 10006).ToErr());
            else if (type is null) errors.Add(catalog.For(method, 10007).ToErr());
        }

        var usable = issuer?.CanIssueFrom(header.PtoVta, expectedKind, today) == true;
        if (header.PtoVta is >= 1 and <= VoucherLimits.MaxPointOfSale && !usable) errors.Add(catalog.For(method, caea ? 701 : 10005).ToErr());
        return errors;
    }

    /// <summary>A detail as it goes back when it is not authorized: echoed, with R and empty CAE fields.</summary>
    private static FECAEDetResponse Unprocessed(FECAEDetRequest detail, DateOnly today) => new()
    {
        Concepto = detail.Concepto,
        DocTipo = detail.DocTipo,
        DocNro = detail.DocNro,
        CbteDesde = detail.CbteDesde,
        CbteHasta = detail.CbteHasta,
        CbteFch = Fev1Dates.Format(VoucherValidator.DateOf(detail, today)),
        Resultado = "R",
        CAE = "",
        CAEFchVto = "",
    };

    /// <summary>
    /// What FECompConsultar will answer: the detail with its date filled in and
    /// the service dates it did not have as empty strings (wsfev1.md §2.4).
    /// </summary>
    private static FEDetRequest Normalized(FEDetRequest detail, DateOnly date)
    {
        var copy = new FECAEDetRequest();
        detail.CopyTo(copy);
        copy.CbteFch = Fev1Dates.Format(date);
        copy.FchServDesde ??= "";
        copy.FchServHasta ??= "";
        copy.FchVtoPago ??= "";
        return copy;
    }

    private static bool IsHeaderCode(int code) => code is >= 10000 and <= 10007 or < 1000;

    /// <summary>The rejection a test asked for through the admin API, with the code's own text when it has one.</summary>
    private Finding Forced(int code)
    {
        try
        {
            return catalog.For(RuleCodes.Cae, code) with { Rejects = true };
        }
        catch (ArgumentException)
        {
            return new Finding(code, catalog.Message(code) ?? $"Rechazo forzado por ArcaSim (código {code}).", true);
        }
    }
}
