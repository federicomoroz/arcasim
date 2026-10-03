using System.Text;
using ArcaSim.Application.Services.Ultimos;

namespace ArcaSim.Tests.Services.Ultimos;

/// <summary>wscta: a DNRPA official reads, approves, rejects and queries transfer certificates, under the manual's 5-day and 3-change rules.</summary>
public class WsctaRulesTests
{
    // The caller's seeded certificates: pending on ZZZ001, pending on ZZZ002, voided on ZZZ001, approved in 2025 on ZZZ003.
    private const long First = UltimosProbe.Caller * 1000 + 1;
    private const long Second = First + 1;
    private const long Voided = First + 2;
    private const long OldApproval = First + 3;

    [Fact]
    public async Task An_official_reads_approves_and_queries_a_certificate()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");

        var pdf = (await cta.CallAsync("getCertificadoPDF", Read(cta, First, "ZZZ001"))).Valid();
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(Convert.FromBase64String(pdf.Value("documentoPDF"))));
        Assert.Equal("1", pdf.Value("idLog"));

        var approved = (await cta.CallAsync("aprobarCertificado", Decide(cta, First, "REGISTRO SECCIONAL PALERMO 2"))).Valid();
        Assert.Equal("2", approved.Value("idLog"));

        var state = (await cta.CallAsync("consultarEstadoCertificado", Read(cta, First, "ZZZ001"))).Valid();
        Assert.Equal("Aprobado", state.Value("estado"));
        Assert.Equal("01-10-2026 12:00:00", state.Value("fechaAprobacion"));
        Assert.Empty(state.All("fechaRechazo"));
        Assert.Equal("REGISTRO SECCIONAL PALERMO 2", state.Value("registroSeccionalDNRPA"));
        Assert.Equal(pdf.Value("documentoPDF"), state.Value("documentoPDF"));
        Assert.Equal("3", state.Value("idLog"));

        // The voided certificate on the same domain is left out.
        var listed = (await cta.CallAsync("getListCertificatesByDomain", ByDomain(cta, "ZZZ001"))).Valid();
        var item = Assert.Single(listed.All("return"));
        Assert.Equal("4", item.Value("idLog"));
        Assert.Equal("ZZZ001", item.Value("nroDominio"));
        Assert.Equal("27666666661", item.Value("cuitSolicitante"));
        Assert.Equal("9000000001", item.Value("nroTramite"));
        Assert.Equal("Aprobado", item.Value("estado"));
        Assert.Equal("01-10-2026 12:00:00", item.Value("fechaAprobacion"));

        var stored = await cta.Store.GetAsync<TransferCertificate>(CertificateRegistry.Certificates, CertificateRegistry.Key(First));
        Assert.Equal(1, stored!.Changes);
        Assert.Equal(["getCertificadoPDF", "aprobarCertificado", "consultarEstadoCertificado", "getListCertificatesByDomain"], stored.Logs!.Select(l => l.Operation));
        Assert.Equal([1L, 2, 3, 4], stored.Logs!.Select(l => l.Id));
    }

    [Fact]
    public async Task A_decision_is_reversed_within_5_days_until_the_certificate_changed_state_more_than_three_times()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");

        (await cta.CallAsync("rechazarCertificado", Decide(cta, Second, "REGISTRO 1"))).Valid();
        sim.Clock.Advance(TimeSpan.FromHours(2));
        (await cta.CallAsync("aprobarCertificado", Decide(cta, Second, "REGISTRO 2"))).Valid();
        (await cta.CallAsync("rechazarCertificado", Decide(cta, Second, "REGISTRO 3"))).Valid();
        (await cta.CallAsync("aprobarCertificado", Decide(cta, Second, "REGISTRO 4"))).Valid();

        var fifth = await cta.CallAsync("rechazarCertificado", Decide(cta, Second, "REGISTRO 5"));
        Assert.Equal(500, fifth.Status);
        Assert.Equal("soapenv:Server", fifth.FaultCode);
        Assert.Equal($"El certificado {Second} tuvo más de 3 modificaciones de estado y ya no puede rechazarse.", fifth.Fault);

        var state = (await cta.CallAsync("consultarEstadoCertificado", Read(cta, Second, "ZZZ002"))).Valid();
        Assert.Equal("Aprobado", state.Value("estado"));
        Assert.Equal("01-10-2026 14:00:00", state.Value("fechaAprobacion"));
        Assert.Equal("01-10-2026 14:00:00", state.Value("fechaRechazo"));
        Assert.Equal("REGISTRO 4", state.Value("registroSeccionalDNRPA"));
    }

    [Fact]
    public async Task A_decision_older_than_5_days_stands()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");
        (await cta.CallAsync("aprobarCertificado", Decide(cta, First, "REGISTRO 1"))).Valid();
        (await cta.CallAsync("rechazarCertificado", Decide(cta, Second, "REGISTRO 1"))).Valid();

        sim.Clock.Advance(TimeSpan.FromDays(5));
        await cta.LoginAsync();
        (await cta.CallAsync("rechazarCertificado", Decide(cta, First, "REGISTRO 2"))).Valid();

        sim.Clock.Advance(TimeSpan.FromDays(1));
        await cta.LoginAsync();
        var late = await cta.CallAsync("aprobarCertificado", Decide(cta, Second, "REGISTRO 2"));
        Assert.Equal($"El certificado {Second} fue rechazado hace más de 5 días y ya no puede aprobarse.", late.Fault);

        var old = await cta.CallAsync("rechazarCertificado", Decide(cta, OldApproval, "REGISTRO 2"));
        Assert.Equal($"El certificado {OldApproval} fue aprobado hace más de 5 días y ya no puede rechazarse.", old.Fault);
    }

    [Fact]
    public async Task Voided_unknown_and_already_decided_certificates_are_faults()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");

        var voided = await cta.CallAsync("getCertificadoPDF", Read(cta, Voided, "ZZZ001"));
        Assert.Equal(500, voided.Status);
        Assert.Equal("soapenv:Server", voided.FaultCode);
        Assert.Equal($"El certificado {Voided} no está activo: fue anulado por el contribuyente.", voided.Fault);
        Assert.Contains("<detail />", voided.Body);
        Assert.Equal(voided.Fault, (await cta.CallAsync("aprobarCertificado", Decide(cta, Voided, "REGISTRO 1"))).Fault);
        Assert.Equal(voided.Fault, (await cta.CallAsync("consultarEstadoCertificado", Read(cta, Voided, "ZZZ001"))).Fault);

        Assert.Equal($"No existe el certificado {First} para el dominio ZZZ999.", (await cta.CallAsync("getCertificadoPDF", Read(cta, First, "ZZZ999"))).Fault);
        Assert.Equal("No existe el certificado 12345678901234.", (await cta.CallAsync("rechazarCertificado", Decide(cta, 12345678901234, "REGISTRO 1"))).Fault);
        Assert.Equal("El registro seccional DNRPA es obligatorio.", (await cta.CallAsync("aprobarCertificado", Decide(cta, First, ""))).Fault);
        Assert.Equal("No existen certificados activos para el dominio ZZZ999.", (await cta.CallAsync("getListCertificatesByDomain", ByDomain(cta, "ZZZ999"))).Fault);

        (await cta.CallAsync("aprobarCertificado", Decide(cta, First, "REGISTRO 1"))).Valid();
        Assert.Equal($"El certificado {First} ya está aprobado.", (await cta.CallAsync("aprobarCertificado", Decide(cta, First, "REGISTRO 1"))).Fault);

        var stored = await cta.Store.GetAsync<TransferCertificate>(CertificateRegistry.Certificates, CertificateRegistry.Key(Voided));
        Assert.Empty(stored!.Logs ?? []);
    }

    [Fact]
    public async Task The_seller_creates_and_voids_certificates_through_the_admin_API()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");
        const long number = 20261001000077;
        var document = Convert.ToBase64String("%PDF-1.4 certificado de la prueba"u8.ToArray());
        await cta.PutDocumentAsync(CertificateRegistry.Certificates, number.ToString(),
            new { number, domain = "ZZZ777", applicant = 20111111112, procedure = "F381-77", state = "Pendiente", pdf = document });

        var pdf = (await cta.CallAsync("getCertificadoPDF", Read(cta, number, "zzz777"))).Valid();
        Assert.Equal(document, pdf.Value("documentoPDF"));
        (await cta.CallAsync("aprobarCertificado", Decide(cta, number, "REGISTRO 1"))).Valid();
        var listed = (await cta.CallAsync("getListCertificatesByDomain", ByDomain(cta, "ZZZ777"))).Valid();
        Assert.Equal("Aprobado", listed.Value("estado"));
        Assert.Equal("20111111112", listed.Value("cuitSolicitante"));

        await cta.PutDocumentAsync(CertificateRegistry.Certificates, number.ToString(),
            new { number, domain = "ZZZ777", applicant = 20111111112, procedure = "F381-77", state = "Anulado" });
        Assert.Equal($"El certificado {number} no está activo: fue anulado por el contribuyente.",
            (await cta.CallAsync("consultarEstadoCertificado", Read(cta, number, "ZZZ777"))).Fault);
    }

    [Fact]
    public async Task Only_an_official_in_the_tickets_relations_operates_and_dummy_needs_no_ticket()
    {
        await using var sim = ArcaSimHarness.Start();
        var cta = await UltimosProbe.StartAsync(sim, "wscta");

        var stranger = await cta.CallAsync("aprobarCertificado",
            $"<token>{cta.Token}</token><sign>{cta.Sign}</sign><nroCertificado>{First}</nroCertificado><cuitRepresentado>20222222223</cuitRepresentado>" +
            "<registroSeccionalDNRPA>REGISTRO 1</registroSeccionalDNRPA>");
        Assert.Equal(500, stranger.Status);
        Assert.Contains("20222222223", stranger.Fault);

        var dummy = await cta.PostAsync("", "http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/dummy");
        Assert.Equal("OK", dummy.Valid().Value("appserver"));
        Assert.Contains("<ns1:dummyResponse xmlns:ns1=\"http://impl.service.cta.afip.gov.ar/CertificadoDNRPAService/\"><return>", dummy.Body);
    }

    private static string Read(UltimosProbe cta, long number, string domain) =>
        $"<token>{cta.Token}</token><sign>{cta.Sign}</sign><nroCertificado>{number}</nroCertificado><cuitRepresentado>{UltimosProbe.Caller}</cuitRepresentado><nroDominio>{domain}</nroDominio>";

    private static string Decide(UltimosProbe cta, long number, string registry) =>
        $"<token>{cta.Token}</token><sign>{cta.Sign}</sign><nroCertificado>{number}</nroCertificado><cuitRepresentado>{UltimosProbe.Caller}</cuitRepresentado>" +
        $"<registroSeccionalDNRPA>{registry}</registroSeccionalDNRPA>";

    private static string ByDomain(UltimosProbe cta, string domain) =>
        $"<token>{cta.Token}</token><sign>{cta.Sign}</sign><cuitRepresentado>{UltimosProbe.Caller}</cuitRepresentado><nroDominio>{domain}</nroDominio>";
}
