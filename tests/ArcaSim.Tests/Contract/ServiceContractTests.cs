using ArcaSim.Application.Contracts;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Contract;

/// <summary>Every WSDL ARCA publishes reads, and every answer the sampler writes validates against that WSDL's own schema.</summary>
public class ServiceContractTests
{
    public static TheoryData<string> Wsdls()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Contracts.WsdlFolder, "*-homologacion.wsdl").Order()) data.Add(Path.GetFileName(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Wsdls))]
    public void Every_response_the_sampler_writes_is_valid_for_the_WSDL(string wsdl)
    {
        var contract = Contracts.Of(wsdl);
        var sampler = new SchemaSampler(contract.Schemas);
        var context = new SampleContext(20111111112, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-3)));

        Assert.NotEmpty(contract.Operations);
        var problems = new List<string>();
        foreach (var operation in contract.Operations)
        {
            var answers = operation.OutputHeaders.Select(h => sampler.Sample(h, context)).Prepend(sampler.Sample(operation.Output, context)).ToList();
            if (sampler.ErrorResponse(operation.Output, context, 600, "ValidacionDeToken: Parametro nulo o vacio (token)") is { } error) answers.Add(error);
            foreach (var answer in answers)
                problems.AddRange(Xsd.Problems(answer, contract).Select(problem => $"{operation.Name} ({answer.Name.LocalName}): {problem}"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
