using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Domain;

namespace ArcaSim.Application.Padron;

/// <summary>
/// Padrón A4: the full view of a taxpayer (docs/arca/servicios/ws_sr_padron_a4.md):
/// taxes with their state, activities, monotributo category, addresses. The
/// same taxpayers WSFEv1 invoices and the constancia shows, so a CUIT looked up
/// here and invoiced there never disagree.
/// </summary>
public sealed class PadronA4Rules(PadronDirectory directory, IClock clock) : IServiceBehavior
{
    public string Service => "ws_sr_padron_a4";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name != "getPersona") return null;
        if (await PadronRules.FindAsync(directory, call, ct) is not { } found) return PadronRules.NotFound(call);
        var taxpayer = found;
        var physical = taxpayer.Kind == PersonKind.Fisica;
        var (first, last) = PadronDirectory.NamesOf(taxpayer);
        var period = PadronDirectory.PeriodOf(taxpayer);
        var registered = PadronRules.Timestamp(taxpayer.Profile.RegisteredOn ?? new DateOnly(period / 100, period % 100, 1));
        var (activityId, activity) = PadronDirectory.ActivityOf(taxpayer);
        var monotributo = PadronRules.IsMonotributo(taxpayer.VatCondition);

        var persona = new XElement("persona",
            monotributo ? null : new XElement("actividad",
                new XElement("descripcionActividad", activity),
                new XElement("idActividad", activityId),
                new XElement("nomenclador", 883),
                new XElement("orden", 1),
                new XElement("periodo", period)),
            physical ? new XElement("apellido", last) : null,
            monotributo
                ? new XElement("categoria",
                    new XElement("descripcionCategoria", "A LOCACIONES DE SERVICIOS"),
                    new XElement("estado", "ACTIVO"),
                    new XElement("idCategoria", 20),
                    new XElement("idImpuesto", 20),
                    new XElement("periodo", period))
                : null,
            PadronRules.Address("domicilio", taxpayer, withOrder: true),
            new XElement("estadoClave", taxpayer.Active ? "ACTIVO" : "INACTIVO"),
            physical ? null : new XElement("fechaContratoSocial", registered),
            new XElement("fechaInscripcion", registered),
            physical ? null : new XElement("formaJuridica", taxpayer.Profile.LegalForm ?? "SOC. ANONIMA"),
            new XElement("idPersona", taxpayer.Cuit),
            PadronRules.TaxesOf(taxpayer).Select(t => new XElement("impuesto",
                new XElement("descripcionImpuesto", t.Description),
                new XElement("diaPeriodo", 1),
                new XElement("estado", "ACTIVO"),
                new XElement("ffInscripcion", registered),
                new XElement("idImpuesto", t.Id),
                new XElement("periodo", period))),
            new XElement("mesCierre", 12),
            physical ? new XElement("nombre", first) : null,
            physical ? new XElement("numeroDocumento", Cuits.DocumentOf(taxpayer.Cuit)) : null,
            physical ? null : new XElement("razonSocial", taxpayer.Name.ToUpperInvariant()),
            new XElement("tipoClave", "CUIT"),
            physical ? new XElement("tipoDocumento", "DNI") : null,
            new XElement("tipoPersona", physical ? "FISICA" : "JURIDICA"));

        return call.Ok(new XElement(call.Operation.Output, new XElement("personaReturn", PadronRules.Metadata(clock), persona)));
    }
}

/// <summary>Padrón A10: the short view, name, address and main activity (docs/arca/servicios/ws_sr_padron_a10.md).</summary>
public sealed class PadronA10Rules(PadronDirectory directory, IClock clock) : IServiceBehavior
{
    public string Service => "ws_sr_padron_a10";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name != "getPersona") return null;
        if (await PadronRules.FindAsync(directory, call, ct) is not { } taxpayer) return PadronRules.NotFound(call);
        var physical = taxpayer.Kind == PersonKind.Fisica;
        var (first, last) = PadronDirectory.NamesOf(taxpayer);
        var (activityId, activity) = PadronDirectory.ActivityOf(taxpayer);

        var persona = new XElement("persona",
            physical ? new XElement("apellido", last) : null,
            new XElement("descripcionActividadPrincipal", activity),
            PadronRules.Address("domicilio", taxpayer, withOrder: false),
            new XElement("estadoClave", taxpayer.Active ? "ACTIVO" : "INACTIVO"),
            new XElement("idActividadPrincipal", activityId),
            new XElement("idPersona", taxpayer.Cuit),
            physical ? new XElement("nombre", first) : null,
            physical ? new XElement("numeroDocumento", Cuits.DocumentOf(taxpayer.Cuit)) : null,
            physical ? null : new XElement("razonSocial", taxpayer.Name.ToUpperInvariant()),
            new XElement("tipoClave", "CUIT"),
            physical ? new XElement("tipoDocumento", "DNI") : null,
            new XElement("tipoPersona", physical ? "FISICA" : "JURIDICA"));

        return call.Ok(new XElement(call.Operation.Output, new XElement("personaReturn", PadronRules.Metadata(clock), persona)));
    }
}

