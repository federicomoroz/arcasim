namespace ArcaSim.Domain;

/// <summary>
/// What WSASS (homologación) and the Administrador de Relaciones (producción) keep:
/// a computer alias tied to a CUIT, and which services it may use on behalf of
/// which CUITs (docs/arca/wsaa.md §3.4).
/// </summary>
public sealed record ClientAlias(long Cuit, string Alias)
{
    /// <summary>The DN ARCA forces on every certificate it issues, in Java's toString order.</summary>
    public string Dn => $"SERIALNUMBER=CUIT {Cuit}, CN={Alias}";
}

public sealed record ServiceAuthorization(long ClientCuit, string Alias, long RepresentedCuit, string Service);

/// <summary>A service id WSAA knows, with the DN that goes in the token's "dst".</summary>
public sealed record WebService(string Id, string Description)
{
    public string TokenDestination => $"CN={Id}, O=AFIP, C=AR";

    /// <summary>The services with their own code; the ones answered through their WSDL register at startup.</summary>
    private static readonly List<WebService> Registered =
    [
        new("wsfe", "Factura Electrónica (WSFEv1)"),
        new("ws_sr_constancia_inscripcion", "Constancia de inscripción"),
        new("ws_sr_padron_a5", "Constancia de inscripción (nombre anterior)"),
        new("ws_sr_padron_a13", "Padrón A13"),
        new("seti-setipago-api", "SETIWS-PAGO-API: VEPs de organismos (REST)"),
    ];

    /// <summary>The services ArcaSim answers for. WSAA rejects any other id with wsn.notFound.</summary>
    public static IReadOnlyList<WebService> Known
    {
        get
        {
            lock (Registered) return Registered.ToList();
        }
    }

    /// <summary>Adds a service id, from the catalog of services answered through their WSDL.</summary>
    public static void Register(string id, string description)
    {
        lock (Registered)
            if (Registered.All(s => s.Id != id)) Registered.Add(new WebService(id, description));
    }

    public static WebService? Find(string id)
    {
        lock (Registered) return Registered.FirstOrDefault(s => s.Id == id);
    }
}

/// <summary>A TA that WSAA handed out, kept to apply the anti-repeat window.</summary>
public sealed record IssuedTicket(
    string ClientDn,
    string Service,
    DateTimeOffset GenerationTime,
    DateTimeOffset ExpirationTime);
