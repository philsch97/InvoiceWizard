using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace InvoiceWizard.Backend.Services;

internal sealed class BankStatementCsvParser
{
    private static readonly string[] CandidateDelimiters = [";", ",", "\t"];
    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bookingDate"] = ["buchungstag", "buchungsdatum", "buchung", "datum", "bookingdate", "date"],
        ["valueDate"] = ["valutadatum", "wertstellung", "valuedate"],
        ["amount"] = ["betrag", "umsatz", "amount"],
        ["balance"] = ["saldo", "kontostand", "balance"],
        ["currency"] = ["waehrung", "wahrung", "currency"],
        ["purpose"] = ["verwendungszweck", "buchungstext", "purpose", "beschreibung", "textschluessel", "buchungstextauftraggeber"],
        ["counterparty"] = ["beguenstigterzahlungspflichtiger", "zahlungspflichtigerbeguenstigter", "auftraggeberbeguenstigter", "auftraggeber", "beguenstigter", "empfaenger", "name"],
        ["counterpartyIban"] = ["kontonummeriban", "gegenkonto", "gegenkontoiban", "iban"],
        ["reference"] = ["referenz", "kundenreferenz", "mandatsreferenz", "endtoendref", "sammlerreferenz", "glaeubigerid"],
        ["type"] = ["umsatzart", "typ", "buchungstyp"],
        ["accountIban"] = ["auftragskonto", "konto", "accountiban"]
    };

    public ParsedBankStatement Parse(byte[] fileBytes, string fileName)
    {
        var text = DecodeText(fileBytes);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var (delimiter, headerIndex, headers) = FindHeader(lines);
        if (headerIndex < 0)
        {
            throw new InvalidOperationException("Die CSV-Datei konnte nicht erkannt werden. Bitte einen Sparkasse- oder Standard-CSV-Kontoauszug mit Kopfzeile importieren.");
        }

        var accountName = ExtractPreHeaderValue(lines, "Kontobezeichnung");
        var import = new ParsedBankStatement
        {
            FileName = fileName,
            AccountName = accountName
        };

        using var parser = CreateParser(text, delimiter);
        for (var skip = 0; skip <= headerIndex && !parser.EndOfData; skip++)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            string[]? row;
            try
            {
                row = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                import.Warnings.Add("Eine fehlerhafte CSV-Zeile wurde uebersprungen.");
                continue;
            }

            if (row is null || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var data = ToRowDictionary(headers, row);
            if (!TryParseDate(Get(data, "bookingDate"), out var bookingDate) || !TryParseAmount(Get(data, "amount"), out var amount))
            {
                continue;
            }

            TryParseDate(Get(data, "valueDate"), out var valueDate);
            TryParseAmount(Get(data, "balance"), out var balance);

            var accountIban = FirstNonEmpty(Get(data, "accountIban"), import.AccountIban);
            if (!string.IsNullOrWhiteSpace(accountIban))
            {
                import.AccountIban = accountIban;
            }

            var currency = FirstNonEmpty(Get(data, "currency"), import.Currency, "EUR");
            import.Currency = currency;

            var item = new ParsedBankTransaction
            {
                BookingDate = bookingDate,
                ValueDate = valueDate,
                Amount = amount,
                BalanceAfterBooking = balance,
                Currency = currency,
                CounterpartyName = Get(data, "counterparty"),
                CounterpartyIban = Get(data, "counterpartyIban"),
                Purpose = Get(data, "purpose"),
                Reference = Get(data, "reference"),
                TransactionType = Get(data, "type"),
                AccountIban = accountIban,
            };

            item.ContentHash = BuildContentHash(item);
            import.Transactions.Add(item);
        }

        if (string.IsNullOrWhiteSpace(import.AccountName))
        {
            import.AccountName = import.AccountIban;
        }

        return import;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static (string delimiter, int headerIndex, Dictionary<string, List<int>> headers) FindHeader(string[] lines)
    {
        for (var i = 0; i < Math.Min(lines.Length, 20); i++)
        {
            foreach (var delimiter in CandidateDelimiters)
            {
                var fields = SplitCsvLine(lines[i], delimiter);
                if (fields.Count < 3)
                {
                    continue;
                }

                var map = BuildHeaderMap(fields);
                if (map.ContainsKey("bookingDate") && map.ContainsKey("amount"))
                {
                    return (delimiter, i, map);
                }
            }
        }

        return (";", -1, new Dictionary<string, List<int>>());
    }

    private static Dictionary<string, List<int>> BuildHeaderMap(IReadOnlyList<string> fields)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < fields.Count; index++)
        {
            var normalized = NormalizeHeader(fields[index]);
            foreach (var pair in HeaderAliases)
            {
                if (pair.Value.Any(alias => normalized.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!result.TryGetValue(pair.Key, out var indexes))
                    {
                        indexes = [];
                        result[pair.Key] = indexes;
                    }

                    indexes.Add(index);
                }
            }
        }

        return result;
    }

    private static TextFieldParser CreateParser(string text, string delimiter)
    {
        var parser = new TextFieldParser(new StringReader(text))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(delimiter);
        return parser;
    }

    private static List<string> SplitCsvLine(string line, string delimiter)
    {
        using var parser = CreateParser(line, delimiter);
        return parser.EndOfData ? [] : (parser.ReadFields() ?? []).ToList();
    }

    private static Dictionary<string, string> ToRowDictionary(Dictionary<string, List<int>> headers, IReadOnlyList<string> row)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            var values = header.Value
                .Where(index => index < row.Count)
                .Select(index => row[index]?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.Equals(header.Key, "purpose", StringComparison.OrdinalIgnoreCase))
            {
                var bookingText = values.FirstOrDefault(value => value.Equals(value.ToUpperInvariant(), StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(bookingText))
                {
                    values.Remove(bookingText);
                    values.Insert(0, bookingText);
                }
            }

            result[header.Key] = string.Join(" | ", values);
        }

        return result;
    }

    private static string Get(Dictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static bool TryParseDate(string? value, out DateTime result)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (DateTime.TryParseExact(trimmed, ["dd.MM.yyyy", "dd.MM.yy", "yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out result))
        {
            return true;
        }

        return DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out result)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool TryParseAmount(string? value, out decimal result)
    {
        var trimmed = (value ?? string.Empty).Trim().Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Trim();
        var isDebitMarker = trimmed.EndsWith("S", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith("D", StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.TrimEnd('H', 'h', 'S', 's', 'C', 'c', 'D', 'd').Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("de-DE"), out result)
            || decimal.TryParse(trimmed, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
        {
            if (isDebitMarker && result > 0m)
            {
                result *= -1m;
            }

            return true;
        }

        return false;
    }

    private static string BuildContentHash(ParsedBankTransaction item)
    {
        var raw = string.Join("|",
            item.BookingDate.ToString("yyyyMMdd"),
            item.ValueDate?.ToString("yyyyMMdd") ?? "",
            item.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            item.CounterpartyName.Trim(),
            item.CounterpartyIban.Trim(),
            item.Purpose.Trim(),
            item.Reference.Trim(),
            item.AccountIban.Trim());

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeHeader(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(", "", StringComparison.OrdinalIgnoreCase)
            .Replace(")", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static string ExtractPreHeaderValue(IEnumerable<string> lines, string label)
    {
        foreach (var line in lines.Take(10))
        {
            var parts = line.Split(';', 2);
            if (parts.Length == 2 && parts[0].Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim();
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}

internal sealed class ParsedBankStatement
{
    public string FileName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountIban { get; set; } = "";
    public string Currency { get; set; } = "EUR";
    public decimal? CurrentBalance { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<ParsedBankTransaction> Transactions { get; set; } = [];
}

internal sealed class ParsedBankTransaction
{
    public DateTime BookingDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal? BalanceAfterBooking { get; set; }
    public string Currency { get; set; } = "EUR";
    public string CounterpartyName { get; set; } = "";
    public string CounterpartyIban { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Reference { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string AccountIban { get; set; } = "";
    public string ContentHash { get; set; } = "";
}
