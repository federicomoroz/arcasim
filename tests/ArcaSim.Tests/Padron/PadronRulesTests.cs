using System.Net.Http.Json;
using System.Xml.Linq;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Padron;

/// <summary>A4, A10 and A100 over the same taxpayers as the constancia and WSFEv1, and valid for their WSDLs.</summary>
public class PadronRulesTests
{
    private const long Caller = ArcaSimHarness.Issuer;

    [Fact]
    public async Task A4_shows_a_company_with_its_taxes_activity_and_legal_form()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(30000000007, "Distribuidora del Plata S.A.", VatCondition.ResponsableInscripto);

        var persona = await GetPersonaAsync(sim, "a4", "ws_sr_padron_a4", "personaServiceA4", 30000000007);

        Assert.Equal("DISTRIBUIDORA DEL PLATA S.A.", persona.Element("razonSocial")!.Value);
        Assert.Equal("SOC. ANONIMA", persona.Element("formaJuridica")!.Value);
        Assert.Equal(["10", "30"], persona.Elements("impuesto").Select(i => i.Element("idImpuesto")!.Value));
        Assert.NotNull(persona.Element("actividad"));
        Assert.Null(persona.Element("categoria"));
    }

    [Fact]
    public async Task A4_shows_a_monotributista_with_its_category_and_document()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(20222222223, "Ana Gomez", VatCondition.Monotributo);

        var persona = await GetPersonaAsync(sim, "a4", "ws_sr_padron_a4", "personaServiceA4", 20222222223);

        Assert.Equal("20", persona.Element("categoria")!.Element("idImpuesto")!.Value);
        Assert.Equal("MONOTRIBUTO", persona.Element("impuesto")!.Element("descripcionImpuesto")!.Value);
        Assert.Equal("22222222", persona.Element("numeroDocumento")!.Value);
        Assert.Equal("GOMEZ", persona.Element("apellido")!.Value);
    }

    [Fact]
    public async Task A10_shows_the_short_view_with_the_address_set_from_the_admin_API()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        (await sim.Http.PutAsJsonAsync("/arcasim/api/taxpayers/30000000007", new
        {
            name = "Distribuidora del Plata S.A.",
            vatCondition = "ResponsableInscripto",
            profile = new { address = new { street = "RUTA 22 KM 1200", locality = "NEUQUEN", postalCode = "8300", provinceId = 20, province = "NEUQUEN" } },
        })).EnsureSuccessStatusCode();

        var persona = await GetPersonaAsync(sim, "a10", "ws_sr_padron_a10", "personaServiceA10", 30000000007);

        Assert.Equal("RUTA 22 KM 1200", persona.Element("domicilio")!.Element("direccion")!.Value);
        Assert.Equal("20", persona.Element("domicilio")!.Element("idProvincia")!.Value);
        Assert.NotNull(persona.Element("idActividadPrincipal"));
    }

    [Fact]
    public async Task A4_and_A10_report_a_CUIT_nobody_has_with_A13s_fault()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var auth = await AuthAsync(sim, "ws_sr_padron_a4");

        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost/sr-padron/webservices/personaServiceA4"),
            Envelope("a4", "getPersona", auth + "<idPersona>27000000006</idPersona>"), "");

        Assert.Equal(500, status);
        Assert.Contains("<faultstring>La Clave (CUIT/CUIL) consultada es inexistente</faultstring>", body);
        Assert.Contains("SRValidationException", body);
    }

    [Fact]
    public async Task A100_serves_the_provinces_the_other_services_name()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var auth = await AuthAsync(sim, "ws_sr_padron_a100");

        var (status, body) = await sim.PostSoapAsync(new Uri("http://localhost/sr-parametros/webservices/parameterServiceA100"),
            Envelope("a100", "getParameterCollectionByName", auth + "<collectionName>SUPA.E_PROVINCIA</collectionName>"), "");
        var rows = XDocument.Parse(body).Descendants("parameterList").ToList();

        Assert.Equal(200, status);
        Validate("ws_sr_padron_a100-homologacion.wsdl", body);
        Assert.Equal(24, rows.Count);
        Assert.Contains(rows, r => r.Element("id")!.Value == "0" && r.Element("description")!.Value == "CIUDAD AUTONOMA BUENOS AIRES");
    }

    private static async Task<XElement> GetPersonaAsync(ArcaSimHarness sim, string prefix, string service, string path, long id)
    {
        var auth = await AuthAsync(sim, service);
        var (status, body) = await sim.PostSoapAsync(new Uri($"http://localhost/sr-padron/webservices/{path}"),
            Envelope(prefix, "getPersona", auth + $"<idPersona>{id}</idPersona>"), "");
        Assert.True(status == 200, body);
        Validate($"{service}-homologacion.wsdl", body);
        return XDocument.Parse(body).Descendants("persona").Single();
    }

    /// <summary>The answer is valid for the WSDL ARCA publishes: what a generated client deserializes.</summary>
    private static void Validate(string wsdl, string body) => Xsd.AssertValid(Soap.Body(body), Contracts.Of(wsdl));

    private static async Task<string> AuthAsync(ArcaSimHarness sim, string service) =>
        Login.Credentials(await sim.TicketAsync(Caller, service), Caller);

    /// <summary>A100 runs in another application (sr-parametros), with its own namespace.</summary>
    private static string Envelope(string prefix, string operation, string inner) =>
        Soap.Envelope($"<{prefix}:{operation}>{inner}</{prefix}:{operation}>",
            (prefix, $"http://{prefix}.soap.ws.server.{(prefix == "a100" ? "pucParam" : "puc")}.sr/"));
}
