using ArcaSim.Application.Services.Ultimos;

namespace ArcaSim.Tests.Services.Ultimos;

/// <summary>TRABAJO_F931: an organism reads the F931 returns by employee and in total, within the 12-month window, through ArcaSim's reconstructed WSDL.</summary>
public class TrabajoF931RulesTests
{
    private const string Ok = "CONSULTA OK – NO HAY ERROR";

    [Fact]
    public async Task A_seeded_return_answers_by_employee_and_in_total()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931");

        var employee = (await f931.CallAsync("getRemEmpleado", ByEmployee(f931, 30555555551, 27222222228, "202609"))).Valid();
        Assert.Equal("00000", employee.Value("CodigoRespuesta"));
        Assert.Equal(Ok, employee.Value("DescripcionRespuesta"));
        Assert.Equal("202609", employee.Value("PeriodoFiscal"));
        Assert.Equal("3450000.00", employee.Value("RemuneracionTotal"));
        Assert.Equal("3100000.00", employee.Value("RemuneracionImponibleAPSS"));
        Assert.Equal("3100000.00", employee.Value("RemuneracionImponibleCOSS"));

        var total = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30555555551, "202609"))).Valid();
        Assert.Equal("00000", total.Value("CodigoRespuesta"));
        Assert.Equal(Ok, total.Value("DescripcionRespuesta"));
        Assert.Equal("202609", total.Value("PeriodoFiscal"));
        Assert.Equal("5680500.50", total.Value("RemuneracionTotal"));
        Assert.Equal("5330500.50", total.Value("RemuneracionImponibleAPSS"));
        Assert.Equal("3", total.Value("CantidadEmpleados"));
        Assert.Contains("<getDeterminativaResponse xmlns=\"http://tempuri.org/\"><getDeterminativaResult><CodigoRespuesta>00000</CodigoRespuesta>", total.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
    }

    [Fact]
    public async Task A_CUIL_outside_the_return_is_00001_and_a_missing_return_is_10014_or_10021()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931");

        var outside = (await f931.CallAsync("getRemEmpleado", ByEmployee(f931, 30555555551, 27333333339, "202609"))).Valid();
        Assert.Equal("00001", outside.Value("CodigoRespuesta"));
        Assert.Equal("SE REGISTRA DJ PARA EL CUIT PERO NO CONTIENE AL CUIL", outside.Value("DescripcionRespuesta"));
        Assert.Empty(outside.All("PeriodoFiscal"));
        Assert.Equal("0", outside.Value("RemuneracionTotal"));

        var noReturn = (await f931.CallAsync("getRemEmpleado", ByEmployee(f931, 30700000008, 27333333339, "202609"))).Valid();
        Assert.Equal("10014", noReturn.Value("CodigoRespuesta"));
        Assert.Equal("NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/CUIL/PERIODO SOLICITADA", noReturn.Value("DescripcionRespuesta"));

        var noTotal = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30700000008, "202609"))).Valid();
        Assert.Equal("10021", noTotal.Value("CodigoRespuesta"));
        Assert.Equal("NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/PERIODO SOLICITADA", noTotal.Value("DescripcionRespuesta"));
        Assert.Equal("0", noTotal.Value("CantidadEmpleados"));

        // The current month's return is not due yet: not even the seeded employers have one.
        var current = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30555555551, "202610"))).Valid();
        Assert.Equal("10021", current.Value("CodigoRespuesta"));
    }

    [Fact]
    public async Task The_period_has_to_fall_in_the_last_12_months()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931");

        var oldest = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30711111111, "202511"))).Valid();
        Assert.Equal("00000", oldest.Value("CodigoRespuesta"));
        Assert.Equal("1", oldest.Value("CantidadEmpleados"));

        foreach (var period in new[] { "202510", "202611", "2026-09", "202613" })
        {
            var refused = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30711111111, period))).Valid();
            Assert.Equal("99901", refused.Value("CodigoRespuesta"));
            Assert.Equal("Exception por argumentos inválidos.", refused.Value("DescripcionRespuesta"));
        }
        var badCuil = (await f931.CallAsync("getRemEmpleado", ByEmployee(f931, 30711111111, 2733333333, "202609"))).Valid();
        Assert.Equal("99901", badCuil.Value("CodigoRespuesta"));
    }

    [Fact]
    public async Task A_preloaded_return_is_read_and_summed_the_same_on_every_call()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931");
        await f931.PutDocumentAsync(TrabajoF931Rules.Returns, "30700000008/202608", new
        {
            employer = 30700000008,
            period = "202608",
            payroll = new[]
            {
                new { cuil = 20666666667, totalPay = 1000.50m, contributionsBase = 900.25m, employerContributionsBase = 950m },
                new { cuil = 27111111117, totalPay = 2000m, contributionsBase = 2000m, employerContributionsBase = 2000m },
            },
        });

        for (var call = 0; call < 2; call++)
        {
            var total = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30700000008, "202608"))).Valid();
            Assert.Equal("3000.50", total.Value("RemuneracionTotal"));
            Assert.Equal("2900.25", total.Value("RemuneracionImponibleAPSS"));
            Assert.Equal("2", total.Value("CantidadEmpleados"));
        }
        var employee = (await f931.CallAsync("getRemEmpleado", ByEmployee(f931, 30700000008, 20666666667, "202608"))).Valid();
        Assert.Equal("1000.50", employee.Value("RemuneracionTotal"));
        Assert.Equal("900.25", employee.Value("RemuneracionImponibleAPSS"));
        Assert.Equal("950.00", employee.Value("RemuneracionImponibleCOSS"));
    }

    [Fact]
    public async Task Refused_tickets_answer_the_manuals_97xxx_and_98xxx_codes_in_the_result()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931");
        const string query = "<cuitEmpleador>30555555551</cuitEmpleador><periodo>202609</periodo>";

        async Task<(string Code, string Text)> RefusalAsync(string credencial)
        {
            var answer = (await f931.CallAsync("getDeterminativa", credencial + query)).Valid();
            return (answer.Value("CodigoRespuesta"), answer.Value("DescripcionRespuesta"));
        }

        Assert.Equal(("97006", "Token Nulo"), await RefusalAsync(Credencial("", f931.Sign, UltimosProbe.Caller)));
        Assert.Equal(("98003", "Firma Nula"), await RefusalAsync(Credencial(f931.Token, "", UltimosProbe.Caller)));
        Assert.Equal(("97001", "Formato de token erróneo. No es base 64 válido."), await RefusalAsync(Credencial("abc", f931.Sign, UltimosProbe.Caller)));
        Assert.Equal(("98002", "Firma inválida para el token."), await RefusalAsync(Credencial(f931.Token, Convert.ToBase64String(new byte[128]), UltimosProbe.Caller)));
        Assert.Equal(("98006", "CUITDelegado inválido."), await RefusalAsync(Credencial(f931.Token, f931.Sign, 20222222223)));
        Assert.Equal(("97006", "Token Nulo"), await RefusalAsync(""));

        var other = await UltimosProbe.StartAsync(sim, "wscta");
        Assert.Equal(("97004", "Token con identificador de servicio inválido."), await RefusalAsync(Credencial(other.Token, other.Sign, UltimosProbe.Caller)));

        sim.Clock.Advance(TimeSpan.FromHours(13));
        Assert.Equal(("97003", "Token expirado."), await RefusalAsync(Credencial(f931.Token, f931.Sign, UltimosProbe.Caller)));
    }

    [Fact]
    public async Task Both_inferred_WSAA_ids_open_the_service()
    {
        await using var sim = ArcaSimHarness.Start();
        var f931 = await UltimosProbe.StartAsync(sim, "TRABAJO_F931", "ssf931");

        var total = (await f931.CallAsync("getDeterminativa", ByEmployer(f931, 30711111111, "202609"))).Valid();

        Assert.Equal("00000", total.Value("CodigoRespuesta"));
        Assert.Equal("2100000.00", total.Value("RemuneracionTotal"));
    }

    private static string Credencial(string token, string sign, long cuit) =>
        $"<credencial><Token>{token}</Token><Sign>{sign}</Sign><CUITDelegado>{cuit}</CUITDelegado></credencial>";

    private static string ByEmployee(UltimosProbe f931, long employer, long cuil, string period) =>
        Credencial(f931.Token, f931.Sign, UltimosProbe.Caller) + $"<cuitEmpleador>{employer}</cuitEmpleador><cuilEmpleado>{cuil}</cuilEmpleado><periodo>{period}</periodo>";

    private static string ByEmployer(UltimosProbe f931, long employer, string period) =>
        Credencial(f931.Token, f931.Sign, UltimosProbe.Caller) + $"<cuitEmpleador>{employer}</cuitEmpleador><periodo>{period}</periodo>";
}
