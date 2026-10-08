using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Arca.Client;
using ArcaSim.Application;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using ArcaSim.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArcaSim.Tests;

/// <summary>
/// ArcaSim in memory, one per test, plus what an application would set up
/// against it: a taxpayer, a certificate and the Arca.Client module.
/// </summary>
public sealed class ArcaSimHarness : IAsyncDisposable
{
    /// <summary>A CUIT with a right check digit, registered as Responsable Inscripto with point of sale 1 (CAE) and 900 (CAEA).</summary>
    public const long Issuer = 20111111112;

    private static readonly string KeysDirectory = Path.Combine(Path.GetTempPath(), "arcasim-tests");

    static ArcaSimHarness()
    {
        // Created once, before any test runs in parallel, so no two hosts race to write the keys.
        _ = new KeyMaterial(Path.Combine(KeysDirectory, "keys"));
    }

    private readonly WebApplicationFactory<Program> _factory;
    private readonly CapturedLogs _logs;
    private bool _expectsLoggedErrors;

    private ArcaSimHarness(WebApplicationFactory<Program> factory, CapturedLogs logs)
    {
        _factory = factory;
        _logs = logs;
        Http = factory.CreateClient();
    }

    public HttpClient Http { get; }

    /// <summary>What ArcaSim has logged at Error or above so far: an unforeseen failure of a service's rules shows up here.</summary>
    public IReadOnlyList<LoggedError> LoggedErrors => _logs.Errors;

    /// <summary>
    /// Fails when ArcaSim logged an error. The engine answers a request its rules did not foresee with the
    /// service's fault, which looks like any refusal; the log is what tells a crash from a refusal.
    /// Disposing the harness does the same, unless the test called <see cref="ExpectLoggedErrors"/>.
    /// </summary>
    public void AssertNoLoggedErrors() => AssertNone(LoggedErrors);

    /// <summary>Declares that the test makes the engine fail on purpose, so disposing the harness does not fail on what it logs.</summary>
    public void ExpectLoggedErrors() => _expectsLoggedErrors = true;

