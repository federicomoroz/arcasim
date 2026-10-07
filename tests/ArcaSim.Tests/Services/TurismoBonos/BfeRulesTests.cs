using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using ArcaSim.Application.Wsfe;
using Microsoft.Extensions.DependencyInjection;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>Bonos fiscales electrónicos: wsbfev1 and wsbfe numbering one book, the Id reproceso, and answers valid for their WSDLs.</summary>
public class BfeRulesTests
{
    private const long RequestId = 993456789012387;

    private static Task<ServiceDesk> OpenAsync(ArcaSimHarness sim) => ServiceDesk.OpenAsync(sim, "wsbfev1-homologacion.wsdl", "wsbfe", "x");

    private static string Auth(ServiceDesk desk, string name = "Auth") =>
        $"<x:{name}><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit></x:{name}>";

    /// <summary>The coffee invoice of PyAfipWs's recording, dated in ArcaSim's today and with the receiver's VAT condition RG 5616 asks for.</summary>
    private static string Cmp(long id, long number, int type = 1, string condition = "<x:CondicionIVAReceptorId>1</x:CondicionIVAReceptorId>", string extra = "") =>
        $"<x:Cmp><x:Id>{id}</x:Id><x:Tipo_doc>80</x:Tipo_doc><x:Nro_doc>20888888883</x:Nro_doc><x:Zona>1</x:Zona><x:Tipo_cbte>{type}</x:Tipo_cbte>" +
        $"<x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>{number}</x:Cbte_nro><x:Imp_total>1754.50</x:Imp_total><x:Imp_tot_conc>0.00</x:Imp_tot_conc>" +
        "<x:Imp_neto>1450.00</x:Imp_neto><x:Impto_liq>304.50</x:Impto_liq><x:Impto_liq_rni>0.00</x:Impto_liq_rni><x:Imp_op_ex>0.00</x:Imp_op_ex>" +
        "<x:Imp_perc>0.00</x:Imp_perc><x:Imp_iibb>0.00</x:Imp_iibb><x:Imp_perc_mun>0.00</x:Imp_perc_mun><x:Imp_internos>0.00</x:Imp_internos>" +
        $"<x:Imp_moneda_Id>PES</x:Imp_moneda_Id><x:Imp_moneda_ctz>1</x:Imp_moneda_ctz><x:Fecha_cbte>20261001</x:Fecha_cbte>{condition}{extra}" +
        "<x:Items><x:Item><x:Pro_codigo_ncm>2101.11.10</x:Pro_codigo_ncm><x:Pro_codigo_sec></x:Pro_codigo_sec><x:Pro_ds>Cafe</x:Pro_ds><x:Pro_qty>10.00</x:Pro_qty>" +
        "<x:Pro_umed>5</x:Pro_umed><x:Pro_precio_uni>150.00</x:Pro_precio_uni><x:Imp_bonif>50.00</x:Imp_bonif><x:Imp_total>1754.50</x:Imp_total><x:Iva_id>5</x:Iva_id></x:Item></x:Items>" +
        "</x:Cmp>";

    private static Task<XElement> AuthorizeAsync(ServiceDesk desk, string cmp) => desk.CallAsync("BFEAuthorize", Auth(desk) + cmp);

    private static Task<XElement> LastAsync(ServiceDesk desk, int type = 1) =>
        desk.CallAsync("BFEGetLast_CMP", $"<x:Auth><x:Token>{desk.Token}</x:Token><x:Sign>{desk.Sign}</x:Sign><x:Cuit>{ServiceDesk.Issuer}</x:Cuit><x:Pto_venta>5</x:Pto_venta><x:Tipo_cbte>{type}</x:Tipo_cbte></x:Auth>");

    private static string Value(XElement answer, string name) => answer.Descendants().First(e => e.Name.LocalName == name).Value;

    [Fact]
    public async Task A_voucher_gets_a_CAE_moves_the_last_number_and_Id_and_is_answered_back()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var authorized = await AuthorizeAsync(desk, Cmp(RequestId, 1));
        Assert.Equal("0", Value(authorized, "ErrCode"));
        Assert.Equal("A", Value(authorized, "Resultado"));
        Assert.Equal("N", Value(authorized, "Reproceso"));
        Assert.Equal(" ", Value(authorized, "Obs"));
        Assert.Equal("20261011", Value(authorized, "Fch_venc_Cae"));
        Assert.Matches("^[0-9]{14}$", Value(authorized, "Cae"));

