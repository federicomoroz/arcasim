using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Arca.Client;

/// <summary>An access ticket (TA): what WSAA hands out and every business service asks for.</summary>
public sealed record AccessTicket(string Token, string Sign, DateTimeOffset GeneratedAt, DateTimeOffset ExpiresAt);

/// <summary>
/// WSAA's loginCms: signs a TRA with the certificate, asks for a TA and keeps
/// it until it is about to expire. One instance per certificate; it is safe to
/// share between threads. It owns one semaphore and nothing else, so disposing
/// it is optional; a disposed client refuses to hand out tickets.
/// </summary>
public sealed class WsaaClient(HttpClient http, ArcaOptions options, TimeProvider? time = null) : IDisposable
{
    private const string WsaaNamespace = "http://wsaa.view.sua.dvadac.desein.afip.gov";
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    // One login at a time, whatever the service: ARCA refuses a second TA while the first one lives.
    private readonly SemaphoreSlim _login = new(1, 1);

    // Guards _tickets. It is held for a lookup or an update and never across an await, so a ticket
    // that is still valid is returned without waiting for a login that is in progress.
    private readonly object _memory = new();
    private readonly Dictionary<string, AccessTicket> _tickets = [];
    private bool _disposed;

    /// <summary>A valid ticket for the service: the one in memory, the one on disk, or a new one.</summary>
    public async Task<AccessTicket> GetTicketAsync(string service, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (FreshInMemory(service) is { } known) return known;

        await _login.WaitAsync(ct);
        try
        {
            // Another caller may have logged in while this one waited its turn.
            if (FreshInMemory(service) is { } renewed) return renewed;
            if (ReadCached(service) is { } cached)
            {
                Remember(service, cached);
                return cached;
            }

            var ticket = await LoginAsync(service, ct);
            Remember(service, ticket);
            WriteCached(service, ticket);
            return ticket;
        }
        finally
        {
            _login.Release();
        }
    }

    /// <summary>
    /// Drops the ticket of a service, so the next call asks for a new one. Prefer
    /// <see cref="Forget(string, AccessTicket)"/> when a call was refused: it cannot
    /// drop a newer ticket that another caller has obtained in the meantime.
    /// </summary>
    public void Forget(string service)
    {
        lock (_memory) _tickets.Remove(service);
        DeleteCached(service, only: null);
    }

    /// <summary>
    /// Drops a ticket the business service refused, if it is still the one in use:
    /// several callers refused with the same ticket drop it once, and the one a
    /// first caller has already replaced is left alone (ARCA would answer
    /// coe.alreadyAuthenticated to a login that was not needed).
    /// </summary>
    public void Forget(string service, AccessTicket refused)
    {
        lock (_memory)
            if (_tickets.TryGetValue(service, out var current) && current == refused)
                _tickets.Remove(service);
        DeleteCached(service, only: refused);
    }

