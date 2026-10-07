using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>What a transaction answered, kept so the same NroTransaccion answers the same again.</summary>
public sealed record DepOutcome(long Code, string Text, string? Additional = null, string? NroSalida = null);

/// <summary>A document (título or declaración) entered into a depósito: quantity per line in and out, and whether its entry was closed.</summary>
public sealed record DepDocument(string Documento, string Declaracion, string Titulo, bool Cerrado, Dictionary<int, decimal> Ingresado, Dictionary<int, decimal> Egresado);

/// <summary>A container entered into a depósito with the declaración and título it carries, and the salida that took it out.</summary>
public sealed record DepContainer(string Id, string Declaracion, string Titulo, string? Salida = null);

/// <summary>
/// wdepMovimientos, the terminals' and depositarios' movements (docs/arca/servicios/wdepMovimientos.md):
/// WdepIngresos enters lines and containers of a título or declaración,
/// WdepSalidas and WdepSalidasPorMT take them out with a NroSalida, and
/// WdepListaTitulosPorContenedor says what a container carries. Every
/// transaction answers by its NroTransaccion (p.8): a repeated one gets the
/// original answer even with other data, and one still in course gets 31209.
/// Success is 0 "Proceso OK". ArcaSim's choices: NroTransaccion is unique per
/// CUIT across the three methods; the salida number reads AA (year) BBB
/// (aduana) "SZP" and a sequence; the type of entry (depósito or zona
/// portuaria) is not told apart; WdepListarRutas keeps the contract's answer.
/// </summary>
public sealed class WdepMovimientosRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Transactions = "wdepMovimientos.transacciones";
    private const string Documents = "wdepMovimientos.documentos";
    private const string Containers = "wdepMovimientos.contenedores";
    private const string Ok = "Proceso OK";
    private readonly InFlight _running = new();

    /// <summary>One CUIT's cargo moves one transaction at a time: an exit reads what is available and writes back what is left.</summary>
    private readonly KeyedLocks<long> _cargo = new();

    public string Service => "wdepMovimientos";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "WdepIngresos" => await TransactionAsync(call, call.Arg("argwdepIngresos"), EnterAsync, ct),
        "WdepSalidas" => await TransactionAsync(call, call.Arg("argwdepSalidas"), ExitAsync, ct),
        "WdepSalidasPorMT" => await TransactionAsync(call, call.Arg("argwdepSalidas"), ExitByCarrierAsync, ct),
        "WdepListaTitulosPorContenedor" => await TitlesAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> TransactionAsync(
        ServiceCall call, XElement? arg, Func<ServiceCall, XElement, string, CancellationToken, Task<DepOutcome>> process, CancellationToken ct)
    {
        if (arg is null) return call.Fail(22, "Campo obligatorio", call.Name == "WdepIngresos" ? "argwdepIngresos" : "argwdepSalidas");
        var number = arg.Long("NroTransaccion");
        if (number <= 0) return call.Fail(36, "Valor invalido.", "NroTransaccion");
        var key = $"{call.Cuit}/{number}";
        if (await store.GetAsync<DepOutcome>(Transactions, key, ct) is { } done) return Answer(call, done);

        using var running = _running.TryEnter(key);
        if (running is null) return call.Fail(31209, "Aguarde, operacion en curso");
        if (await store.GetAsync<DepOutcome>(Transactions, key, ct) is { } meanwhile) return Answer(call, meanwhile);

        DepOutcome outcome;
        using (await _cargo.AcquireAsync(call.Cuit, ct))
            outcome = Dia.FirstMissing(arg, "Aduana", "LugarOperativo") is { } missing
                ? new DepOutcome(22, "Campo obligatorio", missing)
                : arg.Elements().FirstOrDefault(e => e.Name.LocalName == "Carga") is not { } cargo
                    ? new DepOutcome(22, "Campo obligatorio", "Carga")
                    : await process(call, cargo, $"{call.Cuit}/{arg.Field("Aduana")}/{arg.Field("LugarOperativo")}", ct);
        await store.PutAsync(Transactions, key, outcome, ct);
        return Answer(call, outcome);
    }

    private static ContractAnswer Answer(ServiceCall call, DepOutcome outcome) =>
        outcome.Code == 0
            ? call.Done(call.Sample().Receipt(0, Ok).Set("NroSalida", outcome.NroSalida))
            : call.Fail(outcome.Code, outcome.Text, outcome.Additional);

    private async Task<DepOutcome> EnterAsync(ServiceCall call, XElement cargo, string place, CancellationToken ct)
    {
        var declaration = cargo.Field("IdDeclaracion");
        var title = cargo.Field("TituloTransporte");
        if (declaration == "" && title == "") return new(22, "Campo obligatorio", "TituloTransporte");
        var close = cargo.Field("CierreIngreso");
        if (close is not ("" or "S" or "N")) return new(36, "Valor invalido.", "CierreIngreso");

        var name = title != "" ? title : declaration;
        var document = await store.GetAsync<DepDocument>(Documents, $"{place}/{name}", ct)
                       ?? new DepDocument(name, declaration, title, false, [], []);
        if (document.Cerrado) return new(10142, "Ya se efectuo el cierre de ingreso. No se puede ingresar otra vez");

        foreach (var line in Items(cargo, "LineasMercaderia"))
            document.Ingresado[line.Int("NroLinea")] = document.Ingresado.GetValueOrDefault(line.Int("NroLinea")) + line.Decimal("CantidadIngresada");
        await store.PutAsync(Documents, $"{place}/{name}", document with { Cerrado = close == "S" }, ct);
        foreach (var container in Items(cargo, "Contenedores"))
            await store.PutAsync(Containers, $"{place}/{container.Field("IdContenedor")}",
                new DepContainer(container.Field("IdContenedor"), declaration, title), ct);
        return new(0, Ok);
    }

    /// <summary>WdepSalidas: the títulos with their lines and containers, under the salida's declaración.</summary>
    private Task<DepOutcome> ExitAsync(ServiceCall call, XElement cargo, string place, CancellationToken ct)
    {
        var exit = cargo.Elements().FirstOrDefault(e => e.Name.LocalName == "Salida");
        var declaration = exit.Field("IdDeclaracion");
        if (declaration == "") return Task.FromResult(new DepOutcome(22, "Campo obligatorio", "IdDeclaracion"));
        var titles = Items(cargo, "Titulos").ToList();
        var lines = titles.SelectMany(t => Items(t, "LineasMercaderia").Select(l => (t.Field("IdTitulo"), l.Int("NroLinea"), l.Decimal("CantidadEgresada")))).ToList();
        var containers = titles.SelectMany(t => Items(t, "Contenedores").Select(c => c.Field("IdContenedor"))).ToList();
        return TakeOutAsync(place, declaration, lines, containers, ct);
    }

    /// <summary>WdepSalidasPorMT: carga suelta by declaración and título, and containers on the medio transportador.</summary>
    private Task<DepOutcome> ExitByCarrierAsync(ServiceCall call, XElement cargo, string place, CancellationToken ct)
    {
        var declarations = Items(cargo, "CargaSuelta").ToList();
        var lines = declarations.SelectMany(d => Items(d, "Titulos").SelectMany(t => Items(t, "LineasMercaderia")
            .Select(l => (Title: t.Field("IdTitulo") is var id && id != "" ? id : d.Field("IdDeclaracion"), l.Int("NroLinea"), l.Decimal("CantidadEgresada"))))).ToList();
        var containers = Items(cargo, "Contenedores").Select(c => c.Field("IdContenedor")).ToList();
        var declaration = declarations.Select(d => d.Field("IdDeclaracion")).FirstOrDefault() ?? "";
        return TakeOutAsync(place, declaration, lines, containers, ct);
    }

    private async Task<DepOutcome> TakeOutAsync(
        string place, string declaration, List<(string Title, int Line, decimal Quantity)> lines, List<string> containers, CancellationToken ct)
    {
        var documents = new Dictionary<string, DepDocument>();
        foreach (var (title, line, quantity) in lines)
        {
            if (!documents.TryGetValue(title, out var document))
            {
                if (await store.GetAsync<DepDocument>(Documents, $"{place}/{title}", ct) is not { } found)
                    return new(10367, "Titulo de transporte inexistente.", title);
                documents[title] = document = found;
            }
            var available = document.Ingresado.GetValueOrDefault(line) - document.Egresado.GetValueOrDefault(line);
            if (quantity > available) return new(10034, "Cantidad superior a la disponible", $"{title} linea {line}");
            document.Egresado[line] = document.Egresado.GetValueOrDefault(line) + quantity;
        }
        var leaving = new List<DepContainer>();
        foreach (var id in containers)
        {
            if (await store.GetAsync<DepContainer>(Containers, $"{place}/{id}", ct) is not { } container) return new(36, "Valor invalido.", id);
            if (container.Salida is not null) return new(10973, "Contenedor ya afectado a una Salida", id);
            leaving.Add(container);
        }

        var aduana = place.Split('/')[1];
        var number = $"{clock.Now.ToArgentina():yy}{aduana}SZP{await store.NextAsync("wdepMovimientos.salidas", ct):D6}";
        foreach (var (title, document) in documents) await store.PutAsync(Documents, $"{place}/{title}", document, ct);
        foreach (var container in leaving) await store.PutAsync(Containers, $"{place}/{container.Id}", container with { Salida = number }, ct);
        return new(0, Ok, NroSalida: number);
    }

    private async Task<ContractAnswer> TitlesAsync(ServiceCall call, CancellationToken ct)
    {
        var arg = call.Arg("argwdepListaTitulosPorContenedor");
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo", "IdContenedor") is { } missing) return call.Fail(22, "Campo obligatorio", missing);
        var key = $"{call.Cuit}/{arg.Field("Aduana")}/{arg.Field("LugarOperativo")}/{arg.Field("IdContenedor")}";
        if (await store.GetAsync<DepContainer>(Containers, key, ct) is not { } container) return call.Fail(10121, Dia.NoData);

        var answer = call.Sample().Receipt(0, Ok);
        // The sample stops above the títulos, too deep for it: the list is written whole.
        answer.Find("Declaraciones")!.ReplaceNodes(new XElement(call.Name("Declaracion"),
            new XElement(call.Name("IdDeclaracion"), container.Declaracion == "" ? container.Titulo : container.Declaracion),
            new XElement(call.Name("Titulos"), new XElement(call.Name("Titulo"),
                new XElement(call.Name("IdTitulo"), container.Titulo == "" ? container.Declaracion : container.Titulo)))));
        answer.Find("Contenedor")!.Set("IdContenedor", container.Id);
        return call.Done(answer);
    }

    /// <summary>The items of an ASMX array: the children of the named list under the element.</summary>
    private static IEnumerable<XElement> Items(XElement element, string list) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == list)?.Elements() ?? [];
}
