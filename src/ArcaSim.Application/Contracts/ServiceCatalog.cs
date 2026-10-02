using System.Text.Json;
using System.Text.Json.Serialization;
using ArcaSim.Application.Access;

namespace ArcaSim.Application.Contracts;

/// <summary>
/// How a family of ARCA servers speaks SOAP. Each family has its own envelope
/// prefixes and its own way of refusing a request (docs/arca/catalogo.md §4.1).
/// </summary>
public enum Dialect
{
    /// <summary>.NET ASMX (wswhomo/servicios1, aduana): default namespaces, errors inside the result with HTTP 200.</summary>
    Asmx,

    /// <summary>Apache CXF (the padrón, Ventanilla Electrónica, SIRE, DDJJ): soap: envelope, ns2 response, faults with HTTP 500.</summary>
    Cxf,

    /// <summary>JAX-WS RI (fwshomo/serviciosjava: wsmtxca, wsct, FCE, agro, remitos): S: envelope, ns2 response, "[wscommon_NNN]" faults.</summary>
    JaxWs,

    /// <summary>Spring-WS (SUD, the "factu.fisca" Spring Boot services): SOAP-ENV: envelope with an empty Header, ns2 on every qualified element.</summary>
    SpringWs,
}

/// <summary>
/// How a service says the ticket was not accepted: a fault (its code, the
/// element in its detail, the HTTP status it travels with), or an error block
/// in the body with a code, a text prefix and, when the names do not give it
/// away, the block's and fields' names. Codes and texts can differ by problem.
/// CuitField names the element with the represented CUIT when it is not
/// cuit, Cuit or cuitRepresentada next to the token.
/// </summary>
public sealed record AuthErrors(
    bool InBody = false,
    long Code = 600,
    string Prefix = "",
    string FaultCode = "soap:Server",
    string? Detail = null,
    int Status = 500,
    string? Block = null,
    string? CodeField = null,
    string? TextField = null,
    string? CuitField = null,
    Dictionary<TicketProblem, long>? Codes = null,
    Dictionary<TicketProblem, string>? Texts = null)
{
    public ErrorShape Shape => new(Block, CodeField, TextField);

    public long CodeFor(TicketProblem problem) => Codes?.GetValueOrDefault(problem) is { } code and not 0 ? code : Code;
}

/// <summary>
/// One ARCA web service as ArcaSim answers it: the WSDL it publishes, the
/// WSAA ids that open it, its dialect and how it refuses tickets, values that
/// always travel the same (FEHeaderInfo's ambiente, a version), and how far
/// the simulation goes ("reglas" when it keeps state and applies ARCA's rules,
/// "contrato" when it answers the contract with valid data).
/// </summary>
public sealed record ServiceDefinition(
    string Id,
    string Name,
    string Group,
    string Wsdl,
    string[] Wsaa,
    Dialect Dialect,
    AuthErrors? Auth = null,
    Dictionary<string, string>? Values = null,
    string Prefix = "ns2",
    string? EnvelopePrefix = null,
    string Level = "contrato",
    string? Notes = null)
{
    public AuthErrors Errors => Auth ?? (Dialect == Dialect.Asmx ? new AuthErrors(InBody: true) : new AuthErrors());
}

/// <summary>The services of docs/arca/servicios.json: every ARCA web service ArcaSim answers through its WSDL.</summary>
public sealed class ServiceCatalog
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<ServiceDefinition> Services { get; }

    public ServiceCatalog(IEnumerable<ServiceDefinition> services) => Services = services.ToList();

    public static ServiceCatalog Load(string file) =>
        new(JsonSerializer.Deserialize<List<ServiceDefinition>>(File.ReadAllText(file), Json) ?? []);

    public ServiceDefinition? Find(string id) => Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
