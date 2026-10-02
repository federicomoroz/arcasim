using System.Security.Cryptography.X509Certificates;

namespace ArcaSim.Application.Wsaa;

/// <summary>
/// Writes a DN the way ARCA does, which is Java's X500Principal.toString():
/// last RDN first, ", " between them, SERIALNUMBER in capitals
/// (docs/arca/wsaa.md §4.2). .NET's own formatting changes between Windows and
/// Linux, so the RDNs are walked by hand.
/// </summary>
public static class DistinguishedNames
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["2.5.4.3"] = "CN",
        ["2.5.4.5"] = "SERIALNUMBER",
        ["2.5.4.6"] = "C",
        ["2.5.4.7"] = "L",
        ["2.5.4.8"] = "ST",
        ["2.5.4.10"] = "O",
        ["2.5.4.11"] = "OU",
    };

    public static string ToArcaString(X500DistinguishedName name)
    {
        var parts = new List<string>();
        foreach (var rdn in name.EnumerateRelativeDistinguishedNames())
        {
            var oid = rdn.GetSingleElementType().Value ?? "";
            var label = Labels.TryGetValue(oid, out var known) ? known : oid;
            parts.Add($"{label}={rdn.GetSingleElementValue()}");
        }
        parts.Reverse();
        return string.Join(", ", parts);
    }

    /// <summary>The CUIT in "SERIALNUMBER=CUIT 20123456789", if the certificate has one.</summary>
    public static long? CuitOf(X500DistinguishedName name)
    {
        foreach (var rdn in name.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.GetSingleElementType().Value != "2.5.4.5") continue;
            var value = rdn.GetSingleElementValue() ?? "";
            return value.StartsWith("CUIT ", StringComparison.OrdinalIgnoreCase) && long.TryParse(value[5..], out var cuit)
                ? cuit
                : null;
        }
        return null;
    }

    public static string? CommonNameOf(X500DistinguishedName name) =>
        name.EnumerateRelativeDistinguishedNames()
            .Where(r => r.GetSingleElementType().Value == "2.5.4.3")
            .Select(r => r.GetSingleElementValue())
            .FirstOrDefault();

    /// <summary>
    /// Compares two DNs as WSAA does for "source" and "destination": the same
    /// attributes with the same values, regardless of order, spacing or case.
    /// </summary>
    public static bool AreEquivalent(string left, string right) => Canonical(left) == Canonical(right);

    private static string Canonical(string dn) =>
        string.Join(",", dn.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Select(part =>
            {
                var equals = part.IndexOf('=');
                return equals < 0
                    ? part.ToLowerInvariant()
                    : $"{part[..equals].Trim().ToLowerInvariant()}={part[(equals + 1)..].Trim().ToLowerInvariant()}";
            })
            .Order(StringComparer.Ordinal));
}
