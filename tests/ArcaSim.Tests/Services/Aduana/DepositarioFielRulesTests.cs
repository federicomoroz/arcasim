namespace ArcaSim.Tests.Services.Aduana;

/// <summary>A legajo endorsed to a PSAD, received and digitized through wDigDepFiel, and followed in wConsDepFiel.</summary>
public class DepositarioFielRulesTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    private static Task<AduanaService> ConsAsync(AduanaKit kit, long cuit = AduanaKit.Caller) =>
        kit.ServiceAsync("wConsDepFiel", (t, c) => AduanaKit.ArgAutentica(t, c, "PSAD"), cuit);

    private static Task<AduanaService> DigAsync(AduanaKit kit) =>
        kit.ServiceAsync("wDigDepFiel", (t, c) => AduanaKit.Flat("autentica", t, c, "PSAD"));

    private static string Pending(string from = "2026-10-01T00:00:00", string to = "2026-10-01T23:00:00") =>
        $"<argInPndListaEndo><FechaDesde>{from}</FechaDesde><FechaHasta>{to}</FechaHasta></argInPndListaEndo>";

    private static string State(string legajo) =>
        $"<argInListaEstado><CodigoCarpeta>000</CodigoCarpeta><NroLegajo>{legajo}</NroLegajo></argInListaEstado>";

    private static string Received(string legajo, long ie = 30000000007) =>
        $"<nroLegajo>{legajo}</nroLegajo><cuitDeclarante>20222222223</cuitDeclarante><cuitPSAD>{AduanaKit.Caller}</cuitPSAD><cuitIE>{ie}</cuitIE>" +
        "<codigo>000</codigo><fechaHoraAcept>2026-10-01T10:00:00</fechaHoraAcept><nroGuia>G1</nroGuia><fechaGeneracion>2026-10-01T09:00:00</fechaGeneracion>" +
        "<indLugarFisico>R1</indLugarFisico><idEnvio>E1</idEnvio><fechaDespacho>2026-09-29T09:00:00</fechaDespacho><cantFojas>12</cantFojas>";

    private static string Digitized(string legajo, string code = "000", string ticket = "") =>
        $"<nroLegajo>{legajo}</nroLegajo><cuitDeclarante>20222222223</cuitDeclarante><cuitPSAD>{AduanaKit.Caller}</cuitPSAD><cuitIE>30000000007</cuitIE>" +
        $"<codigo>{code}</codigo><url>https://psad.example/{legajo}</url><familias>" +
        string.Concat((code == "001" ? ["01"] : new[] { "01", "02", "03", "04", "05" }).Select(f => $"<Familia><codigo>{f}</codigo><cantidad>2</cantidad></Familia>")) +
        $"</familias>{(ticket == "" ? "" : $"<ticket>{ticket}</ticket>")}<hashing>{Hash}</hashing><cantidadTotal>10</cantidadTotal>";

    [Fact]
    public async Task A_legajo_goes_from_ENDO_to_PSAD_to_DIGI_and_leaves_the_pending_list()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cons = await ConsAsync(kit);
        var dig = await DigAsync(kit);

        var pending = await cons.CallAsync("PndListaEndo", Pending());
        var legajos = pending.All("Legajo").Select(l => l.V("NroLegajo")).ToList();
        var received = await dig.CallAsync("AvisoRecepAcept", Received(legajos[0]));
        var afterReception = await cons.CallAsync("ListaEstado", State(legajos[0]));
        var digitized = await dig.CallAsync("AvisoDigit", Digitized(legajos[0]));
        var afterDigit = await cons.CallAsync("ListaEstado", State(legajos[0]));
        var stillPending = await cons.CallAsync("PndListaEndo", Pending());

        Assert.Equal("0", pending.Code());
        Assert.Equal(2, legajos.Count);
        Assert.Equal("0", received.Code());
        Assert.Equal("OK Procesado", received.V("descError"));
        Assert.Equal("PSAD", afterReception.V("Estado"));
        Assert.StartsWith("2026-10-08", afterReception.V("FechaVtoDIGI"));
        Assert.Equal("0", digitized.Code());
        Assert.Equal("DIGI", afterDigit.V("Estado"));
        Assert.Equal("OK Procesado", afterDigit.V("DescErr"));
        Assert.Equal([legajos[1]], stillPending.All("NroLegajo").Select(l => l.Value));
    }

    [Fact]
    public async Task Repeated_or_mismatched_notices_get_each_methods_code()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cons = await ConsAsync(kit);
        var dig = await DigAsync(kit);
        var legajo = (await cons.CallAsync("PndListaEndo", Pending())).All("NroLegajo").First().Value;

        var wrongImporter = await dig.CallAsync("AvisoRecepAcept", Received(legajo, ie: 30500010912));
        await dig.CallAsync("AvisoRecepAcept", Received(legajo));
        var twice = await dig.CallAsync("AvisoRecepAcept", Received(legajo));
        var additionalTooSoon = await dig.CallAsync("AvisoDigit", Digitized(legajo, "001", "2026000001"));
        var unknown = await dig.CallAsync("AvisoRecepAcept", Received("26001IC04999999Z"));

        Assert.Equal("108", wrongImporter.Code());
        Assert.Equal("111", twice.Code());
        Assert.Equal("Legajo Duplicado", twice.V("descError"));
        Assert.Equal("104", additionalTooSoon.Code());
        Assert.Equal("101", unknown.Code());
    }

    [Fact]
    public async Task Another_PSAD_cannot_see_the_legajo_and_bad_dates_are_refused()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cons = await ConsAsync(kit);
        var other = await ConsAsync(kit, AduanaKit.Other);
        var legajo = (await cons.CallAsync("PndListaEndo", Pending())).All("NroLegajo").First().Value;

        var notTheirs = await other.CallAsync("ListaEstado", State(legajo));
        var backwards = await cons.CallAsync("PndListaEndo", Pending(from: "2026-10-01T00:00:00", to: "2026-09-01T00:00:00"));
        var future = await cons.CallAsync("PndListaEndo", Pending(from: "2026-10-05T00:00:00", to: "2026-10-05T00:00:00"));
        var ticketless = await cons.CallAsync("ListaEstado",
            $"<argInListaEstado><CodigoCarpeta>001</CodigoCarpeta><NroLegajo>{legajo}</NroLegajo></argInListaEstado>");

        Assert.Equal("102", notTheirs.Code());
        Assert.Equal("Usted no es depositario fiel del legajo informado", notTheirs.V("DescErr"));
        Assert.Equal("7", backwards.Code());
        Assert.Equal("5", future.Code());
        Assert.Equal("5", ticketless.Code());
    }
}
