namespace ArcaSim.Domain;

/// <summary>Condición frente al IVA, with the ids of FEParamGetCondicionIvaReceptor.</summary>
public enum VatCondition
{
    ResponsableInscripto = 1,
    Exento = 4,
    ConsumidorFinal = 5,
    Monotributo = 6,
    NoCategorizado = 7,
    ProveedorDelExterior = 8,
    ClienteDelExterior = 9,
    Liberado = 10,
    MonotributistaSocial = 13,
    NoAlcanzado = 15,
    MonotributoTrabajadorIndependientePromovido = 16,
}

/// <summary>How a point of sale was registered with ARCA. Only web service points of sale can ask for a CAE.</summary>
public enum PointOfSaleKind
{
    /// <summary>"RECE" in ARCA's terms: CAE through web services.</summary>
    WebServiceCae,

    /// <summary>CAEA through web services, for contingencies.</summary>
    WebServiceCaea,

    /// <summary>Anything that is not a web service point of sale (Comprobantes en línea, controlador fiscal...).</summary>
    Other,
}

public sealed record PointOfSale(int Number, PointOfSaleKind Kind, bool Blocked = false, DateOnly? DeactivatedOn = null);

public static class VatConditions
{
    /// <summary>The three monotributo regimes, which the manuals' rules treat as one: the general, the social and the promoted independent worker.</summary>
    public static bool IsMonotributo(this VatCondition condition) =>
        condition is VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido;
}

/// <summary>
/// The ranges the invoicing manuals give a point of sale (1 to 99998) and a
/// voucher number (1 to 99999999). A rule whose manual words its range another
/// way (below 99998, nine digits, zero allowed) writes its own bounds next to its text.
/// </summary>
public static class VoucherLimits
{
    public const int MaxPointOfSale = 99_998;

    public const int MaxNumber = 99_999_999;
}

/// <summary>
/// A made-up taxpayer. WSFEv1, and later the padrón services, read the same
/// records, so looking up a CUIT and invoicing it never disagree.
/// </summary>
public sealed class Taxpayer
{
    private readonly List<PointOfSale> _pointsOfSale = [];

    public long Cuit { get; private set; }
    public string Name { get; private set; } = "";
    public VatCondition VatCondition { get; private set; }
    public bool Active { get; private set; } = true;
    public IReadOnlyList<PointOfSale> PointsOfSale => _pointsOfSale;

    /// <summary>What the padrón says about the taxpayer beyond its VAT condition. Defaults come from the CUIT and the name.</summary>
    public TaxpayerProfile Profile { get; private set; } = TaxpayerProfile.Empty;

    public PersonKind Kind => Cuits.KindOf(Cuit);

    private Taxpayer() { }

    public Taxpayer(long cuit, string name, VatCondition vatCondition, IEnumerable<PointOfSale>? pointsOfSale = null)
    {
        if (!Cuits.IsValid(cuit)) throw new ArgumentException($"CUIT {cuit} has a wrong check digit.", nameof(cuit));
        Cuit = cuit;
        Name = name;
        VatCondition = vatCondition;
        if (pointsOfSale is not null) _pointsOfSale.AddRange(pointsOfSale);
    }

    public PointOfSale? FindPointOfSale(int number) => _pointsOfSale.FirstOrDefault(p => p.Number == number);

    /// <summary>Whether the point of sale exists for that kind of voucher, is not blocked and is not yet deactivated on that day.</summary>
    public bool CanIssueFrom(int number, PointOfSaleKind kind, DateOnly today) =>
        FindPointOfSale(number) is { } point && point.Kind == kind && !point.Blocked && !(point.DeactivatedOn <= today);

    public void AddPointOfSale(PointOfSale pointOfSale)
    {
        _pointsOfSale.RemoveAll(p => p.Number == pointOfSale.Number);
        _pointsOfSale.Add(pointOfSale);
    }

    public void Update(string name, VatCondition vatCondition, bool active)
    {
        Name = name;
        VatCondition = vatCondition;
        Active = active;
    }

    public void SetProfile(TaxpayerProfile profile) => Profile = profile;
}

/// <summary>A person (DNI-based CUIT: 20, 23, 24, 27) or a company (30, 33, 34).</summary>
public enum PersonKind
{
    Fisica,
    Juridica,
}

public sealed record TaxAddress(string Street, string Locality, string PostalCode, int ProvinceId, string Province)
{
    /// <summary>A plainly made-up fiscal address for taxpayers nobody described.</summary>
    public static readonly TaxAddress Default = new("CALLE SIMULADA 1234", "CIUDAD AUTONOMA BUENOS AIRES", "1000", 0, "CIUDAD AUTONOMA BUENOS AIRES");
}

/// <summary>
/// The padrón's view of a taxpayer. Every field is optional: what is missing is
/// derived from the CUIT and the name, so a taxpayer created with only a CUIT
/// still answers a full constancia.
/// </summary>
public sealed record TaxpayerProfile(
    string? FirstName = null,
    string? LastName = null,
    TaxAddress? Address = null,
    long? ActivityId = null,
    string? ActivityDescription = null,
    string? LegalForm = null,
    DateOnly? RegisteredOn = null)
{
    public static readonly TaxpayerProfile Empty = new();

    public const long DefaultActivityId = 620100;
    public const string DefaultActivityDescription = "SERVICIOS DE CONSULTORES EN INFORMÁTICA Y SUMINISTROS DE PROGRAMAS DE INFORMÁTICA";
}

public static class Cuits
{
    private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static PersonKind KindOf(long cuit) => (cuit / 1_000_000_000) is 30 or 33 or 34 ? PersonKind.Juridica : PersonKind.Fisica;

    /// <summary>The DNI inside a person's CUIT: the eight digits between the prefix and the check digit.</summary>
    public static string DocumentOf(long cuit) => (cuit / 10 % 100_000_000).ToString();

    /// <summary>The CUIT a DNI gets with prefix 20, or 23 when 20 would need check digit 10, as ARCA assigns them.</summary>
    public static long ForDocument(long document)
    {
        foreach (var prefix in new[] { 20L, 27L, 23L })
        {
            var body = prefix * 100_000_000 + document;
            for (var check = 0; check <= 9; check++)
            {
                var cuit = body * 10 + check;
                if (IsValid(cuit) && IsCanonical(cuit)) return cuit;
            }
        }
        throw new ArgumentException($"No CUIT for document {document}.");
    }

    private static bool IsCanonical(long cuit)
    {
        var digits = cuit.ToString();
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (digits[i] - '0') * Weights[i];
        return 11 - sum % 11 != 10;
    }

    /// <summary>The mod 11 check digit every CUIT carries.</summary>
    public static bool IsValid(long cuit)
    {
        var digits = cuit.ToString();
        if (digits.Length != 11) return false;
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (digits[i] - '0') * Weights[i];
        var check = 11 - sum % 11;
        check = check switch { 11 => 0, 10 => 9, _ => check };
        return check == digits[10] - '0';
    }
}
