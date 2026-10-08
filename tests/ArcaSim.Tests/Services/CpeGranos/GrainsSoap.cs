using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Services.CpeGranos;

/// <summary>
/// Calls wscpe or wslpg the way their manuals write requests (prefixed root,
/// unqualified children), one ticket per CUIT, and checks every answer
/// against the service's WSDL.
/// </summary>
internal sealed class GrainsSoap(ArcaSimHarness sim, string service, string path, string ns, string cuitField)
{
    private readonly Dictionary<long, string> _auth = [];
    private readonly ServiceContract _contract = Contracts.Of(service + "-homologacion.wsdl");

    public static GrainsSoap Wscpe(ArcaSimHarness sim) =>
        new(sim, "wscpe", "/wscpe/services/soap", "https://serviciosjava.afip.gob.ar/wscpe/", "cuitRepresentada");

    public static GrainsSoap Wslpg(ArcaSimHarness sim) =>
        new(sim, "wslpg", "/wslpg/LpgService", "http://serviciosjava.afip.gob.ar/wslpg/", "cuit");

    /// <summary>Registers the CUIT and logs it in to the service.</summary>
    public async Task LoginAsync(long cuit, string name = "Empresa")
    {
        await sim.PutTaxpayerAsync(cuit, name, VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(cuit, "granos" + cuit, service);
        var ticket = await sim.Wsaa(cuit, certificate).LoginAsync(service);
        _auth[cuit] = $"<auth>{Login.Credentials(ticket, cuit, cuitField)}</auth>";
    }

    /// <summary>Posts the operation as <paramref name="cuit"/>, expects HTTP 200 and an answer valid for the WSDL, and returns the Body's element.</summary>
    public async Task<XElement> CallAsync(long cuit, string operation, string root, string inner = "")
    {
        var envelope = Soap.Envelope($"<ns:{root}>{_auth[cuit]}{inner}</ns:{root}>", ("ns", ns));
        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + path), envelope, $"\"{ns}{operation}\"");
        Assert.True(status == 200, body);
        var answer = Soap.Body(body);
        Xsd.AssertValid(answer, _contract);
        return answer;
    }
}

internal static class AnswerReading
{
    public static string? Value(this XElement answer, string name) =>
        answer.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    /// <summary>The first error of the answer: code and text.</summary>
    public static (string Code, string Text)? FirstError(this XElement answer) =>
        answer.Descendants("error").FirstOrDefault() is { } error
            ? (error.Element("codigo")!.Value, error.Element("descripcion")!.Value)
            : null;
}
