using System.Xml.Linq;
using static ArcaSim.Tests.Services.Fce.FceWorld;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>wsfecredsca on top of wsfecred: invoices accepted with the SCA option, read and confirmed by the SCA, valid for its WSDL.</summary>
public class WsfecredscaRulesTests
{
    private static string Query(string? state = null, string kind = "Disponible") =>
        (state is null ? "" : $"<estadoFactura>{state}</estadoFactura>") +
        $"<nroPagina>1</nroPagina><filtroFechas><tipo>{kind}</tipo><desde>2026-10-01</desde><hasta>2026-12-31</hasta></filtroFechas>";

    private static string Confirm(params long[] invoices) =>
        "<facturas>" + string.Concat(invoices.Select(i => $"<factura>{IdFactura(i)}</factura>")) + "</facturas>";

    private static async Task<long> ScaInvoiceAsync(FceWorld world)
    {
        var invoice = await world.InvoiceAsync();
        var changed = await world.FecredAsync(Seller, "modificarOpcionTransferencia", Account(invoice) + "<opcionTransferencia>SCA</opcionTransferencia>");
        Assert.Equal("A", changed.Element("resultado")!.Value);
        world.UntilOperable();
        return invoice;
    }

    private static string Accept(long invoice, string cbu) =>
        Account(invoice) + "<saldoAceptado>6050000.00</saldoAceptado><codMoneda>PES</codMoneda><cotizacionMonedaUlt>1</cotizacionMonedaUlt>" + cbu;

