using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Contract;

/// <summary>
/// A request no rule foresaw (a letter where the schema puts a number, which
/// the rules never check because ARCA's servers refuse it while reading it)
/// gets the service's own fault, and the next request is served as usual:
/// never a bare HTTP 500.
/// </summary>
public class UnexpectedErrorTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    [Theory]
    [InlineData("wstabaco", "solicitarDesnaturalizacion", "cathe", "x", "ns3: Receiver")]
    public async Task A_request_the_rules_did_not_foresee_gets_a_fault_in_the_services_dialect(
        string id, string operationName, string field, string value, string faultCode)
    {
        var definition = Catalog.Find(id)!;
        var contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", definition.Wsdl));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.Single(o => o.Name == operationName);
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(Caller, "errores", definition.Wsaa[0]);
        var ticket = await sim.Wsaa(Caller, certificate).LoginAsync(definition.Wsaa[0]);
        var url = new Uri("http://localhost" + contract.AddressPath);

        var request = sampler.Sample(operation.Input!, new SampleContext(Caller, sim.Clock.Now));
        CatalogServiceTests.Sign(request, ticket.Token, ticket.Sign);
        request.Descendants().First(e => e.Name.LocalName == field).Value = value;
        var (status, body) = await sim.PostSoapAsync(url, CatalogServiceTests.Envelope(request), operation.Action ?? "");
        var dummy = contract.Operations.First(o => o.Name.Equals("dummy", StringComparison.OrdinalIgnoreCase));
        var (afterStatus, _) = await sim.PostSoapAsync(url, CatalogServiceTests.Envelope(dummy.Input is null ? null : new XElement(dummy.Input)), dummy.Action ?? "");

        Assert.Equal(definition.Errors.Status, status);
        var fault = XDocument.Parse(body).Descendants().Single(e => e.Name.LocalName == "Fault");
        Assert.Equal(faultCode, fault.Descendants().First(e => e.Name.LocalName is "faultcode" or "Value").Value);
        Assert.False(string.IsNullOrWhiteSpace(fault.Descendants().First(e => e.Name.LocalName is "faultstring" or "Text").Value));
        Assert.Equal(200, afterStatus);
    }
}
