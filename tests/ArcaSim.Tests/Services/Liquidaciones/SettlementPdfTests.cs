using System.Text;
using ArcaSim.Application.Services.Liquidaciones;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>The one-page PDF the settlement services attach: its bytes are what clients have always received.</summary>
public class SettlementPdfTests
{
    [Fact]
    public void The_page_lists_the_title_and_a_line_per_entry_with_what_a_PDF_string_needs_escaped()
    {
        var pdf = Decode(SimplePdf.Lines("ARCA - Liquidacion 150-00001-00000001", ["CUIT emisor: 20111111112", "Nota (a) \\ ñ"]));

        Assert.Equal(
            "%PDF-1.4\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n" +
            "4 0 obj\n<< /Length 127 >>\nstream\nBT /F1 11 Tf 50 800 Td 14 TL\n(ARCA - Liquidacion 150-00001-00000001) '\n() '\n(CUIT emisor: 20111111112) '\n(Nota \\(a\\) \\\\ ?) '\nET\nendstream\nendobj\n" +
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n" +
            "xref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000241 00000 n \n0000000419 00000 n \n" +
            "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n489\n%%EOF\n",
            pdf);
    }

    [Fact]
    public void A_single_line_page_escapes_what_a_PDF_string_needs_escaped_and_ends_without_a_line_break()
    {
        var pdf = Decode(SimplePdf.Line("COE (3301) \\ ñ"));

        Assert.Contains("BT /F1 12 Tf 72 770 Td (COE \\(3301\\) \\\\ ?) Tj ET", pdf);
        Assert.EndsWith("%%EOF", pdf);
    }

    private static string Decode(string base64) => Encoding.ASCII.GetString(Convert.FromBase64String(base64));
}
