using System.Text.Json;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Contract;

/// <summary>Mistakes in what configures the engine stop ArcaSim from starting instead of changing an answer in silence.</summary>
public class CatalogLoadingTests
{
    [Fact]
    public void The_published_catalog_loads()
    {
        var catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

        Assert.NotEmpty(catalog.Services);
    }

    [Fact]
    public void A_misspelled_key_in_the_catalog_is_an_error()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file,
                """[{ "id": "x", "name": "x", "group": "x", "wsdl": "x.wsdl", "wsaa": ["x"], "dialect": "Cxf", "auth": { "faultCod": "soap:Client" } }]""");

            var error = Assert.Throws<JsonException>(() => ServiceCatalog.Load(file));

            Assert.Contains("faultCod", error.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Two_rules_classes_for_one_service_stop_the_host()
    {
        IServiceBehavior[] rules = [new Rules("wsfoo"), new Rules("WSFOO")];

        var error = Assert.Throws<InvalidOperationException>(() => new ContractHost(new ServiceCatalog([]), "", null!, null!, null!, rules));

        Assert.Contains("both answer", error.Message);
    }

    private sealed class Rules(string service) : IServiceBehavior
    {
        public string Service => service;

        public Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => Task.FromResult<ContractAnswer?>(null);
    }
}
