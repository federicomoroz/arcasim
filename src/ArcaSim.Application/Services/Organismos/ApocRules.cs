using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A case in the APOC base: a CUIT whose invoices ARCA declared apócrifas.</summary>
public sealed record ApocCase(long Cuit, string Description, DateOnly DetectedOn, DateOnly PublishedOn);

/// <summary>
/// Contribuyentes y facturas apócrifas (wsapoc, docs/arca/servicios/wsapoc.md):
/// the APOC base kept in wsapoc.publicaciones, listed whole by GetAll, by
/// publication date by GetAllByPublicacion (dates DD/MM/YYYY, error 200 when
/// malformed) and per CUIT by GetPublicacionAPOC. The seed has three plainly
/// fictitious cases (20888888889, 30888888884, 20777777778). ArcaSim's
/// choices: codigo "0" goes with "Ejecución OK", the manual's description
/// of that code; a CUIT that is not in the base gets codigo 0 and an empty
/// resultados; error 200's texts are ArcaSim's.
/// </summary>
public sealed class ApocRules(IDocumentStore store) : IServiceBehavior
{
    public const string Cases = "wsapoc.publicaciones";

    public static IEnumerable<(string, ApocCase)> Defaults() =>
    [
        ("20888888889", new ApocCase(20888888889, "FACTURACION APOCRIFA - CASO FICTICIO DE ARCASIM", new(2025, 3, 10), new(2025, 3, 17))),
        ("30888888884", new ApocCase(30888888884, "FACTURACION APOCRIFA - CASO FICTICIO DE ARCASIM", new(2025, 11, 4), new(2025, 11, 12))),
        ("20777777778", new ApocCase(20777777778, "FACTURACION APOCRIFA - CASO FICTICIO DE ARCASIM", new(2026, 8, 20), new(2026, 9, 1))),
    ];

    public string Service => "wsapoc";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name is not ("GetAll" or "GetPublicacionAPOC" or "GetAllByPublicacion")) return null;
        await store.SeedAsync(Cases, Cases, Defaults(), ct);
        var all = await store.ListAsync<ApocCase>(Cases, "", ct);

        switch (call.Name)
        {
            case "GetPublicacionAPOC":
                var cuit = call.Request.Long("cuit");
                if (!Cuits.IsValid(cuit)) return Answer(call, "200", $"Errores de validación de parámetros: la CUIT {call.Request.Text("cuit")} no es valida.", null);
                return Answer(call, "0", "Ejecución OK", all.Where(c => c.Cuit == cuit));
            case "GetAllByPublicacion":
                var (from, fromOk) = Parse(call.Request.Text("desde"));
                var (to, toOk) = Parse(call.Request.Text("hasta"));
                if (!fromOk || !toOk)
                    return Answer(call, "200", $"Errores de validación de parámetros: la fecha {(fromOk ? call.Request.Text("hasta") : call.Request.Text("desde"))} no tiene el formato DD/MM/YYYY.", null);
                return Answer(call, "0", "Ejecución OK", all.Where(c => (from is null || c.PublishedOn >= from) && (to is null || c.PublishedOn <= to)));
            default:
                return Answer(call, "0", "Ejecución OK", all);
        }
    }

    private static ContractAnswer Answer(ServiceCall call, string code, string description, IEnumerable<ApocCase>? cases)
    {
        var answer = call.Sample().Set("codigo", code).Set("descripcion", description);
        if (cases is null) answer.Drop("resultados");
        else answer.Repeat("PublicacionAPOC", cases.OrderBy(c => c.PublishedOn).ThenBy(c => c.Cuit), (e, c) => e
            .Set("Cuit", c.Cuit)
            .Set("Descripcion", c.Description)
            .Set("FechaCondicion", c.DetectedOn.DayMonthYear())
            .Set("FechaPublicacion", c.PublishedOn.DayMonthYear()));
        return call.Ok(answer);
    }

    private static (DateOnly? Date, bool Ok) Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return (null, true);
        return DateOnly.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? (date, true) : (null, false);
    }
}
