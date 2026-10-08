using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A query an organism made of a CUIT's export vouchers, with the vouchers it found.</summary>
public sealed record ExportQuery(long Code, long Organism, long Cuit, DateOnly Date, int From, int To, string State, List<AuthorizedVoucher> Vouchers);

/// <summary>
/// Economía del Conocimiento (wscec, docs/arca/servicios/wscec.md): an
/// organism asks for a CUIT's export vouchers by period or for the twelve
/// periods before its registration; each query is kept with its code and
/// read back page by page, and obtenerConsultas lists them with the manual's
/// filter rules. The vouchers are the ones wsfexv1 authorized (AuthorizedVouchers).
/// Codes and texts are the manual's: 2002 to 2005, 2008, 2009 (a format error),
/// 4009, 4010 and 4016. ArcaSim's choices: every CUIT the padrón knows counts
/// as characterized (no 4014, 4015), so every query ends at once in TE (no
/// PE, no 4019); a page holds 50 items; a query without vouchers is kept and
/// answered with 4010; what AuthorizedVoucher does not hold is filled plainly
/// (tipoExportacion 1, moneda PES at 1, cliente the receiver's document, one
/// item with the total).
/// </summary>
public sealed class CecRules(IDocumentStore store, PadronDirectory padron, IClock clock) : IServiceBehavior
{
    public const string Queries = "wscec.consultas";
    private const int PageSize = 50;

    public string Service => "wscec";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "consultarComprobantesExpoPeriodo" => await CreateAsync(call, byPeriod: true, ct),
        "consultarComprobantesExpoInscripcion" => await CreateAsync(call, byPeriod: false, ct),
        "consultarComprobantesExpoCodigoConsulta" => await ReadAsync(call, ct),
        "obtenerConsultas" => await ListAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> CreateAsync(ServiceCall call, bool byPeriod, CancellationToken ct)
    {
        var page = call.Request.Int("pagina");
        var cuit = call.Request.Long("cuit");
        var today = clock.Today();
        int from, to;
        if (byPeriod)
        {
            var period = call.Request.Int("periodo");
            var (year, month) = (period / 100, period % 100);
            if (year is < 2019 or > 9999) return FormatError(call, $"El periodo de consulta debe respetar el formato 'YYYYMM', con año (YYYY) y mes (MM) válidos. El año {year} no es válido.");
            if (month is < 1 or > 12) return FormatError(call, $"El periodo de consulta debe respetar el formato 'YYYYMM', con año (YYYY) y mes (MM) válidos. El mes {month} no es válido.");
            if (page < 1) return Errors(call, 2005, "Número de página inválido. El número de página no puede ser menor a 1.");
            if (await padron.FindAsync(cuit, ct) is null) return Errors(call, 4009, $"La CUIT no se encuentra en los Registros de AFIP. '{cuit}' no está en el padrón.");
            // December 9999 falls due in a year DateOnly does not have: it has not fallen due yet either.
            if ((year, month) == (9999, 12) || today < new DateOnly(year, month, 5).AddMonths(1).AddDays(1))
                return Errors(call, 4016, $"Periodo de consulta igual al actual. El periodo actual {period} todavía no venció. Vence el día 5 del mes que le sigue.");
            (from, to) = (period, period);
        }
        else
        {
            if (page < 1) return Errors(call, 2005, "Número de página inválido. El número de página no puede ser menor a 1.");
            if (await padron.FindAsync(cuit, ct) is not { } taxpayer)
                return Errors(call, 4009, $"La CUIT no se encuentra en los Registros de AFIP. '{cuit}' no está en el padrón.");
            var registered = PadronDirectory.PeriodOf(taxpayer);
            var first = new DateOnly(registered / 100, registered % 100, 1);
            (from, to) = (Period(first.AddMonths(-12)), Period(first.AddMonths(-1)));
        }

        var vouchers = (await store.ListAsync<AuthorizedVoucher>(AuthorizedVouchers.Collection, $"{cuit}/", ct))
            .Where(v => v.Service == "wsfexv1" && Period(v.Date) >= from && Period(v.Date) <= to)
            .OrderBy(v => v.Date).ThenBy(v => v.PointOfSale).ThenBy(v => v.Number)
            .ToList();
        var query = new ExportQuery(await store.NextAsync(Queries, ct), call.Cuit, cuit, today, from, to, "TE", vouchers);
        await store.PutAsync(Queries, Key(query.Code), query, ct);
        return Result(call, query, page);
    }

    private async Task<ContractAnswer> ReadAsync(ServiceCall call, CancellationToken ct)
    {
        var code = call.Request.Long("codigoConsulta");
        var page = call.Request.Int("pagina");
        if (code < 1) return Errors(call, 2008, $"El código de consulta debe ser mayor o igual a 1. El código {code} no es válido.");
        if (page < 1) return Errors(call, 2005, "Número de página inválido. El número de página no puede ser menor a 1.");
        var query = await store.GetAsync<ExportQuery>(Queries, Key(code), ct);
        if (query is null || query.Organism != call.Cuit) return Errors(call, 4010, $"No se encontraron datos. No existe consulta con código {code}.");
        return Result(call, query, page);
    }

