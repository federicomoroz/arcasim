using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using ArcaSim.Tests.Contract.Wsfev1;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Contract;

/// <summary>
/// The contract check of the design (docs/diseno.md §6): a client that
/// dotnet-svcutil generated from ARCA's own WSDL, which knows nothing of
/// ArcaSim, works against it over SOAP 1.1 and SOAP 1.2.
/// </summary>
public class WsdlClientTests
{
    [Theory]
    [InlineData(ServiceSoapClient.EndpointConfiguration.ServiceSoap)]
    [InlineData(ServiceSoapClient.EndpointConfiguration.ServiceSoap12)]
    public async Task The_client_generated_from_ARCA_s_WSDL_authorizes_a_voucher(ServiceSoapClient.EndpointConfiguration endpoint)
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        var ticket = await sim.TicketAsync(ArcaSimHarness.Issuer, "wsfe");
        var auth = new FEAuthRequest { Token = ticket.Token, Sign = ticket.Sign, Cuit = ArcaSimHarness.Issuer };
        // The bindings svcutil derives from ARCA's https addresses only accept https.
        var client = new ServiceSoapClient(endpoint, "https://localhost/wsfev1/service.asmx");
        client.Endpoint.EndpointBehaviors.Add(new TestServerBehavior(sim));

        var dummy = await client.FEDummyAsync();
        var vat = await client.FEParamGetTiposIvaAsync(auth);
        var last = await client.FECompUltimoAutorizadoAsync(auth, 1, 6);
        var authorized = await client.FECAESolicitarAsync(auth, new FECAERequest
        {
            FeCabReq = new FECAECabRequest { CantReg = 1, PtoVta = 1, CbteTipo = 6 },
            FeDetReq =
            [
                new FECAEDetRequest
                {
                    Concepto = 1, DocTipo = 99, DocNro = 0, CbteDesde = last.CbteNro + 1, CbteHasta = last.CbteNro + 1,
                    ImpTotal = 121, ImpNeto = 100, ImpIVA = 21, MonId = "PES", MonCotiz = 1, MonCotizSpecified = true,
                    CondicionIVAReceptorId = 5, CondicionIVAReceptorIdSpecified = true,
                    Iva = [new AlicIva { Id = 5, BaseImp = 100, Importe = 21 }],
                },
            ],
        });

        Assert.Equal("OK", dummy.AppServer);
        Assert.Equal(6, vat.ResultGet.Length);
        Assert.Equal(0, last.CbteNro);
        Assert.Equal("A", authorized.FeCabResp.Resultado);
        Assert.Matches("^[0-9]{14}$", authorized.FeDetResp[0].CAE);
        Assert.Null(authorized.Errors);
    }

    /// <summary>Routes WCF's HTTP requests to the in-memory server, through the hook WCF offers for its HttpClient handler.</summary>
    private sealed class TestServerBehavior(ArcaSimHarness sim) : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection parameters) =>
            parameters.Add(new Func<HttpClientHandler, HttpMessageHandler>(_ => sim.CreateHandler()));

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}
