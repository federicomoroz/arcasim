using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Contract;

/// <summary>The numbers a catalog text counts with {seq:start}: one sequence per simulator, back to its start on reset.</summary>
public class SequenceTests
{
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    [Fact]
    public async Task A_sequence_counts_per_simulator_and_starts_over_on_reset()
    {
        await using var first = ArcaSimHarness.Start();
        await using var second = ArcaSimHarness.Start();

        var one = await ForgedTicketAsync(first);
        var two = await ForgedTicketAsync(first);
        var elsewhere = await ForgedTicketAsync(second);
        (await first.Http.PostAsync("/arcasim/api/reset", null)).EnsureSuccessStatusCode();
        var afterReset = await ForgedTicketAsync(first);

        Assert.Equal("ID MWE : 73329196", one);
        Assert.Equal("ID MWE : 73329197", two);
        Assert.Equal("ID MWE : 73329196", elsewhere);
        Assert.Equal("ID MWE : 73329196", afterReset);
    }

    /// <summary>wgesTabRef answers an unreadable token with 7004 and the next "ID MWE" of its sequence.</summary>
    private static async Task<string> ForgedTicketAsync(ArcaSimHarness sim)
    {
        var definition = Catalog.Find("wgesTabRef")!;
        var contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", definition.Wsdl));
        var sampler = new SchemaSampler(contract.Schemas);
        var operation = contract.Operations.First(o => o.Input is not null && sampler.CarriesTicket(o.Input));
        var request = sampler.Sample(operation.Input!, new SampleContext(ArcaSimHarness.Issuer, new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-3))));
        CatalogServiceTests.Sign(request, "abc", "abc");

        var (_, body) = await sim.PostSoapAsync(new Uri("http://localhost" + contract.AddressPath), CatalogServiceTests.Envelope(request), operation.Action ?? "");

        return XDocument.Parse(body).Descendants().First(e => e.Name.LocalName == "InfoAdicional").Value;
    }
}
