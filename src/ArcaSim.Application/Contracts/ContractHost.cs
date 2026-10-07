using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Access;
using ArcaSim.Application.Events;
using ArcaSim.Application.Soap;
using Microsoft.Extensions.Logging;

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
    public XElement Sample() => ContractHost.Apply(sampler.Sample(Operation.Output, Context), Definition, Context);

    public XElement Sample(XName element) => ContractHost.Apply(sampler.Sample(element, Context), Definition, Context);

    public ContractAnswer Ok(XElement body) => new(body, Headers(), null);

    /// <summary>
    /// The value the catalog says this service always sends in that element (by
    /// name or "Parent/Child" path), its placeholders filled for this call; null
    /// when it sets none. Answers the rules build by hand put it where Sample()
    /// would have, so the service sends the same fixed values whatever builds
    /// the answer: the event a server attaches to everything, its address.
    /// </summary>
    public string? Fixed(string name) =>
        Definition.Values?.GetValueOrDefault(name) is { } value
            ? Placeholders.Fill(value, new PlaceholderValues(Context.Now) { Service = Definition.Id, Cuit = Context.Cuit, Counters = Context.Counters })
            : null;

    /// <summary>A business error the way the service reports it: in its error block when the response has one, else as a fault.</summary>
    public ContractAnswer Error(long code, string message) => Errors([(code, message)]);

    /// <summary>Several business errors in one answer, each in its own row of the error block; a fault with the first when there is no block.</summary>
    public ContractAnswer Errors(IReadOnlyList<(long Code, string Message)> errors) =>
        sampler.ErrorResponse(Operation.Output, Context, errors, Definition.Errors.Shape) is { } body
            ? new(ContractHost.Apply(body, Definition, Context), Headers(), null)
            : Fault(errors.Count > 0 ? errors[0].Message : "");

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
    ServiceCatalog catalog, string wsdlFolder, TicketReader tickets, IClock clock, EventManager events, IEnumerable<IServiceBehavior> behaviors,
    PlaceholderCounters? counters = null, ILogger<ContractHost>? logger = null)
{
    private readonly PlaceholderCounters _counters = counters ?? new();
    private readonly ConcurrentDictionary<string, (ServiceContract Contract, SchemaSampler Sampler)> _contracts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IServiceBehavior> _behaviors = RulesByService(behaviors);

    public ServiceCatalog Catalog => catalog;

    /// <summary>Where a fault's detail is written as it comes, not as an element: its Value goes inside detail untouched.</summary>
    public static readonly XName DetailFragment = XName.Get("fragment", "urn:arcasim:detail");

    public ServiceContract ContractOf(ServiceDefinition definition) => Load(definition).Contract;

    /// <summary>The sequences {seq:start} draws from, for what the endpoint fills on its own (an unknown operation's text).</summary>
    public PlaceholderCounters Counters => _counters;

    /// <summary>ArcaSim's clock, for what the endpoint writes on its own (the balancer's mask).</summary>
    public DateTimeOffset Now => clock.Now;

    /// <summary>Any element of the service's schema with data and the service's fixed values, as an answer outside SOAP needs it (the HTTP GET dummy).</summary>
    public XElement SampleOf(ServiceDefinition definition, XName element)
    {
        var context = new SampleContext(0, clock.Now) { Always = definition.Always, Counters = _counters };
        return Apply(Load(definition).Sampler.Sample(element, context), definition, context);
    }

    /// <summary>The header the service sends that its WSDL does not declare, with this moment's values; null when it sends none.</summary>
    public string? HeaderOf(ServiceDefinition definition) =>
        definition.Header is { } header ? Placeholders.Fill(header, new PlaceholderValues(clock.Now) { Service = definition.Id, Counters = _counters }) : null;

    public bool HasRules(ServiceDefinition definition) => _behaviors.ContainsKey(definition.Id);

    /// <summary>One rules class per service: a second class for the same service stops ArcaSim from starting instead of hiding behind the first.</summary>
    private static Dictionary<string, IServiceBehavior> RulesByService(IEnumerable<IServiceBehavior> behaviors)
    {
        var byService = new Dictionary<string, IServiceBehavior>(StringComparer.OrdinalIgnoreCase);
        foreach (var behavior in behaviors)
            if (!byService.TryAdd(behavior.Service, behavior))
                throw new InvalidOperationException($"{behavior.GetType().Name} and {byService[behavior.Service].GetType().Name} both answer {behavior.Service}.");
        return byService;
    }

    private (ServiceContract Contract, SchemaSampler Sampler) Load(ServiceDefinition definition) =>
        _contracts.GetOrAdd(definition.Id, _ =>
        {
            var contract = ServiceContract.Load(Path.Combine(wsdlFolder, definition.Wsdl));
            return (contract, new SchemaSampler(contract.Schemas));
        });

    public async Task<ContractAnswer> AnswerAsync(ServiceDefinition definition, OperationContract operation, XElement? request, CancellationToken ct)
    {
        var (contract, sampler) = Load(definition);
        var context = new SampleContext(0, clock.Now) { Always = definition.Always, Counters = _counters };
        long cuit = 0;

        if (operation.Input is not null && sampler.CarriesTicket(operation.Input))
        {
            var auth = AuthOf(request, definition.Errors);
            cuit = auth.Cuit;
            var check = tickets.Check(auth.Token, auth.Sign, cuit, definition.Wsaa);
            if (check.Failed)
            {
                var rows = RowsFor(definition, check, auth, context);
                var refusal = Refuse(definition, contract, sampler, operation, rows, context with { Cuit = cuit });
                events.Publish(new ServiceCalled(DateTimeOffset.UtcNow, definition.Id, operation.Name, cuit, "error", rows[0].Text));
                return refusal;
            }
            context = context with { Cuit = cuit };
        }

        var call = new ServiceCall(definition, contract, sampler, operation, request, cuit, context);
        ContractAnswer? answer = null;
        try
        {
            if (_behaviors.TryGetValue(definition.Id, out var behavior)) answer = await behavior.AnswerAsync(call, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A request the rules did not foresee is a server fault, as on ARCA's servers, never a bare HTTP 500.
            logger?.LogError(ex, "{Service}.{Operation} failed", definition.Id, operation.Name);
            answer = call.Fault(Unexpected(definition.Dialect, ex), "soap:Server");
        }
        answer ??= call.Ok(call.Sample());

        events.Publish(new ServiceCalled(DateTimeOffset.UtcNow, definition.Id, operation.Name, cuit,
            answer.Fault is null ? "ok" : "error", answer.Fault?.Message ?? ""));
        return answer;
    }

    /// <summary>
    /// The fixed values the service always sends (FEHeaderInfo's ambiente and
    /// version, a server name), with the placeholders filled for this answer:
    /// values go by element name or by "Parent/Child" path, the path winning;
    /// the elements in Drop are taken out.
    /// </summary>
    public static XElement Apply(XElement answer, ServiceDefinition definition, SampleContext context)
    {
        if (definition.Drop is { Length: > 0 } drop)
            foreach (var element in answer.Descendants().Where(e => drop.Any(d => Matches(e, d))).ToList())
                element.Remove();
        if (definition.Values is not { Count: > 0 } values) return answer;
        var fill = new PlaceholderValues(context.Now) { Service = definition.Id, Cuit = context.Cuit, Counters = context.Counters };
        foreach (var element in answer.DescendantsAndSelf().Where(e => !e.HasElements))
        {
            var path = element.Parent is { } parent ? $"{parent.Name.LocalName}/{element.Name.LocalName}" : null;
            var key = path is not null && values.ContainsKey(path) ? path : element.Name.LocalName;
            if (values.TryGetValue(key, out var value) && value is not null)
                element.Value = Placeholders.Fill(value, fill);
        }
        return answer;
    }

    private static bool Matches(XElement element, string path)
    {
        var slash = path.IndexOf('/');
        return slash < 0
            ? element.Name.LocalName == path
            : element.Name.LocalName == path[(slash + 1)..] && element.Parent?.Name.LocalName == path[..slash];
    }

    // ---- Tickets ---------------------------------------------------------------------

    /// <summary>
    /// The ticket as the request carries it: the token, the sign, the CUIT it
    /// acts for, whether both elements came at all, and the request's root
    /// element as the client wrote it (prefix included).
    /// </summary>
    private sealed record Auth(string? Token, string? Sign, long Cuit, bool HasTicket, string Element);

    /// <summary>
    /// The token, the sign and the represented CUIT wherever the service puts
    /// them: in Auth or authRequest or auth, or loose in the request, as the
    /// padrón does. The CUIT is the one next to the token, or the ticket's own
    /// when the request carries none.
    /// </summary>
    private static Auth AuthOf(XElement? request, AuthErrors errors)
    {
        if (request is null) return new(null, null, 0, false, "");
        var prefix = request.GetPrefixOfNamespace(request.Name.Namespace);
        var element = string.IsNullOrEmpty(prefix) ? request.Name.LocalName : $"{prefix}:{request.Name.LocalName}";
        var token = request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("token", StringComparison.OrdinalIgnoreCase));
        var scope = token?.Parent ?? request;
        var sign = scope.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("sign", StringComparison.OrdinalIgnoreCase)
                                                        || e.Name.LocalName.Equals("firma", StringComparison.OrdinalIgnoreCase));
        var hasTicket = token is not null && sign is not null;
        if (errors.CuitFromTicket) return new(token?.Value, sign?.Value, TicketCuit(token?.Value), hasTicket, element);
        var near = scope.Elements().Where(e => e.Name.LocalName.Contains("cuit", StringComparison.OrdinalIgnoreCase)).ToList();
        var cuit = errors.CuitField is { } cuitField
            ? request.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(cuitField, StringComparison.OrdinalIgnoreCase))
            : near.FirstOrDefault(e => e.Name.LocalName.Contains("represent", StringComparison.OrdinalIgnoreCase)) ?? near.FirstOrDefault()
              ?? request.Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("cuitRepresentad", StringComparison.OrdinalIgnoreCase));
        long.TryParse(cuit?.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number);
        return new(token?.Value, sign?.Value, number, hasTicket, element);
    }

    /// <summary>The first CUIT among the ticket's relations, for services whose request names none; 0 when the token does not read.</summary>
    private static long TicketCuit(string? token)
    {
        try
        {
            var login = SafeXml.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token ?? "")));
            var key = login.Descendants("relation").Select(r => r.Attribute("key")?.Value).FirstOrDefault();
            return long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var cuit) ? cuit : 0;
        }
        catch (Exception ex) when (ex is FormatException or System.Xml.XmlException)
        {
            return 0;
        }
    }

    /// <summary>One error of a refusal, resolved: its code, its text and how it travels.</summary>
    private sealed record Refusal(long Code, string Text, string? Detail, bool Fault, string? FaultCode, int? Status);

    /// <summary>
    /// What the service answers for this problem: its catalog rows ("NoTicket"
    /// when the elements did not come, then the problem, then "*"), each
    /// completed with the service's code and text; or one error with them.
    /// </summary>
    private static List<Refusal> RowsFor(ServiceDefinition definition, TicketCheck check, Auth auth, SampleContext context)
    {
        var errors = definition.Errors;
        var configured = (auth.HasTicket ? null : Row(errors, "NoTicket")) ?? Row(errors, check.Problem.ToString()) ?? Row(errors, "*");
        var values = new PlaceholderValues(check.Now != 0 ? DateTimeOffset.FromUnixTimeSeconds(check.Now) : context.Now)
        {
            Counters = context.Counters,
            Service = definition.Id,
            Cuit = check.Cuit,
            Detail = check.Detail,
            TokenService = check.TokenService,
            GenerationTime = check.GenerationTime,
            ExpirationTime = check.ExpirationTime,
            Token = auth.Token,
            Sign = auth.Sign,
            Element = auth.Element,
        };
        var fallback = Placeholders.Fill(errors.Texts?.GetValueOrDefault(check.Problem) ?? DefaultTexts(definition.Dialect, definition.Wsaa[0])[check.Problem], values);
        return (configured is { Length: > 0 } rows ? rows : [new AuthRow()])
            .Select(row => new Refusal(
                row.Code != 0 ? row.Code : errors.CodeFor(check.Problem),
                row.Text is { } text ? Placeholders.Fill(text, values) : (errors.InBody && !row.Fault ? errors.Prefix : "") + fallback,
                row.Detail is { } detail ? Placeholders.Fill(detail, values) : null,
                row.Fault,
                row.FaultCode,
                row.Status))
            .ToList();
    }

    private static AuthRow[]? Row(AuthErrors errors, string key) =>
        errors.Rows?.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static ContractAnswer Refuse(
        ServiceDefinition definition, ServiceContract contract, SchemaSampler sampler, OperationContract operation, List<Refusal> rows, SampleContext context)
    {
        var errors = definition.Errors;
        var first = rows[0];
        if (errors.InBody && !first.Fault
            && sampler.ErrorResponse(operation.Output, context, rows.Select(r => (r.Code, r.Text)).ToList(), errors.Shape) is { } body)
            return new(Apply(body, definition, context), operation.OutputHeaders.Select(h => Apply(sampler.Sample(h, context), definition, context)).ToList(), null);
        var detail = first.Detail switch
        {
            null => errors.Detail is { } name ? new XElement(XName.Get(name, contract.TargetNamespace)) : null,
            "" => null,
            var fragment => new XElement(DetailFragment, fragment),
        };
        return ContractAnswer.Failed(new SoapFault(first.FaultCode ?? errors.FaultCode, first.Text, detail, first.Status ?? errors.Status));
    }

    /// <summary>
    /// How each family words a refused ticket when the service's own texts are
    /// not known. ASMX: wsfev1's, observed in homologación. CXF: the padrón's,
    /// observed. JAX-WS: "[wscommon_007]" is the text the manuals print
    /// (wsfecred, wslpg, wstabaco...) and "[wscommon_002]" the expired token a
    /// 2021 wsct capture shows; the others are ArcaSim's, without a code, since
    /// none is documented. Axis2: wsmtxca's, observed in producción; the wrong
    /// service and the CUIT outside the relations are ArcaSim's.
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
        Dialect.Axis2 => new Dictionary<TicketProblem, string>
        {
            [TicketProblem.MissingToken] = "Token inválido",
            [TicketProblem.MissingSign] = "Token inválido",
            [TicketProblem.Unreadable] = "Token inválido",
            [TicketProblem.OutOfDate] = ExpiredToken,
            [TicketProblem.BadSignature] = "La firma no corresponde al token enviado.",
            [TicketProblem.WrongService] = $"Acceso Denegado  - El token no corresponde al servicio {service}.",
            [TicketProblem.CuitNotRelated] = "Acceso Denegado  - La CUIT {cuit} no se encuentra entre los representados del token.",
        },
        _ => new Dictionary<TicketProblem, string>
        {
            [TicketProblem.MissingToken] = "El token no fue informado.",
            [TicketProblem.MissingSign] = "La firma no fue informada.",
            [TicketProblem.Unreadable] = "El token no tiene un formato valido.",
            [TicketProblem.OutOfDate] = "[wscommon_002] " + ExpiredToken,
            [TicketProblem.BadSignature] = "[wscommon_007] La firma no corresponde al token enviado.",
            [TicketProblem.WrongService] = $"El token no corresponde al servicio {service}.",
            [TicketProblem.CuitNotRelated] = "La CUIT {cuit} no se encuentra entre los representados del token.",
        },
    };

    /// <summary>
    /// The fault text a server of each family gives for an error nobody
    /// handled: ASMX wraps the exception the way WSFEv1's own faults read,
    /// CXF says its generic sentence, and the Java stacks pass the message on.
    /// These are the frameworks' defaults, not texts captured from ARCA.
    /// </summary>
    public static string Unexpected(Dialect dialect, Exception ex) => dialect switch
    {
        Dialect.Asmx => $"System.Web.Services.Protocols.SoapException: Server was unable to process request. ---> {ex.GetType().FullName}: {ex.Message}",
        Dialect.Cxf => "Fault occurred while processing.",
        _ => ex.Message,
    };

    /// <summary>The expired token as the Java services print it (wsmtxca live, wsct 2021, the wscta manual).</summary>
    private const string ExpiredToken =
        "Token vencido Fecha y Hora de Vencimiento del Token Enviado: {exp:dd-MM-yyyy HH:mm:ss} - Fecha y Hora Actual del Servidor: {now:dd-MM-yyyy HH:mm:ss}";
}
