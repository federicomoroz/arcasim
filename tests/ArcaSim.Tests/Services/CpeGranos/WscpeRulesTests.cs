using System.Xml.Linq;

namespace ArcaSim.Tests.Services.CpeGranos;

/// <summary>wscpe: numbering, CTG, the state machine across the CUITs a CPE names, and the anexo 4.1 rejections, every answer valid for the WSDL.</summary>
public class WscpeRulesTests
{
    private const long Issuer = ArcaSimHarness.Issuer;
    private const long Destination = 30000000007;
    private const long Stranger = 20222222223;

    [Fact]
    public async Task Authorize_takes_the_next_order_number_and_gives_a_CTG_both_consults_read_back()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);

        var last = await cpe.CallAsync(Issuer, "consultarUltNroOrden", "ConsultarUltNroOrdenReq",
            "<solicitud><sucursal>1</sucursal><tipoCPE>74</tipoCPE></solicitud>");
        Assert.Equal("0", last.Value("nroOrden"));

        var authorized = await AuthorizeAsync(cpe, 1);
        var ctg = authorized.Value("nroCTG")!;
        Assert.Equal("AC", authorized.Value("estado"));
        Assert.Matches("^10200000001$", ctg);
        Assert.Equal("2026-10-16T12:00:00", authorized.Value("fechaVencimiento"));
        Assert.False(string.IsNullOrEmpty(authorized.Value("pdf")));

        last = await cpe.CallAsync(Issuer, "consultarUltNroOrden", "ConsultarUltNroOrdenReq",
            "<solicitud><sucursal>1</sucursal><tipoCPE>74</tipoCPE></solicitud>");
        Assert.Equal("1", last.Value("nroOrden"));

        var byKey = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq",
            "<solicitud><cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>1</nroOrden></cartaPorte></solicitud>");
        var byCtg = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq",
            $"<solicitud><nroCTG>{ctg}</nroCTG></solicitud>");
        foreach (var answer in new[] { byKey, byCtg })
        {
            Assert.Equal(ctg, answer.Value("nroCTG"));
            Assert.Equal("1938", answer.Descendants("origen").Single().Element("planta")!.Value);
            Assert.Equal(Destination.ToString(), answer.Descendants("destino").Single().Element("cuit")!.Value);
            Assert.Equal("30000", answer.Value("pesoBruto"));
            Assert.Equal("AB123CD", answer.Value("dominio"));
            Assert.Null(answer.Descendants("errores").FirstOrDefault());
        }
    }

    [Fact]
    public async Task The_PDF_of_a_CPE_is_the_one_page_ArcaSim_has_always_sent()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);

        var authorized = await AuthorizeAsync(cpe, 1);

        Assert.Equal(
            "%PDF-1.4\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n" +
            "4 0 obj\n<< /Length 80 >>\nstream\nBT /F1 12 Tf 72 770 Td (Carta de Porte Electronica - CTG 10200000001 - AC) Tj ET\nendstream\nendobj\n" +
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n" +
            "xref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000241 00000 n \n0000000371 00000 n \n" +
            "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n441\n%%EOF",
            System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(authorized.Value("pdf")!)));
    }

    [Fact]
    public async Task The_destination_confirms_arrival_and_closes_it_with_the_unloaded_weights()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await AuthorizeAsync(cpe, 1);
        var key = "<cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>1</nroOrden></cartaPorte>";

        var arrived = await cpe.CallAsync(Destination, "confirmarArriboCPE", "ConfirmarArriboCPEReq",
            $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante>{key}</solicitud>");
        Assert.Equal("CF", arrived.Value("estado"));

        var confirmed = await cpe.CallAsync(Destination, "confirmacionDefinitivaCPEAutomotor", "ConfirmacionDefinitivaCPEAutomotorReq",
            $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante>{key}<pesoBrutoDescarga>29800</pesoBrutoDescarga><pesoTaraDescarga>10000</pesoTaraDescarga></solicitud>");
        Assert.Equal("CN", confirmed.Value("estado"));
        Assert.Null(confirmed.Value("fechaVencimiento"));

        var read = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", $"<solicitud>{key}</solicitud>");
        Assert.Equal("CN", read.Value("estado"));
        Assert.Equal("29800", read.Value("pesoBrutoDescarga"));

        var voided = await cpe.CallAsync(Issuer, "anularCPE", "AnularCPEReq", $"<solicitud>{key}<anulacionMotivo>1</anulacionMotivo></solicitud>");
        Assert.Equal(("2034", "La transición desde el estado CN hacia el estado AN es inválida."), voided.FirstError());
    }

    [Fact]
    public async Task Order_numbers_must_follow_the_last_and_cannot_be_reused()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        var ctg = (await AuthorizeAsync(cpe, 1)).Value("nroCTG");

        var again = await AuthorizeAsync(cpe, 1);
        Assert.Equal(("2241", $"La Carta de Porte ya fue emitida con el Nro de CTG {ctg}."), again.FirstError());

        var skipped = await AuthorizeAsync(cpe, 5);
        Assert.Equal(("961", "Número de orden incorrecto para el tipo de carta de porte y sucursal ingresados."), skipped.FirstError());
        Assert.Null(skipped.Descendants("cabecera").FirstOrDefault());

        var wrongType = await AuthorizeAsync(cpe, 2, type: 75);
        Assert.Equal(("2055", "Operación no disponible para el tipo de CPE actual."), wrongType.FirstError());
    }

    [Fact]
    public async Task Only_the_CUITs_a_CPE_names_can_read_or_move_it()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await cpe.LoginAsync(Stranger, "Ana Gomez");
        var ctg = (await AuthorizeAsync(cpe, 1)).Value("nroCTG");

        var read = await cpe.CallAsync(Stranger, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", $"<solicitud><nroCTG>{ctg}</nroCTG></solicitud>");
        Assert.Equal(("2039", "Usted no puede realizar operaciones para la solicitud indicada."), read.FirstError());

        var moved = await cpe.CallAsync(Stranger, "confirmarArriboCPE", "ConfirmarArriboCPEReq",
            $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante><cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>1</nroOrden></cartaPorte></solicitud>");
        Assert.Equal(("2037", $"Usted no puede realizar operaciones para la CUIT solicitante {Issuer}."), moved.FirstError());

        var missing = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", "<solicitud><nroCTG>10299999999</nroCTG></solicitud>");
        Assert.Equal(("1302", "No existe una CPE con los parámetros indicados"), missing.FirstError());
    }

    [Fact]
    public async Task Voiding_checks_the_reason_and_the_deadline()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await AuthorizeAsync(cpe, 1);
        await AuthorizeAsync(cpe, 2);
        var key = (int n) => $"<cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>{n}</nroOrden></cartaPorte>";

        var badReason = await cpe.CallAsync(Issuer, "anularCPE", "AnularCPEReq", $"<solicitud>{key(1)}<anulacionMotivo>9</anulacionMotivo></solicitud>");
        Assert.Equal(("2220", "El motivo anulación es inválido."), badReason.FirstError());

        var voided = await cpe.CallAsync(Issuer, "anularCPE", "AnularCPEReq",
            $"<solicitud>{key(1)}<anulacionMotivo>3</anulacionMotivo><anulacionObservaciones>Se rompio el camion</anulacionObservaciones></solicitud>");
        Assert.Equal("AN", voided.Value("estado"));
        Assert.Equal("3", voided.Value("anulacionMotivo"));
        Assert.Equal("Se rompio el camion", voided.Value("anulacionObservaciones"));

        sim.Clock.Advance(TimeSpan.FromDays(16));
        await cpe.LoginAsync(Issuer);
        var late = await cpe.CallAsync(Issuer, "anularCPE", "AnularCPEReq", $"<solicitud>{key(2)}<anulacionMotivo>1</anulacionMotivo></solicitud>");
        Assert.Equal(("2121", "El plazo para la Anulación de la actual Carta de Porte Electrónica fue superado."), late.FirstError());
    }

    [Fact]
    public async Task A_rejected_CPE_takes_two_new_destinations_and_no_more()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await AuthorizeAsync(cpe, 1);
        var key = "<cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>1</nroOrden></cartaPorte>";
        var newDestination = $"<solicitud>{key}<destino><cuit>{Destination}</cuit><esDestinoCampo>false</esDestinoCampo><codProvincia>1</codProvincia>" +
                             "<codLocalidad>10</codLocalidad><planta>7</planta></destino>" +
                             "<transporte><cuitTransportista>20333333334</cuitTransportista><dominio>AA000AA</dominio>" +
                             "<fechaHoraPartida>2026-10-02T08:00:00</fechaHoraPartida><kmRecorrer>30</kmRecorrer></transporte></solicitud>";

        XElement last = null!;
        for (var round = 0; round < 3; round++)
        {
            await cpe.CallAsync(Destination, "confirmarArriboCPE", "ConfirmarArriboCPEReq", $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante>{key}</solicitud>");
            var rejected = await cpe.CallAsync(Destination, "rechazoCPE", "RechazoCPEReq",
                $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante>{key}<rechazoMotivo>2</rechazoMotivo></solicitud>");
            Assert.Equal("RE", rejected.Value("estado"));
            last = await cpe.CallAsync(Issuer, "nuevoDestinoDestinatarioCPEAutomotor", "NuevoDestinoDestinatarioCPEAutomotorReq", newDestination);
            if (round < 2) Assert.Equal("AC", last.Value("estado"));
        }
        Assert.Equal(("2232", "Se superó la cantidad de nuevos destinatarios permitidos permitidos."), last.FirstError());

        var read = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", $"<solicitud>{key}</solicitud>");
        Assert.Equal("7", read.Descendants("destino").Single().Element("planta")!.Value);
        Assert.Equal("AA000AA", read.Value("dominio"));
        Assert.Equal("RE", read.Value("estado"));
    }

    [Fact]
    public async Task Contingencies_open_and_close_by_concept()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await AuthorizeAsync(cpe, 1);
        var key = "<cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>1</nroOrden></cartaPorte>";

        var open = await cpe.CallAsync(Issuer, "informarContingencia", "InformarContingenciaReq",
            $"<solicitud>{key}<contingencia><concepto>C</concepto><descripcion>Desperfecto</descripcion></contingencia></solicitud>");
        Assert.Equal("CO", open.Value("estado"));

        var extended = await cpe.CallAsync(Issuer, "cerrarContingenciaCPE", "CerrarContingenciaCPEReq", $"<solicitud>{key}<concepto>B</concepto></solicitud>");
        Assert.Equal("CO", extended.Value("estado"));

        var reactivated = await cpe.CallAsync(Issuer, "cerrarContingenciaCPE", "CerrarContingenciaCPEReq", $"<solicitud>{key}<concepto>A</concepto></solicitud>");
        Assert.Equal("AC", reactivated.Value("estado"));
    }

    [Fact]
    public async Task The_destination_lists_what_comes_to_its_plant_within_three_days()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        await AuthorizeAsync(cpe, 1);
        await AuthorizeAsync(cpe, 2);

        var listed = await cpe.CallAsync(Destination, "consultarCPEPorDestino", "ConsultarCPEPorDestinoReq",
            "<solicitud><planta>1</planta><fechaPartidaDesde>2026-09-30</fechaPartidaDesde><fechaPartidaHasta>2026-10-02</fechaPartidaHasta></solicitud>");
        Assert.Equal(2, listed.Descendants("cartaPorte").Count());
        Assert.All(listed.Descendants("cartaPorte"), c => Assert.Equal("AC", c.Element("estado")!.Value));

        var wide = await cpe.CallAsync(Destination, "consultarCPEPorDestino", "ConsultarCPEPorDestinoReq",
            "<solicitud><planta>1</planta><fechaPartidaDesde>2026-09-20</fechaPartidaDesde><fechaPartidaHasta>2026-10-02</fechaPartidaHasta></solicitud>");
        Assert.Equal(("2152", "El rango de fechas debe ser como máximo de 3 días."), wide.FirstError());
    }

    [Fact]
    public async Task A_rail_CPE_takes_type_75_and_a_rail_CTG_and_answers_only_to_its_own_operations()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);

        var authorized = await cpe.CallAsync(Issuer, "autorizarCPEFerroviaria", "AutorizarCPEFerroviariaReq",
            "<solicitud><cabecera><sucursal>171</sucursal><nroOrden>1</nroOrden><planta>1938</planta></cabecera>" +
            "<correspondeRetiroProductor>false</correspondeRetiroProductor>" +
            "<datosCarga><codGrano>23</codGrano><cosecha>2526</cosecha><pesoBruto>60000</pesoBruto><pesoTara>20000</pesoTara></datosCarga>" +
            $"<destino><cuit>{Destination}</cuit><esDestinoCampo>false</esDestinoCampo><codProvincia>12</codProvincia><codLocalidad>3058</codLocalidad><planta>1</planta></destino>" +
            $"<destinatario><cuit>{Destination}</cuit></destinatario>" +
            "<transporte><cuitTransportista>30500000005</cuitTransportista><nroVagon>12345678</nroVagon><nroPrecinto>P1</nroPrecinto><nroOperativo>77</nroOperativo>" +
            "<fechaHoraPartidaTren>2026-10-01T06:00:00</fechaHoraPartidaTren><kmRecorrer>400</kmRecorrer></transporte></solicitud>");
        Assert.Equal("75", authorized.Value("tipoCartaPorte"));
        Assert.Equal("20200000001", authorized.Value("nroCTG"));
        Assert.Equal("1938", authorized.Descendants("origen").Single().Element("planta")!.Value);

        var read = await cpe.CallAsync(Destination, "consultarCPEFerroviaria", "ConsultarCPEFerroviariaReq",
            $"<solicitud><cuitSolicitante>{Issuer}</cuitSolicitante><cartaPorte><tipoCPE>75</tipoCPE><sucursal>171</sucursal><nroOrden>1</nroOrden></cartaPorte></solicitud>");
        Assert.Equal("12345678", read.Value("nroVagon"));

        var asTruck = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", "<solicitud><nroCTG>20200000001</nroCTG></solicitud>");
        Assert.Equal("1302", asTruck.FirstError()!.Value.Code);

        var key = $"<cuitSolicitante>{Issuer}</cuitSolicitante><cartaPorte><tipoCPE>75</tipoCPE><sucursal>171</sucursal><nroOrden>1</nroOrden></cartaPorte>";
        await cpe.CallAsync(Destination, "confirmarArriboCPE", "ConfirmarArriboCPEReq", $"<solicitud>{key}</solicitud>");
        var wrongFamily = await cpe.CallAsync(Destination, "confirmacionDefinitivaCPEAutomotor", "ConfirmacionDefinitivaCPEAutomotorReq",
            $"<solicitud>{key}<pesoBrutoDescarga>59000</pesoBrutoDescarga><pesoTaraDescarga>20000</pesoTaraDescarga></solicitud>");
        Assert.Equal(("2055", "Operación no disponible para el tipo de CPE actual."), wrongFamily.FirstError());
    }

    [Fact]
    public async Task Edits_change_the_data_by_CTG_up_to_the_limit_and_not_after_voiding()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);
        var ctg = (await AuthorizeAsync(cpe, 1)).Value("nroCTG");

        for (var edit = 1; edit <= 3; edit++)
        {
            var edited = await cpe.CallAsync(Issuer, "editarCPEAutomotor", "EditarCPEAutomotorReq",
                $"<solicitud><nroCTG>{ctg}</nroCTG><cuitChofer>20444444445</cuitChofer><pesoBruto>{31000 + edit}</pesoBruto><codGrano>23</codGrano>" +
                "<dominio>AC456EF</dominio><dominio>AD789GH</dominio></solicitud>");
            Assert.Equal("AC", edited.Value("estado"));
        }
        var read = await cpe.CallAsync(Issuer, "consultarCPEAutomotor", "ConsultarCPEAutomotorReq", $"<solicitud><nroCTG>{ctg}</nroCTG></solicitud>");
        Assert.Equal("31003", read.Value("pesoBruto"));
        Assert.Equal("20444444445", read.Value("cuitChofer"));
        Assert.Equal(["AC456EF", "AD789GH"], read.Descendants("dominio").Select(d => d.Value));

        var fourth = await cpe.CallAsync(Issuer, "editarCPEAutomotor", "EditarCPEAutomotorReq",
            $"<solicitud><nroCTG>{ctg}</nroCTG><pesoBruto>32000</pesoBruto><codGrano>23</codGrano></solicitud>");
        Assert.Equal(("2238", "La Carta de Porte no puede modificarse porque alcanzó el tope máximo de modificaciones realizadas."), fourth.FirstError());

        var second = (await AuthorizeAsync(cpe, 2)).Value("nroCTG");
        await cpe.CallAsync(Issuer, "anularCPE", "AnularCPEReq",
            "<solicitud><cartaPorte><tipoCPE>74</tipoCPE><sucursal>1</sucursal><nroOrden>2</nroOrden></cartaPorte><anulacionMotivo>1</anulacionMotivo></solicitud>");
        var voided = await cpe.CallAsync(Issuer, "editarCPEAutomotor", "EditarCPEAutomotorReq",
            $"<solicitud><nroCTG>{second}</nroCTG><pesoBruto>32000</pesoBruto><codGrano>23</codGrano></solicitud>");
        Assert.Equal(("2004", "Estado no válido"), voided.FirstError());
    }

    [Fact]
    public async Task Dummy_and_provinces_answer_what_ARCA_shows()
    {
        await using var sim = Start();
        var cpe = await ConnectAsync(sim);

        var provinces = await cpe.CallAsync(Issuer, "consultarProvincias", "ConsultarProvinciasReq");
        Assert.Equal(24, provinces.Descendants("provincia").Count());
        Assert.Contains(provinces.Descendants("provincia"), p => p.Element("codigo")!.Value == "0" && p.Element("descripcion")!.Value == "CAP.FEDERAL");
        Assert.Equal("arcasim", provinces.Value("servidor"));
    }

    private static ArcaSimHarness Start()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        return sim;
    }

    private static async Task<GrainsSoap> ConnectAsync(ArcaSimHarness sim)
    {
        var cpe = GrainsSoap.Wscpe(sim);
        await cpe.LoginAsync(Issuer);
        await cpe.LoginAsync(Destination, "Acopio del Sur S.A.");
        return cpe;
    }

    private static Task<XElement> AuthorizeAsync(GrainsSoap cpe, int order, int type = 74) =>
        cpe.CallAsync(Issuer, "autorizarCPEAutomotor", "AutorizarCPEAutomotorReq",
            "<solicitud>" +
            $"<cabecera><tipoCP>{type}</tipoCP><cuitSolicitante>{Issuer}</cuitSolicitante><sucursal>1</sucursal><nroOrden>{order}</nroOrden></cabecera>" +
            "<origen><operador><codProvincia>12</codProvincia><codLocalidad>5544</codLocalidad><planta>1938</planta></operador></origen>" +
            "<correspondeRetiroProductor>false</correspondeRetiroProductor><esSolicitanteCampo>false</esSolicitanteCampo>" +
            "<datosCarga><codGrano>23</codGrano><cosecha>2526</cosecha><pesoBruto>30000</pesoBruto><pesoTara>10000</pesoTara></datosCarga>" +
            $"<destino><cuit>{Destination}</cuit><esDestinoCampo>false</esDestinoCampo><codProvincia>12</codProvincia><codLocalidad>3058</codLocalidad><planta>1</planta></destino>" +
            $"<destinatario><cuit>{Destination}</cuit></destinatario>" +
            "<transporte><cuitTransportista>20333333334</cuitTransportista><dominio>AB123CD</dominio>" +
            "<fechaHoraPartida>2026-10-01T10:00:00</fechaHoraPartida><kmRecorrer>250</kmRecorrer><cuitChofer>20333333334</cuitChofer>" +
            $"<cuitPagadorFlete>{Issuer}</cuitPagadorFlete><mercaderiaFumigada>false</mercaderiaFumigada></transporte>" +
            "</solicitud>");
}
