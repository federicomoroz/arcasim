using ArcaSim.Application.Services.Organismos;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>SUD: debts by CUIT, limited to taxes 301 and 351, with a new consultaId per query and the manual's fault for a bad CUIT.</summary>
public class SudRulesTests
{
    [Fact]
    public async Task A_seeded_debtor_shows_its_debts_and_every_query_gets_a_new_id()
    {
        await using var sim = ArcaSimHarness.Start();
        var sud = await ServiceProbe.StartAsync(sim, "sud_restricciones");

        var first = (await QueryAsync(sud, "tieneDeuda", 30999999995)).Valid();
        var detail = (await QueryAsync(sud, "tieneDeudaV2", 30999999995)).Valid();

        Assert.Equal("true", first.Value("tieneDeuda"));
        Assert.Equal("2026-10-01T12:00:00-0300", first.Value("fechaHora"));
        Assert.Equal("servicios-externos", first.Value("servidor"));
        Assert.Equal(long.Parse(first.Value("consultaId")) + 1, long.Parse(detail.Value("consultaId")));
        Assert.Equal(["301/202501", "351/202501", "301/202502"],
            detail.All("deuda").Select(d => $"{d.Value("impuesto")}/{d.Value("periodoFiscal")}"));
    }

    [Fact]
    public async Task A_preloaded_document_replaces_the_default_and_other_taxes_are_left_out()
    {
        await using var sim = ArcaSimHarness.Start();
        var sud = await ServiceProbe.StartAsync(sim, "sud_restricciones");
        await sim.PutTaxpayerAsync(30000000007, "Proveedor del Estado S.A.", VatCondition.ResponsableInscripto);
        await sud.Store.PutAsync(SudRules.Debts, "30000000007", new Debtor(30000000007, [new(30, "202604"), new(351, "202605")]));

        var detail = (await QueryAsync(sud, "tieneDeudaV2", 30000000007)).Valid();

        Assert.Equal("true", detail.Value("tieneDeuda"));
        Assert.Equal(["351"], detail.All("deuda").Select(d => d.Value("impuesto")));
    }

    [Fact]
    public async Task A_registered_CUIT_without_debts_owes_nothing()
    {
        await using var sim = ArcaSimHarness.Start();
        var sud = await ServiceProbe.StartAsync(sim, "sud_restricciones");

        Assert.Equal("false", (await QueryAsync(sud, "tieneDeuda", ServiceProbe.Caller)).Valid().Value("tieneDeuda"));
        var detail = (await QueryAsync(sud, "tieneDeudaV2", ServiceProbe.Caller)).Valid();
        Assert.Equal("false", detail.Value("tieneDeuda"));
        Assert.Equal("0", detail.Value("impuesto"));
    }

    [Theory]
    [InlineData(30999999996)]
    [InlineData(20222222223)]
    public async Task A_CUIT_with_a_wrong_check_digit_or_unknown_is_the_manuals_fault(long cuit)
    {
        await using var sim = ArcaSimHarness.Start();
        var sud = await ServiceProbe.StartAsync(sim, "sud_restricciones");

        var answer = await QueryAsync(sud, "tieneDeuda", cuit);

        Assert.Equal(500, answer.Status);
        Assert.Equal("SOAP-ENV:Server", answer.FaultCode);
        Assert.Equal("CUIT sud:cuit INVALIDA", answer.Fault);
    }

    private static Task<SoapAnswer> QueryAsync(ServiceProbe sud, string operation, long cuit) =>
        sud.CallAsync(operation, $"<cuit>{cuit}</cuit><cuitRepresentado>{ServiceProbe.Caller}</cuitRepresentado>" +
                                 $"<token>{sud.Token}</token><sign>{sud.Sign}</sign>", qualified: true);
}
