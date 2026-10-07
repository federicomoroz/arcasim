using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>An account a bank's client registered for a benefit, with the states it went through.</summary>
public sealed record BankAccount(long Bank, long Cuit, string Cbu, string Benefit, List<AccountState> History);

public sealed record AccountState(string State, DateOnly From);

/// <summary>A public entity exempt from the tax on debits and credits by article 2 of law 25.413.</summary>
public sealed record ExemptEntity(long Cuit, string Name);

/// <summary>
/// The register of accounts with benefits in the tax on bank debits and
/// credits, which taxpayers load through another channel. ChangeStateAsync is
/// the hook a test uses to move an account (and produce news); each bank
/// starts with two plainly fictitious accounts, and the exempt entities with one.
/// </summary>
public static class IcdbRegistry
{
    public const string Accounts = "wsicdb.cuentas";
    public const string Entities = "wsicdb.entes";

    public static string Key(long bank, long cuit, string cbu) => $"{bank}/{cuit}/{cbu}";

    public static async Task<BankAccount> ChangeStateAsync(
        IDocumentStore store, long bank, long cuit, string cbu, string benefit, string state, DateOnly from, CancellationToken ct = default)
    {
        var account = await store.GetAsync<BankAccount>(Accounts, Key(bank, cuit, cbu), ct) ?? new BankAccount(bank, cuit, cbu, benefit, []);
        account = account with { Benefit = benefit, History = [.. account.History.Where(h => h.From != from), new AccountState(state, from)] };
        account.History.Sort((a, b) => a.From.CompareTo(b.From));
        await store.PutAsync(Accounts, Key(bank, cuit, cbu), account, ct);
        return account;
    }

    public static Task SeedAsync(IDocumentStore store, long bank, CancellationToken ct) => Task.WhenAll(
        store.SeedAsync<BankAccount>($"{Accounts}/{bank}", Accounts,
        [
            (Key(bank, 20555555556, "9990001800000000000116"),
                new BankAccount(bank, 20555555556, "9990001800000000000116", "1", [new("AC", new(2025, 1, 2))])),
            (Key(bank, 20333333334, "9990001800000000000222"),
                new BankAccount(bank, 20333333334, "9990001800000000000222", "1", [new("AC", new(2024, 6, 3)), new("BA", new(2026, 9, 1))])),
        ], ct),
        store.SeedAsync<ExemptEntity>(Entities, Entities, [("30666666662", new ExemptEntity(30666666662, "ENTE PUBLICO FICTICIO DE ARCASIM"))], ct));
}

