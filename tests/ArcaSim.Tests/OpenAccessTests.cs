using System.Net.Http.Json;
using Arca.Client;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests;

/// <summary>
/// ArcaSim as it starts by default: nothing but ARCA's endpoints. A
/// certificate made on the spot, no taxpayer loaded, no authorization, no
/// point of sale registered, and the first CAE comes back anyway.
/// </summary>
public class OpenAccessTests
{
    private const long Cuit = 20111111112;

    [Fact]
    public async Task With_a_self_signed_certificate_and_nothing_loaded_an_invoice_gets_its_CAE()
    {
        await using var sim = ArcaSimHarness.Start(open: true);
        using var certificate = Certificates.SelfSigned($"SERIALNUMBER=CUIT {Cuit}, CN=mi-aplicacion");
        var wsfe = sim.Wsfe(Cuit, certificate);

        var result = await wsfe.AuthorizeNextAsync(4, 6, Vouchers.ConsumerInvoice());
        var taxpayer = await sim.Http.GetFromJsonAsync<TaxpayerView>($"/arcasim/api/taxpayers/{Cuit}");

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Equal(1, result.Number);
        Assert.Equal("ResponsableInscripto", taxpayer!.VatCondition);
        Assert.Contains(taxpayer.PointsOfSale, p => p.Number == 4 && p.Kind == "WebServiceCae");
    }

    [Fact]
    public async Task An_issuer_whose_first_voucher_is_class_C_is_taken_as_monotributista()
    {
        await using var sim = ArcaSimHarness.Start(open: true);
        using var certificate = Certificates.SelfSigned($"SERIALNUMBER=CUIT {Cuit}, CN=mi-aplicacion");

        var result = await sim.Wsfe(Cuit, certificate).AuthorizeNextAsync(1, 11, new Voucher
        {
            Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 1000, Net = 1000, ReceiverVatCondition = 5,
        });
        var taxpayer = await sim.Http.GetFromJsonAsync<TaxpayerView>($"/arcasim/api/taxpayers/{Cuit}");

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Equal("Monotributo", taxpayer!.VatCondition);
    }

    [Fact]
    public async Task A_certificate_without_a_CUIT_is_still_untrusted()
    {
        await using var sim = ArcaSimHarness.Start(open: true);
        using var certificate = Certificates.SelfSigned("CN=sin-cuit");

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => sim.Wsaa(Cuit, certificate).LoginAsync("wsfe"));

        Assert.Equal("cms.cert.untrusted", failure.Code);
    }

    [Fact]
    public async Task Strict_access_asks_for_what_ARCA_asks_for()
    {
        await using var sim = ArcaSimHarness.Start(open: false);
        using var certificate = Certificates.SelfSigned($"SERIALNUMBER=CUIT {Cuit}, CN=mi-aplicacion");

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => sim.Wsaa(Cuit, certificate).LoginAsync("wsfe"));

        Assert.Equal("cms.cert.untrusted", failure.Code);
    }

    private sealed record PointOfSaleView(int Number, string Kind);

    private sealed record TaxpayerView(long Cuit, string VatCondition, List<PointOfSaleView> PointsOfSale);
}
