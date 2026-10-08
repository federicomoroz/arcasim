using ArcaSim.Application.Contracts;
using ArcaSim.Application.Services.Aduana;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.Aduana;

/// <summary>wgesprecintosdepfis: a CEMA precinto given of alta, activated, reporting alarms, deactivated and given of baja.</summary>
public class WgesPrecintosRulesTests
{
    private static Task<AduanaService> CemaAsync(AduanaKit kit) =>
        kit.ServiceAsync("wgesprecintosdepfis", (t, c) => AduanaKit.ArgAutentica(t, c, "ISTA"));

    private static string Alta(string id) =>
        $"<argPrecinto><IdPrecinto>{id}</IdPrecinto><Aduana>001</Aduana><LugarOperativo>DEPO1</LugarOperativo></argPrecinto>";

    private static string Ids(params string[] ids) => $"<IdPrecinto>{string.Concat(ids.Select(i => $"<string>{i}</string>"))}</IdPrecinto>";

    private static string Event(string id, string alarms) =>
        $"<argInformarEstadoPrecintos><EventoPrecintos><EventoPrecinto><IdPrecinto>{id}</IdPrecinto><CodAlarma>{alarms}</CodAlarma>" +
        "<FechaEvento>2026-10-01T11:30:00</FechaEvento></EventoPrecinto></EventoPrecintos></argInformarEstadoPrecintos>";

    [Fact]
    public async Task A_precinto_goes_through_its_monitoring_cycle()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cema = await CemaAsync(kit);

        Assert.Equal("0", (await cema.CallAsync("NovedadPrecinto", Alta("CEMA01"))).Code());
        var pending = await cema.CallAsync("ConsultarPrecintosPendientes", "");
        var started = await cema.CallAsync("IniciarMonitoreo", $"<argIniciarMonitoreo>{Ids("CEMA01")}</argIniciarMonitoreo>");
        var reported = await cema.CallAsync("InformarEstadoPrecintos", Event("CEMA01", "BTBJ+ABIE"));
        var active = await cema.CallAsync("ConsultarPrecintos", "<argConsultaPrecintos><IdPrecinto>CEMA01</IdPrecinto></argConsultaPrecintos>");

        Assert.Equal("SOAC", pending.V("Estado"));
        Assert.Equal("0", started.Code());
        Assert.Equal("OK", started.V("DesError"));
        Assert.Equal("0", reported.Code());
        Assert.Equal("ACTI", active.V("Estado"));
        Assert.Equal("BTBJ+ABIE", active.V("CodAlarma"));
        Assert.StartsWith("2026-10-01T11:30:00", active.V("FUltEvento"));

        // The depositario asks to deactivate through a channel ArcaSim has no service for: its document changes state.
        var store = kit.Sim.Services.GetRequiredService<IDocumentStore>();
        var precinto = (await store.GetAsync<Cema>(WgesPrecintosRules.Collection, "CEMA01"))!;
        await store.PutAsync(WgesPrecintosRules.Collection, "CEMA01", precinto with { Estado = "SODE", FUltEstado = AduanaKit.Today });
        Assert.Equal("SODE", (await cema.CallAsync("ConsultarPrecintosPendientes", "")).V("Estado"));
        Assert.Equal("0", (await cema.CallAsync("TerminarMonitoreo", $"<argTerminarMonitoreo>{Ids("CEMA01")}</argTerminarMonitoreo>")).Code());
        Assert.Equal("0", (await cema.CallAsync("NovedadPrecinto", "<argPrecinto><IdPrecinto>CEMA01</IdPrecinto></argPrecinto>")).Code());

        var padron = await cema.CallAsync("ConsultaCemaPadron", "<argConsulta><IdPrecinto>CEMA01</IdPrecinto></argConsulta>");
        Assert.Equal("BAJA", padron.V("EstadoPrecinto"));
        Assert.Equal("ACEP", padron.V("EstadoAcepDepo"));
        Assert.Equal("10121", (await cema.CallAsync("ConsultarPrecintosPendientes", "")).Code());
    }

    [Fact]
    public async Task A_batch_with_one_bad_precinto_is_refused_whole_naming_it()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cema = await CemaAsync(kit);
        await cema.CallAsync("NovedadPrecinto", Alta("CEMA02"));
        await cema.CallAsync("NovedadPrecinto", Alta("CEMA03"));

        var unknown = await cema.CallAsync("IniciarMonitoreo", $"<argIniciarMonitoreo>{Ids("CEMA02", "CEMA99")}</argIniciarMonitoreo>");
        var twice = await cema.CallAsync("IniciarMonitoreo", $"<argIniciarMonitoreo>{Ids("CEMA02", "CEMA02")}</argIniciarMonitoreo>");
        var notActive = await cema.CallAsync("InformarEstadoPrecintos", Event("CEMA03", "MONI"));
        var notRequested = await cema.CallAsync("TerminarMonitoreo", $"<argTerminarMonitoreo>{Ids("CEMA03")}</argTerminarMonitoreo>");
        var still = await cema.CallAsync("ConsultarPrecintos", "<argConsultaPrecintos><Estado>SOAC</Estado></argConsultaPrecintos>");

        Assert.Equal("12404", unknown.Code());
        Assert.Equal("CEMA99", unknown.V("DescAdicErr"));
        Assert.Equal("30839", twice.Code());
        Assert.Equal("30840", notActive.Code());
        Assert.Equal("El dispositivo no se encuentra en estado ACTI", notActive.V("DesError"));
        Assert.Equal("30840", notRequested.Code());
        Assert.Equal(2, still.All("Precinto").Count());
    }

    [Fact]
    public async Task An_active_precinto_cannot_leave_its_door_and_unknown_alarms_are_refused()
    {
        await using var kit = await AduanaKit.StartAsync();
        var cema = await CemaAsync(kit);
        await cema.CallAsync("NovedadPrecinto", Alta("CEMA04"));
        await cema.CallAsync("IniciarMonitoreo", $"<argIniciarMonitoreo>{Ids("CEMA04")}</argIniciarMonitoreo>");

        var baja = await cema.CallAsync("NovedadPrecinto", "<argPrecinto><IdPrecinto>CEMA04</IdPrecinto></argPrecinto>");
        var again = await cema.CallAsync("NovedadPrecinto", Alta("CEMA04"));
        var alarm = await cema.CallAsync("InformarEstadoPrecintos", Event("CEMA04", "MONI+XXXX"));
        var nothing = await cema.CallAsync("ConsultarPrecintos", "<argConsultaPrecintos />");

        Assert.Equal("30850", baja.Code());
        Assert.Equal("Puerta Deposito con dispositivo CEMA04 asignado en estado ACTI", baja.V("DesError"));
        Assert.Equal("30846", again.Code());
        Assert.Equal("30841", alarm.Code());
        Assert.Equal("30842", nothing.Code());
    }
}
