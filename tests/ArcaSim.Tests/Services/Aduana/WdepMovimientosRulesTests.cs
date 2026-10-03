namespace ArcaSim.Tests.Services.Aduana;

/// <summary>wdepMovimientos: cargo enters a depósito, leaves it with a salida, and a repeated transaction answers the same.</summary>
public class WdepMovimientosRulesTests
{
    private static Task<AduanaService> DepAsync(AduanaKit kit) =>
        kit.ServiceAsync("wdepMovimientos", (t, c) => AduanaKit.ArgAutentica(t, c, "DEPO"));

    private const string Place = "<Aduana>001</Aduana><LugarOperativo>DEP01</LugarOperativo>";

    private static string Entry(long transaction, string close = "N") =>
        $"<argwdepIngresos>{Place}<NroTransaccion>{transaction}</NroTransaccion><Carga><TituloTransporte>HBL0001</TituloTransporte>" +
        $"<CierreIngreso>{close}</CierreIngreso><CantTotal>30</CantTotal><PesoTotal>1200</PesoTotal><LineasMercaderia>" +
        "<IZPLineaMercaderia><NroLinea>1</NroLinea><CantidadIngresada>20</CantidadIngresada><PesoIngresado>800</PesoIngresado><CantidadAveriada>0</CantidadAveriada></IZPLineaMercaderia>" +
        "<IZPLineaMercaderia><NroLinea>2</NroLinea><CantidadIngresada>10</CantidadIngresada><PesoIngresado>400</PesoIngresado><CantidadAveriada>0</CantidadAveriada></IZPLineaMercaderia>" +
        "</LineasMercaderia><Contenedores><IZPContenedor><IdContenedor>MSCU1234567</IdContenedor></IZPContenedor></Contenedores></Carga></argwdepIngresos>";

    private static string Exit(long transaction, int quantity, string container = "MSCU1234567") =>
        $"<argwdepSalidas>{Place}<NroTransaccion>{transaction}</NroTransaccion><Carga><Salida><IdDeclaracion>26001IC04000001A</IdDeclaracion>" +
        "<TipNumSal><TipoSalida>DEST</TipoSalida></TipNumSal><SalidaAnulada /><ATA /><Transportista /><Transporte /><Conductor /><Portador /></Salida>" +
        "<Titulos><SZPTitulo><IdTitulo>HBL0001</IdTitulo><LineasMercaderia><SZPLineaMercaderia><NroLinea>1</NroLinea>" +
        $"<CantidadEgresada>{quantity}</CantidadEgresada><PesoEgresado>100</PesoEgresado></SZPLineaMercaderia></LineasMercaderia>" +
        (container == "" ? "" : $"<Contenedores><SZPContenedor><IdContenedor>{container}</IdContenedor><PesoEgresado>800</PesoEgresado></SZPContenedor></Contenedores>") +
        "</SZPTitulo></Titulos></Carga></argwdepSalidas>";

    [Fact]
    public async Task Cargo_enters_is_found_by_its_container_and_leaves_with_a_salida()
    {
        await using var kit = await AduanaKit.StartAsync();
        var dep = await DepAsync(kit);

        var entered = await dep.CallAsync("WdepIngresos", Entry(1001));
        var titles = await dep.CallAsync("WdepListaTitulosPorContenedor",
            $"<argwdepListaTitulosPorContenedor>{Place}<IdContenedor>MSCU1234567</IdContenedor></argwdepListaTitulosPorContenedor>");
        var left = await dep.CallAsync("WdepSalidas", Exit(1002, 15));
        var again = await dep.CallAsync("WdepSalidas", Exit(1002, 1, container: ""));

        Assert.Equal("0", entered.Code());
        Assert.Equal("Proceso OK", entered.V("DesError"));
        Assert.Equal("HBL0001", titles.V("IdTitulo"));
        Assert.Equal("0", left.Code());
        Assert.Equal("26001SZP000001", left.V("NroSalida"));
        Assert.Equal(left.V("NroSalida"), again.V("NroSalida"));
    }

    [Fact]
    public async Task What_left_or_was_never_there_cannot_leave_again()
    {
        await using var kit = await AduanaKit.StartAsync();
        var dep = await DepAsync(kit);
        await dep.CallAsync("WdepIngresos", Entry(2001));
        await dep.CallAsync("WdepSalidas", Exit(2002, 15));

        var tooMuch = await dep.CallAsync("WdepSalidas", Exit(2003, 6, container: ""));
        var containerTwice = await dep.CallAsync("WdepSalidas", Exit(2004, 1));
        var stillThere = await dep.CallAsync("WdepSalidas", Exit(2005, 5, container: ""));

        Assert.Equal("10034", tooMuch.Code());
        Assert.Equal("Cantidad superior a la disponible", tooMuch.V("DesError"));
        Assert.Equal("10973", containerTwice.Code());
        Assert.Equal("MSCU1234567", containerTwice.V("DescAdicErr"));
        Assert.Equal("0", stillThere.Code());
    }

    [Fact]
    public async Task A_closed_entry_takes_no_more_and_a_repeated_transaction_keeps_its_first_answer()
    {
        await using var kit = await AduanaKit.StartAsync();
        var dep = await DepAsync(kit);
        await dep.CallAsync("WdepIngresos", Entry(3001, close: "S"));

        var closed = await dep.CallAsync("WdepIngresos", Entry(3002));
        var replay = await dep.CallAsync("WdepIngresos", Entry(3001));
        var refusedReplay = await dep.CallAsync("WdepIngresos", Entry(3002, close: "N"));

        Assert.Equal("10142", closed.Code());
        Assert.Equal("0", replay.Code());
        Assert.Equal("10142", refusedReplay.Code());
    }
}
