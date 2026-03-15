using iText.Barcodes;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using InvoiceWizard.Data.Entities;
using System.IO;

namespace InvoiceWizard.Services;

public static class CustomerInvoicePdfService
{
    public sealed class InvoiceDocument
    {
        public CompanyProfileEntity Company { get; set; } = new();
        public CustomerEntity Customer { get; set; } = new();
        public string InvoiceNumber { get; set; } = "";
        public string CustomerNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string Subject { get; set; } = "";
        public bool ApplySmallBusinessRegulation { get; set; }
        public bool IsDraft { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new();
        public decimal TotalAmount => Lines.Sum(x => x.LineTotal);
    }

    public sealed class InvoiceLine
    {
        public int Position { get; set; }
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public static byte[] Create(InvoiceDocument invoice)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, PageSize.A4);

        document.SetMargins(42, 42, 52, 42);

        if (invoice.IsDraft)
        {
            pdf.AddEventHandler(iText.Kernel.Events.PdfDocumentEvent.END_PAGE, new DraftWatermarkHandler());
        }

        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var accent = new DeviceRgb(78, 154, 248);
        var muted = new DeviceRgb(96, 108, 128);

        document.Add(BuildTopSection(invoice, bold, font, muted));
        document.Add(new Paragraph("Rechnung").SetFont(bold).SetFontSize(18).SetMarginTop(18).SetMarginBottom(8));
        document.Add(BuildMetaTable(invoice, bold, font, accent));
        document.Add(new Paragraph(string.IsNullOrWhiteSpace(invoice.Subject) ? "Unsere Lieferungen/Leistungen stellen wir Ihnen wie folgt in Rechnung." : invoice.Subject)
            .SetFont(font)
            .SetFontSize(11)
            .SetMarginTop(18)
            .SetMarginBottom(18));
        document.Add(BuildLinesTable(invoice, bold, font, accent));
        document.Add(BuildSummarySection(invoice, bold, font));

        if (invoice.ApplySmallBusinessRegulation)
        {
            document.Add(new Paragraph("Umsatzsteuerfreie Leistungen gemaess §19 UStG")
                .SetFont(font)
                .SetFontSize(10)
                .SetMarginTop(16)
                .SetMarginBottom(12));
        }

        document.Add(BuildFooter(invoice, bold, font, muted));

        return stream.ToArray();
    }

