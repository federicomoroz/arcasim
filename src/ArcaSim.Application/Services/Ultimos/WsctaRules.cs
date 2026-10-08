using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Organismos;
using ArcaSim.Application.Wsfe;
using static ArcaSim.Application.Services.Ultimos.CertificateRegistry;

namespace ArcaSim.Application.Services.Ultimos;

/// <summary>One entry of a certificate's log: the operation that read or changed it, the official who called, when, and the registry that decided.</summary>
public sealed record CertificateLog(long Id, string Operation, long Official, DateTimeOffset At, string? Registry = null);

/// <summary>
/// A Certificado de Transferencia de Automotores (F.381 option CTA): the
/// seller (Applicant) requests it in ARCA's web for a domain and a procedure,
/// and the DNRPA approves or rejects it. State is Pendiente, Aprobado,
/// Rechazado or Anulado (voided by the seller); ApprovedAt and RejectedAt are
/// the last approval and rejection, Registry the registro seccional of the
/// last decision, Changes how many times the state changed. Pdf is the
/// document in base64, null for ArcaSim's fictitious one.
/// </summary>
public sealed record TransferCertificate(
    long Number,
    string Domain,
    long Applicant,
    string Procedure,
    string State,
    DateTimeOffset? ApprovedAt = null,
    DateTimeOffset? RejectedAt = null,
    string? Registry = null,
    int Changes = 0,
    string? Pdf = null,
    List<CertificateLog>? Logs = null);

/// <summary>
/// The certificates sellers request in ARCA's web, which no web service
/// creates. Each DNRPA official (the CUIT the ticket acts for) starts with
/// four plainly fictitious ones numbered after that CUIT, its 11 digits
/// followed by 001 to 004: two pending, on domains ZZZ001 and ZZZ002; one the
/// seller voided, on ZZZ001; one approved on 10/03/2025, on ZZZ003. A test or
/// an operator creates or voids a certificate the way the seller would, with
/// the admin API: PUT /arcasim/api/documents/wscta.certificados/{nroCertificado}
/// and a TransferCertificate as JSON, such as {"number": 20111111112009,
/// "domain": "ZZZ009", "applicant": 20111111112, "procedure": "1",
/// "state": "Pendiente"}; a document already there is never overwritten by
/// the seed.
/// </summary>
public static class CertificateRegistry
{
    public const string Certificates = "wscta.certificados";

    public const string Pending = "Pendiente";
    public const string Approved = "Aprobado";
    public const string Rejected = "Rechazado";
    public const string Voided = "Anulado";

    public static string Key(long number) => number.ToString(CultureInfo.InvariantCulture);

    public static string Scope(long official) => $"{Certificates}/{official}";

    public static IEnumerable<(string, TransferCertificate)> Defaults(long official)
    {
        var first = official * 1000;
        return
        [
            (Key(first + 1), new TransferCertificate(first + 1, "ZZZ001", 27666666661, "9000000001", Pending)),
            (Key(first + 2), new TransferCertificate(first + 2, "ZZZ002", 23111111111, "9000000002", Pending)),
            (Key(first + 3), new TransferCertificate(first + 3, "ZZZ001", 27666666661, "9000000003", Voided)),
            (Key(first + 4), new TransferCertificate(first + 4, "ZZZ003", 30777777773, "9000000004", Approved,
                ApprovedAt: new DateTimeOffset(2025, 3, 10, 10, 30, 0, ArgentinaTime.Offset), Registry: "REGISTRO SECCIONAL FICTICIO DE ARCASIM", Changes: 1)),
        ];
    }

    public static Task SeedAsync(IDocumentStore store, long official, CancellationToken ct) =>
        store.SeedAsync(Scope(official), Certificates, Defaults(official), ct);

    /// <summary>The state as the service writes it, whatever case a preloaded document used.</summary>
    public static string StateOf(TransferCertificate certificate) =>
        new[] { Pending, Approved, Rejected, Voided }.FirstOrDefault(s => s.Equals(certificate.State, StringComparison.OrdinalIgnoreCase)) ?? certificate.State;

    /// <summary>Active is what the seller did not void: the only certificates the service reads or changes.</summary>
    public static bool IsActive(TransferCertificate certificate) => StateOf(certificate) is Pending or Approved or Rejected;
}

