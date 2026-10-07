using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A communication in a CUIT's Ventanilla Electrónica inbox. State 1 is unread, 2 read.</summary>
public sealed record Communication(
    long Id,
    long Cuit,
    DateTimeOffset PublishedAt,
    DateOnly? ExpiresOn,
    long PublisherId,
    string? Subject,
    string Message,
    int Priority,
    int State,
    string? Reference1,
    string? Reference2,
    bool Internal,
    List<Attachment> Attachments);

public sealed record Attachment(string FileName, byte[] Content);

/// <summary>A system that publishes in Ventanilla Electrónica, with its subservices.</summary>
public sealed record Publisher(long Id, string Description, List<Subservice> Subservices);

public sealed record Subservice(string Name, string Description);

/// <summary>
/// The Ventanilla Electrónica inbox: where ARCA's systems publish what
/// veconsumerws then lists and reads. Nobody publishes through ARCA's API, so
/// each CUIT's inbox starts with three plainly fictitious communications. A
/// test or an operator that wants its own puts them as documents in
/// Communications; SkipSeedAsync of the CUIT's Scope first leaves the inbox
/// with only those.
/// </summary>
public static class VentanillaInbox
{
    public const string Communications = "veconsumerws.comunicaciones";
    public const string Publishers = "veconsumerws.sistemas";

    public static string Key(long id) => id.ToString("D12", CultureInfo.InvariantCulture);

    public static string Scope(long cuit) => $"{Communications}/{cuit}";

    /// <summary>
    /// ArcaSim's own publishing systems: ARCA's list is not in the manual, so
    /// these are fictitious and say so. More can be put in the collection.
    /// </summary>
    public static IEnumerable<(string, Publisher)> DefaultPublishers() =>
    [
        (Key(1), new Publisher(1, "ARCASIM - NOTIFICACIONES DE PRUEBA", [new("avisos", "Avisos ficticios de ArcaSim")])),
        (Key(2), new Publisher(2, "ARCASIM - RECORDATORIOS DE PRUEBA", [new("vencimientos", "Recordatorios ficticios de ArcaSim")])),
    ];

    public static async Task SeedAsync(IDocumentStore store, long cuit, DateTimeOffset now, CancellationToken ct)
    {
        await store.SeedAsync(Publishers, Publishers, DefaultPublishers(), ct);
        await store.SeedAsync<Communication>(Scope(cuit), Communications, async token =>
        {
            var today = ArgentinaTime.StartOf(now.ArgentinaDate());
            var ids = new List<long>();
            for (var i = 0; i < 3; i++) ids.Add(await store.NextAsync(Communications, token));
            return
            [
                (Key(ids[0]), new Communication(ids[0], cuit, today.AddDays(-10).AddHours(9), DateOnly.FromDateTime(today.AddDays(20).Date), 1,
                    "Comunicacion de prueba de ArcaSim",
                    "Esta comunicacion es ficticia: la publica ArcaSim para que la bandeja no este vacia.",
                    2, 1, null, null, false, [])),
                (Key(ids[1]), new Communication(ids[1], cuit, today.AddDays(-3).AddHours(11), DateOnly.FromDateTime(today.AddDays(10).Date), 2,
                    null,
                    "Recordatorio ficticio de ArcaSim: vence un plazo de prueba. No es una comunicacion de ARCA.",
                    1, 1, "ARCASIM-0001", null, false, [])),
                (Key(ids[2]), new Communication(ids[2], cuit, today.AddDays(-1).AddHours(15), null, 1,
                    "Adjunto de prueba de ArcaSim",
                    "Comunicacion ficticia de ArcaSim con un adjunto de texto.",
                    3, 1, null, null, false, [new("constancia-arcasim.txt", "Adjunto ficticio generado por ArcaSim."u8.ToArray())])),
            ];
        }, ct);
    }
}

