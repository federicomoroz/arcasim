using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>
/// One party calling one of the group's services over HTTP with its own
/// ticket, the way a client generated from the WSDL would, and every answer
/// checked against that WSDL.
/// </summary>
public sealed class ServiceClient
{
    private readonly ArcaSimHarness _sim;
    private readonly ServiceContract _contract;
    private readonly string _service;

    private ServiceClient(ArcaSimHarness sim, string service, ServiceContract contract, long cuit, string token, string sign)
    {
        _sim = sim;
        _service = service;
        _contract = contract;
        Cuit = cuit;
        Token = token;
        Sign = sign;
    }

    public long Cuit { get; }
    public string Token { get; }
    public string Sign { get; }

    /// <summary>The authRequest block of the remitos.</summary>
    public string Auth => $"<authRequest><token>{Token}</token><sign>{Sign}</sign><cuitRepresentada>{Cuit}</cuitRepresentada></authRequest>";

    /// <summary>The same three fields loose, for the requests declared as AuthRequestType itself.</summary>
    public string LooseAuth => $"<token>{Token}</token><sign>{Sign}</sign><cuitRepresentada>{Cuit}</cuitRepresentada>";

    public string Namespace => _contract.TargetNamespace;

    public static async Task<ServiceClient> LoginAsync(ArcaSimHarness sim, string service, long cuit)
    {
        var contract = Load(service);
        var certificate = await sim.IssueCertificateAsync(cuit, $"{service}-{cuit}", service);
        var ticket = await sim.Wsaa(cuit, certificate).LoginAsync(service);
        return new ServiceClient(sim, service, contract, cuit, ticket.Token, ticket.Sign);
    }

    /// <summary>Posts the operation's element with the inner XML given, and returns the Body's element after checking it against the WSDL.</summary>
    public async Task<XElement> CallAsync(string operation, string inner, string? element = null, string prefix = "ns")
    {
        var (status, body) = await PostAsync(operation, inner, element, prefix);
        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Validate(_service, answer);
        return answer;
    }

    public Task<(int Status, string Body)> PostAsync(string operation, string inner, string? element = null, string prefix = "ns", CancellationToken ct = default)
    {
        var name = element ?? operation + "Request";
        var envelope =
            $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:{prefix}=\"{Namespace}\">" +
            $"<soapenv:Header/><soapenv:Body><{prefix}:{name}>{inner}</{prefix}:{name}></soapenv:Body></soapenv:Envelope>";
        return _sim.PostSoapAsync(new Uri("http://localhost" + _contract.AddressPath), envelope, $"\"{Namespace}{operation}\"", ct);
    }

    public static ServiceContract Load(string service) =>
        ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", $"{service}-homologacion.wsdl"));

    /// <summary>The answer is valid for the WSDL ARCA publishes: what a generated client deserializes.</summary>
    public static void Validate(string service, XElement answer)
    {
        var contract = Load(service);
        var problems = new List<string>();
        new XDocument(new XElement(answer)).Validate(contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + answer);
    }
}

public static class RemitoTestKit
{
    /// <summary>A Thursday, 01/10/2026 at noon in Argentina: every date in the group's tests counts from here.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    public const long Issuer = ArcaSimHarness.Issuer;
    public const long Holder = 30000000007;
    public const long Receiver = 20222222223;
    public const long Depositary = 27333333339;

    /// <summary>ArcaSim frozen at <see cref="Now"/> with the issuer, a holder, a receiver and a depositary registered.</summary>
    public static async Task<ArcaSimHarness> StartAsync()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(Now);
        await sim.PutTaxpayerAsync(Issuer, "Molino de Prueba SA", VatCondition.ResponsableInscripto, new PointOfSale(1, PointOfSaleKind.WebServiceCae));
        await sim.PutTaxpayerAsync(Holder, "Titular de Mercaderia SA", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Receiver, "Ana Panadera", VatCondition.Monotributo);
        await sim.PutTaxpayerAsync(Depositary, "Deposito Tercero", VatCondition.ResponsableInscripto);
        return sim;
    }

    /// <summary>The first child with that local name, anywhere below.</summary>
    public static XElement One(this XElement element, string name) =>
        element.Descendants().FirstOrDefault(e => e.Name.LocalName == name) ?? throw new Xunit.Sdk.XunitException($"No <{name}> in {element}");

    public static string Value(this XElement element, string name) => element.One(name).Value;

    public static bool Has(this XElement element, string name) => element.Descendants().Any(e => e.Name.LocalName == name);

    /// <summary>The (codigo, descripcion) pairs of the answer's error block.</summary>
    public static (string Code, string Text) Error(this XElement element) =>
        element.One("arrayErrores").Elements().Select(e => (e.Elements().First(c => c.Name.LocalName == "codigo").Value,
            e.Elements().First(c => c.Name.LocalName == "descripcion").Value)).First();
}
