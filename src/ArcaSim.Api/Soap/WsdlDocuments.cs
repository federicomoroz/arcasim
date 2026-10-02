using ArcaSim.Domain;

namespace ArcaSim.Api.Soap;

/// <summary>
/// ARCA's own WSDL files (docs/arca/wsdl/), served with ArcaSim's address in
/// place of ARCA's and nothing else changed, so a client generated from either
/// one is the same client.
/// </summary>
public static class WsdlDocuments
{
    public static string Wsfev1(ArcaEnvironment environment, string baseUrl) =>
        Load(environment == ArcaEnvironment.Produccion ? "wsfev1-produccion.wsdl" : "wsfev1-homologacion.wsdl")
            .Replace("https://wswhomo.afip.gov.ar/wsfev1/service.asmx", $"{baseUrl}/wsfev1/service.asmx")
            .Replace("https://servicios1.afip.gov.ar/wsfev1/service.asmx", $"{baseUrl}/wsfev1/service.asmx");

    /// <summary>In WSAA's WSDL the address is also the "impl" namespace, so both change together, as they do between ARCA's environments.</summary>
    public static string Wsaa(ArcaEnvironment environment, string baseUrl) =>
        Load(environment == ArcaEnvironment.Produccion ? "wsaa-produccion.wsdl" : "wsaa-homologacion.wsdl")
            .Replace("https://wsaahomo.afip.gov.ar/ws/services/LoginCms", $"{baseUrl}/ws/services/LoginCms")
            .Replace("https://wsaa.afip.gov.ar/ws/services/LoginCms", $"{baseUrl}/ws/services/LoginCms");

    public static string BaseUrl(HttpRequest request) => $"{request.Scheme}://{request.Host}{request.PathBase}";

    public static bool AsksForWsdl(HttpRequest request) =>
        request.Query.Keys.Any(k => k.Equals("wsdl", StringComparison.OrdinalIgnoreCase));

    private static string Load(string name)
    {
        using var stream = typeof(WsdlDocuments).Assembly.GetManifestResourceStream($"ArcaSim.Wsdl.{name}")
                           ?? throw new InvalidOperationException($"WSDL {name} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
