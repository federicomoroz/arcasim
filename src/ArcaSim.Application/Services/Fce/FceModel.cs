using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Fce;

/// <summary>A voucher's identity across the three FCE services: issuer, type, point of sale and number.</summary>
public sealed record FceId(long Cuit, int Type, int PointOfSale, long Number)
{
    public string Key => AuthorizedVouchers.Key(Cuit, PointOfSale, Type, Number);
}

/// <summary>A state and the instant from which it holds (fechaHoraEstado).</summary>
public sealed record FceState(string State, DateTimeOffset Since);

/// <summary>A rejection reason as wsfecred takes and returns it: the table's code, its text, and the buyer's justification.</summary>
public sealed record FceReason(short Code, string Description, string Justification);

public sealed record FceCodeText(short Code, string Description);

public sealed record FceWithholding(short Code, decimal Amount, decimal Rate, string? Reason);

public sealed record FceAdjustment(short Code, decimal Amount);

public sealed record FceVat(short Code, decimal Base, decimal Amount);

public sealed record FceTax(short Code, string? Detail, decimal Base, decimal Amount);

/// <summary>
/// One FCE voucher (invoice, debit or credit note) as wsfecred shows it: what
/// WSFEv1 authorized, the current account it belongs to, and its state history.
/// </summary>
public sealed class FceVoucher
{
    public FceId Id { get; set; } = null!;
    public long Receiver { get; set; }
    public string AuthorizationKind { get; set; } = "E";
    public long AuthorizationCode { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>fechaPuestaDispo: the day the voucher reached the buyer's domicilio fiscal electrónico.</summary>
    public DateOnly AvailableOn { get; set; }

    public DateOnly? PaymentDue { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "PES";
    public decimal Rate { get; set; } = 1;
    public string? Cbu { get; set; }
    public string? Alias { get; set; }

    /// <summary>The transfer option the invoice was issued with (WSFEv1 optional 27), for invoices.</summary>
    public string? Option { get; set; }

    /// <summary>esAnulacion: a cancelling note (WSFEv1 optional 22 = S).</summary>
    public bool Cancels { get; set; }

    /// <summary>esPostAceptacion: a note issued once the account had left Modificable.</summary>
    public bool PostAcceptance { get; set; }

    public FceId? Associated { get; set; }
    public List<string> References { get; set; } = [];
    public List<FceVat> Vat { get; set; } = [];
    public List<FceTax> Taxes { get; set; } = [];
    public List<FceId> DeliveryNotes { get; set; } = [];
    public long Account { get; set; }
    public List<FceState> History { get; set; } = [];
    public string? AcceptanceKind { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public List<FceReason> Rejection { get; set; } = [];

    public FceState State => History[^1];

    public bool IsInvoice => FceTypes.IsInvoice(Id.Type);

    public bool IsDebit => FceTypes.IsDebit(Id.Type);

    /// <summary>"Tipo A" in the manual's diagram: the invoice and the notes issued before acceptance that are not cancelling ones. Only they move the balance.</summary>
    public bool CountsInBalance => IsInvoice || (!Cancels && !PostAcceptance);

    public void MoveTo(string state, DateTimeOffset at)
    {
        if (State.State != state) History.Add(new FceState(state, at));
    }
}

/// <summary>The buyer's choice of SCA, and what the Sistema de Circulación Abierta has done with the invoice (wsfecredsca).</summary>
public sealed class FceSca
{
    public DateTimeOffset AvailableAt { get; set; }

    /// <summary>T (tácita) or E (expresa), as wsfecredsca writes it.</summary>
    public string AcceptanceKind { get; set; } = "E";

    public bool InformsCbu { get; set; }
    public string? Cbu { get; set; }
    public bool? CbuValidated { get; set; }

    /// <summary>D disponible, P pendiente de recepción, R recibida.</summary>
    public string State { get; set; } = "D";

    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>The seller's report to an Agente de Depósito Colectivo, and what the agent did with it (wsfecredagente).</summary>
public sealed class FceAgentReport
{
    public long Agent { get; set; }
    public string AccountId { get; set; } = "";
    public string? Denomination { get; set; }
    public DateTimeOffset AvailableAt { get; set; }

    /// <summary>D disponible, P pendiente, A aceptada, R rechazada.</summary>
    public string State { get; set; } = "D";

    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public short? RejectionCode { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// The current account an FCE invoice opens (CuentaCorrienteType): its state
/// history, the notes that joined it, what the buyer informed when accepting
/// or cancelling, and where the invoice went afterwards (agent or SCA).
/// </summary>
public sealed class FceAccount
{
    public long Code { get; set; }
    public FceId Invoice { get; set; } = null!;
    public long Issuer { get; set; }
    public long Receiver { get; set; }
    public string Option { get; set; } = "ADC";
    public string Currency { get; set; } = "PES";
    public decimal Initial { get; set; }
    public decimal LastRate { get; set; } = 1;
    public DateOnly AcceptanceDue { get; set; }
    public List<string> Notes { get; set; } = [];
    public List<FceState> History { get; set; } = [];
    public string? AcceptanceKind { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public List<FceCodeText> Forms { get; set; } = [];
    public List<FceWithholding> Withholdings { get; set; } = [];
    public List<FceAdjustment> Adjustments { get; set; } = [];
    public decimal? Cancelled { get; set; }
    public decimal? WithholdingsTotal { get; set; }
    public decimal? Embargo { get; set; }
    public decimal? AcceptedBalance { get; set; }
    public FceSca? Sca { get; set; }
    public FceAgentReport? Agent { get; set; }

    public FceState State => History[^1];

    public void MoveTo(string state, DateTimeOffset at)
    {
        if (State.State != state) History.Add(new FceState(state, at));
    }

    /// <summary>The state the account was in at an instant: what a note issued then finds.</summary>
    public string StateAt(DateTimeOffset at) => History.LastOrDefault(h => h.Since <= at)?.State ?? History[0].State;
}

/// <summary>An account an Agente de Depósito Colectivo opened for a seller (altaCuentasAgente), which the seller then reports invoices to.</summary>
public sealed class FceAgentAccount
{
    public long Agent { get; set; }
    public string AccountId { get; set; } = "";
    public long Holder { get; set; }
    public string? Denomination { get; set; }

    /// <summary>A activa, B dada de baja.</summary>
    public string State { get; set; } = "A";

    public DateOnly OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }

    public string Key => $"{Agent}/{AccountId}";
}

public static class FceTypes
{
    public static bool IsInvoice(int type) => type is 201 or 206 or 211;

    public static bool IsDebit(int type) => type is 202 or 207 or 212;

    public static bool IsCredit(int type) => type is 203 or 208 or 213;

    public static bool IsFce(int type) => IsInvoice(type) || IsDebit(type) || IsCredit(type);

    /// <summary>Remitos a FCE invoice may reference in CbtesAsoc (wsfev1.md §4.4): what obtenerRemitos returns.</summary>
    public static bool IsDeliveryNote(int type) => type is 91 or 990 or 991 or 993 or 994 or 995;
}

/// <summary>The literals of the WSDL's state enumerations.</summary>
public static class FceStates
{
    public const string PendingReception = "PendienteRecepcion";
    public const string Received = "Recepcionado";
    public const string Accepted = "Aceptado";
    public const string Rejected = "Rechazado";
    public const string Reported = "InformadaAgDpto";

    public const string Modifiable = "Modificable";
    public const string AccountAccepted = "Aceptada";
    public const string AccountRejected = "Rechazada";
    public const string Cancelled = "CanceladaTotal";
}
