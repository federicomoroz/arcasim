namespace ArcaSim.Tests.Services.Aduana;

/// <summary>
/// wEnysa: a vehicle announced by its form and its border events. The rules
/// are called directly: the engine refuses wEnysa's tickets before them, since
/// its authentication has no CUIT next to the token.
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

    [Fact]
    public async Task A_vehicle_is_announced_and_its_events_recorded_once_each()
    {
        await using var kit = await AduanaKit.StartAsync();

        var announced = await kit.DirectAsync("wEnysa", "CargaDatosVehiculo", Vehicle());
        var entered = await kit.DirectAsync("wEnysa", "CargaEventoEntradaSalida", Event("ED"));
        var left = await kit.DirectAsync("wEnysa", "CargaEventoEntradaSalida", Event("SD"));
        var again = await kit.DirectAsync("wEnysa", "CargaEventoEntradaSalida", Event("ED"));
        var duplicate = await kit.DirectAsync("wEnysa", "CargaDatosVehiculo", Vehicle());

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

        var unknown = await kit.DirectAsync("wEnysa", "CargaEventoEntradaSalida", Event("ED", "9999"));
        var badKind = await kit.DirectAsync("wEnysa", "CargaEventoEntradaSalida", Event("XX"));
        var noPassengers = await kit.DirectAsync("wEnysa", "CargaDatosVehiculo", Vehicle(passengers: 0));
        var missing = await kit.DirectAsync("wEnysa", "CargaDatosVehiculo", "<datosVehiculo><pasajeros>1</pasajeros></datosVehiculo>");
        var query = await kit.DirectAsync("wEnysa", "ConsultaDatosVehiculo",
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
