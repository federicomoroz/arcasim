using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>The sugar remito: issue, the holder's authorization, the receiver's confirmation and the issuer's validation, every answer valid for the WSDL.</summary>
public class WsremazucarRulesTests
{
    private const string Service = "wsremazucar";

    [Fact]
    public async Task A_partial_reception_waits_for_the_issuers_validation_before_new_remitos_to_that_receiver()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);

        var generated = await issuer.CallAsync("generarRemito", Generate(issuer, 1));
        var code = generated.Value("codigoRemito");
        Assert.Equal("A", generated.Value("resultado"));
        Assert.Equal("EMI", generated.Value("estado"));
        Assert.Equal("1", generated.Value("nroComprobante"));
        Assert.Equal("997", generated.Value("idTipoComprobante"));
        Assert.Equal("2026-10-03-03:00", generated.Value("fechaVencimiento"));

        await receiver.CallAsync("confirmarRecepcionMercaderia", receiver.Auth + $"<codigoRemito>{code}</codigoRemito>" +
            "<arrayMercaderiaRecibida><mercaderia><orden>1</orden><cantidad>600</cantidad></mercaderia></arrayMercaderiaRecibida><aceptaRecepcion>S</aceptaRecepcion>");
        await issuer.CallAsync("convalidarEmisor", issuer.Auth +
            $"<convalidaRechazoReceptor><codigoRemito>{code}</codigoRemito><convalida>N</convalida></convalidaRechazoReceptor>");

        var blocked = await issuer.CallAsync("generarRemito", Generate(issuer, 2));
        Assert.Equal(("7014", "Usted posee remitos electrónicos pendientes de convalidación o \"No convalidados\" con el receptor de la mercadería"), blocked.Error());

        await issuer.CallAsync("corregirConvalidacionEmisor", issuer.Auth + $"<codRemito>{code}</codRemito>", "CorregirConvalidacionEmisorRequest");
        var next = await issuer.CallAsync("generarRemito", Generate(issuer, 2));
        Assert.Equal("2", next.Value("nroComprobante"));

        var consulted = await receiver.CallAsync("consultarRemito", receiver.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal("CON", consulted.Value("estado"));
        Assert.Equal("1000", consulted.Value("cantidadEnviada"));
        Assert.Equal("600", consulted.Value("cantidadRecibida"));
        Assert.Equal("1", consulted.Value("idRequest"));

        var history = await issuer.CallAsync("consultarEstadosRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["EMI", "ACP", "NCO", "CON"], history.Descendants("estado").Select(e => e.Value));
        Assert.Equal(["N", "N", "N", "S"], history.Descendants("activo").Select(e => e.Value));
    }

    [Fact]
    public async Task Someone_elses_sugar_waits_for_the_holder_and_is_issued_with_a_new_driver()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var holder = await ServiceClient.LoginAsync(sim, Service, Holder);

        var generated = await issuer.CallAsync("generarRemito", Generate(issuer, 1, holder: Holder));
        var code = generated.Value("codigoRemito");
        Assert.Equal("PAT", generated.Value("estado"));
        Assert.False(generated.Has("nroComprobante"));

        await holder.CallAsync("autorizarRemitoTitular", holder.Auth +
            $"<autorizarRemitoTitular><codigoRemito>{code}</codigoRemito><autorizar>S</autorizar></autorizarRemitoTitular>");
        var issued = await issuer.CallAsync("emitirRemito", issuer.Auth + $"<emitirRemito><codigoRemito>{code}</codigoRemito><transporte><conductor>" +
            $"<conductorNacional><cuitConductor>{Receiver}</cuitConductor></conductorNacional></conductor><dominioVehiculo>ZZ999ZZ</dominioVehiculo></transporte></emitirRemito>");
        Assert.Equal("EMI", issued.Value("estado"));
        Assert.Equal("1", issued.Value("nroComprobante"));

        var byIssuer = await issuer.CallAsync("consultarRemito", issuer.Auth + "<idReqCliente>1</idReqCliente><puntoEmision>1</puntoEmision>");
        Assert.Equal("ZZ999ZZ", byIssuer.Value("dominioVehiculo"));
        Assert.Equal(Receiver.ToString(), byIssuer.Value("cuitConductor"));

        var list = await holder.CallAsync("consultarRemitosTitular", holder.Auth + "<fechaDesde>2026-10-01</fechaDesde><fechaHasta>2026-10-01</fechaHasta>");
        Assert.Equal(code, list.Value("codigoRemito"));
        Assert.Equal("1", list.Value("maxRegistros"));
        Assert.Equal("1", list.Value("maxPaginas"));
    }

    [Fact]
    public async Task A_repeated_request_id_gets_151_and_a_future_harvest_7102()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        await issuer.CallAsync("generarRemito", Generate(issuer, 3));

        var repeated = await issuer.CallAsync("generarRemito", Generate(issuer, 3));
        var future = await issuer.CallAsync("generarRemito", Generate(issuer, 4, harvest: 2030));

        Assert.Equal(("151", "El ID de request 3 ya existe para el punto de emisión 1"), repeated.Error());
        Assert.Equal("R", repeated.Value("resultado"));
        Assert.Equal(("7102", "El año no puede ser posterior al actual, ni anterior a 10 años"), future.Error());
    }

    [Fact]
    public async Task More_received_than_sent_gets_7108_and_a_contingency_cancels_with_the_loss_on_record()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, Service, Receiver);
        var code = (await issuer.CallAsync("generarRemito", Generate(issuer, 1))).Value("codigoRemito");

        var excess = await receiver.CallAsync("confirmarRecepcionMercaderia", receiver.Auth + $"<codigoRemito>{code}</codigoRemito>" +
            "<arrayMercaderiaRecibida><mercaderia><orden>1</orden><cantidad>5000</cantidad></mercaderia></arrayMercaderiaRecibida><aceptaRecepcion>S</aceptaRecepcion>");
        await issuer.CallAsync("informarContingencia", issuer.Auth + $"<informarContingencia><codigoRemito>{code}</codigoRemito><tipoContingencia>1</tipoContingencia>" +
            "<arrayMercaderiaPerdida><mercaderia><orden>1</orden><cantidad>200</cantidad></mercaderia></arrayMercaderiaPerdida><observaciones>Vuelco</observaciones></informarContingencia>");

        Assert.Equal("7108", excess.Error().Code);
        var consulted = await issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal("ANU", consulted.Value("estado"));
        Assert.Equal("200", consulted.Value("cantidadPerdida"));
        Assert.Equal("Vuelco", consulted.One("arrayContingencias").Value("observaciones"));
    }

    [Fact]
    public async Task Tipos_titular_takes_the_ticket_loose_and_serves_the_three_kinds()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, Service, Issuer);

        var kinds = await issuer.CallAsync("consultarTiposTitular", issuer.LooseAuth);
        var (status, body) = await issuer.PostAsync("consultarTiposTitular", $"<token>abc</token><sign>abc</sign><cuitRepresentada>{Issuer}</cuitRepresentada>");

        Assert.Equal(["1", "2", "3"], kinds.Descendants("codigo").Select(e => e.Value));
        Assert.Equal(500, status);
        Assert.Contains("Fault", body);
    }

    private static string Generate(ServiceClient client, long requestId, long holder = Issuer, int harvest = 2026) =>
        client.Auth + $"<idReqCliente>{requestId}</idReqCliente><remito><puntoEmision>1</puntoEmision>" +
        $"<cuitTitularMercaderia>{holder}</cuitTitularMercaderia><tipoTitularMercaderia>1</tipoTitularMercaderia>" +
        $"<receptor><cuitPaisReceptor>55000002002</cuitPaisReceptor><receptorNacional><cuitReceptor>{Receiver}</cuitReceptor></receptorNacional></receptor>" +
        "<viaje><fechaInicioViaje>2026-10-01</fechaInicioViaje><kmDistancia>100</kmDistancia><tramo><automotor><codPaisTransportista>200</codPaisTransportista>" +
        $"<transporteNacional><cuitTransportista>{Holder}</cuitTransportista><cuitConductor>{Depositary}</cuitConductor></transporteNacional>" +
        "<dominioVehiculo>AB123CD</dominioVehiculo></automotor></tramo></viaje>" +
        $"<arrayMercaderias><mercaderia><orden>1</orden><anioZafra>{harvest}</anioZafra><cantidad>1000</cantidad><tipoProducto>1</tipoProducto>" +
        "<unidadMedida>1</unidadMedida><tipoEmbalaje>1</tipoEmbalaje></mercaderia></arrayMercaderias></remito>";
}
