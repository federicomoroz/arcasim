using System.Globalization;
using static ArcaSim.Tests.Services.Liquidaciones.LiquidacionesSim;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>The tests that change the culture of every thread run alone.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureCollection
{
    public const string Name = "Culture";
}

/// <summary>
/// A server whose culture writes years in another calendar (the Thai one counts
/// 2026 as 2569) answers the same as one in the invariant culture: what the
/// services compare or store is written the invariant way.
/// </summary>
[Collection(CultureCollection.Name)]
public class LiquidacionesCultureTests
{
    [Fact]
    public async Task A_dairy_liquidation_is_checked_against_its_period_in_a_server_with_another_calendar()
    {
        await using var lum = await LiquidacionesSim.StartAsync("wslum", "wslum-homologacion.wsdl");
        using var thai = new ThaiCulture();

        var issued = await lum.CallAsync("generarLiquidacion", LumRulesTests.Liquidation(1));

        Assert.Empty(Errors(issued));
        Assert.Equal("2026/10", issued.Element("liquidacion")!.Element("encabezado")!.Element("periodo")!.Value);
    }

    [Fact]
    public async Task A_report_of_elaboration_is_found_under_the_key_it_was_stored_with_in_a_server_with_another_calendar()
    {
        await using var tabaco = await LiquidacionesSim.StartAsync("wstabaco", "wstabaco-homologacion.wsdl", "cuitRepresentada");
        var requested = await tabaco.CallAsync("solicitarCathesTabacoElaborado", "<inicial>S</inicial><deposito>10</deposito><cantidad>1</cantidad>");
        var cathe = long.Parse(requested.Element("arrayCathes")!.Element("cathe")!.Value, CultureInfo.InvariantCulture);
        await tabaco.CallAsync("vincularCathesTabacoElaborado", TabacoRulesTests.Link(cathe, Producer, 50));
        await tabaco.CallAsync("informarElaboracionProductos",
            "<deposito>10</deposito><fechaElaboracion>2026-10-01</fechaElaboracion><rectificativa>0</rectificativa>" +
            $"<arrayCathes><datosCatheProducto><cathe>{cathe}</cathe><tipoProducto>1</tipoProducto></datosCatheProducto></arrayCathes>");
        using var thai = new ThaiCulture();

        var read = await tabaco.CallAsync("consultarElaboracionProductos", "<deposito>10</deposito><fecha>2026-10-01</fecha>");

        Assert.Equal("2026-10-01", read.Element("fechaElaboracion")?.Value);
    }

    /// <summary>The Thai culture, with its Buddhist calendar, for the thread and for every thread that has none of its own.</summary>
    private sealed class ThaiCulture : IDisposable
    {
        private readonly CultureInfo _thread = CultureInfo.CurrentCulture;
        private readonly CultureInfo? _default = CultureInfo.DefaultThreadCurrentCulture;

        public ThaiCulture()
        {
            var thai = new CultureInfo("th-TH");
            Assert.IsType<ThaiBuddhistCalendar>(thai.DateTimeFormat.Calendar);
            CultureInfo.DefaultThreadCurrentCulture = thai;
            CultureInfo.CurrentCulture = thai;
        }

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentCulture = _default;
            CultureInfo.CurrentCulture = _thread;
        }
    }
}
