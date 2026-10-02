using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Arca.Client;

/// <summary>An access ticket (TA): what WSAA hands out and every business service asks for.</summary>
public sealed record AccessTicket(string Token, string Sign, DateTimeOffset GeneratedAt, DateTimeOffset ExpiresAt);

/// <summary>
/// WSAA's loginCms: signs a TRA with the certificate, asks for a TA and keeps
/// it until it is about to expire. One instance per certificate; it is safe to
/// share between threads.
/// </summary>
public sealed class WsaaClient(HttpClient http, ArcaOptions options, TimeProvider? time = null)
{
    private const string WsaaNamespace = "http://wsaa.view.sua.dvadac.desein.afip.gov";
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, AccessTicket> _tickets = [];

    /// <summary>A valid ticket for the service: the one in memory, the one on disk, or a new one.</summary>
    public async Task<AccessTicket> GetTicketAsync(string service, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_tickets.TryGetValue(service, out var known) && IsFresh(known)) return known;
            if (ReadCached(service) is { } cached && IsFresh(cached))
            {
                _tickets[service] = cached;
                return cached;
            }

            var ticket = await LoginAsync(service, ct);
            _tickets[service] = ticket;
            WriteCached(service, ticket);
            return ticket;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Drops a ticket the business service refused, so the next call asks for a new one.</summary>
    public void Forget(string service)
    {
        _tickets.Remove(service);
        if (CachePath(service) is { } path && File.Exists(path)) File.Delete(path);
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
        var document = SoapTransport.Parse(body, "WSAA");
        var fault = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var code = (fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultcode" or "Value")?.Value ?? "").Split(':').Last();
            var text = fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value ?? "";
            throw new WsaaFaultException(code, text);
        }
        if (status >= 400) throw new ArcaUnavailableException($"WSAA answered HTTP {status}.");

        var ticketXml = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value
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

    private static AccessTicket ReadTicket(string ticketXml)
    {
        var ticket = XDocument.Parse(ticketXml).Root!;
        var header = ticket.Element("header")!;
        var credentials = ticket.Element("credentials")!;
        return new AccessTicket(
            credentials.Element("token")!.Value,
            credentials.Element("sign")!.Value,
            DateTimeOffset.Parse(header.Element("generationTime")!.Value, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(header.Element("expirationTime")!.Value, CultureInfo.InvariantCulture));
    }

    private bool IsFresh(AccessTicket ticket) => ticket.ExpiresAt - options.TicketRenewalMargin > _time.GetUtcNow();

    private string? CachePath(string service) =>
        options.TicketCacheDirectory is null
            ? null
            : Path.Combine(options.TicketCacheDirectory, $"ta-{options.Certificate.Thumbprint}-{options.WsaaUrl.Host}-{service}.json");

    private AccessTicket? ReadCached(string service)
    {
        var path = CachePath(service);
        if (path is null || !File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<AccessTicket>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteCached(string service, AccessTicket ticket)
    {
        var path = CachePath(service);
        if (path is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(ticket));
    }
}

/// <summary>HTTP for both services: transport failures become ArcaUnavailableException, everything else is left to the caller.</summary>
internal static class SoapTransport
{
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
        catch (System.Xml.XmlException ex)
        {
            throw new ArcaUnavailableException($"{service} answered something that is not XML.", ex);
        }
    }
}
