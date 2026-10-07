using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A F2005 certificate as SIRE keeps it: issued (VIGENTE), cancelled (ANULADO), or the one that cancels another (ANULACION).</summary>
public sealed record WithholdingCertificate(
    string Number,
    string SecurityCode,
    long Agent,
    long Withheld,
    int Regime,
    DateOnly WithheldOn,
    decimal Amount,
    int VoucherType,
    string? VoucherNumber,
    string? TraceCode,
    string State,
    string? Cancels,
    string? CancelledBy,
    int? CancelReason,
    DateTimeOffset IssuedAt);

/// <summary>A regime of tax 216 that SIRE accepts. ArcaSim has no table of its own: put documents here to restrict emitir to them.</summary>
public sealed record WithholdingRegime(int Code, string Description, bool NeedsCondition);

/// <summary>
/// SIRE, certificate F2005 of VAT withholding (sire-ws, docs/arca/servicios/sire-ws.md):
/// emitir checks the manual's rules (version 100, tax 216, dates, the fields
/// each voucher type requires and the 99999-99999999 format, an existing
/// withheld CUIT) and issues a correlative number with a random security code;
/// anular cancels a certificate of the same agent with a new certificate of
/// its own. Business errors are faults soap:Client with the text in faultstring.
/// The manual gives one text ("No existe persona con id ..."); every other
/// text is ArcaSim's, in the same style. Also ArcaSim's choices: the regime is
/// any number from 1 to 999 unless sire-ws.regimenes holds a table (831 always
/// asks for the condition); certificadoNro is the year and ten digits;
/// codigoSeguridad is eight random characters; the same codigoTrazabilidad
/// from the same agent returns the certificate it already got; a retention
/// date in the future is refused; a second anular of the same number fails.
/// </summary>
public sealed partial class SireRules(IDocumentStore store, PadronDirectory padron, IClock clock) : IServiceBehavior
{
    public const string Certificates = "sire-ws.certificados";
    public const string Regimes = "sire-ws.regimenes";
    private const string Traces = "sire-ws.trazabilidad";
    private static readonly DateOnly FirstDay = new(2019, 12, 1);

    /// <summary>TIPO_COMPROBANTE of the manual.</summary>
    public static readonly IReadOnlyDictionary<int, string> VoucherTypes = new Dictionary<int, string>
    {
        [1] = "FACTURA", [2] = "RECIBO", [3] = "NOTA CREDITO", [4] = "NOTA DEBITO", [5] = "OTRO COMPROBANTE", [6] = "ORDEN DE PAGO",
        [9] = "ESCRITURA PUBLICA", [11] = "FACTURA (16 DIGITOS)",
        [17] = "LIQUIDACION DE SERVICIOS PUBLICOS - CLASE A", [18] = "LIQUIDACION DE SERVICIOS PUBLICOS - CLASE B",
        [19] = "LIQUIDACION DE SERVICIOS PUBLICOS - CLASE A - CON VALOR NEGATIVO",
        [20] = "LIQUIDACION DE SERVICIOS PUBLICOS - CLASE B - CON VALOR NEGATIVO",
    };

    private static readonly int[] NeedsNumber = [1, 2, 3, 4, 5, 6, 9, 11, 17, 18, 19, 20];
    private static readonly int[] NumberedFormat = [1, 2, 3, 4, 17, 18, 19, 20];
    private static readonly int[] RefersToOriginal = [3, 19, 20];

    [GeneratedRegex(@"^\d{5}-\d{8}$")]
    private static partial Regex VoucherNumberFormat();

    public string Service => "sire-ws";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "emitir" => await IssueAsync(call, ct),
        "anular" => await CancelAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> IssueAsync(ServiceCall call, CancellationToken ct)
    {
        var data = call.Request.Find("certificado") ?? new XElement("certificado");
        if (await RejectAsync(data, ct) is { } problem) return Refuse(call, problem);

        var trace = data.Optional("codigoTrazabilidad");
        if (trace is not null && await store.GetAsync<WithholdingCertificate>(Traces, $"{call.Cuit}/{trace}", ct) is { } earlier)
            return Answer(call, earlier);

        var certificate = new WithholdingCertificate(
            await NumberAsync(ct), SecurityCode(), call.Cuit, data.Long("cuitRetenido"), data.Int("regimen"),
            data.Date("fechaRetencion")!.Value, data.Decimal("importeRetencion"), data.Int("tipoComprobante"),
            data.Optional("numeroComprobante"), trace, "VIGENTE", null, null, null, clock.Now);
        await store.PutAsync(Certificates, certificate.Number, certificate, ct);
        if (trace is not null) await store.PutAsync(Traces, $"{call.Cuit}/{trace}", certificate, ct);
        return Answer(call, certificate);
    }

