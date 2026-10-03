using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>
/// A declaration's legajo in the Depositario Fiel regime: ENDO when the
/// documentation was delivered, PSAD once its PSAD received and accepted it,
/// DIGI once digitized. Ticket identifies a 001 (documentación adicional).
/// </summary>
public sealed record Legajo(
    string NroLegajo, string Codigo, string Ticket, long Psad, long Declarante, string DescDeclarante, long Ie, string DescIe,
    decimal ImporteLiq, DateTimeOffset FechaOfic, DateTimeOffset FechaEndo, string Estado,
    DateTimeOffset? Recepcion = null, DateTimeOffset? Digitalizacion = null);

/// <summary>
/// The legajos wDigDepFiel moves and wConsDepFiel reads (docs/arca/servicios/wDigDepFiel.md
/// and wConsDepFiel.md). ArcaSim's choices: nothing in ArcaSim oficializes
/// declarations, so the first PndListaEndo of a PSAD with no legajos finds two
/// carpetas completas (000) endorsed to it that day; the state after
/// AvisoRecepAcept, which the manual does not name, is PSAD (the state
/// AvisoDigit accepts besides ENDO); FechaVtoPSAD is five business days after
/// ENDO and FechaVtoDIGI five business days after the reception, the term the
/// manual gives to digitize, with 0001-01-01 while there is none.
/// </summary>
internal static class Legajos
{
    public const string Collection = "wDigDepFiel.legajos";
    public const long Declarante = 20222222223;
    public const long Ie = 30000000007;

    public static string Key(string legajo, string code, string ticket = "") => ticket == "" ? $"{legajo}/{code}" : $"{legajo}/{code}/{ticket}";

    public static async Task SeedAsync(IDocumentStore store, long psad, DateTimeOffset now, CancellationToken ct)
    {
        if ((await store.ListAsync<Legajo>(Collection, "", ct)).Any(l => l.Psad == psad)) return;
        for (var i = 0; i < 2; i++)
        {
            var number = await store.NextAsync("wDigDepFiel.legajos", ct);
            // A detailed declaration's number: AA BBB CCCC DDDDDD E (year, aduana, type, number, check letter).
            var id = $"{now:yy}001IC04{number:D6}{(char)('A' + number % 26)}";
            await store.PutAsync(Collection, Key(id, "000"), new Legajo(id, "000", "", psad, Declarante, "DESPACHANTE DEL SIMULADOR",
                Ie, "IMPORTADORA DEL SIMULADOR SA", 125000m + number, now.AddDays(-2), now, "ENDO"), ct);
        }
    }

    public static DateTimeOffset BusinessDaysAfter(DateTimeOffset start, int days)
    {
        var date = start;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--;
        }
        return date;
    }

    public static readonly DateTimeOffset None = new(1, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The carpeta codes, from the reference table wgesTabRef serves.</summary>
    public static readonly string[] Codes = AduanaTables.Find("DFCOD_DESC")!.Rows.Select(r => r.Codigo).ToArray();

    public static string Required(string field) => $"Error Atributo/Parametro: \"{field}\" Obligatorio";

    public static string Format(string field) => $"Error Atributo/Parametro: \"{field}\" Formato Incorrecto";

    public static string Forbidden(string field) => $"Error Atributo/Parametro: \"{field}\" Prohibido";

    public const string BadCode = "Error Parametro: \"codigo\" Valor incorrecto";
}

