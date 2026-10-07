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
/// Issuing takes "último + 1" under the sequence's lock. Every change to a
/// remito goes through <see cref="HoldAsync"/>, which takes the remito's own
/// lock and reads it again inside it: of several operations that try the same
/// transition, one takes it and the rest find the state it left.
/// The three kinds of lock never share a key: the sequences' (the service's
/// name), the requests' and the remitos' (the service's name and a suffix), so
/// they can be taken one inside the other in this order: requests, remito,
/// sequence.
/// </summary>
public sealed class RemitoLedger(string service, IDocumentStore store, SequenceLocks locks, IClock clock)
{
    private sealed record Reference(long Code);

    private sealed record Last(long Number, long Code);

    public DateTimeOffset Now => clock.Now.ToArgentina();

    public DateOnly Today => clock.Today();

    public Task<Remito?> FindAsync(long code, CancellationToken ct) => store.GetAsync<Remito>(service, RemitoKey(code), ct);

    /// <summary>The remito by its code, when the caller is a party to it: nobody else sees a remito.</summary>
    public async Task<Remito?> FindForAsync(long code, long cuit, CancellationToken ct) =>
        await FindAsync(code, ct) is { } remito && remito.Involves(cuit) ? remito : null;

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

    /// <summary>
    /// A remito to a receiver without CUIT (carne) is accepted on its own once
    /// its validity ends (manual 2.5.26). The remito is kept in that state the
    /// first time anyone looks at it after the deadline.
    /// </summary>
    private bool DeadlinePassed(Remito? remito) =>
        remito is { Uncategorized: true, State: RemitoStates.Issued } && Today > remito.ExpiresOn;

    private async Task AcceptAsync(Remito remito, CancellationToken ct)
    {
        remito.MoveTo(RemitoStates.Accepted, Now, remito.Issuer);
        await SaveAsync(remito, ct);
    }

    /// <summary>
    /// The remito as a consult sees it, with the deadline applied. It takes
    /// the remito's lock only when the deadline has passed, and reads it again
    /// inside, since another consult may have kept the acceptance already.
    /// </summary>
    public async Task<Remito?> WithDeadlinesAsync(Remito? remito, CancellationToken ct)
    {
        if (!DeadlinePassed(remito)) return remito;
        using var gate = await LockAsync(remito!.Code, ct);
        remito = await FindAsync(remito.Code, ct);
        if (DeadlinePassed(remito)) await AcceptAsync(remito!, ct);
        return remito;
    }

    /// <summary>One operation at a time on one remito. The lock lives apart from the numbering's and the requests'.</summary>
    public Task<IDisposable> LockAsync(long code, CancellationToken ct) => locks.AcquireAsync($"{service}.remito", code, 0, 0, ct);

    /// <summary>
    /// Takes the remito's lock and reads the remito inside it, with its
    /// deadline applied: null when there is none or the caller is no party to
    /// it. What the caller decides from it holds until it lets the hold go.
    /// </summary>
    public async Task<RemitoHold> HoldAsync(long code, long cuit, CancellationToken ct)
    {
        var gate = await LockAsync(code, ct);
        try
        {
            var remito = await FindAsync(code, ct);
            if (DeadlinePassed(remito)) await AcceptAsync(remito!, ct);
            return new RemitoHold(gate, remito is not null && remito.Involves(cuit) ? remito : null);
        }
        catch
        {
            gate.Dispose();
            throw;
        }
    }

    public Task<IReadOnlyList<Remito>> AllAsync(CancellationToken ct) => store.ListAsync<Remito>(service, "remito/", ct);

    public async Task<long> NextCodeAsync(CancellationToken ct) => await store.NextAsync($"{service}.codRemito", ct);

    /// <summary>
    /// One generation at a time per issuer and point of emission, so a request
    /// id cannot be taken twice. Its locks live apart from the numbering's,
    /// which IssueAsync takes inside this one for whatever type the request names.
    /// </summary>
    public Task<IDisposable> LockRequestsAsync(long issuer, int point, CancellationToken ct) =>
        locks.AcquireAsync($"{service}.requests", issuer, point, 0, ct);

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
        using (await locks.AcquireAsync(service, remito.Issuer, remito.Point, remito.Type, ct))
        {
            var last = await store.GetAsync<Last>(service, LastKey(remito.Issuer, remito.Type, remito.Point), ct);
            var now = Now;
            var today = now.ArgentinaDate();
            remito.Number = (last?.Number ?? 0) + 1;
            remito.AuthorizationCode = await CreAsync(today, ct);
            remito.IssuedAt = now;
            remito.ExpiresOn = today.AddDays(RemitoTerms.ValidityDays(remito.DistanceKm));
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

/// <summary>A remito held for change: read fresh inside its lock, which the hold keeps until it is disposed.</summary>
public sealed class RemitoHold(IDisposable gate, Remito? remito) : IDisposable
{
    /// <summary>The remito as it stands now, or null when there is none or the caller is no party to it.</summary>
    public Remito? Remito { get; } = remito;

    public void Dispose() => gate.Dispose();
}

/// <summary>
/// The terms a remito runs by. Its validity depends on the distance by
/// wsremharina's table (manual Anexo, p.140); carne documents no table and
/// azúcar's only example does not match it (NO VERIFICADO), so ArcaSim applies
/// harina's to the three. The window to change the trip is harina's table
/// too, but carne (manual 2.5.8) and azúcar (manual 16.5) say 24 hours flat.
/// </summary>
public static class RemitoTerms
{
    /// <summary>The window carne's modificarViaje and azúcar's modificarConductor give, from the issue, whatever the distance.</summary>
    public const int FlatChangeHours = 24;

    public static int ValidityDays(decimal km) => km switch
    {
        <= 100 => 2,
        <= 500 => 3,
        <= 1000 => 5,
        _ => 10,
    };

    /// <summary>Harina's window to change the trip, by distance (manual Anexo, p.140).</summary>
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
