using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>
/// One catalog service called over HTTP the way a client generated from its
/// WSDL calls it: a real WSAA ticket for the issuer, the operation's element
/// in its namespace, and every answer checked against the WSDL ARCA publishes.
/// </summary>
internal sealed class ServiceProbe(ArcaSimHarness sim, ServiceContract contract, string token, string sign)
{
    public const long Caller = ArcaSimHarness.Issuer;

    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    public string Token { get; } = token;
    public string Sign { get; } = sign;
    public ArcaSimHarness Sim => sim;
    public ServiceContract Contract => contract;
    public IDocumentStore Store => sim.Services.GetRequiredService<IDocumentStore>();

    /// <summary>ArcaSim frozen on Thursday 01/10/2026 at noon, with the issuer registered and a ticket for the service.</summary>
    public static async Task<ServiceProbe> StartAsync(ArcaSimHarness sim, string service, string? wsaa = null)
    {
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        var definition = Catalog.Find(service)!;
        var contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", definition.Wsdl));
        await sim.PutTaxpayerAsync(Caller, "Empresa de Prueba SA", VatCondition.ResponsableInscripto);
        var id = wsaa ?? definition.Wsaa[0];
        var certificate = await sim.IssueCertificateAsync(Caller, id, id);
        var ticket = await sim.Wsaa(Caller, certificate).LoginAsync(id);
        return new ServiceProbe(sim, contract, ticket.Token, ticket.Sign);
    }

    /// <summary>
    /// Posts the operation's element with these children. Qualified services
    /// (ASMX, Spring-WS) get the operation's namespace as the default one; the
    /// rest get it on a prefix, so their children travel unqualified.
    /// </summary>
    public async Task<SoapAnswer> CallAsync(string operation, string inner, bool qualified = false, string? ns = null)
    {
        var op = contract.Operations.First(o => o.Name == operation);
        var input = op.Input!;
        var namespaceName = ns ?? input.NamespaceName;
        var element = qualified
            ? $"<{input.LocalName} xmlns=\"{namespaceName}\">{inner}</{input.LocalName}>"
            : $"<x:{input.LocalName} xmlns:x=\"{namespaceName}\">{inner}</x:{input.LocalName}>";
        var envelope = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Header/>" +
                       $"<soapenv:Body>{element}</soapenv:Body></soapenv:Envelope>";
        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath), envelope, op.Action ?? "");
        return new SoapAnswer(status, body, contract);
    }
}

/// <summary>What came back: the Body's element, valid for the WSDL, or the fault's text.</summary>
internal sealed record SoapAnswer(int Status, string Body, ServiceContract Contract)
{
    public XElement Element => XDocument.Parse(Body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();

    /// <summary>The answer, after checking it is a 200 that validates against the WSDL: what a generated client deserializes.</summary>
    public XElement Valid()
    {
        Assert.True(Status == 200, Body);
        var answer = Element;
        Assert.NotEqual("Fault", answer.Name.LocalName);
        var problems = new List<string>();
        new XDocument(answer).Validate(Contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + Body);
        return answer;
    }

    /// <summary>The fault's text (faultstring, or Reason/Text in SOAP 1.2).</summary>
    public string Fault
    {
        get
        {
            var fault = Element;
            Assert.True(fault.Name.LocalName == "Fault", Body);
            return fault.Descendants().First(e => e.Name.LocalName is "faultstring" or "Text").Value;
        }
    }

    public string FaultCode => Element.Descendants().First(e => e.Name.LocalName is "faultcode" or "Value").Value;
}

internal static class XmlRead
{
    public static string Value(this XElement element, string name) =>
        element.DescendantsAndSelf().First(e => e.Name.LocalName == name).Value;

    public static List<XElement> All(this XElement element, string name) =>
        element.Descendants().Where(e => e.Name.LocalName == name).ToList();
}
