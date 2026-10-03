namespace ArcaSim.Application.Services.Remitos;

/// <summary>A business error the way the remitos report it: a code and its text, in arrayErrores.</summary>
public sealed record RemitoProblem(long Code, string Text);

/// <summary>One state a remito went through, who moved it there and when (consultarEstadosRemito).</summary>
public sealed record RemitoStep(string State, DateTimeOffset At, long Cuit);

/// <summary>
/// One remito of the family (harina, carne, azúcar) as ArcaSim keeps it: the
/// remito element the issuer sent, adjusted by later operations (trip changes,
/// quantities received), the parties, the authorizations still owed, what
/// ARCA assigned when it was issued, and every state it went through.
/// </summary>
public sealed class Remito
{
    /// <summary>codRemito / codigoRemito: ARCA's internal id, given when the remito is generated.</summary>
    public long Code { get; set; }

    public long Issuer { get; set; }

    /// <summary>idReqCliente (harina, azúcar) or idReq (carne): unique per issuer and point of emission.</summary>
    public long RequestId { get; set; }

    public int Point { get; set; }
    public int Type { get; set; }
    public string Movement { get; set; } = "";
    public long Holder { get; set; }
    public long? Depositary { get; set; }
    public long? Receiver { get; set; }

    /// <summary>The receiver is abroad: the issuer confirms the export instead of a receiver confirming the reception.</summary>
    public bool Foreign { get; set; }

    /// <summary>Carne's receiver without CUIT: accepted on its own once the remito expires.</summary>
    public bool Uncategorized { get; set; }

    public decimal DistanceKm { get; set; }

    /// <summary>Who has to authorize (TIT, DEP) and how they answered: PE pending, AU authorized, RE refused.</summary>
    public Dictionary<string, string> Authorizations { get; set; } = new();

    /// <summary>The remito element as sent, without namespaces, kept up to date by the operations that change it.</summary>
    public string Xml { get; set; } = "";

    public long? Number { get; set; }
    public long? AuthorizationCode { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateOnly CreatedOn { get; set; }
    public DateOnly? AuthorizedOn { get; set; }
    public DateOnly? ReceivedOn { get; set; }

    /// <summary>The remito this one redirects (redestino).</summary>
    public long? Redirects { get; set; }

    /// <summary>Each contingency as it was informed, without namespaces.</summary>
    public List<string> Contingencies { get; set; } = [];

    /// <summary>Azúcar's quantities received and lost by item order, which its remito element has no place for.</summary>
    public Dictionary<int, long> Received { get; set; } = new();

    public Dictionary<int, long> Lost { get; set; } = new();

    public List<RemitoStep> History { get; set; } = [];

    public string State => History.Count == 0 ? "" : History[^1].State;

    public bool Issued => Number is not null;

    public bool Involves(long cuit) => cuit == Issuer || cuit == Holder || cuit == Depositary || cuit == Receiver;

    public void MoveTo(string state, DateTimeOffset at, long cuit) => History.Add(new RemitoStep(state, at, cuit));

    /// <summary>The authorization the remito waits for: the holder's first, then the depositary's.</summary>
    public string? Pending => Authorizations.GetValueOrDefault(RemitoStates.Holder) == "PE" ? RemitoStates.Holder
        : Authorizations.GetValueOrDefault(RemitoStates.Depositary) == "PE" ? RemitoStates.Depositary
        : null;
}

/// <summary>
/// The state codes of wsremharina's consultarTiposEstado (wsremharina.md,
/// "Máquina de estados"), which carne and azúcar are assumed to share.
/// </summary>
public static class RemitoStates
{
    public const string Holder = "TIT";
    public const string Depositary = "DEP";

    public const string PendingHolder = "PAT";
    public const string PendingDepositary = "PAD";
    public const string PendingIssue = "PEM";
    public const string Issued = "EMI";
    public const string Denied = "DEN";
    public const string CancelledUnissued = "ANS";
    public const string Cancelled = "ANU";
    public const string Accepted = "ACE";
    public const string PartlyAccepted = "ACP";
    public const string NotAccepted = "NAC";
    public const string Exported = "EXT";
    public const string PartlyExported = "EXP";
    public const string ExportRefused = "EXR";
    public const string Validated = "CON";
    public const string NotValidated = "NCO";

    public static string PendingState(string role) => role == Holder ? PendingHolder : PendingDepositary;
}
