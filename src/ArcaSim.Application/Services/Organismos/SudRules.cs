using System.Globalization;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Padron;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>What a CUIT owes in social security, by tax (301, 351) and period (YYYYMM).</summary>
public sealed record Debtor(long Cuit, List<Debt> Debts);

public sealed record Debt(long Tax, string Period);

/// <summary>
/// Deuda de proveedores y clientes (sud_restricciones,
/// docs/arca/servicios/sud_restricciones.md): whether a CUIT owes, and with
/// tieneDeudaV2 which tax and period, from the debts kept in
/// sud_restricciones.deudas, limited to taxes 301 and 351 as manual 1.3 says.
/// Every query gets a new consultaId; metadata carries the time with an
/// offset without colon and "servicios-externos". A CUIT with a wrong check
/// digit, or that neither the padrón nor the debts know, is the manual's fault
/// "CUIT sud:cuit INVALIDA", literally. The seed has two plainly fictitious
/// debtors, 30999999995 and 27999999994; every other CUIT owes nothing until a
/// document says otherwise. ArcaSim's choice where the schema forbids an empty
/// deudas: a CUIT without debt gets one deuda with impuesto 0 and an empty period.
/// </summary>
public sealed class SudRules(IDocumentStore store, PadronDirectory padron, IClock clock) : IServiceBehavior
{
    public const string Debts = "sud_restricciones.deudas";
    private const long FirstQuery = 200_000;

    public static IEnumerable<(string, Debtor)> Defaults() =>
    [
        ("30999999995", new Debtor(30999999995, [new(301, "202501"), new(351, "202501"), new(301, "202502")])),
        ("27999999994", new Debtor(27999999994, [new(301, "202503")])),
    ];

    public string Service => "sud_restricciones";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name is not ("tieneDeuda" or "tieneDeudaV2")) return null;
        await store.SeedAsync(Debts, Debts, Defaults(), ct);

        var cuit = call.Request.Long("cuit");
        var debtor = Cuits.IsValid(cuit) ? await store.GetAsync<Debtor>(Debts, cuit.ToString(CultureInfo.InvariantCulture), ct) : null;
        if (debtor is null && (!Cuits.IsValid(cuit) || await padron.FindAsync(cuit, ct) is null)) return call.Fault("CUIT sud:cuit INVALIDA");

        var debts = (debtor?.Debts ?? []).Where(d => d.Tax is 301 or 351).OrderBy(d => d.Period).ThenBy(d => d.Tax).ToList();
        var now = clock.Now.ToArgentina();
        var answer = call.Sample()
            .Set("tieneDeuda", debts.Count > 0)
            .Set("consultaId", FirstQuery + await store.NextAsync("sud_restricciones.consultas", ct))
            .Set("fechaHora", now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "-0300")
            .Set("servidor", "servicios-externos");
        if (call.Name == "tieneDeudaV2")
            answer.Repeat("deuda", debts.Count > 0 ? debts : [new Debt(0, "")], (e, d) => e.Set("impuesto", d.Tax).Set("periodoFiscal", d.Period));
        return call.Ok(answer);
    }
}