    private static void AssertNone(IReadOnlyList<LoggedError> errors) =>
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => $"{e.Category}: {e.Message}{e.Exception}")));

    public IServiceProvider Services => _factory.Services;

    public SimulatedClock Clock => Services.GetRequiredService<SimulatedClock>();

    public SimulationSettings Settings => Services.GetRequiredService<SimulationSettings>();

    /// <summary>A handler that sends straight to the in-memory server, for clients that build their own HttpClient.</summary>
    public HttpMessageHandler CreateHandler() => _factory.Server.CreateHandler();

    public static Uri WsaaUrl => new("http://localhost/ws/services/LoginCms");

    public static Uri WsfeUrl => new("http://localhost/wsfev1/service.asmx");

    /// <param name="postgres">A connection string to run on PostgreSQL instead of memory.</param>
    /// <param name="open">Open access, as ArcaSim starts by default. The suite runs strict unless a test asks.</param>
    public static ArcaSimHarness Start(string? postgres = null, bool open = false)
    {
        var logs = new CapturedLogs();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ArcaSim:DataDirectory", KeysDirectory);
            host.UseSetting("ArcaSim:Access", open ? "Open" : "Strict");
            host.UseSetting("ArcaSim:ReplayWindowEnabled", "false");
            host.ConfigureLogging(logging => logging.AddProvider(logs));
            if (postgres is null) return;
            host.UseSetting("ArcaSim:Storage", "Postgres");
            host.UseSetting("ConnectionStrings:ArcaSim", postgres);
        });
        return new ArcaSimHarness(factory, logs);
    }

    /// <summary>The usual starting point: ArcaSim with the issuer registered, frozen on a weekday before 01/12/2026.</summary>
    public static async Task<(ArcaSimHarness Sim, WsfeClient Wsfe)> StartWithIssuerAsync(
        VatCondition condition = VatCondition.ResponsableInscripto, DateTimeOffset? now = null, string? postgres = null)
    {
        var sim = Start(postgres);
        sim.Clock.Freeze(now ?? TestTime.Reference);
        await sim.PutTaxpayerAsync(Issuer, "Empresa de Prueba SA", condition,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(900, PointOfSaleKind.WebServiceCaea));
        var certificate = await sim.IssueCertificateAsync(Issuer, "facturacion");
        return (sim, sim.Wsfe(Issuer, certificate));
    }

    public async Task PutTaxpayerAsync(long cuit, string name, VatCondition condition, params PointOfSale[] points)
    {
        var response = await Http.PutAsJsonAsync($"/arcasim/api/taxpayers/{cuit}", new
        {
            name,
            vatCondition = condition.ToString(),
            active = true,
            pointsOfSale = points.Select(p => new { number = p.Number, kind = p.Kind.ToString(), blocked = p.Blocked }),
        });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>A certificate with its key, from the admin API, authorized for the services given (wsfe by default).</summary>
    public async Task<X509Certificate2> IssueCertificateAsync(long cuit, string alias, params string[] services)
    {
        var response = await Http.PostAsJsonAsync("/arcasim/api/certificates", new
        {
            cuit,
            alias,
            password = "test",
            services = services.Length == 0 ? null : services,
        });
        response.EnsureSuccessStatusCode();
        return new X509Certificate2(await response.Content.ReadAsByteArrayAsync(), "test", X509KeyStorageFlags.EphemeralKeySet);
    }

    public ArcaOptions Options(long cuit, X509Certificate2 certificate) => new()
    {
        WsaaUrl = WsaaUrl,
        WsfeUrl = WsfeUrl,
        Certificate = certificate,
        Cuit = cuit,
    };

    public WsaaClient Wsaa(long cuit, X509Certificate2 certificate) => new(Http, Options(cuit, certificate), new ClockTime(Clock));

    public WsfeClient Wsfe(long cuit, X509Certificate2 certificate)
    {
        var options = Options(cuit, certificate);
        return new WsfeClient(Http, new WsaaClient(Http, options, new ClockTime(Clock)), options);
    }

    /// <summary>A raw SOAP 1.1 call, for the tests that look at the exact bytes.</summary>
    public async Task<(int Status, string Body)> PostSoapAsync(Uri url, string envelope, string? soapAction, CancellationToken ct = default)
    {
        using var content = new StringContent(envelope, Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (soapAction is not null) request.Headers.Add("SOAPAction", soapAction);
        using var response = await Http.SendAsync(request, ct);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>A WSFEv1 envelope around an operation's element, written the way the manual's examples are.</summary>
    public Task<(int Status, string Body)> PostWsfeAsync(string operation, string inner, bool withAction = true) =>
        PostSoapAsync(WsfeUrl,
            Soap.Envelope($"<ar:{operation}>{inner}</ar:{operation}>", ("ar", "http://ar.gov.afip.dif.FEV1/")),
            withAction ? $"\"http://ar.gov.afip.dif.FEV1/{operation}\"" : null);

    public static string AuthXml(AccessTicket ticket, long cuit) =>
        $"<ar:Auth><ar:Token>{ticket.Token}</ar:Token><ar:Sign>{ticket.Sign}</ar:Sign><ar:Cuit>{cuit}</ar:Cuit></ar:Auth>";

    /// <summary>
    /// Shuts ArcaSim down and fails the test if it logged an error on the way, unless the test said it expects
    /// them: a rule that crashes is answered with the service's fault, and a test that only looks at faults
    /// would pass over it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var unforeseen = _expectsLoggedErrors ? [] : LoggedErrors;
        Http.Dispose();
        await _factory.DisposeAsync();
        AssertNone(unforeseen);
    }

    /// <summary>Lets the client sign its TRA with ArcaSim's clock, so frozen or advanced time stays consistent on both ends.</summary>
    private sealed class ClockTime(IClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.Now.ToUniversalTime();
    }
}
