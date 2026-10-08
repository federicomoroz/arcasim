using System.Security.Cryptography.X509Certificates;
using Arca.Client;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Client;

/// <summary>
/// Arca.Client wired to a <see cref="StubArca"/>, a clock the test moves and (when asked) a directory for
/// the ticket cache: everything the client touches, in memory or in a folder that goes away with the rig.
/// </summary>
internal sealed class ClientRig : IDisposable
{
    public const long Cuit = 20111111112;

    private static readonly Lazy<X509Certificate2> SharedCertificate = new(() => Certificates.SelfSigned($"SERIALNUMBER=CUIT {Cuit}, CN=client-tests"));

    public ClientRig(bool withCache = false, Uri? wsaaUrl = null, TimeSpan? renewalMargin = null)
    {
        Time = new FakeTime(TestTime.Reference);
        Arca = new StubArca(Time);
        Http = new HttpClient(Arca);
        CacheDirectory = withCache ? Path.Combine(Path.GetTempPath(), "arca-client-tests", Guid.NewGuid().ToString("N")) : null;
        Options = new ArcaOptions
        {
            WsaaUrl = wsaaUrl ?? StubArca.WsaaUrl,
            WsfeUrl = StubArca.WsfeUrl,
            Certificate = Certificate,
            Cuit = Cuit,
            TicketCacheDirectory = CacheDirectory,
            TicketRenewalMargin = renewalMargin ?? TimeSpan.FromMinutes(10),
        };
        Wsaa = NewWsaa();
        Wsfe = new WsfeClient(Http, Wsaa, Options);
    }

    /// <summary>One certificate for every test: generating a key costs more than the tests that sign with it.</summary>
    public static X509Certificate2 Certificate => SharedCertificate.Value;

    public FakeTime Time { get; }

    public StubArca Arca { get; }

    public HttpClient Http { get; }

    public ArcaOptions Options { get; }

    /// <summary>The folder the ticket cache uses; null when the rig has none.</summary>
    public string? CacheDirectory { get; }

    public WsaaClient Wsaa { get; }

    public WsfeClient Wsfe { get; }

    /// <summary>
    /// Another client with the same clock, server and folder, as a process that starts later would have:
    /// nothing in memory, whatever is on disk. Given an address, it logs in at that one instead.
    /// </summary>
    public WsaaClient NewWsaa(Uri? wsaaUrl = null) =>
        new(Http, wsaaUrl is null ? Options : new ArcaOptions
        {
            WsaaUrl = wsaaUrl,
            WsfeUrl = Options.WsfeUrl,
            Certificate = Options.Certificate,
            Cuit = Options.Cuit,
            TicketCacheDirectory = Options.TicketCacheDirectory,
            TicketRenewalMargin = Options.TicketRenewalMargin,
        }, Time);

    /// <summary>
    /// Where the cache keeps a service's ticket for a WSAA address: the certificate, the address as
    /// <paramref name="endpoint"/> (a host, or a host and a port written for a file name) and the service.
    /// </summary>
    public string CacheFile(string service, string endpoint) =>
        Path.Combine(CacheDirectory!, $"ta-{Certificate.Thumbprint}-{endpoint}-{service}.json");

    public void Dispose()
    {
        Http.Dispose();
        Wsaa.Dispose();
        if (CacheDirectory is null) return;
        if (Directory.Exists(CacheDirectory)) Directory.Delete(CacheDirectory, recursive: true);
        else if (File.Exists(CacheDirectory)) File.Delete(CacheDirectory);
    }
}
