using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Percepción de IVA: one constancia per CUIT of the batch, with the manual's legends and errors.</summary>
public class RgivaRulesTests
{
    [Fact]
    public async Task A_batch_gets_the_manuals_test_legends_the_padrons_and_errors_per_CUIT()
    {
        await using var sim = ArcaSimHarness.Start();
        var rgiva = await ServiceProbe.StartAsync(sim, "wsrgiva");
        await sim.PutTaxpayerAsync(20222222223, "Ana Gomez", VatCondition.Monotributo);

        var answer = (await QueryAsync(rgiva, (24219972942, 1), (20222222223, 1), (ServiceProbe.Caller, 1), (27000000006, 1), (30709778079, 2))).Valid();
        var constancias = answer.All("constancia");

        Assert.Equal(5, constancias.Count);
        Assert.Equal("4", constancias[0].Value("codigoLeyenda"));
        Assert.Equal("Alícuota 7% - El contribuyente se encuentra obligado a emitir comprobantes clase \"M\"", constancias[0].Value("descripcionLeyenda"));
        Assert.Equal("01-10-2026", constancias[0].Value("fechaConsulta"));
        Assert.Equal("31-10-2026", constancias[0].Value("vigencia"));
        Assert.Matches("^[0-9A-F]{16}$", constancias[0].Value("codigoSeguridad"));
        Assert.Equal("20", constancias[1].Value("codigoLeyenda"));
        Assert.Equal("ANA GOMEZ", constancias[1].Value("descripcionContribuyente"));
        Assert.Equal("18", constancias[2].Value("codigoLeyenda"));
        Assert.Equal(("4003", "CUIT Inexistente"), (constancias[3].Value("codigoError"), constancias[3].Value("descripcionError")));
        Assert.Equal(("4002", "Solo se admite tipo de bien 1"), (constancias[4].Value("codigoError"), constancias[4].Value("descripcionError")));
    }

    [Fact]
    public async Task The_security_code_is_the_same_for_the_same_query()
    {
        await using var sim = ArcaSimHarness.Start();
        var rgiva = await ServiceProbe.StartAsync(sim, "wsrgiva");

        var first = (await QueryAsync(rgiva, (30684587559, 1))).Valid();
        var second = (await QueryAsync(rgiva, (30684587559, 1))).Valid();

        Assert.Equal("2", first.Value("codigoLeyenda"));
        Assert.Equal(first.Value("codigoSeguridad"), second.Value("codigoSeguridad"));
    }

    private static Task<SoapAnswer> QueryAsync(ServiceProbe rgiva, params (long Cuit, int Goods)[] items) =>
        rgiva.CallAsync("consultarConstanciaPorLote_v2",
            $"<authRequest><token>{rgiva.Token}</token><sign>{rgiva.Sign}</sign><cuitRepresentada>{ServiceProbe.Caller}</cuitRepresentada></authRequest>" +
            "<datosTransaccionArray>" + string.Concat(items.Select(i =>
                $"<datosTransaccion><cuitContribuyente>{i.Cuit}</cuitContribuyente><tipoBienesInvolucrados>{i.Goods}</tipoBienesInvolucrados></datosTransaccion>")) +
            "</datosTransaccionArray>");
}
