using System.Xml.Linq;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>WSTABACO: a CATHE requested, linked, used, elaborated or denatured, and changes of holder confirmed by both sides.</summary>
public class TabacoRulesTests
{
    private const long Deposit = 10;

    private static Task<LiquidacionesSim> StartAsync() => LiquidacionesSim.StartAsync("wstabaco", "wstabaco-homologacion.wsdl", "cuitRepresentada");

    private static string Parameters(string operation, int units) =>
        $"<arrayParametros><parametros><deposito>{Deposit}</deposito><kilos>500</kilos><cantidad>{units}</cantidad></parametros></arrayParametros><operacion>{operation}</operacion>";

    private static async Task<List<long>> RequestAsync(LiquidacionesSim tabaco, int quantity, string initial = "N")
    {
        var answer = await tabaco.CallAsync("solicitarCathesTabacoElaborado", $"<inicial>{initial}</inicial><deposito>{Deposit}</deposito><cantidad>{quantity}</cantidad>");
        Assert.Equal("A", answer.Element("resultado")!.Value);
        return answer.Element("arrayCathes")!.Elements("cathe").Select(c => long.Parse(c.Value)).ToList();
    }

    internal static string Link(long cathe, long holder, decimal kilos, string used = "", string recovery = "S") =>
        $"<recupero>{recovery}</recupero><tipoMercaderia>3</tipoMercaderia><arrayCathesElaborados><datosTabacoElaborado><cathe>{cathe}</cathe>" +
        $"<cuitTitular>{holder}</cuitTitular><kilosBrutos>{kilos + 5}</kilosBrutos><kilosNetos>{kilos}</kilosNetos></datosTabacoElaborado></arrayCathesElaborados>{used}";

    private static string Used(long cathe, decimal kilos) =>
        $"<arrayCathesUsados><datosCatheUsado><cathe>{cathe}</cathe><kilosNetos>{kilos}</kilosNetos></datosCatheUsado></arrayCathesUsados>";

    private static async Task<List<string>> LinkedAsync(LiquidacionesSim tabaco) =>
        (await tabaco.CallAsync("consultarCathesVinculados")).Descendants("cathe").Select(c => c.Value).ToList();

