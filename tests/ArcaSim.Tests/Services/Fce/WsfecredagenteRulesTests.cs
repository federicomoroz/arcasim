using System.Xml.Linq;
using static ArcaSim.Tests.Services.Fce.FceWorld;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>wsfecredagente on top of wsfecred: the agent's accounts, the invoices sellers report to them, and the agent's answer, valid for its WSDL.</summary>
public class WsfecredagenteRulesTests
{
    private const string Range = "<filtroFechas><tipo>Disponible</tipo><desde>2026-10-01</desde><hasta>2026-12-31</hasta></filtroFechas>";

    private static string OpenAccount(long holder = Seller, string id = "0001234") =>
        $"<cuentas><cuenta><cuitTitular>{holder}</cuitTitular><cuentaId>{id}</cuentaId><denominacion>CUENTA COMITENTE</denominacion></cuenta></cuentas>";

    private static string ToAgent(string id = "0001234") => $"<ctaAgente><cuitAgente>{Agent}</cuitAgente><idCuenta>{id}</idCuenta></ctaAgente>";

    private static string Confirm(long invoice, bool accepts, int? reason = null) =>
        $"<facturas><factura>{IdFactura(invoice)}<aceptada>{(accepts ? "S" : "N")}</aceptada>{(reason is { } r ? $"<codRechazo>{r}</codRechazo>" : "")}</factura></facturas>";

    /// <summary>An invoice the buyer accepted in full and an agent account for the seller: what a report needs.</summary>
    private static async Task<long> AcceptedInvoiceAsync(FceWorld world)
    {
        var opened = await world.AgenteAsync("altaCuentasAgente", OpenAccount());
        Assert.Equal("A", opened.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);
        var invoice = await world.InvoiceAsync();
        world.UntilOperable();
        var accepted = await world.FecredAsync(Buyer, "aceptarFECred",
            Account(invoice) + "<saldoAceptado>6050000.00</saldoAceptado><codMoneda>PES</codMoneda><cotizacionMonedaUlt>1</cotizacionMonedaUlt>");
        Assert.Equal("A", accepted.Element("resultado")!.Value);
        return invoice;
    }

