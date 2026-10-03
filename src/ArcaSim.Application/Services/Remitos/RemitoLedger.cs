using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.Remitos;

/// <summary>
/// Where one remito service keeps its remitos, in its own collection of the
/// document store: by codRemito, with indexes by request id and by number,
/// and the last number issued per issuer, voucher type and point of emission.
/// Issuing takes "último + 1" under the sequence's lock.
/// </summary>
public sealed class RemitoLedger(string service, IDocumentStore store, SequenceLocks locks, IClock clock)
{
    private sealed record Reference(long Code);

    private sealed record Last(long Number, long Code);

    public DateTimeOffset Now => clock.Now.ToArgentina();

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    public Task<Remito?> FindAsync(long code, CancellationToken ct) => store.GetAsync<Remito>(service, RemitoKey(code), ct);

    public async Task<Remito?> FindByRequestAsync(long issuer, int point, long requestId, CancellationToken ct) =>
        await store.GetAsync<Reference>(service, $"idreq/{issuer}/{point:D5}/{requestId:D15}", ct) is { } reference
            ? await FindAsync(reference.Code, ct)
            : null;

    public async Task<Remito?> FindByNumberAsync(long issuer, int type, int point, long number, CancellationToken ct) =>
        await store.GetAsync<Reference>(service, NumberKey(issuer, type, point, number), ct) is { } reference
            ? await FindAsync(reference.Code, ct)
            : null;

    public async Task<Remito?> LastIssuedAsync(long issuer, int type, int point, CancellationToken ct) =>
        await store.GetAsync<Last>(service, LastKey(issuer, type, point), ct) is { } last ? await FindAsync(last.Code, ct) : null;

    public Task<IReadOnlyList<Remito>> AllAsync(CancellationToken ct) => store.ListAsync<Remito>(service, "remito/", ct);

    public async Task<long> NextCodeAsync(CancellationToken ct) => await store.NextAsync($"{service}.codRemito", ct);

    /// <summary>One generation at a time per issuer and point of emission, so a request id cannot be taken twice. Type 0 is no voucher's.</summary>
    public Task<IDisposable> LockRequestsAsync(long issuer, int point, CancellationToken ct) => locks.AcquireAsync(issuer, point, 0, ct);

    public async Task AddAsync(Remito remito, CancellationToken ct)
    {
        await SaveAsync(remito, ct);
        await store.PutAsync(service, $"idreq/{remito.Issuer}/{remito.Point:D5}/{remito.RequestId:D15}", new Reference(remito.Code), ct);
    }

    public Task SaveAsync(Remito remito, CancellationToken ct) => store.PutAsync(service, RemitoKey(remito.Code), remito, ct);

    /// <summary>
    /// Issues the remito: the next number of its sequence, the CRE, the
    /// emission and expiry dates, state EMI. Saved before the lock is let go.
    /// </summary>
    public async Task IssueAsync(Remito remito, long cuit, CancellationToken ct)
    {
        using (await locks.AcquireAsync(remito.Issuer, remito.Point, remito.Type, ct))
        {
            var last = await store.GetAsync<Last>(service, LastKey(remito.Issuer, remito.Type, remito.Point), ct);
            var now = Now;
            remito.Number = (last?.Number ?? 0) + 1;
            remito.AuthorizationCode = await CreAsync(Today, ct);
            remito.IssuedAt = now;
            remito.ExpiresOn = Today.AddDays(RemitoTerms.ValidityDays(remito.DistanceKm));
            remito.MoveTo(RemitoStates.Issued, now, cuit);
            await SaveAsync(remito, ct);
            await store.PutAsync(service, NumberKey(remito.Issuer, remito.Type, remito.Point, remito.Number.Value), new Reference(remito.Code), ct);
            await store.PutAsync(service, LastKey(remito.Issuer, remito.Type, remito.Point), new Last(remito.Number.Value, remito.Code), ct);
        }
    }

