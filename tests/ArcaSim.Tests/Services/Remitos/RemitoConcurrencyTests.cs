using System.Xml.Linq;
using static ArcaSim.Tests.Services.Remitos.RemitoTestKit;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>
/// Clients that call at once. A remito answers one operation at a time: of
/// several that try the same transition, exactly one takes it and the rest are
/// told it is not allowed, instead of each finding the state it started in.
/// </summary>
public class RemitoConcurrencyTests
{
    private const int Callers = 24;

    [Fact]
    public async Task Carne_issues_a_pending_remito_once_and_burns_no_number()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremcarne", Issuer);
        var holder = await ServiceClient.LoginAsync(sim, "wsremcarne", Holder);
        var code = (await issuer.CallAsync("generarRemito", CarneRemito(issuer, 1, Holder))).Value("codRemito");
        await holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");

        var answers = await AllAtOnce(() => issuer.CallAsync("emitirRemito", issuer.Auth + $"<codRemito>{code}</codRemito>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("3070", a.Error().Code));
        var next = await issuer.CallAsync("generarRemito", CarneRemito(issuer, 2, Issuer));
        Assert.Equal("2", next.Value("nroRemito"));
    }

    [Fact]
    public async Task Carne_lets_exactly_one_of_many_authorizations_through()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremcarne", Issuer);
        var holder = await ServiceClient.LoginAsync(sim, "wsremcarne", Holder);
        var code = (await issuer.CallAsync("generarRemito", CarneRemito(issuer, 1, Holder))).Value("codRemito");

        var answers = await AllAtOnce(() => holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("170", a.Error().Code));
    }

    [Fact]
    public async Task Carne_takes_one_reception_of_an_issued_remito()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremcarne", Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, "wsremcarne", Receiver);
        var code = (await issuer.CallAsync("generarRemito", CarneRemito(issuer, 1, Issuer))).Value("codRemito");

        var answers = await AllAtOnce(() => receiver.CallAsync("registrarRecepcion",
            receiver.Auth + $"<codRemito>{code}</codRemito><estado>ACE</estado><categoriaReceptor>1</categoriaReceptor>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        var history = await issuer.CallAsync("consultarEstadosRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["EMI", "ACE"], history.Descendants("estado").Select(e => e.Value));
    }

    [Fact]
    public async Task Harina_issues_a_pending_remito_once_and_burns_no_number()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremharina", Issuer);
        var holder = await ServiceClient.LoginAsync(sim, "wsremharina", Holder);
        var code = (await issuer.CallAsync("generarRemito", HarinaRemito(issuer, 1, Holder))).Value("codRemito");
        await holder.CallAsync("autorizarRemito", holder.Auth + $"<codRemito>{code}</codRemito><estado>A</estado>");

