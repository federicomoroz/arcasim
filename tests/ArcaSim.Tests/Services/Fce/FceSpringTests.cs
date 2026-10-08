using ArcaSim.Application.Services.Fce;
using static ArcaSim.Tests.Services.Fce.FceWorld;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>What wsfecredagente, wsfecredsca and wsfecred share: who may call the Spring services, and the pages of a query.</summary>
public class FceSpringTests
{
    /// <summary>A CUIT with its check digit that nobody registered in ArcaSim.</summary>
    private const long Stranger = 30500010912;

    [Fact]
    public async Task A_caller_unknown_to_ArcaSim_gets_4009_in_errores_in_the_shape_of_each_operation()
    {
        await using var world = await StartAsync();
        await world.AddStrangerAsync(Stranger);
        const string range = "<nroPagina>1</nroPagina><filtroFechas><tipo>Disponible</tipo><desde>2026-10-01</desde><hasta>2026-12-31</hasta></filtroFechas>";

        var accounts = await world.AgenteAsync("consultarCuentasAgente", range, Stranger);
        var opened = await world.AgenteAsync("altaCuentasAgente",
            "<cuentas><cuenta><cuitTitular>20111111112</cuitTitular><cuentaId>0001234</cuentaId></cuenta></cuentas>", Stranger);
        var reasons = await world.AgenteAsync("obtenerMotivosRechazo", "", Stranger);
        var invoices = await world.ScaAsync("consultarFacturasAceptadas", range, Stranger);
        var confirmed = await world.ScaAsync("confirmarRecepcionFacturas", $"<facturas><factura>{IdFactura(1)}</factura></facturas>", Stranger);

        foreach (var answer in new[] { accounts, opened, reasons, invoices, confirmed })
            Assert.Equal(["4009"], Codes(answer.Element("errores")));
        Assert.Empty(accounts.Element("cuentasAgente")!.Elements());
        Assert.Equal("0", accounts.Element("nroPagina")!.Value);
        Assert.Equal("N", accounts.Element("hayMas")!.Value);
        Assert.Empty(invoices.Element("facturas")!.Elements());
        Assert.Empty(opened.Element("resultados")!.Elements());
        Assert.Empty(confirmed.Element("resultados")!.Elements());
    }

    [Theory]
    [InlineData(1, 1, 100, true)]
    [InlineData(2, 101, 200, true)]
    [InlineData(3, 201, 250, false)]
    [InlineData(4, 0, 0, false)]
    public void A_page_holds_a_hundred_items_and_says_whether_more_follow(long page, int first, int last, bool more)
    {
        var all = Enumerable.Range(1, 250).ToList();

        var (items, hasMore) = FceXml.Page(all, page);

        Assert.Equal(first == 0 ? [] : Enumerable.Range(first, last - first + 1), items);
        Assert.Equal(more, hasMore);
    }

    [Theory]
    [InlineData(30_000_000)]
    [InlineData(int.MaxValue)]
    [InlineData(long.MaxValue / 50)]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_page_number_no_list_reaches_is_an_empty_page_whatever_its_size(long page)
    {
        var (items, more) = FceXml.Page(Enumerable.Range(1, 250).ToList(), page);

        Assert.Empty(items);
        Assert.False(more);
    }
}
