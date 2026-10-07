using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A worker's working life as Mi Argentina shows it: employments and household employments.</summary>
public sealed record Worker(long Cuil, string FirstName, string LastName, List<Employment> Employments, List<HouseholdEmployment> Household);

/// <summary>One employment, with the contributions' traffic lights (1 paid, 2 partial, 3 unpaid).</summary>
public sealed record Employment(
    long Employer, string EmployerName, DateOnly Since, string Address, int AddressCode, string LastDeclared, string Hiring,
    int SocialSecurity, int HealthContributions, int HealthEmployer, int MessageCode, string Message);

public sealed record HouseholdEmployment(
    long Employer, string EmployerName, string HealthInsurance, DateOnly Since, string Address, string Category, string WeeklyHours,
    string Hiring, string Insurer, string LastPayment, int MessageCode, string Message);

/// <summary>
/// Mi Argentina, vida laboral (miargentina-ws, docs/arca/servicios/miargentina-ws.md):
/// ObtenerInformacionLaboral answers a CUIL's employments from
/// miargentina-ws.trabajadores, with EstadoTransaccion 0 "OK" and a growing
/// Id. A CUIL with no document gets a plainly fictitious working life: one
/// employment with 30555555551, "EMPLEADOR FICTICIO DE ARCASIM S.A.", declared
/// last month, all contributions paid, and the manual's message 2001.
/// ArcaSim's choices: Datos' own CodigoMensaje is 0 without Mensaje; a CUIL
/// with a wrong check digit gets Codigo 9000 "El CUIL ... no es válido.",
/// ArcaSim's own code (ARCA's is not documented); the worker's name is the
/// padrón's when it knows the CUIL.
/// </summary>
public sealed class MiArgentinaRules(IDocumentStore store, PadronDirectory padron, IClock clock) : IServiceBehavior
{
    public const string Workers = "miargentina-ws.trabajadores";

    public string Service => "miargentina-ws";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name != "ObtenerInformacionLaboral") return null;
        var cuil = call.Request.Long("cuil");
        var id = (await store.NextAsync(Workers, ct)).ToString(CultureInfo.InvariantCulture);
        var answer = call.Sample();
        if (!Cuits.IsValid(cuil))
            return call.Ok(answer.Set("Id", id).Set("Codigo", 9000).Set("Descripcion", $"El CUIL {call.Request.Text("cuil")} no es válido.").Drop("Datos"));

        var worker = await store.GetAsync<Worker>(Workers, cuil.ToString(CultureInfo.InvariantCulture), ct) ?? await DefaultAsync(cuil, ct);
        answer.Set("Id", id).Set("Codigo", 0).Set("Descripcion", "OK");
        var data = answer.Find("Datos")!;
        data.Set("Cuil", worker.Cuil).Set("Nombre", worker.FirstName).Set("Apellido", worker.LastName).Set("CodigoMensaje", 0);
        data.Elements().First(e => e.Name.LocalName == "Mensaje").Remove();
        data.Find("MisAportes")!.Repeat("Item", worker.Employments, (e, job) => e
            .Set("CodigoMensaje", job.MessageCode)
            .Set("Mensaje", job.Message)
            .Set("Cuit", job.Employer)
            .Set("RazonSocial", job.EmployerName)
            .Set("FechaInicioRelacionLaboral", job.Since.DayMonthYear())
            .Set("DomicilioLaboral", job.Address)
            .Set("CodigoDomicilioLaboral", job.AddressCode)
            .Set("UltimaVezQueFuisteDeclaradoPorEsteEmpleador", job.LastDeclared)
            .Set("ModalidadContratacion", job.Hiring)
            .Set("EstadoAportesSeguridadSocial", job.SocialSecurity)
            .Set("EstadoAportesObraSocial", job.HealthContributions)
            .Set("EstadoContribucionesObraSocial", job.HealthEmployer));
        data.Find("MisAportesCasasParticulares")!.Repeat("Item", worker.Household, (e, job) => e
            .Set("CodigoMensaje", job.MessageCode)
            .Set("Mensaje", job.Message)
            .Set("Cuit", job.Employer)
            .Set("RazonSocial", job.EmployerName)
            .Set("ObraSocial", job.HealthInsurance)
            .Set("FechaInicioRelacionLaboral", job.Since.DayMonthYear())
            .Set("DomicilioLaboral", job.Address)
            .Set("CategoriaProfesional", job.Category)
            .Set("HorasSemanales", job.WeeklyHours)
            .Set("ModalidadContratacion", job.Hiring)
            .Set("AseguradoraDeRiesgosDelTrabajo", job.Insurer)
            .Set("UltimoPagoRegistrado", job.LastPayment));
        return call.Ok(answer);
    }

    private async Task<Worker> DefaultAsync(long cuil, CancellationToken ct)
    {
        var (first, last) = await padron.FindAsync(cuil, ct) is { } person ? PadronDirectory.NamesOf(person) : ("TRABAJADOR", "FICTICIO");
        var lastMonth = clock.Now.ToArgentina().AddMonths(-1).ToString("MM/yyyy", CultureInfo.InvariantCulture);
        return new Worker(cuil, first.ToUpperInvariant(), last.ToUpperInvariant(),
        [
            new Employment(30555555551, "EMPLEADOR FICTICIO DE ARCASIM S.A.", new DateOnly(2024, 3, 1), "CALLE FICTICIA 123, CIUDAD AUTONOMA BUENOS AIRES", 0,
                lastMonth, "TIEMPO INDETERMINADO", 1, 1, 1, 2001, $"Última vez que fuiste declarado por este empleador: Período {lastMonth}"),
        ], []);
    }
}
