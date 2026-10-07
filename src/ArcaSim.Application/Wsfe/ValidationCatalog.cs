using System.Text.Json;

namespace ArcaSim.Application.Wsfe;

/// <summary>A validation that failed: its code, the text ARCA answers with, and whether it stops the voucher.</summary>
public sealed record Finding(int Code, string Message, bool Rejects)
{
    public Obs ToObs() => new() { Code = Code, Msg = Message };

    public Err ToErr() => new() { Code = Code, Msg = Message };
}

/// <summary>
/// The 496 validation codes of the WSFEv1 manual (Data/codigos.json, made by
/// tools/extract_codes.py). A code says whether it rejects or only observes,
/// and its message is the one ARCA was seen sending or, failing that, the
/// manual's description of the rule.
/// </summary>
public sealed class ValidationCatalog
{
    private readonly Dictionary<string, string> _literals;
    private readonly Dictionary<(string Method, int Code), Entry> _codes;

    private ValidationCatalog(Dictionary<string, string> literals, Dictionary<(string, int), Entry> codes)
    {
        _literals = literals;
        _codes = codes;
    }

    public static ValidationCatalog Load()
    {
        using var stream = EmbeddedData.Open("codigos.json");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, EmbeddedData.Json)
            ?? throw new InvalidOperationException("codigos.json is empty.");
        var codes = new Dictionary<(string, int), Entry>();
        // The manual numbers two FECAEARegInformativo checks 1445 (an FCE rejection and a date
        // observation). No rule raises 1445, so the first stays; a test keeps it the only repeat.
        foreach (var entry in file.Codes)
            codes.TryAdd((entry.Method, entry.Code), entry);
        return new ValidationCatalog(file.Literals, codes);
    }

    public IEnumerable<(string Method, int Code)> Codes => _codes.Keys;

    /// <summary>A finding for a code of the given method's table.</summary>
    /// <param name="variant">Picks one of the code's literal messages, such as "Obs:1" for 10016's date range on products.</param>
    public Finding For(string method, int code, string? variant = null) =>
        new(code, Message(code, variant) ?? Find(method, code).ManualText, Find(method, code).Kind == "Rechazo");

    /// <summary>A finding with a message of its own, for texts that carry values ("Informado: 2, Enviado:3").</summary>
    public Finding WithMessage(string method, int code, string message) =>
        new(code, message, Find(method, code).Kind == "Rechazo");

    /// <summary>The literal ARCA sends for a code, if one was ever captured.</summary>
    public string? Message(int code, string? variant = null) =>
        variant is not null && _literals.TryGetValue($"{code}:{variant}", out var specific) ? specific
        : _literals.TryGetValue(code.ToString(), out var general) ? general
        : null;

    private Entry Find(string method, int code) =>
        _codes.TryGetValue((method, code), out var entry)
            ? entry
            : throw new ArgumentException($"Code {code} is not in {method}'s table.", nameof(code));

    private sealed record CatalogFile(Dictionary<string, string> Literals, List<Entry> Codes);

    private sealed record Entry(string Method, string Group, int Code, string Kind, string Field, string ManualText);
}

internal static class EmbeddedData
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static Stream Open(string name) =>
        typeof(EmbeddedData).Assembly.GetManifestResourceStream($"ArcaSim.Wsfe.{name}")
        ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
}
