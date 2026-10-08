using System.Text;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.Ultimos;

/// <summary>
/// The PDF of a certificate nobody loaded one for: one A4 page with the
/// certificate's number, domain, seller and procedure that says it is a
/// fictitious ArcaSim document. Plain PDF 1.4 with the standard Helvetica in
/// Latin-1, made by SimplePdf.Build, in base64 as getCertificadoPDF sends it.
/// </summary>
public static class CertificatePdf
{
    public static string Render(TransferCertificate certificate)
    {
        string[] lines =
        [
            "CERTIFICADO DE TRANSFERENCIA DE AUTOMOTORES (F.381)",
            "Documento ficticio generado por ArcaSim: no es un certificado de ARCA.",
            $"Certificado: {certificate.Number}",
            $"Dominio: {certificate.Domain}",
            $"CUIT solicitante: {certificate.Applicant}",
            $"Tramite: {certificate.Procedure}",
        ];
        var content = "BT\n/F1 11 Tf\n16 TL\n56 800 Td\n" + string.Concat(lines.Select(line => $"({Escape(line)}) '\n")) + "ET\n";
        return SimplePdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}endstream",
        ], Encoding.Latin1);
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
