using System.Xml.Linq;
using ArcaSim.Application.Wsfe;
using Microsoft.Extensions.DependencyInjection;
using static ArcaSim.Tests.Services.Fce.FceWorld;

namespace ArcaSim.Tests.Services.Fce;

/// <summary>wsfecred over the FCE vouchers WSFEv1 authorizes: the current account, the buyer's decisions and the queries, valid for its WSDL.</summary>
public class WsfecredRulesTests
{
    private static string Accept(long invoice, decimal balance, string extra = "", string notes = "") =>
        Account(invoice) + notes + extra + $"<saldoAceptado>{balance:0.00}</saldoAceptado><codMoneda>PES</codMoneda><cotizacionMonedaUlt>1</cotizacionMonedaUlt>";

    private static string Reason(int code, string justification) =>
        $"<arrayMotivosRechazo><motivoRechazo><codMotivo>{code}</codMotivo><descMotivo>Motivo</descMotivo><justificacion>{justification}</justificacion></motivoRechazo></arrayMotivosRechazo>";

    [Fact]
    public async Task An_invoice_from_WSFEv1_opens_an_account_the_buyer_accepts_and_both_see_its_history()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();

        var issued = await world.FecredAsync(Seller, "consultarComprobantes", "<rolCUITRepresentada>Emisor</rolCUITRepresentada>");
        var voucher = issued.Element("arrayComprobantes")!.Element("comprobante")!;
        Assert.Equal("201", voucher.Element("codTipoCmp")!.Value);
        Assert.Equal(invoice.ToString(), voucher.Element("nroCmp")!.Value);
        Assert.Equal("COMERCIAL DEL NORTE SA", voucher.Element("razonSocialRecep")!.Value);
        Assert.Equal("6050000.00", voucher.Element("importeTotal")!.Value);
        Assert.Equal("2026-10-31", voucher.Element("fechaVenAcep")!.Value);
        Assert.Equal("PendienteRecepcion", voucher.Element("estado")!.Element("estado")!.Value);
        Assert.Equal("ADC", voucher.Element("opcionTransferencia")!.Value);

