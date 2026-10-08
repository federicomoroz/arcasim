namespace ArcaSim.Tests.Services.Aduana;

/// <summary>
/// wEnysa: a vehicle announced by its form and its border events, over HTTP like
/// the other customs services. Its authentication has no CUIT next to the token,
/// so the engine takes the ticket's own (cuitFromTicket in the catalog).
/// </summary>
public class WEnysaRulesTests
{
    private static string Vehicle(string number = "1001", int passengers = 3) =>
        "<datosVehiculo><anioFabricacion>2020</anioFabricacion><aduanaFormulario>CL1</aduanaFormulario><anioFormulario>2026</anioFormulario>" +
        $"<arrastreSiNo>N</arrastreSiNo><codigoPais>CL</codigoPais><codigoPaisVehiculo>CL</codigoPaisVehiculo><numeroFormulario>{number}</numeroFormulario>" +
        $"<pasajeros>{passengers}</pasajeros><patente>ABCD12</patente><propietario>S</propietario></datosVehiculo>";

    private static string Event(string kind, string number = "1001") =>
        "<eventoEntradaSalida><aduanaEvento>073</aduanaEvento><aduanaFormulario>CL1</aduanaFormulario><anioFormulario>2026</anioFormulario>" +
        $"<codigoPais>CL</codigoPais><fechaEvento>2026/10/01 10:00:00 -03</fechaEvento><numeroFormulario>{number}</numeroFormulario>" +
        $"<pasajeros>3</pasajeros><patente>ABCD12</patente><tipoTransaccion>{kind}</tipoTransaccion></eventoEntradaSalida>";

    private static Task<AduanaService> EnysaAsync(AduanaKit kit) => kit.ServiceAsync("wEnysa", AduanaKit.Enysa);

    [Fact]
    public async Task A_vehicle_is_announced_and_its_events_recorded_once_each()
    {
        await using var kit = await AduanaKit.StartAsync();
        var enysa = await EnysaAsync(kit);

        var announced = await enysa.CallAsync("CargaDatosVehiculo", Vehicle());
        var entered = await enysa.CallAsync("CargaEventoEntradaSalida", Event("ED"));
        var left = await enysa.CallAsync("CargaEventoEntradaSalida", Event("SD"));
        var again = await enysa.CallAsync("CargaEventoEntradaSalida", Event("ED"));
        var duplicate = await enysa.CallAsync("CargaDatosVehiculo", Vehicle());

        Assert.Equal("0", announced.Code());
        Assert.Equal("Operación correcta", announced.V("descripcion"));
        Assert.Equal("0", entered.Code());
        Assert.Equal("0", left.Code());
        Assert.Equal("4", again.Code());
        Assert.Equal("Transacción / Evento ya ingresado", again.V("descripcion"));
        Assert.Equal("4", duplicate.Code());
    }

    [Fact]
    public async Task Events_on_unknown_forms_and_bad_data_get_the_manuals_codes()
    {
        await using var kit = await AduanaKit.StartAsync();
        var enysa = await EnysaAsync(kit);

        var unknown = await enysa.CallAsync("CargaEventoEntradaSalida", Event("ED", "9999"));
        var badKind = await enysa.CallAsync("CargaEventoEntradaSalida", Event("XX"));
        var noPassengers = await enysa.CallAsync("CargaDatosVehiculo", Vehicle(passengers: 0));
        var missing = await enysa.CallAsync("CargaDatosVehiculo", "<datosVehiculo><pasajeros>1</pasajeros></datosVehiculo>");
        var query = await enysa.CallAsync("ConsultaDatosVehiculo",
            "<anioFormulario>2026</anioFormulario><tipoTransaccion>SO</tipoTransaccion><aduanaFormulario>073</aduanaFormulario><numeroFormulario>5</numeroFormulario>");

        Assert.Equal("5", unknown.Code());
        Assert.Equal("6", badKind.Code());
        Assert.Equal("3", noPassengers.Code());
        Assert.Equal("pasajeros", noPassengers.V("descripcionAdicional"));
        Assert.Equal("2", missing.Code());
        Assert.Equal("Faltan datos", missing.V("descripcion"));
        Assert.Equal("5", query.Code());
    }

    [Fact]
    public async Task GetVersion_answers_over_HTTP_without_a_ticket()
    {
        await using var kit = await AduanaKit.StartAsync();
        var enysa = await kit.ServiceAsync("wEnysa", (_, _) => "");

        var version = await enysa.CallAsync("GetVersion", "");

        Assert.Equal("1.0", version.Value);
    }
}