        var answers = await AllAtOnce(() => issuer.CallAsync("emitirRemito", issuer.Auth + $"<codRemito>{code}</codRemito>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("3070", a.Error().Code));
        var next = await issuer.CallAsync("generarRemito", HarinaRemito(issuer, 2, Issuer));
        Assert.Equal("2", next.Value("nroRemito"));
    }

    [Fact]
    public async Task Harina_takes_one_reception_of_an_issued_remito()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremharina", Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, "wsremharina", Receiver);
        var code = (await issuer.CallAsync("generarRemito", HarinaRemito(issuer, 1, Issuer))).Value("codRemito");

        var answers = await AllAtOnce(() => receiver.CallAsync("registrarRecepcion",
            receiver.Auth + $"<codRemito>{code}</codRemito><fecha>2026-10-02</fecha><aceptado>N</aceptado>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("3070", a.Error().Code));
    }

    [Fact]
    public async Task Azucar_issues_a_pending_remito_once_and_burns_no_number()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremazucar", Issuer);
        var holder = await ServiceClient.LoginAsync(sim, "wsremazucar", Holder);
        var code = (await issuer.CallAsync("generarRemito", AzucarRemito(issuer, 1, Holder))).Value("codigoRemito");
        await holder.CallAsync("autorizarRemitoTitular", holder.Auth +
            $"<autorizarRemitoTitular><codigoRemito>{code}</codigoRemito><autorizar>S</autorizar></autorizarRemitoTitular>");

        var answers = await AllAtOnce(() => issuer.CallAsync("emitirRemito",
            issuer.Auth + $"<emitirRemito><codigoRemito>{code}</codigoRemito></emitirRemito>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("3070", a.Error().Code));
        var next = await issuer.CallAsync("generarRemito", AzucarRemito(issuer, 2, Issuer));
        Assert.Equal("2", next.Value("nroComprobante"));
    }

    [Fact]
    public async Task Azucar_takes_one_reception_of_an_issued_remito()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremazucar", Issuer);
        var receiver = await ServiceClient.LoginAsync(sim, "wsremazucar", Receiver);
        var code = (await issuer.CallAsync("generarRemito", AzucarRemito(issuer, 1, Issuer))).Value("codigoRemito");

        var answers = await AllAtOnce(() => receiver.CallAsync("confirmarRecepcionMercaderia",
            receiver.Auth + $"<codigoRemito>{code}</codigoRemito><aceptaRecepcion>S</aceptaRecepcion>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("3070", a.Error().Code));
    }

    [Fact]
    public async Task Carne_accepts_a_remito_without_receiver_once_however_many_consult_it_after_the_deadline()
    {
        await using var sim = await StartAsync();
        var issuer = await ServiceClient.LoginAsync(sim, "wsremcarne", Issuer);
        var code = (await issuer.CallAsync("generarRemitoRecNoCateg", CarneRemito(issuer, 1, Issuer, uncategorized: true))).Value("codRemito");
        sim.Clock.Freeze(Now.AddDays(3));
        issuer = await ServiceClient.LoginAsync(sim, "wsremcarne", Issuer);

        var answers = await AllAtOnce(() => issuer.CallAsync("consultarRemito", issuer.Auth + $"<codRemito>{code}</codRemito>"));

        Assert.All(answers, a => Assert.Equal("ACE", a.Value("estado")));
        var history = await issuer.CallAsync("consultarEstadosRemito", issuer.Auth + $"<codRemito>{code}</codRemito>");
        Assert.Equal(["EMI", "ACE"], history.Descendants("estado").Select(e => e.Value));
    }

    /// <summary>The same call from <see cref="Callers"/> clients that start together.</summary>
    private static async Task<List<XElement>> AllAtOnce(Func<Task<XElement>> call)
    {
        using var start = new ManualResetEventSlim();
        var calls = Enumerable.Range(0, Callers).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return call();
        })).ToList();
        start.Set();
        return [.. await Task.WhenAll(calls)];
    }

    private static string CarneRemito(ServiceClient client, long requestId, long holder, bool uncategorized = false) =>
        client.Auth + $"<idReq>{requestId}</idReq><remito>" + (uncategorized ? "" : "<tipoComprobante>995</tipoComprobante>") + "<tipoMovimiento>ENV</tipoMovimiento>" +
        $"<categoriaEmisor>1</categoriaEmisor><puntoEmision>9000</puntoEmision><cuitTitularMercaderia>{holder}</cuitTitularMercaderia>" +
        (uncategorized
            ? "<tipoReceptor>MI</tipoReceptor><categoriaReceptor>2</categoriaReceptor><documentoReceptor>30111222</documentoReceptor><denomReceptor>Juan Carnicero</denomReceptor>" +
              "<domDestinoCalle>Rivadavia</domDestinoCalle><domDestinoNumero>100</domDestinoNumero><domDestinoCp>1000</domDestinoCp><domDestinoLoc>CABA</domDestinoLoc><domDestinoIdPcia>0</domDestinoIdPcia>"
            : $"<tipoReceptor>MI</tipoReceptor><categoriaReceptor>1</categoriaReceptor><cuitReceptor>{Receiver}</cuitReceptor><codDomDestino>0</codDomDestino>") +
        $"<viaje><cuitTransportista>{Holder}</cuitTransportista><fechaInicioViaje>2026-10-01</fechaInicioViaje><distanciaKm>50</distanciaKm>" +
        "<vehiculo><dominioVehiculo>AB123CD</dominioVehiculo></vehiculo></viaje>" +
        "<arrayMercaderias><mercaderia><orden>1</orden><codTipoProd>1.1</codTipoProd><tropa>123</tropa><kilos>1000</kilos><unidades>4</unidades></mercaderia></arrayMercaderias></remito>";

    private static string HarinaRemito(ServiceClient client, long requestId, long holder) =>
        client.Auth + $"<idReqCliente>{requestId}</idReqCliente>" +
        "<remito><tipoMovimiento>ENV</tipoMovimiento><tipoEmisor>I</tipoEmisor><puntoEmision>1</puntoEmision>" +
        $"<cuitTitular>{holder}</cuitTitular><depositario><tipoDepositario>E</tipoDepositario></depositario>" +
        $"<receptor><cuitPaisReceptor>55000002002</cuitPaisReceptor><receptorNacional><cuitReceptor>{Receiver}</cuitReceptor>" +
        "<tipoDomReceptor>1</tipoDomReceptor><codDomReceptor>0</codDomReceptor></receptorNacional></receptor>" +
        "<viaje><transportista><codPaisTransportista>200</codPaisTransportista><transporteNacional>" +
        $"<cuitTransportista>{Holder}</cuitTransportista></transporteNacional></transportista>" +
        "<fechaInicioViaje>2026-10-01</fechaInicioViaje><distanciaKm>200</distanciaKm>" +
        "<vehiculo><automotor><dominioVehiculo>AB123CD</dominioVehiculo></automotor></vehiculo></viaje>" +
        "<arrayMercaderia><mercaderia><orden>1</orden><codTipo>1</codTipo><codTipoEmb>1</codTipoEmb><cantidadEmb>10</cantidadEmb>" +
        "<codTipoUnidad>1</codTipoUnidad><cantidadUnidad>500</cantidadUnidad><pesoNetoKg>500</pesoNetoKg></mercaderia></arrayMercaderia></remito>";

    private static string AzucarRemito(ServiceClient client, long requestId, long holder) =>
        client.Auth + $"<idReqCliente>{requestId}</idReqCliente><remito><puntoEmision>1</puntoEmision>" +
        $"<cuitTitularMercaderia>{holder}</cuitTitularMercaderia><tipoTitularMercaderia>1</tipoTitularMercaderia>" +
        $"<receptor><cuitPaisReceptor>55000002002</cuitPaisReceptor><receptorNacional><cuitReceptor>{Receiver}</cuitReceptor></receptorNacional></receptor>" +
        "<viaje><fechaInicioViaje>2026-10-01</fechaInicioViaje><kmDistancia>100</kmDistancia><tramo><automotor><codPaisTransportista>200</codPaisTransportista>" +
        $"<transporteNacional><cuitTransportista>{Holder}</cuitTransportista><cuitConductor>{Depositary}</cuitConductor></transporteNacional>" +
        "<dominioVehiculo>AB123CD</dominioVehiculo></automotor></tramo></viaje>" +
        "<arrayMercaderias><mercaderia><orden>1</orden><anioZafra>2026</anioZafra><cantidad>1000</cantidad><tipoProducto>1</tipoProducto>" +
        "<unidadMedida>1</unidadMedida><tipoEmbalaje>1</tipoEmbalaje></mercaderia></arrayMercaderias></remito>";
}