/// <summary>
/// Certificados de Transferencia de Automotores (wscta, docs/arca/servicios/wscta.md):
/// a DNRPA official reads a certificate's PDF by number and domain
/// (getCertificadoPDF), approves or rejects it from a registro seccional,
/// and asks for its state (consultarEstadoCertificado) or for a domain's
/// certificates (getListCertificatesByDomain), on CertificateRegistry. Every
/// call that succeeds adds an entry to the certificate's log, and idLog is
/// that entry's number, sequential across the service. The manual's rules:
/// only active certificates (not voided by the seller) are read, approved or
/// rejected; a pending one can be approved or rejected; a rejected one
/// approved, or an approved one rejected, when that decision is not more than
/// 5 days old and the certificate did not change state more than three times;
/// anything else is a fault. ArcaSim's choices where the manual is silent or
/// unverified: the manual lists those two conditions as separate bullets, and
/// both apply; business faults are soapenv:Server with an empty detail and
/// ArcaSim's own texts; estado carries the states as the spec names them;
/// dates are dd-MM-yyyy HH:mm:ss in Argentina's time, the format the service
/// prints in its expired-token fault; the 5 days are calendar days; every
/// change counts, the first one included, so the fourth change is the last;
/// getCertificadoPDF is logged on every call, and a listing logs each
/// certificate it shows with that entry's idLog; the two operations the manual
/// does not document follow its rule on voided certificates too:
/// consultarEstadoCertificado faults, and the listing leaves them out (and
/// faults when no active certificate is left); calls that fail log nothing.
/// </summary>
public sealed class WsctaRules(IDocumentStore store, IClock clock, SequenceLocks locks) : IServiceBehavior
{
    private const string LogCounter = "wscta.logs";
    private const string BusinessFault = "soapenv:Server";
    private const int DecisionDays = 5;
    private const int MostChanges = 3;
    private const int RegistryLength = 50;

