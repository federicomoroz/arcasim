namespace ArcaSim.Tests.Services.Aduana;

/// <summary>WutiGOPDeclaraciones: a depositario takes its declarations from the queues and reads each one.</summary>
public class WutiGopRulesTests
{
    private const string Place = "<Aduana>001</Aduana><LugarOperativo>DEP01</LugarOperativo>";

    private static Task<AduanaService> GopAsync(AduanaKit kit) =>
        kit.ServiceAsync("WutiGOPDeclaraciones", (t, c) => AduanaKit.ArgAutentica(t, c, "DEPO"));

    [Fact]
    public async Task A_declaration_leaves_the_queue_once_its_caratula_is_read()
    {
        await using var kit = await AduanaKit.StartAsync();
        var gop = await GopAsync(kit);

        var pending = await gop.CallAsync("PndListaGOPDetallada", $"<argPndListaGOPDetallada>{Place}</argPndListaGOPDetallada>");
        var ids = pending.All("IdDecla").Select(i => i.Value).ToList();
        var caratula = await gop.CallAsync("ListaGOPCaratDeta", $"<argListaGOPCaratDeta>{Place}<IdDecla>{ids[0]}</IdDecla></argListaGOPCaratDeta>");
        var after = await gop.CallAsync("PndListaGOPDetallada", $"<argPndListaGOPDetallada>{Place}</argPndListaGOPDetallada>");

        Assert.Equal("0", pending.Code());
        Assert.Equal(2, ids.Count);
        Assert.Equal("OK Procesado", caratula.V("DesError"));
        Assert.Equal(ids[0], caratula.V("IdDecla"));
        Assert.Equal("001", caratula.V("CodAduReg"));
        Assert.Equal("30000000007", caratula.V("CuitImpoExpo"));
        Assert.Equal([ids[1]], after.All("IdDecla").Select(i => i.Value));
    }

    [Fact]
    public async Task Items_come_by_lote_until_the_last_and_states_leave_their_own_queue()
    {
        await using var kit = await AduanaKit.StartAsync();
        var gop = await GopAsync(kit);
        var id = (await gop.CallAsync("PndListaGOPEstados", $"<argPndListaGOPEstados>{Place}</argPndListaGOPEstados>")).All("IdDecla").First().Value;
        string Lote(int n) => $"<argListaGOPItemsDeta>{Place}<NroLote>{n}</NroLote><IdDecla>{id}</IdDecla></argListaGOPItemsDeta>";

        var first = await gop.CallAsync("ListaGOPItemsDeta", Lote(1));
        var second = await gop.CallAsync("ListaGOPItemsDeta", Lote(2));
        var beyond = await gop.CallAsync("ListaGOPItemsDeta", Lote(3));
        var states = await gop.CallAsync("ListaGOPEstados", $"<argListaGOPEstados>{Place}<IdDecla>{id}</IdDecla></argListaGOPEstados>");
        var stillPending = await gop.CallAsync("PndListaGOPEstados", $"<argPndListaGOPEstados>{Place}</argPndListaGOPEstados>");

        Assert.Equal(["1", "2"], first.All("NroItem").Select(i => i.Value));
        Assert.Equal("N", first.V("IndUltLote"));
        Assert.Equal(["3"], second.All("NroItem").Select(i => i.Value));
        Assert.Equal("S", second.V("IndUltLote"));
        Assert.Equal("30286", beyond.Code());
        Assert.Equal("OFIC", states.V("CodEstDecla"));
        Assert.DoesNotContain(stillPending.All("IdDecla"), e => e.Value == id);
    }

    [Fact]
    public async Task An_unknown_declaration_or_one_with_no_blocks_has_no_data()
    {
        await using var kit = await AduanaKit.StartAsync();
        var gop = await GopAsync(kit);
        var id = (await gop.CallAsync("PndListaGOPDetallada", $"<argPndListaGOPDetallada>{Place}</argPndListaGOPDetallada>")).All("IdDecla").First().Value;

        var unknown = await gop.CallAsync("ListaGOPLiquiDeta", $"<argListaGOPLiquiDeta>{Place}<IdDecla>26001IC04999999Z</IdDecla></argListaGOPLiquiDeta>");
        var blocks = await gop.CallAsync("ListaGOPBloqueos", $"<argListaBloqueos>{Place}<IdDecla>{id}</IdDecla></argListaBloqueos>");
        var liquidacion = await gop.CallAsync("ListaGOPLiquiDeta", $"<argListaGOPLiquiDeta>{Place}<IdDecla>{id}</IdDecla></argListaGOPLiquiDeta>");

        Assert.Equal("30286", unknown.Code());
        Assert.Equal("No hay datos para los criterios ingresados", unknown.V("DesError"));
        Assert.Equal("30286", blocks.Code());
        Assert.Equal("4200", liquidacion.V("MontoPagar"));
    }
}
