using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>
/// What the customs ("DIA") services share: a Recibo (CodErr, DesError) or a
/// flat result (CodError, InfoAdicional, codigoError) that carries the outcome
/// even when the call succeeded, the DIA's common texts, and the filler the
/// contract's sample leaves in fields a rule did not set.
/// </summary>
internal static class Dia
{
    private const string Filler = "SIMULADO";

    private static readonly string[] CodeNames = ["CodErr", "codError", "CodError", "codigoError"];

    public const string NoData = "No hay datos para los criterios ingresados";

    public static string MissingText(string field) => $"Falta dato obligatorio {field}";

    /// <summary>The outcome in the answer's own receipt: the code, and the text in the field the service names it (DesError, DescErr...), or none.</summary>
    public static XElement Receipt(this XElement answer, long code, string? text, string textField = "DesError")
    {
        var codeElement = answer.DescendantsAndSelf().First(e => CodeNames.Contains(e.Name.LocalName));
        codeElement.Value = code.ToString(CultureInfo.InvariantCulture);
        foreach (var old in codeElement.ElementsAfterSelf().Where(e => e.Name.LocalName == textField).ToList()) old.Remove();
        if (text is not null) codeElement.AddAfterSelf(new XElement(codeElement.Name.Namespace + textField, text));
        return answer;
    }

    /// <summary>A successful answer, without the fields the sample filled and the rules did not.</summary>
    public static ContractAnswer Done(this ServiceCall call, XElement answer) => call.Ok(answer.Clean());

    /// <summary>
    /// A business error in the service's error block, the way call.Error writes
    /// it, with the counters the refused answer still carries back at zero and
    /// no sample filler around it.
    /// </summary>
    public static ContractAnswer Fail(this ServiceCall call, long code, string text)
    {
        var answer = call.Error(code, text);
        if (answer.Body is not { } body) return answer;
        var codeElement = body.Descendants().FirstOrDefault(e => CodeNames.Contains(e.Name.LocalName) || e.Name.LocalName == "Codigo");
        var block = codeElement?.Parent;
        foreach (var leaf in body.Descendants().Where(e => !e.HasElements && e.Value == "1" && e != codeElement && (block is null || !e.Ancestors().Contains(block))))
            leaf.Value = "0";
        body.Clean();
        return answer;
    }

    /// <summary>Removes the leaves the sample filled with its placeholder text: the optional fields a rule had nothing for.</summary>
    public static XElement Clean(this XElement answer)
    {
        answer.Descendants().Where(e => !e.HasElements && e.Value == Filler).ToList().ForEach(e => e.Remove());
        return answer;
    }

    /// <summary>The first of the fields that came empty, to answer 42034 with its name.</summary>
    public static string? FirstMissing(XElement? scope, params string[] fields) =>
        fields.FirstOrDefault(f => string.IsNullOrWhiteSpace(scope?.Elements().FirstOrDefault(e => e.Name.LocalName == f)?.Value));

    /// <summary>A field of the business argument itself, not one with the same name deeper down or in the authentication.</summary>
    public static string Field(this XElement? scope, string name) =>
        scope?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? "";

    public static XElement? Arg(this ServiceCall call, string name) =>
        call.Request.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    /// <summary>The DIA's dd/mm/aaaa dates (ActualizaDispositivo, InicioCargaSuelta).</summary>
    public static bool TryDayMonthYear(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static XName Name(this ServiceCall call, string local) => XName.Get(local, call.Contract.TargetNamespace);
}

/// <summary>
/// The calls a service is still processing, by key: a repeated transaction
/// that arrives while the first is in course is told to wait (wdepMovimientos'
/// 31209, tiendas libres' 41973) instead of being processed twice.
/// </summary>
internal sealed class InFlight
{
    private readonly ConcurrentDictionary<string, byte> _running = new();

    public IDisposable? TryEnter(string key) => _running.TryAdd(key, 0) ? new Exit(_running, key) : null;

    private sealed class Exit(ConcurrentDictionary<string, byte> running, string key) : IDisposable
    {
        public void Dispose() => running.TryRemove(key, out _);
    }
}
