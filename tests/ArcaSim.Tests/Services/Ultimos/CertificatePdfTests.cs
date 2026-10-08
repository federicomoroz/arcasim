using System.Text;
using ArcaSim.Application.Services.Ultimos;

namespace ArcaSim.Tests.Services.Ultimos;

/// <summary>The PDF getCertificadoPDF sends for a certificate nobody loaded one for: its bytes are what clients have always received.</summary>
public class CertificatePdfTests
{
    [Fact]
    public void The_page_is_Latin_1_with_the_certificates_lines_escaped()
    {
        var certificate = new TransferCertificate(4521, "AB123CD (ñ)", 20111111112, "Transferencia \\ dominio", "AC");

        var pdf = Encoding.Latin1.GetString(Convert.FromBase64String(CertificatePdf.Render(certificate)));

        Assert.Equal(
            "%PDF-1.4\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n" +
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n" +
            "5 0 obj\n<< /Length 286 >>\nstream\nBT\n/F1 11 Tf\n16 TL\n56 800 Td\n" +
            "(CERTIFICADO DE TRANSFERENCIA DE AUTOMOTORES \\(F.381\\)) '\n" +
            "(Documento ficticio generado por ArcaSim: no es un certificado de ARCA.) '\n" +
            "(Certificado: 4521) '\n" +
            "(Dominio: AB123CD \\(ñ\\)) '\n" +
            "(CUIT solicitante: 20111111112) '\n" +
            "(Tramite: Transferencia \\\\ dominio) '\n" +
            "ET\nendstream\nendobj\n" +
            "xref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000241 00000 n \n0000000338 00000 n \n" +
            "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n674\n%%EOF\n",
            pdf);
    }
}