    [Fact]
    public async Task CATHE_are_requested_within_the_fortnight_linked_and_used_up()
    {
        await using var tabaco = await StartAsync();

        Assert.Equal(["1200"], Errors(await tabaco.CallAsync("solicitarCathesTabacoElaborado", $"<deposito>{Deposit}</deposito><cantidad>3</cantidad>")));
        Assert.Equal("A", (await tabaco.CallAsync("informarParametrosProductivos", Parameters("A", 2))).Element("resultado")!.Value);
        var again = await tabaco.CallAsync("informarParametrosProductivos", Parameters("A", 2));
        Assert.Equal("R", again.Element("resultado")!.Value);
        Assert.Equal(["1001"], Errors(again));
        Assert.Equal("2", (await tabaco.CallAsync("consultarParametrosProductivos")).Descendants("cantidad").Single().Value);

        Assert.Equal(["1201"], Errors(await tabaco.CallAsync("solicitarCathesTabacoElaborado", $"<deposito>{Deposit}</deposito><cantidad>31</cantidad>")));
        var cathes = await RequestAsync(tabaco, 3);
        Assert.All(cathes, c => Assert.StartsWith("2026", c.ToString()));
        Assert.All(cathes, c => Assert.Equal(14, c.ToString().Length));
        Assert.Equal(3, (await tabaco.CallAsync("consultarCathesSolicitados", $"<deposito>{Deposit}</deposito>")).Descendants("datosCathe").Count());

        Assert.Equal("A", (await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[0], Producer, 95))).Element("resultado")!.Value);
        Assert.Equal(["1402"], Errors(await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[0], Producer, 95))));
        Assert.Equal([cathes[0].ToString()], await LinkedAsync(tabaco));

        Assert.Equal(["1410"], Errors(await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[1], Producer, 90, Used(cathes[0], 96), "N"))));
        Assert.Equal("A", (await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[1], Producer, 90, Used(cathes[0], 95), "N"))).Element("resultado")!.Value);
        Assert.Equal([cathes[1].ToString()], await LinkedAsync(tabaco));
        Assert.Single((await tabaco.CallAsync("consultarCathesSolicitados")).Descendants("datosCathe"));

        Assert.Equal(["1202"], Errors(await tabaco.CallAsync("solicitarCathesTabacoElaborado", $"<deposito>{Deposit}</deposito><cantidad>1</cantidad>")));
        Assert.Single(await RequestAsync(tabaco, 1, initial: "S"));
    }

    [Fact]
    public async Task An_elaboration_report_retires_its_CATHE_and_rectifications_follow_in_order()
    {
        await using var tabaco = await StartAsync();
        var cathes = await RequestAsync(tabaco, 2, initial: "S");
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[0], Producer, 50));
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[1], Producer, 60));
        string Report(int rectification, long cathe) =>
            $"<deposito>{Deposit}</deposito><fechaElaboracion>{Day()}</fechaElaboracion><rectificativa>{rectification}</rectificativa>" +
            $"<arrayCathes><datosCatheProducto><cathe>{cathe}</cathe><tipoProducto>1</tipoProducto></datosCatheProducto></arrayCathes>";

        Assert.Equal("A", (await tabaco.CallAsync("informarElaboracionProductos", Report(0, cathes[0]))).Element("resultado")!.Value);
        Assert.Equal([cathes[1].ToString()], await LinkedAsync(tabaco));
        Assert.Equal(["1601"], Errors(await tabaco.CallAsync("informarElaboracionProductos", Report(0, cathes[1]))));
        Assert.Equal(["1603"], Errors(await tabaco.CallAsync("informarElaboracionProductos", Report(2, cathes[1]))));

        Assert.Equal("A", (await tabaco.CallAsync("informarElaboracionProductos", Report(1, cathes[1]))).Element("resultado")!.Value);
        Assert.Equal([cathes[0].ToString()], await LinkedAsync(tabaco));
        var report = await tabaco.CallAsync("consultarElaboracionProductos", $"<deposito>{Deposit}</deposito><fecha>{Day()}</fecha>");
        Assert.Equal(cathes[1].ToString(), report.Descendants("cathe").Single().Value);
    }

    [Fact]
    public async Task A_denaturation_locks_its_CATHE_until_SEFI_resolves_it()
    {
        await using var tabaco = await StartAsync();
        var cathes = await RequestAsync(tabaco, 2, initial: "S");
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[0], Producer, 50));
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[1], Producer, 50));
        string Denature(int days, long cathe) =>
            $"<deposito>{Deposit}</deposito><fecha>{Day(days)}</fecha><motivo>Tabaco en mal estado</motivo><arrayCathes><cathe>{cathe}</cathe></arrayCathes>";

        Assert.Equal(["1700"], Errors(await tabaco.CallAsync("solicitarDesnaturalizacion", Denature(0, cathes[0]))));
        var early = await tabaco.CallAsync("solicitarDesnaturalizacion", Denature(5, cathes[1]));
        Assert.Equal("O", early.Element("resultado")!.Value);
        Assert.Equal("1701", early.Element("observaciones")!.Descendants("codigo").Single().Value);

        var answer = await tabaco.CallAsync("solicitarDesnaturalizacion", Denature(20, cathes[0]));
        Assert.Equal("A", answer.Element("resultado")!.Value);
        var id = answer.Element("idSolicitud")!.Value;
        Assert.Empty(await LinkedAsync(tabaco));
        Assert.Equal(["1703"], Errors(await tabaco.CallAsync("solicitarDesnaturalizacion", Denature(20, cathes[0]))));
        Assert.Equal("P", (await tabaco.CallAsync("consultarSolicitudDesnaturalizacion", $"<idSolicitud>{id}</idSolicitud>")).Element("resultadoSolicitud")!.Value);
        Assert.Equal(2, (await tabaco.CallAsync("consultarSolicDesnatPendientes")).Descendants("datosSolicitud").Count());

        tabaco.Sim.Clock.Advance(TimeSpan.FromDays(21));
        var later = await tabaco.AuthForAsync(Issuer);
        var resolved = await tabaco.CallAsync("consultarSolicitudDesnaturalizacion", $"<idSolicitud>{id}</idSolicitud>", later);
        Assert.Equal("A", resolved.Element("resultadoSolicitud")!.Value);
        Assert.Equal("S", resolved.Descendants("estado").Single().Value);
        Assert.Equal(2, (await tabaco.CallAsync("consultarSolicDesnatProcesadas", "", later)).Descendants("datosSolicitud").Count());
        Assert.Equal("O", (await tabaco.CallAsync("consultarSolicDesnatPendientes", "", later)).Element("resultado")!.Value);
        Assert.Equal(["2300"], Errors(await tabaco.CallAsync("consultarSolicitudDesnaturalizacion", "<idSolicitud>999</idSolicitud>", later)));
    }

    [Fact]
    public async Task A_change_of_holder_waits_for_the_buyer_and_then_moves_the_CATHE()
    {
        await using var tabaco = await StartAsync();
        var cathes = await RequestAsync(tabaco, 2, initial: "S");
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[0], Issuer, 50));
        await tabaco.CallAsync("vincularCathesTabacoElaborado", Link(cathes[1], Issuer, 50));
        var buyer = await tabaco.AuthForAsync(Producer);
        string Change(long cathe) =>
            $"<cuitComprador>{Producer}</cuitComprador><cuitVendedor>{Issuer}</cuitVendedor><fechaCambio>{Day()}</fechaCambio><modoFactura>E</modoFactura>" +
            $"<tipoComprobante>1</tipoComprobante><puntoVenta>1</puntoVenta><numeroComprobante>15</numeroComprobante>" +
            $"<importeNetoGravado>1000.00</importeNetoGravado><importeTotal>1210.00</importeTotal><arrayCathes><cathe>{cathe}</cathe></arrayCathes>";
        string Confirm(string id, string yes) => $"<idSolicitud>{id}</idSolicitud><confirma>{yes}</confirma>";

        var request = await tabaco.CallAsync("solicitarCambioTitularSinMovFisico", Change(cathes[0]));
        Assert.Equal("PC", request.Element("estado")!.Value);
        var id = request.Element("idSolicitud")!.Value;
        Assert.Equal(["1802"], Errors(await tabaco.CallAsync("solicitarCambioTitularSinMovFisico", Change(cathes[0]))));
        Assert.Single((await tabaco.CallAsync("consultarSolicCambioTitularPendientes", "", buyer)).Descendants("datosSolicitud"));

        Assert.Equal(["1902"], Errors(await tabaco.CallAsync("confirmarCambioTitularSinMovFisico", Confirm(id, "S"))));
        Assert.Equal("A", (await tabaco.CallAsync("confirmarCambioTitularSinMovFisico", Confirm(id, "S"), buyer)).Element("resultado")!.Value);
        Assert.Equal(["1901"], Errors(await tabaco.CallAsync("confirmarCambioTitularSinMovFisico", Confirm(id, "S"), buyer)));
        Assert.Equal("AP", (await tabaco.CallAsync("consultarSolicitudCambioTitular", $"<idSolicitud>{id}</idSolicitud>", buyer)).Element("estado")!.Value);
        Assert.Single((await tabaco.CallAsync("consultarSolicCambioTitularAprobadas")).Descendants("datosSolicitud"));
        Assert.Equal(["1803"], Errors(await tabaco.CallAsync("solicitarCambioTitularSinMovFisico", Change(cathes[0]))));

        var refused = (await tabaco.CallAsync("solicitarCambioTitularSinMovFisico", Change(cathes[1]))).Element("idSolicitud")!.Value;
        await tabaco.CallAsync("confirmarCambioTitularSinMovFisico", Confirm(refused, "N"), buyer);
        Assert.Equal("RC", (await tabaco.CallAsync("consultarSolicCambioTitularRechazadas")).Descendants("estado").Single().Value);
        Assert.Equal(["1900"], Errors(await tabaco.CallAsync("confirmarCambioTitularSinMovFisico", Confirm("999", "S"))));
    }

    private const string Dispatch = "<nroDespachoImp>99999ZZZZ999999E</nroDespachoImp><cuitDespachante>20111111112</cuitDespachante>";

    [Theory]
    // cantidad over the schema's maximum, on the first request for a deposit (inicial = S), which has no parameters to hold it.
    [InlineData("solicitarCathesTabacoElaborado", "<inicial>S</inicial><deposito>10</deposito><cantidad>1000000</cantidad>")]
    [InlineData("solicitarCathesTabacoElaborado", "<inicial>S</inicial><deposito>10</deposito><cantidad>9223372036854775807</cantidad>")]
    // cantBultos over it, whatever the rows ask for.
    [InlineData("solicitarCathesTabacoImportado", Dispatch + "<cantBultos>1000000</cantBultos><arrayCantSolicitadas><cantPorDeposito><deposito>10</deposito><cantidad>1</cantidad></cantPorDeposito></arrayCantSolicitadas>")]
    // a row over it, with a cantBultos that holds it.
    [InlineData("solicitarCathesTabacoImportado", Dispatch + "<cantBultos>9223372036854775807</cantBultos><arrayCantSolicitadas><cantPorDeposito><deposito>10</deposito><cantidad>9223372036854775807</cantidad></cantPorDeposito></arrayCantSolicitadas>")]
    // rows that ask for more in all, though a negative one brings the sum rule 1302 compares under cantBultos.
    [InlineData("solicitarCathesTabacoImportado", Dispatch + "<cantBultos>999999</cantBultos><arrayCantSolicitadas>" +
        "<cantPorDeposito><deposito>10</deposito><cantidad>999999</cantidad></cantPorDeposito>" +
        "<cantPorDeposito><deposito>11</deposito><cantidad>999999</cantidad></cantPorDeposito>" +
        "<cantPorDeposito><deposito>12</deposito><cantidad>-999999</cantidad></cantPorDeposito></arrayCantSolicitadas>")]
    public async Task A_request_for_more_than_999999_CATHE_is_refused_as_a_fault_instead_of_issuing_them_without_end(string operation, string inner)
    {
        await using var tabaco = await StartAsync();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var (status, body) = await tabaco.PostAsync(operation, inner, limit.Token);

        Assert.Equal(500, status);
        Assert.Contains("cvc-maxInclusive-valid", body);
        Assert.Empty((await tabaco.CallAsync("consultarCathesSolicitados")).Descendants("datosCathe"));
    }

    [Fact]
    public async Task The_tables_answer_the_rows_the_manual_shows()
    {
        await using var tabaco = await StartAsync();

        var states = await tabaco.CallAsync("consultarTiposEstadoSolicitudCambioTitular");
        Assert.Contains(states.Descendants("codigo"), c => c.Value == "PC");
        var results = await tabaco.CallAsync("consultarTiposResultadoDesnaturalizacion");
        Assert.Equal(["P", "A", "B", "C"], results.Descendants("codigo").Select(c => c.Value));
    }
}
