using System.Net.Http.Json;
using System.Xml.Linq;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Padron;

/// <summary>The padrón: the constancia de inscripción (A5) and A13, over the same taxpayers WSFEv1 invoices.</summary>
public class PadronTests
{
    private const long Caller = ArcaSimHarness.Issuer;
    private static readonly Uri A5 = new("http://localhost/sr-padron/webservices/personaServiceA5");
    private static readonly Uri A13 = new("http://localhost/sr-padron/webservices/personaServiceA13");

    [Fact]
    public async Task The_constancia_of_a_company_shows_its_name_address_and_VAT_registration()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        await PutProfiledAsync(sim, 30000000007, "Distribuidora del Plata S.A.", "ResponsableInscripto");
        var auth = await AuthAsync(sim, "ws_sr_constancia_inscripcion");

        var (status, body) = await PostAsync(sim, A5, "a5", "getPersona_v2", auth + "<idPersona>30000000007</idPersona>");
        var persona = XDocument.Parse(body).Descendants("personaReturn").Single();

        Assert.Equal(200, status);
        Assert.Equal("DISTRIBUIDORA DEL PLATA S.A.", persona.Descendants("razonSocial").Single().Value);
        Assert.Equal("JURIDICA", persona.Descendants("tipoPersona").Single().Value);
        Assert.Equal("AV. CORRIENTES 1234", persona.Descendants("direccion").Single().Value);
        Assert.Contains(persona.Element("datosRegimenGeneral")!.Elements("impuesto"), i => i.Element("idImpuesto")!.Value == "30");
        Assert.Contains("<ns2:getPersona_v2Response xmlns:ns2=\"http://a5.soap.ws.server.puc.sr/\"><personaReturn><datosGenerales>", body);
    }

    [Fact]
    public async Task A_monotributista_shows_up_under_the_monotributo_block()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(20222222223, "Ana Gomez", VatCondition.Monotributo);
        var auth = await AuthAsync(sim, "ws_sr_constancia_inscripcion");

        var (_, body) = await PostAsync(sim, A5, "a5", "getPersona_v2", auth + "<idPersona>20222222223</idPersona>");
        var persona = XDocument.Parse(body).Descendants("personaReturn").Single();

        Assert.Equal("GOMEZ", persona.Descendants("apellido").Single().Value);
        Assert.Equal("ANA", persona.Descendants("nombre").Single().Value);
        Assert.Equal("20", persona.Element("datosMonotributo")!.Element("impuesto")!.Element("idImpuesto")!.Value);
        Assert.Null(persona.Element("datosRegimenGeneral"));
    }

    [Fact]
    public async Task A_list_answers_each_key_and_flags_the_ones_that_do_not_exist()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var auth = await AuthAsync(sim, "ws_sr_constancia_inscripcion");

        var (_, body) = await PostAsync(sim, A5, "a5", "getPersonaList_v2",
            auth + $"<idPersona>{Caller}</idPersona><idPersona>27000000006</idPersona>");
        var people = XDocument.Parse(body).Descendants("persona").ToList();

        Assert.Equal(2, people.Count);
        Assert.NotNull(people[0].Element("datosGenerales"));
        Assert.Equal("No existe persona con ese Id", people[1].Element("errorConstancia")!.Element("error")!.Value);
    }

    [Fact]
    public async Task A13_finds_the_CUIT_behind_a_DNI_and_returns_the_person_with_its_document()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(20222222223, "Ana Gomez", VatCondition.Monotributo);
        var auth = await AuthAsync(sim, "ws_sr_padron_a13");

        var (_, ids) = await PostAsync(sim, A13, "a13", "getIdPersonaListByDocumento", auth + "<documento>22222222</documento>");
        var (_, person) = await PostAsync(sim, A13, "a13", "getPersona", auth + "<idPersona>20222222223</idPersona>");

        Assert.Equal("20222222223", XDocument.Parse(ids).Descendants("idPersona").Single().Value);
        Assert.Equal("22222222", XDocument.Parse(person).Descendants("numeroDocumento").Single().Value);
        Assert.Equal("DNI", XDocument.Parse(person).Descendants("tipoDocumento").Single().Value);
    }

    [Fact]
    public async Task A13_reports_a_CUIT_nobody_has_with_a_fault()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var auth = await AuthAsync(sim, "ws_sr_padron_a13");

        var (status, body) = await PostAsync(sim, A13, "a13", "getPersona", auth + "<idPersona>27000000006</idPersona>");

        Assert.Equal(500, status);
        Assert.Contains("<faultcode>soap:Server</faultcode><faultstring>La Clave (CUIT/CUIL) consultada es inexistente</faultstring>", body);
    }

    [Theory]
    [InlineData("", "", "Falta token y/o sign.")]
    [InlineData("abc", "abc", "Token malformado")]
    public async Task Authentication_failures_are_faults_with_the_texts_homologacion_gives(string token, string sign, string expected)
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await PostAsync(sim, A5, "a5", "getPersona_v2",
            $"<token>{token}</token><sign>{sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada><idPersona>{Caller}</idPersona>");

        Assert.Equal(500, status);
        Assert.Contains($"<faultstring>{expected}</faultstring>", body);
        Assert.Contains("SRValidationException", body);
    }

    [Fact]
    public async Task A_ticket_for_invoicing_does_not_open_the_padron()
    {
        await using var sim = ArcaSimHarness.Start();
        await sim.PutTaxpayerAsync(Caller, "Empresa", VatCondition.ResponsableInscripto);
        var auth = await AuthAsync(sim, "wsfe");

        var (_, body) = await PostAsync(sim, A5, "a5", "getPersona_v2", auth + $"<idPersona>{Caller}</idPersona>");

        Assert.Contains("<faultstring>No autorizado, par token/sign invalido.</faultstring>", body);
    }

    [Fact]
    public async Task Dummy_and_the_WSDL_answer_without_a_ticket()
    {
        await using var sim = ArcaSimHarness.Start();

        var (status, body) = await PostAsync(sim, A13, "a13", "dummy", "");
        var wsdl = await sim.Http.GetStringAsync("/sr-padron/webservices/personaServiceA13?WSDL");

        Assert.Equal(200, status);
        Assert.Contains("<return><appserver>OK</appserver><authserver>OK</authserver><dbserver>OK</dbserver></return>", body);
        Assert.Contains("location=\"http://localhost/sr-padron/webservices/personaServiceA13\"", wsdl);
    }

    private static async Task PutProfiledAsync(ArcaSimHarness sim, long cuit, string name, string condition)
    {
        var response = await sim.Http.PutAsJsonAsync($"/arcasim/api/taxpayers/{cuit}", new
        {
            name,
            vatCondition = condition,
            profile = new
            {
                address = new { street = "AV. CORRIENTES 1234", locality = "CIUDAD AUTONOMA BUENOS AIRES", postalCode = "1043", provinceId = 0, province = "CIUDAD AUTONOMA BUENOS AIRES" },
                legalForm = "SOC. ANONIMA",
            },
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> AuthAsync(ArcaSimHarness sim, string service)
    {
        var certificate = await sim.IssueCertificateAsync(Caller, "consultas", "wsfe", "ws_sr_constancia_inscripcion", "ws_sr_padron_a13");
        var ticket = await sim.Wsaa(Caller, certificate).LoginAsync(service);
        return $"<token>{ticket.Token}</token><sign>{ticket.Sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada>";
    }

    private static Task<(int Status, string Body)> PostAsync(ArcaSimHarness sim, Uri url, string prefix, string operation, string inner)
    {
        var ns = prefix == "a5" ? "http://a5.soap.ws.server.puc.sr/" : "http://a13.soap.ws.server.puc.sr/";
        return sim.PostSoapAsync(url,
            $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:{prefix}=\"{ns}\">" +
            $"<soapenv:Header/><soapenv:Body><{prefix}:{operation}>{inner}</{prefix}:{operation}></soapenv:Body></soapenv:Envelope>", "\"\"");
    }
}
