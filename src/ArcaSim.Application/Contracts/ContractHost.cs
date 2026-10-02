using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Access;
using ArcaSim.Application.Events;
using ArcaSim.Application.Soap;

namespace ArcaSim.Application.Contracts;

/// <summary>What one operation answers: the Body's element and the SOAP headers that go with it, or a fault.</summary>
public sealed record ContractAnswer(XElement? Body, IReadOnlyList<XElement> Headers, SoapFault? Fault)
{
    public static ContractAnswer Failed(SoapFault fault) => new(null, [], fault);
}

/// <summary>
/// The rules of one service, on top of its contract: what changes state and
/// what is checked. It answers the operations it knows and returns null for
/// the rest, which then get the contract's answer.
/// </summary>
public interface IServiceBehavior
{
    string Service { get; }

    Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct);
}

/// <summary>One request to one operation, already authenticated, with the means to answer it in the service's own shapes.</summary>
public sealed class ServiceCall(
    ServiceDefinition definition, ServiceContract contract, SchemaSampler sampler, OperationContract operation, XElement? request, long cuit, SampleContext context)
{
    public ServiceDefinition Definition { get; } = definition;
    public ServiceContract Contract { get; } = contract;
    public OperationContract Operation { get; } = operation;
    public string Name => Operation.Name;

    /// <summary>The Body's element, or an empty one named after the operation when the Body came empty.</summary>
    public XElement Request { get; } = request ?? new XElement(operation.Name);

    /// <summary>The CUIT the ticket acts for (cuitRepresentada, cuit, Auth/Cuit), 0 for operations without a ticket.</summary>
    public long Cuit { get; } = cuit;

    public SampleContext Context { get; } = context;

    /// <summary>The contract's answer with data, values the service always sends already in place, ready to be adjusted.</summary>
    public XElement Sample() => ContractHost.Apply(sampler.Sample(Operation.Output, Context), Definition);

    public XElement Sample(XName element) => ContractHost.Apply(sampler.Sample(element, Context), Definition);

    public ContractAnswer Ok(XElement body) => new(body, Headers(), null);

    /// <summary>A business error the way the service reports it: in its error block when the response has one, else as a fault.</summary>
    public ContractAnswer Error(long code, string message) =>
        sampler.ErrorResponse(Operation.Output, Context, code, message, Definition.Errors.Shape) is { } body
            ? new(ContractHost.Apply(body, Definition), Headers(), null)
            : Fault($"{message}");

    public ContractAnswer Fault(string message, string? code = null) =>
        ContractAnswer.Failed(new SoapFault(code ?? Definition.Errors.FaultCode, message,
            Definition.Errors.Detail is { } detail ? new XElement(XName.Get(detail, Contract.TargetNamespace)) : null, Definition.Errors.Status));

    private List<XElement> Headers() => Operation.OutputHeaders.Select(Sample).ToList();
}

