using ArcaSim.Domain;

namespace ArcaSim.Application.Padron;

/// <summary>
/// The padrón's view of ArcaSim's taxpayers: the same records WSFEv1
/// invoices against, so a lookup and an invoice never disagree. With open
/// access a valid CUIT nobody loaded still exists, as an obviously made-up
/// Responsable Inscripto, the way WSFEv1 already treats it.
/// </summary>
public sealed class PadronDirectory(ITaxpayerRepository taxpayers, SimulationSettings settings)
{
    public async Task<Taxpayer?> FindAsync(long cuit, CancellationToken ct)
    {
        if (await taxpayers.FindAsync(cuit, ct) is { } known) return known;
        if (!settings.OpenAccess || !Cuits.IsValid(cuit)) return null;
        return new Taxpayer(cuit, Cuits.KindOf(cuit) == PersonKind.Juridica ? "CONTRIBUYENTE SIMULADO S.A." : "SIMULADO CONTRIBUYENTE",
            VatCondition.ResponsableInscripto);
    }

    /// <summary>Every CUIT behind a DNI: the people loaded with that document and, with open access, the CUIT the DNI would get.</summary>
    public async Task<IReadOnlyList<long>> ByDocumentAsync(string document, CancellationToken ct)
    {
        var all = await taxpayers.ListAsync(ct);
        var found = all.Where(t => t.Kind == PersonKind.Fisica && DocumentOf(t) == document).Select(t => t.Cuit).ToList();
        if (found.Count == 0 && settings.OpenAccess && long.TryParse(document, out var number) && number is > 0 and < 100_000_000)
            found.Add(Cuits.ForDocument(number));
        return found;
    }

    public static string DocumentOf(Taxpayer taxpayer) => Cuits.DocumentOf(taxpayer.Cuit);

    /// <summary>A person's first and last names: from the profile, or from the name with the last word as the surname.</summary>
    public static (string First, string Last) NamesOf(Taxpayer taxpayer)
    {
        if (taxpayer.Profile.LastName is { } last) return ((taxpayer.Profile.FirstName ?? "").ToUpperInvariant(), last.ToUpperInvariant());
        var words = taxpayer.Name.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length < 2 ? ("", words.FirstOrDefault() ?? "") : (string.Join(' ', words[..^1]), words[^1]);
    }

    public static TaxAddress AddressOf(Taxpayer taxpayer) => taxpayer.Profile.Address ?? TaxAddress.Default;

    public static (long Id, string Description) ActivityOf(Taxpayer taxpayer) =>
        (taxpayer.Profile.ActivityId ?? TaxpayerProfile.DefaultActivityId,
         taxpayer.Profile.ActivityDescription ?? TaxpayerProfile.DefaultActivityDescription);

    /// <summary>yyyyMM of the registration, as the padrón writes periods.</summary>
    public static int PeriodOf(Taxpayer taxpayer) =>
        taxpayer.Profile.RegisteredOn is { } day ? day.Year * 100 + day.Month : 201501;
}
