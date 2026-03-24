using System.Globalization;
using System.IO;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using InvoiceWizard.Data.Entities;

namespace InvoiceWizard.Services;

public static class OfferPdfService
{
    public sealed class OfferDocument
    {
        public CompanyProfileEntity Company { get; set; } = new();
        public CustomerEntity Customer { get; set; } = new();
        public string OfferNumber { get; set; } = "";
        public string CustomerNumber { get; set; } = "";
        public DateTime OfferDate { get; set; }
        public DateTime ValidUntilDate { get; set; }
        public string Subject { get; set; } = "";
        public bool ApplySmallBusinessRegulation { get; set; }
        public List<OfferLine> Lines { get; set; } = new();
        public decimal NetTotal => Lines.Sum(x => x.LineTotal);
        public decimal VatAmount => PricingHelper.CalculateRevenueVatAmount(NetTotal, ApplySmallBusinessRegulation);
        public decimal TotalAmount => PricingHelper.CalculateRevenueGrossTotal(NetTotal, ApplySmallBusinessRegulation);
    }

    public sealed class OfferLine
    {
        public int Position { get; set; }
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public static byte[] Create(OfferDocument offer)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, PageSize.A4);

        document.SetMargins(42, 42, 78, 42);
        pdf.AddEventHandler(iText.Kernel.Events.PdfDocumentEvent.END_PAGE, new InvoiceFooterHandler(new CustomerInvoicePdfService.InvoiceDocument
        {
            Company = offer.Company,
            Customer = offer.Customer,
            CustomerNumber = offer.CustomerNumber,
            InvoiceNumber = offer.OfferNumber
        }));

        var font = CustomerInvoicePdfService.CreateRegularPdfFont();
        var bold = CustomerInvoicePdfService.CreateBoldPdfFont();
        var accent = new DeviceRgb(78, 154, 248);
        var muted = new DeviceRgb(96, 108, 128);

        document.Add(BuildTopSection(offer, bold, font, muted));
        document.Add(new Paragraph("Angebot").SetFont(bold).SetFontSize(18).SetMarginTop(18).SetMarginBottom(8));
        document.Add(BuildMetaTable(offer, bold, font, accent));
        document.Add(new Paragraph(string.IsNullOrWhiteSpace(offer.Subject) ? "Gerne bieten wir Ihnen folgende Lieferungen/Leistungen an." : offer.Subject)
            .SetFont(font)
            .SetFontSize(11)
            .SetMarginTop(18)
            .SetMarginBottom(18));
        document.Add(BuildLinesTable(offer, bold, font, accent));

        if (offer.ApplySmallBusinessRegulation)
        {
            document.Add(new Paragraph("Umsatzsteuerfreie Leistungen gemäß §19 UStG.")
                .SetFont(font)
                .SetFontSize(10)
                .SetMarginTop(18)
                .SetMarginBottom(0));
        }

        document.Add(new Paragraph("Wir freuen uns auf Ihre Rückmeldung und danken für Ihr Vertrauen.")
            .SetFont(font)
            .SetFontSize(10)
            .SetMarginTop(18)
            .SetMarginBottom(0));

