using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.CpeGranos;

/// <summary>
/// Calls wscpe or wslpg the way their manuals write requests (prefixed root,
/// unqualified children), one ticket per CUIT, and checks every answer
/// against the service's WSDL.
/// </summary>
internal sealed class GrainsSoap(ArcaSimHarness sim, string service, string path, string ns, string cuitField)
{
    private readonly Dictionary<long, string> _auth = [];
    private readonly ServiceContract _contract =
        ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", service + "-homologacion.wsdl"));

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
        _auth[cuit] = $"<auth><token>{ticket.Token}</token><sign>{ticket.Sign}</sign><{cuitField}>{cuit}</{cuitField}></auth>";
    }

    /// <summary>Posts the operation as <paramref name="cuit"/>, expects HTTP 200 and an answer valid for the WSDL, and returns the Body's element.</summary>
    public async Task<XElement> CallAsync(long cuit, string operation, string root, string inner = "")
    {
        var envelope = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                       $"xmlns:ns=\"{ns}\"><soapenv:Header/><soapenv:Body><ns:{root}>{_auth[cuit]}{inner}</ns:{root}></soapenv:Body></soapenv:Envelope>";
        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + path), envelope, $"\"{ns}{operation}\"");
        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Validate(answer);
        return answer;
    }

    private void Validate(XElement answer)
    {
        var problems = new List<string>();
        new XDocument(new XElement(answer)).Validate(_contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + answer);
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
