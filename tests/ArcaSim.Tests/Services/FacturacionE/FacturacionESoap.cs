using System.Net.Http.Json;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using ArcaSim.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.FacturacionE;

/// <summary>
/// Calls to wsfexv1 and wscdc as their manuals write them (every element in
/// the service's namespace), with a real ticket, and every answer checked
/// against the WSDL ARCA publishes.
/// </summary>
internal sealed class FacturacionESoap
{
    public const long Issuer = ArcaSimHarness.Issuer;
    public static readonly DateOnly Today = new(2026, 10, 1);

    public static readonly ServiceSpec Fex = new("wsfexv1-homologacion.wsdl", "http://ar.gov.afip.dif.fexv1/", "wsfex");
    public static readonly ServiceSpec Cdc = new("wscdc-homologacion.wsdl", "http://servicios1.afip.gob.ar/wscdc/", "wscdc");

    public sealed record ServiceSpec(string Wsdl, string Namespace, string WsaaService)
    {
        public ServiceContract Contract => Contracts.Of(Wsdl);

        public XNamespace Ns => Namespace;
    }

    private readonly ArcaSimHarness _sim;
    private readonly Arca.Client.WsaaClient _wsaa;

    private FacturacionESoap(ArcaSimHarness sim, Arca.Client.WsaaClient wsaa)
    {
        _sim = sim;
        _wsaa = wsaa;
    }

    /// <summary>A client of the issuer authorized for wsfex and wscdc, on ArcaSim with the issuer registered (point of sale 1 for CAE).</summary>
    public static async Task<FacturacionESoap> ForAsync(ArcaSimHarness sim, long cuit = Issuer)
    {
        var certificate = await sim.IssueCertificateAsync(cuit, "exportacion", "wsfex", "wscdc");
        return new FacturacionESoap(sim, sim.Wsaa(cuit, certificate));
    }

    /// <summary>Calls an operation; the answer must be HTTP 200 and valid for the WSDL. Returns the operation's Result element.</summary>
    public async Task<XElement> CallAsync(ServiceSpec service, string operation, string inner, long cuit = Issuer)
    {
        var ticket = await _wsaa.LoginAsync(service.WsaaService);
        var auth = operation == "FEXGetLast_CMP" ? "" : $"<s:Auth><s:Token>{ticket.Token}</s:Token><s:Sign>{ticket.Sign}</s:Sign><s:Cuit>{cuit}</s:Cuit></s:Auth>";
        inner = inner.Replace("{token}", ticket.Token).Replace("{sign}", ticket.Sign);
        var envelope = Soap.Envelope($"<s:{operation}>{auth}{inner}</s:{operation}>", ("s", service.Namespace));
        var (status, body) = await _sim.PostSoapAsync(new Uri("http://localhost" + service.Contract.AddressPath), envelope, $"\"{service.Namespace}{operation}\"");

        Assert.True(status == 200, body);
        var answer = Soap.Body(body);
        Assert.Equal(service.Ns + (operation + "Response"), answer.Name);
        Xsd.AssertValid(answer, service.Contract);
        return answer.Elements().Single();
    }

