using System.Text.Json;

namespace ArcaSim.Application.Wsfe;

/// <summary>Letter of a voucher type, which decides most of the rules (code 10007).</summary>
public enum VoucherClass
{
    A,
    B,
    C,

    /// <summary>"A con leyenda operación sujeta a retención", 51 to 54 (the former class M).</summary>
    ALey,

    /// <summary>49, purchase of used goods from a final consumer.</summary>
    UsedGoods,
}

public enum VoucherKind
{
    Invoice,
    DebitNote,
    CreditNote,
    Receipt,
    Other,
}

public sealed record VoucherTypeInfo(int Id, string Desc, string From, string To, VoucherClass Class, VoucherKind Kind, bool Fce)
{
    /// <summary>Notes whose VAT detail may come without an id or a base (codes 10019 to 10021, 10051, 10061).</summary>
    public bool IsLooseVatNote => Id is 2 or 3 or 7 or 8 or 52 or 53;

    public bool IsNote => Kind is VoucherKind.DebitNote or VoucherKind.CreditNote;
}

public sealed record DatedRow(string Id, string Desc, string From, string To);

public sealed record VatRate(string Id, string Desc, string From, string To, decimal Rate);

public sealed record ReceiverVatConditionRow(int Id, string Desc, IReadOnlyList<string> Classes);

public sealed record Country(int Id, string Desc);

/// <summary>
/// The tables FEParamGet* answer with (Data/parametros.json, made by
/// tools/extract_parameters.py from what homologación returned) and the
/// lookups the validations need.
/// </summary>
public sealed class ParameterTables
{
    public required IReadOnlyList<VoucherTypeInfo> VoucherTypes { get; init; }
    public required IReadOnlyList<DatedRow> DocumentTypes { get; init; }
    public required IReadOnlyList<DatedRow> Concepts { get; init; }
    public required IReadOnlyList<VatRate> VatRates { get; init; }
    public required IReadOnlyList<DatedRow> Currencies { get; init; }
    public required IReadOnlyList<DatedRow> Taxes { get; init; }
    public required IReadOnlyList<DatedRow> Optionals { get; init; }
    public required IReadOnlyList<ReceiverVatConditionRow> ReceiverVatConditions { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }

    public static ParameterTables Load()
    {
        using var stream = EmbeddedData.Open("parametros.json");
        var file = JsonSerializer.Deserialize<TablesFile>(stream, EmbeddedData.Json)
            ?? throw new InvalidOperationException("parametros.json is empty.");
        return new ParameterTables
        {
            VoucherTypes = file.VoucherTypes.Select(v => new VoucherTypeInfo(
                v.Id, v.Desc, v.From, v.To, ParseClass(v.Class), Enum.Parse<VoucherKind>(v.Kind), v.Fce)).ToList(),
            DocumentTypes = file.DocumentTypes.Select(Dated).ToList(),
            Concepts = file.Concepts.Select(Dated).ToList(),
            VatRates = file.VatRates.Select(r => new VatRate(r.Id.ToString(), r.Desc, r.From, r.To, r.Rate)).ToList(),
            Currencies = file.Currencies.Select(Dated).ToList(),
            Taxes = file.Taxes.Select(Dated).ToList(),
            Optionals = file.Optionals.Select(Dated).ToList(),
            ReceiverVatConditions = file.ReceiverVatConditions.Select(c => new ReceiverVatConditionRow(c.Id, c.Desc, c.Classes)).ToList(),
            Countries = file.Countries.Select(c => new Country(c.Id, c.Desc)).ToList(),
        };
    }

    public VoucherTypeInfo? VoucherType(int id) => VoucherTypes.FirstOrDefault(v => v.Id == id);

    public bool HasDocumentType(int id) => DocumentTypes.Any(d => d.Id == id.ToString());

    public bool HasConcept(int id) => Concepts.Any(c => c.Id == id.ToString());

    public VatRate? Vat(int id) => VatRates.FirstOrDefault(v => v.Id == id.ToString());

    public bool HasCurrency(string id) => Currencies.Any(c => c.Id == id);

    public bool HasTax(int id) => Taxes.Any(t => t.Id == id.ToString());

    public ReceiverVatConditionRow? ReceiverVatCondition(int id) => ReceiverVatConditions.FirstOrDefault(c => c.Id == id);

    /// <summary>Whether a receiver condition may go on a voucher of this class (the manual's annex, wsfev1.md §7.8).</summary>
    public static bool Allows(ReceiverVatConditionRow condition, VoucherClass voucherClass) =>
        condition.Classes.Contains(voucherClass switch
        {
            VoucherClass.A or VoucherClass.ALey => "A",
            VoucherClass.B => "B",
            VoucherClass.C => "C",
            _ => "49",
        });

    private static VoucherClass ParseClass(string value) => value switch
    {
        "A" => VoucherClass.A,
        "B" => VoucherClass.B,
        "C" => VoucherClass.C,
        "ALEY" => VoucherClass.ALey,
        "49" => VoucherClass.UsedGoods,
        _ => throw new InvalidOperationException($"Unknown voucher class {value}."),
    };

    private static DatedRow Dated(RawRow row) => new(row.Id.ToString(), row.Desc, row.From, row.To);

    private sealed record TablesFile(
        List<RawVoucherType> VoucherTypes,
        List<RawRow> DocumentTypes,
        List<RawRow> Concepts,
        List<RawVat> VatRates,
        List<RawRow> Currencies,
        List<RawRow> Taxes,
        List<RawRow> Optionals,
        List<RawCondition> ReceiverVatConditions,
        List<RawCountry> Countries);

    private sealed record RawVoucherType(int Id, string Desc, string From, string To, string Class, string Kind, bool Fce);

    private sealed record RawRow(JsonElement Id, string Desc, string From, string To);

    private sealed record RawVat(JsonElement Id, string Desc, string From, string To, decimal Rate);

    private sealed record RawCondition(int Id, string Desc, List<string> Classes);

    private sealed record RawCountry(int Id, string Desc);
}
