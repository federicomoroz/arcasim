using System.Security;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Services.Aduana;

/// <summary>
/// One service's rules called straight, as ContractHost calls them once the
/// ticket passed, over a store the test chooses: what a race between requests
/// needs, since the harness's own store cannot be swapped. Every answer is
/// checked against the WSDL, like the ones that come over HTTP.
/// </summary>
internal sealed class RulesProbe
{
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    private readonly IServiceBehavior _rules;
    private readonly ServiceDefinition _definition;
    private readonly ServiceContract _contract;
    private readonly SchemaSampler _sampler;
    private readonly DateTimeOffset _now;

    public RulesProbe(IServiceBehavior rules, DateTimeOffset now)
    {
        _rules = rules;
        _now = now;
        _definition = Catalog.Find(rules.Service)!;
        _contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", _definition.Wsdl));
        _sampler = new SchemaSampler(_contract.Schemas);
    }

    /// <summary>Calls an operation for a CUIT and returns its *Result element.</summary>
    public async Task<XElement> CallAsync(string operation, string inner, long cuit)
    {
        var request = XElement.Parse($"<{operation} xmlns=\"{SecurityElement.Escape(_contract.TargetNamespace)}\">{inner}</{operation}>");
        var call = new ServiceCall(_definition, _contract, _sampler, _contract.Operations.First(o => o.Name == operation),
            request, cuit, new SampleContext(cuit, _now) { Always = _definition.Always });
        var answer = await _rules.AnswerAsync(call, CancellationToken.None);
        Assert.NotNull(answer?.Body);
        AduanaKit.Validate(_contract, answer.Body);
        return answer.Body.Elements().First();
    }
}
