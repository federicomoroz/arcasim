using ArcaSim.Application;
using ArcaSim.Application.Setiws;
using ArcaSim.Infrastructure.InMemory;
using ArcaSim.Tests.Services.Aduana;

namespace ArcaSim.Tests.Setiws;

/// <summary>VepService on its own: what a request validates, and what requests that arrive together cannot do twice.</summary>
public class VepServiceTests
{
    private const long Owner = 20111111112;

    private static VepService Service()
    {
        var clock = new SimulatedClock(TimeProvider.System);
        clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        return new VepService(new YieldingDocumentStore(new InMemoryStore()), clock);
    }

    private static EdpVep Request(string transaction, int period = 202609) => new(1001, new Vep
    {
        OwnerCuit = Owner,
        OwnerTransactionId = transaction,
        NroFormulario = 6042,
        PeriodoFiscal = period,
        Importe = 100m,
        Obligaciones = [new Obligacion(10, null, 100m)],
    });

    [Fact]
    public async Task A_retry_that_arrives_while_the_first_request_runs_gets_the_same_VEP()
    {
        var veps = Service();

        var answers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => veps.CreateAsync(Request("T-1"), Owner, CancellationToken.None)));

        Assert.All(answers, a => Assert.Null(a.Error));
        Assert.Single(answers.Select(a => a.Vep!.Vep.NroVEP).Distinct());
    }

    [Fact]
    public async Task A_VEP_paid_by_requests_that_arrive_together_keeps_one_payment()
    {
        var veps = Service();
        var created = (await veps.CreateAsync(Request("T-1"), Owner, CancellationToken.None)).Vep!;

        var paid = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => veps.PayAsync(created.Vep.NroVEP!.Value, 8, 91, 11, CancellationToken.None)));

        Assert.Single(paid.Select(p => p!.Cp!.CpId).Distinct());
    }

    [Theory]
    [InlineData(202613)]
    [InlineData(99912)]
    public async Task A_fiscal_period_needs_six_digits_and_a_month_up_to_12(int period)
    {
        var (stored, error) = await Service().CreateAsync(Request("T-2", period), Owner, CancellationToken.None);

        Assert.Null(stored);
        Assert.Equal((400, "InputFormularioException", $"Periodo fiscal invalido: {period}"), (error!.Status, error.Type, error.Message));
    }

    [Theory]
    [InlineData(202600)] // SETIWS-PAGO-API.md: "MM entre 00 y 12, 00 para períodos anuales"
    [InlineData(202601)]
    [InlineData(202612)]
    public async Task A_fiscal_period_of_a_month_or_of_the_whole_year_is_accepted(int period)
    {
        var (stored, error) = await Service().CreateAsync(Request("T-3", period), Owner, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(period, stored!.Vep.PeriodoFiscal);
    }
}
