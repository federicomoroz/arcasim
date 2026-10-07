using System.Xml.Linq;
using static ArcaSim.Tests.Services.FacturacionE.FacturacionESoap;

namespace ArcaSim.Tests.Services.FacturacionE;

/// <summary>Export invoices through WSFEXv1, as an exporter's system sends them, every answer valid for the WSDL.</summary>
public class Wsfexv1RulesTests
{
    [Fact]
    public async Task An_export_invoice_gets_a_CAE_and_is_numbered_read_back_and_found_by_its_Id()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var before = await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 19));
        var result = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1001, number: 1));
        var last = await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 19));
        var lastId = await soap.CallAsync(Fex, "FEXGetLast_ID", "");
        var stored = await soap.CallAsync(Fex, "FEXGetCMP", GetCmp(1, 19, 1));

        Assert.Equal("0", before.Value("FEXResult_LastCMP/Cbte_nro"));
        Assert.Equal("0", result.Value("FEXErr/ErrCode"));
        Assert.Equal("A", result.Value("FEXResultAuth/Resultado"));
        Assert.Equal("N", result.Value("FEXResultAuth/Reproceso"));
        Assert.Equal("1", result.Value("FEXResultAuth/Cbte_nro"));
        Assert.Matches("^[0-9]{14}$", result.Value("FEXResultAuth/Cae"));
        Assert.Equal("20261001", result.Value("FEXResultAuth/Fch_cbte"));
        Assert.Equal("20261011", result.Value("FEXResultAuth/Fch_venc_Cae"));
        Assert.Equal("1", last.Value("FEXResult_LastCMP/Cbte_nro"));
        Assert.Equal("20261001", last.Value("FEXResult_LastCMP/Cbte_fecha"));
        Assert.Equal("1001", lastId.Value("FEXResultGet/Id"));
        Assert.Equal(result.Value("FEXResultAuth/Cae"), stored.Value("FEXResultGet/Cae"));
        Assert.Equal("ACME TRADING LTDA", stored.Value("FEXResultGet/Cliente"));
        Assert.Equal("1000", stored.Value("FEXResultGet/Imp_total"));
        Assert.Equal("Soja en grano", stored.Value("FEXResultGet/Items/Item/Pro_ds"));
    }

    [Fact]
    public async Task Numbers_follow_each_other_per_point_of_sale_and_type()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1));
        var second = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 2, number: 2));
        var note = await soap.CallAsync(Fex, "FEXAuthorize", CreditNote(id: 3, number: 1, invoice: 2));

        Assert.Equal("2", second.Value("FEXResultAuth/Cbte_nro"));
        Assert.Equal("1", note.Value("FEXResultAuth/Cbte_nro"));
        Assert.Equal("A", note.Value("FEXResultAuth/Resultado"));
        Assert.Equal("2", (await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 19))).Value("FEXResult_LastCMP/Cbte_nro"));
        Assert.Equal("1", (await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 21))).Value("FEXResult_LastCMP/Cbte_nro"));
        Assert.Equal("3", (await soap.CallAsync(Fex, "FEXGetLast_ID", "")).Value("FEXResultGet/Id"));
    }

    [Fact]
    public async Task A_repeated_Id_answers_what_was_granted_with_Reproceso_S()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var first = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 77, number: 1));
        var again = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 77, number: 1));

        Assert.Equal("S", again.Value("FEXResultAuth/Reproceso"));
        Assert.Equal(first.Value("FEXResultAuth/Cae"), again.Value("FEXResultAuth/Cae"));
        Assert.Equal("1", (await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 19))).Value("FEXResult_LastCMP/Cbte_nro"));
    }

    [Fact]
    public async Task A_number_out_of_sequence_is_refused_with_1535_and_its_Id_can_be_sent_again()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var refused = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 5, number: 2));
        var retried = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 5, number: 1));

        Assert.Equal("1535", refused.Value("FEXErr/ErrCode"));
        Assert.Equal("Verifica que el comprobante ingresado corresponde en secuencia al próximo inmediato a autorizar.", refused.Value("FEXErr/ErrMsg"));
        Assert.Null(refused.Child("FEXResultAuth"));
        Assert.Equal("N", retried.Value("FEXResultAuth/Reproceso"));
        Assert.Equal("1", retried.Value("FEXResultAuth/Cbte_nro"));
    }

    [Fact]
    public async Task A_total_that_is_not_the_sum_of_the_items_is_refused_with_1610()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var refused = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1, total: "1200"));

        Assert.Equal("1610", refused.Value("FEXErr/ErrCode"));
        Assert.Equal("0", (await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(1, 19))).Value("FEXResult_LastCMP/Cbte_nro"));
    }

    [Fact]
    public async Task The_relative_margin_of_the_total_is_measured_on_the_total_informed()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        // The items add up to 1000. 0.10 under it is exactly 0.01 % of the sum but a little over 0.01 % of the 999.90 informed.
        var under = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1, total: "999.90"));
        var over = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 2, number: 1, total: "1000.10"));

        Assert.Equal("1610", under.Value("FEXErr/ErrCode"));
        Assert.Equal("A", over.Value("FEXResultAuth/Resultado"));
    }

    [Fact]
    public async Task A_point_of_sale_not_registered_for_web_services_is_refused_with_1510_and_1607()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var refused = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1, pointOfSale: 7));
        var last = await soap.CallAsync(Fex, "FEXGetLast_CMP", LastCmp(7, 19));

        Assert.Equal("1510", refused.Value("FEXErr/ErrCode"));
        Assert.Equal("1607", last.Value("FEXErr/ErrCode"));
    }

    [Fact]
    public async Task A_credit_note_for_an_invoice_that_was_never_authorized_is_refused_with_1749()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var refused = await soap.CallAsync(Fex, "FEXAuthorize", CreditNote(id: 1, number: 1, invoice: 9));

        Assert.Equal("1749", refused.Value("FEXErr/ErrCode"));
    }

    [Fact]
    public async Task FEXGetCMP_of_a_voucher_nobody_authorized_answers_1020()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var missing = await soap.CallAsync(Fex, "FEXGetCMP", GetCmp(1, 19, 1));

        Assert.Equal("1020", missing.Value("FEXErr/ErrCode"));
        Assert.Null(missing.Child("FEXResultGet"));
    }

    [Fact]
    public async Task The_parameter_tables_answer_the_manuals_values()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;

        var types = await soap.CallAsync(Fex, "FEXGetPARAM_Cbte_Tipo", "");
        var exports = await soap.CallAsync(Fex, "FEXGetPARAM_Tipo_Expo", "");
        var languages = await soap.CallAsync(Fex, "FEXGetPARAM_Idiomas", "");
        var currencies = await soap.CallAsync(Fex, "FEXGetPARAM_MON", "");
        var points = await soap.CallAsync(Fex, "FEXGetPARAM_PtoVenta", "");
        var incoterms = await soap.CallAsync(Fex, "FEXGetPARAM_Incoterms", "");
        var cuits = await soap.CallAsync(Fex, "FEXGetPARAM_DST_CUIT", "");

        Assert.Equal(["19", "20", "21"], Ids(types, "Cbte_Id"));
        Assert.Equal(["1", "2", "4"], Ids(exports, "Tex_Id"));
        Assert.Equal(["1", "2", "3"], Ids(languages, "Idi_Id"));
        Assert.Contains("DOL", Ids(currencies, "Mon_Id"));
        Assert.Equal(["1"], Ids(points, "Pve_Nro"));
        Assert.Contains("FOB", Ids(incoterms, "Inc_Id"));
        Assert.Contains("50000000016", Ids(cuits, "DST_CUIT"));
    }

    [Fact]
    public async Task The_rate_table_serves_the_rate_loaded_in_ArcaSim()
    {
        var (sim, _, soap) = await StartAsync();
        await using var _s = sim;
        await SetRateAsync(sim, "DOL", new DateOnly(2026, 9, 30), 1450.5m);

        var rate = await soap.CallAsync(Fex, "FEXGetPARAM_Ctz", "<s:Mon_id>DOL</s:Mon_id><s:FchCotiz>2026-09-30</s:FchCotiz>");
        var refused = await soap.CallAsync(Fex, "FEXAuthorize", Export(id: 1, number: 1, rate: "10"));

        Assert.Equal("1450.5", rate.Value("FEXResultGet/Mon_ctz"));
        Assert.Equal("20260930", rate.Value("FEXResultGet/Mon_fecha"));
        Assert.Equal("1667", refused.Value("FEXErr/ErrCode"));
    }

    private static List<string> Ids(XElement result, string field) =>
        result.Child("FEXResultGet")!.Elements().Select(e => e.Value(field)).ToList();
}
