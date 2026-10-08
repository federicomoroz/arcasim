using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Services.Remitos;

/// <summary>
/// One party calling one of the group's services over HTTP with its own
/// ticket, the way a client generated from the WSDL would, and every answer
/// checked against that WSDL.
/// </summary>
public sealed class ServiceClient
{
    private readonly ArcaSimHarness _sim;
    private readonly ServiceContract _contract;

    private ServiceClient(ArcaSimHarness sim, ServiceContract contract, long cuit, string token, string sign)
    {
        _sim = sim;
        _contract = contract;
        Cuit = cuit;
        Token = token;
        Sign = sign;
    }

    public long Cuit { get; }
    public string Token { get; }
    public string Sign { get; }

    /// <summary>The authRequest block of the remitos.</summary>
    public string Auth => $"<authRequest><token>{Token}</token><sign>{Sign}</sign><cuitRepresentada>{Cuit}</cuitRepresentada></authRequest>";

    /// <summary>The same three fields loose, for the requests declared as AuthRequestType itself.</summary>
    public string LooseAuth => $"<token>{Token}</token><sign>{Sign}</sign><cuitRepresentada>{Cuit}</cuitRepresentada>";

    public string Namespace => _contract.TargetNamespace;

    public static async Task<ServiceClient> LoginAsync(ArcaSimHarness sim, string service, long cuit)
    {
        var contract = ContractOf(service);
        var certificate = await sim.IssueCertificateAsync(cuit, $"{service}-{cuit}", service);
        var ticket = await sim.Wsaa(cuit, certificate).LoginAsync(service);
        return new ServiceClient(sim, contract, cuit, ticket.Token, ticket.Sign);
    }

    /// <summary>Posts the operation's element with the inner XML given, and returns the Body's element after checking it against the WSDL.</summary>
    public async Task<XElement> CallAsync(string operation, string inner, string? element = null, string prefix = "ns")
    {
        var (status, body) = await PostAsync(operation, inner, element, prefix);
        Assert.True(status == 200, body);
        var answer = Soap.Body(body);
        Xsd.AssertValid(answer, _contract);
        return answer;
    }

    public Task<(int Status, string Body)> PostAsync(string operation, string inner, string? element = null, string prefix = "ns", CancellationToken ct = default)
    {
        var name = element ?? operation + "Request";
        var envelope = Soap.Envelope($"<{prefix}:{name}>{inner}</{prefix}:{name}>", (prefix, Namespace));
        return _sim.PostSoapAsync(new Uri("http://localhost" + _contract.AddressPath), envelope, $"\"{Namespace}{operation}\"", ct);
    }

    private static ServiceContract ContractOf(string service) => Contracts.Of($"{service}-homologacion.wsdl");

    /// <summary>The answer is valid for the WSDL ARCA publishes: what a generated client deserializes.</summary>
    public static void Validate(string service, XElement answer) => Xsd.AssertValid(answer, ContractOf(service));
}

public static class RemitoTestKit
{
    /// <summary>A Thursday, 01/10/2026 at noon in Argentina: every date in the group's tests counts from here.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    public const long Issuer = ArcaSimHarness.Issuer;
    public const long Holder = 30000000007;
    public const long Receiver = 20222222223;
    public const long Depositary = 27333333339;

