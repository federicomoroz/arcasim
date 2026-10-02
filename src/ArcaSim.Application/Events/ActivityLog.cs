namespace ArcaSim.Application.Events;

/// <summary>One line of the panel's live log.</summary>
public sealed record Activity(DateTimeOffset At, string Kind, string Service, string Text);

/// <summary>
/// The last events, in words, for the panel: who logged in, which voucher got
/// which CAE, what was rejected and with which codes, what the gate turned away.
/// It only listens; nothing in ArcaSim knows it exists.
/// </summary>
public sealed class ActivityLog
{
    public const int Capacity = 200;
    private readonly object _lock = new();
    private readonly LinkedList<Activity> _entries = new();

    public ActivityLog(EventManager events) => events.SubscribeAll(e =>
    {
        if (Describe(e) is { } activity) Add(activity);
    });

    public IReadOnlyList<Activity> Latest(int count)
    {
        lock (_lock) return _entries.Take(Math.Clamp(count, 1, Capacity)).ToList();
    }

    public void Reset()
    {
        lock (_lock) _entries.Clear();
    }

    private void Add(Activity activity)
    {
        lock (_lock)
        {
            _entries.AddFirst(activity);
            while (_entries.Count > Capacity) _entries.RemoveLast();
        }
    }

    private static Activity? Describe(IArcaSimEvent e) => e switch
    {
        TicketIssued t => new(t.At, "ok", "wsaa", $"Ticket para {t.Service}: {t.ClientDn}"),
        LoginRefused l => new(l.At, "error", "wsaa", $"{l.FaultCode}: {l.Message}"),
        VoucherAuthorized v => new(v.At, "ok", "wsfe",
            $"{v.Cuit} · PV {v.PointOfSale} · tipo {v.VoucherType} · {(v.From == v.To ? $"{v.From}" : $"{v.From}-{v.To}")} → {v.EmissionType} {v.Code}"),
        VoucherRejected r => new(r.At, "error", "wsfe",
            $"{r.Cuit} · PV {r.PointOfSale} · tipo {r.VoucherType} · {r.Number} rechazado ({string.Join(", ", r.Codes)})"),
        CaeaGranted c => new(c.At, "ok", "wsfe", $"{c.Cuit} · CAEA {c.Code} para {c.Period}/{c.Fortnight}"),
        RequestRefused r => new(r.At, "refused", r.Service, "Saturado: 503"),
        ServiceCalled s => new(s.At, s.Outcome, s.Service, s.Cuit > 0 ? $"{s.Cuit} · {s.Operation}{(s.Text.Length > 0 ? $": {s.Text}" : "")}" : s.Operation),
        _ => null,
    };
}
