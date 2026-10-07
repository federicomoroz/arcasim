using System.Globalization;
using System.Text.Json.Serialization;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Setiws;

public sealed record Obligacion(int? Impuesto, string? ImpuestoDesc, decimal? Importe);

public sealed record Detalle(int? Campo, string? CampoTipo, string? CampoDesc, string? Contenido, string? ContenidoDesc);

/// <summary>A VEP as SETIWS-PAGO-API's OpenAPI describes it. Numbers may come as strings, as in the manual's examples.</summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed record Vep
{
    public long? NroVEP { get; init; }
    public string? FechaHoraCreacion { get; init; }
    public string? FechaExpiracion { get; init; }
    public int? NroFormulario { get; init; }
    public string? OrgRecaudDesc { get; init; }
    public int? CodTipoPago { get; init; }
    public string? PagoDesc { get; init; }
    public string? PagoDescExtracto { get; init; }
    public long? UsuarioCUIT { get; init; }
    public long? ContribuyenteCUIT { get; init; }
    public int? Establecimiento { get; init; }
    public int? Concepto { get; init; }
    public string? ConceptoDesc { get; init; }
    public int? SubConcepto { get; init; }
    public string? SubConceptoDesc { get; init; }
    public int? PeriodoFiscal { get; init; }
    public int? AnticipoCuota { get; init; }
    public decimal? Importe { get; init; }
    public long? OwnerCuit { get; init; }
    public string? OwnerTransactionId { get; init; }
    public List<Obligacion>? Obligaciones { get; init; }
    public List<Detalle>? Detalles { get; init; }
    public long? PagadorCuit { get; init; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed record EdpVep(int? EntidadDePago, Vep? Vep);

/// <summary>The payment receipt (CP) the payment entity reports once the VEP is paid.</summary>
public sealed record Cp(
    long CpId, long NroVEP, long NroTransaccion, int EntidadDePago, int BancoPagador, int TipoSucursal, int FormaPago,
    int Moneda, long ContribuyenteCUIT, decimal Importe, string FechaHoraPago, string FechaPosting);

public sealed record StoredVep(Vep Vep, int EntidadDePago, Cp? Cp);

/// <summary>An application error of SETIWS-PAGO-API: its HTTP status, the exception type it names and its message.</summary>
public sealed record VepError(int Status, string Type, string Message);

/// <summary>
/// SETIWS-PAGO-API's VEPs (docs/arca/servicios/SETIWS-PAGO-API.md): created
/// once per owner's transaction id, numbered in sequence, pending until the
/// payment entity reports a payment, which ARCA's API has no endpoint for and
/// ArcaSim's admin API simulates. The manual describes each error but not its
/// message, so the messages are ArcaSim's.
/// </summary>
public sealed class VepService(IDocumentStore store, IClock clock)
{
    private const string Veps = "setiws-veps";
    private const string Owners = "setiws-owners";
    public static readonly IReadOnlyList<int> PaymentEntities = [0, 1001, 1002, 1003];

    // A create finds out whether its owner's transaction already has a VEP before it makes one, and a payment
    // reads the VEP it writes back: requests that arrive together (a retry after a timeout) take turns.
    private readonly KeyedLocks<string> _creations = new();
    private readonly KeyedLocks<long> _payments = new();

    public async Task<(StoredVep? Vep, VepError? Error)> CreateAsync(EdpVep request, long represented, CancellationToken ct)
    {
        if (request.Vep is not { } vep) return (null, Validation("VEP no informado"));
        var entity = request.EntidadDePago ?? -1;
        if (!PaymentEntities.Contains(entity)) return (null, Validation($"Entidad de pago invalida: {entity}"));
        if (vep.OwnerCuit is null || string.IsNullOrWhiteSpace(vep.OwnerTransactionId))
            return (null, Validation("El VEP debe informar ownerCuit y ownerTransactionId"));
        if (vep.OwnerCuit != represented)
            return (null, Validation($"El ownerCuit {vep.OwnerCuit} no coincide con la CUIT del sistema de autenticacion ({represented})"));
        if (vep.Importe is not (>= 0.01m and <= 9_999_999_999.99m) || vep.Obligaciones is not { Count: > 0 })
            return (null, new VepError(400, "MethodArgumentNotValidException", "importe y obligaciones son requeridos"));
        if (vep.Obligaciones.Any(o => o.Importe is null || o.Impuesto is not (>= 1 and <= 9999)))
            return (null, Validation("Impuesto invalido en las obligaciones"));
        if (vep.Obligaciones.Sum(o => o.Importe!.Value) != vep.Importe)
            return (null, new VepError(400, "InputFormularioException", "La sumatoria de las obligaciones no coincide con el importe del VEP"));
        if (vep.PeriodoFiscal is { } period && (period % 100 > 12 || period < 100_000))
            return (null, new VepError(400, "InputFormularioException", $"Periodo fiscal invalido: {period}"));
        if (vep.ContribuyenteCUIT is { } payer && !Cuits.IsValid(payer))
            return (null, new VepError(400, "InvalidContribuyenteException", $"CUIT del contribuyente invalida: {payer}"));

        var ownerKey = $"{vep.OwnerCuit}/{vep.OwnerTransactionId}";
        using var creating = await _creations.AcquireAsync(ownerKey, ct);
        if (await store.GetAsync<StoredVep>(Owners, ownerKey, ct) is { } existing) return (existing, null);

        var now = clock.Now;
        var number = 55_000_000 + await store.NextAsync("setiws.vep", ct);
        var stored = new StoredVep(vep with
        {
            NroVEP = number,
            FechaHoraCreacion = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            FechaExpiracion = vep.FechaExpiracion ?? DateOnly.FromDateTime(now.Date).AddDays(25).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            OrgRecaudDesc = vep.OrgRecaudDesc ?? "ARCA",
            UsuarioCUIT = vep.UsuarioCUIT ?? represented,
            Establecimiento = vep.Establecimiento ?? 0,
            Concepto = vep.Concepto ?? 19,
            SubConcepto = vep.SubConcepto ?? 19,
            AnticipoCuota = vep.AnticipoCuota ?? 0,
        }, entity, null);
        await store.PutAsync(Veps, Key(number), stored, ct);
        await store.PutAsync(Owners, ownerKey, stored, ct);
        return (stored, null);
    }

    public async Task<(StoredVep? Vep, VepError? Error)> FindAsync(long ownerCuit, long? number, string? transactionId, CancellationToken ct)
    {
        StoredVep? found = null;
        if (number is { } n) found = await store.GetAsync<StoredVep>(Veps, Key(n), ct);
        else if (!string.IsNullOrWhiteSpace(transactionId))
        {
            var byOwner = await store.GetAsync<StoredVep>(Owners, $"{ownerCuit}/{transactionId}", ct);
            found = byOwner is null ? null : await store.GetAsync<StoredVep>(Veps, Key(byOwner.Vep.NroVEP!.Value), ct);
        }
        if (found is null) return (null, new VepError(404, "VepNotFoundException", "El VEP no existe"));
        if (found.Vep.OwnerCuit != ownerCuit) return (null, new VepError(404, "OwnerCuitException", "El VEP pertenece a otro ownerCuit"));
        return (found, null);
    }

    /// <summary>What the payment entity reports when the VEP is paid: the CP. Paying twice keeps the first payment.</summary>
    public async Task<StoredVep?> PayAsync(long number, int branchType, int paymentForm, int bank, CancellationToken ct)
    {
        using var paying = await _payments.AcquireAsync(number, ct);
        if (await store.GetAsync<StoredVep>(Veps, Key(number), ct) is not { } stored) return null;
        if (stored.Cp is not null) return stored;
        var now = clock.Now;
        var paid = stored with
        {
            Cp = new Cp(await store.NextAsync("setiws.cp", ct), number, await store.NextAsync("setiws.transaction", ct),
                stored.EntidadDePago, bank, branchType, paymentForm, 1, stored.Vep.ContribuyenteCUIT ?? stored.Vep.UsuarioCUIT ?? 0,
                stored.Vep.Importe ?? 0, now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        };
        await store.PutAsync(Veps, Key(number), paid, ct);
        await store.PutAsync(Owners, $"{stored.Vep.OwnerCuit}/{stored.Vep.OwnerTransactionId}", paid, ct);
        return paid;
    }

    /// <summary>
    /// What travels next to a VEP still pending: the QR with with-qr=true, and
    /// the payment entity's URL when it went to one. The manual's table is torn
    /// in the PDF; this reading, the 1x1 image and the .invalid URL are ArcaSim's.
    /// </summary>
    public static (string? Qr, string? Url) PendingPayment(StoredVep stored, bool withQr) =>
        stored.Cp is not null ? (null, null) : (
            withQr ? "data:image/jpeg;base64," + BlankJpeg : null,
            stored.EntidadDePago > 0 ? $"https://edp-{stored.EntidadDePago}.arcasim.invalid/vep/{stored.Vep.NroVEP}" : null);

    /// <summary>The manual's TIPO_SUCURSAL and FORMA_PAGO codes, the ones a simulated payment may carry.</summary>
    public static readonly IReadOnlyList<int> BranchTypes = [3, 6, 7, 8, 9, 11, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40];

    public static readonly IReadOnlyList<int> PaymentForms = [1, 2, 3, 4, 5, 41, 42, 43, 62, 63, 64, 68, 69, 91];

    private const string BlankJpeg =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDABALDA4MChAODQ4SERATGCgaGBYWGDEjJR0oOjM9PDkzODdASFxOQERXRTc4UG1RV19iZ2hnPk1xeXBkeFxlZ2P/wAALCAABAAEBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APQK/9k=";

    private static VepError Validation(string message) => new(400, "ValidationException", message);

    private static string Key(long number) => number.ToString("D12", CultureInfo.InvariantCulture);
}
