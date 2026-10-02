namespace ArcaSim.Application.Events;

// What happens in ArcaSim, as facts in the past tense. Times are real time, not
// ArcaSim's simulated clock: they say when it happened on this machine.

/// <summary>The traffic gate turned a request away: the service is saturated.</summary>
public sealed record RequestRefused(DateTimeOffset At, string Service) : IArcaSimEvent;

/// <summary>A request went through the gate and was answered.</summary>
public sealed record RequestServed(DateTimeOffset At, string Service, TimeSpan Took) : IArcaSimEvent;

/// <summary>WSAA handed out an access ticket.</summary>
public sealed record TicketIssued(DateTimeOffset At, string ClientDn, string Service) : IArcaSimEvent;

/// <summary>WSAA refused a login with one of its faults.</summary>
public sealed record LoginRefused(DateTimeOffset At, string FaultCode, string Message) : IArcaSimEvent;

/// <summary>A voucher (or a class B range) got its CAE, or was reported under a CAEA.</summary>
public sealed record VoucherAuthorized(
    DateTimeOffset At, long Cuit, int PointOfSale, int VoucherType, long From, long To, string EmissionType, string Code) : IArcaSimEvent;

/// <summary>A voucher was rejected; the codes are the observations or errors that rejected it.</summary>
public sealed record VoucherRejected(
    DateTimeOffset At, long Cuit, int PointOfSale, int VoucherType, long Number, IReadOnlyList<int> Codes) : IArcaSimEvent;

/// <summary>A CAEA was granted for a fortnight.</summary>
public sealed record CaeaGranted(DateTimeOffset At, long Cuit, int Period, short Fortnight, string Code) : IArcaSimEvent;

/// <summary>One of the services answered from its WSDL took a call; the outcome is "ok" or "error", with the error's text.</summary>
public sealed record ServiceCalled(DateTimeOffset At, string Service, string Operation, long Cuit, string Outcome, string Text) : IArcaSimEvent;
