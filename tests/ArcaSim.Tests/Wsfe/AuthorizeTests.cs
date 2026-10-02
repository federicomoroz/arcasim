using Arca.Client;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Wsfe;

/// <summary>An application asking for CAEs through Arca.Client, as it would against ARCA.</summary>
public class AuthorizeTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    /// <summary>A class B invoice to an unidentified final consumer: $1210 with 21 % VAT.</summary>
    private static Voucher ConsumerInvoice(decimal net = 1000m) => new()
    {
        Concept = 1,
        DocumentType = 99,
        DocumentNumber = 0,
        Total = net * 1.21m,
        Net = net,
        Vat = net * 0.21m,
        ReceiverVatCondition = 5,
        VatLines = [new VatLine(5, net, net * 0.21m)],
    };

    [Fact]
    public async Task A_valid_invoice_gets_a_14_digit_CAE_due_ten_days_after_its_date()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
        Assert.Equal(1, result.Number);
        Assert.Matches("^[1-9][0-9]{13}$", result.Cae);
        Assert.Equal(Today, result.Date);
        Assert.Equal(Today.AddDays(10), result.CaeDue);
        Assert.Empty(result.Observations);
    }

    [Fact]
    public async Task Numbers_follow_each_other_per_point_of_sale_and_voucher_type()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());
        var second = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());
        var otherType = await wsfe.AuthorizeNextAsync(1, 1, new Voucher
        {
            Concept = 1, DocumentType = 80, DocumentNumber = 30000000007, Total = 121, Net = 100, Vat = 21,
            ReceiverVatCondition = 1, VatLines = [new VatLine(5, 100, 21)],
        });

        Assert.Equal(2, second.Number);
        Assert.Equal(1, otherType.Number);
        Assert.Equal(2, await wsfe.LastAuthorizedAsync(1, 6));
        Assert.Equal(1, await wsfe.LastAuthorizedAsync(1, 1));
    }

    [Fact]
    public async Task Sending_the_same_number_twice_fails_with_10016_as_in_ARCA()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        var first = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());

        var again = await wsfe.AuthorizeAsync(1, 6, ConsumerInvoice() with { Number = first.Number });

        Assert.False(again.Approved);
        var error = Assert.Single(again.Errors);
        Assert.Equal(10016, error.Code);
        Assert.Equal("El numero o fecha del comprobante no se corresponde con el proximo a autorizar. Consultar metodo FECompUltimoAutorizado.", error.Message);
    }

    [Fact]
    public async Task A_total_that_does_not_add_up_is_rejected_with_10048()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice() with { Total = 1300 });

        Assert.False(result.Approved);
        Assert.Contains(result.Observations, o => o.Code == 10048);
        Assert.Null(result.Cae);
        Assert.Equal(0, await wsfe.LastAuthorizedAsync(1, 6));
    }

    [Fact]
    public async Task A_date_out_of_range_is_rejected_with_the_message_ARCA_sends()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice() with { Date = Today.AddDays(-6) });

        Assert.False(result.Approved);
        var observation = Assert.Single(result.Observations);
        Assert.Equal(10016, observation.Code);
        Assert.Equal("Campo CbteFch Debe estar comprendido  en el  rango  N-5 y N+5 siendo N la fecha de envio del pedido  de autorizacion para 1 - Productos", observation.Message);
    }

    [Fact]
    public async Task Without_the_receiver_VAT_condition_it_is_observed_until_december_and_rejected_after()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var before = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice() with { ReceiverVatCondition = null });
        sim.Clock.Freeze(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(-3)));
        var after = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice() with { ReceiverVatCondition = null });

        Assert.True(before.Approved);
        Assert.Equal(10245, Assert.Single(before.Observations).Code);
        Assert.False(after.Approved);
        Assert.Equal(10246, Assert.Single(after.Observations).Code);
    }

    [Fact]
    public async Task A_class_A_invoice_to_a_monotributista_carries_the_10217_legend()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        await sim.PutTaxpayerAsync(20222222223, "Cliente Monotributista", VatCondition.Monotributo);

        var result = await wsfe.AuthorizeNextAsync(1, 1, new Voucher
        {
            Concept = 1, DocumentType = 80, DocumentNumber = 20222222223, Total = 121, Net = 100, Vat = 21,
            ReceiverVatCondition = 6, VatLines = [new VatLine(5, 100, 21)],
        });

        Assert.True(result.Approved);
        var legend = Assert.Single(result.Observations);
        Assert.Equal(10217, legend.Code);
        Assert.StartsWith("El credito fiscal discriminado", legend.Message);
    }

    [Fact]
    public async Task A_monotributista_invoices_class_C_without_VAT()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync(VatCondition.Monotributo);
        await using var _ = sim;

        var result = await wsfe.AuthorizeNextAsync(1, 11, new Voucher
        {
            Concept = 2, DocumentType = 99, DocumentNumber = 0, Total = 1000, Net = 1000, ReceiverVatCondition = 5,
            ServiceFrom = new DateOnly(2026, 9, 1), ServiceTo = new DateOnly(2026, 9, 30), PaymentDue = new DateOnly(2026, 10, 10),
        });

        Assert.True(result.Approved, string.Join("; ", result.Errors.Concat(result.Observations)));
    }

    [Fact]
    public async Task A_monotributista_cannot_issue_class_B()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync(VatCondition.Monotributo);
        await using var _ = sim;

        var result = await wsfe.AuthorizeAsync(1, 6, ConsumerInvoice() with { Number = 1 });

        Assert.False(result.Approved);
        Assert.Equal(10000, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task A_point_of_sale_that_is_not_registered_for_web_services_is_rejected_with_10005()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var result = await wsfe.AuthorizeAsync(7, 6, ConsumerInvoice() with { Number = 1 });

        Assert.False(result.Approved);
        Assert.Equal(10005, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Above_the_final_consumer_threshold_the_receiver_has_to_be_identified()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;

        var anonymous = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice(net: 9_000_000m));
        var identified = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice(net: 9_000_000m) with { DocumentType = 96, DocumentNumber = 30123456 });

        Assert.False(anonymous.Approved);
        Assert.Contains(anonymous.Observations, o => o.Code == 10015);
        Assert.True(identified.Approved, string.Join("; ", identified.Observations));
    }

    [Fact]
    public async Task FECompConsultar_returns_what_was_sent_and_how_it_was_authorized()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());

        var found = await wsfe.QueryAsync(1, 6, issued.Number);
        var missing = await wsfe.QueryAsync(1, 6, issued.Number + 1);

        Assert.NotNull(found);
        Assert.Equal(issued.Cae, found.AuthorizationCode);
        Assert.Equal("CAE", found.EmissionType);
        Assert.Equal(1210m, found.Total);
        Assert.Equal(issued.CaeDue, found.Due);
        Assert.Null(missing);
    }

    [Fact]
    public async Task A_lost_answer_is_recovered_with_FECompConsultar_instead_of_failing_with_10016()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        sim.Settings.ChaosFor("wsfe").DropNextResponse = true;

        var result = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());

        Assert.True(result.Approved);
        Assert.True(result.Recovered);
        Assert.Equal(1, result.Number);
        Assert.Equal(1, await wsfe.LastAuthorizedAsync(1, 6));
    }

    [Fact]
    public async Task A_forced_rejection_rejects_the_next_voucher_with_that_code()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        sim.Settings.ChaosFor("wsfe").ForceNextRejection(10017);

        var rejected = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());
        var next = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice());

        Assert.False(rejected.Approved);
        Assert.Equal(10017, rejected.Observations[0].Code);
        Assert.True(next.Approved);
    }

    [Fact]
    public async Task With_the_service_down_the_client_gets_a_retryable_failure()
    {
        var (sim, wsfe) = await ArcaSimHarness.StartWithIssuerAsync();
        await using var _ = sim;
        sim.Settings.ChaosFor("wsfe").Down = true;

        // WSAA itself answers wsn.unavailable for a service that is down, before WSFEv1 is even called.
        var failure = await Assert.ThrowsAnyAsync<ArcaException>(() => wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice()));

        Assert.True(failure.Retryable);
    }
}