    public string Service => "wscta";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name is not ("getCertificadoPDF" or "aprobarCertificado" or "rechazarCertificado" or "consultarEstadoCertificado" or "getListCertificatesByDomain"))
            return null;
        await SeedAsync(store, call.Cuit, ct);
        var number = call.Request.Long("nroCertificado");
        var domain = call.Request.Text("nroDominio") ?? "";

        switch (call.Name)
        {
            case "aprobarCertificado":
                return await DecideAsync(call, number, Approved, ct);
            case "rechazarCertificado":
                return await DecideAsync(call, number, Rejected, ct);
            case "getListCertificatesByDomain":
                return await ListAsync(call, domain, ct);
        }

        using var _ = await LockAsync(number, ct);
        var certificate = await store.GetAsync<TransferCertificate>(Certificates, Key(number), ct);
        if (certificate is null || !certificate.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            return Refuse(call, $"No existe el certificado {number} para el dominio {domain}.");
        if (!IsActive(certificate)) return Refuse(call, NotActive(number));
        var (logged, id) = await LogAsync(certificate, call, null, ct);
        var answer = call.Sample();
        return call.Ok(call.Name == "getCertificadoPDF"
            ? answer.Set("documentoPDF", PdfOf(logged)).Set("idLog", id)
            : Describe(answer, logged, id));
    }

    private async Task<ContractAnswer> DecideAsync(ServiceCall call, long number, string decision, CancellationToken ct)
    {
        var registry = call.Request.Text("registroSeccionalDNRPA") ?? "";
        if (registry.Length == 0) return Refuse(call, "El registro seccional DNRPA es obligatorio.");
        if (registry.Length > RegistryLength) return Refuse(call, $"El registro seccional DNRPA no puede superar los {RegistryLength} caracteres.");

        using var _ = await LockAsync(number, ct);
        var certificate = await store.GetAsync<TransferCertificate>(Certificates, Key(number), ct);
        if (certificate is null) return Refuse(call, $"No existe el certificado {number}.");
        var now = clock.Now;
        if (Objection(certificate, decision, now) is { } objection) return Refuse(call, objection);

        var decided = certificate with
        {
            State = decision,
            ApprovedAt = decision == Approved ? now : certificate.ApprovedAt,
            RejectedAt = decision == Rejected ? now : certificate.RejectedAt,
            Registry = registry,
            Changes = certificate.Changes + 1,
        };
        var logged = await LogAsync(decided, call, registry, ct);
        return call.Ok(call.Sample().Set("idLog", logged.Id));
    }

    /// <summary>Why the certificate cannot take that decision now, or null when it can.</summary>
    private static string? Objection(TransferCertificate certificate, string decision, DateTimeOffset now)
    {
        var number = certificate.Number;
        var state = StateOf(certificate);
        var verb = decision == Approved ? "aprobarse" : "rechazarse";
        if (state == Pending) return null;
        if (state is not (Approved or Rejected)) return NotActive(number);
        if (state == decision) return $"El certificado {number} ya está {state.ToLowerInvariant()}.";
        if ((state == Approved ? certificate.ApprovedAt : certificate.RejectedAt) is { } decidedAt && Days(decidedAt, now) > DecisionDays)
            return $"El certificado {number} fue {state.ToLowerInvariant()} hace más de {DecisionDays} días y ya no puede {verb}.";
        if (certificate.Changes > MostChanges)
            return $"El certificado {number} tuvo más de {MostChanges} modificaciones de estado y ya no puede {verb}.";
        return null;
    }

    private async Task<ContractAnswer> ListAsync(ServiceCall call, string domain, CancellationToken ct)
    {
        var numbers = (await store.ListAsync<TransferCertificate>(Certificates, "", ct))
            .Where(c => c.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && IsActive(c))
            .Select(c => c.Number)
            .Order()
            .ToList();
        if (numbers.Count == 0) return Refuse(call, $"No existen certificados activos para el dominio {domain}.");

        var listed = new List<(TransferCertificate Certificate, long Id)>();
        foreach (var number in numbers)
        {
            using var _ = await LockAsync(number, ct);
            if (await store.GetAsync<TransferCertificate>(Certificates, Key(number), ct) is { } certificate)
                listed.Add(await LogAsync(certificate, call, null, ct));
        }
        return call.Ok(call.Sample().Repeat("return", listed, (item, entry) => Describe(item, entry.Certificate, entry.Id)));
    }

    /// <summary>A certificate as consultarEstadoCertificado and the listing show it; the listing's own fields only where the answer has them.</summary>
    private static XElement Describe(XElement target, TransferCertificate certificate, long id) => target
        .Set("idLog", id)
        .Set("nroDominio", certificate.Domain)
        .Set("cuitSolicitante", certificate.Applicant)
        .Set("nroTramite", certificate.Procedure)
        .Set("estado", StateOf(certificate))
        .SetOrDrop("fechaAprobacion", Stamp(certificate.ApprovedAt))
        .SetOrDrop("fechaRechazo", Stamp(certificate.RejectedAt))
        .SetOrDrop("registroSeccionalDNRPA", certificate.Registry)
        .Set("documentoPDF", PdfOf(certificate));

    /// <summary>Adds the call to the certificate's log and stores it: the entry's number is the idLog the answer carries.</summary>
    private async Task<(TransferCertificate Certificate, long Id)> LogAsync(TransferCertificate certificate, ServiceCall call, string? registry, CancellationToken ct)
    {
        var id = await store.NextAsync(LogCounter, ct);
        var logged = certificate with { Logs = [.. certificate.Logs ?? [], new CertificateLog(id, call.Name, call.Cuit, clock.Now, registry)] };
        await store.PutAsync(Certificates, Key(certificate.Number), logged, ct);
        return (logged, id);
    }

    /// <summary>One certificate at a time; its 14-digit number never meets an 11-digit CUIT among the vouchers' locks.</summary>
    private Task<IDisposable> LockAsync(long number, CancellationToken ct) => locks.AcquireAsync(Service, number, -1, 381, ct);

    private static ContractAnswer Refuse(ServiceCall call, string message) => call.Fault(message, BusinessFault);

    private static string NotActive(long number) => $"El certificado {number} no está activo: fue anulado por el contribuyente.";

    private static string PdfOf(TransferCertificate certificate) => certificate.Pdf is { Length: > 0 } pdf ? pdf : CertificatePdf.Render(certificate);

    /// <summary>A moment as the service prints one: dd-MM-yyyy HH:mm:ss in Argentina's time.</summary>
    private static string? Stamp(DateTimeOffset? at) => at?.ToArgentina().ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private static int Days(DateTimeOffset from, DateTimeOffset to) =>
        to.ArgentinaDate().DayNumber - from.ArgentinaDate().DayNumber;
}
