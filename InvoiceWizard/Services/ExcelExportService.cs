using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace InvoiceWizard.Services;

public static class ExcelExportService
{
    public sealed class ExportRow
    {
        public string SupplierInvoiceNumber { get; set; } = "";
        public string ArticleNumber { get; set; } = "";
        public string Ean { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal PurchaseUnitPrice { get; set; }
        public decimal MarkupPercent { get; set; }
        public decimal SalesUnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public static void ExportOpenItems(string filePath, string customerName, IEnumerable<ExportRow> rows)
    {
        var rowList = rows.ToList();

        using var stream = File.Create(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
        WriteEntry(archive, "_rels/.rels", RootRelsXml);
        WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
        WriteEntry(archive, "xl/styles.xml", StylesXml);
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(customerName, rowList));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildSheetXml(string customerName, IReadOnlyList<ExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.AppendLine("  <sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
        sb.AppendLine("  <sheetFormatPr defaultRowHeight=\"15\"/>");
        sb.AppendLine("  <cols>");
        sb.AppendLine("    <col min=\"1\" max=\"1\" width=\"18\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"2\" max=\"2\" width=\"16\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"3\" max=\"3\" width=\"18\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"4\" max=\"4\" width=\"42\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"5\" max=\"5\" width=\"12\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"6\" max=\"6\" width=\"10\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"7\" max=\"7\" width=\"14\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"8\" max=\"8\" width=\"12\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"9\" max=\"9\" width=\"14\" customWidth=\"1\"/>");
        sb.AppendLine("    <col min=\"10\" max=\"10\" width=\"14\" customWidth=\"1\"/>");
        sb.AppendLine("  </cols>");
        sb.AppendLine("  <sheetData>");
        AppendInlineRow(sb, 1, 1, $"Offene Positionen fuer {customerName}");
        AppendHeaderRow(sb, 3, new[]
        {
            "Lieferantenrechnung", "Artikel", "EAN", "Beschreibung", "Menge",
            "Einheit", "EK/Stk", "Zuschlag %", "VK/Stk", "Gesamt"
        });

        var rowIndex = 4;
        foreach (var row in rows)
        {
            sb.AppendLine($"    <row r=\"{rowIndex}\">");
            AppendInlineCell(sb, "A", rowIndex, row.SupplierInvoiceNumber);
            AppendInlineCell(sb, "B", rowIndex, row.ArticleNumber);
            AppendInlineCell(sb, "C", rowIndex, row.Ean);
            AppendInlineCell(sb, "D", rowIndex, row.Description);
            AppendNumberCell(sb, "E", rowIndex, row.Quantity);
            AppendInlineCell(sb, "F", rowIndex, row.Unit);
            AppendNumberCell(sb, "G", rowIndex, row.PurchaseUnitPrice);
            AppendNumberCell(sb, "H", rowIndex, row.MarkupPercent);
            AppendNumberCell(sb, "I", rowIndex, row.SalesUnitPrice);
            AppendNumberCell(sb, "J", rowIndex, row.Total);
            sb.AppendLine("    </row>");
            rowIndex++;
        }

        sb.AppendLine("  </sheetData>");
        sb.AppendLine("</worksheet>");
        return sb.ToString();
    }

    private static void AppendHeaderRow(StringBuilder sb, int rowIndex, IReadOnlyList<string> headers)
    {
        sb.AppendLine($"    <row r=\"{rowIndex}\">");
        for (var i = 0; i < headers.Count; i++)
        {
            var column = (char)('A' + i);
            AppendInlineCell(sb, column.ToString(), rowIndex, headers[i]);
        }
        sb.AppendLine("    </row>");
    }

    private static void AppendInlineRow(StringBuilder sb, int rowIndex, int styleIndex, string value)
    {
        sb.AppendLine($"    <row r=\"{rowIndex}\">");
        sb.AppendLine($"      <c r=\"A{rowIndex}\" t=\"inlineStr\" s=\"{styleIndex}\"><is><t>{Escape(value)}</t></is></c>");
        sb.AppendLine("    </row>");
    }

    private static void AppendInlineCell(StringBuilder sb, string column, int rowIndex, string value)
    {
        sb.AppendLine($"      <c r=\"{column}{rowIndex}\" t=\"inlineStr\"><is><t>{Escape(value)}</t></is></c>");
    }

    private static void AppendNumberCell(StringBuilder sb, string column, int rowIndex, decimal value)
    {
        sb.AppendLine($"      <c r=\"{column}{rowIndex}\"><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>");
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";

    private const string ContentTypesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\n  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\n  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\n  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\n  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\n  <Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>\n</Types>";

    private const string RootRelsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\n</Relationships>";

    private const string WorkbookXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\n  <sheets>\n    <sheet name=\"Offene Positionen\" sheetId=\"1\" r:id=\"rId1\"/>\n  </sheets>\n</workbook>";

    private const string WorkbookRelsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\n  <Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n</Relationships>";

    private const string StylesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\n  <fonts count=\"2\">\n    <font><sz val=\"11\"/><name val=\"Calibri\"/></font>\n    <font><b/><sz val=\"12\"/><name val=\"Calibri\"/></font>\n  </fonts>\n  <fills count=\"2\">\n    <fill><patternFill patternType=\"none\"/></fill>\n    <fill><patternFill patternType=\"gray125\"/></fill>\n  </fills>\n  <borders count=\"1\">\n    <border><left/><right/><top/><bottom/><diagonal/></border>\n  </borders>\n  <cellStyleXfs count=\"1\">\n    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>\n  </cellStyleXfs>\n  <cellXfs count=\"2\">\n    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\n    <xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>\n  </cellXfs>\n  <cellStyles count=\"1\">\n    <cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/>\n  </cellStyles>\n</styleSheet>";
}