    public static async Task<(ArcaSimHarness Sim, Arca.Client.WsfeClient Wsfe, FacturacionESoap Soap)> StartAsync()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        return (sim, wsfe, await ForAsync(sim));
    }

    public static Task SetRateAsync(ArcaSimHarness sim, string currency, DateOnly day, decimal rate) =>
        sim.Services.GetRequiredService<IExchangeRates>().SetAsync(currency, day, rate);

    /// <summary>Registers (or replaces) one of the issuer's CAE points of sale, with the day ARCA will deactivate it.</summary>
    public static async Task PutPointOfSaleAsync(ArcaSimHarness sim, int number, DateOnly deactivatedOn)
    {
        var response = await sim.Http.PutAsJsonAsync($"/arcasim/api/taxpayers/{Issuer}", new
        {
            name = "Empresa de Prueba SA",
            vatCondition = "ResponsableInscripto",
            active = true,
            pointsOfSale = new[] { new { number, kind = "WebServiceCae", blocked = false, deactivatedOn } },
        });
        response.EnsureSuccessStatusCode();
    }

    // ---- WSFEXv1 requests ----------------------------------------------------------

    /// <summary>A goods export invoice (19, Tipo_expo 1) to a Brazilian buyer: 10 tonnes of soy at USD 100, without the permit yet.</summary>
    public static string Export(long id, long number, int pointOfSale = 1, string total = "1000", string rate = "1450.5") =>
        $"<s:Cmp><s:Id>{id}</s:Id><s:Fecha_cbte>20261001</s:Fecha_cbte><s:Cbte_Tipo>19</s:Cbte_Tipo>" +
        $"<s:Punto_vta>{pointOfSale}</s:Punto_vta><s:Cbte_nro>{number}</s:Cbte_nro><s:Tipo_expo>1</s:Tipo_expo>" +
        "<s:Permiso_existente>N</s:Permiso_existente><s:Dst_cmp>203</s:Dst_cmp><s:Cliente>ACME TRADING LTDA</s:Cliente>" +
        "<s:Cuit_pais_cliente>50000000016</s:Cuit_pais_cliente><s:Domicilio_cliente>Av. Paulista 1000, Sao Paulo</s:Domicilio_cliente>" +
        $"<s:Moneda_Id>DOL</s:Moneda_Id><s:Moneda_ctz>{rate}</s:Moneda_ctz><s:Imp_total>{total}</s:Imp_total>" +
        "<s:Forma_pago>Transferencia 30 dias</s:Forma_pago><s:Incoterms>FOB</s:Incoterms><s:Idioma_cbte>1</s:Idioma_cbte>" +
        Items + "</s:Cmp>";

    /// <summary>A credit note (21) for goods, against the issuer's invoice 19 number <paramref name="invoice"/> on point of sale 1.</summary>
    public static string CreditNote(long id, long number, long invoice) =>
        $"<s:Cmp><s:Id>{id}</s:Id><s:Fecha_cbte>20261001</s:Fecha_cbte><s:Cbte_Tipo>21</s:Cbte_Tipo>" +
        $"<s:Punto_vta>1</s:Punto_vta><s:Cbte_nro>{number}</s:Cbte_nro><s:Tipo_expo>1</s:Tipo_expo>" +
        "<s:Dst_cmp>203</s:Dst_cmp><s:Cliente>ACME TRADING LTDA</s:Cliente>" +
        "<s:Cuit_pais_cliente>50000000016</s:Cuit_pais_cliente><s:Domicilio_cliente>Av. Paulista 1000, Sao Paulo</s:Domicilio_cliente>" +
        "<s:Moneda_Id>DOL</s:Moneda_Id><s:Moneda_ctz>1450.5</s:Moneda_ctz><s:Imp_total>1000</s:Imp_total>" +
        $"<s:Cmps_asoc><s:Cmp_asoc><s:Cbte_tipo>19</s:Cbte_tipo><s:Cbte_punto_vta>1</s:Cbte_punto_vta><s:Cbte_nro>{invoice}</s:Cbte_nro>" +
        $"<s:Cbte_cuit>{Issuer}</s:Cbte_cuit></s:Cmp_asoc></s:Cmps_asoc><s:Idioma_cbte>1</s:Idioma_cbte>" +
        Items + "</s:Cmp>";

    private const string Items =
        "<s:Items><s:Item><s:Pro_codigo>SOJA</s:Pro_codigo><s:Pro_ds>Soja en grano</s:Pro_ds><s:Pro_qty>10</s:Pro_qty>" +
        "<s:Pro_umed>29</s:Pro_umed><s:Pro_precio_uni>100</s:Pro_precio_uni><s:Pro_bonificacion>0</s:Pro_bonificacion>" +
        "<s:Pro_total_item>1000</s:Pro_total_item></s:Item></s:Items>";

    /// <summary>FEXGetLast_CMP's body: its Auth carries the point of sale and type.</summary>
    public static string LastCmp(int pointOfSale, int type) =>
        $"<s:Auth><s:Token>{{token}}</s:Token><s:Sign>{{sign}}</s:Sign><s:Cuit>{Issuer}</s:Cuit>" +
        $"<s:Pto_venta>{pointOfSale}</s:Pto_venta><s:Cbte_Tipo>{type}</s:Cbte_Tipo></s:Auth>";

    public static string GetCmp(int pointOfSale, int type, long number) =>
        $"<s:Cmp><s:Cbte_tipo>{type}</s:Cbte_tipo><s:Punto_vta>{pointOfSale}</s:Punto_vta><s:Cbte_nro>{number}</s:Cbte_nro></s:Cmp>";

    // ---- WSCDC requests ------------------------------------------------------------

    /// <summary>ComprobanteConstatar's CmpReq; the receiver's document goes only when given.</summary>
    public static string Constatar(
        string mode, long cuit, int pointOfSale, int type, long number, string date, string total, string code,
        string? docType = null, string? docNumber = null) =>
        $"<s:CmpReq><s:CbteModo>{mode}</s:CbteModo><s:CuitEmisor>{cuit}</s:CuitEmisor><s:PtoVta>{pointOfSale}</s:PtoVta>" +
        $"<s:CbteTipo>{type}</s:CbteTipo><s:CbteNro>{number}</s:CbteNro><s:CbteFch>{date}</s:CbteFch>" +
        $"<s:ImpTotal>{total}</s:ImpTotal><s:CodAutorizacion>{code}</s:CodAutorizacion>" +
        (docType is null ? "" : $"<s:DocTipoReceptor>{docType}</s:DocTipoReceptor>") +
        (docNumber is null ? "" : $"<s:DocNroReceptor>{docNumber}</s:DocNroReceptor>") +
        "</s:CmpReq>";
}

internal static class ResultXml
{
    public static string Value(this XElement element, string path)
    {
        var current = element;
        foreach (var name in path.Split('/'))
            current = current.Elements().FirstOrDefault(e => e.Name.LocalName == name)
                      ?? throw new Xunit.Sdk.XunitException($"No {name} in {element}");
        return current.Value;
    }

    public static XElement? Child(this XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
}