/// <summary>
/// wConsDepFiel (docs/arca/servicios/wConsDepFiel.md): PndListaEndo lists a PSAD's
/// legajos in ENDO between two dates, ListaEstado gives one legajo's state, with
/// the manual's codes (pp.11, 14-15) and 0 "OK Procesado" on success.
/// </summary>
public sealed class WConsDepFielRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Ok = "OK Procesado";

    public string Service => "wConsDepFiel";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "PndListaEndo" => await PendingAsync(call, ct),
        "ListaEstado" => await StateAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> PendingAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argInPndListaEndo");
        var today = DateOnly.FromDateTime(clock.Now.ToArgentina().DateTime);
        if (arg?.Date("FechaDesde") is not { } from) return call.Fail(2, Legajos.Required("FechaDesde"));
        if (arg.Date("FechaHasta") is not { } to) return call.Fail(2, Legajos.Required("FechaHasta"));
        if (from > today) return call.Fail(5, "Error Fecha Desde mayor a fecha del dia");
        if (to > today) return call.Fail(6, "Error Fecha Hasta mayor a fecha del dia");
        if (from > to) return call.Fail(7, "Error Fecha Desde mayor a Fecha Hasta.");
        var code = arg.Field("CodigoCarpeta");
        if (code != "" && !Legajos.Codes.Contains(code)) return call.Fail(4, Legajos.BadCode);
        var declarant = arg.Field("CuitDeclarante");

        await Legajos.SeedAsync(store, call.Cuit, clock.Now.ToArgentina(), ct);
        var found = (await store.ListAsync<Legajo>(Legajos.Collection, "", ct))
            .Where(l => l.Psad == call.Cuit && l.Estado == "ENDO"
                        && DateOnly.FromDateTime(l.FechaEndo.DateTime) is var endo && endo >= from && endo <= to
                        && (code == "" || l.Codigo == code) && (declarant == "" || l.Declarante.ToString() == declarant))
            .ToList();
        if (found.Count == 0) return call.Fail(101, "No existen legajos en estado ENDO entre las fechas solicitadas");

        var answer = call.Sample().Receipt(0, Ok, "DescErr");
        answer.Repeat("Legajo", found, (row, l) => row
            .Set("CuitDeclarante", l.Declarante)
            .Set("DescDeclarante", l.DescDeclarante)
            .Set("CuitIE", l.Ie)
            .Set("DescIE", l.DescIe)
            .Set("NroLegajo", l.NroLegajo)
            .Set("Codigo", l.Codigo)
            .Set("Ticket", l.Ticket)
            .Set("ImporteLiq", l.ImporteLiq)
            .Set("FechaOfic", l.FechaOfic)
            .Set("FechaEndo", l.FechaEndo)
            .Set("OptoCambioVia", "N"));
        return call.Done(answer);
    }

    private async Task<ContractAnswer> StateAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argInListaEstado");
        if (Dia.FirstMissing(arg, "CodigoCarpeta", "NroLegajo") is { } missing) return call.Fail(2, Legajos.Required(missing));
        var number = arg.Field("NroLegajo");
        var code = arg.Field("CodigoCarpeta");
        var ticket = arg.Field("Ticket");
        if (number.Length != 16) return call.Fail(3, Legajos.Format("NroLegajo"));
        if (!Legajos.Codes.Contains(code)) return call.Fail(4, Legajos.BadCode);
        if (code != "000" && ticket == "") return call.Fail(5, $"Debe informar Ticket si el codigo de carpeta es \"{code}\"");

        if (await store.GetAsync<Legajo>(Legajos.Collection, Legajos.Key(number, code, code == "001" ? ticket : ""), ct) is not { } legajo)
            return call.Fail(101, "Legajo inexistente");
        if (legajo.Psad != call.Cuit) return call.Fail(102, "Usted no es depositario fiel del legajo informado");

        var answer = call.Sample().Receipt(0, Ok, "DescErr");
        answer.Find("LegajoEstado")!
            .Set("NroLegajo", legajo.NroLegajo)
            .Set("Codigo", legajo.Codigo)
            .Set("Estado", legajo.Estado)
            .Set("FechaVtoPSAD", Legajos.BusinessDaysAfter(legajo.FechaEndo, 5))
            .Set("FechaVtoDIGI", legajo.Recepcion is { } received ? Legajos.BusinessDaysAfter(received, 5) : Legajos.None)
            .Set("CuitIE", legajo.Ie)
            .Set("DescIE", legajo.DescIe)
            .Set("CuitDesp", legajo.Declarante)
            .Set("DescDesp", legajo.DescDeclarante);
        return call.Done(answer);
    }
}

