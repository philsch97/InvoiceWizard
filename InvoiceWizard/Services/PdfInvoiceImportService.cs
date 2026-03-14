using System.Globalization;
using System.IO;
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

        result.SupplierName = DetectSupplierName(lines, result.RawText, pdfPath);
        result.InvoiceNumber = DetectInvoiceNumber(lines, result.RawText, pdfPath);
        result.InvoiceDate = DetectInvoiceDate(lines, result.RawText, pdfPath);
        return result;
    }

    private static string DetectSupplierName(IReadOnlyCollection<string> lines, string text, string pdfPath)
    {
        foreach (var knownSupplier in KnownSuppliers)
        {
            if (ContainsIgnoreCase(text, knownSupplier.Key) || ContainsIgnoreCase(pdfPath, knownSupplier.Key))
            {
                return knownSupplier.Value;
            }
        }

        var supplierLine = lines
            .Select(NormalizeWhitespace)
            .Where(IsSupplierCandidate)
            .Select(line => new { Line = line, Score = ScoreSupplierCandidate(line) })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Line)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplierLine))
        {
            return supplierLine;
        }

        var directoryHints = pdfPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Reverse()
            .FirstOrDefault(segment => segment.Any(char.IsLetter) && !Path.HasExtension(segment));
        return NormalizeDirectoryHint(directoryHints);
    }

    private static string DetectInvoiceNumber(IEnumerable<string> lines, string text, string pdfPath)
    {
        foreach (var regex in InvoiceNumberRegexes)
        {
            var invoiceMatch = regex.Match(text);
            if (invoiceMatch.Success)
            {
                return invoiceMatch.Groups["number"].Value.Trim();
            }
        }

        foreach (var line in lines.Select(NormalizeWhitespace))
        {
            var labeledMatch = InvoiceNumberLineRegex().Match(line);
            if (labeledMatch.Success)
            {
                return labeledMatch.Groups["number"].Value.Trim();
            }
        }

        var fileNameMatch = FileNamePatternRegex().Match(Path.GetFileNameWithoutExtension(pdfPath));
        if (fileNameMatch.Success)
        {
            return fileNameMatch.Groups["number"].Value.Trim();
        }

        return "";
    }

    private static DateTime? DetectInvoiceDate(IEnumerable<string> lines, string text, string pdfPath)
    {
        foreach (var regex in LabeledDateRegexes)
        {
            var labeledMatch = regex.Match(text);
            if (labeledMatch.Success && TryParseDate(labeledMatch.Groups["date"].Value, out var labeledDate))
            {
                return labeledDate;
            }
        }

        foreach (var line in lines.Select(NormalizeWhitespace))
        {
            var lineMatch = DateLineRegex().Match(line);
            if (lineMatch.Success && TryParseDate(lineMatch.Groups["date"].Value, out var lineDate))
            {
                return lineDate;
            }
        }

        foreach (Match match in DateRegex().Matches(text))
        {
            var value = match.Value.Trim();
            if (TryParseDate(value, out var date))
            {
                return date;
            }
        }

        var fileNameMatch = FileNamePatternRegex().Match(Path.GetFileNameWithoutExtension(pdfPath));
        if (fileNameMatch.Success && TryParseDate(fileNameMatch.Groups["date"].Value, out var fileNameDate))
        {
            return fileNameDate;
        }

        return null;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value.Trim(),
            ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "yyyyMMdd"],
            CultureInfo.GetCultureInfo("de-DE"),
            DateTimeStyles.None,
            out date);
    }

    private static bool IsSupplierCandidate(string line)
    {
        if (line.Length < 4 || !line.Any(char.IsLetter))
        {
            return false;
        }

        var normalized = line.ToLowerInvariant();
        if (normalized.Contains("rechnung")
            || normalized.Contains("lieferschein")
            || normalized.Contains("auftragsnummer")
            || normalized.Contains("kundennummer")
            || normalized.Contains("seite ")
            || normalized.Contains("ust-id")
            || normalized.Contains("telefon")
            || normalized.Contains("fax")
            || normalized.Contains("mail")
            || normalized.Contains("www.")
            || normalized.Contains("iban")
            || normalized.Contains("bic")
            || normalized.Contains("summe")
            || normalized.Contains("brutto")
            || normalized.Contains("netto"))
        {
            return false;
        }

        return normalized.Contains("gmbh")
            || normalized.Contains("kg")
            || normalized.Contains("ag")
            || normalized.Contains("ug")
            || normalized.Contains("gbr")
            || normalized.Contains("sonepar")
            || normalized.Contains("wuerth")
            || normalized.Contains("würth")
            || normalized.Contains("amazon")
            || normalized.Contains("hornbach")
            || normalized.Contains("bauhaus")
            || normalized.Contains("obeta")
            || normalized.Contains("rexel");
    }

    private static int ScoreSupplierCandidate(string line)
    {
        var normalized = line.ToLowerInvariant();
        var score = 0;
        if (normalized.Contains("gmbh")) score += 30;
        if (normalized.Contains("kg")) score += 20;
        if (normalized.Contains("ag")) score += 20;
        if (normalized.Contains("sonepar")) score += 80;
        if (normalized.Contains("rexel")) score += 80;
        if (normalized.Contains("wuerth") || normalized.Contains("würth")) score += 80;
        if (normalized.Contains("amazon")) score += 70;
        score += Math.Min(line.Length, 60);
        return score;
    }

    private static string NormalizeDirectoryHint(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "";
        }

        foreach (var knownSupplier in KnownSuppliers)
        {
            if (ContainsIgnoreCase(segment, knownSupplier.Key))
            {
                return knownSupplier.Value;
            }
        }

        return segment.Trim();
    }

    private static string NormalizeWhitespace(string value)
        => WhitespaceRegex().Replace(value.Trim(), " ");

    private static bool ContainsIgnoreCase(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static readonly Regex[] InvoiceNumberRegexes =
    [
        InvoiceNumberRegex(),
        AltInvoiceNumberRegex(),
        CompactInvoiceNumberRegex()
    ];

    private static readonly Regex[] LabeledDateRegexes =
    [
        LabeledDateRegex(),
        AltLabeledDateRegex()
    ];

    private static readonly Dictionary<string, string> KnownSuppliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sonepar"] = "Sonepar Deutschland/ Region Sued GmbH",
        ["rexel"] = "Rexel Germany GmbH & Co. KG",
        ["wuerth"] = "Adolf Wuerth GmbH & Co. KG",
        ["würth"] = "Adolf Wuerth GmbH & Co. KG",
        ["amazon"] = "Amazon",
        ["hornbach"] = "Hornbach",
        ["bauhaus"] = "BAUHAUS",
        ["obeta"] = "OBETA"
    };

    [GeneratedRegex(@"(?im)(?:rechnungs(?:nummer|nr\.?)|invoice(?:\s*no\.?)?|rg\.?\s*nr\.?)\s*[:#]?\s*(?<number>[A-Z0-9][A-Z0-9\-\/\.]{5,})", RegexOptions.Compiled)]
    private static partial Regex InvoiceNumberRegex();

    [GeneratedRegex(@"(?im)(?:beleg(?:nummer|nr\.?)|dokument(?:nummer|nr\.?)|faktura(?:nummer|nr\.?)?)\s*[:#]?\s*(?<number>[A-Z0-9][A-Z0-9\-\/\.]{5,})", RegexOptions.Compiled)]
    private static partial Regex AltInvoiceNumberRegex();

    [GeneratedRegex(@"(?im)(?:rechnung)\D{0,15}(?<number>\d{6,})", RegexOptions.Compiled)]
    private static partial Regex CompactInvoiceNumberRegex();

    [GeneratedRegex(@"(?im)(?:rechnungsdatum|datum|belegdatum|invoice date)\s*[:#]?\s*(?<date>\d{1,2}\.\d{1,2}\.\d{4}|\d{4}-\d{2}-\d{2}|\d{8})", RegexOptions.Compiled)]
    private static partial Regex LabeledDateRegex();

    [GeneratedRegex(@"(?im)(?:leistungsdatum|erstellt am)\s*[:#]?\s*(?<date>\d{1,2}\.\d{1,2}\.\d{4}|\d{4}-\d{2}-\d{2}|\d{8})", RegexOptions.Compiled)]
    private static partial Regex AltLabeledDateRegex();

    [GeneratedRegex(@"(?im)(?:rechnungs(?:nummer|nr\.?)|beleg(?:nummer|nr\.?)|dokument(?:nummer|nr\.?)|rg\.?\s*nr\.?)\s*[:#]?\s*(?<number>[A-Z0-9][A-Z0-9\-\/\.]{5,})", RegexOptions.Compiled)]
    private static partial Regex InvoiceNumberLineRegex();

    [GeneratedRegex(@"(?im)(?:rechnungsdatum|datum|belegdatum|leistungsdatum|erstellt am)\s*[:#]?\s*(?<date>\d{1,2}\.\d{1,2}\.\d{4}|\d{4}-\d{2}-\d{2}|\d{8})", RegexOptions.Compiled)]
    private static partial Regex DateLineRegex();

    [GeneratedRegex(@"^(?<number>\d{6,})[_-](?<date>\d{8})$", RegexOptions.Compiled)]
    private static partial Regex FileNamePatternRegex();

    [GeneratedRegex(@"\b\d{1,2}\.\d{1,2}\.\d{4}\b|\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
