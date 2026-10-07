using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ArcaSim.Application.Events;
using ArcaSim.Domain;

namespace ArcaSim.Application.Wsaa;

/// <summary>
/// loginCms: checks the signed TRA in the order ARCA was seen to check it and
/// hands out a TA (docs/arca/wsaa.md §5). Each failing step stops the process,
/// as it does in ARCA.
/// </summary>
public sealed partial class WsaaService(
    IClock clock,
    SimulationSettings settings,
    ITrustStore trustStore,
    IAccessRepository access,
    ITicketLog tickets,
    ITokenSigner signer,
    EventManager events,
    TimeProvider time,
    ServiceDirectory directory)
{
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan Tolerance = TimeSpan.FromHours(24);

    private readonly KeyedLocks<(string ClientDn, string Service)> _issuing = new();

    public async Task<LoginResult> LoginAsync(string? in0, CancellationToken ct = default)
    {
        var result = await CheckAndIssueAsync(in0, ct);
        if (result.Fault is { } fault) events.Publish(new LoginRefused(time.GetUtcNow(), fault.Code, fault.Message));
        return result;
    }

    private async Task<LoginResult> CheckAndIssueAsync(string? in0, CancellationToken ct)
    {
        if (settings.ChaosOf("wsaa").Down) return LoginResult.Fail(WsaaFault.WsaaUnavailable);

        // 1-2. Base64, then CMS.
        byte[] der;
        try
        {
            der = Convert.FromBase64String(StripPem(in0 ?? ""));
        }
        catch (FormatException)
        {
            return LoginResult.Fail(WsaaFault.BadBase64);
        }
        if (der.Length == 0) return LoginResult.Fail(WsaaFault.BadBase64);

        var cms = new SignedCms();
        try
        {
            cms.Decode(der);
        }
        catch (CryptographicException)
        {
            return LoginResult.Fail(WsaaFault.BadCms);
        }

        // 3. The signer's certificate has to travel inside the CMS.
        var signerInfo = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0] : null;
        var certificate = signerInfo?.Certificate;
        if (signerInfo is null || certificate is null) return LoginResult.Fail(WsaaFault.CertificateNotFound);

        // 4. Signature before trust: a detached CMS fails here even with an untrusted certificate.
        if (cms.ContentInfo.Content.Length == 0) return LoginResult.Fail(WsaaFault.InvalidSignature);
        try
        {
            signerInfo.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException)
        {
            return LoginResult.Fail(WsaaFault.InvalidSignature);
        }

        // 5. Validity, with no tolerance for a clock a few seconds ahead.
        var now = clock.Now.ToArgentina();
        if (certificate.NotBefore.ToUniversalTime() > now.UtcDateTime) return LoginResult.Fail(WsaaFault.CertificateNotYetValid);
        if (certificate.NotAfter.ToUniversalTime() < now.UtcDateTime) return LoginResult.Fail(WsaaFault.CertificateExpired);

        // 6. Trust, before the TRA is even read. With open access any certificate that names
        //    its CUIT will do: the one WSASS issued for homologación, or a self-signed one.
        var clientCuit = DistinguishedNames.CuitOf(certificate.SubjectName);
        var openTrust = settings.OpenAccess && clientCuit is not null;
        if (!openTrust && !IsTrusted(certificate, now)) return LoginResult.Fail(WsaaFault.CertificateUntrusted);

        // 7. The TRA.
        var parsed = ParseTra(cms.ContentInfo.Content);
        if (parsed.Fault is not null) return LoginResult.Fail(parsed.Fault);
        var tra = parsed.Tra!;

        var clientDn = DistinguishedNames.ToArcaString(certificate.SubjectName);
        if (tra.Source is not null && !DistinguishedNames.AreEquivalent(tra.Source, clientDn))
            return LoginResult.Fail(WsaaFault.BadSource);
        if (tra.Destination is not null && !DistinguishedNames.AreEquivalent(tra.Destination, settings.Profile.WsaaDestinationDn))
            return LoginResult.Fail(WsaaFault.BadDestination);

        if (tra.GenerationTime > now || tra.GenerationTime < now - Tolerance) return LoginResult.Fail(WsaaFault.BadGenerationTime);
        if (tra.ExpirationTime < now) return LoginResult.Fail(WsaaFault.Expired);
        if (tra.ExpirationTime > now + Tolerance) return LoginResult.Fail(WsaaFault.BadExpirationTime);

        // 8. The service, the authorization and the anti-repeat window.
        var service = directory.Find(tra.Service);
        if (service is null) return LoginResult.Fail(WsaaFault.ServiceNotFound);

        var alias = DistinguishedNames.CommonNameOf(certificate.SubjectName) ?? "";
        var authorizations = clientCuit is null
            ? []
            : await access.AuthorizationsForAsync(clientCuit.Value, alias, service.Id, ct);
        // With open access, a certificate nobody authorized acts for its own CUIT, on every service.
        if (authorizations.Count == 0 && openTrust)
            authorizations = [new ServiceAuthorization(clientCuit!.Value, alias, clientCuit.Value, service.Id)];
        if (authorizations.Count == 0) return LoginResult.Fail(WsaaFault.NotAuthorized);

        DateTimeOffset generation, expiration;
        // The window is checked and the ticket recorded under one lock: two logins at once cannot both pass it.
        using (await _issuing.AcquireAsync((clientDn, service.Id), ct))
        {
            if (settings.ReplayWindowEnabled)
            {
                var latest = await tickets.LatestAsync(clientDn, service.Id, ct);
                if (latest is not null && latest.ExpirationTime > now && latest.GenerationTime > now - settings.Profile.ReplayWindow)
                    return LoginResult.Fail(WsaaFault.AlreadyAuthenticated);
            }

            if (settings.ChaosOf(service.Id).Down) return LoginResult.Fail(WsaaFault.ServiceUnavailable);

            generation = TruncateToMilliseconds(now);
            expiration = generation + TicketLifetime;
            await tickets.AddAsync(new IssuedTicket(clientDn, service.Id, generation, expiration), ct);
        }

        var token = BuildToken(service, clientDn, authorizations, generation, expiration);
        events.Publish(new TicketIssued(time.GetUtcNow(), clientDn, service.Id));
        return LoginResult.Ok(BuildTicket(clientDn, generation, expiration, token, signer.Sign(token)));
    }

    private bool IsTrusted(X509Certificate2 certificate, DateTimeOffset now)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now.UtcDateTime;
        chain.ChainPolicy.CustomTrustStore.AddRange(trustStore.TrustedRoots.ToArray());
        chain.ChainPolicy.ExtraStore.AddRange(trustStore.Intermediates.ToArray());
        return chain.Build(certificate);
    }

    private sealed record Tra(string? Source, string? Destination, uint UniqueId, DateTimeOffset GenerationTime, DateTimeOffset ExpirationTime, string Service);

    private sealed record ParsedTra(Tra? Tra, WsaaFault? Fault);

    /// <summary>The TRA's schema: no namespace, case-sensitive, nothing it does not define (docs/arca/wsaa.md §2.1).</summary>
    private static ParsedTra ParseTra(byte[] content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(Encoding.UTF8.GetString(content).TrimStart('﻿'));
        }
        catch (XmlException)
        {
            return new(null, WsaaFault.BadXml);
        }

        var root = document.Root;
        if (root is null || root.Name != "loginTicketRequest") return new(null, WsaaFault.BadXml);

        var version = root.Attribute("version")?.Value;
        if (version is not null && !decimal.TryParse(version, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            return new(null, WsaaFault.BadXml);
        if (version is not null && decimal.Parse(version, CultureInfo.InvariantCulture) != 1m)
            return new(null, WsaaFault.VersionNotSupported);

        var children = root.Elements().ToList();
        if (children.Count != 2 || children[0].Name != "header" || children[1].Name != "service") return new(null, WsaaFault.BadXml);

        var header = children[0].Elements().ToList();
        var names = header.Select(e => e.Name.LocalName).ToList();
        var expected = new[] { "source", "destination", "uniqueId", "generationTime", "expirationTime" };
        if (header.Any(e => e.Name.NamespaceName != "") || !IsOrderedSubset(names, expected)) return new(null, WsaaFault.BadXml);

        string? Value(string name) => header.FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        if (!uint.TryParse(Value("uniqueId"), NumberStyles.None, CultureInfo.InvariantCulture, out var uniqueId)) return new(null, WsaaFault.BadXml);
        if (!TryParseXsdDateTime(Value("generationTime"), out var generation)) return new(null, WsaaFault.BadXml);
        if (!TryParseXsdDateTime(Value("expirationTime"), out var expiration)) return new(null, WsaaFault.BadXml);

        var service = children[1].Value;
        // The XSD allows 3 to 32 characters; ARCA's FAQ says 35. The XSD wins until someone measures it.
        if (service.Length is < 3 or > 32 || !ServicePattern().IsMatch(service)) return new(null, WsaaFault.BadXml);

        return new(new Tra(Value("source"), Value("destination"), uniqueId, generation, expiration, service), null);
    }

    private static bool IsOrderedSubset(List<string> names, string[] expected)
    {
        var position = 0;
        foreach (var name in names)
        {
            var index = Array.IndexOf(expected, name, position);
            if (index < 0) return false;
            position = index + 1;
        }
        return names.Contains("uniqueId") && names.Contains("generationTime") && names.Contains("expirationTime");
    }

    /// <summary>An xsd:dateTime. Without an offset it is taken as Argentina's time, the zone ARCA asks clients to use.</summary>
    private static bool TryParseXsdDateTime(string? value, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var parsed = XmlConvert.ToDateTimeOffset(value.Trim());
            var hasOffset = OffsetPattern().IsMatch(value.Trim());
            result = hasOffset ? parsed : new DateTimeOffset(parsed.DateTime, ArgentinaTime.Offset);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string StripPem(string value) =>
        string.Join("\n", value.Split('\n').Where(line => !line.TrimStart().StartsWith("-----", StringComparison.Ordinal)));

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Offset);

    /// <summary>The sso 2.0 token, Base64 (docs/arca/wsaa.md §4.3). Its gen_time is 60 s before the TA's.</summary>
    private string BuildToken(
        WebService service, string clientDn, IReadOnlyList<ServiceAuthorization> authorizations,
        DateTimeOffset generation, DateTimeOffset expiration)
    {
        var relations = string.Concat(authorizations
            .Select(a => a.RepresentedCuit)
            .Distinct()
            .Select(cuit => $"                <relation key=\"{cuit}\" reltype=\"4\"/>\n"));
        var xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<sso version=\"2.0\">\n" +
            $"    <id src=\"{Escape(settings.Profile.WsaaDn)}\" dst=\"{Escape(service.TokenDestination)}\" unique_id=\"{RandomUInt()}\" gen_time=\"{generation.AddSeconds(-60).ToUnixTimeSeconds()}\" exp_time=\"{expiration.ToUnixTimeSeconds()}\"/>\n" +
            "    <operation type=\"login\" value=\"granted\">\n" +
            $"        <login entity=\"33693450239\" service=\"{Escape(service.Id)}\" uid=\"{Escape(clientDn)}\" authmethod=\"cms\" regmethod=\"22\">\n" +
            "            <relations>\n" +
            relations +
            "            </relations>\n" +
            "        </login>\n" +
            "    </operation>\n" +
            "</sso>\n";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
    }

    /// <summary>The TA exactly as ARCA writes it: version="1", standalone, 4-space indent, milliseconds and -03:00 (§4.2).</summary>
    private string BuildTicket(string clientDn, DateTimeOffset generation, DateTimeOffset expiration, string token, string sign) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<loginTicketResponse version=\"1\">\n" +
        "    <header>\n" +
        $"        <source>{Escape(settings.Profile.WsaaDn)}</source>\n" +
        $"        <destination>{Escape(clientDn)}</destination>\n" +
        $"        <uniqueId>{RandomUInt()}</uniqueId>\n" +
        $"        <generationTime>{FormatTime(generation)}</generationTime>\n" +
        $"        <expirationTime>{FormatTime(expiration)}</expirationTime>\n" +
        "    </header>\n" +
        "    <credentials>\n" +
        $"        <token>{token}</token>\n" +
        $"        <sign>{sign}</sign>\n" +
        "    </credentials>\n" +
        "</loginTicketResponse>\n";

    public static string FormatTime(DateTimeOffset value) =>
        value.ToArgentina().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    private static uint RandomUInt() => (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue) * 2u;

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";

    [GeneratedRegex("^[a-zA-Z][a-zA-Z_\\-0-9]*$")]
    private static partial Regex ServicePattern();

    [GeneratedRegex("(Z|[+-]\\d{2}:\\d{2})$")]
    private static partial Regex OffsetPattern();
}
