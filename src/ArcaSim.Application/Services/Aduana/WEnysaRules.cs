using System.Globalization;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Aduana;

/// <summary>A vehicle the neighbouring country announced, by its form, with the entry and exit events it reported.</summary>
public sealed record EnysaForm(string Aduana, string Anio, string Numero, string CodigoPais, string Patente, int Pasajeros, List<string> Eventos);

/// <summary>
/// wEnysa, vehicles crossing from Chile (docs/arca/servicios/wEnysa.md):
/// CargaDatosVehiculo announces a vehicle by its form (aduana, year, number),
/// CargaEventoEntradaSalida adds its ED, SD and EO events, with the manual's
/// MsgError codes (p.7) and 0 "Operación correcta". A repeated form or event is
/// 4, an event on a form never announced is 5. ArcaSim's choices: the forms are
/// kept per caller, since ArcaSim does not tie a ticket to a country;
/// NotificaSalidaObservada, without a manual, records the notice and answers 0;
/// ConsultaDatosVehiculo reads Argentine vehicles leaving to Chile, which
/// nothing in ArcaSim creates, so a valid query answers 5; GetVersion is 1.0,
/// as observed.
/// </summary>
public sealed class WEnysaRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    private const string Forms = "wEnysa.formularios";
    private static readonly string[] Events = ["ED", "SD", "EO"];

    public string Service => "wEnysa";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "CargaDatosVehiculo" => await AnnounceAsync(call, call.Arg("datosVehiculo"), ct),
        "CargaEventoEntradaSalida" => await EventAsync(call, call.Arg("eventoEntradaSalida"), ct),
        "NotificaSalidaObservada" => await ObservedAsync(call, call.Arg("salidaObservada"), ct),
        "ConsultaDatosVehiculo" => Query(call),
        "GetVersion" => call.Ok(call.Sample().Set("GetVersionResult", "1.0")),
        _ => null,
    };

    private async Task<ContractAnswer> AnnounceAsync(ServiceCall call, XElement? data, CancellationToken ct)
    {
        if (data is null) return Error(call, 2, "Faltan datos", "datosVehiculo");
        if (Dia.FirstMissing(data, "aduanaFormulario", "anioFormulario", "numeroFormulario", "codigoPais", "patente") is { } missing)
            return Error(call, 2, "Faltan datos", missing);
        if (FormProblem(data) is { } invalid) return Error(call, 3, "Datos inválidos", invalid);
        if (data.Int("pasajeros") <= 0) return Error(call, 3, "Datos inválidos", "pasajeros");
        foreach (var flag in new[] { "propietario", "arrastreSiNo" })
            if (data.Field(flag) is not ("" or "S" or "N")) return Error(call, 3, "Datos inválidos", flag);

        var key = Key(call, data);
        if (await store.GetAsync<EnysaForm>(Forms, key, ct) is not null) return Error(call, 4, "Transacción / Evento ya ingresado", null);
        await store.PutAsync(Forms, key, new EnysaForm(data.Field("aduanaFormulario"), data.Field("anioFormulario"), data.Field("numeroFormulario"),
            data.Field("codigoPais"), data.Field("patente"), data.Int("pasajeros"), []), ct);
        return Correct(call);
    }

    private async Task<ContractAnswer> EventAsync(ServiceCall call, XElement? data, CancellationToken ct)
    {
        if (data is null) return Error(call, 2, "Faltan datos", "eventoEntradaSalida");
        if (Dia.FirstMissing(data, "aduanaFormulario", "anioFormulario", "numeroFormulario", "tipoTransaccion", "aduanaEvento", "fechaEvento") is { } missing)
            return Error(call, 2, "Faltan datos", missing);
        var kind = data.Field("tipoTransaccion");
        if (!Events.Contains(kind)) return Error(call, 6, "Operación inválida", "tipoTransaccion");
        if (FormProblem(data) is { } invalid) return Error(call, 3, "Datos inválidos", invalid);
        if (Day(data.Field("fechaVencimiento")) is { } expiry && expiry < DateOnly.FromDateTime(clock.Now.ToArgentina().DateTime))
            return Error(call, 3, "Datos inválidos", "fechaVencimiento");

        var key = Key(call, data);
        if (await store.GetAsync<EnysaForm>(Forms, key, ct) is not { } form) return Error(call, 5, "Transacción inexistente", null);
        if (form.Eventos.Contains(kind)) return Error(call, 4, "Transacción / Evento ya ingresado", null);
        form.Eventos.Add(kind);
        await store.PutAsync(Forms, key, form, ct);
        return Correct(call);
    }

    private async Task<ContractAnswer> ObservedAsync(ServiceCall call, XElement? data, CancellationToken ct)
    {
        if (data is null) return Error(call, 2, "Faltan datos", "salidaObservada");
        if (Dia.FirstMissing(data, "codigoAduanaMovimiento", "fechaMovimiento", "codigoPaisVehiculo", "patente") is { } missing)
            return Error(call, 2, "Faltan datos", missing);
        await store.PutAsync("wEnysa.salidasObservadas", $"{call.Cuit}/{await store.NextAsync("wEnysa.salidasObservadas", ct):D8}",
            new EnysaForm(data.Field("codigoAduanaMovimiento"), "", "", data.Field("codigoPaisVehiculo"), data.Field("patente"), 0, []), ct);
        return Correct(call);
    }

    private ContractAnswer Query(ServiceCall call)
    {
        var r = call.Request;
        if (Dia.FirstMissing(r, "anioFormulario", "tipoTransaccion", "aduanaFormulario", "numeroFormulario") is { } missing)
            return Error(call, 2, "Faltan datos", missing);
        if (r.Field("tipoTransaccion") != "SO") return Error(call, 6, "Operación inválida", "tipoTransaccion");
        if (FormProblem(r) is { } invalid) return Error(call, 3, "Datos inválidos", invalid);
        return Error(call, 5, "Transacción inexistente", null);
    }

    /// <summary>The form's year is not after this one and its number is above zero (manual p.10).</summary>
    private string? FormProblem(XElement data)
    {
        if (!int.TryParse(data.Field("anioFormulario"), NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year > clock.Now.ToArgentina().Year)
            return "anioFormulario";
        if (!long.TryParse(data.Field("numeroFormulario"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
            return "numeroFormulario";
        return null;
    }

    private static string Key(ServiceCall call, XElement data) =>
        $"{call.Cuit}/{data.Field("aduanaFormulario")}/{data.Field("anioFormulario")}/{data.Field("numeroFormulario")}";

    /// <summary>The manual's "yyyy/mm/dd hh:mi:ss tz" dates, by their day.</summary>
    private static DateOnly? Day(string text) =>
        text.Length >= 10 && DateOnly.TryParseExact(text[..10], "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static ContractAnswer Correct(ServiceCall call) => call.Done(call.Sample().Receipt(0, "Operación correcta", "descripcion"));

    private static ContractAnswer Error(ServiceCall call, long code, string text, string? field)
    {
        var answer = call.Fail(code, text);
        if (field is null || answer.Body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "descripcion") is not { } description) return answer;
        if (description.ElementsAfterSelf().FirstOrDefault(e => e.Name.LocalName == "descripcionAdicional") is { } existing) existing.Value = field;
        else description.AddAfterSelf(new XElement(description.Name.Namespace + "descripcionAdicional", field));
        return answer;
    }
}
