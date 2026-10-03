using System.Globalization;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Organismos;

namespace ArcaSim.Application.Services.Ultimos;

/// <summary>
/// One employee of an F931 return: TotalPay is the RemuneracionTotal,
/// ContributionsBase the remuneración imponible for the employee's social
/// security contributions (APSS), EmployerContributionsBase the one for the
/// employer's (COSS).
/// </summary>
public sealed record F931Employee(long Cuil, decimal TotalPay, decimal ContributionsBase, decimal EmployerContributionsBase);

/// <summary>An employer's F931 return (declaración jurada) for one period, AAAAMM, with its payroll.</summary>
public sealed record F931Return(long Employer, string Period, List<F931Employee>? Payroll);

/// <summary>
/// Consulta de F931 para el MTEySS (TRABAJO_F931, docs/arca/servicios/TRABAJO_F931.md),
/// answered through ArcaSim's reconstruction of its WSDL, since ARCA
/// publishes none. An organism reads, and never writes, the F931 returns kept
/// in TRABAJO_F931.ddjj by employer and period ("{cuitEmpleador}/{periodo}"):
/// getRemEmpleado answers one CUIL's three remunerations, getDeterminativa the
/// return's total and APSS remunerations summed and how many employees it
/// declares. The manual's codes and texts: 00000; 00001 when the employer's
/// return for the period does not have the CUIL; 10014 (getRemEmpleado) and
/// 10021 (getDeterminativa) when the employer has no return for the period.
/// The period has to fall in the last 12 months, the current one included.
/// Every period of that window but the current one, whose return is not due
/// yet, holds plainly fictitious returns for two employers, seeded the first
/// time the period is asked for: 30555555551, with three employees, and
/// 30711111111, with one. A test or an operator preloads its own returns with
/// the admin API, PUT /arcasim/api/documents/TRABAJO_F931.ddjj/{cuitEmpleador}/{periodo}
/// and an F931Return as JSON, such as {"employer": 30700000008, "period":
/// "202609", "payroll": [{"cuil": 27111111117, "totalPay": 1000.50,
/// "contributionsBase": 1000.50, "employerContributionsBase": 1000.50}]}.
/// ArcaSim's choices where the manual is silent: 99901 "Exception por
/// argumentos inválidos." for a period outside the window or not AAAAMM, and
/// for a CUIT or CUIL that is not 11 digits; with any code but 00000 the
/// answer leaves PeriodoFiscal out and carries 0 in the numbers, as .NET
/// writes a result it did not fill; amounts go with two decimals.
/// </summary>
public sealed class TrabajoF931Rules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    public const string Returns = "TRABAJO_F931.ddjj";
    private const int WindowMonths = 12;

    public string Service => "TRABAJO_F931";

    public static string Key(long employer, string period) => $"{employer}/{period}";

    public static string Scope(string period) => $"{Returns}/{period}";

    public static IEnumerable<(string, F931Return)> Defaults(string period) =>
    [
        (Key(30555555551, period), new F931Return(30555555551, period,
        [
            new F931Employee(27111111117, 1_250_000.00m, 1_250_000.00m, 1_250_000.00m),
            new F931Employee(20666666667, 980_500.50m, 980_500.50m, 980_500.50m),
            new F931Employee(27222222228, 3_450_000.00m, 3_100_000.00m, 3_100_000.00m),
        ])),
        (Key(30711111111, period), new F931Return(30711111111, period,
        [
            new F931Employee(27333333339, 2_100_000.00m, 2_100_000.00m, 2_100_000.00m),
        ])),
    ];

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name is not ("getRemEmpleado" or "getDeterminativa")) return null;
        var byEmployee = call.Name == "getRemEmpleado";
        var employer = ElevenDigits(call.Request.Text("cuitEmpleador"));
        var cuil = byEmployee ? ElevenDigits(call.Request.Text("cuilEmpleado")) : 0;
        var period = call.Request.Text("periodo") ?? "";
        var current = clock.Now.ToArgentina().ToString("yyyyMM", CultureInfo.InvariantCulture);
        if (employer is null || cuil is null || !InWindow(period, current))
            return Failed(call, "99901", "Exception por argumentos inválidos.");

        if (period != current) await store.SeedAsync(Scope(period), Returns, Defaults(period), ct);
        var declared = await store.GetAsync<F931Return>(Returns, Key(employer.Value, period), ct);
        if (declared is null)
            return byEmployee
                ? Failed(call, "10014", "NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/CUIL/PERIODO SOLICITADA")
                : Failed(call, "10021", "NO SE REGISTRAN DDJJ PARA LA RELACION CUIT/PERIODO SOLICITADA");

        var payroll = declared.Payroll ?? [];
        if (!byEmployee)
            return Answered(call, period, payroll.Sum(e => e.TotalPay), payroll.Sum(e => e.ContributionsBase), null, payroll.Count);
        return payroll.FirstOrDefault(e => e.Cuil == cuil) is { } employee
            ? Answered(call, period, employee.TotalPay, employee.ContributionsBase, employee.EmployerContributionsBase, null)
            : Failed(call, "00001", "SE REGISTRA DJ PARA EL CUIT PERO NO CONTIENE AL CUIL");
    }

    /// <summary>A period AAAAMM among the window's months: the current one and the 11 before it.</summary>
    private static bool InWindow(string period, string current)
    {
        if (period.Length != 6 || !period.All(char.IsAsciiDigit)) return false;
        var month = int.Parse(period[4..], CultureInfo.InvariantCulture);
        if (month is < 1 or > 12) return false;
        static int Index(string yyyymm) => int.Parse(yyyymm[..4], CultureInfo.InvariantCulture) * 12 + int.Parse(yyyymm[4..], CultureInfo.InvariantCulture) - 1;
        var distance = Index(current) - Index(period);
        return distance is >= 0 and < WindowMonths;
    }

    private static long? ElevenDigits(string? text) =>
        text is { Length: 11 } && text.All(char.IsAsciiDigit) ? long.Parse(text, CultureInfo.InvariantCulture) : null;

    private static ContractAnswer Answered(ServiceCall call, string period, decimal total, decimal contributions, decimal? employerContributions, long? employees) =>
        call.Ok(call.Sample()
            .Set("CodigoRespuesta", "00000")
            .Set("DescripcionRespuesta", "CONSULTA OK – NO HAY ERROR")
            .Set("PeriodoFiscal", period)
            .Set("RemuneracionTotal", Amount(total))
            .Set("RemuneracionImponibleAPSS", Amount(contributions))
            .Set("RemuneracionImponibleCOSS", Amount(employerContributions ?? 0))
            .Set("CantidadEmpleados", employees ?? 0));

    private static ContractAnswer Failed(ServiceCall call, string code, string description) =>
        call.Ok(call.Sample()
            .Set("CodigoRespuesta", code)
            .Set("DescripcionRespuesta", description)
            .SetOrDrop("PeriodoFiscal", null)
            .Set("RemuneracionTotal", 0)
            .Set("RemuneracionImponibleAPSS", 0)
            .Set("RemuneracionImponibleCOSS", 0)
            .Set("CantidadEmpleados", 0));

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