/// <summary>
/// Beneficios en créditos y débitos bancarios (wsicdb, docs/arca/servicios/wsicdb.md):
/// a bank (the CUIT of the ticket) asks for the news of a date, the state of
/// an account at a date, its active accounts and the exempt entities, from
/// IcdbRegistry. Validations and texts are the manual's: 1000 (bad CUIT),
/// 1001 (exempt entity), 1002 (future date), 1003 (bad CBU, by its two check
/// digits), 1004 (nothing found). The states table is the manual's; of the
/// benefits table only code 1 is copied in the spec, so that is the only row.
/// ArcaSim's choices: news are only the bank's own clients; a CUIT that is
/// not an exempt entity gets 1004 from consultarEnteExentoLey25413.
/// </summary>
public sealed class IcdbRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private static readonly (string Code, string Description)[] States =
        [("AC", "Autorizado"), ("BA", "Baja"), ("EX", "Excluido"), ("UI", "Baja por Uso Indebido")];

    public string Service => "wsicdb";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name == "dummy") return null;
        await IcdbRegistry.SeedAsync(store, call.Cuit, ct);
        var today = clock.Today();
        var request = call.Request.Find("solicitud") ?? new XElement("solicitud");

        switch (call.Name)
        {
            case "consultarNovedadesPorFecha":
            {
                var date = request.Date("fecha");
                if (date > today) return Answer(call, Error(1002));
                var news = (await AccountsAsync(call.Cuit, ct))
                    .SelectMany(a => a.History.Where(h => h.From == date).Select(h => Registro(a, h)))
                    .ToList();
                return news.Count == 0 ? Answer(call, Error(1004)) : Answer(call, news);
            }
            case "consultarEstadoCuentaPorFecha":
            {
                var cuit = request.Long("cuitCliente");
                var cbu = request.Text("cbu") ?? "";
                var date = request.Date("fecha");
                if (!Cuits.IsValid(cuit)) return Answer(call, Error(1000));
                if (date > today) return Answer(call, Error(1002));
                if (!ValidCbu(cbu)) return Answer(call, Error(1003));
                if (await store.GetAsync<ExemptEntity>(IcdbRegistry.Entities, cuit.ToString(CultureInfo.InvariantCulture), ct) is not null)
                    return Answer(call, Error(1001));
                var account = await store.GetAsync<BankAccount>(IcdbRegistry.Accounts, IcdbRegistry.Key(call.Cuit, cuit, cbu), ct);
                var state = account?.History.LastOrDefault(h => h.From <= date);
                return state is null ? Answer(call, Error(1004)) : Answer(call, Registro(account!, state));
            }
            case "consultarInscriptosRegistro":
                return Answer(call, (await AccountsAsync(call.Cuit, ct))
                    .Where(a => a.History.LastOrDefault(h => h.From <= today)?.State == "AC")
                    .Select(a => new XElement("registro",
                        new XElement("cuit", a.Cuit),
                        new XElement("cbu", a.Cbu),
                        new XElement("codBeneficio", a.Benefit),
                        new XElement("fechaVigencia", Iso(a.History.Last(h => h.From <= today).From)))));
            case "consultarEnteExentoLey25413":
            {
                var cuit = request.Long("cuitCliente");
                if (!Cuits.IsValid(cuit)) return Answer(call, Error(1000));
                var entity = await store.GetAsync<ExemptEntity>(IcdbRegistry.Entities, cuit.ToString(CultureInfo.InvariantCulture), ct);
                return entity is null ? Answer(call, Error(1004)) : Answer(call, Entity(entity));
            }
            case "consultarEntesExentosLey25413":
                return Answer(call, (await store.ListAsync<ExemptEntity>(IcdbRegistry.Entities, "", ct)).Select(Entity));
            case "consultarEstados":
                return Answer(call, States.Select(s => new XElement("estado", CodeAndText(s.Code, s.Description))));
            case "consultarBeneficios":
                return Answer(call, new XElement("beneficio",
                    CodeAndText("1", "Débitos y Créditos en cuenta corriente cuando se trate de Obra Sociales creadas o reconocidas por normas legales, nacionales o provinciales."),
                    new XElement("norma", "Anexo del Decreto N° 380/2001"),
                    new XElement("articulo", "7°")));
            default:
                return null;
        }
    }

    /// <summary>The CBU's two blocks, each closed by its check digit (BCRA's weights).</summary>
    public static bool ValidCbu(string cbu)
    {
        if (cbu.Length != 22 || !cbu.All(char.IsAsciiDigit)) return false;
        static bool Check(string block, int[] weights) =>
            (10 - block.Take(weights.Length).Select((d, i) => (d - '0') * weights[i]).Sum() % 10) % 10 == block[^1] - '0';
        return Check(cbu[..8], [7, 1, 3, 9, 7, 1, 3]) && Check(cbu[8..], [3, 9, 7, 1, 3, 9, 7, 1, 3, 9, 7, 1, 3]);
    }

    private async Task<IReadOnlyList<BankAccount>> AccountsAsync(long bank, CancellationToken ct) =>
        await store.ListAsync<BankAccount>(IcdbRegistry.Accounts, $"{bank}/", ct);

    private static XElement Registro(BankAccount account, AccountState state) => new("registro",
        new XElement("cuit", account.Cuit),
        new XElement("cbu", account.Cbu),
        new XElement("codigoBeneficio", account.Benefit),
        new XElement("codigoEstado", state.State),
        new XElement("fechaVigencia", Iso(state.From)));

    private static XElement Entity(ExemptEntity entity) => new("ente", new XElement("cuit", entity.Cuit), new XElement("razonSocial", entity.Name));

    private static XElement Error(int code) => new("errores", new XElement("error", Pair(code.ToString(CultureInfo.InvariantCulture), code switch
    {
        1000 => "La CUIT ingresada es inválida o inexistente.",
        1001 => "La cuitCliente ingresada, pertenece a un ente público Exento por Ley 25.413 art. 2.",
        1002 => "La fecha a consultar no debe ser posterior al día actual.",
        1003 => "El número de CBU es inválido.",
        _ => "No existen registros según los parámetros de búsqueda ingresados.",
    })));

    private static XElement CodeAndText(string code, string text) => new("codigoDescripcion", Pair(code, text));

    private static XElement[] Pair(string code, string text) => [new("codigo", code), new("descripcion", text)];

    private static ContractAnswer Answer(ServiceCall call, params object[] content) =>
        call.Ok(new XElement(call.Operation.Output, new XElement("respuesta", content)));

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