    private async Task<ContractAnswer> ListAsync(ServiceCall call, CancellationToken ct)
    {
        var filter = call.Request.Find("filtro") ?? new XElement("filtro");
        var page = call.Request.Int("pagina");
        var cuit = filter.OptionalLong("cuit");
        var from = filter.Date("fechaDesde");
        var to = filter.Date("fechaHasta");
        if (from is null || to is null) (from, to) = (null, null);

        if (cuit is { } c && !Cuits.IsValid(c)) return Errors(call, 2002, $"Formato de CUIT inválido. Revisar '{c}': tiene un formato incorrecto.");
        if (cuit is null && from is null) return Errors(call, 2004, "Faltan campos. Si 'cuit' está vacío, se deben indicar 'fechaDesde' y 'fechaHasta'.");
        if (from is { } start && to is { } end)
        {
            if (end < start)
                return Errors(call, 2003, $"Rango de fechas inválido. 'fechaHasta' tiene que ser más antigua que 'fechaDesde': {end.Iso()} es una fecha previa a {start.Iso()}.");
            if (end.DayNumber - start.DayNumber > 31)
                return Errors(call, 2003, $"Rango de fechas inválido. La diferencia entre {end.Iso()} y {start.Iso()} son {end.DayNumber - start.DayNumber} días: el intervalo máximo de dias es 31.");
        }
        if (page < 1) return Errors(call, 2005, "Número de página inválido. El número de página no puede ser menor a 1.");

        var queries = (await store.ListAsync<ExportQuery>(Queries, "", ct))
            .Where(q => q.Organism == call.Cuit && (cuit is null || q.Cuit == cuit) && (from is null || (q.Date >= from && q.Date <= to)))
            .ToList();
        var (shown, more) = PageOf(queries, page);
        var answer = new XElement(call.Operation.Output, new XElement("obtenerConsultasReturn",
            shown.Count == 0 ? null : new XElement("consultas", shown.Select(q => Data("consulta", q))),
            new XElement("pagina", page),
            new XElement("hayMas", more ? "S" : "N")));
        return call.Ok(answer);
    }

    private static ContractAnswer Result(ServiceCall call, ExportQuery query, int page)
    {
        var (shown, more) = PageOf(query.Vouchers, page);
        var result = new XElement("consultarComprobantesExpoReturn",
            Data("datosConsulta", query),
            shown.Count == 0 ? null : new XElement("comprobantesExportacion", shown.Select(Voucher)),
            new XElement("pagina", page),
            new XElement("hayMas", more ? "S" : "N"),
            query.Vouchers.Count == 0
                ? ErrorBlock(4010, $"No se encontraron datos. No se encontraron comprobantes para la consulta de código {query.Code}.")
                : null);
        return call.Ok(new XElement(call.Operation.Output, result));
    }

    private static XElement Data(string name, ExportQuery query) => new(name,
        new XElement("codigoConsulta", query.Code),
        new XElement("fechaConsulta", query.Date.Iso()),
        new XElement("cuit", query.Cuit),
        new XElement("estado", query.State),
        new XElement("periodoDesde", query.From),
        new XElement("periodoHasta", query.To));

    private static XElement Voucher(AuthorizedVoucher v) => new("comprobanteExportacion",
        new XElement("puntoVenta", v.PointOfSale),
        new XElement("tipoComprobante", v.VoucherType),
        new XElement("numero", v.Number),
        new XElement("tipoExportacion", 1),
        new XElement("fecha", v.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
        new XElement("cliente", v.ReceiverDocNumber.ToString(CultureInfo.InvariantCulture)),
        new XElement("moneda", "PES"),
        new XElement("cotizacionMoneda", 1),
        new XElement("importeTotal", v.Total.ToString("0.00", CultureInfo.InvariantCulture)),
        long.TryParse(v.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var cae) ? new XElement("cae", cae) : null,
        v.CodeExpiry is { } expiry ? new XElement("fechaVencimiento", expiry.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) : null,
        new XElement("items", new XElement("itemComprobanteExportacion",
            new XElement("descripcionProducto", "Total del comprobante"),
            new XElement("totalItem", v.Total.ToString("0.00", CultureInfo.InvariantCulture)))));

    /// <summary>An answer that only carries business errors: page 0 and no more pages, as the manual's examples.</summary>
    private static ContractAnswer Errors(ServiceCall call, int code, string text) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(ReturnOf(call), Zero(), ErrorBlock(code, text))));

    private static ContractAnswer FormatError(ServiceCall call, string text) =>
        call.Ok(new XElement(call.Operation.Output, new XElement(ReturnOf(call), Zero(),
            new XElement("erroresFormato", new XElement("errorFormato", new XElement("codigo", "2009"), new XElement("descripcion", text))))));

    private static XElement[] Zero() => [new("pagina", 0), new("hayMas", "N")];

    private static XElement ErrorBlock(int code, string text) =>
        new("errores", new XElement("error", new XElement("codigo", code), new XElement("descripcion", text)));

    private static string ReturnOf(ServiceCall call) => call.Name == "obtenerConsultas" ? "obtenerConsultasReturn" : "consultarComprobantesExpoReturn";

    /// <summary>
    /// The items of the 1-based page (never below 1 here), and whether more follow it: a page number near the
    /// int limit is a page past the last, not a wrapped number that shows the first.
    /// </summary>
    private static (List<T> Shown, bool More) PageOf<T>(IReadOnlyList<T> items, int page) => Paging.Page(items, page, PageSize);

    private static int Period(DateOnly date) => date.Year * 100 + date.Month;

    private static string Key(long code) => code.ToString("D10", CultureInfo.InvariantCulture);
}