/// <summary>
/// wDigDepFiel (docs/arca/servicios/wDigDepFiel.md): the PSAD's two notices on a
/// legajo, AvisoRecepAcept (received and accepted: ENDO to PSAD) and AvisoDigit
/// (digitized: to DIGI), with each method's own codes (pp.13-14 and 18-19),
/// which mean different things in each. A repeated notice is 111 "Legajo
/// Duplicado"; a 001 needs its 000 in ENDO or later to be received and in DIGI
/// to be digitized (104), and gets its own legajo, keyed by its ticket.
/// </summary>
public sealed class WDigDepFielRules(IDocumentStore store) : IServiceBehavior
{
    private static readonly string[] Places = ["R1", "R2", "R3", "R4", "A1"];

    public string Service => "wDigDepFiel";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "AvisoRecepAcept" => await ReceiveAsync(call, ct),
        "AvisoDigit" => await DigitizeAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> ReceiveAsync(ServiceCall call, CancellationToken ct)
    {
        var r = call.Request;
        if (Dia.FirstMissing(r, "nroLegajo", "cuitDeclarante", "cuitPSAD", "cuitIE", "codigo", "indLugarFisico") is { } missing)
            return call.Fail(2, Legajos.Required(missing));
        if (r.Field("cuitPSAD") != call.Cuit.ToString()) return call.Fail(1, "PSAD incorrecto");
        var number = r.Field("nroLegajo");
        var code = r.Field("codigo");
        var ticket = r.Field("ticket");
        if (number.Length != 16) return call.Fail(3, Legajos.Format("nroLegajo"));
        if (!Legajos.Codes.Contains(code)) return call.Fail(4, Legajos.BadCode);
        if (code == "001" && ticket == "") return call.Fail(5, "Error Parametro: \"ticket\" Valor incorrecto");
        if (code != "001" && ticket != "") return call.Fail(6, Legajos.Forbidden("ticket"));
        var sigea = r.Field("sigea");
        if (code is "000" or "001" or "100" or "101" && sigea != "") return call.Fail(6, Legajos.Forbidden("sigea"));
        if (code is "002" or "003" or "004" && sigea == "") return call.Fail(2, Legajos.Required("sigea"));
        if (!Places.Contains(r.Field("indLugarFisico"))) return call.Fail(3, Legajos.Format("indLugarFisico"));
        var hashing = r.Field("hashing");
        if (code == "100" && hashing == "") return call.Fail(2, Legajos.Required("hashing"));
        if (code != "100" && hashing != "") return call.Fail(6, Legajos.Forbidden("hashing"));

        var key = Legajos.Key(number, code, code == "001" ? ticket : "");
        var legajo = await store.GetAsync<Legajo>(Legajos.Collection, key, ct);
        if (code == "001" && legajo is null)
        {
            if (await store.GetAsync<Legajo>(Legajos.Collection, Legajos.Key(number, "000"), ct) is not { } folder) return call.Fail(101, "Nro de Legajo inválido");
            if (folder.Psad != call.Cuit) return call.Fail(107, "Declarante PSAD inválido");
            legajo = folder with { Codigo = "001", Ticket = ticket, Estado = "ENDO", Recepcion = null, Digitalizacion = null };
        }
        if (legajo is null) return call.Fail(101, "Nro de Legajo inválido");
        if (Mismatch(call, legajo, r) is { } refused) return refused;
        if (legajo.Estado != "ENDO") return call.Fail(111, "Legajo Duplicado");

        await store.PutAsync(Legajos.Collection, key, legajo with { Estado = "PSAD", Recepcion = Moment(r.Date("fechaHoraAcept")) }, ct);
        return Ok(call, "OK Procesado");
    }

