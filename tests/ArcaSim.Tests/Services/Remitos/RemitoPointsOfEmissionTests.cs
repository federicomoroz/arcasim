using ArcaSim.Domain;
using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>The points of emission the three remito services list: the issuer's own, as many as their WSDLs let them write.</summary>
public class RemitoPointsOfEmissionTests
{
    /// <summary>
    /// A point of emission goes up to 99999 in the three WSDLs, but carne and
    /// harina write it in a codigo typed xsd:short, so one above 32767 cannot
    /// be listed there; azúcar's codigo is xsd:long.
    /// </summary>
    [Theory]
    [InlineData("wsremcarne", new[] { "1" })]
    [InlineData("wsremharina", new[] { "1" })]
    [InlineData("wsremazucar", new[] { "1", "40000", "99999" })]
    public async Task The_points_listed_are_the_ones_the_codigo_of_the_service_can_hold(string service, string[] expected)
    {
        await using var sim = await StartAsync();
        await sim.PutTaxpayerAsync(Issuer, "Molino de Prueba SA", VatCondition.ResponsableInscripto,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(40000, PointOfSaleKind.WebServiceCae),
            new PointOfSale(99999, PointOfSaleKind.WebServiceCae));
        var issuer = await ServiceClient.LoginAsync(sim, service, Issuer);

        var points = await issuer.CallAsync("consultarPuntosEmision", issuer.Auth);

        Assert.Equal(expected, points.Descendants("codigo").Select(e => e.Value));
    }
}