    [Fact]
    public async Task An_invoice_accepted_with_SCA_is_read_then_received_and_wsfecred_shows_the_reading()
    {
        await using var world = await StartAsync();
        var invoice = await ScaInvoiceAsync(world);
        var accepted = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, $"<informaCBU>S</informaCBU><CBUComprador>{BuyerCbu}</CBUComprador>"));
        Assert.Equal("A", accepted.Element("resultado")!.Value);

        var read = await world.ScaAsync("consultarFacturasAceptadas", Query("D"));
        var factura = read.Element("facturas")!.Element("factura")!;
        Assert.Equal(invoice.ToString(), factura.Element("idFactura")!.Element("nroCmp")!.Value);
        Assert.Equal("E", factura.Element("tipoAceptacion")!.Value);
        Assert.Equal("EMPRESA DE PRUEBA SA", factura.Element("razonSocialEmisor")!.Value);
        Assert.Equal("S", factura.Element("informaCbuComprador")!.Value);
        Assert.Equal(BuyerCbu, factura.Element("cbuComprador")!.Value);
        Assert.Equal("6050000.00", factura.Element("saldoAceptado")!.Value);
        Assert.Equal("P", factura.Element("estadoFactura")!.Value);
        Assert.Equal("1", read.Element("nroPagina")!.Value);

        var sca = (await world.FecredAsync(Seller, "consultarCtaCte", Account(invoice)))
            .Element("ctaCte")!.Element("factura")!.Element("infoTransferencia")!.Element("infoSCA")!;
        Assert.Equal("S", sca.Element("CBUValidada")!.Value);
        Assert.NotNull(sca.Element("fechaLecturaSCA"));

        Assert.Empty((await world.ScaAsync("consultarFacturasAceptadas", Query("D"))).Element("facturas")!.Elements());
        var confirmed = await world.ScaAsync("confirmarRecepcionFacturas", Confirm(invoice));
        Assert.Equal("A", confirmed.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);

        var received = await world.ScaAsync("consultarFacturasAceptadas", Query("R", "Recibida"));
        Assert.Equal("R", received.Element("facturas")!.Element("factura")!.Element("estadoFactura")!.Value);
        Assert.NotNull(received.Element("facturas")!.Element("factura")!.Element("fechaHoraConfirmacionSCA"));
    }

    [Fact]
    public async Task Confirming_an_unread_unknown_or_received_invoice_is_refused_per_item()
    {
        await using var world = await StartAsync();
        var invoice = await ScaInvoiceAsync(world);
        await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, "<informaCBU>N</informaCBU>"));

        var unread = await world.ScaAsync("confirmarRecepcionFacturas", Confirm(invoice, 99));
        var items = unread.Element("resultados")!.Elements().ToList();
        Assert.Equal(["4014"], Codes(items[0].Element("errores")));
        var unknown = items[1].Element("errores")!.Element("codigoDescripcion")!;
        Assert.Equal("4012", unknown.Element("codigo")!.Value);
        Assert.Equal("No se encontró ninguna Factura al SCA con los Datos del Comprobante Enviados que se encuentre Pendiente de Confirmar.", unknown.Element("descripcion")!.Value);

        await world.ScaAsync("consultarFacturasAceptadas", Query());
        await world.ScaAsync("confirmarRecepcionFacturas", Confirm(invoice));
        var twice = await world.ScaAsync("confirmarRecepcionFacturas", Confirm(invoice));
        Assert.Equal(["4013"], Codes(twice.Element("resultados")!.Element("resultado")!.Element("errores")));
    }

    [Fact]
    public async Task An_invoice_id_the_schema_cannot_hold_is_not_the_invoice_it_wraps_to()
    {
        await using var world = await StartAsync();
        var invoice = await ScaInvoiceAsync(world);
        await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, "<informaCBU>N</informaCBU>"));
        await world.ScaAsync("consultarFacturasAceptadas", Query("D"));

        // 4294967297 is 1 once cut to 32 bits: the invoice's point of sale.
        var wrapped = $"<facturas><factura><idFactura><cuitEmisor>{Seller}</cuitEmisor><tipoCmp>201</tipoCmp><ptoVta>4294967297</ptoVta><nroCmp>{invoice}</nroCmp></idFactura></factura></facturas>";
        var refused = await world.ScaAsync("confirmarRecepcionFacturas", wrapped);
        var received = await world.ScaAsync("confirmarRecepcionFacturas", Confirm(invoice));

        Assert.Equal(["2002"], Codes(refused.Element("erroresFormato")));
        Assert.Empty(refused.Element("resultados")!.Elements());
        Assert.Equal("A", received.Element("resultados")!.Element("resultado")!.Element("resultado")!.Value);
    }

    [Fact]
    public async Task The_SCA_option_needs_the_buyer_to_say_whether_it_informs_a_CBU()
    {
        await using var world = await StartAsync();
        var invoice = await ScaInvoiceAsync(world);

        var missing = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, ""));
        Assert.Equal(["12014"], Codes(missing.Element("arrayErrores")));
        Assert.Empty((await world.ScaAsync("consultarFacturasAceptadas", Query())).Element("facturas")!.Elements());

        var cancel = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, "<informaCBU>N</informaCBU>"));
        Assert.Equal("A", cancel.Element("resultado")!.Value);
        var later = await world.FecredAsync(Buyer, "informarCancelacionTotalFECred", Account(invoice) +
            "<arrayFormasCancelacion><codigoDescripcion><codigo>2</codigo><descripcion>Cheque</descripcion></codigoDescripcion></arrayFormasCancelacion>" +
            "<importeCancelacion>6050000.00</importeCancelacion>");
        Assert.Equal(["1108"], Codes(later.Element("arrayErrores")));
    }

    [Fact]
    public async Task A_tacitly_accepted_SCA_invoice_reaches_the_SCA_without_a_buyer_CBU()
    {
        await using var world = await StartAsync();
        var invoice = await ScaInvoiceAsync(world);

        world.Advance(TimeSpan.FromDays(30));
        var read = await world.ScaAsync("consultarFacturasAceptadas", Query());

        var factura = read.Element("facturas")!.Element("factura")!;
        Assert.Equal("T", factura.Element("tipoAceptacion")!.Value);
        Assert.Equal("N", factura.Element("informaCbuComprador")!.Value);
        Assert.Null(factura.Element("cbuComprador"));
        Assert.Equal("2026-11-01T00:00:00", factura.Element("fechaHoraDisponible")!.Value);
        Assert.Equal(invoice.ToString(), factura.Element("idFactura")!.Element("nroCmp")!.Value);
    }

    [Fact]
    public async Task A_CUIT_without_its_check_digit_is_a_format_error()
    {
        await using var world = await StartAsync();

        var answer = await world.ScaAsync("consultarFacturasAceptadas", "<nroPagina>1</nroPagina><cuitEmisor>20111111111</cuitEmisor>" +
            "<filtroFechas><tipo>Disponible</tipo><desde>2026-10-01</desde><hasta>2026-10-31</hasta></filtroFechas>");

        var error = answer.Element("erroresFormato")!.Element("codigoDescripcionString")!;
        Assert.Equal("2002", error.Element("codigo")!.Value);
        Assert.Equal("Formato CUIT, CUIL o CDI inválido", error.Element("descripcion")!.Value);
        Assert.Equal("0", answer.Element("nroPagina")!.Value);
    }
}
