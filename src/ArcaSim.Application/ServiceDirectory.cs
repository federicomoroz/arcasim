using System.Collections.Concurrent;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application;

/// <summary>
/// The services ArcaSim answers: the WSAA ids that open them (WSAA answers
/// wsn.notFound for any other) and the path each one is served on, which the
/// traffic gate, its meter and the admin API's failures go by. It is built
/// from the hand-written services and the catalog before any request, so
/// nothing depends on the order the endpoints are mapped in.
/// </summary>
public sealed class ServiceDirectory
{
    /// <summary>The services with code of their own; the catalog's come from their entries.</summary>
    private static readonly WebService[] HandWritten =
    [
        new("wsfe", "Factura Electrónica (WSFEv1)"),
        new("ws_sr_constancia_inscripcion", "Constancia de inscripción"),
        new("ws_sr_padron_a5", "Constancia de inscripción (nombre anterior)"),
        new("ws_sr_padron_a13", "Padrón A13"),
        new("seti-setipago-api", "SETIWS-PAGO-API: VEPs de organismos (REST)"),
    ];

    private readonly Dictionary<string, WebService> _services = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _routes = new(StringComparer.OrdinalIgnoreCase);

    public ServiceDirectory(ServiceCatalog catalog)
    {
        foreach (var service in HandWritten) _services.TryAdd(service.Id, service);
        foreach (var definition in catalog.Services)
        foreach (var id in definition.Wsaa)
            _services.TryAdd(id, new WebService(id, definition.Name));
        Route("/wsfev1/service.asmx", "wsfe");
        Route("/ws/services/LoginCms", "wsaa");
    }

    /// <summary>The service a WSAA id names; null for an id ArcaSim does not answer.</summary>
    public WebService? Find(string id) => _services.GetValueOrDefault(id);

    /// <summary>Puts a service on a path: its requests go through the traffic gate under that name.</summary>
    public void Route(string path, string service) => _routes[path] = service;

    /// <summary>The service a path belongs to; null for the panel, the admin API and anything no service serves.</summary>
    public string? ServiceOf(string path) => _routes.GetValueOrDefault(path);

    /// <summary>Whether the admin API's failures and limits can name this service: one served on some path.</summary>
    public bool Serves(string service) => _routes.Values.Contains(service, StringComparer.OrdinalIgnoreCase);
}
