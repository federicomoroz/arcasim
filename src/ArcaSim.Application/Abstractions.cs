using System.Security.Cryptography.X509Certificates;
using ArcaSim.Domain;

namespace ArcaSim.Application;

/// <summary>ArcaSim's own time. Tests and the admin API move it to check expirations without waiting.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

/// <summary>Argentina has no daylight saving time: ARCA always answers in -03:00.</summary>
public static class ArgentinaTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateTimeOffset ToArgentina(this DateTimeOffset value) => value.ToOffset(Offset);
}

/// <summary>Aliases and service authorizations: ArcaSim's stand-in for WSASS.</summary>
public interface IAccessRepository
{
    Task<IReadOnlyList<ServiceAuthorization>> AuthorizationsForAsync(long clientCuit, string alias, string service, CancellationToken ct = default);
    Task<IReadOnlyList<ClientAlias>> ListAliasesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServiceAuthorization>> ListAuthorizationsAsync(CancellationToken ct = default);
    Task SaveAliasAsync(ClientAlias alias, CancellationToken ct = default);
    Task SaveAuthorizationAsync(ServiceAuthorization authorization, CancellationToken ct = default);
    Task DeleteAuthorizationAsync(ServiceAuthorization authorization, CancellationToken ct = default);
}

/// <summary>The TAs WSAA has issued, for the anti-repeat window.</summary>
public interface ITicketLog
{
    Task<IssuedTicket?> LatestAsync(string clientDn, string service, CancellationToken ct = default);
    Task AddAsync(IssuedTicket ticket, CancellationToken ct = default);
}

/// <summary>The certification authorities WSAA trusts to sign client certificates.</summary>
public interface ITrustStore
{
    IReadOnlyList<X509Certificate2> TrustedRoots { get; }
    IReadOnlyList<X509Certificate2> Intermediates { get; }
}

/// <summary>
/// Signs and checks the TA's "sign". ARCA's real algorithm is not public, so
/// ArcaSim signs with a key of its own; its tickets only work against ArcaSim.
/// </summary>
public interface ITokenSigner
{
    string Sign(string token);
    bool Verify(string token, string sign);
}

public interface ITaxpayerRepository
{
    Task<Taxpayer?> FindAsync(long cuit, CancellationToken ct = default);
    Task<IReadOnlyList<Taxpayer>> ListAsync(CancellationToken ct = default);
    Task SaveAsync(Taxpayer taxpayer, CancellationToken ct = default);
}

/// <summary>A store the admin API can empty, to start a test run from nothing.</summary>
public interface IResettable
{
    Task ResetAsync(CancellationToken ct = default);
}

/// <summary>Every port ArcaSim keeps state behind, as one provider (memory or PostgreSQL) implements them.</summary>
public interface ISimulatorStore :
    IAccessRepository, ITicketLog, ITaxpayerRepository, IResettable,
    Wsfe.IVoucherStore, Wsfe.ICaeaStore, Wsfe.IExchangeRates;
