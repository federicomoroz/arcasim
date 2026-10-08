namespace ArcaSim.Domain;

/// <summary>
/// What WSASS (homologación) and the Administrador de Relaciones (producción) keep:
/// a computer alias tied to a CUIT, and which services it may use on behalf of
/// which CUITs (docs/arca/wsaa.md §3.4).
/// </summary>
public sealed record ClientAlias(long Cuit, string Alias);

public sealed record ServiceAuthorization(long ClientCuit, string Alias, long RepresentedCuit, string Service);

/// <summary>A service id WSAA knows, with the DN that goes in the token's "dst". The application's ServiceDirectory lists them.</summary>
public sealed record WebService(string Id, string Description)
{
    public string TokenDestination => $"CN={Id}, O=AFIP, C=AR";
}

/// <summary>A TA that WSAA handed out, kept to apply the anti-repeat window.</summary>
public sealed record IssuedTicket(
    string ClientDn,
    string Service,
    DateTimeOffset GenerationTime,
    DateTimeOffset ExpirationTime);
