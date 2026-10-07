using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>One product line of a movement: what the stock is kept by (NCM, product code, origin) and how much.</summary>
public sealed record TlItem(string Ncm, string Codigo, string Origen, string Descripcion, decimal Cantidad, decimal Valor = 0);

/// <summary>A stock movement with its id: ING (entry), SAL (salida particular), VTA (sale), DES (destruction), DEV (return), JUS (DIFE justification).</summary>
public sealed record TlMovement(
    string Id, string Codigo, DateTimeOffset Fecha, string Aduana, string Lugar, string Comprobante, string Estado, List<TlItem> Items);

/// <summary>A product's stock at one depósito; it goes negative when a sale finds less than it sells.</summary>
public sealed record TlStock(string Aduana, string Lugar, TlItem Item);

/// <summary>A stock difference (DIFE) a sale left, and the justifications given for it.</summary>
public sealed record TlDife(
    string Id, string Aduana, string Lugar, TlItem Item, string TipoComprobante, string NroComprobante, DateTimeOffset Fecha,
    DateTimeOffset Vencimiento, string Estado, string IdMovimiento, List<TlJustification> Justificaciones);

public sealed record TlJustification(string Codigo, string Texto, decimal Cantidad, DateTimeOffset Fecha);

/// <summary>The answer a transaction got, replayed when the same transaccion comes again to the same method.</summary>
public sealed record TlReplay(string Xml);

