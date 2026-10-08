using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ArcaSim.Application.Contracts;

/// <summary>What a catalog text or header can mention: the moment, the ticket that came and the request it came in.</summary>
public sealed record PlaceholderValues(DateTimeOffset Now)
{
    public string Service { get; init; } = "";
    public long Cuit { get; init; }
    public string? Detail { get; init; }
    public string? TokenService { get; init; }
    public long GenerationTime { get; init; }
    public long ExpirationTime { get; init; }
    public string? Token { get; init; }
    public string? Sign { get; init; }
    public string? Element { get; init; }
    public string? Expected { get; init; }

    /// <summary>Where {seq:start} counts. A host passes its own, so a sequence runs across answers until the simulation resets.</summary>
    public PlaceholderCounters Counters { get; init; } = new();
}

/// <summary>The sequences {seq:start} draws from, one per service and start: what ARCA's servers number their transactions with.</summary>
public sealed class PlaceholderCounters
{
    private readonly ConcurrentDictionary<string, long> _last = new(StringComparer.OrdinalIgnoreCase);

    public long Next(string service, long first) => _last.AddOrUpdate($"{service}|{first}", first, (_, last) => last + 1);

    public void Reset() => _last.Clear();
}

/// <summary>
/// Fills the placeholders of the catalog's texts, headers and values (see
/// AuthErrors). Dates go out in Argentina's time, the one ARCA's servers
/// print; an unknown placeholder, or a brace that is just text, stays as it is.
/// </summary>
public static partial class Placeholders
{
    [GeneratedRegex(@"\{(\w+)(?::([^{}]*))?\}")]
    private static partial Regex Placeholder();

    public static string Fill(string text, PlaceholderValues values) =>
        text.Contains('{') ? Placeholder().Replace(text, m => Value(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null, values) ?? m.Value) : text;

    private static string? Value(string name, string? argument, PlaceholderValues v) => (name, argument) switch
    {
        ("cuit", null) => v.Cuit.ToString(CultureInfo.InvariantCulture),
        ("detail", null) => v.Detail ?? "",
        ("service", null) => v.TokenService ?? "",
        ("token", null) => v.Token ?? "",
        ("sign", null) => v.Sign ?? "",
        ("signbytes", null) => Base64Length(v.Sign).ToString(CultureInfo.InvariantCulture),
        ("element", null) => v.Element ?? "",
        ("expected", null) => v.Expected ?? "",
        ("uuid", null) => Guid.NewGuid().ToString(),
        ("now", _) => Moment(v.Now.ToUnixTimeMilliseconds(), argument),
        ("exp", _) => Moment(v.ExpirationTime * 1000, argument),
        ("gen", _) => Moment(v.GenerationTime * 1000, argument),
        ("seq", { } start) when long.TryParse(start, CultureInfo.InvariantCulture, out var first) =>
            v.Counters.Next(v.Service, first).ToString(CultureInfo.InvariantCulture),
        ("digits", { } n) when int.TryParse(n, CultureInfo.InvariantCulture, out var count) => Random("0123456789", count),
        ("letters", { } n) when int.TryParse(n, CultureInfo.InvariantCulture, out var count) =>
            Random("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", count),
        _ => null,
    };

    /// <summary>Epoch seconds without a format, epoch milliseconds with "ms", else the date in Argentina's time.</summary>
    private static string Moment(long milliseconds, string? format) => format switch
    {
        null => (milliseconds / 1000).ToString(CultureInfo.InvariantCulture),
        "ms" => milliseconds.ToString(CultureInfo.InvariantCulture),
        _ => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToArgentina().ToString(format, CultureInfo.InvariantCulture),
    };

    /// <summary>How many bytes a lenient base64 decoder (Java's) reads from the text: "abc" gives 2.</summary>
    private static int Base64Length(string? text)
    {
        var chars = (text ?? "").Count(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/');
        return chars * 3 / 4;
    }

    private static string Random(string alphabet, int count) =>
        string.Create(Math.Clamp(count, 0, 256), alphabet, (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        });
}
