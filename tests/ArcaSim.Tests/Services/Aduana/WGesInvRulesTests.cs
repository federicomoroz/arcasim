namespace ArcaSim.Tests.Services.Aduana;

/// <summary>WGesINV: the INV finds pending wine despachos, approves or denies them, and they stop being pending.</summary>
public class WGesInvRulesTests
{
    private static Task<AduanaService> InvAsync(AduanaKit kit) =>
        kit.ServiceAsync("WGesINV", (t, c) => AduanaKit.ArgAutentica(t, c));

    private static string Approve(string id, string aduana = "001") =>
        $"<argAprobarDespacho><Aduana>{aduana}</Aduana><IdDestinacion>{id}</IdDestinacion><IdAutorizacionINV>77</IdAutorizacionINV><IdUsuarioDesbloqueo>INV01</IdUsuarioDesbloqueo></argAprobarDespacho>";

    private static string Deny(string id) =>
        $"<argDenegarDespacho><Aduana>001</Aduana><IdDestinacion>{id}</IdDestinacion><IdUsuarioDenegacion>INV01</IdUsuarioDenegacion><MotivoDenegacion>Analisis pendiente</MotivoDenegacion></argDenegarDespacho>";

    [Fact]
    public async Task Pending_despachos_are_approved_with_a_sequence_and_stop_being_pending()
    {
        await using var kit = await AduanaKit.StartAsync();
        var inv = await InvAsync(kit);

        var pending = await inv.CallAsync("ConsultaDespachosPendientes", "<argIdTransaccion>0</argIdTransaccion>");
        var ids = pending.All("Oficializacion").Select(o => o.V("IdDestinacion")).ToList();
        var approved = await inv.CallAsync("AprobarDespacho", Approve(ids[0]));
        var transaction = await inv.CallAsync("ConsultaIdTransaccionDespacho", $"<argIdDespacho>{ids[0]}</argIdDespacho>");
        var after = await inv.CallAsync("ConsultaDespachosPendientes", "<argIdTransaccion>0</argIdTransaccion>");

        Assert.Equal("20304", pending.Code());
        Assert.Equal(3, ids.Count);
        Assert.Equal("3", pending.V("CantidadTotal"));
        Assert.All(ids, id => Assert.Equal(16, id.Length));
        Assert.Equal("20304", approved.Code());
        Assert.Equal("1", approved.V("NroSecuencia"));
        Assert.Equal(pending.All("Oficializacion").First().V("InvTransacExpo"), transaction.V("IdTransaccion"));
        Assert.Equal("2", after.V("CantidadOficializaciones"));
        Assert.DoesNotContain(after.All("IdDestinacion"), e => e.Value == ids[0]);
    }

    [Fact]
    public async Task A_resolved_despacho_answers_the_manuals_codes()
    {
        await using var kit = await AduanaKit.StartAsync();
        var inv = await InvAsync(kit);
        var ids = (await inv.CallAsync("ConsultaDespachosPendientes", "<argIdTransaccion>0</argIdTransaccion>"))
            .All("Oficializacion").Select(o => o.V("IdDestinacion")).ToList();
        await inv.CallAsync("AprobarDespacho", Approve(ids[0]));
        await inv.CallAsync("DenegarDespacho", Deny(ids[1]));

        var denyApproved = await inv.CallAsync("DenegarDespacho", Deny(ids[0]));
        var denyTwice = await inv.CallAsync("DenegarDespacho", Deny(ids[1]));
        var approveDenied = await inv.CallAsync("AprobarDespacho", Approve(ids[1]));
        var unknown = await inv.CallAsync("AprobarDespacho", Approve("26001EC01999999Z"));
        var badCustoms = await inv.CallAsync("AprobarDespacho", Approve(ids[2], "1"));

        Assert.Equal("30687", denyApproved.Code());
        Assert.Equal($"Desbloqueo ya registrado {ids[0]}", denyApproved.V("DesError"));
        Assert.Equal("30688", denyTwice.Code());
        Assert.Equal("30330", approveDenied.Code());
        Assert.Equal("0", approveDenied.V("NroSecuencia"));
        Assert.Equal("20150", unknown.Code());
        Assert.Equal("10015", badCustoms.Code());
    }

    [Fact]
    public async Task The_transaction_cursor_leaves_out_what_was_already_seen()
    {
        await using var kit = await AduanaKit.StartAsync();
        var inv = await InvAsync(kit);
        var first = await inv.CallAsync("ConsultaDespachosPendientes", "<argIdTransaccion>0</argIdTransaccion>");
        var last = first.All("InvTransacExpo").Max(t => long.Parse(t.Value));

        var none = await inv.CallAsync("ConsultaDespachosPendientes", $"<argIdTransaccion>{last}</argIdTransaccion>");

        Assert.Equal("10121", none.Code());
        Assert.Equal("0", none.V("CantidadTotal"));
    }

    [Fact]
    public async Task A_VUCEA_form_is_approved_once_and_a_state_other_than_A_or_R_is_invalid()
    {
        await using var kit = await AduanaKit.StartAsync();
        var inv = await InvAsync(kit);
        var forms = await inv.CallAsync("ConsultaVUCEAPendientes", "<argIdTransaccion>0</argIdTransaccion>");
        var form = forms.All("FormularioVUCEA").Single();
        string Assign(string state) =>
            $"<argAsignarEstadoVUCEA><NroTramite>{form.V("NroTramite")}</NroTramite><IdTransaccionTramite>{form.V("IdTransaccionTramite")}</IdTransaccionTramite>" +
            $"<IdDestinacion>{form.V("IdDestinacion")}</IdDestinacion><Estado>{state}</Estado></argAsignarEstadoVUCEA>";

        var invalid = await inv.CallAsync("AsignarEstadoVUCEA", Assign("X"));
        var approved = await inv.CallAsync("AsignarEstadoVUCEA", Assign("A"));
        var after = await inv.CallAsync("ConsultaVUCEAPendientes", "<argIdTransaccion>0</argIdTransaccion>");

        Assert.Equal("1", forms.V("CantidadFormulariosVUCEA"));
        Assert.Equal("411", invalid.Code());
        Assert.Equal("20304", approved.Code());
        Assert.Equal("10121", after.Code());
    }
}
