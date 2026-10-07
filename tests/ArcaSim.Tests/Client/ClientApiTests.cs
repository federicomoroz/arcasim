using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Arca.Client;

namespace ArcaSim.Tests.Client;

/// <summary>The parts of Arca.Client's public surface the end-to-end tests do not reach: the server check and loading a certificate.</summary>
public class ClientApiTests
{
    [Fact]
    public async Task The_dummy_reports_ARCA_s_three_servers_and_asks_for_no_ticket()
    {
        using var rig = new ClientRig();

        var status = await rig.Wsfe.DummyAsync();

        Assert.Equal(new ServerStatus("OK", "OK", "OK"), status);
        Assert.True(status.AllOk);
        Assert.Equal(["FEDummy:"], rig.Arca.WsfeRequests);
        Assert.Equal(0, rig.Arca.Logins);
    }

    [Fact]
    public async Task A_server_that_is_down_makes_the_status_not_all_OK()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Soap(StubArca.Result("FEDummy", "<AppServer>OK</AppServer><DbServer>NO</DbServer><AuthServer>OK</AuthServer>")));

        var status = await rig.Wsfe.DummyAsync();

        Assert.Equal("NO", status.DbServer);
        Assert.False(status.AllOk);
    }

    [Fact]
    public void A_certificate_is_loaded_from_a_PFX_with_its_private_key()
    {
        using var pfx = TemporaryPfx("secret");

        using var loaded = ArcaOptions.LoadCertificate(pfx.Path, "secret");

        Assert.True(loaded.HasPrivateKey);
        Assert.Equal(pfx.Thumbprint, loaded.Thumbprint);
    }

    [Fact]
    public void A_PFX_without_a_password_loads_without_one()
    {
        using var pfx = TemporaryPfx(null);

        using var loaded = ArcaOptions.LoadCertificate(pfx.Path);

        Assert.True(loaded.HasPrivateKey);
    }

    [Fact]
    public void A_wrong_password_is_a_cryptographic_error_for_the_caller_to_report()
    {
        using var pfx = TemporaryPfx("secret");

        Assert.ThrowsAny<CryptographicException>(() => ArcaOptions.LoadCertificate(pfx.Path, "not the password"));
    }

    [Fact]
    public async Task A_loaded_certificate_signs_the_login()
    {
        using var rig = new ClientRig();
        using var pfx = TemporaryPfx("secret");
        using var loaded = ArcaOptions.LoadCertificate(pfx.Path, "secret");
        var options = new ArcaOptions { WsaaUrl = StubArca.WsaaUrl, WsfeUrl = StubArca.WsfeUrl, Certificate = loaded, Cuit = ClientRig.Cuit };
        using var wsaa = new WsaaClient(rig.Http, options, rig.Time);

        var ticket = await wsaa.GetTicketAsync("wsfe");

        Assert.Equal("token-wsfe-1", ticket.Token);
    }

    private static TemporaryCertificateFile TemporaryPfx(string? password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("SERIALNUMBER=CUIT 20111111112, CN=load-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(Path.GetTempPath(), $"arca-client-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, password is null ? certificate.Export(X509ContentType.Pfx) : certificate.Export(X509ContentType.Pfx, password));
        return new TemporaryCertificateFile(path, certificate.Thumbprint);
    }

    private sealed record TemporaryCertificateFile(string Path, string Thumbprint) : IDisposable
    {
        public void Dispose() => File.Delete(Path);
    }
}
