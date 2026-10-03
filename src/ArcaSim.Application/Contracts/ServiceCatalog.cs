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

    /// <summary>Apache Axis2 (wsmtxca, wscta): soapenv: envelope, ns1 response, faults with an empty detail and HTTP 500.</summary>
    Axis2,
}

/// <summary>
/// One error a service answers for a refused ticket: its code, its text and,
/// for faults, what goes in the detail. Text null keeps the service's text for
/// that problem; Code 0 keeps its code; Detail null keeps the service's detail
/// element, and "" sends none. Detail is written as it comes (an XML fragment
/// or plain text), after its placeholders are filled. Fault answers this
/// problem with a fault even when the service reports the others in the body,
/// as the customs services do when the authentication block is missing.
/// </summary>
public sealed record AuthRow(
    long Code = 0,
    string? Text = null,
    string? Detail = null,
    bool Fault = false,
    string? FaultCode = null,
    int? Status = null);

/// <summary>
/// How a service says the ticket was not accepted: a fault (its code, the
/// element in its detail, the HTTP status it travels with), or an error block
/// in the body with a code, a text prefix and, when the names do not give it
/// away, the block's and fields' names. Codes and texts can differ by problem.
/// CuitField names the element with the represented CUIT when it is not
/// cuit, Cuit or cuitRepresentada next to the token; CuitFromTicket is for
/// services whose request carries no CUIT (wEnysa): the entity is the ticket's.
/// Rows, by TicketProblem name, "NoTicket" (the request has no token or no
/// sign element at all) or "*" (any problem), are the exact errors a service
/// answers, one or more per problem, with their full texts (Prefix does not
/// apply to them). Texts accept placeholders: {cuit} {detail} {service} {gen}
/// {exp} {now} (epoch seconds), {now:format} {exp:format} {gen:format}
/// (Argentina time; "ms" gives epoch milliseconds), {token} {sign}
/// {signbytes} {element} (the request's root as sent), {seq:start} (a counter
/// per service), {digits:n} {letters:n} {uuid}.
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
    public Dictionary<string, AuthRow[]>? Rows { get; init; }

    public bool CuitFromTicket { get; init; }

    public ErrorShape Shape => new(Block, CodeField, TextField);

    public long CodeFor(TicketProblem problem) => Codes?.GetValueOrDefault(problem) is { } code and not 0 ? code : Code;
}

/// <summary>
/// One ARCA web service as ArcaSim answers it: the WSDL it publishes, the
/// WSAA ids that open it, its dialect and how it refuses tickets, values that
/// always travel the same (by element name or "Parent/Child" path, with the
/// same placeholders as AuthErrors' texts), and how far the simulation goes
/// ("reglas" when it keeps state and applies ARCA's rules, "contrato" when it
/// answers the contract with valid data).
/// Header is the SOAP header the service sends that its WSDL does not declare
/// (FEHeaderInfo, info, serverTime...), as an XML fragment with placeholders;
/// it replaces the declared headers and, with HeaderOnFaults, travels on
/// faults too. Always names optional elements every answer carries (the
/// events homologación repeats); Drop, elements or paths never sent. Mtom
/// answers in a multipart/related MTOM package, as veconsumerws does even
/// without attachments; DummyOnGet answers a GET to the endpoint with the
/// dummy operation, as the factu.fisca Spring Boot services do.
/// UnknownOperation is how the service answers a request no operation takes
/// (the manual's namespace, mostly): its fault code, status, detail or text
/// ({expected} names the operation with that name in the WSDL's namespace);
/// Text "" answers the status with an empty body, as Spring-WS's 404.
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
    public string? Header { get; init; }

    public bool HeaderOnFaults { get; init; }

    public string[]? Always { get; init; }

    public string[]? Drop { get; init; }

    public bool Mtom { get; init; }

    public bool DummyOnGet { get; init; }

    public AuthRow? UnknownOperation { get; init; }

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
