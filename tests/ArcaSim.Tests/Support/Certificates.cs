using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ArcaSim.Tests.Support;

internal static class Certificates
{
    /// <summary>
    /// A certificate like the one openssl makes in one line, with its private key: nobody vouches for
    /// it, so ArcaSim's authority does not trust it. The caller disposes it.
    /// </summary>
    public static X509Certificate2 SelfSigned(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.EphemeralKeySet);
    }
}
