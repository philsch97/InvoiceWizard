using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace InvoiceWizard.Backend.Services;

internal sealed class BankStatementCamtParser
{
    public ParsedBankStatement Parse(byte[] fileBytes, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".zip" => ParseZip(fileBytes, fileName),
            ".xml" => ParseXml(fileBytes, fileName),
            _ => throw new InvalidOperationException("Nur ZIP- oder XML-Dateien im CAMT-Format werden unterstuetzt.")
        };
    }

    private ParsedBankStatement ParseZip(byte[] fileBytes, string fileName)
    {
        using var stream = new MemoryStream(fileBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var result = new ParsedBankStatement { FileName = fileName };

        foreach (var entry in archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            entryStream.CopyTo(memory);
            var parsed = ParseXml(memory.ToArray(), entry.FullName);

            if (string.IsNullOrWhiteSpace(result.AccountIban))
            {
                result.AccountIban = parsed.AccountIban;
            }

            if (string.IsNullOrWhiteSpace(result.AccountName))
            {
                result.AccountName = parsed.AccountName;
            }

            if (string.IsNullOrWhiteSpace(result.Currency))
            {
                result.Currency = parsed.Currency;
            }

            if (parsed.CurrentBalance.HasValue)
            {
                result.CurrentBalance = parsed.CurrentBalance;
            }

            result.Warnings.AddRange(parsed.Warnings);
            result.Transactions.AddRange(parsed.Transactions);
        }

        if (result.Transactions.Count == 0)
        {
            throw new InvalidOperationException("In der ZIP-Datei wurden keine CAMT-Buchungen gefunden.");
        }

        result.Transactions = result.Transactions
            .OrderBy(x => x.BookingDate)
            .ThenBy(x => x.ValueDate)
            .ThenBy(x => x.Amount)
            .ThenBy(x => x.Reference, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result;
    }

    private ParsedBankStatement ParseXml(byte[] fileBytes, string fileName)
    {
        using var stream = new MemoryStream(fileBytes);
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("Die CAMT-Datei ist leer.");
        XNamespace ns = root.Name.Namespace;

        var report = root.Descendants(ns + "Rpt").FirstOrDefault()
            ?? throw new InvalidOperationException("Die CAMT-Datei enthaelt keinen Kontoreport.");

        var account = report.Element(ns + "Acct");
        var iban = account?.Descendants(ns + "IBAN").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        var currency = account?.Element(ns + "Ccy")?.Value?.Trim() ?? "EUR";
        var bankName = account?.Descendants(ns + "Nm").FirstOrDefault()?.Value?.Trim() ?? string.Empty;

        var result = new ParsedBankStatement
        {
            FileName = fileName,
            AccountIban = iban,
            AccountName = bankName,
            Currency = currency
        };

        var openingBalance = FindBalance(report, ns, "PRCD", "OPBD");
        var closingBalance = FindBalance(report, ns, "CLBD");
        if (closingBalance.HasValue)
        {
            result.CurrentBalance = closingBalance;
        }

        var entries = report.Elements(ns + "Ntry")
            .Select(entry => ParseEntry(entry, ns, iban, currency))
            .Where(x => x is not null)
            .Cast<ParsedBankTransaction>()
            .ToList();

        if (entries.Count == 0)
        {
            result.Warnings.Add($"In {fileName} wurden keine Ntry-Eintraege gefunden.");
            return result;
        }

        ApplyBalances(entries, openingBalance, closingBalance);
        foreach (var entry in entries)
        {
            entry.ContentHash = BuildContentHash(entry);
        }

        result.Transactions.AddRange(entries);
        return result;
    }

    private static ParsedBankTransaction? ParseEntry(XElement entry, XNamespace ns, string accountIban, string fallbackCurrency)
    {
        var amountElement = entry.Element(ns + "Amt");
        if (amountElement is null || !decimal.TryParse(amountElement.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        if (string.Equals(entry.Element(ns + "CdtDbtInd")?.Value?.Trim(), "DBIT", StringComparison.OrdinalIgnoreCase))
        {
            amount *= -1m;
        }

        var bookingDate = ParseIsoDate(entry.Element(ns + "BookgDt")?.Descendants(ns + "Dt").FirstOrDefault()?.Value);
        if (!bookingDate.HasValue)
        {
            return null;
        }

        var valueDate = ParseIsoDate(entry.Element(ns + "ValDt")?.Descendants(ns + "Dt").FirstOrDefault()?.Value);
        var tx = entry.Descendants(ns + "TxDtls").FirstOrDefault();
        var refs = tx?.Element(ns + "Refs");
        var remittanceParts = tx?.Descendants(ns + "Ustrd").Select(x => x.Value.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        var additionalInfo = entry.Element(ns + "AddtlNtryInf")?.Value?.Trim() ?? string.Empty;

        var creditorName = tx?.Descendants(ns + "Cdtr").Elements(ns + "Nm").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        var debtorName = tx?.Descendants(ns + "Dbtr").Elements(ns + "Nm").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        var creditorIban = tx?.Descendants(ns + "CdtrAcct").Descendants(ns + "IBAN").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        var debtorIban = tx?.Descendants(ns + "DbtrAcct").Descendants(ns + "IBAN").FirstOrDefault()?.Value?.Trim() ?? string.Empty;

        var counterpartyName = amount >= 0m ? debtorName : creditorName;
        var counterpartyIban = amount >= 0m ? debtorIban : creditorIban;
        if (string.IsNullOrWhiteSpace(counterpartyName))
        {
            counterpartyName = amount >= 0m
                ? tx?.Descendants(ns + "UltmtDbtr").Elements(ns + "Nm").FirstOrDefault()?.Value?.Trim() ?? string.Empty
                : tx?.Descendants(ns + "UltmtCdtr").Elements(ns + "Nm").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        }

        var referenceParts = new[]
        {
            refs?.Element(ns + "EndToEndId")?.Value?.Trim(),
            refs?.Element(ns + "MndtId")?.Value?.Trim(),
            refs?.Element(ns + "AcctSvcrRef")?.Value?.Trim(),
            refs?.Descendants(ns + "Ref").FirstOrDefault()?.Value?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);

        return new ParsedBankTransaction
        {
            BookingDate = bookingDate.Value,
            ValueDate = valueDate,
            Amount = decimal.Round(amount, 2),
            Currency = amountElement.Attribute("Ccy")?.Value?.Trim() ?? fallbackCurrency,
            CounterpartyName = counterpartyName,
            CounterpartyIban = counterpartyIban,
            Purpose = string.Join(" | ", new[] { additionalInfo }.Concat(remittanceParts).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
            Reference = string.Join(" | ", referenceParts),
            TransactionType = additionalInfo,
            AccountIban = accountIban
        };
    }

    private static void ApplyBalances(List<ParsedBankTransaction> entries, decimal? openingBalance, decimal? closingBalance)
    {
        if (openingBalance.HasValue)
        {
            var running = openingBalance.Value;
            foreach (var entry in entries.OrderBy(x => x.BookingDate).ThenBy(x => x.ValueDate).ToList())
            {
                running += entry.Amount;
                entry.BalanceAfterBooking = decimal.Round(running, 2);
            }

            return;
        }

        if (closingBalance.HasValue)
        {
            var ordered = entries.OrderBy(x => x.BookingDate).ThenBy(x => x.ValueDate).ToList();
            var running = closingBalance.Value;
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                ordered[i].BalanceAfterBooking = decimal.Round(running, 2);
                running -= ordered[i].Amount;
            }
        }
    }

    private static decimal? FindBalance(XElement report, XNamespace ns, params string[] codes)
    {
        foreach (var balance in report.Elements(ns + "Bal"))
        {
            var code = balance.Descendants(ns + "Cd").FirstOrDefault()?.Value?.Trim();
            if (!codes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var amountElement = balance.Element(ns + "Amt");
            if (amountElement is null || !decimal.TryParse(amountElement.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            var creditDebit = balance.Element(ns + "CdtDbtInd")?.Value?.Trim();
            if (string.Equals(creditDebit, "DBIT", StringComparison.OrdinalIgnoreCase))
            {
                amount *= -1m;
            }

            return decimal.Round(amount, 2);
        }

        return null;
    }

    private static DateTime? ParseIsoDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.Date
            : null;
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
}
