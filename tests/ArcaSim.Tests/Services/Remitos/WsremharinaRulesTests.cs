using System.Xml.Linq;
using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>The flour remito from generation to reception, with the state kept between calls and every answer valid for the WSDL.</summary>
public class WsremharinaRulesTests
{
    private const string Service = "wsremharina";

    [Fact]
    public async Task A_remito_of_the_holders_own_goods_is_issued_at_once_and_numbered_last_plus_one()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var first = await issuer.CallAsync("generarRemito", Generate(issuer, requestId: 1, holder: Issuer));
        var second = await issuer.CallAsync("generarRemito", Generate(issuer, requestId: 2, holder: Issuer));

        Assert.Equal("A", first.Value("resultado"));
        Assert.Equal("EMI", first.Value("estadoRemito"));
        Assert.Equal("1", first.Value("nroRemito"));
        Assert.Equal("2", second.Value("nroRemito"));
        Assert.Equal("993", first.Value("tipoCmp"));
        Assert.Equal("2026-10-01-03:00", first.Value("fechaEmision"));
        Assert.Equal("2026-10-04-03:00", first.Value("fechaVencimiento"));
        Assert.Matches("^4640400000000\\d$", first.Value("codAutorizacion"));
        Assert.StartsWith("/9j/4AAQSkZJRg", first.Value("qr"));

        var last = await issuer.CallAsync("consultarUltimoRemitoEmitido",
            issuer.Auth + "<tipoComprobante>993</tipoComprobante><puntoEmision>1</puntoEmision>");
        Assert.Equal(second.Value("codRemito"), last.Value("codRemito"));
        Assert.Equal("2", last.Value("nroRemito"));

