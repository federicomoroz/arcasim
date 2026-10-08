using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Liquidaciones;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>
/// ArcaSim with an issuer that has web service points of sale 1 and 3000, a
/// ticket for one sector service, and raw SOAP calls written the way the
/// manuals' examples are: the operation's element in the target namespace,
/// auth and solicitud unqualified. Every answer is checked against the WSDL.
/// </summary>
internal sealed class LiquidacionesSim : IAsyncDisposable
{
    public const long Issuer = ArcaSimHarness.Issuer;
    public const long Producer = 20222222223;
    public const long Monotributista = 27333333339;

    /// <summary>Thursday 01/10/2026, noon in Buenos Aires.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    private readonly ServiceContract _contract;
    private readonly string _service;
    private readonly string _cuitField;
    private readonly string _auth;

    private LiquidacionesSim(ArcaSimHarness sim, ServiceContract contract, string service, string cuitField, string auth)
    {
        Sim = sim;
        _contract = contract;
        _service = service;
        _cuitField = cuitField;
        _auth = auth;
    }

    public ArcaSimHarness Sim { get; }

    public static async Task<LiquidacionesSim> StartAsync(string service, string wsdl, string cuitField = "cuit")
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(Now);
        await sim.PutTaxpayerAsync(Issuer, "Industrias del Campo SA", VatCondition.ResponsableInscripto,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(3000, PointOfSaleKind.WebServiceCae));
        await sim.PutTaxpayerAsync(Producer, "Juan Productor", VatCondition.ResponsableInscripto);
        await sim.PutTaxpayerAsync(Monotributista, "Ana Chacarera", VatCondition.Monotributo);
        var contract = Contracts.Of(wsdl);
        return new LiquidacionesSim(sim, contract, service, cuitField, await AuthAsync(sim, Issuer, service, cuitField));
    }

    /// <summary>The auth block of another registered taxpayer, for the operations its counterpart answers.</summary>
    public Task<string> AuthForAsync(long cuit) => AuthAsync(Sim, cuit, _service, _cuitField);

    private static async Task<string> AuthAsync(ArcaSimHarness sim, long cuit, string service, string cuitField)
    {
        var certificate = await sim.IssueCertificateAsync(cuit, $"liquidaciones{Guid.NewGuid():N}", service);
        var ticket = await sim.Wsaa(cuit, certificate).LoginAsync(service);
        return $"<auth>{Login.Credentials(ticket, cuit, cuitField)}</auth>";
    }

    /// <summary>Calls an operation with the ticket in auth and the rest as given; returns the answer's first child (respuesta, or the response itself when it has none).</summary>
    public async Task<XElement> CallAsync(string operation, string inner = "", string? auth = null)
    {
        var answer = await RawAsync(operation, (auth ?? _auth) + inner);
        return answer.Element("respuesta") ?? answer;
    }

    /// <summary>The raw answer, whatever its status: for what the service refuses with a fault.</summary>
    public Task<(int Status, string Body)> PostAsync(string operation, string inner, CancellationToken ct = default)
    {
        var contract = _contract.Operations.Single(o => o.Name == operation);
        var element = contract.Input!;
        var envelope = Soap.Envelope($"<x:{element.LocalName}>{_auth}{inner}</x:{element.LocalName}>", ("x", element.NamespaceName));
        return Sim.PostSoapAsync(new Uri("http://localhost" + _contract.AddressPath), envelope, $"\"{contract.Action}\"", ct);
    }

    /// <summary>The Body's element, valid for the WSDL.</summary>
    public async Task<XElement> RawAsync(string operation, string inner)
    {
        var contract = _contract.Operations.Single(o => o.Name == operation);
        var element = contract.Input!;
        var envelope = Soap.Envelope($"<x:{element.LocalName}>{inner}</x:{element.LocalName}>", ("x", element.NamespaceName));
        var (status, body) = await Sim.PostSoapAsync(new Uri("http://localhost" + _contract.AddressPath), envelope, $"\"{contract.Action}\"");
        Assert.True(status == 200, body);
        var answer = Soap.Body(body);
        Assert.Equal(contract.Output, answer.Name);
        Xsd.AssertValid(answer, _contract);
        return answer;
    }

    /// <summary>Edits the detail of a voucher the service stored, as if the store held one built without a field.</summary>
    public async Task EditStoredAsync(string service, long cuit, int pointOfSale, int voucherType, long number, Action<XElement> edit)
    {
        var store = Sim.Services.GetRequiredService<IDocumentStore>();
        var key = $"liq/{AuthorizedVouchers.Key(cuit, pointOfSale, voucherType, number)}";
        var stored = (await store.GetAsync<Settlement>(service, key))!;
        var detail = stored.DetailXml();
        edit(detail);
        await store.PutAsync(service, key, stored with { Detail = detail.ToString(SaveOptions.DisableFormatting) });
    }

    public static string Day(int offset = 0) => Now.AddDays(offset).ToString("yyyy-MM-dd");

    /// <summary>The codes in respuesta/errores (or errores/codigoDescripcion), in order.</summary>
    public static List<string> Errors(XElement answer) =>
        answer.Descendants().Where(e => e.Name.LocalName == "errores").SelectMany(e => e.Elements())
            .Select(e => e.Element("codigo")!.Value).ToList();

    public ValueTask DisposeAsync() => Sim.DisposeAsync();
}
