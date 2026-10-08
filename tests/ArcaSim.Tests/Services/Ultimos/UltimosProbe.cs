using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.Ultimos;

/// <summary>
/// One catalog service called over HTTP the way a client generated from its
/// WSDL calls it: a real WSAA ticket for the caller, the operation's element
/// in its namespace (the default one for ASMX, a prefix otherwise), and every
/// answer checked against the service's WSDL. LoginAsync gets a new ticket
/// once a test moves the clock past the old one.
/// </summary>
internal sealed class UltimosProbe
{
    public const long Caller = ArcaSimHarness.Issuer;

    private readonly X509Certificate2 _certificate;
    private readonly string _wsaa;
    private readonly bool _qualified;

    private UltimosProbe(ArcaSimHarness sim, ServiceContract contract, X509Certificate2 certificate, string wsaa, bool qualified)
    {
        Sim = sim;
        Contract = contract;
        _certificate = certificate;
        _wsaa = wsaa;
        _qualified = qualified;
    }

    public ArcaSimHarness Sim { get; }
    public ServiceContract Contract { get; }
    public string Token { get; private set; } = "";
    public string Sign { get; private set; } = "";
    public IDocumentStore Store => Sim.Services.GetRequiredService<IDocumentStore>();

    /// <summary>ArcaSim frozen on Thursday 01/10/2026 at noon, the caller registered, and a ticket for the service (its first WSAA id unless another is given).</summary>
    public static async Task<UltimosProbe> StartAsync(ArcaSimHarness sim, string service, string? wsaa = null)
    {
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        var definition = Contracts.Definition(service);
        var contract = Contracts.Of(definition);
        await sim.PutTaxpayerAsync(Caller, "Organismo de Prueba", VatCondition.ResponsableInscripto);
        var id = wsaa ?? definition.Wsaa[0];
        var probe = new UltimosProbe(sim, contract, await sim.IssueCertificateAsync(Caller, id, id), id, definition.Dialect == Dialect.Asmx);
        await probe.LoginAsync();
        return probe;
    }

    public async Task LoginAsync()
    {
        var ticket = await Sim.Wsaa(Caller, _certificate).LoginAsync(_wsaa);
        Token = ticket.Token;
        Sign = ticket.Sign;
    }

    /// <summary>Posts the operation's element with these children.</summary>
    public Task<SoapReply> CallAsync(string operation, string inner)
    {
        var op = Contract.Operations.First(o => o.Name == operation);
        var input = op.Input!;
        var element = _qualified
            ? $"<{input.LocalName} xmlns=\"{input.NamespaceName}\">{inner}</{input.LocalName}>"
            : $"<x:{input.LocalName} xmlns:x=\"{input.NamespaceName}\">{inner}</x:{input.LocalName}>";
        return PostAsync(element, op.Action ?? "");
    }

    /// <summary>Posts whatever goes in the Body, even nothing, with that SOAPAction.</summary>
    public async Task<SoapReply> PostAsync(string body, string action)
    {
        var (status, text) = await Sim.PostSoapAsync(new Uri("http://localhost" + Contract.AddressPath), Soap.Envelope(body), action);
        return new SoapReply(status, text, Contract);
    }

    /// <summary>Puts a document through the admin API, as an operator preloads what nobody writes through ARCA's API.</summary>
    public async Task PutDocumentAsync(string collection, string key, object document)
    {
        var response = await Sim.Http.PutAsJsonAsync($"/arcasim/api/documents/{collection}/{key}", document);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>What came back: the Body's element, valid for the WSDL, or the fault.</summary>
internal sealed record SoapReply(int Status, string Body, ServiceContract Contract)
{
    public XElement Element => Soap.Body(Body);

    /// <summary>The answer, after checking it is a 200 that validates against the WSDL: what a generated client deserializes.</summary>
    public XElement Valid()
    {
        Assert.True(Status == 200, Body);
        var answer = Element;
        Assert.NotEqual("Fault", answer.Name.LocalName);
        Xsd.AssertValid(answer, Contract);
        return answer;
    }

    /// <summary>The fault's text, after checking it is one.</summary>
    public string Fault
    {
        get
        {
            var fault = Element;
            Assert.True(fault.Name.LocalName == "Fault", Body);
            return fault.Element("faultstring")!.Value;
        }
    }

    public string FaultCode => Element.Element("faultcode")!.Value;
}

internal static class ReplyXml
{
    public static string Value(this XElement element, string name) =>
        element.DescendantsAndSelf().First(e => e.Name.LocalName == name).Value;

    public static List<XElement> All(this XElement element, string name) =>
        element.Descendants().Where(e => e.Name.LocalName == name).ToList();
}
