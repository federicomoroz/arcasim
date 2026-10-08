using System.Globalization;
using System.Text;

namespace ArcaSim.Application.Services.Liquidaciones;

/// <summary>
/// The one-page PDF the services attach to their answers (the manuals: "el mismo archivo que se
/// imprime por la aplicación web"). ArcaSim's is a page of text, enough for a client that stores or shows it.
/// The two layouts keep the bytes their services have always sent, which differ in the order of the page
/// dictionary's keys and in whether the file ends with a line break.
/// </summary>
public static class SimplePdf
{
    private const string ResourcesFirstPage =
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>";

    private const string ContentsFirstPage =
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>";

    /// <summary>A page with a title and the lines under it, one per line, in 11 points from the top left (the liquidation services).</summary>
    public static string Lines(string title, IEnumerable<string> lines)
    {
        var content = new StringBuilder("BT /F1 11 Tf 50 800 Td 14 TL\n");
        foreach (var line in new[] { title, "" }.Concat(lines)) content.Append('(').Append(Escape(line)).Append(") '\n");
        content.Append("ET");
        return Assemble(ResourcesFirstPage, content.ToString(), "%%EOF\n");
    }

    /// <summary>A page with one line of text, in 12 points (wscpe and wslpg).</summary>
    public static string Line(string text) => Assemble(ContentsFirstPage, $"BT /F1 12 Tf 72 770 Td ({Escape(text)}) Tj ET", "%%EOF");

    private static string Assemble(string page, string content, string end)
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            page,
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append('\n').Append(end);
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(pdf.ToString()));
    }

    /// <summary>What a PDF string can hold: the characters past the ASCII are lost, and the backslash and the parentheses are escaped.</summary>
    private static string Escape(string text) => new string(text.Select(c => c > 126 ? '?' : c).ToArray())
        .Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