/// <summary>
/// Answers every service of the catalog from its WSDL: checks the ticket the
/// way the service's dialect words it, hands the call to the service's rules
/// when it has them, and otherwise answers the contract with schema-valid data.
/// </summary>
public sealed class ContractHost(
    ServiceCatalog catalog, string wsdlFolder, TicketReader tickets, IClock clock, EventManager events, IEnumerable<IServiceBehavior> behaviors)
{
    private readonly ConcurrentDictionary<string, (ServiceContract Contract, SchemaSampler Sampler)> _contracts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILookup<string, IServiceBehavior> _behaviors = behaviors.ToLookup(b => b.Service, StringComparer.OrdinalIgnoreCase);

    public ServiceCatalog Catalog => catalog;

    public ServiceContract ContractOf(ServiceDefinition definition) => Load(definition).Contract;

    public bool HasRules(ServiceDefinition definition) => _behaviors.Contains(definition.Id);

    private (ServiceContract Contract, SchemaSampler Sampler) Load(ServiceDefinition definition) =>
        _contracts.GetOrAdd(definition.Id, _ =>
        {
            var contract = ServiceContract.Load(Path.Combine(wsdlFolder, definition.Wsdl));
            return (contract, new SchemaSampler(contract.Schemas));
        });

    public async Task<ContractAnswer> AnswerAsync(ServiceDefinition definition, OperationContract operation, XElement? request, CancellationToken ct)
    {
        var (contract, sampler) = Load(definition);
        var context = new SampleContext(0, clock.Now);
        long cuit = 0;

        if (operation.Input is not null && sampler.CarriesTicket(operation.Input))
        {
            var auth = AuthOf(request, definition.Errors.CuitField);
            cuit = auth.Cuit;
            var check = tickets.Check(auth.Token, auth.Sign, cuit, definition.Wsaa);
            if (check.Failed)
            {
                var refusal = Refuse(definition, contract, sampler, operation, check, context);
                events.Publish(new ServiceCalled(DateTimeOffset.UtcNow, definition.Id, operation.Name, cuit, "error", TextOf(definition, check)));
                return refusal;
            }
            context = context with { Cuit = cuit };
        }

        var call = new ServiceCall(definition, contract, sampler, operation, request, cuit, context);
        ContractAnswer? answer = null;
        foreach (var behavior in _behaviors[definition.Id])
            if ((answer = await behavior.AnswerAsync(call, ct)) is not null) break;
        answer ??= call.Ok(call.Sample());

        events.Publish(new ServiceCalled(DateTimeOffset.UtcNow, definition.Id, operation.Name, cuit,
            answer.Fault is null ? "ok" : "error", answer.Fault?.Message ?? ""));
        return answer;
    }

    /// <summary>Fixed values the service always sends, by element name: FEHeaderInfo's ambiente and version, a server name.</summary>
    public static XElement Apply(XElement answer, ServiceDefinition definition)
    {
        if (definition.Values is null) return answer;
        foreach (var element in answer.DescendantsAndSelf().Where(e => !e.HasElements))
            if (definition.Values.TryGetValue(element.Name.LocalName, out var value))
                element.Value = value;
        return answer;
    }

    // ---- Tickets ---------------------------------------------------------------------

    private sealed record Auth(string? Token, string? Sign, long Cuit);

    /// <summary>
    /// The token, the sign and the represented CUIT wherever the service puts
    /// them: in Auth or authRequest or auth, or loose in the request, as the
    /// padrón does. The CUIT is the one next to the token.
    /// </summary>
    private static Auth AuthOf(XElement? request, string? cuitField)
    {
        if (request is null) return new(null, null, 0);
        var token = request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("token", StringComparison.OrdinalIgnoreCase));
        var scope = token?.Parent ?? request;
        var sign = scope.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("sign", StringComparison.OrdinalIgnoreCase)
                                                        || e.Name.LocalName.Equals("firma", StringComparison.OrdinalIgnoreCase));
        var near = scope.Elements().Where(e => e.Name.LocalName.Contains("cuit", StringComparison.OrdinalIgnoreCase)).ToList();
        var cuit = cuitField is not null
            ? request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(cuitField, StringComparison.OrdinalIgnoreCase))
            : near.FirstOrDefault(e => e.Name.LocalName.Contains("represent", StringComparison.OrdinalIgnoreCase)) ?? near.FirstOrDefault()
              ?? request.Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("cuitRepresentad", StringComparison.OrdinalIgnoreCase));
        long.TryParse(cuit?.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number);
        return new(token?.Value, sign?.Value, number);
    }

    private static ContractAnswer Refuse(
        ServiceDefinition definition, ServiceContract contract, SchemaSampler sampler, OperationContract operation, TicketCheck check, SampleContext context)
    {
        var errors = definition.Errors;
        var text = TextOf(definition, check);
        if (errors.InBody && sampler.ErrorResponse(operation.Output, context, errors.CodeFor(check.Problem), errors.Prefix + text, errors.Shape) is { } body)
            return new(Apply(body, definition), operation.OutputHeaders.Select(h => Apply(sampler.Sample(h, context), definition)).ToList(), null);
        return ContractAnswer.Failed(new SoapFault(errors.FaultCode, text,
            errors.Detail is { } detail ? new XElement(XName.Get(detail, contract.TargetNamespace)) : null, errors.Status));
    }

    private static string TextOf(ServiceDefinition definition, TicketCheck check) =>
        definition.Errors.Texts?.GetValueOrDefault(check.Problem) is { } text
            ? Fill(text, check)
            : Fill(DefaultTexts(definition.Dialect, definition.Wsaa[0])[check.Problem], check);

    private static string Fill(string text, TicketCheck check) => text
        .Replace("{cuit}", check.Cuit.ToString(CultureInfo.InvariantCulture))
        .Replace("{detail}", check.Detail ?? "")
        .Replace("{service}", check.TokenService ?? "")
        .Replace("{gen}", check.GenerationTime.ToString(CultureInfo.InvariantCulture))
        .Replace("{exp}", check.ExpirationTime.ToString(CultureInfo.InvariantCulture))
        .Replace("{now}", check.Now.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// How each family words a refused ticket when the service's own texts are
    /// not known. ASMX: wsfev1's, observed in homologación. CXF: the padrón's,
    /// observed. JAX-WS: "[wscommon_007]" is the one text a manual prints
    /// (wsfecred, wslpg); the others follow its style and are ArcaSim's.
    /// </summary>
    public static IReadOnlyDictionary<TicketProblem, string> DefaultTexts(Dialect dialect, string service) => dialect switch
    {
        Dialect.Asmx => new Dictionary<TicketProblem, string>
        {
            [TicketProblem.MissingToken] = "ValidacionDeToken: Parametro nulo o vacio (token)",
            [TicketProblem.MissingSign] = "ValidacionDeToken: Parametro nulo o vacio (sign)",
            [TicketProblem.Unreadable] = "ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: Error al cargar token XML. Excepcion: {detail}",
            [TicketProblem.OutOfDate] = "ValidacionDeToken: No validaron las fechas del token. GenTime={gen}, ExpTime={exp}, NowUTC={now}",
            [TicketProblem.BadSignature] = "ValidacionDeToken: Error al verificar hash: ",
            [TicketProblem.WrongService] = $"ValidacionDeToken: No valido Id Sistema: {service}(Id Sistema de token es: {{service}})",
            [TicketProblem.CuitNotRelated] = "ValidacionDeToken: No apareció CUIT en lista de relaciones: {cuit}",
        },
        Dialect.Cxf => new Dictionary<TicketProblem, string>
        {
            [TicketProblem.MissingToken] = "Falta token y/o sign.",
            [TicketProblem.MissingSign] = "Falta token y/o sign.",
            [TicketProblem.Unreadable] = "Token malformado",
            [TicketProblem.OutOfDate] = "Token vencido",
            [TicketProblem.BadSignature] = "No se pudo verificar que <sign> contenga una firma valida de <token>",
            [TicketProblem.WrongService] = "No autorizado, par token/sign invalido.",
            [TicketProblem.CuitNotRelated] = "Este token no le permite actuar en representacion de la CUIT {cuit}",
        },
        _ => new Dictionary<TicketProblem, string>
        {
            [TicketProblem.MissingToken] = "[wscommon_001] El token no fue informado.",
            [TicketProblem.MissingSign] = "[wscommon_002] La firma no fue informada.",
            [TicketProblem.Unreadable] = "[wscommon_003] El token no tiene un formato valido.",
            [TicketProblem.OutOfDate] = "[wscommon_004] El token esta vencido.",
            [TicketProblem.BadSignature] = "[wscommon_007] La firma no corresponde al token enviado.",
            [TicketProblem.WrongService] = $"[wscommon_005] El token no corresponde al servicio {service}.",
            [TicketProblem.CuitNotRelated] = "[wscommon_006] La CUIT {cuit} no se encuentra entre los representados del token.",
        },
    };
}
