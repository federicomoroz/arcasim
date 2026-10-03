namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Seguimiento vehicular: a transfer started, followed and ended, and the TEST prefix that skips its checks.</summary>
public class WssvRulesTests
{
    [Fact]
    public async Task A_transfer_is_active_until_it_ends_and_keeps_its_positions()
    {
        await using var sim = ArcaSimHarness.Start();
        var sv = await ServiceProbe.StartAsync(sim, "wssv");

        Assert.Equal("0", (await CallAsync(sv, "TrasladoBegin", "<IdTras>T-100</IdTras><IdDES>PEMA-1</IdDES><IdRuta>R-9</IdRuta>")).Valid().Value("ErrNum"));
        await CallAsync(sv, "Reporte", "<IdTras>T-100</IdTras><IdDES>PEMA-1</IdDES><FHDES>2026-10-01T09:15:00-03:00</FHDES><Lat>-34.6037</Lat><Lng>-58.3816</Lng><Alarmas><string>PTA</string></Alarmas>");
        Assert.Equal(["T-100"], (await CallAsync(sv, "ListarTrasladosActivos", "")).Valid().All("IdTras").Select(t => t.Value));

        var positions = (await CallAsync(sv, "ListarTraslado", "<IdTras>T-100</IdTras><IdDES>PEMA-1</IdDES>")).Valid();
        Assert.Equal("2026-10-01T12:15:00Z", positions.Value("Fecha"));
        Assert.Equal("-34.6037", positions.Value("Lat"));

        (await CallAsync(sv, "TrasladoEnd", "<IdTras>T-100</IdTras>")).Valid();
        Assert.Empty((await CallAsync(sv, "ListarTrasladosActivos", "")).Valid().All("Traslado"));
        var late = (await CallAsync(sv, "Reporte", "<IdTras>T-100</IdTras><IdDES>PEMA-1</IdDES><FHDES>2026-10-01T10:00:00-03:00</FHDES><Lat>0</Lat><Lng>0</Lng>")).Valid();
        Assert.Equal(("3", "El traslado T-100 ya fue finalizado."), (late.Value("ErrNum"), late.Value("ErrMsg")));
    }

    [Fact]
    public async Task A_repeated_transfer_is_refused_unless_it_starts_with_TEST()
    {
        await using var sim = ArcaSimHarness.Start();
        var sv = await ServiceProbe.StartAsync(sim, "wssv");

        await CallAsync(sv, "TrasladoBegin", "<IdTras>T-1</IdTras><IdDES>PEMA-1</IdDES>");
        Assert.Equal("1", (await CallAsync(sv, "TrasladoBegin", "<IdTras>T-1</IdTras><IdDES>PEMA-1</IdDES>")).Valid().Value("ErrNum"));
        Assert.Equal("2", (await CallAsync(sv, "TrasladoEnd", "<IdTras>T-2</IdTras>")).Valid().Value("ErrNum"));

        await CallAsync(sv, "TrasladoBegin", "<IdTras>TEST-1</IdTras><IdDES>PEMA-1</IdDES>");
        Assert.Equal("0", (await CallAsync(sv, "TrasladoBegin", "<IdTras>TEST-1</IdTras><IdDES>PEMA-1</IdDES>")).Valid().Value("ErrNum"));
        Assert.Equal("0", (await CallAsync(sv, "Reporte", "<IdTras>TEST-9</IdTras><IdDES>PEMA-2</IdDES><FHDES>2026-10-01T10:00:00-03:00</FHDES><Lat>0</Lat><Lng>0</Lng><Alarmas><string>NPM</string></Alarmas>")).Valid().Value("ErrNum"));
        Assert.Equal(["PTA", "NPM", "NPG"], (await CallAsync(sv, "ListarAlarmas", "")).Valid().All("Id").Select(a => a.Value));
    }

    private static Task<SoapAnswer> CallAsync(ServiceProbe sv, string operation, string inner) =>
        sv.CallAsync(operation, $"<AuthObj><Token>{sv.Token}</Token><Sign>{sv.Sign}</Sign><CUIT>{ServiceProbe.Caller}</CUIT></AuthObj>" + inner, qualified: true);
}
