using System.Security;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Services.Mtxca;
using ArcaSim.Application.Services.TurismoBonos;
using ArcaSim.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>
/// The events the invoicing services publish are stamped by the time provider
/// they are given, as WSFEv1's are, not by the system's clock. The rules are
/// built with a provider of the test's and handed a request the way
/// ContractHost does once the ticket passed.
/// </summary>
public class EventStampTests
{
    private static readonly DateTimeOffset Fixed = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly ServiceCatalog Catalog = ServiceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "arca-servicios.json"));

    [Fact]
    public async Task A_rejected_WSCT_voucher_is_stamped_by_the_time_provider_it_was_given()
    {
        await using var sim = ArcaSimHarness.Start();

        var events = await RunAsync<WsctRules>(sim, "wsct", "autorizarComprobante", "<comprobanteRequest/>");

        Assert.NotEmpty(events.OfType<VoucherRejected>());
        Assert.All(events.OfType<VoucherRejected>(), e => Assert.Equal(Fixed, e.At));
    }

    [Fact]
    public async Task A_rejected_wsbfev1_voucher_is_stamped_by_the_time_provider_it_was_given()
    {
        await using var sim = ArcaSimHarness.Start();

        var events = await RunAsync<WsbfeV1Rules>(sim, "wsbfev1", "BFEAuthorize", "<Cmp/>");

        Assert.NotEmpty(events.OfType<VoucherRejected>());
        Assert.All(events.OfType<VoucherRejected>(), e => Assert.Equal(Fixed, e.At));
    }

    [Fact]
    public async Task A_rejected_wsmtxca_voucher_and_a_granted_CAEA_are_stamped_by_the_time_provider_it_was_given()
    {
        await using var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        await sim.PutTaxpayerAsync(ArcaSimHarness.Issuer, "Empresa de Prueba SA", VatCondition.ResponsableInscripto,
            new PointOfSale(1, PointOfSaleKind.WebServiceCae), new PointOfSale(900, PointOfSaleKind.WebServiceCaea));

        var rejected = await RunAsync<MtxcaRules>(sim, "wsmtxca", "autorizarComprobante", "<comprobanteCAERequest/>");
        var granted = await RunAsync<MtxcaRules>(sim, "wsmtxca", "solicitarCAEA", "<solicitudCAEA><periodo>202610</periodo><orden>1</orden></solicitudCAEA>");

        Assert.NotEmpty(rejected.OfType<VoucherRejected>());
        Assert.All(rejected.OfType<VoucherRejected>(), e => Assert.Equal(Fixed, e.At));
        Assert.Single(granted.OfType<CaeaGranted>());
        Assert.All(granted.OfType<CaeaGranted>(), e => Assert.Equal(Fixed, e.At));
    }

    /// <summary>The events the rules publish while answering one request, the rules built with the test's time provider.</summary>
    private static async Task<List<IArcaSimEvent>> RunAsync<TRules>(ArcaSimHarness sim, string service, string operation, string inner)
        where TRules : IServiceBehavior
    {
        var rules = ActivatorUtilities.CreateInstance<TRules>(sim.Services, new FixedTime(Fixed));
        var seen = new List<IArcaSimEvent>();
        sim.Services.GetRequiredService<EventManager>().SubscribeAll(seen.Add);

        var definition = Catalog.Find(service)!;
        var contract = ServiceContract.Load(Path.Combine(AppContext.BaseDirectory, "arca-wsdl", definition.Wsdl));
        var request = XElement.Parse($"<{operation} xmlns=\"{SecurityElement.Escape(contract.TargetNamespace)}\">{inner}</{operation}>");
        var call = new ServiceCall(definition, contract, new SchemaSampler(contract.Schemas), contract.Operations.First(o => o.Name == operation),
            request, ArcaSimHarness.Issuer, new SampleContext(ArcaSimHarness.Issuer, sim.Clock.Now));

        Assert.NotNull(await rules.AnswerAsync(call, CancellationToken.None));
        return seen;
    }

    private sealed class FixedTime(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }
}
