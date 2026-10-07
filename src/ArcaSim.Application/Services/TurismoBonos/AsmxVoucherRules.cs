using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Events;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Application.Services.TurismoBonos;

/// <summary>The one event an ASMX answer of these services carries in {P}Events.</summary>
public sealed record AsmxEvent(int Code, string Message);

/// <summary>A business refusal: the one code and text {P}Err carries.</summary>
public sealed record AsmxRefusal(int Code, string Message);

/// <summary>
/// What every authorization request of wsbfev1, wsbfe and wsseg carries in
/// Cmp, read leniently the way .NET deserializes it: a missing number is 0.
/// </summary>
public sealed record AsmxCmp(
    long Id,
    int DocType,
    long DocNumber,
    int VoucherType,
    int PointOfSale,
    long Number,
    decimal Total,
    decimal Exempt,
    string? Currency,
    decimal? Rate,
    string? DateText,
    string? ReceiverConditionText,
    string? SameCurrency,
    XElement Raw)
{
    public static AsmxCmp Read(XElement request)
    {
        var cmp = request.Child("Cmp") ?? new XElement("Cmp");
        return new AsmxCmp(
            cmp.Whole("Id") ?? 0,
            (int)(cmp.Whole("Tipo_doc") ?? 0),
            cmp.Whole("Nro_doc") ?? 0,
            (int)(cmp.Whole("Tipo_cbte") ?? 0),
            (int)(cmp.Whole("Punto_vta") ?? 0),
            cmp.Whole("Cbte_nro") ?? 0,
            cmp.Amount("Imp_total") ?? 0,
            cmp.Amount("Imp_op_ex") ?? 0,
            cmp.Field("Imp_moneda_Id"),
            cmp.Amount("Imp_moneda_ctz"),
            cmp.Field("Fecha_cbte"),
            cmp.Field("CondicionIVAReceptorId"),
            cmp.Child("CanMisMonExt")?.Value,
            cmp);
    }
}

/// <summary>
/// The texts of the 1014 rows of the wsbfe, wsbfev1 and wsseg manuals: the
/// condition each row checks, which is what ArcaSim answers. The real ErrMsg
/// of each one is not published (wsbfev1.md, Validaciones y errores).
/// </summary>
internal static class Text1014
{
    public const string Id = "Identificador del requerimiento sea mayor que 0.";
    public const string PointOfSale = "Campo punto_vta se encuentre entre 1 y 99998 y que sea único para el requerimiento.";
    public const string VoucherType = "Tipo de comprobante inválido.";
    public const string Number = "Campo cbte_nro esté entre 1 y 99999999.";
    public const string DocType = "El tipo de documento debe ser igual a 80 (CUIT) en comprobantes tipo A.";
    public const string DateFormat = "No es una fecha valida. Debe ser numérico de 8 con formato (yyyymmdd).";
    public const string DateMonth = "No podrá exceder el mes de la fecha de envío del pedido de autorización.";
    public const string DateWindow = "La fecha debe estar incluida en el periodo +- 5 días de la fecha de presentación.";
    public const string Items = "Se valida que la suma de importes de los ítems sea menor igual a los importes totales del comprobante.";
    public const string Exempt = "Se valida que el importe de operaciones exentas sea mayor a 0 en los casos donde exista alguna item de factura con Iva exento";

    /// <summary>"Valor inválido en campo", with the field and cause the manual promises; the detail is ArcaSim's.</summary>
    public static string InvalidValue(string field, string cause) => $"Valor inválido en campo {field}: {cause}";

    /// <summary>ArcaSim's text for a number that is not the next one: no manual has a code for it, so it goes out as 1014.</summary>
    public static string NotNext(long last) =>
        $"Valor inválido en campo Cbte_nro: el número no es el próximo a autorizar (último autorizado: {last}).";
}

