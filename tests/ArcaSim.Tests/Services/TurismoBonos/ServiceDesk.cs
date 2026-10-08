using System.Net.Http.Json;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>
/// One of these services over HTTP, the way a client generated from its WSDL
/// calls it: a ticket for the issuer, a SOAP envelope per operation, and every
/// answer checked against the WSDL before the test looks at it.
/// </summary>
internal sealed class ServiceDesk
{
    public const long Issuer = ArcaSimHarness.Issuer;

    private readonly ArcaSimHarness _sim;
    private readonly ServiceContract _contract;
    private readonly string _prefix;

    private ServiceDesk(ArcaSimHarness sim, string wsdl, string prefix, string token, string sign)
    {
        _sim = sim;
        _contract = Contracts.Of(wsdl);
        _prefix = prefix;
        Token = token;
        Sign = sign;
    }

    public string Token { get; }
    public string Sign { get; }
    public XNamespace Ns => _contract.TargetNamespace;

    /// <summary>ArcaSim on a fixed weekday (01/10/2026, noon), the issuer registered with point of sale 1, and a ticket for the WSAA id.</summary>
    public static async Task<ServiceDesk> OpenAsync(ArcaSimHarness sim, string wsdl, string wsaa, string prefix)
    {
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(Issuer, "Hotel del Sur SA", VatCondition.ResponsableInscripto, new PointOfSale(1, PointOfSaleKind.WebServiceCae));
        var certificate = await sim.IssueCertificateAsync(Issuer, prefix, wsaa);
        var ticket = await sim.Wsaa(Issuer, certificate).LoginAsync(wsaa);
        return new ServiceDesk(sim, wsdl, prefix, ticket.Token, ticket.Sign);
    }

    /// <summary>Another service on the same ArcaSim and the same ticket (wsbfe and wsbfev1 share the WSAA id).</summary>
    public ServiceDesk Sibling(string wsdl, string prefix) => new(_sim, wsdl, prefix, Token, Sign);

    /// <summary>Calls the operation with that inner XML (already prefixed with this desk's prefix) and returns the Body's answer, valid for the WSDL.</summary>
    public async Task<XElement> CallAsync(string operation, string inner)
    {
        var action = _contract.Operations.Single(o => o.Name == operation).Action;
        var envelope = Soap.Envelope($"<{_prefix}:{operation}>{inner}</{_prefix}:{operation}>", (_prefix, _contract.TargetNamespace));
        var (status, body) = await _sim.PostSoapAsync(new Uri("http://localhost" + _contract.AddressPath), envelope, $"\"{action}\"");
        Assert.True(status == 200, body);
        var answer = XDocument.Parse(body, LoadOptions.PreserveWhitespace).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        Xsd.AssertValid(answer, _contract);
        return answer;
    }

    public Task SetRateAsync(string currency, DateOnly day, decimal rate) =>
        _sim.Http.PutAsJsonAsync("/arcasim/api/rates", new { currency, day, rate }).ContinueWith(t => t.Result.EnsureSuccessStatusCode());

    /// <summary>Registers (or replaces) one of the issuer's CAE points of sale, with the day ARCA will deactivate it.</summary>
    public async Task PutPointOfSaleAsync(int number, DateOnly deactivatedOn)
    {
        var response = await _sim.Http.PutAsJsonAsync($"/arcasim/api/taxpayers/{Issuer}", new
        {
            name = "Hotel del Sur SA",
            vatCondition = "ResponsableInscripto",
            active = true,
            pointsOfSale = new[] { new { number, kind = "WebServiceCae", blocked = false, deactivatedOn } },
        });
        response.EnsureSuccessStatusCode();
    }
}