    /// <summary>
    /// The CRE: fourteen digits, (year - 1980), ISO week, a 4 and a correlative
    /// of nine. The pattern fits the five CRE in the manuals' examples; ARCA
    /// does not document it (NO VERIFICADO), so it is ArcaSim's choice. One
    /// correlative for the three services.
    /// </summary>
    private async Task<long> CreAsync(DateOnly date, CancellationToken ct)
    {
        var correlative = await store.NextAsync("remitos.cre", ct) % 1_000_000_000;
        var week = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        return long.Parse($"{date.Year - 1980:D2}{week:D2}4{correlative:D9}", CultureInfo.InvariantCulture);
    }

    private static string RemitoKey(long code) => $"remito/{code:D12}";

    private static string NumberKey(long issuer, int type, int point, long number) => $"nro/{issuer}/{type:D3}/{point:D5}/{number:D8}";

    private static string LastKey(long issuer, int type, int point) => $"ultimo/{issuer}/{type:D3}/{point:D5}";
}

/// <summary>
/// Validity and the window to change the trip, by distance: wsremharina's
/// table (manual Anexo, p.140). Carne and azúcar document no table of their
/// own (NO VERIFICADO); ArcaSim applies harina's to the three.
/// </summary>
public static class RemitoTerms
{
    public static int ValidityDays(decimal km) => km switch
    {
        <= 100 => 2,
        <= 500 => 3,
        <= 1000 => 5,
        _ => 10,
    };

    public static int ChangeHours(decimal km) => km switch
    {
        <= 100 => 24,
        <= 500 => 48,
        <= 1000 => 96,
        _ => 240,
    };

    /// <summary>
    /// The qr field: base64 of a JPEG, as the examples start (/9j/4AAQSkZJRg).
    /// ArcaSim's is a JFIF header with the remito's data in a comment, not a
    /// scannable image: what ARCA encodes in the code is not documented.
    /// </summary>
    public static string Qr(Remito remito)
    {
        var text = Encoding.ASCII.GetBytes(
            $"ArcaSim remito cuit={remito.Issuer} tipo={remito.Type} pto={remito.Point} nro={remito.Number} cre={remito.AuthorizationCode}");
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00 };
        bytes.AddRange([0xFF, 0xFE, (byte)((text.Length + 2) >> 8), (byte)((text.Length + 2) & 0xFF)]);
        bytes.AddRange(text);
        bytes.AddRange([0xFF, 0xD9]);
        return Convert.ToBase64String(bytes.ToArray());
    }
}

/// <summary>What the remito rules need from a request, whatever the service calls the fields.</summary>
public static class RemitoQueries
{
    /// <summary>
    /// The remito a consult asks for: by codRemito; by request id and point of
    /// emission (the caller's own); or by type, point, number and issuer. Only
    /// a party to the remito sees it.
    /// </summary>
    public static async Task<Remito?> FindAsync(RemitoLedger ledger, XElement request, long cuit, string requestIdName, CancellationToken ct)
    {
        Remito? remito = null;
        if (request.ChildLong("codRemito") is { } code)
            remito = await ledger.FindAsync(code, ct);
        else if (request.ChildLong(requestIdName) is { } requestId && request.ChildLong("puntoEmision") is { } point)
            remito = await ledger.FindByRequestAsync(cuit, (int)point, requestId, ct);
        else if (request.ChildLong("tipoComprobante") is { } type && request.ChildLong("puntoEmision") is { } at
                 && request.ChildLong("nroComprobante") is { } number)
            remito = await ledger.FindByNumberAsync(request.ChildLong("cuitEmisor") ?? cuit, (int)type, (int)at, number, ct);
        return remito is not null && remito.Involves(cuit) ? remito : null;
    }

    public static bool AsksForOne(XElement request, string requestIdName) =>
        request.Element("codRemito") is not null || request.Element(requestIdName) is not null || request.Element("nroComprobante") is not null;
}
