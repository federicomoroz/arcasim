namespace ArcaSim.Tests.Services.Aduana;

/// <summary>WDiaUtiDES: a PEMA device given of alta, sent on a trip and back, and consulted at each step.</summary>
public class WDiaUtiDesRulesTests
{
    private static Task<AduanaService> PemaAsync(AduanaKit kit, long cuit = AduanaKit.Caller) =>
        kit.ServiceAsync("WDiaUtiDES", (t, c) => AduanaKit.ArgAutentica(t, c), cuit);

    private static string Novelty(string id, string novelty, string interno = "INT-1") =>
        $"<argNovedadDispositivo><IdentificadorDispositivo>{id}</IdentificadorDispositivo><TipoDispositivo>CONT</TipoDispositivo>" +
        $"<IdentificadorDispositivoInterno>{interno}</IdentificadorDispositivoInterno><ModeloDispositivo>M1</ModeloDispositivo><Novedad>{novelty}</Novedad></argNovedadDispositivo>";

    private static string Move(string id, string state, string container = "MSCU1234567", string destination = "26001EC01000001A") =>
        $"<argDispositivo><IdentificadorDispositivo>{id}</IdentificadorDispositivo><IdentificadorDestinacion>{destination}</IdentificadorDestinacion>" +
        $"<Contenedor>{container}</Contenedor><Estado>{state}</Estado><FechaEstado>01/10/2026</FechaEstado></argDispositivo>";

    [Fact]
    public async Task A_device_given_of_alta_travels_and_comes_back_available()
    {
        await using var kit = await AduanaKit.StartAsync();
        var pema = await PemaAsync(kit);

        Assert.Equal("0", (await pema.CallAsync("NovedadDispositivo", Novelty("PEMA001", "A"))).Code());
        var leaving = await pema.CallAsync("ActualizaDispositivo", Move("PEMA001", "ZGSA"));
        var device = await pema.CallAsync("ConsultaDispositivo", "<argIdentificadorDispositivo>PEMA001</argIdentificadorDispositivo>");

        Assert.Equal("20304", leaving.Code());
        Assert.Equal("Procedimiento terminado OK.", leaving.V("DesError"));
        Assert.Equal("20304", device.Code());
        Assert.Equal("SALI", device.V("EstadoOperacion"));
        Assert.Equal("001", device.V("IdentificadorAduana"));
        Assert.Equal("MSCU1234567", device.V("IdentificadorContenedor"));
        Assert.Equal("26001SALI000001", device.V("IdentificadorSalida"));

        Assert.Equal("20304", (await pema.CallAsync("ActualizaDispositivo", Move("PEMA001", "PASA"))).Code());
        Assert.Equal("20304", (await pema.CallAsync("ActualizaDispositivo", Move("PEMA001", "ZGAR"))).Code());
        Assert.Equal("20304", (await pema.CallAsync("ActualizaDispositivo", Move("PEMA001", "DISP"))).Code());

        var containers = await pema.CallAsync("ConsultaContenedor", "<argContenedor><IdentificadorDispositivo>PEMA001</IdentificadorDispositivo></argContenedor>");
        Assert.Equal("DISP", containers.V("EstadoContenedor"));
        Assert.Equal("ARRI", containers.V("EstadoOperacion"));
    }

