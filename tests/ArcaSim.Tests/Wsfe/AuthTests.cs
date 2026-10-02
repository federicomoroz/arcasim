namespace ArcaSim.Tests.Wsfe;

/// <summary>
/// How WSFEv1 checks Auth, in the order and with the texts observed against
/// homologación (docs/arca/wsfev1-codigos.md §2).
/// </summary>
public class AuthTests
{
    [Fact]
    public async Task Without_Auth_the_answer_is_500_and_FECAESolicitar_has_no_header()
    {
        await using var sim = ArcaSimHarness.Start();

        var (_, body) = await sim.PostWsfeAsync("FECAESolicitar", "");

        Assert.Contains("<FECAESolicitarResult><Errors><Err><Code>500</Code><Msg>Campo Auth no fue ingresado o esta mal formado.</Msg></Err></Errors></FECAESolicitarResult>", body);
    }

    [Fact]
    public async Task Auth_without_the_service_namespace_is_ignored_like_ASMX_does()
    {
        await using var sim = ArcaSimHarness.Start();

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", "<Auth><Token>x</Token><Sign>y</Sign><Cuit>1</Cuit></Auth>");

        Assert.Contains("<RegXReq>0</RegXReq><Errors><Err><Code>500</Code>", body);
    }

    [Fact]
    public async Task An_empty_token_is_reported_as_a_null_parameter()
    {
        await using var sim = ArcaSimHarness.Start();

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", "<ar:Auth><ar:Token></ar:Token><ar:Sign>x</ar:Sign><ar:Cuit>20111111112</ar:Cuit></ar:Auth>");

        Assert.Contains("<Msg>ValidacionDeToken: Parametro nulo o vacio (token)</Msg>", body);
    }

    [Fact]
    public async Task An_expired_ticket_is_checked_before_its_signature()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var ticket = await sim.Wsaa(ArcaSimHarness.Issuer, await sim.IssueCertificateAsync(ArcaSimHarness.Issuer, "facturacion")).LoginAsync("wsfe");
        sim.Clock.Advance(TimeSpan.FromHours(13));

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", ArcaSimHarness.AuthXml(ticket with { Sign = "AAAA" }, ArcaSimHarness.Issuer));

        Assert.Matches(@"<Msg>ValidacionDeToken: No validaron las fechas del token\. GenTime=\d+, ExpTime=\d+, NowUTC=\d+</Msg>", body);
    }

    [Fact]
    public async Task A_sign_that_does_not_match_the_token_fails_the_hash_check()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var ticket = await sim.Wsaa(ArcaSimHarness.Issuer, await sim.IssueCertificateAsync(ArcaSimHarness.Issuer, "facturacion")).LoginAsync("wsfe");

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", ArcaSimHarness.AuthXml(ticket with { Sign = "AAAA" }, ArcaSimHarness.Issuer));

        Assert.Contains("<Msg>ValidacionDeToken: Error al verificar hash: </Msg>", body);
    }

    [Fact]
    public async Task A_CUIT_that_is_not_among_the_token_s_relations_is_refused()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var ticket = await sim.Wsaa(ArcaSimHarness.Issuer, await sim.IssueCertificateAsync(ArcaSimHarness.Issuer, "facturacion")).LoginAsync("wsfe");

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", ArcaSimHarness.AuthXml(ticket, 23000000000));

        Assert.Contains("<Msg>ValidacionDeToken: No apareció CUIT en lista de relaciones: 23000000000</Msg>", body);
    }

    [Fact]
    public async Task A_ticket_for_another_service_names_both_services()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var certificate = await sim.IssueCertificateAsync(ArcaSimHarness.Issuer, "facturacion", "wsfe", "ws_sr_padron_a13");
        var ticket = await sim.Wsaa(ArcaSimHarness.Issuer, certificate).LoginAsync("ws_sr_padron_a13");

        var (_, body) = await sim.PostWsfeAsync("FECompTotXRequest", ArcaSimHarness.AuthXml(ticket, ArcaSimHarness.Issuer));

        Assert.Contains("<Msg>ValidacionDeToken: No valido Id Sistema: wsfe(Id Sistema de token es: ws_sr_padron_a13)</Msg>", body);
    }
}
