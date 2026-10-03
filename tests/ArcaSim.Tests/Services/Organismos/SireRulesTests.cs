using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>SIRE F2005: emitir a certificate, anular it with a new one, and the manual's rules on the data.</summary>
public class SireRulesTests
{
    private const long Withheld = 30000000007;

    [Fact]
    public async Task A_certificate_is_issued_then_cancelled_once_with_a_new_number()
    {
        await using var sim = ArcaSimHarness.Start();
        var sire = await StartAsync(sim);

        var issued = (await sire.CallAsync("emitir", Auth(sire) + Certificate())).Valid();
        var number = issued.Value("certificadoNro");
        Assert.Matches(@"^2026\d{10}$", number);
        Assert.Matches("^[A-Z0-9]{8}$", issued.Value("codigoSeguridad"));

        var cancelled = (await sire.CallAsync("anular", Auth(sire) + Cancellation(number))).Valid();
        Assert.NotEqual(number, cancelled.Value("certificadoNro"));
        Assert.Matches(@"^2026\d{10}$", cancelled.Value("certificadoNro"));

        var again = await sire.CallAsync("anular", Auth(sire) + Cancellation(number));
        Assert.Equal(500, again.Status);
        Assert.Equal("soapenv:Client", again.FaultCode);
        Assert.Equal($"El certificado {number} ya se encuentra anulado.", again.Fault);
    }

    [Fact]
    public async Task The_same_trace_code_gets_the_certificate_it_already_got()
    {
        await using var sim = ArcaSimHarness.Start();
        var sire = await StartAsync(sim);

        var first = (await sire.CallAsync("emitir", Auth(sire) + Certificate(trace: "<codigoTrazabilidad>OP-0001</codigoTrazabilidad>"))).Valid();
        var second = (await sire.CallAsync("emitir", Auth(sire) + Certificate(trace: "<codigoTrazabilidad>OP-0001</codigoTrazabilidad>"))).Valid();
        var other = (await sire.CallAsync("emitir", Auth(sire) + Certificate(trace: "<codigoTrazabilidad>OP-0002</codigoTrazabilidad>"))).Valid();

        Assert.Equal(first.Value("certificadoNro"), second.Value("certificadoNro"));
        Assert.Equal(first.Value("codigoSeguridad"), second.Value("codigoSeguridad"));
        Assert.NotEqual(first.Value("certificadoNro"), other.Value("certificadoNro"));
    }

    [Theory]
    [InlineData("<cuitRetenido>30000000007</cuitRetenido>", "<cuitRetenido>20222222223</cuitRetenido>", "No existe persona con id 20222222223.")]
    [InlineData("<version>100</version>", "<version>101</version>", "La version del certificado debe ser 100.")]
    [InlineData("<impuesto>216</impuesto>", "<impuesto>217</impuesto>", "El impuesto debe ser 216.")]
    [InlineData("<condicion>1</condicion>", "", "La condicion es obligatoria para el regimen 831.")]
    [InlineData("<numeroComprobante>00001-00000123</numeroComprobante>", "<numeroComprobante>1-123</numeroComprobante>", "El numero de comprobante 1-123 no tiene el formato 99999-99999999.")]
    [InlineData("<fechaRetencion>2026-09-30T10:00:00.000-03:00</fechaRetencion>", "<fechaRetencion>2019-11-30T10:00:00.000-03:00</fechaRetencion>", "La fecha de retencion no puede ser anterior al 01/12/2019.")]
    [InlineData("<tipoComprobante>1</tipoComprobante>", "<tipoComprobante>3</tipoComprobante>", "Para el tipo de comprobante 3 la fecha del comprobante debe ser igual a la fecha de retencion.")]
    public async Task Data_the_manual_forbids_is_refused_as_a_client_fault(string valid, string wrong, string expected)
    {
        await using var sim = ArcaSimHarness.Start();
        var sire = await StartAsync(sim);

        var answer = await sire.CallAsync("emitir", Auth(sire) + Certificate().Replace(valid, wrong));

        Assert.Equal(500, answer.Status);
        Assert.Equal("soapenv:Client", answer.FaultCode);
        Assert.Equal(expected, answer.Fault);
    }

    [Fact]
    public async Task Another_agent_cannot_cancel_a_certificate()
    {
        await using var sim = ArcaSimHarness.Start();
        var sire = await StartAsync(sim);
        var number = (await sire.CallAsync("emitir", Auth(sire) + Certificate())).Valid().Value("certificadoNro");

        await sim.PutTaxpayerAsync(20222222223, "Otro Agente", VatCondition.ResponsableInscripto);
        var certificate = await sim.IssueCertificateAsync(20222222223, "otro", "sire-ws");
        var ticket = await sim.Wsaa(20222222223, certificate).LoginAsync("sire-ws");
        var answer = await sire.CallAsync("anular",
            $"<token>{ticket.Token}</token><sign>{ticket.Sign}</sign><cuitAgente>20222222223</cuitAgente>" + Cancellation(number));

        Assert.Equal($"No existe el certificado {number}.", answer.Fault);
    }

    private static async Task<ServiceProbe> StartAsync(ArcaSimHarness sim)
    {
        var sire = await ServiceProbe.StartAsync(sim, "sire-ws");
        await sim.PutTaxpayerAsync(Withheld, "Proveedor del Plata S.A.", VatCondition.ResponsableInscripto);
        return sire;
    }

    private static string Auth(ServiceProbe sire) =>
        $"<token>{sire.Token}</token><sign>{sire.Sign}</sign><cuitAgente>{ServiceProbe.Caller}</cuitAgente>";

    private static string Certificate(string trace = "") =>
        "<certificado><version>100</version>" + trace + "<impuesto>216</impuesto><regimen>831</regimen>" +
        "<fechaRetencion>2026-09-30T10:00:00.000-03:00</fechaRetencion><condicion>1</condicion>" +
        "<imposibilidadRetencion>false</imposibilidadRetencion><importeRetencion>2100.00</importeRetencion>" +
        "<importeBaseCalculo>10000.00</importeBaseCalculo><regimenExclusion>false</regimenExclusion>" +
        "<tipoComprobante>1</tipoComprobante><fechaComprobante>2026-09-28T00:00:00.000-03:00</fechaComprobante>" +
        "<numeroComprobante>00001-00000123</numeroComprobante><importeComprobante>12100.00</importeComprobante>" +
        $"<cuitRetenido>{Withheld}</cuitRetenido></certificado>";

    private static string Cancellation(string number) =>
        $"<certificadoAnulacion><version>100</version><impuesto>216</impuesto><numeroCertificado>{number}</numeroCertificado>" +
        "<motivoAnulacion>3</motivoAnulacion></certificadoAnulacion>";
}