    private async Task<ContractAnswer> DigitizeAsync(ServiceCall call, CancellationToken ct)
    {
        var r = call.Request;
        if (Dia.FirstMissing(r, "nroLegajo", "cuitDeclarante", "cuitIE", "codigo", "url", "hashing") is { } missing)
            return call.Fail(2, Legajos.Required(missing));
        var psad = r.Field("cuitPSAD");
        if (psad != "" && psad != call.Cuit.ToString()) return call.Fail(3, "PSAD incorrecto");
        if (psad == "" && r.Field("cuitDeclarante") != call.Cuit.ToString()) return call.Fail(1, "Declarante incorrecto");
        var number = r.Field("nroLegajo");
        var code = r.Field("codigo");
        var ticket = r.Field("ticket");
        if (number.Length != 16) return call.Fail(4, Legajos.Format("nroLegajo"));
        if (!Legajos.Codes.Contains(code)) return call.Fail(5, Legajos.BadCode);
        if (r.Field("hashing") is var hash && (hash.Length != 40 || !hash.All(Uri.IsHexDigit))) return call.Fail(4, Legajos.Format("hashing"));
        if (code == "001" && ticket == "") return call.Fail(9, Legajos.Required("ticket"));

        var families = r.Find("familias")?.Elements().Select(f => f.Field("codigo")).ToList() ?? [];
        if (code is "004" or "100" && families.Count > 0) return call.Fail(10, "Error Atributo/Parametro: \"familia\" no debe informarse");
        string[]? expected = code switch
        {
            "000" or "002" or "003" => ["01", "02", "03", "04", "05"],
            "101" => ["01", "02"],
            _ => null,
        };
        if (expected is not null && !families.Order().SequenceEqual(expected)) return call.Fail(4, Legajos.Format("familias"));
        if (code == "001" && families.Count != 1) return call.Fail(4, Legajos.Format("familias"));

        var key = Legajos.Key(number, code, code == "001" ? ticket : "");
        if (await store.GetAsync<Legajo>(Legajos.Collection, key, ct) is not { } legajo)
        {
            if (code == "001" && await store.GetAsync<Legajo>(Legajos.Collection, Legajos.Key(number, "000"), ct) is { } folder && folder.Estado != "DIGI")
                return call.Fail(104, "Transmisión no autorizada. Legajo no digitalizado");
            return call.Fail(101, "Nro de Legajo inválido");
        }
        if (Mismatch(call, legajo, r) is { } refused) return refused;
        if (legajo.Estado == "DIGI") return call.Fail(111, "Legajo Duplicado");
        if (legajo.Estado is not ("ENDO" or "PSAD")) return call.Fail(102, "Estado del Legajo inválido");

        await store.PutAsync(Legajos.Collection, key, legajo with { Estado = "DIGI" }, ct);
        return Ok(call, "OK procesado");
    }

    /// <summary>The legajo is the PSAD's, and the declarant and importer/exporter informed are the legajo's (106, 107, 108).</summary>
    private static ContractAnswer? Mismatch(ServiceCall call, Legajo legajo, XElement r)
    {
        if (legajo.Psad != call.Cuit) return call.Fail(107, call.Name == "AvisoDigit" ? "PSAD inválido" : "Declarante PSAD inválido");
        if (r.Field("cuitDeclarante") != legajo.Declarante.ToString()) return call.Fail(106, "Declarante inválido para Legajo");
        if (r.Field("cuitIE") != legajo.Ie.ToString()) return call.Fail(108, "El Legajo no se corresponde con el importador/exportador informado");
        return null;
    }

    private static DateTimeOffset? Moment(DateOnly? date) =>
        date is { } day ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3)) : null;

    private static ContractAnswer Ok(ServiceCall call, string text) => call.Done(call.Sample().Receipt(0, text, "descError"));
}