        var byRequest = await issuer.CallAsync("consultarRemito", issuer.Auth + "<idReqCliente>1</idReqCliente><puntoEmision>1</puntoEmision>");
        Assert.Equal(first.Value("codRemito"), byRequest.Value("codRemito"));
        Assert.Equal("500", byRequest.One("mercaderia").Element("pesoNetoKg")!.Value);
    }

    [Fact]
    public async Task The_receiver_accepts_part_of_the_goods_and_the_remito_ends_partly_accepted()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);
        var code = (await issuer.CallAsync("generarRemito", Generate(issuer, 1, Issuer))).Value("codRemito");

        var reception = await receiver.CallAsync("registrarRecepcion", receiver.Auth +
            $"<codRemito>{code}</codRemito><fecha>2026-10-02</fecha><aceptado>S</aceptado><arrayRecepcionMercaderia>" +
            "<recepcionMercaderia><orden>1</orden><pesoNetoKG>500</pesoNetoKG></recepcionMercaderia>" +
            "<recepcionMercaderia><orden>2</orden><pesoNetoKG>100</pesoNetoKG></recepcionMercaderia></arrayRecepcionMercaderia>");
        Assert.Equal("A", reception.Value("resultado"));

        var remito = await receiver.CallAsync("consultarRemito", receiver.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal("ACP", remito.Value("estadoRemito"));
        Assert.Equal("2026-10-02-03:00", remito.Value("fechaRec"));
        Assert.Equal(["500", "100"], remito.Descendants("pesoNetoRecKg").Select(e => e.Value));

        var history = await issuer.CallAsync("consultarEstadosRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["EMI", "ACP"], history.Descendants("estado").Select(e => e.Value));
        Assert.Equal("ANA PANADERA", history.Descendants("cuitDesc").Last().Value);

        var list = await receiver.CallAsync("consultarRemitosReceptor", receiver.Auth + "<estadoRecepcion>ACP</estadoRecepcion>");
        Assert.Equal(code, list.Value("codRemito"));
        Assert.Equal("N", list.Value("hayMas"));
    }

    [Fact]
    public async Task Someone_elses_goods_wait_for_the_holder_then_the_issuer_issues_them()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var holder = await ServiceClient.LoginAsync(sim, Service, Holder);

        var generated = await issuer.CallAsync("generarRemito", Generate(issuer, 1, Holder));
        var code = generated.Value("codRemito");
        Assert.Equal("PAT", generated.Value("estadoRemito"));
        Assert.False(generated.Has("datosAutAFIP"));

        var pending = await holder.CallAsync("consultarRemitosAutorizador", holder.Auth + "<rolAutorizador>TIT</rolAutorizador><estadoAutorizacion>PE</estadoAutorizacion>");
        Assert.Equal(code, pending.Value("codRemito"));

        var byIssuer = await issuer.CallAsync("autorizarRemito", issuer.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");
        Assert.Equal(("3070", "Operación no permitida"), byIssuer.Error());

        await holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");
        var issued = await issuer.CallAsync("emitirRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");

        Assert.Equal("EMI", issued.Value("estadoRemito"));
        Assert.Equal("1", issued.Value("nroRemito"));
        Assert.Equal("2026-10-01-03:00", issued.Value("fechaAut"));
        var history = await holder.CallAsync("consultarEstadosRemito", holder.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["PAT", "PEM", "EMI"], history.Descendants("estado").Select(e => e.Value));
    }

    [Fact]
    public async Task A_repeated_request_id_is_refused_with_151()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        await issuer.CallAsync("generarRemito", Generate(issuer, 7, Issuer));

        var again = await issuer.CallAsync("generarRemito", Generate(issuer, 7, Issuer));

        Assert.Equal("R", again.Value("resultado"));
        Assert.Equal(("151", "El ID de request 7 ya existe para el punto de emisión 1"), again.Error());
        Assert.False(again.Has("remitoOutput"));
    }

    [Fact]
    public async Task A_receiver_nobody_registered_is_refused_with_100()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var answer = await issuer.CallAsync("generarRemito", Generate(issuer, 1, Issuer, receiver: 20111111120));

        Assert.Equal(("100", "CUIT debe encontrarse en el Sistema Registral"), answer.Error());
    }

    [Fact]
    public async Task More_weight_received_than_sent_is_refused_with_3023()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);
        var code = (await issuer.CallAsync("generarRemito", Generate(issuer, 1, Issuer))).Value("codRemito");

        var answer = await receiver.CallAsync("registrarRecepcion", receiver.Auth +
            $"<codRemito>{code}</codRemito><fecha>2026-10-02</fecha><aceptado>S</aceptado><arrayRecepcionMercaderia>" +
            "<recepcionMercaderia><orden>1</orden><pesoNetoKG>900</pesoNetoKG></recepcionMercaderia></arrayRecepcionMercaderia>");

        Assert.Equal("3023", answer.Error().Code);
    }

    [Fact]
    public async Task A_pending_remito_is_cancelled_without_issue_and_a_total_loss_cancels_an_issued_one()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var pending = (await issuer.CallAsync("generarRemito", Generate(issuer, 1, Holder))).Value("codRemito");
        var issued = (await issuer.CallAsync("generarRemito", Generate(issuer, 2, Issuer))).Value("codRemito");

        await issuer.CallAsync("anularRemito", issuer.Auth + $"<codRemito>{pending}</codRemito><observacion>Error de carga</observacion>");
        var cancelIssued = await issuer.CallAsync("anularRemito", issuer.Auth + $"<codRemito>{issued}</codRemito>");
        await issuer.CallAsync("informarContingencia", issuer.Auth +
            $"<codRemito>{issued}</codRemito><contingencia><codTipoContingencia>12</codTipoContingencia><fecha>2026-10-01</fecha></contingencia>");

        Assert.Equal("3070", cancelIssued.Error().Code);
        Assert.Equal("ANS", (await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{pending}</codRemito>")).Value("estadoRemito"));
        var lost = await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{issued}</codRemito>");
        Assert.Equal("ANU", lost.Value("estadoRemito"));
        Assert.Equal("12", lost.One("arrayContingencias").Value("codTipoContingencia"));
    }

    [Fact]
    public async Task What_was_not_accepted_is_redirected_in_a_new_issued_remito()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);
        var code = (await issuer.CallAsync("generarRemito", Generate(issuer, 1, Issuer))).Value("codRemito");
        await receiver.CallAsync("registrarRecepcion", receiver.Auth + $"<codRemito>{code}</codRemito><fecha>2026-10-02</fecha><aceptado>N</aceptado>");

        var redirected = await issuer.CallAsync("registrarRedestino", issuer.Auth +
            $"<idReqCliente>2</idReqCliente><codRemito>{code}</codRemito><cuitReceptor>{Depositary}</cuitReceptor><tipoDomReceptor>1</tipoDomReceptor>" +
            "<codDomReceptor>0</codDomReceptor><arrayRedestinoMercaderia><recepcionMercaderia><orden>2</orden><pesoNetoKG>300</pesoNetoKG></recepcionMercaderia></arrayRedestinoMercaderia>");

        Assert.NotEqual(code, redirected.Value("codRemito"));
        Assert.Equal("RED", redirected.Value("tipoMovimiento"));
        Assert.Equal(code, redirected.Value("codRemRedestinar"));
        Assert.Equal("EMI", redirected.Value("estadoRemito"));
        Assert.Equal(Depositary.ToString(), redirected.Value("cuitReceptor"));
        Assert.Single(redirected.Descendants("mercaderia"));
    }

    [Fact]
    public async Task The_tables_serve_the_documented_codes()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var types = await issuer.CallAsync("consultarTiposComprobante", issuer.Auth);
        var states = await issuer.CallAsync("consultarTiposEstado", issuer.Auth);
        var contingencies = await issuer.CallAsync("consultarTiposContingencia", issuer.Auth);

        Assert.Equal(["993", "994"], types.Descendants("codigo").Select(e => e.Value));
        Assert.Equal(18, states.Descendants("codigoDescripcionString").Count());
        Assert.Equal(["9", "10", "11", "12", "13", "14"], contingencies.Descendants("codigo").Select(e => e.Value));
    }

    [Fact]
    public async Task Aduanas_takes_the_ticket_loose_without_authRequest()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        await issuer.CallAsync("consultarAduanas", issuer.LooseAuth);
        var (status, body) = await issuer.PostAsync("consultarAduanas", $"<token>abc</token><sign>abc</sign><cuitRepresentada>{Issuer}</cuitRepresentada>");

        Assert.Equal(500, status);
        Assert.Contains("Fault", body);
    }

    private static string Generate(ServiceClient client, long requestId, long holder, long receiver = Receiver) =>
        client.Auth + $"<idReqCliente>{requestId}</idReqCliente>" +
        "<remito><tipoMovimiento>ENV</tipoMovimiento><tipoEmisor>I</tipoEmisor><puntoEmision>1</puntoEmision>" +
        $"<cuitTitular>{holder}</cuitTitular><depositario><tipoDepositario>E</tipoDepositario></depositario>" +
        $"<receptor><cuitPaisReceptor>55000002002</cuitPaisReceptor><receptorNacional><cuitReceptor>{receiver}</cuitReceptor>" +
        "<tipoDomReceptor>1</tipoDomReceptor><codDomReceptor>0</codDomReceptor></receptorNacional></receptor>" +
        "<viaje><transportista><codPaisTransportista>200</codPaisTransportista><transporteNacional>" +
        $"<cuitTransportista>{Holder}</cuitTransportista></transporteNacional></transportista>" +
        "<fechaInicioViaje>2026-10-01</fechaInicioViaje><distanciaKm>200</distanciaKm>" +
        "<vehiculo><automotor><dominioVehiculo>AB123CD</dominioVehiculo></automotor></vehiculo></viaje>" +
        "<arrayMercaderia>" + Goods(1, 500) + Goods(2, 300) + "</arrayMercaderia></remito>";

    private static string Goods(int order, int kilos) =>
        $"<mercaderia><orden>{order}</orden><codTipo>1</codTipo><codTipoEmb>1</codTipoEmb><cantidadEmb>10</cantidadEmb>" +
        $"<codTipoUnidad>1</codTipoUnidad><cantidadUnidad>{kilos}</cantidadUnidad><pesoNetoKg>{kilos}</pesoNetoKg></mercaderia>";
}