        document.Close();
        return stream.ToArray();
    }

    private static IBlockElement BuildTopSection(OfferDocument offer, iText.Kernel.Font.PdfFont bold, iText.Kernel.Font.PdfFont font, DeviceRgb muted)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 0.9f })).UseAllAvailableWidth();
        table.SetBorder(Border.NO_BORDER);

        var customerAddress = new Paragraph()
            .Add(new Text(offer.Customer.Name ?? string.Empty).SetFont(bold))
            .Add($"\n{BuildAddressLine(offer.Customer.Street, offer.Customer.HouseNumber)}")
            .Add($"\n{BuildAddressLine(offer.Customer.PostalCode, offer.Customer.City)}")
            .SetFont(font)
            .SetFontSize(11)
            .SetMargin(0);

        var companyAddress = new Paragraph()
            .Add(new Text(offer.Company.CompanyName ?? string.Empty).SetFont(bold))
            .Add($"\n{BuildAddressLine(offer.Company.CompanyStreet, offer.Company.CompanyHouseNumber)}")
            .Add($"\n{BuildAddressLine(offer.Company.CompanyPostalCode, offer.Company.CompanyCity)}")
            .Add(string.IsNullOrWhiteSpace(offer.Company.CompanyPhoneNumber) ? string.Empty : $"\nTelefon: {offer.Company.CompanyPhoneNumber}")
            .Add(string.IsNullOrWhiteSpace(offer.Company.CompanyEmailAddress) ? string.Empty : $"\n{offer.Company.CompanyEmailAddress}")
            .SetFont(font)
            .SetFontSize(10)
            .SetFontColor(muted)
            .SetTextAlignment(TextAlignment.RIGHT)
            .SetMargin(0);

        table.AddCell(new Cell().Add(customerAddress).SetBorder(Border.NO_BORDER).SetPadding(0).SetPaddingTop(46));
        table.AddCell(new Cell().Add(companyAddress).SetBorder(Border.NO_BORDER).SetPadding(0));
        return table;
    }

    private static IBlockElement BuildMetaTable(OfferDocument offer, iText.Kernel.Font.PdfFont bold, iText.Kernel.Font.PdfFont font, DeviceRgb accent)
    {
        var wrapper = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        wrapper.SetBorder(Border.NO_BORDER);

        var left = new Paragraph()
            .Add(new Text("Kundennummer: ").SetFont(bold))
            .Add(offer.CustomerNumber ?? string.Empty)
            .SetFont(font)
            .SetFontSize(10)
            .SetMargin(0);

        var meta = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
        meta.SetBorder(new SolidBorder(accent, 1));
        meta.SetFixedLayout();
        AddMetaRow(meta, "Angebotsnummer", offer.OfferNumber, bold, font);
        AddMetaRow(meta, "Angebotsdatum", offer.OfferDate.ToString("dd.MM.yyyy"), bold, font);
        AddMetaRow(meta, "Gültig bis", offer.ValidUntilDate.ToString("dd.MM.yyyy"), bold, font);

        wrapper.AddCell(new Cell().Add(left).SetBorder(Border.NO_BORDER).SetPadding(0).SetVerticalAlignment(VerticalAlignment.TOP));
        wrapper.AddCell(new Cell().Add(meta).SetBorder(Border.NO_BORDER).SetPadding(0));
        return wrapper;
    }

    private static IBlockElement BuildLinesTable(OfferDocument offer, iText.Kernel.Font.PdfFont bold, iText.Kernel.Font.PdfFont font, DeviceRgb accent)
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

        foreach (var line in offer.Lines)
        {
            AddBodyCell(table, line.Position.ToString(), font, TextAlignment.LEFT);
            AddBodyCell(table, line.Description, font, TextAlignment.LEFT);
            AddBodyCell(table, line.Quantity.ToString("0.##"), font, TextAlignment.RIGHT);
            AddBodyCell(table, line.Unit, font, TextAlignment.LEFT);
            AddBodyCell(table, FormatCurrency(line.UnitPrice), font, TextAlignment.RIGHT);
            AddBodyCell(table, FormatCurrency(line.LineTotal), font, TextAlignment.RIGHT);
        }

        AddSummaryRow(table, "Gesamtnetto", offer.NetTotal, bold, font, true, false);
        AddSummaryRow(table, offer.ApplySmallBusinessRegulation ? "Umsatzsteuer" : "Umsatzsteuer 19 %", offer.VatAmount, bold, font, false, false);
        AddSummaryRow(table, "Gesamtbetrag", offer.TotalAmount, bold, font, true, true);

        return table;
    }

    private static void AddSummaryRow(Table table, string label, decimal amount, iText.Kernel.Font.PdfFont bold, iText.Kernel.Font.PdfFont font, bool topBorder, bool bottomBorder)
    {
        var paragraphFont = topBorder ? bold : font;
        var leftCell = new Cell(1, 5)
            .Add(new Paragraph(label).SetFont(paragraphFont).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
            .SetPadding(6)
            .SetBorderLeft(Border.NO_BORDER)
            .SetBorderRight(Border.NO_BORDER);
        var rightCell = new Cell()
            .Add(new Paragraph(FormatCurrency(amount)).SetFont(paragraphFont).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
            .SetPadding(6)
            .SetBorderLeft(Border.NO_BORDER)
            .SetBorderRight(Border.NO_BORDER);

        leftCell.SetBorderTop(topBorder ? new SolidBorder(ColorConstants.BLACK, 1) : Border.NO_BORDER);
        rightCell.SetBorderTop(topBorder ? new SolidBorder(ColorConstants.BLACK, 1) : Border.NO_BORDER);
        leftCell.SetBorderBottom(bottomBorder ? new SolidBorder(ColorConstants.BLACK, 1) : Border.NO_BORDER);
        rightCell.SetBorderBottom(bottomBorder ? new SolidBorder(ColorConstants.BLACK, 1) : Border.NO_BORDER);

        table.AddCell(leftCell);
        table.AddCell(rightCell);
    }

    private static void AddHeaderCell(Table table, string text, iText.Kernel.Font.PdfFont font, DeviceRgb accent)
    {
        table.AddHeaderCell(new Cell()
            .Add(new Paragraph(text).SetFont(font).SetFontSize(10).SetFontColor(ColorConstants.WHITE))
            .SetBackgroundColor(accent)
            .SetPadding(6)
            .SetBorder(Border.NO_BORDER));
    }

    private static void AddBodyCell(Table table, string text, iText.Kernel.Font.PdfFont font, TextAlignment alignment)
    {
        table.AddCell(new Cell()
            .Add(new Paragraph(text).SetFont(font).SetFontSize(10).SetTextAlignment(alignment))
            .SetPadding(6)
            .SetBorderBottom(new SolidBorder(new DeviceRgb(220, 225, 232), 0.7f))
            .SetBorderLeft(Border.NO_BORDER)
            .SetBorderRight(Border.NO_BORDER)
            .SetBorderTop(Border.NO_BORDER));
    }

    private static void AddMetaRow(Table table, string label, string value, iText.Kernel.Font.PdfFont bold, iText.Kernel.Font.PdfFont font)
    {
        table.AddCell(new Cell().Add(new Paragraph(label).SetFont(bold).SetFontSize(9)).SetPadding(6));
        table.AddCell(new Cell().Add(new Paragraph(value).SetFont(font).SetFontSize(9)).SetPadding(6));
    }

    private static string BuildAddressLine(string? first, string? second)
    {
        return string.Join(" ", new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') + " EUR";
    }
}
