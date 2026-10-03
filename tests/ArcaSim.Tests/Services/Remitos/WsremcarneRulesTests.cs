using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>The meat remito: its four generar variants, the two authorizations, the reception the receiver chooses, and every answer valid for the WSDL.</summary>
public class WsremcarneRulesTests
{
    private const string Service = "wsremcarne";

    [Fact]
    public async Task A_remito_is_issued_consulted_as_the_last_one_and_partly_accepted_by_the_receiver()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);

        var generated = await issuer.CallAsync("generarRemito", Generate(issuer, 1));
        var code = generated.Value("codRemito");
        Assert.Equal("A", generated.Value("resultado"));
        Assert.Equal("EMI", generated.Value("estado"));
        Assert.Equal("995", generated.Value("tipoComprobante"));
        Assert.Equal("1", generated.Value("nroRemito"));
        Assert.Equal("2026-10-03-03:00", generated.Value("fechaVencimiento"));

        var last = await issuer.CallAsync("consultarUltimoRemitoEmitido", issuer.Auth + "<tipoComprobante>995</tipoComprobante><puntoEmision>9000</puntoEmision>");
        Assert.Equal(code, last.Value("codRemito"));

        await receiver.CallAsync("registrarRecepcion", receiver.Auth +
            $"<codRemito>{code}</codRemito><estado>ACP</estado><arrayRecepcionMercaderia><recepcionMercaderia>" +
            "<orden>1</orden><kilos>800</kilos><unidades>3</unidades></recepcionMercaderia></arrayRecepcionMercaderia><categoriaReceptor>1</categoriaReceptor>");

        var consulted = await issuer.CallAsync("consultarRemito", issuer.Auth + "<idReq>1</idReq><puntoEmision>9000</puntoEmision>");
        Assert.Equal("ACP", consulted.Value("estado"));
        Assert.Equal("800", consulted.Value("kilosRec"));
        Assert.Equal("3", consulted.Value("unidadesRec"));
        Assert.Equal("1", consulted.Value("idReq"));
        Assert.True(consulted.Has("qr"));

        var history = await receiver.CallAsync("consultarEstadosRemito", receiver.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["EMI", "ACP"], history.Descendants("estado").Select(e => e.Value));
    }

    [Fact]
    public async Task The_holder_then_the_depositary_authorize_and_only_they_can()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var holder = await ServiceClient.LoginAsync(sim, Service, Holder);
        var depositary = await ServiceClient.LoginAsync(sim, Service, Depositary);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);

        var generated = await issuer.CallAsync("generarRemito", Generate(issuer, 1, holder: Holder, depositary: Depositary));
        var code = generated.Value("codRemito");
        Assert.Equal("PAT", generated.Value("estado"));

        var stranger = await receiver.CallAsync("autorizarRemito", receiver.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");
        Assert.Equal(("2201", "La CUIT no es un autorizador válido para el remito"), stranger.Error());

        await holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");
        Assert.Equal("PAD", (await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{code}</codRemito>")).Value("estado"));
        await depositary.CallAsync("autorizarRemito", depositary.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");
        var issued = await issuer.CallAsync("emitirRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        var again = await holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");

        Assert.Equal("EMI", issued.Value("estado"));
        Assert.Equal(("170", "El remito debe encontrarse pendiente de autorización"), again.Error());
        var authorized = await depositary.CallAsync("consultarRemitosAutorizador", depositary.Auth + "<rolAutorizador>DEP</rolAutorizador><estadoAutorizacion>AU</estadoAutorizacion>");
        Assert.Equal(code, authorized.Value("codRemito"));
    }

    [Fact]
    public async Task A_movement_other_than_RED_must_carry_its_trip_and_category()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var answer = await issuer.CallAsync("generarRemito", issuer.Auth + "<idReq>1</idReq><remito><tipoComprobante>995</tipoComprobante>" +
            $"<tipoMovimiento>ENV</tipoMovimiento><puntoEmision>9000</puntoEmision><cuitReceptor>{Receiver}</cuitReceptor>" +
            "<arrayMercaderias><mercaderia><orden>1</orden><codTipoProd>6.24</codTipoProd><kilos>10</kilos></mercaderia></arrayMercaderias></remito>");

        Assert.Equal("R", answer.Value("resultado"));
        Assert.Equal(["1206", "1207", "1208"], answer.One("arrayErrores").Descendants("codigo").Select(e => e.Value));
        Assert.False(answer.Has("codRemito"));
    }

    [Fact]
    public async Task Sending_the_same_idReq_again_answers_the_remito_already_created()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var first = await issuer.CallAsync("generarRemito", Generate(issuer, 5));
        var again = await issuer.CallAsync("generarRemito", Generate(issuer, 5));

        Assert.Equal(first.Value("codRemito"), again.Value("codRemito"));
        Assert.Equal("1", again.Value("nroRemito"));
    }

    [Fact]
    public async Task A_remito_to_a_receiver_without_CUIT_is_accepted_once_it_expires()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var generated = await issuer.CallAsync("generarRemitoRecNoCateg", issuer.Auth + "<idReq>1</idReq><remito><tipoMovimiento>ENV</tipoMovimiento>" +
            $"<categoriaEmisor>1</categoriaEmisor><puntoEmision>9000</puntoEmision><cuitTitularMercaderia>{Issuer}</cuitTitularMercaderia>" +
            "<tipoReceptor>MI</tipoReceptor><categoriaReceptor>2</categoriaReceptor><documentoReceptor>30111222</documentoReceptor>" +
            "<denomReceptor>Juan Carnicero</denomReceptor><domDestinoCalle>Rivadavia</domDestinoCalle><domDestinoNumero>100</domDestinoNumero>" +
            "<domDestinoCp>1000</domDestinoCp><domDestinoLoc>CABA</domDestinoLoc><domDestinoIdPcia>0</domDestinoIdPcia>" + Trip() + Goods() + "</remito>");
        var code = generated.Value("codRemito");
        Assert.Equal("GenerarRemitoRecNoCategResponse", generated.Name.LocalName);

        sim.Clock.Freeze(Now.AddDays(3));
        issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var consulted = await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");

        Assert.Equal("ACE", consulted.Value("estado"));
        Assert.Equal("995", consulted.Value("tipoComprobante"));
    }

    [Fact]
    public async Task The_importe_COT_shows_in_consultarRemitoImporte_and_a_contingency_cancels()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var code = (await issuer.CallAsync("generarRemitoImporte", Generate(issuer, 1, importe: "150000.50"))).Value("codRemito");

        await issuer.CallAsync("informarContingencia", issuer.Auth +
            $"<codRemito>{code}</codRemito><contingencia><tipoContingencia>1</tipoContingencia><observacion>Rotura del camion</observacion></contingencia>");
        var withAmount = await issuer.CallAsync("consultarRemitoImporte", issuer.Auth + $"<codRemito>{code}</codRemito>");
        var plain = await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");

        Assert.Equal("150000.50", withAmount.Value("importeCot"));
        Assert.False(plain.Has("importeCot"));
        Assert.Equal("ANU", plain.Value("estado"));
        Assert.Equal("Rotura del camion", plain.Value("observacion"));
    }

    [Fact]
    public async Task The_tables_serve_type_995_and_the_family_states()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var types = await issuer.CallAsync("consultarTiposComprobante", issuer.Auth);
        var points = await issuer.CallAsync("consultarPuntosEmision", issuer.Auth);
        var addresses = await issuer.CallAsync("consultarCodigosDomicilio", issuer.Auth + $"<cuitTitularDomicilio>{Receiver}</cuitTitularDomicilio>");

        Assert.Equal("995", types.Value("codigo"));
        Assert.Equal("1", points.Value("codigo"));
        Assert.Equal("0", addresses.Value("codigo"));
    }

    private static string Generate(ServiceClient client, long requestId, long holder = Issuer, long? depositary = null, string? importe = null) =>
        client.Auth + $"<idReq>{requestId}</idReq><remito><tipoComprobante>995</tipoComprobante><tipoMovimiento>ENV</tipoMovimiento>" +
        $"<categoriaEmisor>1</categoriaEmisor><puntoEmision>9000</puntoEmision><cuitTitularMercaderia>{holder}</cuitTitularMercaderia>" +
        (depositary is null ? "" : $"<cuitDepositario>{depositary}</cuitDepositario>") +
        $"<tipoReceptor>MI</tipoReceptor><categoriaReceptor>1</categoriaReceptor><cuitReceptor>{Receiver}</cuitReceptor>" +
        (depositary is null ? "" : "<codDomOrigen>0</codDomOrigen>") + "<codDomDestino>0</codDomDestino>" + Trip() + Goods() +
        (importe is null ? "" : $"<importeCot>{importe}</importeCot>") + "</remito>";

    private static string Trip() =>
        $"<viaje><cuitTransportista>{Holder}</cuitTransportista><fechaInicioViaje>2026-10-01</fechaInicioViaje><distanciaKm>50</distanciaKm>" +
        "<vehiculo><dominioVehiculo>AB123CD</dominioVehiculo></vehiculo></viaje>";

    private static string Goods() =>
        "<arrayMercaderias><mercaderia><orden>1</orden><codTipoProd>1.1</codTipoProd><tropa>123</tropa><kilos>1000</kilos><unidades>4</unidades></mercaderia></arrayMercaderias>";
}
