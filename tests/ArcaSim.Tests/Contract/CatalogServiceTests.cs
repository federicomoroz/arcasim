using System.Xml.Linq;
using ArcaSim.Application.Access;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;
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

    public static TheoryData<string> Services()
    {
        var data = new TheoryData<string>();
        foreach (var service in Contracts.Catalog.Services) data.Add(service.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Every_operation_answers_what_its_WSDL_promises(string id)
    {
        var definition = Contracts.Definition(id);
        var contract = Contracts.Of(definition);
        var sampler = new SchemaSampler(contract.Schemas);
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var ticket = await sim.TicketAsync(Caller, definition.Wsaa[0]);
        var url = new Uri("http://localhost" + contract.AddressPath);

        var wsdl = await sim.Http.GetStringAsync(contract.AddressPath + "?wsdl");
        Assert.Contains($"location=\"http://localhost{contract.AddressPath}\"", wsdl);

        // A service with rules may refuse the schema's made-up data (idPersona 1) with a business fault: that is an answer too.
        var host = sim.Services.GetRequiredService<ContractHost>();
        var hasRules = host.HasRules(definition);

        var problems = new List<string>();
        var refused = new List<string>();
        var answered = 0;
        foreach (var operation in contract.Operations)
        {
            var request = operation.Input is null ? null : sampler.Sample(operation.Input, new SampleContext(Caller, sim.Clock.Now));
            if (request is not null) Support.Soap.Sign(request, ticket.Token, ticket.Sign, Caller);
            var (status, body) = await sim.PostSoapAsync(url, Support.Soap.Envelope(request), operation.Action ?? "");
            if (status != 200 && hasRules && body.Contains("Fault>", StringComparison.Ordinal) && !body.Contains("oken", StringComparison.Ordinal))
            {
                refused.Add(operation.Name);
                continue;
            }
            if (status != 200)
            {
                problems.Add($"{operation.Name}: HTTP {status} {body[Math.Max(0, body.IndexOf("<faultstring", StringComparison.Ordinal))..]}");
                continue;
            }
            var answer = Support.Soap.Body(body);
            if (answer.Name != operation.Output) problems.Add($"{operation.Name}: answered {answer.Name}");
            var invalid = Xsd.Problems(answer, contract);
            problems.AddRange(invalid.Select(problem => $"{operation.Name}: {problem}"));
            if (answer.Name == operation.Output && invalid.Count == 0) answered++;
        }

        // The refusals above are told from a crash only by the log: the engine answers a request its rules did not
        // foresee with the service's fault, which is what a refusal looks like.
        sim.AssertNoLoggedErrors();
        // Without one operation answered, the refusals would let the test pass without reading a single answer.
        if (answered == 0) problems.Add($"no operation was answered with HTTP 200 (refused: {string.Join(", ", refused)}): no answer was checked against the WSDL");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task A_forged_ticket_is_refused_in_the_services_own_way(string id)
    {
        var definition = Contracts.Definition(id);
        var contract = Contracts.Of(definition);
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Input is not null && sampler.CarriesTicket(o.Input));
        await using var sim = ArcaSimHarness.Start();

        var request = sampler.Sample(operation.Input!, new SampleContext(Caller, sim.Clock.Now));
        Support.Soap.Sign(request, "abc", "abc", Caller);
        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath), Support.Soap.Envelope(request), operation.Action ?? "");

        // "abc" is an unreadable token: the service's row for it, or for any problem, says what comes back.
        var errors = definition.Errors;
        var row = errors.Rows?.FirstOrDefault(r => r.Key == "Unreadable").Value?.FirstOrDefault()
                  ?? errors.Rows?.FirstOrDefault(r => r.Key == "*").Value?.FirstOrDefault() ?? new AuthRow();
        if (errors.InBody && !row.Fault)
        {
            Assert.Equal(200, status);
            var code = row.Code != 0 ? row.Code : errors.CodeFor(TicketProblem.Unreadable);
            Assert.True(body.Contains($">{code}<"), body);
        }
        else
        {
            Assert.Equal(row.Status ?? errors.Status, status);
            Assert.True(body.Contains("Fault"), body);
        }
        sim.AssertNoLoggedErrors();
    }

    // The group kits under Services/ still call these three on this class; they move to Support.Soap
    // with the kits, and these go with them.

    /// <summary>The SOAP envelope of an answer, out of its MTOM package when the service sends one.</summary>
    public static string Soap(string body) => Support.Soap.Unwrap(body);

    internal static void Sign(XElement request, string token, string sign) => Support.Soap.Sign(request, token, sign, Caller);

    internal static string Envelope(XElement? request) => Support.Soap.Envelope(request);
}
