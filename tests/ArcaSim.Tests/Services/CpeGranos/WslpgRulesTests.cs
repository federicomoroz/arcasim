using System.Xml.Linq;

namespace ArcaSim.Tests.Services.CpeGranos;

/// <summary>wslpg: numbering per punto de emisión, COE, the manual's amounts, voids, adjustments, LSG and certificates, every answer valid for the WSDL.</summary>
public class WslpgRulesTests
{
    private const long Buyer = ArcaSimHarness.Issuer;
    private const long Depositor = 30000000007;

    [Fact]
    public async Task Authorize_gives_a_COE_with_the_manuals_amounts_and_both_consults_read_it_back()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);

        Assert.Equal("0", (await LastAsync(lpg, "liquidacionUltimoNroOrdenConsultar", "liqUltNroOrdenReq")).Value("nroOrden"));

        var authorized = await AuthorizeAsync(lpg, 1);
        var coe = authorized.Value("coe")!;
        Assert.Matches("^3301[0-9]{8}$", coe);
        Assert.Equal("AC", authorized.Value("estado"));
        Assert.Equal("1.970", authorized.Value("precioOperacion"));
        Assert.Equal("1970.00", authorized.Value("subTotal"));
        Assert.Equal("206.85", authorized.Value("importeIva"));
        Assert.Equal("2176.85", authorized.Value("operacionConIva"));
        Assert.Equal("159.60", authorized.Value("totalRetencion"));
        Assert.Equal("2017.25", authorized.Value("totalNetoAPagar"));
        Assert.Equal("49.25", authorized.Value("totalIvaRg4310_18"));
        Assert.Equal("1968.00", authorized.Value("totalPagoSegunCondicion"));
        Assert.Equal(["157.60", "2.00"], authorized.Descendants("importeRetencion").Select(e => e.Value));

        Assert.Equal("1", (await LastAsync(lpg, "liquidacionUltimoNroOrdenConsultar", "liqUltNroOrdenReq")).Value("nroOrden"));

        var byCoe = await lpg.CallAsync(Buyer, "liquidacionXCoeConsultar", "liqConsXCoeReq", $"<coe>{coe}</coe><pdf>S</pdf>");
        Assert.Equal("2017.25", byCoe.Value("totalNetoAPagar"));
        Assert.Equal("101200604", byCoe.Value("nroCertificadoDeposito"));
        Assert.StartsWith("JVBERi", byCoe.Value("pdf"));

        var byNumber = await lpg.CallAsync(Buyer, "liquidacionXNroOrdenConsultar", "liqConsXNroOrdenReq", "<ptoEmision>1</ptoEmision><nroOrden>1</nroOrden>");
        Assert.Equal(coe, byNumber.Value("coe"));
        Assert.Null(byNumber.Value("pdf"));

        var none = await lpg.CallAsync(Buyer, "liquidacionXNroOrdenConsultar", "liqConsXNroOrdenReq", "<ptoEmision>1</ptoEmision><nroOrden>2</nroOrden>");
        Assert.Equal(("600", "No existen datos en las bases de la Administración según los parámetros de búsqueda informados."), none.FirstError());
    }

    [Fact]
    public async Task The_order_number_must_be_the_last_plus_one_per_point_of_issue()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);

        var skipped = await AuthorizeAsync(lpg, 2);
        Assert.Equal(("1508", "El nro de orden, no es consecutivo al último utilizado para el punto de emisión indicado."), skipped.FirstError());
        Assert.Null(skipped.Value("autorizacion"));

        await AuthorizeAsync(lpg, 1);
        var repeated = await AuthorizeAsync(lpg, 1);
        Assert.Equal("1508", repeated.FirstError()!.Value.Code);

        var otherPoint = await AuthorizeAsync(lpg, 1, pointOfIssue: 2);
        Assert.Equal("AC", otherPoint.Value("estado"));
    }

    [Fact]
    public async Task Voiding_answers_A_once_and_R_with_1527_after()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var coe = (await AuthorizeAsync(lpg, 1)).Value("coe");

        var voided = await lpg.CallAsync(Buyer, "liquidacionAnular", "anulacionReq", $"<coe>{coe}</coe>");
        Assert.Equal("A", voided.Value("resultado"));

        var again = await lpg.CallAsync(Buyer, "liquidacionAnular", "anulacionReq", $"<coe>{coe}</coe>");
        Assert.Equal("R", again.Value("resultado"));
        Assert.Equal(("1527", "La liquidacion fue anulada con anterioridad."), again.FirstError());

        var read = await lpg.CallAsync(Buyer, "liquidacionXCoeConsultar", "liqConsXCoeReq", $"<coe>{coe}</coe>");
        Assert.Equal("AN", read.Value("estado"));
    }

    [Fact]
    public async Task An_adjustment_takes_the_next_LPG_number_has_its_own_COE_and_blocks_the_void()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var original = (await AuthorizeAsync(lpg, 1)).Value("coe");

        var adjusted = await lpg.CallAsync(Buyer, "liquidacionAjustarUnificado", "ajustarUnificadoReq",
            $"<ajusteBase><ptoEmision>1</ptoEmision><nroOrden>2</nroOrden><coeAjustado>{original}</coeAjustado><codLocalidad>3</codLocalidad><codProv>1</codProv></ajusteBase>" +
            "<ajusteDebito><diferenciaPesoNeto>0</diferenciaPesoNeto><diferenciaPrecioOperacion>0</diferenciaPrecioOperacion>" +
            "<conceptoImporteIva105>Diferencia de precio</conceptoImporteIva105><importeAjustarIva105>100</importeAjustarIva105>" +
            "<retenciones><retencion><codigoConcepto>RI</codigoConcepto><baseCalculo>100</baseCalculo><alicuota>8</alicuota></retencion></retenciones></ajusteDebito>");
        var coe = adjusted.Value("coe")!;
        Assert.Matches("^3302[0-9]{8}$", coe);
        Assert.Equal(original, adjusted.Value("coeAjustado"));
        Assert.Equal("10.50", adjusted.Value("iva105"));
        Assert.Equal("102.50", adjusted.Value("importeNeto"));
        Assert.Equal("0.00", adjusted.Descendants("ajusteCredito").Single().Element("subTotal")!.Value);

        Assert.Equal("2", (await LastAsync(lpg, "liquidacionUltimoNroOrdenConsultar", "liqUltNroOrdenReq")).Value("nroOrden"));

        var byCoe = await lpg.CallAsync(Buyer, "ajusteXCoeConsultar", "ajusteXCoeConsReq", $"<coe>{coe}</coe>");
        Assert.Equal("100.00", byCoe.Descendants("ajusteDebito").Single().Element("subTotal")!.Value);
        var byNumber = await lpg.CallAsync(Buyer, "ajusteXNroOrdenConsultar", "ajusteXNroOrdenConsReq", "<ptoEmision>1</ptoEmision><nroOrden>2</nroOrden>");
        Assert.Equal(coe, byNumber.Value("coe"));

        var asOriginal = await lpg.CallAsync(Buyer, "liquidacionXCoeConsultar", "liqConsXCoeReq", $"<coe>{coe}</coe>");
        Assert.Equal("1861", asOriginal.FirstError()!.Value.Code);
        var asAdjustment = await lpg.CallAsync(Buyer, "ajusteXCoeConsultar", "ajusteXCoeConsReq", $"<coe>{original}</coe>");
        Assert.Equal(("1649", "El COE consultado debe corresponder a un ajuste."), asAdjustment.FirstError());

        var voided = await lpg.CallAsync(Buyer, "liquidacionAnular", "anulacionReq", $"<coe>{original}</coe>");
        Assert.Equal("R", voided.Value("resultado"));
        Assert.Equal("1519", voided.FirstError()!.Value.Code);
    }

    [Fact]
    public async Task Another_CUIT_cannot_read_the_liquidacion()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var coe = (await AuthorizeAsync(lpg, 1)).Value("coe");

        var read = await lpg.CallAsync(Depositor, "liquidacionXCoeConsultar", "liqConsXCoeReq", $"<coe>{coe}</coe>");
        Assert.Equal(("1510", "La liquidación consultada, corresponde a otra CUIT."), read.FirstError());
    }

    [Fact]
    public async Task A_counterdocument_starts_pending_with_its_own_number_and_COE()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var original = (await AuthorizeAsync(lpg, 1)).Value("coe");

        var counter = await lpg.CallAsync(Buyer, "lpgAnularContraDocumento", "LpgAnularContraDocumentoReq",
            $"<anulacionBase><puntoEmision>1</puntoEmision><nroOrden>2</nroOrden><coeAnular>{original}</coeAnular></anulacionBase>");
        Assert.Equal("PA", counter.Value("estado"));
        Assert.Equal(original, counter.Value("coeAjustado"));
        Assert.NotEqual(original, counter.Value("coe"));

        var twice = await lpg.CallAsync(Buyer, "lpgAnularContraDocumento", "LpgAnularContraDocumentoReq",
            $"<anulacionBase><puntoEmision>1</puntoEmision><nroOrden>3</nroOrden><coeAnular>{original}</coeAnular></anulacionBase>");
        Assert.Equal("1527", twice.FirstError()!.Value.Code);
    }

    [Fact]
    public async Task A_secondary_liquidacion_numbers_on_its_own_and_reads_back()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        await AuthorizeAsync(lpg, 1);

        var authorized = await lpg.CallAsync(Buyer, "lsgAutorizar", "lsgAutorizarReq",
            "<liqSecundariaBase><ptoEmision>1</ptoEmision><nroOrden>1</nroOrden>" +
            $"<cuitComprador>{Buyer}</cuitComprador><nroIngBrutoComprador>1</nroIngBrutoComprador><codPuerto>2</codPuerto><desPuertoLocalidad>ROSARIO</desPuertoLocalidad>" +
            $"<codGrano>23</codGrano><cantidadTn>30.000</cantidadTn><cuitVendedor>{Depositor}</cuitVendedor><nroActVendedor>40</nroActVendedor><nroIngBrutoVendedor>2</nroIngBrutoVendedor>" +
            "<actuaCorredor>N</actuaCorredor><liquidaCorredor>N</liquidaCorredor><fechaPrecioOperacion>2026-09-30</fechaPrecioOperacion>" +
            "<precioRefTn>250000</precioRefTn><precioOperacion>250000</precioOperacion><alicIvaOperacion>10.5</alicIvaOperacion><campaniaPPal>2526</campaniaPPal>" +
            "<codLocalidad>3</codLocalidad><codProvincia>12</codProvincia></liqSecundariaBase>");
        var coe = authorized.Value("coe")!;
        Assert.Matches("^3310[0-9]{8}$", coe);
        Assert.Equal("7500000.00", authorized.Value("subTotal"));
        Assert.Equal("787500.00", authorized.Value("importeIva"));
        Assert.Equal("1", (await LastAsync(lpg, "lsgConsultarUltimoNroOrden", "lsgConsultarUltimoNroOrdenReq")).Value("nroOrden"));

        var read = await lpg.CallAsync(Buyer, "lsgConsultarXNroOrden", "lsgConsultarXNroOrdenReq", "<ptoEmision>1</ptoEmision><nroOrden>1</nroOrden>");
        Assert.Equal(coe, read.Value("coe"));
        Assert.Equal("30.000", read.Value("pesoNetoEnTn"));
        Assert.Equal("7500000.00", read.Value("subtotal"));
        Assert.Equal("AC", read.Value("estado"));

        var voided = await lpg.CallAsync(Buyer, "lsgAnular", "lsgAnularReq", $"<coe>{coe}</coe>");
        Assert.Equal("A", voided.Value("resultado"));
        var again = await lpg.CallAsync(Buyer, "lsgAnular", "lsgAnularReq", $"<coe>{coe}</coe>");
        Assert.Equal(("1527", "La liquidacion fue anulada con anterioridad."), again.FirstError());
    }

    [Fact]
    public async Task A_certificate_is_voided_at_once_until_the_15th_and_pending_the_depositors_confirmation_after()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        await lpg.LoginAsync(Depositor, "Productor del Sur S.A.");

        var first = (await CertifyAsync(lpg, 1)).Value("coe");
        var second = (await CertifyAsync(lpg, 2)).Value("coe")!;
        Assert.Matches("^3320[0-9]{8}$", second);

        var read = await lpg.CallAsync(Buyer, "cgConsultarXNroOrden", "CgConsultarXNroOrdenReq", "<ptoEmision>1</ptoEmision><nroOrden>2</nroOrden>");
        Assert.Equal(second, read.Value("coe"));
        Assert.Equal("29500", read.Value("pesoNetoCertificado"));

        var voided = await lpg.CallAsync(Buyer, "cgSolicitarAnulacion", "cgSolicitarAnulacionReq", $"<coe>{first}</coe>");
        Assert.Equal("AN", voided.Value("estadoCertificado"));

        sim.Clock.Advance(TimeSpan.FromDays(50));
        await lpg.LoginAsync(Buyer);
        await lpg.LoginAsync(Depositor, "Productor del Sur S.A.");
        var late = await lpg.CallAsync(Buyer, "cgSolicitarAnulacion", "cgSolicitarAnulacionReq", $"<coe>{second}</coe>");
        Assert.Equal("PA", late.Value("estadoCertificado"));

        var notYours = await lpg.CallAsync(Buyer, "cgConfirmarAnulacion", "cgConfirmarAnulacionReq", $"<coe>{second}</coe>");
        Assert.Equal("3502", notYours.FirstError()!.Value.Code);
        var confirmed = await lpg.CallAsync(Depositor, "cgConfirmarAnulacion", "cgConfirmarAnulacionReq", $"<coe>{second}</coe>");
        Assert.Equal("AN", confirmed.Value("estadoCertificado"));
        var twice = await lpg.CallAsync(Depositor, "cgConfirmarAnulacion", "cgConfirmarAnulacionReq", $"<coe>{second}</coe>");
        Assert.Equal(("3501", "La certificación seleccionada no es anulable ya que la transición de estados no es la correcta."), twice.FirstError());
    }

    [Fact]
    public async Task Parameter_tables_serve_the_values_the_manual_prints()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);

        var ports = await lpg.CallAsync(Buyer, "puertoConsultar", "puertoReq");
        Assert.Equal(["1", "2", "3", "4"], ports.Descendants("codigo").Select(e => e.Value));

        var campaigns = await lpg.CallAsync(Buyer, "campaniasConsultar", "campaniaReq");
        Assert.Contains(campaigns.Descendants("codigoDescripcion"), c => c.Element("codigo")!.Value == "708" && c.Element("descripcion")!.Value == "2007/2008");
        Assert.Contains(campaigns.Descendants("codigoDescripcion"), c => c.Element("codigo")!.Value == "2627");

        var grades = await lpg.CallAsync(Buyer, "codigoGradoEntregadoXTipoGranoConsultar", "gradoEntregadoReq", "<codGrano>23</codGrano>");
        Assert.Equal(["1.01", "1.00", "0.985"], grades.Descendants("valor").Select(e => e.Value));
    }

    private const int Callers = 24;

    [Fact]
    public async Task Of_many_voids_of_one_liquidacion_exactly_one_takes_it()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var coe = (await AuthorizeAsync(lpg, 1)).Value("coe");

        var answers = await AllAtOnce(_ => lpg.CallAsync(Buyer, "liquidacionAnular", "anulacionReq", $"<coe>{coe}</coe>"));

        Assert.Equal(1, answers.Count(a => a.Value("resultado") == "A"));
        Assert.All(answers.Where(a => a.Value("resultado") == "R"), a => Assert.Equal("1527", a.FirstError()!.Value.Code));
    }

    [Fact]
    public async Task Of_many_counterdocuments_of_one_liquidacion_exactly_one_is_issued()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var original = (await AuthorizeAsync(lpg, 1)).Value("coe");

        // Each caller numbers on a point of issue of its own, so only the original's state tells them apart.
        var answers = await AllAtOnce(i => lpg.CallAsync(Buyer, "lpgAnularContraDocumento", "LpgAnularContraDocumentoReq",
            $"<anulacionBase><puntoEmision>{i + 2}</puntoEmision><nroOrden>1</nroOrden><coeAnular>{original}</coeAnular></anulacionBase>"));

        Assert.Equal(1, answers.Count(a => a.Value("estado") == "PA"));
        Assert.All(answers.Where(a => a.Value("estado") != "PA"), a => Assert.Equal("1527", a.FirstError()!.Value.Code));
    }

    [Fact]
    public async Task A_void_and_an_adjustment_of_one_liquidacion_never_both_succeed()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);

        for (var round = 1; round <= 20; round++)
        {
            var original = (await AuthorizeAsync(lpg, round, pointOfIssue: 2 * round)).Value("coe")!;
            var voiding = lpg.CallAsync(Buyer, "liquidacionAnular", "anulacionReq", $"<coe>{original}</coe>");
            var adjusting = lpg.CallAsync(Buyer, "liquidacionAjustarUnificado", "ajustarUnificadoReq", Adjustment(2 * round, original));
            await Task.WhenAll(voiding, adjusting);

            var voided = voiding.Result.Value("resultado") == "A";
            var adjusted = adjusting.Result.Value("coe") is not null;
            Assert.False(voided && adjusted, $"Round {round}: the liquidacion was voided and adjusted.");
        }
    }

    [Fact]
    public async Task Of_many_requests_to_void_one_certificate_exactly_one_takes_it()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);
        var coe = (await CertifyAsync(lpg, 1)).Value("coe");

        var answers = await AllAtOnce(_ => lpg.CallAsync(Buyer, "cgSolicitarAnulacion", "cgSolicitarAnulacionReq", $"<coe>{coe}</coe>"));

        Assert.Equal(1, answers.Count(a => a.Value("estadoCertificado") == "AN"));
        Assert.All(answers.Where(a => a.Value("estadoCertificado") != "AN"), a => Assert.Equal("3501", a.FirstError()!.Value.Code));
    }

    [Fact]
    public async Task A_certificate_a_retiro_uses_is_not_voided_at_the_same_time()
    {
        await using var sim = Start();
        var lpg = await ConnectAsync(sim);

        for (var round = 1; round <= 20; round++)
        {
            var deposit = (await CertifyAsync(lpg, 2 * round - 1)).Value("coe")!;
            var voiding = lpg.CallAsync(Buyer, "cgSolicitarAnulacion", "cgSolicitarAnulacionReq", $"<coe>{deposit}</coe>");
            var withdrawing = Withdraw(lpg, 2 * round, deposit);
            await Task.WhenAll(voiding, withdrawing);

            var voided = voiding.Result.Value("estadoCertificado") == "AN";
            var state = (await lpg.CallAsync(Buyer, "cgConsultarXCoe", "cgConsultarXCoeReq", $"<coe>{deposit}</coe>")).Value("estado");
            Assert.True(voided == (state == "AN"), $"Round {round}: the void answered {(voided ? "AN" : "a refusal")} and the certificate is {state}.");
        }
    }

    private static async Task<List<XElement>> AllAtOnce(Func<int, Task<XElement>> call)
    {
        using var start = new ManualResetEventSlim();
        var calls = Enumerable.Range(0, Callers).Select(i => Task.Run(() =>
        {
            start.Wait();
            return call(i);
        })).ToList();
        start.Set();
        return [.. await Task.WhenAll(calls)];
    }

    private static string Adjustment(int order, string original) =>
        $"<ajusteBase><ptoEmision>{order}</ptoEmision><nroOrden>2</nroOrden><coeAjustado>{original}</coeAjustado><codLocalidad>3</codLocalidad><codProv>1</codProv></ajusteBase>" +
        "<ajusteDebito><diferenciaPesoNeto>0</diferenciaPesoNeto><diferenciaPrecioOperacion>0</diferenciaPrecioOperacion>" +
        "<conceptoImporteIva105>Diferencia de precio</conceptoImporteIva105><importeAjustarIva105>100</importeAjustarIva105></ajusteDebito>";

    private static Task<XElement> Withdraw(GrainsSoap lpg, int order, string deposit) =>
        lpg.CallAsync(Buyer, "cgAutorizar", "cgAutorizarReq",
            $"<cabecera><tipoCertificado>R</tipoCertificado><ptoEmision>1</ptoEmision><nroOrden>{order}</nroOrden><nroIngBrutoDepositario>1</nroIngBrutoDepositario>" +
            $"<titularGrano>T</titularGrano><cuitDepositante>{Depositor}</cuitDepositante><nroIngBrutoDepositante>2</nroIngBrutoDepositante><codGrano>23</codGrano><campania>2526</campania></cabecera>" +
            $"<retiroTransferencia><certificadoDeposito><coeCertificadoDeposito>{deposit}</coeCertificadoDeposito><pesoNeto>1000</pesoNeto></certificadoDeposito>" +
            "<nroActDepositario>36</nroActDepositario><nroCartaPorteAUtilizar>1</nroCartaPorteAUtilizar></retiroTransferencia>");

    private static ArcaSimHarness Start()
    {
        var sim = ArcaSimHarness.Start();
        sim.Clock.Freeze(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3)));
        return sim;
    }

    private static async Task<GrainsSoap> ConnectAsync(ArcaSimHarness sim)
    {
        var lpg = GrainsSoap.Wslpg(sim);
        await lpg.LoginAsync(Buyer);
        await lpg.LoginAsync(Depositor, "Productor del Sur S.A.");
        return lpg;
    }

    private static Task<XElement> LastAsync(GrainsSoap lpg, string operation, string root) =>
        lpg.CallAsync(Buyer, operation, root, "<ptoEmision>1</ptoEmision>");

    /// <summary>Ejemplo 1 of §2.4.2.4: 1000 kg at 2000 per ton, factor 98, flete 10, IVA 10.5%, retenciones RI 8% and RG 2%.</summary>
    private static Task<XElement> AuthorizeAsync(GrainsSoap lpg, int order, int pointOfIssue = 1) =>
        lpg.CallAsync(Buyer, "liquidacionAutorizar", "liquidacionReq",
            $"<liquidacion><ptoEmision>{pointOfIssue}</ptoEmision><nroOrden>{order}</nroOrden><cuitComprador>{Buyer}</cuitComprador>" +
            $"<nroActComprador>50</nroActComprador><nroIngBrutoComprador>{Buyer}</nroIngBrutoComprador><codTipoOperacion>1</codTipoOperacion>" +
            "<esLiquidacionPropia>N</esLiquidacionPropia><esCanje>N</esCanje><codPuerto>14</codPuerto><desPuertoLocalidad>DETALLE PUERTO</desPuertoLocalidad>" +
            $"<codGrano>31</codGrano><cuitVendedor>{Depositor}</cuitVendedor><nroIngBrutoVendedor>{Depositor}</nroIngBrutoVendedor>" +
            "<actuaCorredor>N</actuaCorredor><liquidaCorredor>N</liquidaCorredor><fechaPrecioOperacion>2026-09-30</fechaPrecioOperacion>" +
            "<precioRefTn>2000</precioRefTn><codGradoRef>G1</codGradoRef><codGradoEnt>G1</codGradoEnt><factorEnt>98</factorEnt><precioFleteTn>10</precioFleteTn>" +
            "<contProteico>20</contProteico><alicIvaOperacion>10.5</alicIvaOperacion><campaniaPPal>2526</campaniaPPal>" +
            "<codLocalidadProcedencia>3</codLocalidadProcedencia><codProvProcedencia>1</codProvProcedencia><datosAdicionales>DATOS ADICIONALES</datosAdicionales>" +
            "<certificados><certificado><tipoCertificadoDeposito>5</tipoCertificadoDeposito><nroCertificadoDeposito>101200604</nroCertificadoDeposito>" +
            "<pesoNeto>1000</pesoNeto><codLocalidadProcedencia>3</codLocalidadProcedencia><codProvProcedencia>1</codProvProcedencia><campania>2526</campania>" +
            "<fechaCierre>2026-09-13</fechaCierre></certificado></certificados></liquidacion>" +
            "<retenciones><retencion><codigoConcepto>RI</codigoConcepto><detalleAclaratorio>DETALLE DE IVA</detalleAclaratorio><baseCalculo>1970</baseCalculo><alicuota>8</alicuota></retencion>" +
            "<retencion><codigoConcepto>RG</codigoConcepto><detalleAclaratorio>DETALLE DE GANANCIAS</detalleAclaratorio><baseCalculo>100</baseCalculo><alicuota>2</alicuota></retencion></retenciones>");

    private static Task<XElement> CertifyAsync(GrainsSoap lpg, int order) =>
        lpg.CallAsync(Buyer, "cgAutorizar", "cgAutorizarReq",
            $"<cabecera><tipoCertificado>P</tipoCertificado><ptoEmision>1</ptoEmision><nroOrden>{order}</nroOrden><nroIngBrutoDepositario>1</nroIngBrutoDepositario>" +
            $"<titularGrano>T</titularGrano><cuitDepositante>{Depositor}</cuitDepositante><nroIngBrutoDepositante>2</nroIngBrutoDepositante><codGrano>23</codGrano><campania>2526</campania></cabecera>" +
            "<primaria><nroActDepositario>36</nroActDepositario><ctg><nroCTG>10200000001</nroCTG><nroCartaDePorte>1</nroCartaDePorte>" +
            "<pesoNetoConfirmadoDefinitivo>30000</pesoNetoConfirmadoDefinitivo><porcentajeSecadoHumedad>0</porcentajeSecadoHumedad><importeSecado>0</importeSecado>" +
            "<pesoNetoMermaSecado>300</pesoNetoMermaSecado><tarifaSecado>0</tarifaSecado><importeZarandeo>0</importeZarandeo><pesoNetoMermaZarandeo>200</pesoNetoMermaZarandeo>" +
            "<tarifaZarandeo>0</tarifaZarandeo></ctg><descripcionTipoGrano>SOJA</descripcionTipoGrano><montoAlmacenaje>0</montoAlmacenaje><montoAcarreo>0</montoAcarreo>" +
            "<montoGastosGenerales>0</montoGastosGenerales><montoZarandeo>0</montoZarandeo><porcentajeSecadoDe>0</porcentajeSecadoDe><porcentajeSecadoA>0</porcentajeSecadoA>" +
            "<montoSecado>0</montoSecado><montoPorCadaPuntoExceso>0</montoPorCadaPuntoExceso><montoOtros>0</montoOtros><pesoNetoMermaVolatil>0</pesoNetoMermaVolatil></primaria>");
}
