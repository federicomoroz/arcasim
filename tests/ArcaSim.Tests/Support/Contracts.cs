using System.Collections.Concurrent;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Support;

/// <summary>
/// The service catalog and ARCA's WSDL files as the tests read them, loaded once per run.
/// Compiling a WSDL's schemas costs more than most of the checks made with it, and a
/// loaded contract is never changed, so every test shares the same instance.
/// </summary>
internal static class Contracts
{
    private static readonly ConcurrentDictionary<string, Lazy<ServiceContract>> Loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folder the Api project copies ARCA's WSDL files to.</summary>
    public static string WsdlFolder { get; } = Path.Combine(AppContext.BaseDirectory, "arca-wsdl");

    public static ServiceCatalog Catalog { get; } = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    /// <summary>The compiled contract of a WSDL file, by its name inside <see cref="WsdlFolder"/> (or by its full path).</summary>
    public static ServiceContract Of(string wsdl) =>
        Loaded.GetOrAdd(Path.GetFullPath(Path.Combine(WsdlFolder, wsdl)), file => new Lazy<ServiceContract>(() => ServiceContract.Load(file))).Value;

    public static ServiceContract Of(ServiceDefinition definition) => Of(definition.Wsdl);

    /// <summary>A service of the catalog by its id; a test that names one that is not there fails with that name.</summary>
    public static ServiceDefinition Definition(string id) =>
        Catalog.Find(id) ?? throw new InvalidOperationException($"{id} is not in the service catalog.");
}
