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
}

public static class Cuits
{
    private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

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