    [Fact]
    public async Task A_device_out_of_order_or_on_another_carrier_is_refused_with_the_manuals_codes()
    {
        await using var kit = await AduanaKit.StartAsync();
        var pema = await PemaAsync(kit);
        await pema.CallAsync("NovedadDispositivo", Novelty("PEMA002", "A"));

        var early = await pema.CallAsync("ActualizaDispositivo", Move("PEMA002", "PASA"));
        await pema.CallAsync("ActualizaDispositivo", Move("PEMA002", "ZGSA"));
        var otherCarrier = await pema.CallAsync("ActualizaDispositivo", Move("PEMA002", "PASA", container: "TGHU7654321"));
        var unknown = await pema.CallAsync("ActualizaDispositivo", Move("PEMA999", "ZGSA"));
        var badDate = await pema.CallAsync("InicioCargaSuelta",
            "<argInicioCargaSuelta><IdentificadorDispositivo>PEMA002</IdentificadorDispositivo><IdentificadorDestinacion>26001EC01000002B</IdentificadorDestinacion>" +
            "<PaisPatente>ARAB123CD</PaisPatente><Fecha>2026-10-01</Fecha></argInicioCargaSuelta>");

        Assert.Equal("12403", early.Code());
        Assert.Equal("El dispositivo está en estado incorrecto debe ser : ZGSA", early.V("DesError"));
        Assert.Equal("12623", otherCarrier.Code());
        Assert.Equal("12404", unknown.Code());
        Assert.Equal("Dispositivo INEXISTENTE", unknown.V("DesError"));
        Assert.Equal("10238", badDate.Code());
    }

    [Fact]
    public async Task The_padron_shows_a_baja_and_a_device_in_baja_can_no_longer_travel()
    {
        await using var kit = await AduanaKit.StartAsync();
        var pema = await PemaAsync(kit);
        await pema.CallAsync("NovedadDispositivo", Novelty("PEMA003", "A"));

        var duplicate = await pema.CallAsync("NovedadDispositivo", Novelty("PEMA004", "A"));
        Assert.Equal("0", (await pema.CallAsync("NovedadDispositivo", Novelty("PEMA003", "B"))).Code());
        var padron = await pema.CallAsync("ConsultaPemaPadron", "<argConsultaPemaPadron><IdentificadorDispositivo>PEMA003</IdentificadorDispositivo></argConsultaPemaPadron>");
        var trip = await pema.CallAsync("InicioCargaSuelta",
            "<argInicioCargaSuelta><IdentificadorDispositivo>PEMA003</IdentificadorDispositivo><IdentificadorDestinacion>26001EC01000002B</IdentificadorDestinacion>" +
            "<PaisPatente>ARAB123CD</PaisPatente><Fecha>01/10/2026</Fecha></argInicioCargaSuelta>");

        Assert.Equal("11895", duplicate.Code());
        Assert.Equal("BAJA", padron.V("Estado"));
        Assert.Equal("CONT", padron.V("Tipo"));
        Assert.Equal("12404", trip.Code());
    }

    [Fact]
    public async Task Another_prestadors_device_is_not_this_ones()
    {
        await using var kit = await AduanaKit.StartAsync();
        var mine = await PemaAsync(kit);
        var theirs = await PemaAsync(kit, AduanaKit.Other);
        await mine.CallAsync("NovedadDispositivo", Novelty("PEMA005", "A"));

        var baja = await theirs.CallAsync("NovedadDispositivo", Novelty("PEMA005", "B"));
        var padron = await theirs.CallAsync("ConsultaPemaPadron", "<argConsultaPemaPadron />");

        Assert.Equal("11891", baja.Code());
        Assert.Equal("10121", padron.Code());
    }

    [Fact]
    public async Task An_ATA_comes_from_the_taxpayers_and_a_bad_CUIT_is_refused()
    {
        await using var kit = await AduanaKit.StartAsync();
        var pema = await PemaAsync(kit);

        var ata = await pema.CallAsync("ConsultaDatosATA", $"<argCuitATA>{AduanaKit.Other}</argCuitATA>");
        var malformed = await pema.CallAsync("ConsultaDatosATA", "<argCuitATA>20-1</argCuitATA>");
        var badCheck = await pema.CallAsync("ConsultaDatosATA", "<argCuitATA>20222222220</argCuitATA>");

        Assert.Equal("HABI", ata.V("CodigoEstado"));
        Assert.Equal("ANA GOMEZ", ata.V("RazonSocial"));
        Assert.Equal("27045", malformed.Code());
        Assert.Equal("20714", badCheck.Code());
    }
}
