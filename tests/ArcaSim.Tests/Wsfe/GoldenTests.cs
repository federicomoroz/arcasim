using System.Text.RegularExpressions;
using ArcaSim.Application;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Wsfe;

/// <summary>
/// ArcaSim's answers next to ARCA's, byte for byte. The expected strings are
/// real responses captured in the study (docs/arca/wsfev1.md): homologación on
/// 2026-10-01 and the recordings of 2021. Only what has to differ (the node
/// name, the time, a CAE) is masked.
/// </summary>
public class GoldenTests
{
    [Fact]
    public async Task FEDummy_is_identical_to_homologacion_except_node_and_time()
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await sim.PostWsfeAsync("FEDummy", "");

        Assert.Equal(200, status);
        const string real = "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><soap:Header><FEHeaderInfo xmlns=\"http://ar.gov.afip.dif.FEV1/\"><ambiente>HomologacionExterno - srt</ambiente><fecha>2026-10-01T22:47:00.4411169-03:00</fecha><id>7.0.0.53</id></FEHeaderInfo></soap:Header><soap:Body><FEDummyResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FEDummyResult><AppServer>OK</AppServer><DbServer>OK</DbServer><AuthServer>OK</AuthServer></FEDummyResult></FEDummyResponse></soap:Body></soap:Envelope>";
        Assert.Equal(MaskHeader(real), MaskHeader(body));
        Assert.Matches(@"<fecha>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}-03:00</fecha>", body);
    }

    [Fact]
    public async Task A_made_up_token_gets_the_same_error_as_in_homologacion()
    {
        await using var sim = ArcaSimHarness.Start();

        var (_, body) = await sim.PostWsfeAsync("FECompUltimoAutorizado",
            "<ar:Auth><ar:Token>abc</ar:Token><ar:Sign>abc</ar:Sign><ar:Cuit>20111111112</ar:Cuit></ar:Auth><ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>1</ar:CbteTipo>");

        Assert.Equal(
            "<FECompUltimoAutorizadoResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FECompUltimoAutorizadoResult><PtoVta>0</PtoVta><CbteTipo>0</CbteTipo><CbteNro>0</CbteNro><Errors><Err><Code>600</Code><Msg>ValidacionDeToken: No valido token. Excepcion: CargarStringBase64Token: Excepción: CargarNodos: Error al cargar token XML. Excepcion: Data at the root level is invalid. Line 1, position 1.</Msg></Err></Errors></FECompUltimoAutorizadoResult></FECompUltimoAutorizadoResponse>",
            BodyOf(body));
    }

    [Fact]
    public async Task FECompTotXRequest_and_FEParamGetTiposIva_match_the_2021_recordings()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        var auth = await AuthAsync(sim);

        var (_, total) = await sim.PostWsfeAsync("FECompTotXRequest", auth);
        var (_, vat) = await sim.PostWsfeAsync("FEParamGetTiposIva", auth);

        Assert.Equal("<FECompTotXRequestResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FECompTotXRequestResult><RegXReq>250</RegXReq></FECompTotXRequestResult></FECompTotXRequestResponse>", BodyOf(total));
        Assert.Equal("<FEParamGetTiposIvaResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FEParamGetTiposIvaResult><ResultGet><IvaTipo><Id>3</Id><Desc>0%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>4</Id><Desc>10.5%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>5</Id><Desc>21%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>6</Id><Desc>27%</Desc><FchDesde>20090220</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>8</Id><Desc>5%</Desc><FchDesde>20141020</FchDesde><FchHasta>NULL</FchHasta></IvaTipo><IvaTipo><Id>9</Id><Desc>2.5%</Desc><FchDesde>20141020</FchDesde><FchHasta>NULL</FchHasta></IvaTipo></ResultGet></FEParamGetTiposIvaResult></FEParamGetTiposIvaResponse>", BodyOf(vat));
    }

    /// <summary>
    /// The 2021 recording of an approved invoice A with the monotributo legend
    /// (wsfev1.md §3.7, example 2), replayed at the same moment, for the same
    /// CUITs and right after voucher 1835. Only the CAE differs.
    /// </summary>
    [Fact]
    public async Task An_approved_invoice_matches_the_2021_recording_but_for_the_CAE()
    {
        const long issuer = 20267565393;
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2021, 7, 1, 17, 21, 1, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(issuer, "Emisor de la grabación", VatCondition.ResponsableInscripto, new PointOfSale(4000, PointOfSaleKind.WebServiceCae));
        await sim.PutTaxpayerAsync(30000000007, "Receptor monotributista", VatCondition.Monotributo);
        await SeedLastVoucherAsync(sim, issuer, 4000, 1, 1835, new DateOnly(2021, 7, 1));
        var auth = await AuthAsync(sim, issuer);

        var (_, body) = await sim.PostWsfeAsync("FECAESolicitar", auth + RecordedInvoice(1836));

        const string real = "<FECAESolicitarResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FECAESolicitarResult><FeCabResp><Cuit>20267565393</Cuit><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo><FchProceso>20210701172101</FchProceso><CantReg>1</CantReg><Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEDetResponse><Concepto>3</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1836</CbteDesde><CbteHasta>1836</CbteHasta><CbteFch>20210701</CbteFch><Resultado>A</Resultado><Observaciones><Obs><Code>10217</Code><Msg>El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.</Msg></Obs></Observaciones><CAE>71263951827464</CAE><CAEFchVto>20210711</CAEFchVto></FECAEDetResponse></FeDetResp></FECAESolicitarResult></FECAESolicitarResponse>";
        Assert.Equal(MaskCae(real), MaskCae(BodyOf(body)));
    }

    [Fact]
    public async Task Resending_an_approved_invoice_is_rejected_like_the_2021_recording()
    {
        const long issuer = 20267565393;
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2021, 7, 1, 17, 21, 15, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(issuer, "Emisor de la grabación", VatCondition.ResponsableInscripto, new PointOfSale(4000, PointOfSaleKind.WebServiceCae));
        await SeedLastVoucherAsync(sim, issuer, 4000, 1, 1839, new DateOnly(2021, 7, 1));
        var auth = await AuthAsync(sim, issuer);

        var (_, body) = await sim.PostWsfeAsync("FECAESolicitar", auth + RecordedInvoice(1839, concept: 1));

        Assert.Equal(
            "<FECAESolicitarResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FECAESolicitarResult><FeCabResp><Cuit>20267565393</Cuit><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo><FchProceso>20210701172115</FchProceso><CantReg>1</CantReg><Resultado>R</Resultado><Reproceso>N</Reproceso></FeCabResp><FeDetResp><FECAEDetResponse><Concepto>1</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1839</CbteDesde><CbteHasta>1839</CbteHasta><CbteFch>20210701</CbteFch><Resultado>R</Resultado><CAE /><CAEFchVto /></FECAEDetResponse></FeDetResp><Errors><Err><Code>10016</Code><Msg>El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.</Msg></Err></Errors></FECAESolicitarResult></FECAESolicitarResponse>",
            BodyOf(body));
    }

    [Fact]
    public async Task FECompConsultar_matches_the_2021_recording_but_for_the_CAE()
    {
        const long issuer = 20267565393;
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2021, 7, 1, 17, 21, 6, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(issuer, "Emisor de la grabación", VatCondition.ResponsableInscripto, new PointOfSale(4000, PointOfSaleKind.WebServiceCae));
        await sim.PutTaxpayerAsync(30000000007, "Receptor monotributista", VatCondition.Monotributo);
        await SeedLastVoucherAsync(sim, issuer, 4000, 1, 1836, new DateOnly(2021, 7, 1));
        var auth = await AuthAsync(sim, issuer);
        await sim.PostWsfeAsync("FECAESolicitar", auth + RecordedInvoice(1837, withVatCondition: false));

        var (_, body) = await sim.PostWsfeAsync("FECompConsultar",
            auth + "<ar:FeCompConsReq><ar:CbteTipo>1</ar:CbteTipo><ar:CbteNro>1837</ar:CbteNro><ar:PtoVta>4000</ar:PtoVta></ar:FeCompConsReq>");

        // In 2021 there was no CondicionIVAReceptorId: today the same request also gets observation 10245, which the recording cannot have.
        const string real = "<FECompConsultarResponse xmlns=\"http://ar.gov.afip.dif.FEV1/\"><FECompConsultarResult><ResultGet><Concepto>3</Concepto><DocTipo>80</DocTipo><DocNro>30000000007</DocNro><CbteDesde>1837</CbteDesde><CbteHasta>1837</CbteHasta><CbteFch>20210701</CbteFch><ImpTotal>122</ImpTotal><ImpTotConc>0</ImpTotConc><ImpNeto>100</ImpNeto><ImpOpEx>0</ImpOpEx><ImpTrib>1</ImpTrib><ImpIVA>21</ImpIVA><FchServDesde>20210701</FchServDesde><FchServHasta>20210701</FchServHasta><FchVtoPago>20210701</FchVtoPago><MonId>PES</MonId><MonCotiz>1</MonCotiz><Tributos><Tributo><Id>99</Id><Desc>Impuesto Municipal Matanza</Desc><BaseImp>100</BaseImp><Alic>1</Alic><Importe>1</Importe></Tributo></Tributos><Iva><AlicIva><Id>5</Id><BaseImp>100</BaseImp><Importe>21</Importe></AlicIva></Iva><Resultado>A</Resultado><CodAutorizacion>71263951827477</CodAutorizacion><EmisionTipo>CAE</EmisionTipo><FchVto>20210711</FchVto><FchProceso>20210701172106</FchProceso><Observaciones><Obs><Code>10217</Code><Msg>El credito fiscal discriminado en el presente comprobante solo podra ser computado a efectos del Procedimiento permanente de transicion al Regimen General.</Msg></Obs></Observaciones><PtoVta>4000</PtoVta><CbteTipo>1</CbteTipo></ResultGet></FECompConsultarResult></FECompConsultarResponse>";
        var today = Regex.Replace(BodyOf(body), "<Obs><Code>10245</Code><Msg>[^<]*</Msg></Obs>", "");
        Assert.Equal(MaskCae(real), MaskCae(today));
    }

    [Fact]
    public async Task A_date_out_of_range_comes_back_as_an_observation_without_errors_like_the_2021_recording()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync(now: new DateTimeOffset(2021, 7, 22, 10, 0, 0, TimeSpan.FromHours(-3)));
        await using var __ = sim;
        var auth = await AuthAsync(sim);

        var (_, body) = await sim.PostWsfeAsync("FECAESolicitar", auth +
            "<ar:FeCAEReq><ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>6</ar:CbteTipo></ar:FeCabReq><ar:FeDetReq><ar:FECAEDetRequest>" +
            "<ar:Concepto>1</ar:Concepto><ar:DocTipo>99</ar:DocTipo><ar:DocNro>0</ar:DocNro><ar:CbteDesde>1</ar:CbteDesde><ar:CbteHasta>1</ar:CbteHasta><ar:CbteFch>20210701</ar:CbteFch>" +
            "<ar:ImpTotal>121</ar:ImpTotal><ar:ImpTotConc>0</ar:ImpTotConc><ar:ImpNeto>100</ar:ImpNeto><ar:ImpOpEx>0</ar:ImpOpEx><ar:ImpTrib>0</ar:ImpTrib><ar:ImpIVA>21</ar:ImpIVA>" +
            "<ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz><ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>" +
            "<ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva></ar:Iva></ar:FECAEDetRequest></ar:FeDetReq></ar:FeCAEReq>");

        Assert.Contains("<CbteFch>20210701</CbteFch><Resultado>R</Resultado><Observaciones><Obs><Code>10016</Code><Msg>Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos</Msg></Obs></Observaciones><CAE /><CAEFchVto /></FECAEDetResponse>", body);
        Assert.Contains("<Resultado>R</Resultado><Reproceso>N</Reproceso></FeCabResp>", body);
        Assert.DoesNotContain("<Errors>", body);
    }

    [Fact]
    public async Task A_batch_stops_at_the_first_rejection_and_the_rest_go_back_unprocessed()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var auth = await AuthAsync(sim);
        string Detail(long number, decimal total) =>
            "<ar:FECAEDetRequest><ar:Concepto>1</ar:Concepto><ar:DocTipo>99</ar:DocTipo><ar:DocNro>0</ar:DocNro>" +
            $"<ar:CbteDesde>{number}</ar:CbteDesde><ar:CbteHasta>{number}</ar:CbteHasta>" +
            $"<ar:ImpTotal>{total}</ar:ImpTotal><ar:ImpTotConc>0</ar:ImpTotConc><ar:ImpNeto>100</ar:ImpNeto><ar:ImpOpEx>0</ar:ImpOpEx><ar:ImpTrib>0</ar:ImpTrib><ar:ImpIVA>21</ar:ImpIVA>" +
            "<ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz><ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>" +
            "<ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva></ar:Iva></ar:FECAEDetRequest>";

        var (_, body) = await sim.PostWsfeAsync("FECAESolicitar", auth +
            "<ar:FeCAEReq><ar:FeCabReq><ar:CantReg>3</ar:CantReg><ar:PtoVta>1</ar:PtoVta><ar:CbteTipo>6</ar:CbteTipo></ar:FeCabReq><ar:FeDetReq>" +
            Detail(1, 121) + Detail(2, 999) + Detail(3, 121) + "</ar:FeDetReq></ar:FeCAEReq>");

        Assert.Contains("<Resultado>P</Resultado>", body);
        var results = Regex.Matches(body, "<CbteDesde>(\\d)</CbteDesde><CbteHasta>\\d</CbteHasta><CbteFch>\\d{8}</CbteFch><Resultado>(\\w)</Resultado>(<Observaciones>.*?</Observaciones>)?")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success)).ToList();
        Assert.Equal([("1", "A", false), ("2", "R", true), ("3", "R", false)], results);
    }

    [Fact]
    public async Task Broken_XML_gets_an_empty_400_and_an_unknown_SOAPAction_a_fault()
    {
        await using var sim = ArcaSimHarness.Start();

        var (brokenStatus, brokenBody) = await sim.PostSoapAsync(ArcaSimHarness.WsfeUrl, "<soapenv:Envelope", null);
        var (unknownStatus, unknownBody) = await sim.PostWsfeAsync("FEXXX", "");

        Assert.Equal(400, brokenStatus);
        Assert.Empty(brokenBody);
        Assert.Equal(500, unknownStatus);
        Assert.Contains("<faultcode>soap:Client</faultcode><faultstring>System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.FEV1/FEXXX.", unknownBody);
        Assert.DoesNotContain("FEHeaderInfo", unknownBody);
    }

    [Fact]
    public async Task Like_ASMX_it_accepts_any_element_order_ignores_unknown_ones_and_routes_without_SOAPAction()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var auth = await AuthAsync(sim);

        var (_, body) = await sim.PostWsfeAsync("FECompUltimoAutorizado",
            "<ar:CbteTipo>6</ar:CbteTipo><ar:Desconocido>x</ar:Desconocido>" + auth + "<ar:PtoVta>1</ar:PtoVta>", withAction: false);

        Assert.Contains("<FECompUltimoAutorizadoResult><PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>0</CbteNro></FECompUltimoAutorizadoResult>", body);
    }

    private static string RecordedInvoice(long number, int concept = 3, bool withVatCondition = true) =>
        "<ar:FeCAEReq><ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>4000</ar:PtoVta><ar:CbteTipo>1</ar:CbteTipo></ar:FeCabReq><ar:FeDetReq><ar:FECAEDetRequest>" +
        $"<ar:Concepto>{concept}</ar:Concepto><ar:DocTipo>80</ar:DocTipo><ar:DocNro>30000000007</ar:DocNro>" +
        $"<ar:CbteDesde>{number}</ar:CbteDesde><ar:CbteHasta>{number}</ar:CbteHasta><ar:CbteFch>20210701</ar:CbteFch>" +
        "<ar:ImpTotal>122.00</ar:ImpTotal><ar:ImpTotConc>0.00</ar:ImpTotConc><ar:ImpNeto>100.00</ar:ImpNeto><ar:ImpOpEx>0.00</ar:ImpOpEx><ar:ImpTrib>1.00</ar:ImpTrib><ar:ImpIVA>21.00</ar:ImpIVA>" +
        (concept == 1 ? "" : "<ar:FchServDesde>20210701</ar:FchServDesde><ar:FchServHasta>20210701</ar:FchServHasta><ar:FchVtoPago>20210701</ar:FchVtoPago>") +
        "<ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz>" +
        (withVatCondition ? "<ar:CondicionIVAReceptorId>6</ar:CondicionIVAReceptorId>" : "") +
        "<ar:Tributos><ar:Tributo><ar:Id>99</ar:Id><ar:Desc>Impuesto Municipal Matanza</ar:Desc><ar:BaseImp>100.00</ar:BaseImp><ar:Alic>1.00</ar:Alic><ar:Importe>1.00</ar:Importe></ar:Tributo></ar:Tributos>" +
        "<ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100.00</ar:BaseImp><ar:Importe>21.00</ar:Importe></ar:AlicIva></ar:Iva>" +
        "</ar:FECAEDetRequest></ar:FeDetReq></ar:FeCAEReq>";

    /// <summary>Puts a voucher in the store so the next number is the one the recording used.</summary>
    private static Task SeedLastVoucherAsync(ArcaSimHarness sim, long cuit, int pointOfSale, int type, long number, DateOnly date) =>
        sim.Services.GetRequiredService<IVoucherStore>().AddAsync(new StoredVoucher(
            cuit, pointOfSale, type, number, number, date, EmissionType.Cae, "00000000000000", date.AddDays(10),
            sim.Clock.Now, new FECAEDetRequest(), []));

    private static async Task<string> AuthAsync(ArcaSimHarness sim, long cuit = ArcaSimHarness.Issuer)
    {
        var certificate = await sim.IssueCertificateAsync(cuit, "facturacion");
        var ticket = await sim.Wsaa(cuit, certificate).LoginAsync("wsfe");
        return ArcaSimHarness.AuthXml(ticket, cuit);
    }

    private static string BodyOf(string envelope)
    {
        var start = envelope.IndexOf("<soap:Body>", StringComparison.Ordinal) + "<soap:Body>".Length;
        return envelope[start..envelope.IndexOf("</soap:Body>", StringComparison.Ordinal)];
    }

    private static string MaskHeader(string envelope) =>
        Regex.Replace(Regex.Replace(envelope, "<ambiente>[^<]*</ambiente>", "<ambiente/>"), "<fecha>[^<]*</fecha>", "<fecha/>");

    private static string MaskCae(string body) =>
        Regex.Replace(body, "<(CAE|CodAutorizacion)>\\d{14}</", "<$1>CAE</");
}