    private static IBlockElement BuildTopSection(InvoiceDocument invoice, PdfFont bold, PdfFont font, DeviceRgb muted)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 0.9f })).UseAllAvailableWidth();
        table.SetBorder(Border.NO_BORDER);

        var customerAddress = new Paragraph()
            .Add(new Text(invoice.Customer.Name).SetFont(bold))
            .Add($"\n{BuildAddressLine(invoice.Customer.Street, invoice.Customer.HouseNumber)}")
            .Add($"\n{BuildAddressLine(invoice.Customer.PostalCode, invoice.Customer.City)}")
            .SetFont(font)
            .SetFontSize(11)
            .SetMargin(0);

        var companyAddress = new Paragraph()
            .Add(new Text(invoice.Company.CompanyName).SetFont(bold))
            .Add($"\n{BuildAddressLine(invoice.Company.CompanyStreet, invoice.Company.CompanyHouseNumber)}")
            .Add($"\n{BuildAddressLine(invoice.Company.CompanyPostalCode, invoice.Company.CompanyCity)}")
            .Add(string.IsNullOrWhiteSpace(invoice.Company.CompanyPhoneNumber) ? string.Empty : $"\nTelefon {invoice.Company.CompanyPhoneNumber}")
            .Add(string.IsNullOrWhiteSpace(invoice.Company.CompanyEmailAddress) ? string.Empty : $"\n{invoice.Company.CompanyEmailAddress}")
            .SetFont(font)
            .SetFontSize(10)
            .SetFontColor(muted)
            .SetTextAlignment(TextAlignment.RIGHT)
            .SetMargin(0);

        table.AddCell(new Cell().Add(customerAddress).SetBorder(Border.NO_BORDER).SetPadding(0).SetPaddingTop(46));
        table.AddCell(new Cell().Add(companyAddress).SetBorder(Border.NO_BORDER).SetPadding(0));
        return table;
    }

    private static IBlockElement BuildMetaTable(InvoiceDocument invoice, PdfFont bold, PdfFont font, DeviceRgb accent)
    {
        var wrapper = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        wrapper.SetBorder(Border.NO_BORDER);

        var left = new Paragraph()
            .Add(new Text("Kundennummer: ").SetFont(bold))
            .Add(invoice.CustomerNumber)
            .SetFont(font)
            .SetFontSize(10)
            .SetMargin(0);

        var meta = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        meta.SetBorder(new SolidBorder(accent, 1));
        meta.SetFixedLayout();
        AddMetaRow(meta, "Rechnungsnummer", invoice.InvoiceNumber, bold, font);
        AddMetaRow(meta, "Rechnungsdatum", invoice.InvoiceDate.ToString("dd.MM.yyyy"), bold, font);
        AddMetaRow(meta, "Lieferdatum", invoice.DeliveryDate.ToString("dd.MM.yyyy"), bold, font);

        wrapper.AddCell(new Cell().Add(left).SetBorder(Border.NO_BORDER).SetPadding(0).SetVerticalAlignment(VerticalAlignment.TOP));
        wrapper.AddCell(new Cell().Add(meta).SetBorder(Border.NO_BORDER).SetPadding(0));
        return wrapper;
    }

    private static Table BuildLinesTable(InvoiceDocument invoice, PdfFont bold, PdfFont font, DeviceRgb accent)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.6f, 3.6f, 0.8f, 0.8f, 1.1f, 1.2f })).UseAllAvailableWidth();
        table.SetFixedLayout();
        table.SetMarginTop(10);

        AddHeaderCell(table, "Pos.", bold, accent);
        AddHeaderCell(table, "Beschreibung", bold, accent);
        AddHeaderCell(table, "Menge", bold, accent);
        AddHeaderCell(table, "Einheit", bold, accent);
        AddHeaderCell(table, "Preis", bold, accent);
        AddHeaderCell(table, "Gesamt", bold, accent);

        foreach (var line in invoice.Lines)
        {
            AddBodyCell(table, line.Position.ToString(), font, TextAlignment.LEFT);
            AddBodyCell(table, line.Description, font, TextAlignment.LEFT);
            AddBodyCell(table, line.Quantity.ToString("0.##"), font, TextAlignment.RIGHT);
            AddBodyCell(table, line.Unit, font, TextAlignment.LEFT);
            AddBodyCell(table, FormatCurrency(line.UnitPrice), font, TextAlignment.RIGHT);
            AddBodyCell(table, FormatCurrency(line.LineTotal), font, TextAlignment.RIGHT);
        }

        return table;
    }

    private static IBlockElement BuildSummarySection(InvoiceDocument invoice, PdfFont bold, PdfFont font)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.6f, 0.8f })).UseAllAvailableWidth();
        table.SetBorder(Border.NO_BORDER);
        table.SetMarginTop(14);

        var qrText = BuildEpcQrPayload(invoice);
        var qr = new BarcodeQRCode(qrText).CreateFormXObject(ColorConstants.BLACK, null);
        var image = new Image(qr).ScaleToFit(110, 110);

        var left = new Paragraph("Bitte ueberweisen Sie den Rechnungsbetrag unter Angabe von Kundennummer und Rechnungsnummer.")
            .SetFont(font)
            .SetFontSize(10)
            .SetMargin(0);
        left.Add($"\nKundennummer: {invoice.CustomerNumber}");
        left.Add($"\nRechnungsnummer: {invoice.InvoiceNumber}");

        var totalTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        totalTable.SetBorder(new SolidBorder(ColorConstants.BLACK, 1));
        AddMetaRow(totalTable, "Gesamtbetrag", FormatCurrency(invoice.TotalAmount), bold, font);

        var rightWrapper = new Div();
        rightWrapper.Add(totalTable);
        rightWrapper.Add(new Paragraph().Add(image).SetMarginTop(10).SetTextAlignment(TextAlignment.RIGHT));

        table.AddCell(new Cell().Add(left).SetBorder(Border.NO_BORDER).SetPadding(0).SetVerticalAlignment(VerticalAlignment.TOP));
        table.AddCell(new Cell().Add(rightWrapper).SetBorder(Border.NO_BORDER).SetPadding(0));
        return table;
    }

    private static IBlockElement BuildFooter(InvoiceDocument invoice, PdfFont bold, PdfFont font, DeviceRgb muted)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        table.SetMarginTop(20);
        table.SetBorderTop(new SolidBorder(muted, 1));

        var left = new Paragraph()
            .Add(new Text("Steuernummer\n").SetFont(bold))
            .Add(string.IsNullOrWhiteSpace(invoice.Company.TaxNumber) ? "-" : invoice.Company.TaxNumber)
            .SetFont(font)
            .SetFontSize(9)
            .SetFontColor(muted)
            .SetMarginTop(8);

        var right = new Paragraph()
            .Add(new Text("Bankverbindung\n").SetFont(bold))
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankName) ? "-" : invoice.Company.BankName)
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankIban) ? string.Empty : $"\nIBAN {invoice.Company.BankIban}")
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankBic) ? string.Empty : $"\nBIC {invoice.Company.BankBic}")
            .SetFont(font)
            .SetFontSize(9)
            .SetFontColor(muted)
            .SetMarginTop(8)
            .SetTextAlignment(TextAlignment.RIGHT);

        table.AddCell(new Cell().Add(left).SetBorder(Border.NO_BORDER).SetPadding(0));
        table.AddCell(new Cell().Add(right).SetBorder(Border.NO_BORDER).SetPadding(0));
        return table;
    }

    private static void AddHeaderCell(Table table, string text, PdfFont font, DeviceRgb accent)
    {
        table.AddHeaderCell(new Cell()
            .Add(new Paragraph(text).SetFont(font).SetFontSize(10).SetFontColor(ColorConstants.WHITE))
            .SetBackgroundColor(accent)
            .SetPadding(6)
            .SetBorder(Border.NO_BORDER));
    }

    private static void AddBodyCell(Table table, string text, PdfFont font, TextAlignment alignment)
    {
        table.AddCell(new Cell()
            .Add(new Paragraph(text).SetFont(font).SetFontSize(10).SetTextAlignment(alignment))
            .SetPadding(6)
            .SetBorderBottom(new SolidBorder(new DeviceRgb(220, 225, 232), 0.7f))
            .SetBorderLeft(Border.NO_BORDER)
            .SetBorderRight(Border.NO_BORDER)
            .SetBorderTop(Border.NO_BORDER));
    }

    private static void AddMetaRow(Table table, string label, string value, PdfFont bold, PdfFont font)
    {
        table.AddCell(new Cell().Add(new Paragraph(label).SetFont(bold).SetFontSize(9)).SetPadding(6));
        table.AddCell(new Cell().Add(new Paragraph(value).SetFont(font).SetFontSize(9)).SetPadding(6));
    }

    private static string BuildEpcQrPayload(InvoiceDocument invoice)
    {
        var amount = invoice.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var remittance = $"KUNDENNR {invoice.CustomerNumber} RECHNUNG {invoice.InvoiceNumber}";
        return string.Join("\n", new[]
        {
            "BCD",
            "002",
            "1",
            "SCT",
            invoice.Company.BankBic ?? string.Empty,
            invoice.Company.CompanyName,
            invoice.Company.BankIban,
            $"EUR{amount}",
            string.Empty,
            remittance,
            string.Empty
        });
    }

    private static string BuildAddressLine(string first, string second)
    {
        return string.Join(" ", new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("0.00") + " EUR";
    }
}

internal sealed class DraftWatermarkHandler : iText.Kernel.Events.IEventHandler
{
    public void HandleEvent(iText.Kernel.Events.Event @event)
    {
        var documentEvent = (iText.Kernel.Events.PdfDocumentEvent)@event;
        var page = documentEvent.GetPage();
        var pageSize = page.GetPageSize();
        var pdfCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), documentEvent.GetDocument());
        using var layoutCanvas = new Canvas(pdfCanvas, pageSize);
        layoutCanvas.Add(
            new Paragraph("ENTWURF")
                .SetFontSize(72)
                .SetFontColor(new DeviceRgb(210, 220, 236), 0.35f)
                .SetBold()
                .SetRotationAngle(Math.PI / 4)
                .SetFixedPosition(pageSize.GetWidth() / 2 - 180, pageSize.GetHeight() / 2 - 20, 360));
    }
}