        var early = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 6_050_000m));
        Assert.Equal(["1106"], Codes(early.Element("arrayErrores")));

        world.UntilOperable();
        var accepted = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 6_050_000m));
        Assert.Equal("A", accepted.Element("resultado")!.Value);
        Assert.Null(accepted.Element("arrayErrores"));

        var account = (await world.FecredAsync(Seller, "consultarCtaCte", Account(invoice))).Element("ctaCte")!;
        Assert.Equal("Aceptada", account.Element("estadoCtaCte")!.Element("estado")!.Value);
        Assert.Equal("6050000.00", account.Element("saldoAceptado")!.Value);
        Assert.Equal("Expresa", account.Element("factura")!.Element("tipoAcep")!.Value);

        var history = await world.FecredAsync(Buyer, "consultarHistorialEstadosCtaCte", Account(invoice));
        Assert.Equal(["Modificable", "Aceptada"], history.Element("arrayHistorialEstados")!.Elements().Select(e => e.Element("estado")!.Value));
        var vouchers = await world.FecredAsync(Seller, "consultarHistorialEstadosComprobante", Voucher("idComprobante", 201, invoice));
        Assert.Equal(["PendienteRecepcion", "Recepcionado", "Aceptado"],
            vouchers.Element("arrayHistorialEstados")!.Elements().Select(e => e.Element("estado")!.Value));
    }

    [Fact]
    public async Task An_invoice_older_than_a_hundred_thousand_other_vouchers_still_opens_its_account()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        var vouchers = world.Sim.Services.GetRequiredService<IVoucherStore>();
        var detail = new FECAEDetRequest();
        var later = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        for (var number = 1; number <= 100_001; number++)
            await vouchers.AddAsync(new StoredVoucher(
                Seller, 9, 6, number, number, new DateOnly(2026, 10, 2), EmissionType.Cae, "70000000000000", new DateOnly(2026, 10, 12), later, detail, []));

        var issued = await world.FecredAsync(Seller, "consultarComprobantes", "<rolCUITRepresentada>Emisor</rolCUITRepresentada>");

        Assert.Equal([invoice.ToString()], issued.Element("arrayComprobantes")!.Elements("comprobante").Select(v => v.Element("nroCmp")!.Value));
    }

    [Fact]
    public async Task A_balance_that_does_not_add_up_is_refused_with_12002_and_the_seller_may_not_accept()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        world.UntilOperable();

        var wrong = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 6_000_000m));
        var error = wrong.Element("arrayErrores")!.Element("codigoDescripcion")!;
        Assert.Equal("R", wrong.Element("resultado")!.Value);
        Assert.Equal("12002", error.Element("codigo")!.Value);
        Assert.Equal("El saldo aceptado que informa no coincide por el calculado por nuestros registros, verifique sus cuentas.", error.Element("descripcion")!.Value);

        var seller = await world.FecredAsync(Seller, "aceptarFECred", Accept(invoice, 6_050_000m));
        Assert.Equal(["1101"], Codes(seller.Element("arrayErrores")));

        var unknown = await world.FecredAsync(Buyer, "consultarCtaCte", "<idCtaCte><codCtaCte>999</codCtaCte></idCtaCte>");
        Assert.Equal(["1102"], Codes(unknown.Element("arrayErrores")));
    }

    [Fact]
    public async Task The_buyer_rejects_with_reasons_once_and_the_account_stays_rejected()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        world.UntilOperable();

        var unjustified = await world.FecredAsync(Buyer, "rechazarFECred", Account(invoice) + Reason(1, ""));
        Assert.Contains("3000", Codes(unjustified.Element("arrayErrores")));

        var rejected = await world.FecredAsync(Buyer, "rechazarFECred", Account(invoice) + Reason(1, "No llegó la mercadería"));
        Assert.Equal("A", rejected.Element("resultado")!.Value);
        var again = await world.FecredAsync(Buyer, "rechazarFECred", Account(invoice) + Reason(2, "Otra vez"));
        Assert.Equal(["1108"], Codes(again.Element("arrayErrores")));

        var factura = (await world.FecredAsync(Seller, "consultarCtaCte", Account(invoice))).Element("ctaCte")!.Element("factura")!;
        Assert.Equal("Rechazado", factura.Element("estado")!.Element("estado")!.Value);
        Assert.Equal("1", factura.Element("arrayMotivosRechazo")!.Element("motivoRechazo")!.Element("codMotivo")!.Value);
        var listed = await world.FecredAsync(Buyer, "consultarCtasCtes", "<rolCUITRepresentada>Receptor</rolCUITRepresentada><estadoCtaCte>Rechazada</estadoCtaCte>");
        Assert.Single(listed.Element("arrayInfosCtaCte")!.Elements());
    }

    [Fact]
    public async Task Notes_move_the_balance_until_rejected_and_must_be_confirmed_when_accepting()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        var debit = await world.NoteAsync(202, invoice, 100_000m);
        var credit = await world.NoteAsync(203, invoice, 50_000m);
        world.UntilOperable();

        var account = (await world.FecredAsync(Buyer, "consultarCtaCte", Account(invoice))).Element("ctaCte")!;
        Assert.Equal(2, account.Element("arrayNotasDCAsociadas")!.Elements().Count());
        Assert.Equal("6110500.00", account.Element("saldo")!.Value);

        var rejectNote = await world.FecredAsync(Buyer, "rechazarNotaDC", Voucher("idComprobante", 202, debit) + Reason(3, "Precio no pactado"));
        Assert.Equal("A", rejectNote.Element("resultado")!.Value);
        var balance = (await world.FecredAsync(Buyer, "consultarCtaCte", Account(invoice))).Element("ctaCte")!.Element("saldo")!.Value;
        Assert.Equal("5989500.00", balance);

        var forgotten = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 5_989_500m));
        Assert.Contains("12003", Codes(forgotten.Element("arrayErrores")));

        var notes = "<arrayConfirmarNotasDC><confirmarNota><acepta>S</acepta>" + Voucher("idNota", 203, credit) + "</confirmarNota></arrayConfirmarNotasDC>";
        var accepted = await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 5_989_500m, notes: notes));
        Assert.Equal("A", accepted.Element("resultado")!.Value);
    }

    [Fact]
    public async Task A_total_cancellation_at_acceptance_leaves_nothing_to_negotiate()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        world.UntilOperable();
        const string forms = "<arrayFormasCancelacion><codigoDescripcion><codigo>1</codigo><descripcion>Transferencia bancaria</descripcion></codigoDescripcion></arrayFormasCancelacion>";

        var partialAmount = await world.FecredAsync(Buyer, "aceptarFECred",
            Accept(invoice, 0m, forms + "<tipoCancelacion>TOT</tipoCancelacion><importeCancelado>1000.00</importeCancelado>"));
        Assert.Contains("4000", Codes(partialAmount.Element("arrayErrores")));

        var cancelled = await world.FecredAsync(Buyer, "aceptarFECred",
            Accept(invoice, 0m, forms + "<tipoCancelacion>TOT</tipoCancelacion><importeCancelado>6050000.00</importeCancelado>"));
        Assert.Equal("A", cancelled.Element("resultado")!.Value);

        var account = (await world.FecredAsync(Seller, "consultarCtaCte", Account(invoice))).Element("ctaCte")!;
        Assert.Equal("CanceladaTotal", account.Element("estadoCtaCte")!.Element("estado")!.Value);
        Assert.Equal("0.00", account.Element("saldoAceptado")!.Value);
        var report = await world.FecredAsync(Seller, "informarFacturaAgtDptoCltv",
            Account(invoice) + $"<ctaAgente><cuitAgente>{Agent}</cuitAgente><idCuenta>0001234</idCuenta></ctaAgente>");
        Assert.Equal(["1108"], Codes(report.Element("arrayErrores")));
    }

    [Fact]
    public async Task An_accepted_account_is_cancelled_later_with_informarCancelacionTotalFECred()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();
        world.UntilOperable();
        await world.FecredAsync(Buyer, "aceptarFECred", Accept(invoice, 6_050_000m));

        var cancel = await world.FecredAsync(Buyer, "informarCancelacionTotalFECred", Account(invoice) +
            "<arrayFormasCancelacion><codigoDescripcion><codigo>2</codigo><descripcion>Cheque</descripcion></codigoDescripcion></arrayFormasCancelacion>" +
            "<importeCancelacion>6050000.00</importeCancelacion>");

        Assert.Equal("A", cancel.Element("resultado")!.Value);
        var history = await world.FecredAsync(Seller, "consultarHistorialEstadosCtaCte", Account(invoice));
        Assert.Equal(["Modificable", "Aceptada", "CanceladaTotal"], history.Element("arrayHistorialEstados")!.Elements().Select(e => e.Element("estado")!.Value));
    }

    [Fact]
    public async Task An_account_nobody_decided_on_is_accepted_tacitly_after_its_deadline()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();

        world.Advance(TimeSpan.FromDays(31));
        var account = (await world.FecredAsync(Buyer, "consultarCtaCte", Account(invoice))).Element("ctaCte")!;
        Assert.Equal("Aceptada", account.Element("estadoCtaCte")!.Element("estado")!.Value);
        Assert.Equal("Tacita", account.Element("factura")!.Element("tipoAcep")!.Value);
        Assert.Equal("6050000.00", account.Element("saldoAceptado")!.Value);

        var late = await world.FecredAsync(Buyer, "rechazarFECred", Account(invoice) + Reason(1, "Tarde"));
        Assert.Equal(["1107"], Codes(late.Element("arrayErrores")));
    }

    [Fact]
    public async Task The_parameter_tables_and_the_obligation_answer_from_their_tables()
    {
        await using var world = await StartAsync();

        var reasons = await world.FecredAsync(Buyer, "consultarTiposMotivosRechazo");
        Assert.Equal(5, reasons.Element("arrayCodigoDescripcion")!.Elements().Count());
        var withholdings = await world.FecredAsync(Buyer, "consultarTiposRetenciones");
        Assert.NotEmpty(withholdings.Element("arrayTiposRetenciones")!.Elements());

        var obliged = await world.FecredAsync(Seller, "consultarMontoObligadoRecepcion", $"<cuitConsultada>{Buyer}</cuitConsultada><fechaEmision>2026-10-01</fechaEmision>");
        Assert.Equal("S", obliged.Element("obligado")!.Value);
        Assert.Equal("5549862.00", obliged.Element("montoDesde")!.Value);

        var none = await world.FecredAsync(Seller, "consultarComprobantes", "<rolCUITRepresentada>Emisor</rolCUITRepresentada>");
        Assert.Equal(["32767"], Codes(none.Element("arrayObservaciones")));
        Assert.Equal("N", none.Element("hayMas")!.Value);
    }

    [Fact]
    public async Task The_seller_changes_the_transfer_option_while_the_account_is_modifiable()
    {
        await using var world = await StartAsync();
        var invoice = await world.InvoiceAsync();

        var changed = await world.FecredAsync(Seller, "modificarOpcionTransferencia", Account(invoice) + "<opcionTransferencia>SCA</opcionTransferencia>");
        Assert.Equal("A", changed.Element("resultado")!.Value);
        var same = await world.FecredAsync(Seller, "modificarOpcionTransferencia", Account(invoice) + "<opcionTransferencia>SCA</opcionTransferencia>");
        Assert.Equal(["7000"], Codes(same.Element("arrayErrores")));
        var buyer = await world.FecredAsync(Buyer, "modificarOpcionTransferencia", Account(invoice) + "<opcionTransferencia>ADC</opcionTransferencia>");
        Assert.Equal(["1101"], Codes(buyer.Element("arrayErrores")));

        var info = (await world.FecredAsync(Buyer, "consultarCtasCtes", "<rolCUITRepresentada>Receptor</rolCUITRepresentada>"))
            .Element("arrayInfosCtaCte")!.Element("infoCtaCte")!;
        Assert.Equal("SCA", info.Element("opcionTransferencia")!.Value);
    }
}
