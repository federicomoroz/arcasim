using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Remitos;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>WSAGR over the AGR registry: ratings, the consults they leave, and the general and per-CUIT errors, every answer valid for the WSDL.</summary>
public class WsagrRulesTests
{
    private const string Service = "wsagr";
    private const long Unknown = 20111111120;

    [Fact]
    public async Task Consulta_rates_each_CUIT_and_the_history_and_other_consultants_read_it_back()
    {
        await using var sim = await StartAsync();
        await Registry(sim).RateAsync(Holder, "3", "10/2026", "10");
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var other = await ServiceClient.LoginAsync(sim, Service, Depositary);

        var answer = await Call(consultant, "Consulta", "<w:Periodo>10/2026</w:Periodo>" + Cuits(Holder, Receiver, Unknown));
        var details = answer.Descendants(Ns + "DetalleCuits").ToList();

        Assert.Equal(3, details.Count);
        Assert.Equal("3", details[0].Element(Ns + "Rsp")!.Value);
        Assert.Equal("10", details[0].Element(Ns + "CodObs")!.Value);
        Assert.Matches("^[1-9]\\d{20}$", details[0].Element(Ns + "RTran")!.Value);
        Assert.Equal("01/10/2026", details[0].Element(Ns + "FTran")!.Value);
        Assert.Equal("1", details[1].Element(Ns + "Rsp")!.Value);
        Assert.Equal("108", details[2].Element(Ns + "Err")!.Element(Ns + "Code")!.Value);
        Assert.Equal("El CUIT es invalido o inexistente", details[2].Element(Ns + "Err")!.Element(Ns + "Msg")!.Value);

        var history = await Call(consultant, "ConsultaHistorica", Cuits(Holder));
        Assert.Equal(details[0].Element(Ns + "RTran")!.Value, history.Descendants(Ns + "RTran").Single().Value);

        var byOthers = await Call(other, "ConsultaCondRet", "<w:Periodo>10/2026</w:Periodo>" + Cuits(Holder, Receiver));
        Assert.Equal(["3", "1"], byOthers.Descendants(Ns + "Rsp").Select(e => e.Value));
        Assert.False(byOthers.Descendants(Ns + "RTran").Any());

        var nothing = await Call(other, "ConsultaHistorica", Cuits(Holder));
        Assert.Empty(nothing.Descendants(Ns + "DetalleCuits"));
        Assert.False(nothing.Descendants(Ns + "Err").Any());
    }

    [Fact]
    public async Task ConsultaRectificada_shows_the_CUITs_whose_rating_changed_and_103_when_none_did()
    {
        await using var sim = await StartAsync();
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);
        await Call(consultant, "Consulta", "<w:Periodo>10/2026</w:Periodo>" + Cuits(Holder, Receiver));

        var unchanged = await Call(consultant, "ConsultaRectificada", "<w:Periodo>10/2026</w:Periodo>");
        await Registry(sim).RateAsync(Holder, "2", "10/2026");
        var changed = await Call(consultant, "ConsultaRectificada", "<w:Periodo>10/2026</w:Periodo>");

        Assert.Equal("103", unchanged.Value("Code"));
        Assert.Equal("No se encontraron registros para dicha consulta", unchanged.Value("Msg"));
        var detail = changed.Descendants(Ns + "DetalleCuits").Single();
        Assert.Equal(Holder.ToString(), detail.Element(Ns + "Cuit")!.Value);
        Assert.Equal("2", detail.Element(Ns + "Rsp")!.Value);
    }

    [Theory]
    [InlineData("<w:Periodo>202610</w:Periodo><w:Cuit>20222222223</w:Cuit>", "113")]
    [InlineData("<w:Periodo>12/2026</w:Periodo><w:Cuit>20222222223</w:Cuit>", "107")]
    [InlineData("<w:Periodo>10/2026</w:Periodo>", "111")]
    [InlineData("<w:Periodo>10/2026</w:Periodo><w:Cuit>20222222223</w:Cuit><w:Cuit>20222222223</w:Cuit>", "104")]
    [InlineData("<w:Periodo>10/2026</w:Periodo><w:Cuit>20222222223</w:Cuit><w:Cuit>30000000007</w:Cuit><w:Cuit>27333333339</w:Cuit>", "102")]
    public async Task Consulta_refuses_a_bad_request_with_one_general_error(string inner, string code)
    {
        await using var sim = await StartAsync();
        await Registry(sim).SetLimitAsync(2);
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var answer = await Call(consultant, "Consulta", inner);

        var respuesta = answer.Element(Ns + "Respuesta")!;
        Assert.Equal(code, respuesta.Element(Ns + "Err")!.Element(Ns + "Code")!.Value);
        Assert.Null(respuesta.Element(Ns + "Det"));
    }

    [Fact]
    public async Task ConsultaCondRet_requires_a_period_with_118()
    {
        await using var sim = await StartAsync();
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var answer = await Call(consultant, "ConsultaCondRet", Cuits(Holder));

        Assert.Equal("118", answer.Value("Code"));
        Assert.Equal("Es obligatorio el ingreso de un periodo (formato MM/AAAA)", answer.Value("Msg"));
    }

    [Fact]
    public async Task The_parameter_consults_serve_the_manuals_codes_and_the_limit()
    {
        await using var sim = await StartAsync();
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var responses = await Call(consultant, "ConsultaCodResp", "");
        var observations = await Call(consultant, "ConsultaObs", "");
        var limit = await Call(consultant, "ConsultaCantCuit", "");

        Assert.Equal(["0", "1", "2", "3", "4", "5"], responses.Descendants(Ns + "WsResp").Select(e => e.Element(Ns + "Code")!.Value));
        Assert.Equal("CREDITO FISCAL NO COMPUTABLE", responses.Descendants(Ns + "Msg").ElementAt(3).Value);
        Assert.Equal("10", observations.Value("Code"));
        Assert.Equal("100", limit.Value("Cantidad"));
    }

    /// <summary>
    /// The engine refuses the ticket before the rules run. It writes the 501 in
    /// the first error block of the schema (Det/DetalleCuits/Err), where the
    /// capture shows Respuesta/Err: an engine change, reported, not this test's.
    /// </summary>
    [Fact]
    public async Task A_forged_ticket_is_refused_inside_Respuesta_with_501()
    {
        await using var sim = await StartAsync();
        var consultant = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var (status, body) = await consultant.PostAsync("Consulta",
            $"<w:Auth><w:Token>abc</w:Token><w:Sign>abc</w:Sign><w:Cuit>{Issuer}</w:Cuit></w:Auth><w:Periodo>10/2026</w:Periodo>", "Consulta", "w");

        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body).Descendants(Ns + "ConsultaResponse").Single();
        ServiceClient.Validate(Service, answer);
        Assert.Equal("501", answer.Element(Ns + "Respuesta")!.Descendants(Ns + "Code").Single().Value);
    }

    private static readonly XNamespace Ns = "http://servicios1.afip.gob.ar/wsagr/";

    private static IDocumentStore Registry(ArcaSimHarness sim) => sim.Services.GetRequiredService<IDocumentStore>();

    private static Task<XElement> Call(ServiceClient client, string operation, string inner) =>
        client.CallAsync(operation,
            $"<w:Auth><w:Token>{client.Token}</w:Token><w:Sign>{client.Sign}</w:Sign><w:Cuit>{client.Cuit}</w:Cuit></w:Auth>" + inner, operation, "w");

    private static string Cuits(params long[] cuits) => string.Concat(cuits.Select(c => $"<w:Cuit>{c}</w:Cuit>"));
}
