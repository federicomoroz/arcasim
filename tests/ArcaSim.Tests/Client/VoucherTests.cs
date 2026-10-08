using System.Net.Http.Json;
using Arca.Client;
using ArcaSim.Tests.Support;

namespace ArcaSim.Tests.Client;

/// <summary>
/// The parts of a voucher the client writes only when it has them, sent to ArcaSim and read back from it:
/// other taxes (Tributos), a foreign currency and CanMisMonExt. ArcaSim checks each of them, so a
/// field the client left out or misspelled shows up as a rejection or as a voucher that came back different.
/// </summary>
public class VoucherTests
{
    private static readonly OtherTaxLine Municipal = new(99, "Impuesto Municipal Matanza", 100m, 1m, 1m);

    [Fact]
    public async Task Other_taxes_travel_as_Tributos_and_come_back_as_they_were_sent()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Total = 122m, OtherTaxes = 1m, OtherTaxLines = [Municipal] });
        var stored = await ConsultAsync(sim, result.Number);

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Contains("<ImpTrib>1</ImpTrib>", stored);
        Assert.Contains("<Tributos><Tributo><Id>99</Id><Desc>Impuesto Municipal Matanza</Desc><BaseImp>100</BaseImp><Alic>1</Alic><Importe>1</Importe></Tributo></Tributos>", stored);
    }

    [Fact]
    public async Task Other_taxes_without_their_lines_are_rejected()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Total = 122m, OtherTaxes = 1m });

        Assert.False(result.Approved);
        Assert.Contains(result.Observations.Concat(result.Errors), message => message.Code == 10024);
    }

    [Fact]
    public async Task The_amounts_the_voucher_leaves_out_are_sent_as_zero()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        var exempt = new Voucher { Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 500m, Exempt = 500m, ReceiverVatCondition = 4 };

        var result = await wsfe.AuthorizeNextAsync(1, 6, exempt);
        var stored = await ConsultAsync(sim, result.Number);

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Contains("<ImpTotal>500</ImpTotal>", stored);
        Assert.Contains("<ImpOpEx>500</ImpOpEx>", stored);
        Assert.Contains("<ImpNeto>0</ImpNeto>", stored);
    }

    [Fact]
    public async Task A_voucher_in_a_foreign_currency_travels_with_its_currency_and_its_rate()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Currency = "DOL", ExchangeRate = 1450.5m });
        var stored = await ConsultAsync(sim, result.Number);

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Contains("<MonId>DOL</MonId><MonCotiz>1450.5</MonCotiz>", stored);
    }

    [Fact]
    public async Task A_currency_ARCA_does_not_have_is_rejected_by_its_code()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Currency = "XYZ", ExchangeRate = 2m });

        Assert.False(result.Approved);
        Assert.Contains(result.Observations.Concat(result.Errors), message => message.Code == 10037);
    }

    [Fact]
    public async Task Paying_in_pesos_in_the_same_currency_is_refused_and_saying_no_is_accepted()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var yes = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { PaidInSameCurrency = true });
        var no = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { PaidInSameCurrency = false });

        Assert.False(yes.Approved);
        Assert.Contains(yes.Observations.Concat(yes.Errors), message => message.Code == 10241);
        Assert.True(no.Approved, string.Join("; ", no.Errors.Concat(no.Observations)));
    }

    [Fact]
    public async Task Paying_in_the_same_foreign_currency_needs_the_rate_ARCA_has_for_the_day()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        (await sim.Http.PutAsJsonAsync("/arcasim/api/rates", new { currency = "DOL", day = "2026-09-30", rate = 1385.5m })).EnsureSuccessStatusCode();

        var same = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Currency = "DOL", ExchangeRate = 1385.5m, PaidInSameCurrency = true });
        var other = await wsfe.AuthorizeNextAsync(1, 6, Vouchers.ConsumerInvoice() with { Currency = "DOL", ExchangeRate = 1400m, PaidInSameCurrency = true });

        Assert.True(same.Approved, string.Join("; ", same.Errors.Concat(same.Observations)));
        Assert.False(other.Approved);
        Assert.Contains(other.Observations.Concat(other.Errors), message => message.Code == 10038);
    }

    /// <summary>FECompConsultar as ArcaSim stores it, raw: the client's own reading leaves out what these tests look at.</summary>
    private static async Task<string> ConsultAsync(ArcaSimHarness sim, long number)
    {
        var (_, body) = await sim.PostWsfeAsync("FECompConsultar",
            await sim.WsfeAuthAsync() + $"<ar:FeCompConsReq><ar:CbteTipo>6</ar:CbteTipo><ar:CbteNro>{number}</ar:CbteNro><ar:PtoVta>1</ar:PtoVta></ar:FeCompConsReq>");
        return body;
    }
}
