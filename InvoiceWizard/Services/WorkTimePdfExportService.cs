using System.Globalization;
using System.IO;
using System.Text;

namespace InvoiceWizard.Services;

public static class WorkTimePdfExportService
{
    public sealed class ExportRow
    {
        public string DateText { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public decimal HoursWorked { get; set; }
        public string Description { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    public static void Export(string filePath, string customerName, string? projectName, IEnumerable<ExportRow> rows)
    {
        var rowList = rows.ToList();
        var pageWidth = 595;
        var pageHeight = 842;
        var top = 800;
        var lineHeight = 14;
        var left = 40;
        var pages = BuildPages(customerName, projectName, rowList, left, top, lineHeight);
        var textEncoding = Encoding.Latin1;

        var objects = new List<string>();
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");

        var pageObjectIds = new List<int>();
        var contentObjectIds = new List<int>();
        for (var i = 0; i < pages.Count; i++)
        {
            var pageObjectId = 3 + i * 2;
            var contentObjectId = 4 + i * 2;
            pageObjectIds.Add(pageObjectId);
            contentObjectIds.Add(contentObjectId);
        }

        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        objects.Add($"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>");

        for (var i = 0; i < pages.Count; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> /F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >> >> /Contents {contentObjectIds[i]} 0 R >>");
            var streamBytes = textEncoding.GetBytes(pages[i]);
            objects.Add($"<< /Length {streamBytes.Length} >>\nstream\n{pages[i]}\nendstream");
        }

        WritePdf(filePath, objects, textEncoding);
    }

    private static List<string> BuildPages(string customerName, string? projectName, IReadOnlyList<ExportRow> rows, int left, int top, int lineHeight)
    {
        var pages = new List<string>();
        var currentPage = BuildHeader(left, top, customerName, projectName, rows);
        var currentY = top - 116;
        var index = 1;

        foreach (var row in rows)
        {
            var block = BuildRowLines(index, row);
            var neededHeight = block.Count * lineHeight + 14;
            if (currentY - neededHeight < 60)
            {
                pages.Add(string.Join("\n", currentPage));
                currentPage = BuildHeader(left, top, customerName, projectName, rows);
                currentY = top - 116;
            }

            currentPage.AddRange(BuildRowBlock(left, currentY, block, lineHeight));
            currentY -= neededHeight;
            index++;
        }

        if (rows.Count == 0)
        {
            currentPage.AddRange(BuildRowBlock(left, currentY, new List<string> { "Keine Arbeitszeiten vorhanden." }, lineHeight));
        }

        pages.Add(string.Join("\n", currentPage));
        return pages;
    }

    private static List<string> BuildHeader(int left, int top, string customerName, string? projectName, IReadOnlyList<ExportRow> rows)
    {
        var totalHours = rows.Sum(r => r.HoursWorked).ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        var lines = new List<string>
        {
            "BT",
            "/F2 20 Tf",
            $"{left} {top} Td",
            "(Elektro Schneider) Tj",
            "ET",
            "BT",
            "/F2 14 Tf",
            $"{left} {top - 26} Td",
            "(Arbeitszeitnachweis) Tj",
            "ET",
            "BT",
            "/F1 10 Tf",
            $"{left} {top - 48} Td",
            $"({Escape($"Kunde: {customerName}")}) Tj",
            "ET",
            "BT",
            "/F1 10 Tf",
            $"{left} {top - 62} Td",
            $"({Escape($"Projekt: {projectName ?? "Alle Projekte"}")}) Tj",
            "ET",
            "BT",
            "/F1 10 Tf",
            $"{left} {top - 76} Td",
            $"({Escape($"Erstellt am: {DateTime.Now:dd.MM.yyyy HH:mm}")}) Tj",
            "ET",
            "BT",
            "/F1 10 Tf",
            $"{left} {top - 90} Td",
            $"({Escape($"Gesamtstunden: {totalHours} h")}) Tj",
            "ET",
            $"{left} {top - 98} m 555 {top - 98} l S"
        };

        return lines;
    }

    private static List<string> BuildRowBlock(int left, int startY, IReadOnlyList<string> block, int lineHeight)
    {
        var commands = new List<string>();
        var currentY = startY;
        foreach (var line in block)
        {
            commands.Add("BT");
            commands.Add(line.StartsWith("   ") ? "/F1 9 Tf" : "/F1 10 Tf");
            commands.Add($"{left} {currentY} Td");
            commands.Add($"({Escape(line)}) Tj");
            commands.Add("ET");
            currentY -= lineHeight;
        }

        commands.Add($"{left} {currentY + 4} m 555 {currentY + 4} l S");
        return commands;
    }

    private static List<string> BuildRowLines(int index, ExportRow row)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var comment = string.IsNullOrWhiteSpace(row.Comment) ? "-" : row.Comment.Trim();
        var lines = new List<string>
        {
            $"{index}. {row.DateText} | {row.ProjectName}",
            $"   Zeit: {row.TimeRange} | Stunden: {row.HoursWorked.ToString("0.##", culture)} h",
            $"   Leistung: {Sanitize(row.Description)}"
        };

        lines.AddRange(WrapText($"   Kommentar: {Sanitize(comment)}", 95));
        return lines;
    }

    private static List<string> WrapText(string text, int maxLength)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length > maxLength)
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append("   ").Append(word);
            }
            else
            {
                current.Append(' ').Append(word);
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count > 0 ? lines : new List<string> { "" };
    }

    private static string Sanitize(string text)
    {
        return text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");
    }

    private static string Escape(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static void WritePdf(string filePath, IReadOnlyList<string> objects, Encoding textEncoding)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, textEncoding);
        writer.NewLine = "\n";

        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{i + 1} 0 obj");
            writer.WriteLine(objects[i]);
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (var i = 1; i < offsets.Count; i++)
        {
            writer.WriteLine($"{offsets[i]:D10} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPosition.ToString(CultureInfo.InvariantCulture));
        writer.Write("%%EOF");
        writer.Flush();
    }
}
