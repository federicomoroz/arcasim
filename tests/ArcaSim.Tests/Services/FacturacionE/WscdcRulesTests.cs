using System.Xml.Linq;
using Arca.Client;
using static ArcaSim.Tests.Services.FacturacionE.FacturacionESoap;

namespace ArcaSim.Tests.Services.FacturacionE;

/// <summary>Constatación of vouchers ArcaSim really issued, through WSFEv1 and WSFEXv1, every answer valid for the WSDL.</summary>
public class WscdcRulesTests
{
    /// <summary>A class B invoice to an unidentified final consumer: $1210 with 21 % VAT.</summary>
    private static readonly Voucher ConsumerInvoice = new()
    {
        Concept = 1,
        DocumentType = 99,
        DocumentNumber = 0,
        Total = 1210,
        Net = 1000,
        Vat = 210,
        ReceiverVatCondition = 5,
        VatLines = [new VatLine(5, 1000, 210)],
    };

    [Fact]
    public async Task A_voucher_issued_through_WSFEv1_is_approved_with_its_data_echoed()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);

        var result = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 6, issued.Number, "20261001", "1210", issued.Cae!));

        Assert.Equal("A", result.Value("Resultado"));
        Assert.Null(result.Child("Observaciones"));
        Assert.Null(result.Child("Errors"));
        Assert.Equal(issued.Cae, result.Value("CmpResp/CodAutorizacion"));
        Assert.Equal(issued.Number.ToString(), result.Value("CmpResp/CbteNro"));
        Assert.Equal("20261001120000", result.Value("FchProceso"));
    }

    [Fact]
    public async Task A_total_that_differs_from_the_issued_one_is_rejected_with_110_every_time()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);
        var query = Constatar("CAE", Issuer, 1, 6, issued.Number, "20261001", "1300", issued.Cae!);

        var first = await soap.CallAsync(Cdc, "ComprobanteConstatar", query);
        var again = await soap.CallAsync(Cdc, "ComprobanteConstatar", query);

        Assert.Equal("R", first.Value("Resultado"));
        Assert.Equal(["110"], Codes(first, "Observaciones"));
        Assert.Equal(first.Value("Observaciones/Obs/Msg"), again.Value("Observaciones/Obs/Msg"));
        Assert.Equal(["110"], Codes(again, "Observaciones"));
    }

    [Fact]
    public async Task A_total_within_the_tolerance_of_one_peso_is_approved()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);

        var result = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 6, issued.Number, "20261001", "1210.9", issued.Cae!));

        Assert.Equal("A", result.Value("Resultado"));
    }

    [Fact]
    public async Task A_code_nobody_issued_is_rejected_with_100()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);

        var result = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 6, issued.Number + 1, "20261001", "1210", "71234567890123"));

        Assert.Equal("R", result.Value("Resultado"));
        Assert.Equal(["100"], Codes(result, "Observaciones"));
        Assert.Equal("Verificar que el CAE/CAI/CAEA exista registrado y autorizado en las bases del organismo.", result.Value("Observaciones/Obs/Msg"));
    }

    [Fact]
    public async Task A_real_code_presented_for_another_issuer_and_number_names_each_field_that_differs()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);

        var result = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", 30000000007, 1, 6, issued.Number + 4, "20261001", "1210", issued.Cae!));

        Assert.Equal("R", result.Value("Resultado"));
        Assert.Equal(["102", "105"], Codes(result, "Observaciones"));
    }

    [Fact]
    public async Task A_class_A_voucher_constatado_with_another_receiver_is_rejected_with_112()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 1, new Voucher
        {
            Concept = 1, DocumentType = 80, DocumentNumber = 30000000007, Total = 121, Net = 100, Vat = 21,
            ReceiverVatCondition = 1, VatLines = [new VatLine(5, 100, 21)],
        });

        var right = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 1, issued.Number, "20261001", "121", issued.Cae!, "80", "30000000007"));
        var wrong = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 1, issued.Number, "20261001", "121", issued.Cae!, "80", "20222222223"));
        var anonymous = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 1, issued.Number, "20261001", "121", issued.Cae!));

        Assert.Equal("A", right.Value("Resultado"));
        Assert.Equal(["112"], Codes(wrong, "Observaciones"));
        Assert.Equal(["113", "114"], Codes(anonymous, "Observaciones"));
    }

    [Fact]
    public async Task The_types_115_and_116_name_are_taken_and_need_the_receiver_above_ten_million()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        // 37 is not in any table of the manual, but 115 and 116 name it: it is no format error (4), and above $10.000.000 it must identify the receiver.
        var small = await soap.CallAsync(Cdc, "ComprobanteConstatar", Constatar("CAI", Issuer, 1, 37, 1, "20261001", "1000", "71234567890123"));
        var large = await soap.CallAsync(Cdc, "ComprobanteConstatar", Constatar("CAI", Issuer, 1, 37, 1, "20261001", "10000001", "71234567890123"));
        var unknown = await soap.CallAsync(Cdc, "ComprobanteConstatar", Constatar("CAI", Issuer, 1, 36, 1, "20261001", "1000", "71234567890123"));
        var types = await soap.CallAsync(Cdc, "ComprobantesTipoConsultar", "");

        Assert.Equal(["100"], Codes(small, "Observaciones"));
        Assert.Null(small.Child("Errors"));
        Assert.Equal(["100", "115", "116"], Codes(large, "Observaciones").Order());
        Assert.Equal(["4"], Codes(unknown, "Errors"));
        Assert.DoesNotContain("37", Rows(types, "Id"));
    }

    [Fact]
    public async Task Format_errors_are_all_reported_in_Errors_without_looking_the_voucher_up()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var result = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("XYZ", Issuer, 1, 6, 1, "2026-10-01", "1210", "123"));

        Assert.Equal("R", result.Value("Resultado"));
        Assert.Equal(["1", "6", "10"], Codes(result, "Errors"));
        Assert.Null(result.Child("Observaciones"));
    }

    [Fact]
    public async Task An_export_invoice_authorized_through_WSFEXv1_can_be_constatado()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1));
        var cae = issued.Value("FEXResultAuth/Cae");

        var right = await soap.CallAsync(Cdc, "ComprobanteConstatar", Constatar("CAE", Issuer, 1, 19, 1, "20261001", "1000", cae));
        var wrongDate = await soap.CallAsync(Cdc, "ComprobanteConstatar", Constatar("CAE", Issuer, 1, 19, 1, "20260930", "1000", cae));

        Assert.Equal("A", right.Value("Resultado"));
        Assert.Equal(["107"], Codes(wrongDate, "Observaciones"));
    }

    [Fact]
    public async Task Every_answer_carries_the_event_code_the_catalog_records_and_no_message()
    {
        var (sim, wsfe, soap) = await StartAsync();
        await using var _s = sim;
        var issued = await wsfe.AuthorizeNextAsync(1, 6, ConsumerInvoice);

        var approved = await soap.CallAsync(Cdc, "ComprobanteConstatar",
            Constatar("CAE", Issuer, 1, 6, issued.Number, "20261001", "1210", issued.Cae!));
        var modes = await soap.CallAsync(Cdc, "ComprobantesModalidadConsultar", "");

        foreach (var answer in new[] { approved, modes })
        {
            Assert.Equal("0", answer.Value("Events/Evt/Code"));
            Assert.Null(answer.Child("Events")!.Child("Evt")!.Child("Msg"));
        }
    }

    [Fact]
    public async Task The_parameter_queries_answer_the_tables_the_checks_use()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var modes = await soap.CallAsync(Cdc, "ComprobantesModalidadConsultar", "");
        var types = await soap.CallAsync(Cdc, "ComprobantesTipoConsultar", "");
        var documents = await soap.CallAsync(Cdc, "DocumentosTipoConsultar", "");
        var optionals = await soap.CallAsync(Cdc, "OpcionalesTipoConsultar", "");

        Assert.Equal(["CAE", "CAEA", "CAI"], Rows(modes, "Cod"));
        Assert.Contains("1", Rows(types, "Id"));
        Assert.Contains("19", Rows(types, "Id"));
        Assert.Contains("195", Rows(types, "Id"));
        Assert.Contains("80", Rows(documents, "Id"));
        Assert.Empty(Rows(optionals, "Id"));
    }

    private static List<string> Codes(XElement result, string list) =>
        result.Child(list)?.Elements().Select(e => e.Value("Code")).ToList() ?? [];

    private static List<string> Rows(XElement result, string field) =>
        result.Child("ResultGet")?.Elements().Select(e => e.Value(field)).ToList() ?? [];
}