/// <summary>
/// Padrón A100: the tables the other padrón services take their texts from
/// (docs/arca/servicios/ws_sr_padron_a100.md). A collection name ArcaSim does
/// not have is a fault: what ARCA answers is not documented.
/// </summary>
public sealed class PadronA100Rules(IClock clock) : IServiceBehavior
{
    public string Service => "ws_sr_padron_a100";

    public Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct)
    {
        if (call.Name != "getParameterCollectionByName") return Task.FromResult<ContractAnswer?>(null);
        var name = call.Request.Text("collectionName") ?? "";
        if (!PadronTables.Collections.TryGetValue(name, out var collection))
            return Task.FromResult<ContractAnswer?>(call.Fault($"No existe la coleccion {name}"));

        var answer = new XElement(call.Operation.Output,
            new XElement("parameterCollectionReturn",
                PadronRules.Metadata(clock),
                new XElement("parameterCollection",
                    new XElement("name", name),
                    collection.Rows.Select(row => new XElement("parameterList",
                        row.Attributes.Select(a => new XElement("attributeList", new XElement("name", a.Key), new XElement("value", a.Value))),
                        new XElement("description", row.Description),
                        new XElement("id", row.Id))))));
        return Task.FromResult<ContractAnswer?>(call.Ok(answer));
    }
}

internal static class PadronRules
{
    public static async Task<Taxpayer?> FindAsync(PadronDirectory directory, ServiceCall call, CancellationToken ct)
    {
        var id = call.Request.Long("idPersona");
        return Cuits.IsValid(id) ? await directory.FindAsync(id, ct) : null;
    }

    /// <summary>A13's texts, the documented ones of the same system (sr-padron).</summary>
    public static ContractAnswer NotFound(ServiceCall call) =>
        Cuits.IsValid(call.Request.Long("idPersona"))
            ? call.Fault("La Clave (CUIT/CUIL) consultada es inexistente")
            : call.Fault("El Id de la persona no es valido");

    public static bool IsMonotributo(VatCondition condition) =>
        condition is VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido;

    /// <summary>The taxes the padrón shows for a VAT condition, with the ids the constancia uses.</summary>
    public static IEnumerable<(int Id, string Description)> TaxesOf(Taxpayer taxpayer)
    {
        if (IsMonotributo(taxpayer.VatCondition))
        {
            yield return (20, "MONOTRIBUTO");
            yield break;
        }
        if (taxpayer.VatCondition is not (VatCondition.ResponsableInscripto or VatCondition.Exento)) yield break;
        yield return taxpayer.Kind == PersonKind.Juridica ? (10, "GANANCIAS SOCIEDADES") : (11, "GANANCIAS PERSONAS FISICAS");
        yield return taxpayer.VatCondition == VatCondition.Exento ? (32, "IVA EXENTO") : (30, "IVA");
    }

    public static XElement Address(string name, Taxpayer taxpayer, bool withOrder)
    {
        var address = PadronDirectory.AddressOf(taxpayer);
        return new XElement(name,
            new XElement("codPostal", address.PostalCode),
            new XElement("descripcionProvincia", address.Province),
            new XElement("direccion", address.Street),
            new XElement("idProvincia", address.ProvinceId),
            new XElement("localidad", address.Locality),
            withOrder ? new XElement("orden", 1) : null,
            new XElement("tipoDomicilio", "FISCAL"));
    }

    public static XElement Metadata(IClock clock) => new("metadata",
        new XElement("fechaHora", clock.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)),
        new XElement("servidor", "arcasim"));

    public static string Timestamp(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);
}