    /// <summary>One call to loginCms, without looking at the cache.</summary>
    public async Task<AccessTicket> LoginAsync(string service, CancellationToken ct = default)
    {
        var cms = Convert.ToBase64String(SignTra(BuildTra(service, _time.GetUtcNow())));
        var envelope =
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
            $"xmlns:wsaa=\"{WsaaNamespace}\"><soapenv:Header/><soapenv:Body><wsaa:loginCms>" +
            $"<wsaa:in0>{cms}</wsaa:in0></wsaa:loginCms></soapenv:Body></soapenv:Envelope>";

        using var content = new StringContent(envelope, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");
        using var message = new HttpRequestMessage(HttpMethod.Post, options.WsaaUrl) { Content = content };
        message.Headers.Add("SOAPAction", "\"\"");

        var (status, body) = await SoapTransport.SendAsync(http, message, "WSAA", ct);
        // An error page that is not XML still says what the status was: only a good answer has to parse.
        var document = status >= 400 ? SoapTransport.TryParse(body) : SoapTransport.Parse(body, "WSAA");
        var fault = document?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var code = (fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultcode" or "Value")?.Value ?? "").Split(':').Last();
            var text = fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value ?? "";
            throw new WsaaFaultException(code, text);
        }
        if (status >= 400) throw SoapTransport.Failed("WSAA", status, faultText: null);

        var ticketXml = document!.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value
                        ?? throw new ArcaUnavailableException("WSAA's answer has no loginCmsReturn.");
        return ReadTicket(ticketXml);
    }

    /// <summary>The TRA: ten minutes either side of now, which absorbs any clock difference with ARCA.</summary>
    private static byte[] BuildTra(string service, DateTimeOffset now)
    {
        var local = now.ToOffset(TimeSpan.FromHours(-3));
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest", new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                    new XElement("generationTime", local.AddMinutes(-10).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)),
                    new XElement("expirationTime", local.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))),
                new XElement("service", service)));
        return Encoding.UTF8.GetBytes(xml.Declaration + xml.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>CMS SignedData with the TRA inside and the certificate attached, as WSAA requires (wsaa.md §3.1).</summary>
    private byte[] SignTra(byte[] tra)
    {
        var signed = new SignedCms(new ContentInfo(tra), detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, options.Certificate)
        {
            IncludeOption = System.Security.Cryptography.X509Certificates.X509IncludeOption.EndCertOnly,
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"),
        };
        signed.ComputeSignature(signer);
        return signed.Encode();
    }

    /// <summary>Reads the TA out of loginCmsReturn. One that does not parse is an answer ARCA should not have sent, not a crash.</summary>
    private static AccessTicket ReadTicket(string ticketXml)
    {
        try
        {
            var ticket = XDocument.Parse(ticketXml).Root ?? throw new FormatException("it has no root element");
            var header = Required(ticket, "header");
            var credentials = Required(ticket, "credentials");
            var token = Required(credentials, "token").Value;
            var sign = Required(credentials, "sign").Value;
            if (token.Length == 0 || sign.Length == 0) throw new FormatException("its token or its sign is empty");
            return new AccessTicket(
                token,
                sign,
                DateTimeOffset.Parse(Required(header, "generationTime").Value, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(Required(header, "expirationTime").Value, CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is XmlException or FormatException)
        {
            throw new ArcaBadResponseException($"WSAA's ticket could not be read: {ex.Message}", ex);
        }
    }

    private static XElement Required(XElement parent, string name) =>
        parent.Element(name) ?? throw new FormatException($"it has no <{name}>");

    private bool IsFresh(AccessTicket ticket) => ticket.ExpiresAt - options.TicketRenewalMargin > _time.GetUtcNow();

    private AccessTicket? FreshInMemory(string service)
    {
        lock (_memory) return _tickets.TryGetValue(service, out var ticket) && IsFresh(ticket) ? ticket : null;
    }

    private void Remember(string service, AccessTicket ticket)
    {
        lock (_memory) _tickets[service] = ticket;
    }

    // ---- The ticket cache on disk. It is a convenience, never a reason to fail: whatever the file
    // ---- system or a damaged file can do is skipped, and the client logs in as it would without one.

    /// <summary>
    /// The file that keeps a service's ticket between runs, or null without a cache
    /// directory. Its name carries the certificate, the WSAA address with its port
    /// (two simulators on localhost have different tickets) and the service.
    /// </summary>
    private string? CachePath(string service) =>
        CacheFile(FileSafe(options.WsaaUrl.Authority), FileSafe(service));

    /// <summary>
    /// The file an earlier version wrote, named by the host alone, or null when it
    /// is the file CachePath names (an address on its scheme's default port). A
    /// ticket still alive when the client is updated is still found, because ARCA
    /// refuses a second login while it lives.
    /// </summary>
    private string? LegacyCachePath(string service)
    {
        var legacy = CacheFile(options.WsaaUrl.Host, service);
        return legacy == CachePath(service) ? null : legacy;
    }

    private string? CacheFile(string endpoint, string service) =>
        options.TicketCacheDirectory is null
            ? null
            : Path.Combine(options.TicketCacheDirectory, $"ta-{options.Certificate.Thumbprint}-{endpoint}-{service}.json");

    private static string FileSafe(string text) =>
        string.Create(text.Length, text, (chars, source) =>
        {
            for (var i = 0; i < chars.Length; i++)
                chars[i] = char.IsAsciiLetterOrDigit(source[i]) || source[i] is '.' or '-' or '_' ? source[i] : '_';
        });

    private static bool IsCacheFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException
            or InvalidOperationException or System.Security.SecurityException;

    /// <summary>The freshest ticket on disk that is worth using, under the current name or the earlier one; null when there is none.</summary>
    private AccessTicket? ReadCached(string service)
    {
        try
        {
            if (CachePath(service) is not { } path) return null;
            var current = ReadFile(path);
            var legacy = LegacyCachePath(service) is { } legacyPath ? ReadFile(legacyPath) : null;
            var best = new[] { current, legacy }.Where(t => t is not null && IsFresh(t)).MaxBy(t => t!.ExpiresAt);
            if (best is not null && ReferenceEquals(best, legacy)) WriteCached(service, best);
            return best;
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            return null;
        }
    }

    private static AccessTicket? ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var ticket = JsonSerializer.Deserialize<AccessTicket>(File.ReadAllText(path));
            return ticket is { Token.Length: > 0, Sign.Length: > 0 } ? ticket : null;
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            return null;
        }
    }

    private void WriteCached(string service, AccessTicket ticket)
    {
        try
        {
            if (CachePath(service) is not { } path) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Written aside and moved in, so a process that reads at the same moment never sees half a ticket.
            var partial = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(partial, JsonSerializer.Serialize(ticket));
                File.Move(partial, path, overwrite: true);
            }
            catch
            {
                try { File.Delete(partial); } catch (Exception ex) when (IsCacheFailure(ex)) { }
                throw;
            }
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
        }
    }

    /// <summary>
    /// Deletes the files of a service, or only those that hold <paramref name="only"/> (or cannot be read),
    /// so a newer ticket another process has written is kept.
    /// </summary>
    private void DeleteCached(string service, AccessTicket? only)
    {
        try
        {
            foreach (var path in new[] { CachePath(service), LegacyCachePath(service) }.OfType<string>())
            {
                if (!File.Exists(path)) continue;
                if (only is not null && ReadFile(path) is { } stored && stored != only) continue;
                File.Delete(path);
            }
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _login.Dispose();
    }
}

/// <summary>HTTP for both services: transport failures become ArcaUnavailableException, everything else is left to the caller.</summary>
internal static class SoapTransport
{
    /// <summary>
    /// What a server's fault says when it could not make sense of the request itself (ASMX, Axis2):
    /// the request is wrong, and sending it again will not change that.
    /// </summary>
    private static readonly string[] UnreadableRequest =
    [
        "Server was unable to read request",
        "Server did not recognize the value of HTTP Header SOAPAction",
        "Unable to handle request without a valid action parameter",
    ];

    public static async Task<(int Status, string Body)> SendAsync(HttpClient http, HttpRequestMessage message, string service, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(message, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            throw new ArcaUnavailableException($"{service} did not answer: {ex.Message}", ex);
        }
    }

    public static XDocument Parse(string body, string service)
    {
        try
        {
            return XDocument.Parse(body);
        }
        catch (XmlException ex)
        {
            throw new ArcaUnavailableException($"{service} answered something that is not XML.", ex);
        }
    }

    /// <summary>The body as XML, or null when it is not (an error page from a proxy, an empty answer).</summary>
    public static XDocument? TryParse(string body)
    {
        try
        {
            return XDocument.Parse(body);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>The text of the first SOAP fault in a body (1.1 faultstring, 1.2 Reason/Text), or null when the body is not XML or has none.</summary>
    public static string? FaultText(string body) =>
        body.TrimStart().StartsWith('<') && TryParse(body) is { } document ? FaultText(document) : null;

    public static string? FaultText(XDocument document) =>
        document.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value;

    public static bool IsUnreadableRequest(string? faultText) =>
        faultText is not null && UnreadableRequest.Any(marker => faultText.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What an answer that is not a success is. A request the server refused (an HTTP 4xx, or a fault
    /// that says it could not read the request) fails the same way when repeated; everything else,
    /// from a 5xx to a proxy's page, may pass. 408 and 429 are 4xx that ask to try again later.
    /// </summary>
    public static ArcaException Failed(string service, int status, string? faultText)
    {
        var reason = faultText is { Length: > 0 } ? $": {faultText}" : "";
        if (IsUnreadableRequest(faultText) || (status is >= 400 and < 500 and not (408 or 429)))
            return new ArcaBadRequestException($"{service} refused the request with HTTP {status}{reason}.", status);
        return new ArcaUnavailableException($"{service} answered HTTP {status}{reason}.");
    }
}
