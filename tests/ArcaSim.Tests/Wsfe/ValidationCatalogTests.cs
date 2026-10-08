using System.Text.Json;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Tests.Wsfe;

/// <summary>The manual's codes as ArcaSim loads them: one entry per method and code, the manual's own repeat aside.</summary>
public class ValidationCatalogTests
{
    [Fact]
    public void The_only_code_the_manual_repeats_is_1445_of_FECAEARegInformativo()
    {
        using var stream = typeof(ValidationCatalog).Assembly.GetManifestResourceStream("ArcaSim.Wsfe.codigos.json")!;
        var codes = JsonDocument.Parse(stream).RootElement.GetProperty("codes").EnumerateArray()
            .Select(c => (Method: c.GetProperty("method").GetString(), Code: c.GetProperty("code").GetInt32()));

        var repeated = codes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Equal([("FECAEARegInformativo", 1445)], repeated);
        Assert.DoesNotContain(RuleCodes.All, r => r.Caea == 1445 || r.Cae == 1445);
    }
}