    [Fact]
    public async Task A_reported_invoice_goes_from_available_to_pending_to_accepted_and_the_seller_sees_the_receipt()
    {
        await using var world = await StartAsync();
        var invoice = await AcceptedInvoiceAsync(world);

        var accounts = await world.FecredAsync(Seller, "consultarCuentasEnAgtDptoCltv");
        var account = accounts.Element("arrayCuentasEnAgente")!.Element("cuentaEnAgente")!;
        Assert.Equal(Agent.ToString(), account.Element("cuitAgente")!.Value);
        Assert.Equal("CAJA DE VALORES SA", account.Element("razonSocialAgente")!.Value);

        var reported = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        Assert.Equal("A", reported.Element("resultado")!.Value);
        var waiting = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        Assert.Equal(["6007"], Codes(waiting.Element("arrayErrores")));

        var read = await world.AgenteAsync("consultarFacturasInformadas", $"<estadoInforme>D</estadoInforme><nroPagina>1</nroPagina>{Range}");
        var factura = read.Element("facturasInformadas")!.Element("facturaInformada")!;
        Assert.Equal(invoice.ToString(), factura.Element("idFactura")!.Element("nroCmp")!.Value);
        Assert.Equal(Buyer.ToString(), factura.Element("cuitComprador")!.Value);
        Assert.Equal("6050000.00", factura.Element("saldoNegociable")!.Value);
        Assert.Equal("2026-11-30", factura.Element("fechaVencimientoPago")!.Value);
        Assert.Equal("P", factura.Element("estadoInforme")!.Value);
        Assert.NotNull(factura.Element("fechaHoraLecturaAgente"));

        var available = await world.AgenteAsync("consultarFacturasInformadas", $"<estadoInforme>D</estadoInforme><nroPagina>1</nroPagina>{Range}");
        Assert.Empty(available.Element("facturasInformadas")!.Elements());
        var pending = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        Assert.Equal(["6001"], Codes(pending.Element("arrayErrores")));

        var confirmed = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: true));
        Assert.Equal("A", confirmed.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);

        var info = (await world.FecredAsync(Seller, "consultarFacturasAgtDptoCltv", Account(invoice)))
            .Element("arrayFacturasAgtDptoCltv")!.Element("facturaInformada")!.Element("infoAgtDptoCltv")!;
        Assert.Equal("S", info.Element("recibida")!.Value);
        Assert.Equal("S", info.Element("aceptada")!.Value);
        Assert.NotNull(info.Element("fechaLectura"));
        var state = (await world.FecredAsync(Seller, "consultarCtaCte", Account(invoice))).Element("ctaCte")!.Element("estadoCtaCte")!;
        Assert.Equal("InformadaAgDpto", state.Element("estado")!.Value);
        var done = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        Assert.Equal(["6000"], Codes(done.Element("arrayErrores")));
    }

    [Theory]
    [InlineData("4294967297")]
    [InlineData("40000")]
    public async Task A_page_number_the_schema_cannot_hold_is_a_format_error_not_a_page(string page)
    {
        await using var world = await StartAsync();
        await world.AgenteAsync("altaCuentasAgente", OpenAccount());

        // 4294967297 is page 1 once cut to 32 bits; 40000 does not fit the short the answer repeats it in.
        var answer = await world.AgenteAsync("consultarCuentasAgente", $"<nroPagina>{page}</nroPagina>{Range}");

        Assert.Equal(["2004"], Codes(answer.Element("erroresFormato")));
        Assert.Empty(answer.Element("cuentasAgente")!.Elements());
        Assert.Equal("0", answer.Element("nroPagina")!.Value);
    }

    [Fact]
    public async Task A_rejection_code_the_schema_cannot_hold_is_refused_not_read_as_the_code_it_wraps_to()
    {
        await using var world = await StartAsync();
        var invoice = await AcceptedInvoiceAsync(world);
        await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        await world.AgenteAsync("consultarFacturasInformadas", $"<estadoInforme>D</estadoInforme><nroPagina>1</nroPagina>{Range}");

        // 65537 is 1 once cut to 16 bits: one of the agent's reasons.
        var wrapped = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: false, reason: 65537));
        var right = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: false, reason: 1));

        Assert.Equal(["2006"], Codes(wrapped.Element("erroresFormato")));
        Assert.Empty(wrapped.Element("resultados")!.Elements());
        Assert.Equal("A", right.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);
    }

    [Fact]
    public async Task Confirming_before_reading_is_4021_and_twice_is_4020()
    {
        await using var world = await StartAsync();
        var invoice = await AcceptedInvoiceAsync(world);
        await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());

        var unread = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: true));
        var item = unread.Element("resultados")!.Element("resultado")!;
        Assert.Equal("R", item.Element("resultado")!.Value);
        var error = item.Element("errores")!.Element("codigoDescripcion")!;
        Assert.Equal("4021", error.Element("codigo")!.Value);
        Assert.Equal("La Solicitud de Informe debe ser Consultada por el Agente antes de ser enviada para Confirmar", error.Element("descripcion")!.Value);

        await world.AgenteAsync("consultarFacturasInformadas", $"<nroPagina>1</nroPagina>{Range}");
        await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: true));
        var twice = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: false, reason: 1));
        Assert.Equal(["4020"], Codes(twice.Element("resultados")!.Element("resultado")!.Element("errores")));

        var unknown = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(99, accepts: true));
        Assert.Equal(["4019"], Codes(unknown.Element("resultados")!.Element("resultado")!.Element("errores")));
    }

    [Fact]
    public async Task A_rejection_reaches_the_seller_with_its_reason_and_the_invoice_may_be_reported_again()
    {
        await using var world = await StartAsync();
        var invoice = await AcceptedInvoiceAsync(world);
        await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        await world.AgenteAsync("consultarFacturasInformadas", $"<nroPagina>1</nroPagina>{Range}");

        var withoutReason = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: false));
        Assert.Equal(["2006"], Codes(withoutReason.Element("erroresFormato")));
        Assert.Null(withoutReason.Element("errores"));

        var reasons = await world.AgenteAsync("obtenerMotivosRechazo");
        var first = reasons.Element("parametros")!.Element("parametrosTipoCodigosDescripciones")!.Elements().First();
        var rejected = await world.AgenteAsync("confirmarFacturasInformadas", Confirm(invoice, accepts: false, reason: int.Parse(first.Element("codigo")!.Value)));
        Assert.Equal("A", rejected.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);

        var info = (await world.FecredAsync(Seller, "consultarFacturasAgtDptoCltv"))
            .Element("arrayFacturasAgtDptoCltv")!.Element("facturaInformada")!.Element("infoAgtDptoCltv")!;
        Assert.Equal("N", info.Element("aceptada")!.Value);
        Assert.Equal(first.Element("descripcion")!.Value, info.Element("motivoRechazo")!.Value);

        var again = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv", Account(invoice) + ToAgent());
        Assert.Equal("A", again.Element("resultado")!.Value);
    }

    [Fact]
    public async Task Accounts_open_once_close_only_for_their_holder_and_list_by_date()
    {
        await using var world = await StartAsync();
        await world.AgenteAsync("altaCuentasAgente", OpenAccount());

        var duplicate = await world.AgenteAsync("altaCuentasAgente", OpenAccount());
        Assert.Equal(["4012"], Codes(duplicate.Element("resultados")!.Element("resultado")!.Element("errores")));
        var otherHolder = await world.AgenteAsync("bajaCuentasAgente", OpenAccount(holder: Buyer));
        Assert.Equal(["4015"], Codes(otherHolder.Element("resultados")!.Element("resultado")!.Element("errores")));
        var tooShort = await world.AgenteAsync("altaCuentasAgente", OpenAccount(id: "1"));
        Assert.Equal(["2005"], Codes(tooShort.Element("erroresFormato")));

        var active = await world.AgenteAsync("consultarCuentasAgente",
            "<estadoCuenta>A</estadoCuenta><nroPagina>1</nroPagina><filtroFechas><tipo>Alta</tipo><desde>2026-10-01</desde><hasta>2026-10-01</hasta></filtroFechas>");
        Assert.Equal(Seller.ToString(), active.Element("cuentasAgente")!.Element("cuenta")!.Element("cuitTitular")!.Value);

        var closed = await world.AgenteAsync("bajaCuentasAgente", OpenAccount());
        Assert.Equal("A", closed.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);
        var twice = await world.AgenteAsync("bajaCuentasAgente", OpenAccount());
        Assert.Equal(["4014"], Codes(twice.Element("resultados")!.Element("resultado")!.Element("errores")));

        var seller = await world.FecredAsync(Seller, "consultarCuentasEnAgtDptoCltv");
        Assert.Equal(["32767"], Codes(seller.Element("arrayObservacion")));
        var byClosing = await world.AgenteAsync("consultarCuentasAgente",
            "<nroPagina>1</nroPagina><filtroFechas><tipo>Baja</tipo><desde>2026-10-01</desde><hasta>2026-10-31</hasta></filtroFechas>");
        Assert.Single(byClosing.Element("cuentasAgente")!.Elements());
    }

    [Fact]
    public async Task A_bad_page_or_range_is_a_format_error_with_the_empty_answer()
    {
        await using var world = await StartAsync();

        var page = await world.AgenteAsync("consultarFacturasInformadas",
            "<nroPagina>0</nroPagina><filtroFechas><tipo>Disponible</tipo><desde>2026-10-31</desde><hasta>2026-10-01</hasta></filtroFechas>");

        Assert.Equal(["2004", "2003"], Codes(page.Element("erroresFormato")));
        Assert.Empty(page.Element("facturasInformadas")!.Elements());
        Assert.Equal("0", page.Element("nroPagina")!.Value);
        Assert.Equal("N", page.Element("hayMas")!.Value);
    }
}
