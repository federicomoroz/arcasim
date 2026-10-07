using System.Text.Json.Serialization;
using ArcaSim.Api.Admin;
using ArcaSim.Api.Rest;
using ArcaSim.Api.Soap;
using ArcaSim.Application;
using ArcaSim.Application.Access;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Padron;
using ArcaSim.Application.Services.Fce;
using ArcaSim.Application.Setiws;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsaa;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.InMemory;
using ArcaSim.Infrastructure.Postgres;
using ArcaSim.Infrastructure.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.GetSection("ArcaSim");

builder.Services
    .AddControllers()
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SimulatedClock>();
builder.Services.AddSingleton<IClock>(sp => sp.GetRequiredService<SimulatedClock>());
builder.Services.AddSingleton(_ => new SimulationSettings
{
    Environment = options.GetValue("Environment", ArcaEnvironment.Homologacion),
    ReplayWindowEnabled = options.GetValue("ReplayWindowEnabled", true),
    OpenAccess = !string.Equals(options["Access"], "Strict", StringComparison.OrdinalIgnoreCase),
});

var dataDirectory = options["DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "data");
builder.Services.AddSingleton(_ => new KeyMaterial(Path.Combine(dataDirectory, "keys")));
builder.Services.AddSingleton<ITrustStore>(sp => sp.GetRequiredService<KeyMaterial>());
builder.Services.AddSingleton<ITokenSigner>(sp => sp.GetRequiredService<KeyMaterial>());

// Memory for an application's tests (starts in milliseconds, gone with the process);
// Postgres for an ArcaSim several developers or a CI share.
if (string.Equals(options["Storage"], "Postgres", StringComparison.OrdinalIgnoreCase))
{
    var connection = builder.Configuration.GetConnectionString("ArcaSim")
        ?? throw new InvalidOperationException("ArcaSim:Storage is Postgres but ConnectionStrings:ArcaSim is missing.");
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
    builder.Services.AddSingleton<PostgresStore>();
    AddStore<PostgresStore>(builder.Services);
}
else
{
    builder.Services.AddSingleton<InMemoryStore>();
    AddStore<InMemoryStore>(builder.Services);
}

builder.Services.AddSingleton(_ => ValidationCatalog.Load());
builder.Services.AddSingleton(_ => ParameterTables.Load());
builder.Services.AddSingleton<IAuthorizationCodes, RandomAuthorizationCodes>();
builder.Services.AddSingleton<SequenceLocks>();
builder.Services.AddSingleton<EventManager>();
builder.Services.AddSingleton<TrafficGate>();
builder.Services.AddSingleton<TrafficMeter>();
builder.Services.AddSingleton<ActivityLog>();
builder.Services.AddSingleton<TicketReader>();
builder.Services.AddSingleton<TokenValidator>();
builder.Services.AddSingleton<VoucherValidator>();
builder.Services.AddSingleton<WsfeService>();
builder.Services.AddSingleton<PadronDirectory>();
builder.Services.AddSingleton<PadronService>();
builder.Services.AddSingleton<WsaaService>();
builder.Services.AddSingleton<WsfeEndpoint>();
builder.Services.AddSingleton<WsaaEndpoint>();
builder.Services.AddSingleton<SetiwsGateway>();
builder.Services.AddSingleton<VepService>();
builder.Services.AddSingleton<FceLedger>();

// The rest of ARCA's services, answered from their WSDL; IServiceBehavior adds a service's rules on top.
// Every rule set in the Application assembly is picked up: adding a service's rules is adding a class.
foreach (var behavior in typeof(ContractHost).Assembly.GetTypes()
             .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IServiceBehavior).IsAssignableFrom(t)))
    builder.Services.AddSingleton(typeof(IServiceBehavior), behavior);
builder.Services.AddSingleton(_ => ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json")));
builder.Services.AddSingleton(sp => new ContractHost(
    sp.GetRequiredService<ServiceCatalog>(), WsdlDocuments.Directory, sp.GetRequiredService<TicketReader>(),
    sp.GetRequiredService<IClock>(), sp.GetRequiredService<EventManager>(), sp.GetServices<IServiceBehavior>(),
    sp.GetRequiredService<ILogger<ContractHost>>()));

var app = builder.Build();

if (app.Services.GetService<PostgresStore>() is { } postgres) await postgres.EnsureSchemaAsync();

// The listeners subscribe when they are built: build them before the first request.
app.Services.GetRequiredService<TrafficMeter>();
app.Services.GetRequiredService<ActivityLog>();

TrafficMiddleware.Use(app);
app.UseDefaultFiles();
app.UseStaticFiles();
WsaaEndpoint.Map(app);
WsfeEndpoint.Map(app);
PadronModule.Map(app);
SetiwsEndpoint.Map(app);
ContractEndpoint.Map(app, app.Services.GetRequiredService<ContractHost>());
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/arcasim/"));

app.Run();

static void AddStore<TStore>(IServiceCollection services)
    where TStore : class, ISimulatorStore
{
    services.AddSingleton<IAccessRepository>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<ITicketLog>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<ITaxpayerRepository>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<IVoucherStore>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<ICaeaStore>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<IExchangeRates>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<IResettable>(sp => sp.GetRequiredService<TStore>());
    services.AddSingleton<IDocumentStore>(sp => sp.GetRequiredService<TStore>());
}

/// <summary>Entry point, visible to the tests' WebApplicationFactory.</summary>
public partial class Program;
