using System.Security.Cryptography;
using System.Text;

namespace ArcaSim.Tests.Services.Organismos;

/// <summary>Presentación de DDJJ: upload gives a transaction number, the same one for the same file, and consulta finds it.</summary>
public class DdjjRulesTests
{
    private static readonly byte[] Content = Encoding.ASCII.GetBytes("F2002 DDJJ ficticia de ArcaSim\n");
    private static readonly string Md5 = Convert.ToHexString(MD5.HashData(Content)).ToLowerInvariant();

    [Fact]
    public async Task An_upload_gets_a_transaction_that_consulta_finds_and_the_same_file_keeps()
    {
        await using var sim = ArcaSimHarness.Start();
        var ddjj = await ServiceProbe.StartAsync(sim, "uploadPresentacionService", "djprocessorcontribuyente");
        var fileName = $"F2002.{Md5}.txt";

        var transaction = long.Parse((await UploadAsync(ddjj, fileName, Content)).Valid().Value("return"));
        Assert.True(transaction > 60_000_000);

        var found = (await ConsultaAsync(ddjj, fileName, 2002, Md5)).Valid();
        Assert.Equal(transaction.ToString(), found.Value("numeroTransaccion"));
        Assert.Equal("2026-10-01 12:00:00", found.Value("fechaHoraPresentacion"));
        Assert.Equal(ServiceProbe.Caller.ToString(), found.Value("contribuyenteCuit"));

        Assert.Equal(transaction.ToString(), (await UploadAsync(ddjj, fileName, Content)).Valid().Value("return"));
        var other = (await UploadAsync(ddjj, "951616F0159.dat", Content)).Valid().Value("return");
        Assert.NotEqual(transaction.ToString(), other);
    }

    [Fact]
    public async Task A_file_name_without_a_valid_format_or_a_wrong_md5_is_a_business_error()
    {
        await using var sim = ArcaSimHarness.Start();
        var ddjj = await ServiceProbe.StartAsync(sim, "uploadPresentacionService", "presentacionprocessor");

        var badName = await UploadAsync(ddjj, "F05680.txt", Content);
        Assert.Equal(500, badName.Status);
        Assert.Equal("soap:User.businessError", badName.FaultCode);
        Assert.Equal("El nombre del archivo [F05680.txt] no corresponde con ningún formato válido", badName.Fault);

        var badMd5 = await UploadAsync(ddjj, $"F2002.{new string('0', 32)}.txt", Content);
        Assert.Equal("Archivo adjunto inválido", badMd5.Fault);
    }

    [Fact]
    public async Task Consulta_of_a_file_never_presented_is_archivo_inexistente()
    {
        await using var sim = ArcaSimHarness.Start();
        var ddjj = await ServiceProbe.StartAsync(sim, "uploadPresentacionService", "djprocessorcontribuyente");

        var answer = await ConsultaAsync(ddjj, $"F2002.{Md5}.txt", 2002, Md5);

        Assert.Equal(500, answer.Status);
        Assert.Equal($"Archivo inexistente. Parámetros: contribuyenteCuit [{ServiceProbe.Caller}] fileName [F2002.{Md5}.txt] formulario [2002] md5 [{Md5}]", answer.Fault);
    }

    private static Task<SoapAnswer> UploadAsync(ServiceProbe ddjj, string fileName, byte[] content) =>
        ddjj.CallAsync("upload", Auth(ddjj) +
                                 $"<presentacion><presentacionDataHandler>{Convert.ToBase64String(content)}</presentacionDataHandler><fileName>{fileName}</fileName></presentacion>");

    private static Task<SoapAnswer> ConsultaAsync(ServiceProbe ddjj, string fileName, int form, string md5) =>
        ddjj.CallAsync("consulta", Auth(ddjj) + $"<fileName>{fileName}</fileName><formulario>{form}</formulario>" +
                                   $"<contribuyenteCuit>{ServiceProbe.Caller}</contribuyenteCuit><md5>{md5}</md5>");

    private static string Auth(ServiceProbe ddjj) =>
        $"<token>{ddjj.Token}</token><sign>{ddjj.Sign}</sign><representadoCuit>{ServiceProbe.Caller}</representadoCuit>";
}
