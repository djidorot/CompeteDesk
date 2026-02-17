using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CompeteDesk.Services.Exports;

/// <summary>
/// Minimal PDF writer for simple text exports.
/// No external dependencies. Produces a single-page PDF with Helvetica text.
/// This is sufficient for "export summary to PDF" MVP.
/// </summary>
public static class SimplePdfWriter
{
    public static byte[] CreateTextPdf(string title, IEnumerable<string> lines)
    {
        title ??= "Report";
        var safeLines = (lines ?? Array.Empty<string>()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        // Basic PDF structure.
        // We draw text using a single content stream.
        var content = BuildContentStream(title, safeLines);

        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        w.WriteLine("%PDF-1.4");

        var xref = new List<long>();

        void Obj(int id, string body)
        {
            xref.Add(ms.Position);
            w.WriteLine($"{id} 0 obj");
            w.WriteLine(body);
            w.WriteLine("endobj");
        }

        // 1: Catalog
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");

        // 2: Pages
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");

        // 3: Page
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");

        // 4: Font
        Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        // 5: Contents
        var contentBytes = Encoding.ASCII.GetBytes(content);
        Obj(5, $"<< /Length {contentBytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n{content}\nendstream");

        // xref table
        var xrefStart = ms.Position;
        w.Flush();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            bw.Write(Encoding.ASCII.GetBytes("xref\n"));
            bw.Write(Encoding.ASCII.GetBytes($"0 {xref.Count + 1}\n"));
            bw.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));

            foreach (var pos in xref)
            {
                bw.Write(Encoding.ASCII.GetBytes(pos.ToString("0000000000", CultureInfo.InvariantCulture)));
                bw.Write(Encoding.ASCII.GetBytes(" 00000 n \n"));
            }

            bw.Write(Encoding.ASCII.GetBytes("trailer\n"));
            bw.Write(Encoding.ASCII.GetBytes($"<< /Size {xref.Count + 1} /Root 1 0 R >>\n"));
            bw.Write(Encoding.ASCII.GetBytes("startxref\n"));
            bw.Write(Encoding.ASCII.GetBytes(xrefStart.ToString(CultureInfo.InvariantCulture)));
            bw.Write(Encoding.ASCII.GetBytes("\n%%EOF"));
        }

        return ms.ToArray();
    }

    private static string BuildContentStream(string title, IList<string> lines)
    {
        // PDF text operators.
        // Start at top margin, move down per line.
        var sb = new StringBuilder();
        sb.AppendLine("BT");
        sb.AppendLine("/F1 18 Tf");
        sb.AppendLine("72 740 Td");
        sb.AppendLine($"({Escape(title)}) Tj");

        sb.AppendLine("/F1 11 Tf");
        sb.AppendLine("0 -22 Td");
        sb.AppendLine($"({Escape("Generated: " + DateTime.UtcNow.ToString("u"))}) Tj");

        sb.AppendLine("0 -18 Td");

        var yStep = 14;
        foreach (var l in lines.Take(45))
        {
            sb.AppendLine($"({Escape(l)}) Tj");
            sb.AppendLine($"0 -{yStep} Td");
        }

        sb.AppendLine("ET");
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
