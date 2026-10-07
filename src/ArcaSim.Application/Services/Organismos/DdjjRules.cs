using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArcaSim.Application.Contracts;
using SoapFault = ArcaSim.Application.Soap.SoapFault;

namespace ArcaSim.Application.Services.Organismos;

/// <summary>A sworn statement presented through the web service, under its transaction number.</summary>
public sealed record Presentation(long Transaction, long Cuit, int Form, string FileName, string Md5, long Size, DateTimeOffset PresentedAt);

/// <summary>
/// Presentación de DDJJ (uploadPresentacionService,
/// docs/arca/servicios/uploadPresentacionService.md): upload takes the file,
/// checks its name and its MD5, and gives a transaction number, the same one
/// again when the same file of the same taxpayer comes back; consulta finds
/// the presentation by taxpayer, file name, form and MD5, or answers "Archivo
/// inexistente". ArcaSim's choices: the file name is
/// [digits]F&lt;3 or 4 digit form&gt;[.&lt;md5&gt;].&lt;extension&gt; (the
/// manual's examples; each form's interface document is not public);
/// the taxpayer of an upload is representadoCuit; the file travels inline as
/// base64 (the engine does not read MTOM) and over 1 MB inline is "413
/// Request Entity Too Large"; transaction numbers start at 60000001; the
/// presentation is processed at once; business faults are
/// soap:User.businessError with HTTP 500 (the manual does not say).
/// </summary>
public sealed partial class DdjjRules(IDocumentStore store, IClock clock) : IServiceBehavior
{
    public const string Presentations = "uploadPresentacionService.presentaciones";
    public const int InlineLimit = 1024 * 1024;
    private const long FirstTransaction = 60_000_000;

    /// <summary>The same file uploaded twice at once is one presentation: the second request waits for the first one's transaction.</summary>
    private readonly KeyedLocks<string> _uploads = new();

    [GeneratedRegex(@"^\d*F(?<form>\d{3,4})(\.(?<md5>[0-9a-fA-F]{32}))?\.[A-Za-z0-9]{1,4}$")]
    private static partial Regex FileNameFormat();

    public string Service => "uploadPresentacionService";

    public async Task<ContractAnswer?> AnswerAsync(ServiceCall call, CancellationToken ct) => call.Name switch
    {
        "upload" => await UploadAsync(call, ct),
        "consulta" => await FindAsync(call, ct),
        _ => null,
    };

    private async Task<ContractAnswer> UploadAsync(ServiceCall call, CancellationToken ct)
    {
        var presentation = call.Request.Find("presentacion") ?? new XElement("presentacion");
        var fileName = presentation.Text("fileName") ?? "";
        var match = FileNameFormat().Match(fileName);
        if (!match.Success) return Business($"El nombre del archivo [{fileName}] no corresponde con ningún formato válido");

        var handler = presentation.Find("presentacionDataHandler");
        if (handler is null || handler.HasElements) return Business("Archivo adjunto inválido");
        var text = handler.Value.Trim();
        if (text.Length / 4 * 3 > InlineLimit) return Business("413 Request Entity Too Large");
        byte[] content;
        try { content = Convert.FromBase64String(text); }
        catch (FormatException) { return Business("Archivo adjunto inválido"); }
        if (content.Length == 0) return Business("Archivo adjunto inválido");

        // MD5 because the contract says so: the file name carries the file's MD5 and consulta asks for it
        // (uploadPresentacionService.md). It identifies the file; it protects nothing.
        var md5 = Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();
        if (match.Groups["md5"].Success && !match.Groups["md5"].Value.Equals(md5, StringComparison.OrdinalIgnoreCase))
            return Business("Archivo adjunto inválido");

        var form = int.Parse(match.Groups["form"].Value, CultureInfo.InvariantCulture);
        var key = Key(call.Cuit, form, fileName, md5);
        using var uploading = await _uploads.AcquireAsync(key, ct);
        var existing = await store.GetAsync<Presentation>(Presentations, key, ct);
        if (existing is null)
        {
            existing = new Presentation(FirstTransaction + await store.NextAsync(Presentations, ct), call.Cuit, form, fileName, md5, content.LongLength, clock.Now);
            await store.PutAsync(Presentations, key, existing, ct);
        }
        return call.Ok(call.Sample().Set("return", existing.Transaction));
    }

    private async Task<ContractAnswer> FindAsync(ServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var cuit = request.Long("contribuyenteCuit");
        var fileName = request.Text("fileName") ?? "";
        var form = request.Int("formulario");
        var md5 = (request.Text("md5") ?? "").ToLowerInvariant();
        var found = await store.GetAsync<Presentation>(Presentations, Key(cuit, form, fileName, md5), ct);
        if (found is null)
            return Business($"Archivo inexistente. Parámetros: contribuyenteCuit [{cuit}] fileName [{fileName}] formulario [{form}] md5 [{request.Text("md5")}]");

        var answer = call.Sample()
            .Set("contribuyenteCuit", found.Cuit)
            .Set("fechaHoraPresentacion", found.PresentedAt.ToArgentina().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Set("fileName", found.FileName)
            .Set("formulario", found.Form)
            .Set("md5", found.Md5)
            .Set("numeroTransaccion", found.Transaction);
        return call.Ok(answer);
    }

    private static string Key(long cuit, int form, string fileName, string md5) => $"{cuit}/{form:D4}/{md5}/{fileName}";

    private static ContractAnswer Business(string message) => ContractAnswer.Failed(new SoapFault("User.businessError", message, Detail()));

    /// <summary>The detail's node number, as the manual's example prints it; the real one adds url, faultid and datetime.</summary>
    private static XElement Detail() => new("server", 67);
}
