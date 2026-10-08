using System.Xml.Linq;
using ArcaSim.Tests.Support;
using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>
/// The lists of the three services give 2000 remitos a page. A page counted past
/// the last one is empty, whatever the size of its number: the offset of a page
/// from 1,073,743 on does not fit an int, and a number that does not fit one is
/// not the small number its low bits make.
/// </summary>
public class RemitoPagingTests
{
    public static TheoryData<string, long> PastTheEnd()
    {
        var data = new TheoryData<string, long>();
        foreach (var service in new[] { "wsremcarne", "wsremharina", "wsremazucar" })
            foreach (var page in new long[] { 2, 1_073_743, int.MaxValue, 4_294_967_297 })
                data.Add(service, page);
        return data;
    }

    [Theory]
    [MemberData(nameof(PastTheEnd))]
    public async Task A_page_past_the_last_one_is_empty_instead_of_the_first(string service, long page)
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, service, Issuer);
        await issuer.CallAsync("generarRemito", service switch
        {
            "wsremcarne" => CarneRemito(issuer, 1, Issuer),
            "wsremharina" => HarinaRemito(issuer, 1, Issuer),
            _ => AzucarRemito(issuer, 1, Issuer),
        });

        Assert.Equal(1, Count(await ListAsync(issuer, service, 1)));
        Assert.Equal(0, Count(await ListAsync(issuer, service, page)));
    }

    /// <summary>The issuer's list, asked for a page; the answer as it comes, since a page like these is not one the WSDL allows (xsd:short).</summary>
    private static async Task<XElement> ListAsync(ServiceClient issuer, string service, long page)
    {
        var paging = service switch
        {
            "wsremcarne" => $"<puntoEmision>9000</puntoEmision><nroPagina>{page}</nroPagina>",
            "wsremharina" => $"<ptoEmision>1</ptoEmision><nroPagina>{page}</nroPagina>",
            _ => $"<numeroPagina>{page}</numeroPagina>",
        };
        var (status, body) = await issuer.PostAsync("consultarRemitosEmisor",
            issuer.Auth + "<fechaDesde>2026-10-01</fechaDesde><fechaHasta>2026-10-01</fechaHasta>" + paging);
        Assert.True(status == 200, body);
        return Soap.Body(body);
    }

    private static int Count(XElement answer) =>
        answer.Descendants().Count(e => e.Name.LocalName is "remito" or "remitosConsulta" or "infoRemito");
}
