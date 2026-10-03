namespace ArcaSim.Tests.Services.Aduana;

/// <summary>wgesTabRef serves the tables the other customs services take their codes from.</summary>
public class WgesTabRefRulesTests
{
    private static Task<AduanaService> TabRefAsync(AduanaKit kit) =>
        kit.ServiceAsync("wgesTabRef", (t, cuit) => AduanaKit.Flat("Autentica", t, cuit, "IMEX"));

    [Fact]
    public async Task Lists_the_tables_then_a_tables_rows_and_its_last_update()
    {
        await using var kit = await AduanaKit.StartAsync();
        var tabref = await TabRefAsync(kit);

        var tables = await tabref.CallAsync("ListaTablasReferencia", "");
        var rows = await tabref.CallAsync("ListaDescripcion", "<IdReferencia>DFCOD_DESC</IdReferencia>");
        var updated = await tabref.CallAsync("ConsultarFechaUltAct", "<IdReferencia>DFCOD_DESC</IdReferencia>");

        Assert.Equal("0", tables.Code());
        Assert.Contains(tables.All("TablaReferencia"), t => t.V("IdTabRef") == "ESTCEL_DESC" && t.V("WebMethod") == "ListaDescripcion");
        Assert.Equal("DFCOD_DESC", rows.V("IdReferencia"));
        Assert.Equal(["000", "001", "002", "003", "004", "100", "101"], rows.All("Codigo").Select(c => c.Value));
        Assert.Equal("0", updated.Code());
        Assert.StartsWith("2026-01-02T00:00:00", updated.V("Fecha"));
    }

    [Fact]
    public async Task Filters_the_tables_by_the_service_that_reads_them()
    {
        await using var kit = await AduanaKit.StartAsync();
        var tabref = await TabRefAsync(kit);

        var tables = await tabref.CallAsync("ListaTablasReferenciaServicio", "<IdServicio>wgesprecintosdepfis</IdServicio>");

        Assert.Equal(["ESTCEL_DESC", "ESTMON_DESC"], tables.All("IdTabRef").Select(t => t.Value));
    }

    [Fact]
    public async Task A_table_it_does_not_have_answers_no_data()
    {
        await using var kit = await AduanaKit.StartAsync();
        var tabref = await TabRefAsync(kit);

        var rows = await tabref.CallAsync("ListaVigencias", "<IdReferencia>NO_EXISTE</IdReferencia>");

        Assert.Equal("10121", rows.Code());
        Assert.Equal("No hay datos para los criterios ingresados", rows.V("InfoAdicional"));
        Assert.Equal("NO_EXISTE", rows.V("IdReferencia"));
        Assert.Empty(rows.All("Vigencia"));
    }
}
