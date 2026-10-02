using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ArcaSim.Application;

namespace ArcaSim.Infrastructure.Security;

/// <summary>
/// ArcaSim's own keys, kept as PEM files in the data directory so they
/// survive a restart: an application caches its TA for 12 hours, and a new
/// signing key would turn every cached ticket into "Error al verificar hash".
/// <list type="bullet">
/// <item>A certification authority that stands in for ARCA's "Computadores Test"
/// and issues client certificates with the DN WSASS would give them.</item>
/// <item>The key that signs TAs. ARCA's algorithm is not public, so ArcaSim's
/// tickets only work against ArcaSim (docs/arca/wsaa.md §8.3).</item>
/// </list>
/// </summary>
public sealed class KeyMaterial : ITrustStore, ITokenSigner
{
    public const string AuthorityName = "CN=ArcaSim Computadores Test, O=ArcaSim, C=AR";
    private static readonly TimeSpan ClientCertificateLifetime = TimeSpan.FromDays(730);

    private readonly X509Certificate2 _authority;
    private readonly RSA _tokenKey;

    public KeyMaterial(string directory)
    {
        Directory.CreateDirectory(directory);
        _authority = LoadOrCreateAuthority(Path.Combine(directory, "ca.crt"), Path.Combine(directory, "ca.key"));
        _tokenKey = LoadOrCreateKey(Path.Combine(directory, "token-signing.key"), 1024);
    }

    public IReadOnlyList<X509Certificate2> TrustedRoots => [_authority];

    public IReadOnlyList<X509Certificate2> Intermediates => [];

    /// <summary>The authority's certificate, for an application that wants to check what it was given.</summary>
    public string AuthorityPem => _authority.ExportCertificatePem();

    public string Sign(string token) =>
        Convert.ToBase64String(_tokenKey.SignData(Encoding.UTF8.GetBytes(token), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    public bool Verify(string token, string sign)
    {
        try
        {
            return _tokenKey.VerifyData(Encoding.UTF8.GetBytes(token), Convert.FromBase64String(sign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Signs a CSR the way WSASS does: whatever DN the request carries, the
    /// certificate says "SERIALNUMBER=CUIT n, CN=alias" (wsaa.md §3.2).
    /// </summary>
    public string IssueFromCsr(string csrPem, long cuit, string alias, DateTimeOffset now)
    {
        var request = CertificateRequest.LoadSigningRequestPem(
            csrPem, HashAlgorithmName.SHA256, CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
        var forced = new CertificateRequest(ClientName(cuit, alias), request.PublicKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Issue(forced, now).ExportCertificatePem();
    }

    /// <summary>A key pair and its certificate in one PFX, for whoever does not want to make a CSR.</summary>
    public byte[] IssueWithKey(long cuit, string alias, string password, DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(ClientName(cuit, alias), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = Issue(request, now);
        using var withKey = certificate.CopyWithPrivateKey(key);
        return withKey.Export(X509ContentType.Pfx, password);
    }

    private X509Certificate2 Issue(CertificateRequest request, DateTimeOffset now)
    {
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        var serial = RandomNumberGenerator.GetBytes(8);
        serial[0] &= 0x7F;
        return request.Create(_authority, now.AddMinutes(-5), now.Add(ClientCertificateLifetime), serial);
    }

    /// <summary>CN first and SERIALNUMBER last in the encoding, so Java prints "SERIALNUMBER=CUIT n, CN=alias" as ARCA does.</summary>
    private static X500DistinguishedName ClientName(long cuit, string alias)
    {
        var builder = new X500DistinguishedNameBuilder();
        builder.AddCommonName(alias);
        builder.Add("2.5.4.5", $"CUIT {cuit}");
        return builder.Build();
    }

    private static X509Certificate2 LoadOrCreateAuthority(string certificatePath, string keyPath)
    {
        if (File.Exists(certificatePath) && File.Exists(keyPath))
            return X509Certificate2.CreateFromPem(File.ReadAllText(certificatePath), File.ReadAllText(keyPath));

        using var key = RSA.Create(2048);
        var request = new CertificateRequest(AuthorityName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        // Wide enough for any date a test may move the clock to: the 2021 recordings, or years ahead.
        using var authority = request.CreateSelfSigned(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2050, 1, 1, 0, 0, 0, TimeSpan.Zero));
        File.WriteAllText(certificatePath, authority.ExportCertificatePem());
        File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());
        return X509Certificate2.CreateFromPem(File.ReadAllText(certificatePath), File.ReadAllText(keyPath));
    }

    private static RSA LoadOrCreateKey(string path, int size)
    {
        var key = RSA.Create(size);
        if (File.Exists(path)) key.ImportFromPem(File.ReadAllText(path));
        else File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        return key;
    }
}
