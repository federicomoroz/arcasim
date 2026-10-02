using ArcaSim.Application.Padron;

namespace ArcaSim.Api.Soap;

/// <summary>The padrón services, on the paths ARCA serves them from (awshomo / aws).</summary>
public static class PadronModule
{
    public static void Map(WebApplication app)
    {
        JavaSoapEndpoint.Map(app, new JavaSoapService(
            "/sr-padron/webservices/personaServiceA5",
            "ws_sr_constancia_inscripcion-homologacion.wsdl",
            PadronService.A5Namespace,
            "ws_sr_constancia_inscripcion",
            PadronService.A5Operations,
            (sp, op, request, ct) => sp.GetRequiredService<PadronService>().A5Async(op, request, ct)));

        JavaSoapEndpoint.Map(app, new JavaSoapService(
            "/sr-padron/webservices/personaServiceA13",
            "ws_sr_padron_a13-homologacion.wsdl",
            PadronService.A13Namespace,
            "ws_sr_padron_a13",
            PadronService.A13Operations,
            (sp, op, request, ct) => sp.GetRequiredService<PadronService>().A13Async(op, request, ct)));
    }
}
