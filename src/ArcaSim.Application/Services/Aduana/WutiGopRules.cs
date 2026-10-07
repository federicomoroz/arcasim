using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>An oficialized detailed declaration a depositario follows, and whether it already took it from each queue.</summary>
public sealed record GopDeclaration(
    long Cuit, string Aduana, string Lugar, string IdDecla, string CodTipDecla, long CuitImpoExpo, string NomImpoExpo, long CuitDesp,
    DateTimeOffset FechOfic, string CodEstDecla, string CodCanal, int Items, decimal MontoFob, decimal MontoPagar,
    bool CaratulaLeida = false, bool EstadoLeido = false);

/// <summary>
/// WutiGOPDeclaraciones, the Grandes Operadores' queries (docs/arca/servicios/WutiGOPDeclaraciones.md).
/// A read-only service: two queues (new oficialized declarations, state
/// changes) and each declaration's carátula, items by lote, liquidación and
/// states, with 0 "OK Procesado" and 30286 "No hay datos para los criterios
/// ingresados", the one business code of its own. ArcaSim's choices: nothing
/// in ArcaSim oficializes declarations, so the first query of a depositario at
/// an aduana and lugar operativo with none finds two import declarations
/// (IC04) oficialized that day; a declaration leaves PndListaGOPDetallada once
/// its carátula is read and PndListaGOPEstados once its states are; items come
/// two per lote; state OFIC and canal VERDE are ArcaSim's values; nothing is
/// cancelled or blocked; a missing Aduana or LugarOperativo is the DIA's 42034.
/// </summary>
public sealed class WutiGopRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Collection = "WutiGOPDeclaraciones.declaraciones";
    private const string Ok = "OK Procesado";
    private const string NoData = "No hay datos para los criterios ingresados";
    private const int PerLote = 2;

    /// <summary>The first query at an aduana and lugar operativo finds its declarations; requests that arrive together make them once.</summary>
    private readonly KeyedLocks<string> _seeding = new();

    public string Service => "WutiGOPDeclaraciones";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name == "Dummy") return null;
        var arg = call.Request.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("arg", StringComparison.Ordinal) && e.Name.LocalName != "argAutentica");
        if (Dia.FirstMissing(arg, "Aduana", "LugarOperativo") is { } missing) return call.Fail(42034, Dia.MissingText(missing));
        var place = $"{call.Cuit}/{arg.Field("Aduana")}/{arg.Field("LugarOperativo")}/";
        await SeedAsync(call.Cuit, arg.Field("Aduana"), arg.Field("LugarOperativo"), place, ct);

        if (call.Name is "PndListaGOPDetallada" or "PndListaGOPEstados")
        {
            var detailed = call.Name == "PndListaGOPDetallada";
            var pending = (await store.ListAsync<GopDeclaration>(Collection, place, ct)).Where(d => detailed ? !d.CaratulaLeida : !d.EstadoLeido).ToList();
            if (pending.Count == 0) return call.Fail(30286, NoData);
            var answer = call.Sample().Receipt(0, Ok, "DesError");
            answer.Repeat("Pendiente", pending, (row, d) => row.Set("IdDecla", d.IdDecla));
            return call.Done(answer);
        }

        var key = place + arg.Field("IdDecla");
        if (await store.GetAsync<GopDeclaration>(Collection, key, ct) is not { } declaration) return call.Fail(30286, NoData);
        switch (call.Name)
        {
            case "ListaGOPCaratDeta":
                await store.PutAsync(Collection, key, declaration with { CaratulaLeida = true }, ct);
                return call.Done(Caratula(call, declaration));
            case "ListaGOPItemsDeta":
                return ItemsOf(call, declaration, (int)arg!.Decimal("NroLote"));
            case "ListaGOPLiquiDeta":
                return call.Done(Liquidacion(call, declaration));
            case "ListaGOPEstados":
                await store.PutAsync(Collection, key, declaration with { EstadoLeido = true }, ct);
                return call.Done(States(call, declaration));
            case "ListaGOPCancelaA":
            case "ListaGOPItemsCancelados":
            case "ListaGOPBloqueos":
                return call.Fail(30286, NoData);
            default:
                return null;
        }
    }

    private static XElement Caratula(ServiceCall call, GopDeclaration d)
    {
        var answer = call.Sample().Receipt(0, Ok);
        answer.Find("Declaracion")!
            .Set("IdDecla", d.IdDecla)
            .Set("CodAduReg", d.Aduana)
            .Set("CodTipDecla", d.CodTipDecla)
            .Set("CodImpoExpo", "I")
            .Set("NomImpoExpo", d.NomImpoExpo)
            .Set("CuitImpoExpo", d.CuitImpoExpo)
            .Set("CuitDesp", d.CuitDesp)
            .Set("CodDivisaFob", "DOL")
            .Set("FechOfic", d.FechOfic)
            .Set("FechCump", Legajos.None)
            .Set("FechVencDestSusp", Legajos.None)
            .Set("MontoFobTotDol", d.MontoFob)
            .Set("MontoFob", d.MontoFob)
            .Set("CantDiasAutDestSusp", 0)
            .Set("CantDiasProrrDestSusp", 0)
            .Set("CantBultos", d.Items)
            .Set("CodEstDecla", d.CodEstDecla)
            .Set("CotizDivisa", 1)
            .Drop("Embarques").Drop("MarcasYNumeros").Drop("Documentos").Drop("DatosComplementarios")
            .Drop("Terceros").Drop("Beneficios").Drop("Reintegros");
        return answer;
    }

    private static ContractAnswer ItemsOf(ServiceCall call, GopDeclaration d, int lote)
    {
        var first = (Math.Max(lote, 1) - 1) * PerLote + 1;
        if (first > d.Items) return call.Fail(30286, NoData);
        var numbers = Enumerable.Range(first, Math.Min(PerLote, d.Items - first + 1)).ToList();
        var answer = call.Sample().Receipt(0, Ok);
        answer.Find("Items")!
            .Set("IdDecla", d.IdDecla)
            .Set("NroLote", Math.Max(lote, 1))
            .Set("IndUltLote", numbers[^1] == d.Items ? "S" : "N");
        answer.Repeat("Item", numbers, (row, n) => row
            .Set("NroItem", n)
            .Set("PosArancelaria", "8471.30.12.000K")
            .Set("CodUniDecla", "07")
            .Set("CantUniDecla", 10)
            .Set("MontoFobDivisa", d.MontoFob / d.Items)
            .Set("MontoFobDol", d.MontoFob / d.Items)
            .Set("PrecioUnitario", d.MontoFob / d.Items / 10)
            .Set("NroItemCancel", 0)
            .Drop("DocumentosItem").Drop("DatosComplementariosItem").Drop("BeneficiosItem")
            .Drop("LiquidacionesItem").Drop("VentajasItem").Drop("SubItems"));
        return call.Done(answer);
    }

    private static XElement Liquidacion(ServiceCall call, GopDeclaration d)
    {
        var answer = call.Sample().Receipt(0, Ok);
        answer.Find("Liquidaciones")!.Set("IdDecla", d.IdDecla);
        answer.Find("Liquidacion")!
            .Set("NroLiq", $"{d.IdDecla}01")
            .Set("MontoPagar", d.MontoPagar)
            .Set("MontoGaran", 0)
            .Set("CodDivisa", "PES")
            .Drop("LiqCarLineas");
        return answer;
    }

    private static XElement States(ServiceCall call, GopDeclaration d)
    {
        var answer = call.Sample().Receipt(0, Ok);
        var states = answer.Find("EstadosDeclaracion")!;
        foreach (var date in states.Elements().Where(e => e.Name.LocalName.StartsWith("Fech", StringComparison.Ordinal)))
            date.Value = ContractXml.Format(Legajos.None);
        states.Set("IdDecla", d.IdDecla)
            .Set("CuitImpoExpo", d.CuitImpoExpo)
            .Set("CodEstDecla", d.CodEstDecla)
            .Set("CodCanal", d.CodCanal)
            .Set("FechOfic", d.FechOfic)
            .Set("FechPresen", d.FechOfic);
        return answer;
    }

    private async Task SeedAsync(long cuit, string aduana, string lugar, string place, CancellationToken ct)
    {
        using var turn = await _seeding.AcquireAsync(place, ct);
        if ((await store.ListAsync<GopDeclaration>(Collection, place, ct)).Count > 0) return;
        var now = clock.Now.ToArgentina();
        for (var i = 0; i < 2; i++)
        {
            var number = await store.NextAsync("WutiGOPDeclaraciones.declaraciones", ct);
            var id = $"{now:yy}{aduana}IC04{number:D6}{(char)('A' + number % 26)}";
            await store.PutAsync(Collection, place + id, new GopDeclaration(cuit, aduana, lugar, id, "IC04", 30000000007, "IMPORTADORA DEL SIMULADOR SA",
                20222222223, now.AddHours(-2 - i), "OFIC", "VERDE", 3 - i, 15000m * (i + 1), 4200m * (i + 1)), ct);
        }
    }
}
