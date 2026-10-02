using System.Text.RegularExpressions;

namespace ArcaSim.Tests.Wsfe;

/// <summary>The contingency regime: ask for a CAEA, report vouchers under it, or report it unused (wsfev1.md §6).</summary>
public class CaeaTests
{
    [Fact]
    public async Task A_CAEA_covers_the_fortnight_and_can_be_looked_up_afterwards()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync(now: new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(-3)));
        await using var __ = sim;
        var auth = await AuthAsync(sim);

        var (_, granted) = await sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");
        var (_, again) = await sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");
        var (_, found) = await sim.PostWsfeAsync("FECAEAConsultar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");

        Assert.Matches("<CAEA>\\d{14}</CAEA><Periodo>202610</Periodo><Orden>2</Orden><FchVigDesde>20261016</FchVigDesde><FchVigHasta>20261031</FchVigHasta><FchTopeInf>20261130</FchTopeInf>", granted);
        Assert.Contains("<Code>15008</Code>", again);
        Assert.Equal(Caea(granted), Caea(found));
    }

    [Fact]
    public async Task Outside_its_window_the_CAEA_is_refused_with_the_dates_written_as_ARCA_writes_them()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync(now: new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-3)));
        await using var __ = sim;
        var auth = await AuthAsync(sim);

        var (_, body) = await sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");

        Assert.Contains("<FECAEASolicitarResult><Errors><Err><Code>15006</Code><Msg>Fecha de envío podrá ser desde 5 días corridos anteriores al inicio hasta el último dia de cada quincena. Del 10/11/2026 hasta 10/31/2026</Msg></Err></Errors></FECAEASolicitarResult>", body);
    }

    [Fact]
    public async Task Looking_up_a_CAEA_that_was_never_granted_echoes_period_and_fortnight_with_602()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var __ = sim;
        var auth = await AuthAsync(sim);

        var (_, body) = await sim.PostWsfeAsync("FECAEAConsultar", auth + "<ar:Periodo>202107</ar:Periodo><ar:Orden>1</ar:Orden>");

        Assert.Contains("<FECAEAConsultarResult><ResultGet><Periodo>202107</Periodo><Orden>1</Orden></ResultGet><Errors><Err><Code>602</Code><Msg>No existen datos en nuestros registros para los parametros ingresados.</Msg></Err></Errors></FECAEAConsultarResult>", body);
    }

    [Fact]
    public async Task Vouchers_issued_offline_are_reported_and_then_found_as_CAEA()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync(now: new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(-3)));
        await using var __ = sim;
        var auth = await AuthAsync(sim);
        var (_, granted) = await sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");
        sim.Clock.Advance(TimeSpan.FromDays(7));
        auth = await AuthAsync(sim);

        var (_, reported) = await sim.PostWsfeAsync("FECAEARegInformativo", auth + Report(Caea(granted), "20261018", "20261018093000"));
        var (_, withoutCaea) = await sim.PostWsfeAsync("FECAEARegInformativo", auth + Report("", "20261018", "20261018100000", number: 2));
        var found = await wsfe.QueryAsync(900, 6, 1);

        Assert.Contains("<Resultado>A</Resultado><Reproceso>N</Reproceso></FeCabResp>", reported);
        Assert.Contains($"<Resultado>A</Resultado><CAEA>{Caea(granted)}</CAEA></FECAEADetResponse>", reported);
        Assert.Contains("<Resultado>R</Resultado><CAEA /></FECAEADetResponse></FeDetResp><Errors><Err><Code>782</Code><Msg>El &lt;CAEA&gt; es obligatorio informarlo.</Msg></Err></Errors>", withoutCaea);
        Assert.NotNull(found);
        Assert.Equal("CAEA", found.EmissionType);
        Assert.Equal(Caea(granted), found.AuthorizationCode);
    }

    [Fact]
    public async Task An_unused_CAEA_is_reported_once_per_point_of_sale()
    {
        var (sim, _) = await ArcaSimHarness.StartWithIssuerAsync(now: new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(-3)));
        await using var __ = sim;
        var auth = await AuthAsync(sim);
        var (_, granted) = await sim.PostWsfeAsync("FECAEASolicitar", auth + "<ar:Periodo>202610</ar:Periodo><ar:Orden>2</ar:Orden>");
        var caea = Caea(granted);
        sim.Clock.Advance(TimeSpan.FromDays(20));
        auth = await AuthAsync(sim);

        var (_, first) = await sim.PostWsfeAsync("FECAEASinMovimientoInformar", auth + $"<ar:PtoVta>900</ar:PtoVta><ar:CAEA>{caea}</ar:CAEA>");
        var (_, second) = await sim.PostWsfeAsync("FECAEASinMovimientoInformar", auth + $"<ar:PtoVta>900</ar:PtoVta><ar:CAEA>{caea}</ar:CAEA>");
        var (_, unknown) = await sim.PostWsfeAsync("FECAEASinMovimientoInformar", auth + "<ar:PtoVta>900</ar:PtoVta><ar:CAEA>71293955911805</ar:CAEA>");
        var (_, listed) = await sim.PostWsfeAsync("FECAEASinMovimientoConsultar", auth + $"<ar:CAEA>{caea}</ar:CAEA><ar:PtoVta>0</ar:PtoVta>");

        Assert.Contains($"<CAEA>{caea}</CAEA><FchProceso>20261101</FchProceso><PtoVta>900</PtoVta><Resultado>A</Resultado></FECAEASinMovimientoInformarResult>", first);
        Assert.Contains("<Resultado>R</Resultado><Errors><Err><Code>1209</Code>", second);
        Assert.Contains("<Errors><Err><Code>1200</Code><Msg>El codigo de autorizacion debe ser del tipo CAEA</Msg></Err></Errors>", unknown);
        Assert.Contains($"<ResultGet><FECAEASinMov><CAEA>{caea}</CAEA><FchProceso>20261101</FchProceso><PtoVta>900</PtoVta></FECAEASinMov></ResultGet>", listed);
    }

    private static string Report(string caea, string date, string generated, long number = 1) =>
        "<ar:FeCAEARegInfReq><ar:FeCabReq><ar:CantReg>1</ar:CantReg><ar:PtoVta>900</ar:PtoVta><ar:CbteTipo>6</ar:CbteTipo></ar:FeCabReq><ar:FeDetReq><ar:FECAEADetRequest>" +
        $"<ar:Concepto>1</ar:Concepto><ar:DocTipo>99</ar:DocTipo><ar:DocNro>0</ar:DocNro><ar:CbteDesde>{number}</ar:CbteDesde><ar:CbteHasta>{number}</ar:CbteHasta><ar:CbteFch>{date}</ar:CbteFch>" +
        "<ar:ImpTotal>121</ar:ImpTotal><ar:ImpTotConc>0</ar:ImpTotConc><ar:ImpNeto>100</ar:ImpNeto><ar:ImpOpEx>0</ar:ImpOpEx><ar:ImpTrib>0</ar:ImpTrib><ar:ImpIVA>21</ar:ImpIVA>" +
        "<ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz><ar:CondicionIVAReceptorId>5</ar:CondicionIVAReceptorId>" +
        "<ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva></ar:Iva>" +
        $"<ar:CAEA>{caea}</ar:CAEA><ar:CbteFchHsGen>{generated}</ar:CbteFchHsGen></ar:FECAEADetRequest></ar:FeDetReq></ar:FeCAEARegInfReq>";

    private static string Caea(string body) => Regex.Match(body, "<CAEA>(\\d{14})</CAEA>").Groups[1].Value;

    private static async Task<string> AuthAsync(ArcaSimHarness sim)
    {
        var certificate = await sim.IssueCertificateAsync(ArcaSimHarness.Issuer, "facturacion");
        return ArcaSimHarness.AuthXml(await sim.Wsaa(ArcaSimHarness.Issuer, certificate).LoginAsync("wsfe"), ArcaSimHarness.Issuer);
    }
}