/// <summary>
/// The authorization flow wsbfev1, wsbfe and wsseg share (one voucher per
/// call, a requirement Id of its own, one error and one event per answer),
/// following wsbfev1.md and wsseg.md, Comportamiento a simular:
/// - {P}Authorize checks the voucher, numbers it "último + 1" per CUIT, point
///   of sale and type, and gives it a CAE that expires
///   SimulationSettings.CaeLifetimeDays after its date (Fch_cbte + 10 in every
///   real wsbfev1 answer). An Id already authorized for the CUIT answers the
///   stored result again with Reproceso S, whatever the rest of the request
///   says; a refused request leaves no trace and its Id can be used again.
///   A refusal carries {P}ResultAuth with Id and Cuit 0, as wsbfev1 does.
/// - {P}GetCMP answers what was authorized, or 1020 "Comprobante inexistente"
///   without {P}ResultGet.
/// - {P}GetLast_CMP answers the last number and its date, 0 and no date when
///   nothing was issued; {P}GetLast_ID the highest Id authorized, 0 when none.
/// The numbering rule is ArcaSim's: no manual has a code for a number that is
/// not the next one, so it is refused with 1014 and a text of ArcaSim's.
/// </summary>
public abstract class AsmxVoucherRules(
    IDocumentStore documents,
    IAuthorizationCodes codes,
    SequenceLocks locks,
    IClock clock,
    SimulationSettings settings,
    EventManager events) : IServiceBehavior
{
    public abstract string Service { get; }

    /// <summary>BFE or SEG: how the operations, blocks and result elements are named.</summary>
    protected abstract string Prefix { get; }

    /// <summary>The collections' name: services that share it share numbering, Ids and vouchers.</summary>
    protected abstract string Family { get; }

    protected IClock Clock => clock;

    protected SimulationSettings Settings => settings;

    protected VoucherBook Book => _book ??= new VoucherBook(documents, Family);

    private VoucherBook? _book;

    /// <summary>The first rule the voucher breaks, or null when it can be authorized.</summary>
    protected abstract Task<AsmxRefusal?> CheckAsync(ServiceCall call, AsmxCmp cmp, DateOnly today, CancellationToken ct);

    /// <summary>The observations an authorized voucher carries in Obs; none by default.</summary>
    protected virtual List<BookNote> Observe(AsmxCmp cmp) => [];

    /// <summary>{P}ResultGet's children for a stored voucher, in the schema's order.</summary>
    protected abstract IEnumerable<XElement> Detail(XNamespace ns, BookedVoucher voucher);

    /// <summary>The rest of the service's operations; null keeps the contract's answer.</summary>
    protected abstract Task<ContractAnswer?> OtherAsync(ServiceCall call, CancellationToken ct);

    /// <summary>The event every answer carries. With none, 0 and "Ok" (an empty text in the authorization and its query, as wsbfev1 answers).</summary>
    protected virtual AsmxEvent Event(ServiceCall call) =>
        new(0, call.Name.EndsWith("Authorize", StringComparison.Ordinal) || call.Name.EndsWith("GetCMP", StringComparison.Ordinal) ? "" : "Ok");

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        var operation = call.Name.StartsWith(Prefix, StringComparison.Ordinal) ? call.Name[Prefix.Length..] : call.Name;
        return operation switch
        {
            "Authorize" => await AuthorizeAsync(call, ct),
            "GetCMP" => await ConsultAsync(call, ct),
            "GetLast_CMP" => await LastAsync(call, ct),
            "GetLast_ID" => Answer(call, new XElement(Ns(call) + $"{Prefix}ResultGet",
                new XElement(Ns(call) + "Id", await Book.LastRequestIdAsync(call.Cuit, ct)))),
            _ => await OtherAsync(call, ct),
        };
    }

    protected static XNamespace Ns(ServiceCall call) => call.Operation.Output.Namespace;

    /// <summary>{Op}Result with the result, {P}Err (0 and OK when nothing failed) and {P}Events.</summary>
    protected ContractAnswer Answer(ServiceCall call, XElement? result, AsmxRefusal? refusal = null)
    {
        var ns = Ns(call);
        var evt = Event(call);
        return call.Ok(new XElement(call.Operation.Output,
            new XElement(ns + $"{call.Name}Result",
                result,
                new XElement(ns + $"{Prefix}Err",
                    new XElement(ns + "ErrCode", refusal?.Code ?? 0),
                    new XElement(ns + "ErrMsg", refusal?.Message ?? "OK")),
                new XElement(ns + $"{Prefix}Events",
                    new XElement(ns + "EventCode", evt.Code),
                    new XElement(ns + "EventMsg", evt.Message)))));
    }

    protected ContractAnswer Refuse(ServiceCall call, AsmxRefusal refusal) => Answer(call, null, refusal);

    /// <summary>A parameter table: one row element per row, each with its own fields.</summary>
    protected ContractAnswer Table(ServiceCall call, IEnumerable<XElement> rows) =>
        Answer(call, new XElement(Ns(call) + $"{Prefix}ResultGet", rows));

    /// <summary>A row with an id, a description and its validity, named the way each table names them (Cbte_Id, Cbte_Ds...).</summary>
    protected static XElement Row(XNamespace ns, string row, string field, object id, string description, string from, string to = "NULL") =>
        new(ns + row,
            new XElement(ns + $"{field}_Id", id),
            new XElement(ns + $"{field}_Ds", description),
            new XElement(ns + $"{field}_vig_desde", from),
            new XElement(ns + $"{field}_vig_hasta", to));

    private async Task<ContractAnswer> AuthorizeAsync(ServiceCall call, CancellationToken ct)
    {
        var cmp = AsmxCmp.Read(call.Request);
        var ns = Ns(call);
        if (cmp.Id > 0 && await Book.FindByRequestAsync(call.Cuit, cmp.Id, ct) is { } again)
            return Answer(call, Authorized(ns, again, reprocessed: true));

        var today = clock.Today();
        if (await CheckAsync(call, cmp, today, ct) is { } refusal) return Rejected(call, cmp, refusal);

        using (await locks.AcquireAsync(Service, call.Cuit, cmp.PointOfSale, cmp.VoucherType, ct))
        {
            if (await Book.FindByRequestAsync(call.Cuit, cmp.Id, ct) is { } raced)
                return Answer(call, Authorized(ns, raced, reprocessed: true));
            var last = await Book.LastAsync(call.Cuit, cmp.PointOfSale, cmp.VoucherType, ct);
            if (cmp.Number != (last?.Number ?? 0) + 1)
                return Rejected(call, cmp, new AsmxRefusal(1014, Text1014.NotNext(last?.Number ?? 0)));

            var date = Figures.ParseDay(cmp.DateText) ?? today;
            var voucher = new BookedVoucher(Service, call.Cuit, cmp.PointOfSale, cmp.VoucherType, cmp.Number, cmp.Id, date,
                string.IsNullOrEmpty(cmp.DateText) ? null : cmp.DateText, codes.NextCae(), date.AddDays(settings.CaeLifetimeDays), "A", Observe(cmp),
                Figures.Strip(cmp.Raw).ToString(SaveOptions.DisableFormatting), clock.Now);
            await Book.AddAsync(voucher, new AuthorizedVoucher(Service, call.Cuit, cmp.PointOfSale, cmp.VoucherType, cmp.Number, date,
                cmp.Total, cmp.DocType, cmp.DocNumber, "CAE", voucher.Cae, voucher.CaeDue), ct);
            events.Publish(new VoucherAuthorized(DateTimeOffset.UtcNow, call.Cuit, cmp.PointOfSale, cmp.VoucherType, cmp.Number, cmp.Number, "CAE", voucher.Cae));
            return Answer(call, Authorized(ns, voucher, reprocessed: false));
        }
    }

    private ContractAnswer Rejected(ServiceCall call, AsmxCmp cmp, AsmxRefusal refusal)
    {
        events.Publish(new VoucherRejected(DateTimeOffset.UtcNow, call.Cuit, cmp.PointOfSale, cmp.VoucherType, cmp.Number, [refusal.Code]));
        var ns = Ns(call);
        return Answer(call, new XElement(ns + $"{Prefix}ResultAuth", new XElement(ns + "Id", 0), new XElement(ns + "Cuit", 0)), refusal);
    }

    private XElement Authorized(XNamespace ns, BookedVoucher voucher, bool reprocessed) =>
        new(ns + $"{Prefix}ResultAuth",
            new XElement(ns + "Id", voucher.RequestId),
            new XElement(ns + "Cuit", voucher.Cuit),
            new XElement(ns + "Cae", voucher.Cae),
            new XElement(ns + "Fch_venc_Cae", Figures.Day(voucher.CaeDue)),
            new XElement(ns + "Fch_cbte", Figures.Day(voucher.Date)),
            new XElement(ns + "Resultado", voucher.Result),
            new XElement(ns + "Reproceso", reprocessed ? "S" : "N"),
            new XElement(ns + "Obs", Observations(voucher)));

    /// <summary>The observation codes, or one space when there are none, as wsbfev1 answers.</summary>
    protected static string Observations(BookedVoucher voucher) =>
        voucher.Notes.Count == 0 ? " " : string.Join(",", voucher.Notes.Select(n => n.Code.ToString(CultureInfo.InvariantCulture)));

    private async Task<ContractAnswer> ConsultAsync(ServiceCall call, CancellationToken ct)
    {
        var cmp = call.Request.Child("Cmp") ?? new XElement("Cmp");
        var found = await Book.FindAsync(call.Cuit, (int)(cmp.Whole("Punto_vta") ?? 0), (int)(cmp.Whole("Tipo_cbte") ?? 0), cmp.Whole("Cbte_nro") ?? 0, ct);
        if (found is null) return Refuse(call, new AsmxRefusal(1020, "Comprobante inexistente"));
        return Answer(call, new XElement(Ns(call) + $"{Prefix}ResultGet", Detail(Ns(call), found)));
    }

    private async Task<ContractAnswer> LastAsync(ServiceCall call, CancellationToken ct)
    {
        var auth = call.Request.Child("Auth") ?? new XElement("Auth");
        var last = await Book.LastAsync(call.Cuit, (int)(auth.Whole("Pto_venta") ?? 0), (int)(auth.Whole("Tipo_cbte") ?? 0), ct);
        var ns = Ns(call);
        return Answer(call, new XElement(ns + $"{Prefix}Result_LastCMP",
            new XElement(ns + "Cbte_nro", last?.Number ?? 0),
            last is null ? null : new XElement(ns + "Cbte_fecha", Figures.Day(last.Date))));
    }

    /// <summary>
    /// The common head of {P}ResultGet: the requirement, the voucher and its
    /// amounts as they were sent. The dates and the rest follow per service.
    /// </summary>
    protected static IEnumerable<XElement> Head(XNamespace ns, BookedVoucher voucher, XElement detail) =>
    [
        new XElement(ns + "Id", voucher.RequestId),
        new XElement(ns + "Cuit", voucher.Cuit),
        .. Figures.Ordered(ns, detail, ["Tipo_doc!", "Nro_doc!"]),
        new XElement(ns + "Tipo_cbte", voucher.VoucherType),
        new XElement(ns + "Punto_vta", voucher.PointOfSale),
        new XElement(ns + "Cbte_nro", voucher.Number),
        .. Figures.Ordered(ns, detail, ["Imp_total!", "Imp_tot_conc!", "Imp_neto!", "Impto_liq!", "Impto_liq_rni!", "Imp_op_ex!",
            "Imp_perc!", "Imp_iibb!", "Imp_perc_mun!", "Imp_internos!", "Imp_moneda_Id", "Imp_moneda_ctz!"]),
    ];

    // ---- Checks the two families share ------------------------------------------------------

    /// <summary>
    /// Fecha_cbte: yyyymmdd, within 5 days of today and not in a month after
    /// the current one (1014). "No podrá exceder el mes" is read as no later
    /// month: a date a few days back, in the previous month, is accepted.
    /// </summary>
    protected static AsmxRefusal? CheckDate(string? text, DateOnly today, int daysBack = 5, int daysAhead = 5)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length != 8 || Figures.ParseDay(text) is not { } date) return new AsmxRefusal(1014, Text1014.DateFormat);
        if (date > today && (date.Month != today.Month || date.Year != today.Year)) return new AsmxRefusal(1014, Text1014.DateMonth);
        if (date < today.AddDays(-daysBack) || date > today.AddDays(daysAhead)) return new AsmxRefusal(1014, Text1014.DateWindow);
        return null;
    }

    /// <summary>The items' total against the voucher's (1014), within ARCA's usual margin.</summary>
    protected static AsmxRefusal? CheckItemsTotal(AsmxCmp cmp, IReadOnlyList<XElement> items)
    {
        var sum = items.Sum(i => i.Amount("Imp_total") ?? 0);
        return sum > cmp.Total && !Figures.Close(cmp.Total, sum, items.Count) ? new AsmxRefusal(1014, Text1014.Items) : null;
    }

    protected static IReadOnlyList<XElement> Items(AsmxCmp cmp) =>
        cmp.Raw.Child("Items")?.Children("Item").ToList() ?? [];
}