    /// <summary>ArcaSim frozen at <see cref="Now"/> with the issuer, a holder, a receiver and a depositary registered.</summary>
    public static async Task<ArcaSimHarness> StartAsync()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(Now);
        await sim.PutTaxpayerAsync(Issuer, "Molino de Prueba SA", VatCondition.ResponsableInscripto, new PointOfSale(1, PointOfSaleKind.WebServiceCae));
        await sim.PutTaxpayerAsync(Holder, "Titular de Mercaderia SA", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Receiver, "Ana Panadera", VatCondition.Monotributo);
        await sim.PutTaxpayerAsync(Depositary, "Deposito Tercero", VatCondition.ResponsableInscripto);
        return sim;
    }

    // ---- A remito of each service, written the way its manual shows it ---------------------

    public static string CarneRemito(ServiceClient client, long requestId, long holder, bool uncategorized = false) =>
        client.Auth + $"<idReq>{requestId}</idReq><remito>" + (uncategorized ? "" : "<tipoComprobante>995</tipoComprobante>") + "<tipoMovimiento>ENV</tipoMovimiento>" +
        $"<categoriaEmisor>1</categoriaEmisor><puntoEmision>9000</puntoEmision><cuitTitularMercaderia>{holder}</cuitTitularMercaderia>" +
        (uncategorized
            ? "<tipoReceptor>MI</tipoReceptor><categoriaReceptor>2</categoriaReceptor><documentoReceptor>30111222</documentoReceptor><denomReceptor>Juan Carnicero</denomReceptor>" +
              "<domDestinoCalle>Rivadavia</domDestinoCalle><domDestinoNumero>100</domDestinoNumero><domDestinoCp>1000</domDestinoCp><domDestinoLoc>CABA</domDestinoLoc><domDestinoIdPcia>0</domDestinoIdPcia>"
            : $"<tipoReceptor>MI</tipoReceptor><categoriaReceptor>1</categoriaReceptor><cuitReceptor>{Receiver}</cuitReceptor><codDomDestino>0</codDomDestino>") +
        $"<viaje><cuitTransportista>{Holder}</cuitTransportista><fechaInicioViaje>2026-10-01</fechaInicioViaje><distanciaKm>50</distanciaKm>" +
        "<vehiculo><dominioVehiculo>AB123CD</dominioVehiculo></vehiculo></viaje>" +
        "<arrayMercaderias><mercaderia><orden>1</orden><codTipoProd>1.1</codTipoProd><tropa>123</tropa><kilos>1000</kilos><unidades>4</unidades></mercaderia></arrayMercaderias></remito>";

    public static string HarinaRemito(ServiceClient client, long requestId, long holder) =>
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

    public static string AzucarRemito(ServiceClient client, long requestId, long holder) =>
        client.Auth + $"<idReqCliente>{requestId}</idReqCliente><remito><puntoEmision>1</puntoEmision>" +
        $"<cuitTitularMercaderia>{holder}</cuitTitularMercaderia><tipoTitularMercaderia>1</tipoTitularMercaderia>" +
        $"<receptor><cuitPaisReceptor>55000002002</cuitPaisReceptor><receptorNacional><cuitReceptor>{Receiver}</cuitReceptor></receptorNacional></receptor>" +
        "<viaje><fechaInicioViaje>2026-10-01</fechaInicioViaje><kmDistancia>100</kmDistancia><tramo><automotor><codPaisTransportista>200</codPaisTransportista>" +
        $"<transporteNacional><cuitTransportista>{Holder}</cuitTransportista><cuitConductor>{Depositary}</cuitConductor></transporteNacional>" +
        "<dominioVehiculo>AB123CD</dominioVehiculo></automotor></tramo></viaje>" +
        "<arrayMercaderias><mercaderia><orden>1</orden><anioZafra>2026</anioZafra><cantidad>1000</cantidad><tipoProducto>1</tipoProducto>" +
        "<unidadMedida>1</unidadMedida><tipoEmbalaje>1</tipoEmbalaje></mercaderia></arrayMercaderias></remito>";

    /// <summary>The first child with that local name, anywhere below.</summary>
    public static XElement One(this XElement element, string name) =>
        element.Descendants().FirstOrDefault(e => e.Name.LocalName == name) ?? throw new Xunit.Sdk.XunitException($"No <{name}> in {element}");

    public static string Value(this XElement element, string name) => element.One(name).Value;

    public static bool Has(this XElement element, string name) => element.Descendants().Any(e => e.Name.LocalName == name);

    /// <summary>The (codigo, descripcion) pairs of the answer's error block.</summary>
    public static (string Code, string Text) Error(this XElement element) =>
        element.One("arrayErrores").Elements().Select(e => (e.Elements().First(c => c.Name.LocalName == "codigo").Value,
            e.Elements().First(c => c.Name.LocalName == "descripcion").Value)).First();
}
