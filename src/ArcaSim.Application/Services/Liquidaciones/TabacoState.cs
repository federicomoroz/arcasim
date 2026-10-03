using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// A CATHE (Código de Autorización de Tabaco en Hebras) as wstabaco keeps it:
/// the depositor that asked for it, its deposit and import dispatch, and where
/// it is in its life (docs/arca/servicios/wstabaco.md, "Ciclo de vida"):
/// solicitado, vinculado (with holder, kilos and the kilos still available),
/// or baja (used up, elaborated or denatured). A pending denaturation or change
/// of holder locks it out of every other operation.
/// </summary>
public sealed record Cathe(
    long Code,
    long Owner,
    long Deposit,
    string? Dispatch,
    DateOnly RequestedOn,
    string State,
    long? Holder = null,
    decimal GrossKilos = 0,
    decimal NetKilos = 0,
    decimal Available = 0,
    int? Goods = null,
    DateOnly? LinkedOn = null,
    string? Lock = null,
    string? Retirement = null)
{
    public const string Requested = "solicitado";
    public const string Linked = "vinculado";
    public const string Retired = "baja";

    public bool InStock => State == Linked && Lock is null;
}

/// <summary>A CATA declared as initial stock, with the kilos still available.</summary>
public sealed record Cata(long Code, long Owner, long Deposit, decimal Kilos, decimal Available);

/// <summary>A deposit's productive parameters: average daily kilos and packing units.</summary>
public sealed record ProductionParameters(long Deposit, decimal Kilos, long Quantity);

/// <summary>One CATHE of an elaboration report and the product it went into.</summary>
public sealed record ProductLine(long Cathe, int Product, string? OtherProduct);

/// <summary>A deposit's elaboration report for one day, at its latest rectification.</summary>
public sealed record ElaborationReport(long Deposit, DateOnly Date, int Rectification, DateOnly InformedOn, List<ProductLine> Lines);

/// <summary>A denaturation request: P until SEFI resolves it A, B or C; each CATHE P, S or N.</summary>
public sealed record Denaturation(
    long Id,
    long Owner,
    long Deposit,
    DateOnly Date,
    string Reason,
    Dictionary<string, string> Cathes,
    string Result,
    DateOnly? ResolvedOn);

/// <summary>A change of holder without moving the tobacco, PC or PV until both sides agree (AP) or one refuses (RC, RV).</summary>
public sealed record TitleChange(
    long Id,
    long Informant,
    long Seller,
    long Buyer,
    DateOnly Date,
    string InvoiceMode,
    string VoucherType,
    long PointOfSale,
    long Number,
    decimal NetAmount,
    decimal Total,
    List<long> Cathes,
    string State);

/// <summary>wstabaco's documents, in its own collection.</summary>
public sealed class TabacoBook(IDocumentStore store)
{
    private const string Collection = "wstabaco";

    public Task<Cathe?> CatheAsync(long owner, long code, CancellationToken ct) => store.GetAsync<Cathe>(Collection, $"cathe/{owner}/{code}", ct);

    public Task PutAsync(Cathe cathe, CancellationToken ct) => store.PutAsync(Collection, $"cathe/{cathe.Owner}/{cathe.Code}", cathe, ct);

    public Task<IReadOnlyList<Cathe>> CathesAsync(long owner, CancellationToken ct) => store.ListAsync<Cathe>(Collection, $"cathe/{owner}/", ct);

    /// <summary>Fourteen digits, the year and a sequence: the manual's CATHE start with the year, the rest is ArcaSim's.</summary>
    public async Task<long> NewCatheAsync(int year, CancellationToken ct) => year * 10_000_000_000L + await store.NextAsync("wstabaco-cathe", ct);

    public Task<Cata?> CataAsync(long owner, long code, CancellationToken ct) => store.GetAsync<Cata>(Collection, $"cata/{owner}/{code}", ct);

    public Task PutAsync(Cata cata, CancellationToken ct) => store.PutAsync(Collection, $"cata/{cata.Owner}/{cata.Code}", cata, ct);

    public Task<IReadOnlyList<Cata>> CatasAsync(long owner, CancellationToken ct) => store.ListAsync<Cata>(Collection, $"cata/{owner}/", ct);

    public Task<ProductionParameters?> ParametersAsync(long owner, long deposit, CancellationToken ct) =>
        store.GetAsync<ProductionParameters>(Collection, $"param/{owner}/{deposit}", ct);

    public Task PutAsync(long owner, ProductionParameters parameters, CancellationToken ct) =>
        store.PutAsync(Collection, $"param/{owner}/{parameters.Deposit}", parameters, ct);

    public Task<IReadOnlyList<ProductionParameters>> AllParametersAsync(long owner, CancellationToken ct) =>
        store.ListAsync<ProductionParameters>(Collection, $"param/{owner}/", ct);

    public Task<ElaborationReport?> ReportAsync(long owner, long deposit, DateOnly date, CancellationToken ct) =>
        store.GetAsync<ElaborationReport>(Collection, $"elab/{owner}/{deposit}/{date:yyyyMMdd}", ct);

    public Task PutAsync(long owner, ElaborationReport report, CancellationToken ct) =>
        store.PutAsync(Collection, $"elab/{owner}/{report.Deposit}/{report.Date:yyyyMMdd}", report, ct);

    /// <summary>Denaturations and changes of holder share one numbering: whether ARCA's do is not documented.</summary>
    public Task<long> NewRequestIdAsync(CancellationToken ct) => store.NextAsync("wstabaco-solicitud", ct);

    public Task<Denaturation?> DenaturationAsync(long id, CancellationToken ct) => store.GetAsync<Denaturation>(Collection, $"desnat/{id:D12}", ct);

    public Task PutAsync(Denaturation request, CancellationToken ct) => store.PutAsync(Collection, $"desnat/{request.Id:D12}", request, ct);

    public Task<IReadOnlyList<Denaturation>> DenaturationsAsync(CancellationToken ct) => store.ListAsync<Denaturation>(Collection, "desnat/", ct);

    public Task<TitleChange?> TitleChangeAsync(long id, CancellationToken ct) => store.GetAsync<TitleChange>(Collection, $"cambio/{id:D12}", ct);

    public Task PutAsync(TitleChange request, CancellationToken ct) => store.PutAsync(Collection, $"cambio/{request.Id:D12}", request, ct);

    public Task<IReadOnlyList<TitleChange>> TitleChangesAsync(CancellationToken ct) => store.ListAsync<TitleChange>(Collection, "cambio/", ct);
}
