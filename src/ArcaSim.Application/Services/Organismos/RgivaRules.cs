using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A buyer with the legend (and so the perception rate) the regime gives it.</summary>
public sealed record PerceptionSubject(long Cuit, string Name, int Legend);

/// <summary>
/// Régimen de percepción de IVA (wsrgiva, docs/arca/servicios/wsrgiva.md):
/// one constancia per CUIT of the batch, with the legend of the manual's
/// table, or an error constancia (4002 for a goods type other than 1, 4003 for
/// a CUIT nobody has). The legends come from wsrgiva.leyendas, seeded with the
/// seven homologación CUITs the manual lists; any other CUIT the padrón knows
/// gets one by its VAT condition (Responsable Inscripto 18, Exento 2,
/// monotributo 20, the rest 23). ArcaSim's choices: vigencia is the last day
/// of the month of the query; codigoSeguridad is the first 16 hex digits of a
/// SHA-256 of agent, CUIT, date and legend; the names of the seeded CUITs are
/// fictitious; 4002 and 4003 go one per CUIT.
/// </summary>
public sealed class RgivaRules(IDocumentStore store, PadronDirectory padron, IClock clock) : IServiceBehavior
{
    public const string Subjects = "wsrgiva.leyendas";
    private const string Missing = "El contribuyente registra falta de presentación de la";

    /// <summary>The legends of the manual, section 2.3.</summary>
    public static readonly IReadOnlyDictionary<int, string> Legends = new Dictionary<int, string>
    {
        [2] = "Alícuota 0% - Sujeto Exento o No Alcanzado en IVA",
        [3] = "Alícuota 8% - El sujeto registra alguna de las situaciones dispuestas en el segundo párrafo del artículo 8° de la RG 5319/2023",
        [4] = "Alícuota 7% - El contribuyente se encuentra obligado a emitir comprobantes clase \"M\"",
        [5] = "Alícuota 7% - El contribuyente registra en SIPER Categoría D: Alto o Categoría E: Muy Alto",
        [6] = "Alícuota 7% - El contribuyente presenta la C.U.I.T. limitada o inactiva",
        [7] = "Alícuota 5% - El contribuyente posee algunos de los domicilios fiscal, legal y/o comercial inválido",
        [8] = "Alícuota 5% - El contribuyente no tiene constituido el Domicilio fiscal electrónico.",
        [9] = $"Alícuota 3% - {Missing} Declaración Jurada de IVA período/s: XXXX",
        [10] = $"Alícuota 3% - {Missing} Declaración Jurada del Impuesto a las Ganancias período fiscal",
        [11] = $"Alícuota 3% - {Missing} Declaración Jurada de Seguridad Social período/s: XXXX",
        [12] = $"Alícuota 3% - {Missing} Declaración Jurada de Bienes Personales / Bienes Personales Acciones o Participaciones, período/s: XXXX",
        [13] = $"Alícuota 3% - {Missing} Declaración Jurada de Libro de Iva Digital período/s: XXXX",
        [14] = $"Alícuota 3% - {Missing} Declaración Jurada de Memoria y Estados Contables, período/s: XXXX",
        [15] = $"Alícuota 3% - {Missing} Declaración Jurada de Participaciones societarias, período/s: XXXX",
        [16] = "Alícuota 0% - El contribuyente se encuentra exceptuado de la percepción por estar comprendido en el inciso a) del Artículo 4° de la RG 5319/2023",
        [17] = "Alícuota 0% - El contribuyente se encuentra nominado como agente de percepción de la RG 5319/2023",
        [18] = "Alícuota 1% - Responsables inscriptos, sin incumplimientos.",
        [20] = "Sujeto adherido al RS de Monotributo. Se encuentra exceptuado de la percepción, salvo aplicación del Art. 9°de la RG 5319/2023",
        [22] = "Alícuota 8% - El sujeto registra alguna de las situaciones dispuestas en el segundo párrafo del artículo 8° de la RG 5319/2023",
        [23] = "Alícuota 8% - Sujeto no categorizado",
    };

    /// <summary>The homologación test CUITs of the manual (1.3) with their legends; the names are ArcaSim's.</summary>
    public static IEnumerable<(string, PerceptionSubject)> Defaults() =>
        new[] { (24219972942L, 4), (30709778079L, 5), (30636414936L, 6), (23922228094L, 7), (33692433349L, 8), (27140463618L, 23), (30684587559L, 2) }
            .Select(s => (s.Item1.ToString(CultureInfo.InvariantCulture), new PerceptionSubject(s.Item1, $"CONTRIBUYENTE DE PRUEBA LEYENDA {s.Item2}", s.Item2)));

    public string Service => "wsrgiva";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name != "consultarConstanciaPorLote_v2") return null;
        await store.SeedAsync(Subjects, Subjects, Defaults(), ct);
        var today = clock.Today();
        var date = today.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var validUntil = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

        var constancias = new List<XElement>();
        foreach (var item in call.Request.FindAll("datosTransaccion"))
        {
            var cuit = item.Long("cuitContribuyente");
            if (item.Int("tipoBienesInvolucrados") != 1)
            {
                constancias.Add(Failure(date, cuit, 4002, "Solo se admite tipo de bien 1"));
                continue;
            }
            if (await SubjectAsync(cuit, ct) is not { } subject)
            {
                constancias.Add(Failure(date, cuit, 4003, "CUIT Inexistente"));
                continue;
            }
            var code = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{call.Cuit}|{cuit}|{date}|{subject.Legend}")))[..16];
            constancias.Add(new XElement("constancia",
                new XElement("fechaConsulta", date),
                new XElement("idContribuyente", cuit),
                new XElement("descripcionContribuyente", subject.Name),
                new XElement("vigencia", validUntil),
                new XElement("codigoLeyenda", subject.Legend),
                new XElement("descripcionLeyenda", Legends.GetValueOrDefault(subject.Legend, "")),
                new XElement("codigoSeguridad", code)));
        }
        return call.Ok(new XElement(call.Operation.Output, new XElement("return", constancias)));
    }

    private async Task<PerceptionSubject?> SubjectAsync(long cuit, CancellationToken ct)
    {
        if (!Cuits.IsValid(cuit)) return null;
        if (await store.GetAsync<PerceptionSubject>(Subjects, cuit.ToString(CultureInfo.InvariantCulture), ct) is { } known) return known;
        if (await padron.FindAsync(cuit, ct) is not { } taxpayer) return null;
        var legend = taxpayer.VatCondition switch
        {
            VatCondition.ResponsableInscripto => 18,
            VatCondition.Exento => 2,
            VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido => 20,
            _ => 23,
        };
        return new PerceptionSubject(cuit, taxpayer.Name.ToUpperInvariant(), legend);
    }

    private static XElement Failure(string date, long cuit, int code, string text) => new("constancia",
        new XElement("fechaConsulta", date),
        new XElement("idContribuyente", cuit),
        new XElement("codigoError", code),
        new XElement("descripcionError", text));
}
