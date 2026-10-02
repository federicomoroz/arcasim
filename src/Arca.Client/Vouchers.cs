namespace Arca.Client;

/// <summary>
/// One voucher to authorize, in ARCA's terms (docs/arca/wsfev1.md §3.3).
/// Amounts are decimals; the client writes them with a dot, as WSFEv1 expects.
/// </summary>
public sealed record Voucher
{
    /// <summary>1 products, 2 services, 3 products and services.</summary>
    public required int Concept { get; init; }

    /// <summary>80 CUIT, 86 CUIL, 96 DNI, 99 final consumer without identification...</summary>
    public required int DocumentType { get; init; }

    public required long DocumentNumber { get; init; }

    /// <summary>Set by AuthorizeNextAsync; only needed with AuthorizeAsync.</summary>
    public long Number { get; init; }

    /// <summary>Null lets ARCA use the day of the request.</summary>
    public DateOnly? Date { get; init; }

    public required decimal Total { get; init; }
    public decimal NotTaxed { get; init; }
    public decimal Net { get; init; }
    public decimal Exempt { get; init; }
    public decimal OtherTaxes { get; init; }
    public decimal Vat { get; init; }

    /// <summary>Required for concepts 2 and 3.</summary>
    public DateOnly? ServiceFrom { get; init; }
    public DateOnly? ServiceTo { get; init; }
    public DateOnly? PaymentDue { get; init; }

    public string Currency { get; init; } = "PES";
    public decimal ExchangeRate { get; init; } = 1;

    /// <summary>CanMisMonExt: whether a foreign currency voucher is paid in that same currency. Null leaves it out.</summary>
    public bool? PaidInSameCurrency { get; init; }

    /// <summary>The receiver's VAT condition (FEParamGetCondicionIvaReceptor). Mandatory from 01/12/2026.</summary>
    public int? ReceiverVatCondition { get; init; }

    public IReadOnlyList<VatLine> VatLines { get; init; } = [];
    public IReadOnlyList<OtherTaxLine> OtherTaxLines { get; init; } = [];
    public IReadOnlyList<AssociatedVoucher> Associated { get; init; } = [];
}

/// <summary>One VAT rate: 3 is 0 %, 4 is 10.5 %, 5 is 21 %, 6 is 27 %, 8 is 5 %, 9 is 2.5 %.</summary>
public sealed record VatLine(int Id, decimal Base, decimal Amount);

public sealed record OtherTaxLine(short Id, string? Description, decimal Base, decimal Rate, decimal Amount);

/// <summary>The voucher a credit or debit note refers to.</summary>
public sealed record AssociatedVoucher(int Type, int PointOfSale, long Number, long? Cuit = null, DateOnly? Date = null);

/// <summary>
/// What ARCA decided. A rejection is an answer, not a failure: fix the voucher
/// and send it again. Failures that a retry can solve are thrown as
/// ArcaUnavailableException instead.
/// </summary>
public sealed record AuthorizationResult(
    bool Approved,
    long Number,
    DateOnly Date,
    string? Cae,
    DateOnly? CaeDue,
    IReadOnlyList<ArcaMessage> Observations,
    IReadOnlyList<ArcaMessage> Errors)
{
    /// <summary>True when the CAE came from FECompConsultar because the answer to FECAESolicitar was lost.</summary>
    public bool Recovered { get; init; }
}

/// <summary>A voucher as FECompConsultar returns it.</summary>
public sealed record AuthorizedVoucher(
    int PointOfSale,
    int VoucherType,
    long Number,
    DateOnly Date,
    int DocumentType,
    long DocumentNumber,
    decimal Total,
    string AuthorizationCode,
    string EmissionType,
    DateOnly Due,
    IReadOnlyList<ArcaMessage> Observations);

/// <summary>FEDummy: whether ARCA's application, database and authentication servers are up.</summary>
public sealed record ServerStatus(string AppServer, string DbServer, string AuthServer)
{
    public bool AllOk => AppServer == "OK" && DbServer == "OK" && AuthServer == "OK";
}