        Assert.Equal("1", Value(await LastAsync(desk), "Cbte_nro"));
        Assert.Equal(RequestId.ToString(), Value(await desk.CallAsync("BFEGetLast_ID", Auth(desk)), "Id"));
        var consulted = await desk.CallAsync("BFEGetCMP", Auth(desk) + "<x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>1</x:Cbte_nro></x:Cmp>");
        Assert.Equal(Value(authorized, "Cae"), Value(consulted, "Cae"));
        Assert.Equal("1754.5", Value(consulted, "Imp_total"));
        Assert.Equal("Cafe", Value(consulted, "Pro_ds"));
        Assert.Equal("1", Value(consulted, "CondicionIVAReceptorId"));

        var recorded = await sim.Services.GetRequiredService<IDocumentStore>().FindVoucherAsync(ServiceDesk.Issuer, 5, 1, 1);
        Assert.Equal("wsbfev1", recorded!.Service);
    }

    [Fact]
    public async Task The_same_Id_again_answers_the_same_CAE_as_a_reproceso_without_numbering_again()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        var first = await AuthorizeAsync(desk, Cmp(RequestId, 1));

        var again = await AuthorizeAsync(desk, Cmp(RequestId, 1));

        Assert.Equal("S", Value(again, "Reproceso"));
        Assert.Equal(Value(first, "Cae"), Value(again, "Cae"));
        Assert.Equal("1", Value(await LastAsync(desk), "Cbte_nro"));
    }

    [Fact]
    public async Task A_voucher_without_the_receivers_VAT_condition_is_refused_with_4963_and_leaves_no_Id()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var refused = await AuthorizeAsync(desk, Cmp(RequestId, 1, condition: ""));

        Assert.Equal("4963", Value(refused, "ErrCode"));
        Assert.Equal("0", Value(refused, "Id"));
        Assert.Equal("0", Value(refused, "Cuit"));
        Assert.Empty(refused.Descendants(desk.Ns + "Cae"));
        Assert.Equal("0", Value(await desk.CallAsync("BFEGetLast_ID", Auth(desk)), "Id"));
        Assert.Equal("0", Value(await LastAsync(desk), "Cbte_nro"));
    }

    [Fact]
    public async Task A_credit_invoice_without_its_payment_date_is_refused_with_the_real_4900_text()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var refused = await AuthorizeAsync(desk, Cmp(RequestId, 1, type: 201));

        Assert.Equal("4900", Value(refused, "ErrCode"));
        Assert.Equal("Para Factura de Credito, es obligatorio informar el campo Fecha_vto_pago.", Value(refused, "ErrMsg"));
    }

    [Fact]
    public async Task A_number_that_is_not_the_next_one_is_refused_with_1014()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);

        var refused = await AuthorizeAsync(desk, Cmp(RequestId, 2581));

        Assert.Equal("1014", Value(refused, "ErrCode"));
        Assert.Contains("Cbte_nro", Value(refused, "ErrMsg"));
    }

    [Fact]
    public async Task Wsbfe_and_wsbfev1_number_the_same_book_and_each_carries_the_event_of_its_catalog_entry()
    {
        await using var sim = ArcaSimHarness.Start();
        var v1 = await OpenAsync(sim);
        var old = v1.Sibling("wsbfe-homologacion.wsdl", "x");

        var authorized = await AuthorizeAsync(old, Cmp(RequestId, 1));
        var next = await AuthorizeAsync(v1, Cmp(RequestId + 1, 2));

        Assert.Equal("A", Value(authorized, "Resultado"));
        Assert.Equal("102", Value(authorized, "EventCode"));
        Assert.Equal("A", Value(next, "Resultado"));
        Assert.Equal("39", Value(next, "EventCode"));
        Assert.StartsWith("IMPORTANTE: Por motivos de mantenimiento", Value(next, "EventMsg"));
        var fromOld = await old.CallAsync("BFEGetCMP", Auth(old) + "<x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>2</x:Cbte_nro></x:Cmp>");
        Assert.Equal(Value(next, "Cae"), Value(fromOld, "Cae"));
        Assert.Equal("2", Value(await LastAsync(old), "Cbte_nro"));
    }

    [Fact]
    public async Task Wsbfev1_waits_for_the_lock_of_the_book_it_shares_with_wsbfe()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        var locks = sim.Services.GetRequiredService<SequenceLocks>();

        Task<XElement> authorizing;
        // The book's name is wsbfe, whichever of the two services numbers: its lock for point of sale 5 and type 1.
        using (await locks.AcquireAsync("wsbfe", ServiceDesk.Issuer, 5, 1, CancellationToken.None))
        {
            authorizing = AuthorizeAsync(desk, Cmp(RequestId, 1));
            Assert.NotSame(authorizing, await Task.WhenAny(authorizing, Task.Delay(300)));
        }

        Assert.Equal("A", Value(await authorizing, "Resultado"));
    }

    [Fact]
    public async Task The_highest_Id_of_a_CUIT_is_read_and_written_under_a_lock_of_its_own()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        var locks = sim.Services.GetRequiredService<SequenceLocks>();

        Task<XElement> authorizing;
        // Two points of sale hold different sequence locks but share the CUIT's highest Id: the book keeps it under "<book>-ultimo-id".
        using (await locks.AcquireAsync("wsbfe-ultimo-id", ServiceDesk.Issuer, 0, 0, CancellationToken.None))
        {
            authorizing = AuthorizeAsync(desk, Cmp(RequestId, 1));
            Assert.NotSame(authorizing, await Task.WhenAny(authorizing, Task.Delay(300)));
        }

        Assert.Equal("A", Value(await authorizing, "Resultado"));
        Assert.Equal(RequestId.ToString(), Value(await desk.CallAsync("BFEGetLast_ID", Auth(desk)), "Id"));
    }

    [Fact]
    public async Task Wsbfe_has_no_credit_invoices()
    {
        await using var sim = ArcaSimHarness.Start();
        var old = (await OpenAsync(sim)).Sibling("wsbfe-homologacion.wsdl", "x");

        var refused = await AuthorizeAsync(old, Cmp(RequestId, 1, type: 201));
        var types = await old.CallAsync("BFEGetPARAM_Tipo_Cbte", Auth(old, "auth"));

        Assert.Equal("1014", Value(refused, "ErrCode"));
        Assert.Equal("Tipo de comprobante inválido.", Value(refused, "ErrMsg"));
        Assert.Equal(["1", "2", "3", "6", "7", "8"], types.Descendants(old.Ns + "Cbte_Id").Select(e => e.Value));
    }

    [Fact]
    public async Task The_queries_answer_1020_4967_the_annex_and_ArcaSims_rates()
    {
        await using var sim = ArcaSimHarness.Start();
        var desk = await OpenAsync(sim);
        await desk.SetRateAsync("DOL", new DateOnly(2026, 9, 30), 1450.5m);

        var missing = await desk.CallAsync("BFEGetCMP", Auth(desk) + "<x:Cmp><x:Tipo_cbte>1</x:Tipo_cbte><x:Punto_vta>5</x:Punto_vta><x:Cbte_nro>9</x:Cbte_nro></x:Cmp>");
        var badClass = await desk.CallAsync("BFEGetPARAM_CondicionIvaReceptor", Auth(desk, "auth") + "<x:ClaseCmp>Z</x:ClaseCmp>");
        var classA = await desk.CallAsync("BFEGetPARAM_CondicionIvaReceptor", Auth(desk, "auth") + "<x:ClaseCmp>A</x:ClaseCmp>");
        var rate = await desk.CallAsync("BFEGetCotizacion", Auth(desk) + "<x:MonId>DOL</x:MonId><x:FchCotiz>20261001</x:FchCotiz>");
        var types = await desk.CallAsync("BFEGetPARAM_Tipo_Cbte", Auth(desk, "auth"));

        Assert.Equal("1020", Value(missing, "ErrCode"));
        Assert.Equal("4967", Value(badClass, "ErrCode"));
        Assert.Equal(["1", "6", "13", "16"], classA.Descendants(desk.Ns + "Id").Select(e => e.Value));
        Assert.Equal("1450.5", Value(rate, "MonCotiz"));
        Assert.Equal("20260930", Value(rate, "FchCotiz"));
        Assert.Equal(12, types.Descendants(desk.Ns + "Cbte_Id").Count());
    }
}
