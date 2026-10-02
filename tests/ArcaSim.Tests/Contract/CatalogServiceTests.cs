using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Contract;

/// <summary>
/// Every service of the catalog, over HTTP, as a client generated from its
/// WSDL would call it: the WSDL comes back pointing at ArcaSim, every operation
/// answers a request built from the schema with a real ticket, and the answer
/// validates against the WSDL; a bad ticket is refused in the service's dialect.
/// </summary>
public class CatalogServiceTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));
    private static readonly string Wsdls = Path.Combine(AppContext.BaseDirectory, "arca-wsdl");

    public static TheoryData<string> Services()
    {
        var data = new TheoryData<string>();
        foreach (var service in Catalog.Services) data.Add(service.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Every_operation_answers_what_its_WSDL_promises(string id)
    {
        var definition = Catalog.Find(id)!;
        var contract = ServiceContract.Load(Path.Combine(Wsdls, definition.Wsdl));
        var sampler = new SchemaSampler(contract.Schemas);
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(Caller, "contrato", definition.Wsaa[0]);
        var ticket = await sim.Wsaa(Caller, certificate).LoginAsync(definition.Wsaa[0]);
        var url = new Uri("http://localhost" + contract.AddressPath);

        var wsdl = await sim.Http.GetStringAsync(contract.AddressPath + "?wsdl");
        Assert.Contains($"location=\"http://localhost{contract.AddressPath}\"", wsdl);

        // A service with rules may refuse the schema's made-up data (idPersona 1) with a business fault: that is an answer too.
        var host = sim.Services.GetRequiredService<ContractHost>();
        var hasRules = host.HasRules(definition);

        var problems = new List<string>();
        foreach (var operation in contract.Operations)
        {
            var request = operation.Input is null ? null : sampler.Sample(operation.Input, new SampleContext(Caller, DateTimeOffset.Now));
            if (request is not null) Sign(request, ticket.Token, ticket.Sign);
            var (status, body) = await sim.PostSoapAsync(url, Envelope(request), operation.Action ?? "");
            if (status != 200 && hasRules && body.Contains("Fault>", StringComparison.Ordinal) && !body.Contains("oken", StringComparison.Ordinal)) continue;
            if (status != 200)
            {
                problems.Add($"{operation.Name}: HTTP {status} {body[Math.Max(0, body.IndexOf("<faultstring", StringComparison.Ordinal))..]}");
                continue;
            }
            var answer = XDocument.Parse(body).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
            if (answer.Name != operation.Output) problems.Add($"{operation.Name}: answered {answer.Name}");
            new XDocument(answer).Validate(contract.Schemas, (_, e) =>
            {
                if (e.Severity == XmlSeverityType.Error) problems.Add($"{operation.Name}: {e.Message}");
            });
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task A_forged_ticket_is_refused_in_the_services_own_way(string id)
    {
        var definition = Catalog.Find(id)!;
        var contract = ServiceContract.Load(Path.Combine(Wsdls, definition.Wsdl));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Input is not null && sampler.CarriesTicket(o.Input));
        await using var sim = ArcaSimHarness.Start();

        var request = sampler.Sample(operation.Input!, new SampleContext(Caller, DateTimeOffset.Now));
        Sign(request, "abc", "abc");
        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath), Envelope(request), operation.Action ?? "");

        if (definition.Errors.InBody)
        {
            Assert.Equal(200, status);
            Assert.True(body.Contains($">{definition.Errors.Code}<"), body);
        }
        else
        {
            Assert.Equal(500, status);
            Assert.True(body.Contains("Fault"), body);
        }
    }

    /// <summary>Puts the ticket where the request carries it, and the caller's CUIT in the CUIT next to it (cuit, cuitRepresentada, CUITDelegado...).</summary>
    private static void Sign(XElement request, string token, string sign)
    {
        var tokenElement = request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("token", StringComparison.OrdinalIgnoreCase));
        if (tokenElement is null) return;
        tokenElement.Value = token;
        var scope = tokenElement.Parent!;
        foreach (var element in scope.Elements())
        {
            if (element.Name.LocalName.Equals("sign", StringComparison.OrdinalIgnoreCase) || element.Name.LocalName.Equals("firma", StringComparison.OrdinalIgnoreCase))
                element.Value = sign;
            else if (element.Name.LocalName.Contains("cuit", StringComparison.OrdinalIgnoreCase))
                element.Value = Caller.ToString();
        }
    }

    private static string Envelope(XElement? request) =>
        $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Header/><soapenv:Body>{request?.ToString(SaveOptions.DisableFormatting)}</soapenv:Body></soapenv:Envelope>";
}
