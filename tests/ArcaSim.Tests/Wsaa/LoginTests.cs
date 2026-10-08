using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Arca.Client;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Wsaa;

/// <summary>loginCms: the ticket ArcaSim hands out and the faults it answers with (docs/arca/wsaa.md).</summary>
public class LoginTests
{
    private const long Cuit = ArcaSimHarness.Issuer;

    [Fact]
    public async Task The_ticket_has_ARCA_s_format_lasts_12_hours_and_lists_the_represented_CUIT()
    {
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-3)));
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");

        var (status, body) = await PostLoginAsync(sim, SignedTra(certificate, "wsfe", sim.Clock.Now));

        Assert.Equal(200, status);
        var ticketXml = XDocument.Parse(body).Descendants().Single(e => e.Name.LocalName == "loginCmsReturn").Value;
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<loginTicketResponse version=\"1\">\n    <header>\n", ticketXml);
        var header = XDocument.Parse(ticketXml).Root!.Element("header")!;
        Assert.Equal("CN=wsaahomo, O=AFIP, C=AR, SERIALNUMBER=CUIT 33693450239", header.Element("source")!.Value);
        Assert.Equal($"SERIALNUMBER=CUIT {Cuit}, CN=facturacion", header.Element("destination")!.Value);
        Assert.Matches(@"^2026-10-01T10:00:00\.\d{3}-03:00$", header.Element("generationTime")!.Value);
        Assert.Matches(@"^2026-10-01T22:00:00\.\d{3}-03:00$", header.Element("expirationTime")!.Value);

        var token = Encoding.UTF8.GetString(Convert.FromBase64String(XDocument.Parse(ticketXml).Root!.Element("credentials")!.Element("token")!.Value));
        Assert.Contains("<sso version=\"2.0\">", token);
        Assert.Contains($"<relation key=\"{Cuit}\" reltype=\"4\"/>", token);
        Assert.Contains("service=\"wsfe\"", token);
    }

    [Fact]
    public async Task Bad_base64_gets_the_fault_homologacion_gives_with_ArcaSim_s_node_name()
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await PostLoginAsync(sim, "%%%");

        Assert.Equal(500, status);
        const string real = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><soapenv:Body><soapenv:Fault><faultcode xmlns:ns1=\"http://xml.apache.org/axis/\">ns1:cms.bad.base64</faultcode><faultstring>No se puede decodificar el BASE64</faultstring><detail><ns2:exceptionName xmlns:ns2=\"http://xml.apache.org/axis/\">gov.afip.desein.dvadac.sua.view.wsaa.LoginFault</ns2:exceptionName><ns3:hostname xmlns:ns3=\"http://xml.apache.org/axis/\">wsaaext0.homo.afip.gov.ar</ns3:hostname></detail></soapenv:Fault></soapenv:Body></soapenv:Envelope>";
        Assert.Equal(real.Replace("wsaaext0.homo.afip.gov.ar", "wsaaext0.homo.arcasim"), body);
    }

    [Fact]
    public async Task A_detached_signature_is_an_invalid_signature_written_as_Axis_writes_it()
    {
        await using var sim = ArcaSimHarness.Start();
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");

        var (_, body) = await PostLoginAsync(sim, SignedTra(certificate, "wsfe", sim.Clock.Now, detached: true));

        Assert.Contains("<faultcode xmlns:ns1=\"http://xml.apache.org/axis/\">ns1:cms.sign.invalid</faultcode><faultstring>Firma inv&#xE1;lida o algoritmo no soportado</faultstring>", body);
    }

    [Fact]
    public async Task A_certificate_ArcaSim_did_not_issue_is_untrusted()
    {
        await using var sim = ArcaSimHarness.Start();
        using var selfSigned = Certificates.SelfSigned($"SERIALNUMBER=CUIT {Cuit}, CN=casera");

        var (_, body) = await PostLoginAsync(sim, SignedTra(selfSigned, "wsfe", sim.Clock.Now));

        Assert.Contains("ns1:cms.cert.untrusted", body);
    }

    [Fact]
    public async Task A_service_the_certificate_is_not_authorized_for_is_refused()
    {
        await using var sim = ArcaSimHarness.Start();
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion", "wsfe");

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => sim.Wsaa(Cuit, certificate).LoginAsync("ws_sr_padron_a13"));

        Assert.Equal("coe.notAuthorized", failure.Code);
        Assert.Equal("Computador no autorizado a acceder al servicio", failure.FaultMessage);
    }

    [Fact]
    public async Task An_unknown_service_is_wsn_notFound()
    {
        await using var sim = ArcaSimHarness.Start();
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => sim.Wsaa(Cuit, certificate).LoginAsync("wsinventado"));

        Assert.Equal("wsn.notFound", failure.Code);
    }

    [Fact]
    public async Task With_the_window_on_a_second_login_within_ten_minutes_is_refused()
    {
        await using var sim = ArcaSimHarness.Start();
        sim.Settings.ReplayWindowEnabled = true;
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");
        var wsaa = sim.Wsaa(Cuit, certificate);
        var first = await wsaa.LoginAsync("wsfe");

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => wsaa.LoginAsync("wsfe"));
        sim.Clock.Advance(TimeSpan.FromMinutes(11));
        var later = await wsaa.LoginAsync("wsfe");

        Assert.Equal("coe.alreadyAuthenticated", failure.Code);
        Assert.Equal("El CEE ya posee un TA valido para el acceso al WSN solicitado", failure.FaultMessage);
        Assert.False(failure.Retryable);
        Assert.NotEqual(first.Token, later.Token);
        Assert.True(later.ExpiresAt >= first.ExpiresAt + TimeSpan.FromMinutes(11), "the new ticket is issued eleven minutes after the first");
    }

    [Fact]
    public async Task The_client_keeps_its_ticket_instead_of_asking_again()
    {
        await using var sim = ArcaSimHarness.Start();
        sim.Settings.ReplayWindowEnabled = true;
        var certificate = await sim.IssueCertificateAsync(Cuit, "facturacion");
        var wsaa = sim.Wsaa(Cuit, certificate);

        var first = await wsaa.GetTicketAsync("wsfe");
        var second = await wsaa.GetTicketAsync("wsfe");

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Without_SOAPAction_Axis_refuses_the_call()
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await sim.PostSoapAsync(ArcaSimHarness.WsaaUrl, Envelope("abc"), soapAction: null);

        Assert.Equal(500, status);
        Assert.Contains("ns1:Client.NoSOAPAction</faultcode><faultstring>no SOAPAction header!</faultstring>", body);
        Assert.DoesNotContain("exceptionName", body);
    }

    [Fact]
    public async Task The_WSDL_is_ARCA_s_with_ArcaSim_s_address()
    {
        await using var sim = ArcaSimHarness.Start();

        var wsdl = await sim.Http.GetStringAsync("/ws/services/LoginCms?wsdl");

        Assert.Contains("<wsdlsoap:address location=\"http://localhost/ws/services/LoginCms\"/>", wsdl);
        Assert.Contains("targetNamespace=\"http://wsaa.view.sua.dvadac.desein.afip.gov\"", wsdl);
        Assert.DoesNotContain("afip.gov.ar", wsdl);
    }

    private static Task<(int Status, string Body)> PostLoginAsync(ArcaSimHarness sim, string in0) =>
        sim.PostSoapAsync(ArcaSimHarness.WsaaUrl, Envelope(in0), "\"\"");

    private static string Envelope(string in0) =>
        Soap.Envelope($"<wsaa:loginCms><wsaa:in0>{in0}</wsaa:in0></wsaa:loginCms>", ("wsaa", "http://wsaa.view.sua.dvadac.desein.afip.gov"));

    private static string SignedTra(X509Certificate2 certificate, string service, DateTimeOffset now, bool detached = false)
    {
        var local = now.ToOffset(TimeSpan.FromHours(-3));
        var tra = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><loginTicketRequest version=\"1.0\"><header>" +
                  $"<uniqueId>{now.ToUnixTimeSeconds()}</uniqueId>" +
                  $"<generationTime>{local.AddMinutes(-10):yyyy-MM-dd'T'HH:mm:sszzz}</generationTime>" +
                  $"<expirationTime>{local.AddMinutes(10):yyyy-MM-dd'T'HH:mm:sszzz}</expirationTime>" +
                  $"</header><service>{service}</service></loginTicketRequest>";
        var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(tra)), detached);
        cms.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly });
        return Convert.ToBase64String(cms.Encode());
    }
}
