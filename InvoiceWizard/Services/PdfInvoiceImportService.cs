using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace InvoiceWizard.Services;

public class PdfInvoiceImportResult
{
    public string InvoiceNumber { get; set; } = "";
    public DateTime? InvoiceDate { get; set; }
    public string SupplierName { get; set; } = "";
    public string RawText { get; set; } = "";
}

public static partial class PdfInvoiceImportService
{
    public static PdfInvoiceImportResult Parse(string pdfPath)
    {
        var lines = new List<string>();
        using var document = PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            lines.AddRange(text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        var result = new PdfInvoiceImportResult
        {
            RawText = string.Join(Environment.NewLine, lines)
        };

        result.SupplierName = DetectSupplierName(lines);
        result.InvoiceNumber = DetectInvoiceNumber(result.RawText);
        result.InvoiceDate = DetectInvoiceDate(result.RawText);
        return result;
    }

    private static string DetectSupplierName(IEnumerable<string> lines)
    {
        return lines
            .Select(x => x.Trim())
            .FirstOrDefault(x => x.Length > 3 && x.Any(char.IsLetter) && !x.Contains("rechnung", StringComparison.OrdinalIgnoreCase))
            ?? "";
    }

    private static string DetectInvoiceNumber(string text)
    {
        var invoiceMatch = InvoiceNumberRegex().Match(text);
        if (invoiceMatch.Success)
        {
            return invoiceMatch.Groups["number"].Value.Trim();
        }

        return "";
    }

    private static DateTime? DetectInvoiceDate(string text)
    {
        foreach (Match match in DateRegex().Matches(text))
        {
            var value = match.Value.Trim();
            if (DateTime.TryParseExact(value, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    [GeneratedRegex(@"(?im)(?:rechnungs(?:nummer|nr\.?)|invoice(?:\s*no\.?)?)\s*[:#]?\s*(?<number>[A-Z0-9\-\/]+)", RegexOptions.Compiled)]
    private static partial Regex InvoiceNumberRegex();

    [GeneratedRegex(@"\b\d{1,2}\.\d{1,2}\.\d{4}\b|\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex DateRegex();
}
