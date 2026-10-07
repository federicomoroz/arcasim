using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Economía del Conocimiento: queries of a CUIT's export vouchers, kept and read back, with the manual's errors.</summary>
public class CecRulesTests
{
    private const long Caller = ServiceProbe.Caller;

    [Fact]
    public async Task A_query_by_period_finds_the_export_vouchers_and_is_kept()
    {
        await using var sim = ArcaSimHarness.Start();
        var cec = await ServiceProbe.StartAsync(sim, "wscec");
        await cec.Store.PutAsync(Voucher(1, new DateOnly(2026, 8, 10), 1500m));
        await cec.Store.PutAsync(Voucher(2, new DateOnly(2026, 8, 28), 2500.5m));
        await cec.Store.PutAsync(Voucher(3, new DateOnly(2026, 9, 2), 900m));

        var created = (await cec.CallAsync("consultarComprobantesExpoPeriodo", Auth(cec) + $"<cuit>{Caller}</cuit><periodo>202608</periodo><pagina>1</pagina>")).Valid();
        Assert.Equal("TE", created.Value("estado"));
        Assert.Equal(["1", "2"], created.All("numero").Select(n => n.Value));
        Assert.Equal("2500.50", created.All("importeTotal")[1].Value);
        var code = created.Value("codigoConsulta");

        var read = (await cec.CallAsync("consultarComprobantesExpoCodigoConsulta", Auth(cec) + $"<codigoConsulta>{code}</codigoConsulta><pagina>1</pagina>")).Valid();
        Assert.Equal(2, read.All("comprobanteExportacion").Count);
        Assert.Equal("N", read.Value("hayMas"));

        var listed = (await cec.CallAsync("obtenerConsultas", Auth(cec) + $"<filtro><cuit>{Caller}</cuit></filtro><pagina>1</pagina>")).Valid();
        Assert.Equal([code], listed.All("codigoConsulta").Select(c => c.Value));
        Assert.Equal("202608", listed.Value("periodoDesde"));
    }

    [Theory]
    [InlineData("consultarComprobantesExpoPeriodo", "<cuit>20111111112</cuit><periodo>202610</periodo><pagina>1</pagina>", "errores", "4016",
        "Periodo de consulta igual al actual. El periodo actual 202610 todavía no venció. Vence el día 5 del mes que le sigue.")]
    [InlineData("consultarComprobantesExpoPeriodo", "<cuit>20111111112</cuit><periodo>999912</periodo><pagina>1</pagina>", "errores", "4016",
        "Periodo de consulta igual al actual. El periodo actual 999912 todavía no venció. Vence el día 5 del mes que le sigue.")]
    [InlineData("consultarComprobantesExpoPeriodo", "<cuit>20111111112</cuit><periodo>202613</periodo><pagina>1</pagina>", "erroresFormato", "2009",
        "El periodo de consulta debe respetar el formato 'YYYYMM', con año (YYYY) y mes (MM) válidos. El mes 13 no es válido.")]
    [InlineData("consultarComprobantesExpoPeriodo", "<cuit>20222222223</cuit><periodo>202608</periodo><pagina>1</pagina>", "errores", "4009",
        "La CUIT no se encuentra en los Registros de AFIP. '20222222223' no está en el padrón.")]
    [InlineData("consultarComprobantesExpoCodigoConsulta", "<codigoConsulta>999</codigoConsulta><pagina>1</pagina>", "errores", "4010",
        "No se encontraron datos. No existe consulta con código 999.")]
    [InlineData("obtenerConsultas", "<filtro/><pagina>1</pagina>", "errores", "2004",
        "Faltan campos. Si 'cuit' está vacío, se deben indicar 'fechaDesde' y 'fechaHasta'.")]
    [InlineData("obtenerConsultas", "<filtro><fechaDesde>2026-08-01</fechaDesde><fechaHasta>2026-09-30</fechaHasta></filtro><pagina>1</pagina>", "errores", "2003",
        "Rango de fechas inválido. La diferencia entre 2026-09-30 y 2026-08-01 son 60 días: el intervalo máximo de dias es 31.")]
    public async Task What_the_manual_forbids_comes_back_in_its_error_block(string operation, string inner, string block, string code, string text)
    {
        await using var sim = ArcaSimHarness.Start();
        var cec = await ServiceProbe.StartAsync(sim, "wscec");

        var answer = (await cec.CallAsync(operation, Auth(cec) + inner)).Valid();

        var error = answer.All(block).Single();
        Assert.Equal(code, error.Value("codigo"));
        Assert.Equal(text, error.Value("descripcion"));
        Assert.Equal("0", answer.Value("pagina"));
    }

    // pagina is an xsd:short, so a conformant client never sends the larger two: they are not valid for the
    // schema and neither is the answer, but they have to read as "past the last", not wrap around to the first.
    [Theory]
    [InlineData(2)]
    [InlineData(60_000_000)] // (page - 1) * 50 no longer fits an int
    [InlineData(int.MaxValue)]
    public async Task A_page_past_the_last_is_empty_and_has_no_more_however_large_the_number(int page)
    {
        await using var sim = ArcaSimHarness.Start();
        var cec = await ServiceProbe.StartAsync(sim, "wscec");
        await cec.Store.PutAsync(Voucher(1, new DateOnly(2026, 8, 10), 1500m));
        var created = (await cec.CallAsync("consultarComprobantesExpoPeriodo", Auth(cec) + $"<cuit>{Caller}</cuit><periodo>202608</periodo><pagina>1</pagina>")).Valid();
        var code = created.Value("codigoConsulta");

        var read = (await cec.CallAsync("consultarComprobantesExpoCodigoConsulta", Auth(cec) + $"<codigoConsulta>{code}</codigoConsulta><pagina>{page}</pagina>")).Element;
        var again = (await cec.CallAsync("consultarComprobantesExpoPeriodo", Auth(cec) + $"<cuit>{Caller}</cuit><periodo>202608</periodo><pagina>{page}</pagina>")).Element;
        var listed = (await cec.CallAsync("obtenerConsultas", Auth(cec) + $"<filtro><cuit>{Caller}</cuit></filtro><pagina>{page}</pagina>")).Element;

        foreach (var answer in new[] { read, again })
        {
            Assert.Empty(answer.All("comprobanteExportacion"));
            Assert.Equal((page.ToString(), "N"), (answer.Value("pagina"), answer.Value("hayMas")));
        }
        Assert.Empty(listed.All("consulta"));
        Assert.Equal((page.ToString(), "N"), (listed.Value("pagina"), listed.Value("hayMas")));
    }

    private static AuthorizedVoucher Voucher(long number, DateOnly date, decimal total) =>
        new("wsfexv1", Caller, 5, 19, number, date, total, 80, 50000000016, "CAE", "76123456789012", date.AddDays(10));

    private static string Auth(ServiceProbe cec) =>
        $"<autenticacion><token>{cec.Token}</token><sign>{cec.Sign}</sign><cuitRepresentada>{Caller}</cuitRepresentada></autenticacion>";
}