/// <summary>
/// wgestiendaslibres, the free shops' stock (docs/arca/servicios/wgestiendaslibres.md):
/// entries (IngresarMercaderia, enabled by SalidaParticular when foreign),
/// sales (VentaMercaderia, which never fails for lack of stock: it goes
/// negative and leaves a DIFE per article), destruction and returns, the DIFE's
/// justification (REG to PRE), and the stock, movements and DIFE queries, in
/// the "diav2" answer (ListaErrores, Server, TimeStamp) with Codigo 0 on
/// success. Every alta answers by its transaccion (pp.8-9): the same one again
/// in the same method gets the original answer, and one still in course gets
/// 41973. ArcaSim's choices where the manual is silent: an entry whose
/// comprobante is a particular's declaration (type PI in its number) waits
/// for SalidaParticular, any other is a national entry the customs authorize
/// at once; a DIFE falls due 30 days after the sale; an unknown declaration
/// or DIFE is 30286. TrasladarMercaderia, RegistrarNotaDebitoCredito, the
/// packs and CambiarCodigoProducto keep the contract's answer.
/// </summary>
public sealed class TiendasLibresRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Stock = "wgestiendaslibres.stock";
    private const string Movements = "wgestiendaslibres.movimientos";
    private const string Difes = "wgestiendaslibres.dife";
    private const string Replays = "wgestiendaslibres.transacciones";
    private const string NoData = "No hay datos para los criterios ingresados";
    private readonly InFlight _running = new();

    public string Service => "wgestiendaslibres";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "IngresarMercaderia" => await OnceAsync(call, EnterAsync, ct),
        "SalidaParticular" => await OnceAsync(call, ReleaseAsync, ct),
        "VentaMercaderia" => await OnceAsync(call, SellAsync, ct),
        "DestruirMercaderia" => await OnceAsync(call, (c, a, t) => TakeAsync(c, a, "listaMercaderiaDestruida", "DES", t), ct),
        "DevolverMercaderia" => await OnceAsync(call, (c, a, t) => TakeAsync(c, a, "listaMercaderiaDevuelta", "DEV", t), ct),
        "RegistrarJustificacionDIFE" => await OnceAsync(call, JustifyAsync, ct),
        "ConsultarStock" => await StockAsync(call, ct),
        "ConsultarMovimientos" => await MovementsAsync(call, ct),
        "GetMovimiento" => await MovementAsync(call, ct),
        "ConsultarDIFE" => await DifeAsync(call, ct),
        _ => null,
    };

    // ---- Altas, once per transaccion ---------------------------------------------------

    private async Task<ContractAnswer> OnceAsync(ServiceCall call, Func<ServiceCall, XElement, CancellationToken, Task<XElement>> process, CancellationToken ct)
    {
        var arg = call.Request.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Params", StringComparison.Ordinal));
        if (arg is null) return call.Ok(Refused(call, 42034, $"Falta el dato obligatorio arg{call.Name}Params"));
        var transaction = arg.Field("transaccion");
        if (transaction == "") return call.Ok(Refused(call, 42034, "Falta el dato obligatorio transaccion"));
        var key = $"{call.Cuit}/{call.Name}/{transaction}";
        if (await store.GetAsync<TlReplay>(Replays, key, ct) is { } done) return call.Ok(XElement.Parse(done.Xml));

        using var running = _running.TryEnter(key);
        if (running is null) return call.Ok(Refused(call, 41973, $"La transaccion {transaction} ya se encuentra en proceso - acceso denegado."));
        if (await store.GetAsync<TlReplay>(Replays, key, ct) is { } meanwhile) return call.Ok(XElement.Parse(meanwhile.Xml));
        var answer = await process(call, arg, ct);
        await store.PutAsync(Replays, key, new TlReplay(answer.ToString(SaveOptions.DisableFormatting)), ct);
        return call.Ok(answer);
    }

    private async Task<XElement> EnterAsync(ServiceCall call, XElement arg, CancellationToken ct)
    {
        if (Dia.FirstMissing(arg, "aduana", "lugarOperativo", "idComprobante", "origen") is { } missing) return Refused(call, 42034, $"Falta el dato obligatorio {missing}");
        var receipt = arg.Field("idComprobante");
        var items = Items(arg, "ListaMercaderiaIngresada", arg.Field("origen"));
        if (items.Count == 0) return Refused(call, 42034, "Falta el dato obligatorio ListaMercaderiaIngresada");
        var foreign = receipt.Length == 16 && receipt[5..7] == "PI";
        var movement = await MoveAsync(call, "ING", arg.Field("aduana"), arg.Field("lugarOperativo"), receipt, foreign ? "PEND" : "AUTO", items, ct);
        if (!foreign) await AddAsync(call, movement.Aduana, movement.Lugar, items, ct);
        return Answer(call, r => r.Set("id", movement.Id).Set("idMovimiento", movement.Id));
    }

    /// <summary>The salida de oficio of a particular's declaration: its entry's goods become stock.</summary>
    private async Task<XElement> ReleaseAsync(ServiceCall call, XElement arg, CancellationToken ct)
    {
        if (Dia.FirstMissing(arg, "aduana", "lugarOperativo", "idDeclaracion") is { } missing) return Refused(call, 42034, $"Falta el dato obligatorio {missing}");
        var entry = (await store.ListAsync<TlMovement>(Movements, $"{call.Cuit}/", ct))
            .FirstOrDefault(m => m.Codigo == "ING" && m.Estado == "PEND" && m.Comprobante == arg.Field("idDeclaracion")
                                 && m.Aduana == arg.Field("aduana") && m.Lugar == arg.Field("lugarOperativo"));
        if (entry is null) return Refused(call, 30286, NoData);
        await store.PutAsync(Movements, $"{call.Cuit}/{entry.Id}", entry with { Estado = "AUTO" }, ct);
        await AddAsync(call, entry.Aduana, entry.Lugar, entry.Items, ct);
        var salida = await MoveAsync(call, "SAL", entry.Aduana, entry.Lugar, entry.Comprobante, "AUTO", entry.Items, ct);
        return Answer(call, r => r.Set("nroSalida", $"{clock.Now.ToArgentina():yy}{entry.Aduana}SALP{salida.Id.PadLeft(6, '0')}"));
    }

    private async Task<XElement> SellAsync(ServiceCall call, XElement arg, CancellationToken ct)
    {
        if (Dia.FirstMissing(arg, "aduana", "lugarOperativo", "tipoComprobante", "nroComprobante") is { } missing) return Refused(call, 42034, $"Falta el dato obligatorio {missing}");
        var (type, number) = (arg.Field("tipoComprobante"), arg.Field("nroComprobante"));
        var (aduana, place) = (arg.Field("aduana"), arg.Field("lugarOperativo"));
        if ((await store.ListAsync<TlMovement>(Movements, $"{call.Cuit}/", ct)).Any(m => m.Codigo == "VTA" && m.Comprobante == $"{type} {number}"))
            return Refused(call, 21526, $"Venta ya registrada {type} {number}");
        var items = Items(arg, "listaMercaderiaVendida", null);
        if (items.Count == 0) return Refused(call, 42034, "Falta el dato obligatorio listaMercaderiaVendida");

        var movement = await MoveAsync(call, "VTA", aduana, place, $"{type} {number}", "AUTO", items, ct);
        var negative = false;
        foreach (var item in items)
        {
            var stock = await StockOfAsync(call, aduana, place, item, ct);
            var available = Math.Max(stock?.Item.Cantidad ?? 0, 0);
            await PutStockAsync(call, aduana, place, (stock?.Item ?? item) with { Cantidad = (stock?.Item.Cantidad ?? 0) - item.Cantidad }, ct);
            if (available >= item.Cantidad) continue;
            negative = true;
            var id = (await store.NextAsync("wgestiendaslibres.dife", ct)).ToString(CultureInfo.InvariantCulture);
            var now = clock.Now.ToArgentina();
            await store.PutAsync(Difes, $"{call.Cuit}/{id.PadLeft(10, '0')}", new TlDife(id, aduana, place, item with { Cantidad = item.Cantidad - available },
                type, number, now, now.AddDays(30), "REG", movement.Id, []), ct);
        }
        return Answer(call, r => r.Set("idMovimiento", movement.Id), negative ? "Se registra diferencia por stock en negativo" : null);
    }

    /// <summary>Destruction and returns: every product has to have the stock it takes, or nothing moves.</summary>
    private async Task<XElement> TakeAsync(ServiceCall call, XElement arg, string list, string code, CancellationToken ct)
    {
        if (Dia.FirstMissing(arg, "aduana", "lugarOperativo", "idComprobante") is { } missing) return Refused(call, 42034, $"Falta el dato obligatorio {missing}");
        var (aduana, place) = (arg.Field("aduana"), arg.Field("lugarOperativo"));
        var items = Items(arg, list, arg.Field("origen") is var origin && origin != "" ? origin : null);
        if (items.Count == 0) return Refused(call, 42034, $"Falta el dato obligatorio {list}");
        foreach (var item in items)
        {
            if (await StockOfAsync(call, aduana, place, item, ct) is not { } stock)
                return Refused(call, 42303, "Producto inexistente para la combinacion CUIT-Aduana-Lugar Operativo.");
            if (stock.Item.Cantidad < item.Cantidad) return Refused(call, 42302, "No hay stock disponible para afectar.");
        }
        foreach (var item in items)
        {
            var stock = (await StockOfAsync(call, aduana, place, item, ct))!;
            await PutStockAsync(call, aduana, place, stock.Item with { Cantidad = stock.Item.Cantidad - item.Cantidad }, ct);
        }
        var movement = await MoveAsync(call, code, aduana, place, arg.Field("idComprobante"), "AUTO", items, ct);
        return Answer(call, r => r.Set("idMovimiento", movement.Id));
    }

    private async Task<XElement> JustifyAsync(ServiceCall call, XElement arg, CancellationToken ct)
    {
        if (Dia.FirstMissing(arg, "idDIFE") is { } missing) return Refused(call, 42034, $"Falta el dato obligatorio {missing}");
        var key = $"{call.Cuit}/{arg.Field("idDIFE").PadLeft(10, '0')}";
        if (await store.GetAsync<TlDife>(Difes, key, ct) is not { Estado: "REG" or "REC" } dife) return Refused(call, 30286, NoData);
        var reasons = arg.Elements().FirstOrDefault(e => e.Name.LocalName == "listaJustificacion")?.Elements().ToList() ?? [];
        if (reasons.Count == 0) return Refused(call, 42034, "Falta el dato obligatorio listaJustificacion");
        if (reasons.FirstOrDefault(r => r.Field("codJustificacion") == "") is not null) return Refused(call, 42034, "Falta el dato obligatorio codJustificacion");
        var total = reasons.Sum(r => r.Decimal("cantidadJustificacion"));
        if (total != dife.Item.Cantidad)
            return Refused(call, 21550, $"Total de cant justificadas {Number(total)} difiere de cant registrada en la DIFE {Number(dife.Item.Cantidad)}");

        var now = clock.Now.ToArgentina();
        var movement = await MoveAsync(call, "JUS", dife.Aduana, dife.Lugar, dife.Id, "AUTO", [dife.Item], ct);
        await store.PutAsync(Difes, key, dife with
        {
            Estado = "PRE",
            Justificaciones = reasons.Select(r => new TlJustification(r.Field("codJustificacion"), r.Field("textoJustificacion"), r.Decimal("cantidadJustificacion"), now)).ToList(),
        }, ct);
        return Answer(call, r => r.Set("idMovimiento", movement.Id));
    }

    // ---- Queries -----------------------------------------------------------------------

    private async Task<ContractAnswer> StockAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultarStockParams");
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo") is { } missing) return call.Ok(Refused(call, 42034, $"Falta el dato obligatorio {missing}"));
        bool Matches(string field, string value) => arg.Field(field) is var wanted && (wanted == "" || wanted == value);
        var found = (await store.ListAsync<TlStock>(Stock, $"{call.Cuit}/{arg.Field("Aduana")}/{arg.Field("LugarOperativo")}/", ct))
            .Where(s => Matches("NCM", s.Item.Ncm) && Matches("CodProducto", s.Item.Codigo) && Matches("Origen", s.Item.Origen)).ToList();
        if (found.Count == 0) return call.Ok(Refused(call, 30286, NoData));
        return call.Ok(Answer(call, r => r.Repeat("StockMercaderia", found, (row, s) => row
            .Set("NCM", s.Item.Ncm).Set("CodProducto", s.Item.Codigo).Set("Origen", s.Item.Origen).Set("Cantidad", s.Item.Cantidad).Set("EsPack", "N"))));
    }

    private async Task<ContractAnswer> MovementsAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultarMovimientosParams");
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo") is { } missing) return call.Ok(Refused(call, 42034, $"Falta el dato obligatorio {missing}"));
        var (from, to) = (arg!.Date("FechaDesde") ?? DateOnly.MinValue, arg!.Date("FechaHasta") ?? DateOnly.MaxValue);
        var found = (await store.ListAsync<TlMovement>(Movements, $"{call.Cuit}/", ct))
            .Where(m => m.Aduana == arg.Field("Aduana") && m.Lugar == arg.Field("LugarOperativo")
                        && DateOnly.FromDateTime(m.Fecha.DateTime) is var day && day >= from && day <= to)
            .ToList();
        if (found.Count == 0) return call.Ok(Refused(call, 30286, NoData));
        return call.Ok(Answer(call, r => r.Repeat("MovimientoMercaderia", found, (row, m) => row
            .Set("CodMovimiento", m.Codigo).Set("fechaMovimiento", m.Fecha).Set("idMovimiento", m.Id))));
    }

    private async Task<ContractAnswer> MovementAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultarMovimientosParams");
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo", "IdMovimiento") is { } missing) return call.Ok(Refused(call, 42034, $"Falta el dato obligatorio {missing}"));
        if (await store.GetAsync<TlMovement>(Movements, $"{call.Cuit}/{arg.Field("IdMovimiento").PadLeft(10, '0')}", ct) is not { } movement
            || movement.Aduana != arg.Field("Aduana") || movement.Lugar != arg.Field("LugarOperativo"))
            return call.Ok(Refused(call, 30286, NoData));
        return call.Ok(Answer(call, r =>
        {
            r.Find("DetalleMovimiento")!
                .Set("CodMovimiento", movement.Codigo)
                .Set("FechaMovimiento", movement.Fecha)
                .Set("AduanaOrigen", movement.Aduana)
                .Set("LugarOperativoOrigen", movement.Lugar)
                .Set("IdComprobante", movement.Comprobante)
                .Set("EstadoMovimiento", movement.Estado);
            r.Repeat("DetalleMovimientoMercaderia", movement.Items, (row, i) => row
                .Set("Origen", i.Origen).Set("NCM", i.Ncm).Set("CodProducto", i.Codigo).Set("DescProducto", i.Descripcion)
                .Set("Cantidad", i.Cantidad).Set("ValorUnitarioDol", i.Valor));
        }));
    }

    private async Task<ContractAnswer> DifeAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argConsultarDIFEParams");
        if (arg is null) return call.Ok(Refused(call, 7026, "Los parametros en la llamada al web method son obligatorios"));
        var (from, to) = (arg.Date("fechaDesde") ?? DateOnly.MinValue, arg.Date("fechaHasta") ?? DateOnly.MaxValue);
        if (to < from) return call.Ok(Refused(call, 20337, "La fecha HASTA debe ser mayor o igual a la fecha DESDE"));
        if (to > clock.Today()) return call.Ok(Refused(call, 20341, "La fecha HASTA debe ser menor o igual a la del dia"));
        bool Matches(string field, string value) => arg.Field(field) is var wanted && (wanted == "" || wanted == value);
        var found = (await store.ListAsync<TlDife>(Difes, $"{call.Cuit}/", ct))
            .Where(d => Matches("idDIFE", d.Id) && Matches("idMovimiento", d.IdMovimiento) && Matches("tipoComprobante", d.TipoComprobante)
                        && Matches("nroComprobante", d.NroComprobante) && Matches("codEstado", d.Estado)
                        && DateOnly.FromDateTime(d.Fecha.DateTime) is var day && day >= from && day <= to)
            .ToList();
        if (found.Count == 0) return call.Ok(Refused(call, 30286, NoData));
        return call.Ok(Answer(call, r => r.Repeat("DetalleDIFE", found, (row, d) =>
        {
            row.Set("idDIFE", d.Id).Set("aduana", d.Aduana).Set("lugarOperativo", d.Lugar).Set("NCM", d.Item.Ncm).Set("codProducto", d.Item.Codigo)
                .Set("descProducto", d.Item.Descripcion).Set("origen", d.Item.Origen).Set("cantidad", d.Item.Cantidad)
                .Set("tipoComprobante", d.TipoComprobante).Set("nroComprobante", d.NroComprobante).Set("fecha", d.Fecha).Set("fechaVenc", d.Vencimiento)
                .Set("codEstado", d.Estado).Set("fechaCobroLMAN", Legajos.None).Set("montoLMAN", 0).Set("idMovimiento", d.IdMovimiento);
            row.Repeat("DetalleJustificacionDIFE", d.Justificaciones, (j, value) => j
                .Set("codJustificacion", value.Codigo).Set("textoJustificacion", value.Texto).Set("fecJustificacion", value.Fecha)
                .Set("cantidadJustificacion", value.Cantidad));
        })));
    }

    // ---- Shapes and state --------------------------------------------------------------

    /// <summary>
    /// A successful answer: Codigo 0 in ListaErrores, the server (the address the catalog says the service always sends, which
    /// the sample already carries) and the result's own fields as the callback fills them.
    /// </summary>
    private static XElement Answer(ServiceCall call, Action<XElement> fill, string? additional = null)
    {
        var answer = call.Sample();
        var result = answer.Elements().First();
        fill(result);
        result.AddFirst(Errors(call, 0, null, additional));
        return answer.Clean();
    }

    /// <summary>A refused call: its error in ListaErrores, the server (the catalog's) and the moment, nothing else.</summary>
    private static XElement Refused(ServiceCall call, long code, string text)
    {
        var answer = call.Sample();
        var result = answer.Elements().First();
        result.Elements().Where(e => e.Name.LocalName is not ("Server" or "TimeStamp")).Remove();
        result.AddFirst(Errors(call, code, text, null));
        return answer;
    }

    private static XElement Errors(ServiceCall call, long code, string? text, string? additional) =>
        new(call.Name("ListaErrores"), new XElement(call.Name("DetalleError"),
            new XElement(call.Name("Codigo"), code),
            text is null ? null : new XElement(call.Name("Descripcion"), text),
            additional is null ? null : new XElement(call.Name("DescripcionAdicional"), additional)));

    private static List<TlItem> Items(XElement arg, string list, string? origin) =>
        arg.Elements().FirstOrDefault(e => e.Name.LocalName == list)?.Elements()
            .Select(i => new TlItem(i.Field("NCM"), i.Field("codProducto"), origin ?? i.Field("origen"), i.Field("descProducto"), i.Decimal("cantidad"), i.Decimal("valorUnitarioDol")))
            .ToList() ?? [];

    private async Task<TlMovement> MoveAsync(ServiceCall call, string code, string aduana, string place, string receipt, string state, List<TlItem> items, CancellationToken ct)
    {
        var id = (await store.NextAsync("wgestiendaslibres.movimientos", ct)).ToString(CultureInfo.InvariantCulture);
        var movement = new TlMovement(id, code, clock.Now.ToArgentina(), aduana, place, receipt, state, items);
        await store.PutAsync(Movements, $"{call.Cuit}/{id.PadLeft(10, '0')}", movement, ct);
        return movement;
    }

    private async Task AddAsync(ServiceCall call, string aduana, string place, IEnumerable<TlItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var stock = await StockOfAsync(call, aduana, place, item, ct);
            await PutStockAsync(call, aduana, place, item with { Cantidad = (stock?.Item.Cantidad ?? 0) + item.Cantidad }, ct);
        }
    }

    private Task<TlStock?> StockOfAsync(ServiceCall call, string aduana, string place, TlItem item, CancellationToken ct) =>
        store.GetAsync<TlStock>(Stock, StockKey(call, aduana, place, item), ct);

    private Task PutStockAsync(ServiceCall call, string aduana, string place, TlItem item, CancellationToken ct) =>
        store.PutAsync(Stock, StockKey(call, aduana, place, item), new TlStock(aduana, place, item), ct);

    private static string StockKey(ServiceCall call, string aduana, string place, TlItem item) =>
        $"{call.Cuit}/{aduana}/{place}/{item.Ncm}/{item.Codigo}/{item.Origen}";

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