/// <summary>
/// Ventanilla Electrónica (veconsumerws, docs/arca/servicios/veconsumerws.md):
/// each CUIT's inbox, listed by consultarComunicaciones with the manual's
/// filters, pages and errors 100 to 111, read by consumirComunicacion (which
/// marks it read: estado 1 to 2, or answers 104, 105, 110), the three states
/// and the publishing systems. Business errors are faults "Error NNN: text".
/// ArcaSim's choices where the manual is silent: dates in filters are
/// yyyy-MM-dd (102 otherwise); without resultadosPorPagina a page holds the
/// maximum, 500; a page past the last is 100 only when there are items;
/// tiempoDeVida is the days between publication and expiry (0 without one);
/// attachments travel inline as base64 (MTOM is the engine's).
/// </summary>
public sealed class VentanillaRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const int MaxPageSize = 500;
    private const int MaxDays = 31;
    private const int OldestDays = 360;

    private static readonly (int Id, string Description)[] States =
    [
        (1, "Comunicacion No Leida"),
        (2, "Comunicacion Leida"),
        (0, "Comunicacion sin procesar - No disponible"),
    ];

    public string Service => "veconsumerws";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name == "dummy") return null;
        await VentanillaInbox.SeedAsync(store, call.Cuit, clock.Now, ct);
        return call.Name switch
        {
            "consultarComunicaciones" => await ListAsync(call, ct),
            "consumirComunicacion" => await ReadAsync(call, ct),
            "consultarEstados" => call.Ok(call.Sample().Repeat("Estado", States, (e, s) => e.Set("id", s.Id).Set("descripcion", s.Description))),
            "consultarSistemasPublicadores" => await PublishersAsync(call, ct),
            _ => null,
        };
    }

    private async Task<ContractAnswer> ListAsync(ServiceCall call, CancellationToken ct)
    {
        var filter = call.Request.Find("filter") ?? new XElement("filter");
        var today = clock.Today();

        var fromText = filter.Text("fechaDesde") ?? "";
        if (ParseDate(fromText) is not { } from) return Failure(call, 102, $"Formato de fecha no soportado para [{fromText}]. Se esperaba [yyyy-MM-dd]");
        var toText = filter.OptionalText("fechaHasta");
        DateOnly? to = null;
        if (toText is not null)
        {
            if (ParseDate(toText) is not { } parsed) return Failure(call, 102, $"Formato de fecha no soportado para [{toText}]. Se esperaba [yyyy-MM-dd]");
            to = parsed;
        }
        var oldest = today.AddDays(-OldestDays);
        if (from < oldest)
            return Failure(call, 101, $"Fecha desde no soportada. Mínima fecha [{oldest.ToString("dd/MM/yy", CultureInfo.InvariantCulture)} 00:00]");
        if (to is { } until && from > until) return Failure(call, 108, $"Fecha desde [{fromText}] se solapa con Fecha hasta [{toText}]");
        if ((to ?? today).DayNumber - from.DayNumber > MaxDays)
            return Failure(call, 111, $"La cantidad de días no puede superar los [{MaxDays}] entre la fechaDesde y fechaHasta o entre la fechaDesde y la fecha actual");

        var idFrom = filter.OptionalLong("comunicacionIdDesde");
        var idTo = filter.OptionalLong("comunicacionIdHasta");
        if (idFrom is { } low && idTo is { } high && low > high)
            return Failure(call, 107, $"Id Comunicación desde [{low}] se solapa con Id Comunicación hasta [{high}]");
        var state = filter.OptionalLong("estado");
        if (state is { } s && States.All(x => x.Id != s)) return Failure(call, 103, $"Código de estado inválido [{s}]");
        var publisher = filter.OptionalLong("sistemaPublicadorId");
        if (publisher is { } p && await store.GetAsync<Publisher>(VentanillaInbox.Publishers, VentanillaInbox.Key(p), ct) is null)
            return Failure(call, 109, $"idSistema [{p}] no es valido");
        var size = filter.OptionalLong("resultadosPorPagina") ?? 0;
        if (size is < 0 or > MaxPageSize) return Failure(call, 106, $"Cantidad de ítems por página no válida [{size}]");
        if (size == 0) size = MaxPageSize;

        var attachment = filter.Flag("tieneAdjunto");
        var reference1 = filter.OptionalText("referencia1");
        var reference2 = filter.OptionalText("referencia2");
        var found = (await store.ListAsync<Communication>(VentanillaInbox.Communications, "", ct))
            .Where(c => c.Cuit == call.Cuit && !c.Internal)
            .Where(c => c.PublishedAt.ArgentinaDate() is var day && day >= from && (to is null || day <= to))
            .Where(c => (idFrom is null || c.Id >= idFrom) && (idTo is null || c.Id <= idTo))
            .Where(c => state is null || c.State == state)
            .Where(c => publisher is null || c.PublisherId == publisher)
            .Where(c => attachment is null || c.Attachments.Count > 0 == attachment)
            .Where(c => (reference1 is null || c.Reference1 == reference1) && (reference2 is null || c.Reference2 == reference2))
            .OrderBy(c => c.Id)
            .ToList();

        var pages = (int)Math.Ceiling(found.Count / (double)size);
        var page = filter.Text("pagina") is { Length: > 0 } ? filter.Int("pagina") : 1;
        if (page < 1 || (found.Count > 0 && page > pages)) return Failure(call, 100, $"Número de página inválida [{page}]");

        var publishers = await PublisherNamesAsync(ct);
        var answer = call.Sample()
            .Set("pagina", page).Set("totalPaginas", pages).Set("itemsPorPagina", size).Set("totalItems", found.Count)
            .Repeat("ComunicacionSimplificada", found.Skip((page - 1) * (int)size).Take((int)size),
                (e, c) => Fill(e, c, publishers, c.PublishedAt.ToArgentina().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        return call.Ok(answer);
    }

    private async Task<ContractAnswer> ReadAsync(ServiceCall call, CancellationToken ct)
    {
        var id = call.Request.Long("idComunicacion");
        var communication = await store.GetAsync<Communication>(VentanillaInbox.Communications, VentanillaInbox.Key(id), ct);
        if (communication is null) return Failure(call, 104, $"La Comunicación [{id}] no existe");
        if (communication.Cuit != call.Cuit)
            return Failure(call, 105, $"La CUIT representada [{call.Cuit}] no es la destinataria de la Comunicación indicada [{id}]");
        if (communication.Internal)
            return Failure(call, 110, $"La Comunicación por la que se está consultando [{id}] no es posible obtenerla a través de este servicio");

        if (communication.State == 1)
        {
            communication = communication with { State = 2 };
            await store.PutAsync(VentanillaInbox.Communications, VentanillaInbox.Key(id), communication, ct);
        }

        var withContent = call.Request.Flag("incluirAdjuntos") == true;
        var published = communication.PublishedAt.ToArgentina();
        var answer = call.Sample();
        var body = answer.Find("Comunicacion")!;
        Fill(body, communication, await PublisherNamesAsync(ct), published.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ".0");
        var lifetime = communication.ExpiresOn is { } expires ? expires.DayNumber - DateOnly.FromDateTime(published.DateTime).DayNumber : 0;
        body.Set("mensaje", communication.Message).Set("tiempoDeVida", lifetime);
        body.Repeat("adjunto", communication.Attachments, (e, a) => e
            .Set("filename", a.FileName)
            .SetOrDrop("content", withContent ? Convert.ToBase64String(a.Content) : null)
            .Set("compressed", false).Set("signed", false).Set("encrypted", false).Set("processed", false).Set("public", false)
            // MD5 because the contract has an md5 element for the attachment (veconsumerws.md). It identifies the content; it protects nothing.
            .Set("md5", Convert.ToHexString(MD5.HashData(a.Content)).ToLowerInvariant())
            .Set("contentSize", a.Content.LongLength));
        return call.Ok(answer);
    }

    private async Task<ContractAnswer> PublishersAsync(ServiceCall call, CancellationToken ct)
    {
        var all = await store.ListAsync<Publisher>(VentanillaInbox.Publishers, "", ct);
        if (call.Request.OptionalLong("idSistemaPublicador") is { } id)
        {
            all = all.Where(p => p.Id == id).ToList();
            if (all.Count == 0) return Failure(call, 109, $"idSistema [{id}] no es valido");
        }
        var answer = call.Sample().Repeat("Sistema", all, (e, p) => e
            .Set("id", p.Id).Set("descripcion", p.Description).Drop("certCNs")
            .Repeat("Subservicio", p.Subservices, (s, sub) => s.Set("nombre", sub.Name).Set("descripcion", sub.Description)));
        return call.Ok(answer);
    }

    private static void Fill(XElement element, Communication c, IReadOnlyDictionary<long, string> publishers, string published) => element
        .Set("idComunicacion", c.Id)
        .Set("cuitDestinatario", c.Cuit)
        .Set("fechaPublicacion", published)
        .SetOrDrop("fechaVencimiento", c.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        .Set("sistemaPublicador", c.PublisherId)
        .Set("sistemaPublicadorDesc", publishers.GetValueOrDefault(c.PublisherId, ""))
        .Set("estado", c.State)
        .Set("estadoDesc", States.First(s => s.Id == c.State).Description)
        .Set("asunto", c.Subject ?? c.Message[..Math.Min(50, c.Message.Length)])
        .Set("prioridad", c.Priority)
        .Set("tieneAdjunto", c.Attachments.Count > 0)
        .SetOrDrop("referencia1", c.Reference1)
        .SetOrDrop("referencia2", c.Reference2);

    private async Task<IReadOnlyDictionary<long, string>> PublisherNamesAsync(CancellationToken ct) =>
        (await store.ListAsync<Publisher>(VentanillaInbox.Publishers, "", ct)).ToDictionary(p => p.Id, p => p.Description);

    private static DateOnly? ParseDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date
        : DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? DateOnly.FromDateTime(moment)
        : null;

    private static ContractAnswer Failure(ServiceCall call, int code, string message) => call.Fault($"Error {code}: {message}");
}