    private async Task<string?> RejectAsync(XElement data, CancellationToken ct)
    {
        if (data.Int("version") != 100) return "La version del certificado debe ser 100.";
        if (data.Int("impuesto") != 216) return "El impuesto debe ser 216.";

        var regime = data.Int("regimen");
        var table = await store.ListAsync<WithholdingRegime>(Regimes, "", ct);
        var known = table.Count == 0 ? regime is >= 1 and <= 999 : table.Any(r => r.Code == regime);
        if (!known) return $"No existe el regimen {regime} para el impuesto 216.";

        var today = clock.Today();
        if (data.Date("fechaRetencion") is not { } withheldOn) return "La fecha de retencion es obligatoria.";
        if (withheldOn < FirstDay) return "La fecha de retencion no puede ser anterior al 01/12/2019.";
        if (withheldOn > today) return "La fecha de retencion no puede ser posterior a la fecha actual.";

        var needsCondition = regime == 831 || table.Any(r => r.Code == regime && r.NeedsCondition);
        var condition = data.OptionalLong("condicion");
        if (condition is { } c && c is not (1 or 2)) return $"No existe la condicion {c}.";
        if (needsCondition && condition is null) return $"La condicion es obligatoria para el regimen {regime}.";

        if (data.Flag("imposibilidadRetencion") == true && data.Optional("motivoNoRetencion") is null)
            return "El motivo de no retencion es obligatorio si no se efectuo la retencion.";
        if (data.Flag("regimenExclusion") == true)
        {
            if (data.Text("porcentajeExclusion") is not { Length: > 0 } || data.Decimal("porcentajeExclusion") is not (50 or 100))
                return "El porcentaje de exclusion debe ser 50 o 100.";
            if (data.Date("fechaPublicacion") is null) return "La fecha de publicacion es obligatoria si el regimen esta excluido.";
        }

        var type = data.Int("tipoComprobante");
        if (!VoucherTypes.ContainsKey(type)) return $"No existe el tipo de comprobante {type}.";
        if (data.Date("fechaComprobante") is not { } voucherDate) return "La fecha del comprobante es obligatoria.";
        if (voucherDate > withheldOn) return "La fecha del comprobante no puede ser posterior a la fecha de retencion.";
        if (RefersToOriginal.Contains(type) && voucherDate != withheldOn)
            return $"Para el tipo de comprobante {type} la fecha del comprobante debe ser igual a la fecha de retencion.";

        var number = data.Optional("numeroComprobante");
        if (NeedsNumber.Contains(type) && number is null) return $"El numero de comprobante es obligatorio para el tipo de comprobante {type}.";
        if (number is not null && NumberedFormat.Contains(type) && !VoucherNumberFormat().IsMatch(number))
            return $"El numero de comprobante {number} no tiene el formato 99999-99999999.";
        if (number is { Length: > 16 }) return $"El numero de comprobante {number} supera los 16 caracteres.";

        if (type == 3 && data.Optional("motivoEmisionNotaCredito") is null) return "El motivo de emision de la nota de credito es obligatorio.";
        if (RefersToOriginal.Contains(type) &&
            (data.Optional("numeroCertificadoOriginal") is null || data.Date("fechaRetencionCertificadoOriginal") is null
             || data.Text("importeCertificadoOriginal") is not { Length: > 0 }))
            return $"Los datos del certificado original son obligatorios para el tipo de comprobante {type}.";
        if (data.Optional("motivoAnulacion") is not null) return "El motivo de anulacion no se informa al emitir un certificado.";

        var withheld = data.Long("cuitRetenido");
        if (!Cuits.IsValid(withheld) || await padron.FindAsync(withheld, ct) is null) return $"No existe persona con id {withheld}.";
        return null;
    }

    private async Task<ContractAnswer> CancelAsync(ServiceCall call, CancellationToken ct)
    {
        var data = call.Request.Find("certificadoAnulacion") ?? new XElement("certificadoAnulacion");
        if (data.Int("version") != 100) return Refuse(call, "La version del certificado debe ser 100.");
        if (data.Int("impuesto") != 216) return Refuse(call, "El impuesto debe ser 216.");
        var reason = data.Int("motivoAnulacion");
        if (reason is < 1 or > 4) return Refuse(call, $"No existe el motivo de anulacion {reason}.");

        var number = data.Text("numeroCertificado") ?? "";
        var original = await store.GetAsync<WithholdingCertificate>(Certificates, number, ct);
        if (original is null || original.Agent != call.Cuit || original.State == "ANULACION")
            return Refuse(call, $"No existe el certificado {number}.");
        if (original.State == "ANULADO") return Refuse(call, $"El certificado {number} ya se encuentra anulado.");

        var cancellation = original with
        {
            Number = await NumberAsync(ct), SecurityCode = SecurityCode(), TraceCode = data.Optional("codigoTrazabilidad"),
            State = "ANULACION", Cancels = original.Number, CancelledBy = null, CancelReason = reason, IssuedAt = clock.Now,
        };
        await store.PutAsync(Certificates, cancellation.Number, cancellation, ct);
        await store.PutAsync(Certificates, original.Number, original with { State = "ANULADO", CancelledBy = cancellation.Number, CancelReason = reason }, ct);
        return Answer(call, cancellation);
    }

    private async Task<string> NumberAsync(CancellationToken ct) =>
        $"{clock.Now.ToArgentina().Year}{await store.NextAsync(Certificates, ct):D10}";

    private static string SecurityCode() => new(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 8));

    private static ContractAnswer Answer(ServiceCall call, WithholdingCertificate certificate) =>
        call.Ok(call.Sample().Set("certificadoNro", certificate.Number).Set("codigoSeguridad", certificate.SecurityCode));

    private static ContractAnswer Refuse(ServiceCall call, string message) => call.Fault(message, "Client");
}
