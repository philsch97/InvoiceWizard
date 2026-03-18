using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using iText.Barcodes;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Extgstate;
using iText.Kernel.Pdf.Filespec;
using iText.Kernel.XMP;
using iText.Kernel.XMP.Options;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Pdfa;
using InvoiceWizard.Data.Entities;

namespace InvoiceWizard.Services;

public static partial class CustomerInvoicePdfService
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
        public decimal NetTotal => Lines.Sum(x => x.LineTotal);
        public decimal VatAmount => PricingHelper.CalculateRevenueVatAmount(NetTotal, ApplySmallBusinessRegulation);
        public decimal TotalAmount => PricingHelper.CalculateRevenueGrossTotal(NetTotal, ApplySmallBusinessRegulation);
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
        Stream? colorProfileStream = null;
        try
        {
            if (!invoice.IsDraft)
            {
                colorProfileStream = OpenPdfAColorProfileStream();
            }

            var pdf = invoice.IsDraft
                ? new PdfDocument(writer)
                : CreatePdfAInvoiceDocument(writer, colorProfileStream!);
            ConfigureDocumentMetadata(invoice, pdf);
            var document = new Document(pdf, PageSize.A4, false);

            document.SetMargins(42, 42, 78, 42);
            if (!invoice.IsDraft)
            {
                AttachZugferdXml(invoice, pdf);
            }
            pdf.AddEventHandler(iText.Kernel.Events.PdfDocumentEvent.END_PAGE, new InvoiceFooterHandler(invoice));

            if (invoice.IsDraft)
            {
                pdf.AddEventHandler(iText.Kernel.Events.PdfDocumentEvent.END_PAGE, new DraftWatermarkHandler());
            }

            var font = CreateRegularPdfFont();
            var bold = CreateBoldPdfFont();
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

            var pages = PaginateLines(invoice.Lines);
            for (var i = 0; i < pages.Count; i++)
            {
                if (i > 0)
                {
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    document.Add(new Paragraph($"Rechnungsnummer: {invoice.InvoiceNumber}")
                        .SetFont(font)
                        .SetFontSize(10)
                        .SetMarginTop(0)
                        .SetMarginBottom(10));
                }

                var page = pages[i];
                var isLastPage = i == pages.Count - 1;
                var carryForward = pages.Take(i).Sum(x => x.PageSubtotal);
                document.Add(BuildLinesTable(
                    page.Lines,
                    bold,
                    font,
                    accent,
                    !isLastPage,
                    carryForward + page.PageSubtotal,
                    i > 0,
                    carryForward,
                    isLastPage,
                    invoice.VatAmount,
                    invoice.TotalAmount,
                    invoice.ApplySmallBusinessRegulation));
            }

            var closingSection = new Div().SetKeepTogether(true);
            closingSection.Add(BuildSummarySection(invoice, pdf, bold, font));
            if (invoice.ApplySmallBusinessRegulation)
            {
                closingSection.Add(new Paragraph("Umsatzsteuerfreie Leistungen gemäß §19 UStG.*")
                    .SetFont(font)
                    .SetFontSize(10)
                    .SetMarginTop(18)
                    .SetMarginBottom(0));
            }

            document.Add(closingSection);
            document.Close();
            return stream.ToArray();
        }
        finally
        {
            colorProfileStream?.Dispose();
        }
    }

    private static PdfADocument CreatePdfAInvoiceDocument(PdfWriter writer, Stream colorProfileStream)
    {
        var outputIntent = new PdfOutputIntent(
            "Custom",
            string.Empty,
            "http://www.color.org",
            "sRGB IEC61966-2.1",
            colorProfileStream);

        return new PdfADocument(writer, PdfAConformanceLevel.PDF_A_3B, outputIntent);
    }

    internal static PdfFont CreateRegularPdfFont()
    {
        return PdfFontFactory.CreateFont(GetWindowsFontPath("arial.ttf"), iText.IO.Font.PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
    }

    internal static PdfFont CreateBoldPdfFont()
    {
        return PdfFontFactory.CreateFont(GetWindowsFontPath("arialbd.ttf"), iText.IO.Font.PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
    }

    private static string GetWindowsFontPath(string fileName)
    {
        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Die benoetigte Schriftdatei wurde nicht gefunden: {fileName}");
        }

        return path;
    }

    private static Stream OpenPdfAColorProfileStream()
    {
        return File.OpenRead(GetPdfAColorProfilePath());
    }

    private static string GetPdfAColorProfilePath()
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spool", "drivers", "color", "sRGB Color Space Profile.icm"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spool", "drivers", "color", "surface-srgb.icm")
        };

        return candidates.FirstOrDefault(candidate => File.Exists(candidate))
            ?? throw new FileNotFoundException("Kein lokales sRGB-Farbprofil für PDF/A-3 gefunden.");
    }

    private static void ConfigureDocumentMetadata(InvoiceDocument invoice, PdfDocument pdf)
    {
        pdf.GetCatalog().SetLang(new PdfString("de-DE"));

        var info = pdf.GetDocumentInfo();
        info.SetTitle($"Rechnung {invoice.InvoiceNumber}");
        info.SetAuthor(Safe(invoice.Company.CompanyName));
        info.SetCreator("InvoiceWizard");
        info.SetProducer("InvoiceWizard");
        info.SetSubject(string.IsNullOrWhiteSpace(invoice.Subject) ? "Rechnung" : invoice.Subject);

        if (!invoice.IsDraft)
        {
            var serializeOptions = new SerializeOptions();
            serializeOptions.SetUseCanonicalFormat(true);
            pdf.SetXmpMetadata(XMPMetaFactory.ParseFromString(BuildZugferdXmpMetadata(invoice)), serializeOptions);
        }
    }

    private static string BuildZugferdXmpMetadata(InvoiceDocument invoice)
    {
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace pdf = "http://ns.adobe.com/pdf/1.3/";
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        XNamespace pdfaid = "http://www.aiim.org/pdfa/ns/id/";
        XNamespace pdfaExtension = "http://www.aiim.org/pdfa/ns/extension/";
        XNamespace pdfaSchema = "http://www.aiim.org/pdfa/ns/schema#";
        XNamespace pdfaProperty = "http://www.aiim.org/pdfa/ns/property#";
        XNamespace fx = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";

        var title = $"Rechnung {invoice.InvoiceNumber}";
        var subject = string.IsNullOrWhiteSpace(invoice.Subject) ? "Rechnung" : invoice.Subject;

        var extensionProperties = new[]
        {
            BuildPdfaExtensionProperty(rdf, pdfaProperty, "DocumentFileName", "Text", "external", "The name of the embedded XML document"),
            BuildPdfaExtensionProperty(rdf, pdfaProperty, "DocumentType", "Text", "external", "The type of the hybrid document"),
            BuildPdfaExtensionProperty(rdf, pdfaProperty, "Version", "Text", "external", "The Factur-X/ZUGFeRD version"),
            BuildPdfaExtensionProperty(rdf, pdfaProperty, "ConformanceLevel", "Text", "external", "The Factur-X/ZUGFeRD conformance level")
        };

        var extensionSchemaDescription = new XElement(rdf + "Description",
            new XAttribute(rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "pdfaExtension", pdfaExtension),
            new XAttribute(XNamespace.Xmlns + "pdfaSchema", pdfaSchema),
            new XAttribute(XNamespace.Xmlns + "pdfaProperty", pdfaProperty),
            new XElement(pdfaExtension + "schemas",
                new XElement(rdf + "Bag",
                    new XElement(rdf + "li",
                        new XAttribute(rdf + "parseType", "Resource"),
                        new XElement(pdfaSchema + "schema", "Factur-X PDFA Extension Schema"),
                        new XElement(pdfaSchema + "namespaceURI", fx.NamespaceName),
                        new XElement(pdfaSchema + "prefix", "fx"),
                        new XElement(pdfaSchema + "property",
                            new XElement(rdf + "Seq", extensionProperties))))));

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x),
                new XAttribute(x + "xmptk", "InvoiceWizard"),
                new XElement(rdf + "RDF",
                    new XElement(rdf + "Description",
                        new XAttribute(rdf + "about", string.Empty),
                        new XAttribute(XNamespace.Xmlns + "pdfaid", pdfaid),
                        new XElement(pdfaid + "part", "3"),
                        new XElement(pdfaid + "conformance", "B")),
                    new XElement(rdf + "Description",
                        new XAttribute(rdf + "about", string.Empty),
                        new XAttribute(XNamespace.Xmlns + "dc", dc),
                        new XAttribute(XNamespace.Xmlns + "pdf", pdf),
                        new XAttribute(XNamespace.Xmlns + "xmp", xmp),
                        new XElement(dc + "format", "application/pdf"),
                        new XElement(dc + "title",
                            new XElement(rdf + "Alt",
                                new XElement(rdf + "li",
                                    new XAttribute(XNamespace.Xml + "lang", "x-default"),
                                    title))),
                        new XElement(dc + "creator",
                            new XElement(rdf + "Seq",
                                new XElement(rdf + "li", Safe(invoice.Company.CompanyName)))),
                        new XElement(pdf + "Producer", "InvoiceWizard"),
                        new XElement(xmp + "CreatorTool", "InvoiceWizard"),
                        new XElement(xmp + "CreateDate", invoice.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ssK")),
                        new XElement(xmp + "ModifyDate", DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ssK")),
                        new XElement(xmp + "MetadataDate", DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ssK")),
                        new XElement(xmp + "Label", subject)),
                    new XElement(rdf + "Description",
                        new XAttribute(rdf + "about", string.Empty),
                        new XAttribute(XNamespace.Xmlns + "fx", fx),
                        new XElement(fx + "DocumentType", "INVOICE"),
                        new XElement(fx + "DocumentFileName", "factur-x.xml"),
                        new XElement(fx + "Version", "1.0"),
                        new XElement(fx + "ConformanceLevel", "BASIC WL")),
                    extensionSchemaDescription)));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildPdfaExtensionProperty(XNamespace rdf, XNamespace pdfaProperty, string name, string valueType, string category, string description)
    {
        return new XElement(rdf + "li",
            new XAttribute(rdf + "parseType", "Resource"),
            new XElement(pdfaProperty + "name", name),
            new XElement(pdfaProperty + "valueType", valueType),
            new XElement(pdfaProperty + "category", category),
            new XElement(pdfaProperty + "description", description));
    }

    private static IBlockElement BuildTopSection(InvoiceDocument invoice, PdfFont bold, PdfFont font, DeviceRgb muted)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 0.9f })).UseAllAvailableWidth();
        table.SetBorder(Border.NO_BORDER);

        var customerAddress = new Paragraph()
            .Add(new Text(Safe(invoice.Customer.Name)).SetFont(bold))
            .Add($"\n{BuildAddressLine(invoice.Customer.Street, invoice.Customer.HouseNumber)}")
            .Add($"\n{BuildAddressLine(invoice.Customer.PostalCode, invoice.Customer.City)}")
            .SetFont(font)
            .SetFontSize(11)
            .SetMargin(0);

        var companyAddress = new Paragraph()
            .Add(new Text(Safe(invoice.Company.CompanyName)).SetFont(bold))
            .Add($"\n{BuildAddressLine(invoice.Company.CompanyStreet, invoice.Company.CompanyHouseNumber)}")
            .Add($"\n{BuildAddressLine(invoice.Company.CompanyPostalCode, invoice.Company.CompanyCity)}")
            .Add(string.IsNullOrWhiteSpace(invoice.Company.CompanyPhoneNumber) ? string.Empty : $"\nTelefon: {invoice.Company.CompanyPhoneNumber}")
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
            .Add(Safe(invoice.CustomerNumber))
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

    private static Table BuildLinesTable(
        IReadOnlyList<InvoiceLine> lines,
        PdfFont bold,
        PdfFont font,
        DeviceRgb accent,
        bool showIntermediateSubtotal,
        decimal intermediateSubtotal,
        bool showCarryForward,
        decimal carryForward,
        bool showGrandTotal,
        decimal vatAmount,
        decimal grandTotal,
        bool markGrandTotal)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 0.6f, 3.6f, 0.8f, 0.8f, 1.1f, 1.2f })).UseAllAvailableWidth();
        table.SetFixedLayout();
        table.SetMarginTop(10);
        table.SetKeepTogether(false);

        if (showCarryForward)
        {
            table.AddCell(new Cell(1, 5)
                .Add(new Paragraph("Übertrag").SetFont(bold).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                .SetPadding(6)
                .SetBorderTop(Border.NO_BORDER)
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(Border.NO_BORDER)
                .SetBorderRight(Border.NO_BORDER));
            table.AddCell(new Cell()
                .Add(new Paragraph(FormatCurrency(carryForward)).SetFont(bold).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                .SetPadding(6)
                .SetBorderTop(Border.NO_BORDER)
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(Border.NO_BORDER)
                .SetBorderRight(Border.NO_BORDER));
        }

        AddHeaderCell(table, "Pos.", bold, accent);
        AddHeaderCell(table, "Beschreibung", bold, accent);
        AddHeaderCell(table, "Menge", bold, accent);
        AddHeaderCell(table, "Einheit", bold, accent);
        AddHeaderCell(table, "Preis", bold, accent);
        AddHeaderCell(table, "Gesamt", bold, accent);

        foreach (var line in lines)
        {
            AddBodyCell(table, line.Position.ToString(), font, TextAlignment.LEFT);
            AddBodyCell(table, SanitizeLineDescription(line.Description), font, TextAlignment.LEFT);
            AddBodyCell(table, line.Quantity.ToString("0.##"), font, TextAlignment.RIGHT);
            AddBodyCell(table, line.Unit, font, TextAlignment.LEFT);
            AddBodyCell(table, FormatCurrency(line.UnitPrice), font, TextAlignment.RIGHT);
            AddBodyCell(table, FormatCurrency(line.LineTotal), font, TextAlignment.RIGHT);
        }

        if (showIntermediateSubtotal)
        {
            table.AddCell(new Cell(1, 5)
                .Add(new Paragraph("Zwischensumme").SetFont(bold).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                .SetPadding(6)
                .SetBorderTop(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(Border.NO_BORDER)
                .SetBorderRight(Border.NO_BORDER));
            table.AddCell(new Cell()
                .Add(new Paragraph(FormatCurrency(intermediateSubtotal)).SetFont(bold).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                .SetPadding(6)
                .SetBorderTop(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(Border.NO_BORDER)
                .SetBorderRight(Border.NO_BORDER));
        }

        if (showGrandTotal)
        {
            var totalTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f })).UseAllAvailableWidth();
            if (vatAmount > 0m)
            {
                totalTable.AddCell(new Cell()
                    .Add(new Paragraph("zzgl. 19 % USt.").SetFont(font).SetFontSize(10))
                    .SetPadding(6)
                    .SetBorderTop(new SolidBorder(ColorConstants.BLACK, 1))
                    .SetBorderBottom(Border.NO_BORDER)
                    .SetBorderLeft(new SolidBorder(ColorConstants.BLACK, 1))
                    .SetBorderRight(Border.NO_BORDER));
                totalTable.AddCell(new Cell()
                    .Add(new Paragraph(FormatCurrency(vatAmount)).SetFont(font).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                    .SetPadding(6)
                    .SetBorderTop(new SolidBorder(ColorConstants.BLACK, 1))
                    .SetBorderBottom(Border.NO_BORDER)
                    .SetBorderLeft(new SolidBorder(ColorConstants.BLACK, 1))
                    .SetBorderRight(new SolidBorder(ColorConstants.BLACK, 1)));
            }

            totalTable.AddCell(new Cell()
                .Add(new Paragraph(markGrandTotal ? "Gesamtbetrag*" : "Gesamtbetrag").SetFont(bold).SetFontSize(10))
                .SetPadding(6)
                .SetKeepTogether(true)
                .SetBorderTop(vatAmount > 0m ? Border.NO_BORDER : new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderRight(Border.NO_BORDER));
            totalTable.AddCell(new Cell()
                .Add(new Paragraph(FormatCurrency(grandTotal)).SetFont(font).SetFontSize(10).SetTextAlignment(TextAlignment.RIGHT))
                .SetPadding(6)
                .SetBorderTop(vatAmount > 0m ? Border.NO_BORDER : new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderLeft(new SolidBorder(ColorConstants.BLACK, 1))
                .SetBorderRight(new SolidBorder(ColorConstants.BLACK, 1)));

            table.AddCell(new Cell(1, 3)
                .SetBorder(Border.NO_BORDER)
                .SetPadding(0));
            table.AddCell(new Cell(1, 3)
                .Add(totalTable)
                .SetBorder(Border.NO_BORDER)
                .SetPaddingTop(0)
                .SetPaddingBottom(0)
                .SetPaddingLeft(0)
                .SetPaddingRight(0));
        }

        return table;
    }

    private static IBlockElement BuildSummarySection(InvoiceDocument invoice, PdfDocument pdf, PdfFont bold, PdfFont font)
    {
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.4f, 1f })).UseAllAvailableWidth();
        table.SetBorder(Border.NO_BORDER);
        table.SetMarginTop(14);
        table.SetKeepTogether(true);

        var qrText = BuildEpcQrPayload(invoice);
        var qr = new BarcodeQRCode(qrText).CreateFormXObject(ColorConstants.BLACK, pdf);
        var image = new Image(qr).ScaleToFit(88, 88);

        var left = new Paragraph("Bitte überweisen Sie den Rechnungsbetrag unter Angabe von Kundennummer und Rechnungsnummer.")
            .SetFont(font)
            .SetFontSize(10)
            .SetMargin(0);
        left.Add($"\nKundennummer: {invoice.CustomerNumber}");
        left.Add($"\nRechnungsnummer: {invoice.InvoiceNumber}");
        left.Add("\n\nVielen Dank für die gute Zusammenarbeit.");

        var rightWrapper = new Div().SetTextAlignment(TextAlignment.RIGHT);
        var qrCard = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 1f })).UseAllAvailableWidth();
        qrCard.SetBorder(new SolidBorder(new DeviceRgb(210, 210, 210), 1));
        qrCard.SetPadding(0);
        qrCard.AddCell(new Cell(1, 2)
            .Add(new Paragraph("Überweisen per QR Code")
                .SetFont(bold)
                .SetFontSize(11)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(0)
                .SetMarginBottom(0))
            .SetBorder(Border.NO_BORDER)
            .SetPaddingTop(12)
            .SetPaddingBottom(4)
            .SetPaddingLeft(12)
            .SetPaddingRight(12));
        qrCard.AddCell(new Cell()
            .Add(new Paragraph("Ganz bequem Code mit der\nBanking-App scannen.")
                .SetFont(font)
                .SetFontSize(10)
                .SetMargin(0)
                .SetMultipliedLeading(1.2f))
            .SetBorder(Border.NO_BORDER)
            .SetPadding(12)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE));
        qrCard.AddCell(new Cell()
            .Add(new Paragraph().Add(image).SetTextAlignment(TextAlignment.CENTER).SetMargin(0))
            .SetBorder(Border.NO_BORDER)
            .SetPadding(12)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE));
        rightWrapper.Add(qrCard);

        table.AddCell(new Cell().Add(left).SetBorder(Border.NO_BORDER).SetPadding(0).SetVerticalAlignment(VerticalAlignment.TOP));
        table.AddCell(new Cell().Add(rightWrapper).SetBorder(Border.NO_BORDER).SetPadding(0));
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
            Safe(invoice.Company.CompanyName),
            Safe(invoice.Company.BankIban),
            $"EUR{amount}",
            string.Empty,
            remittance,
            string.Empty
        });
    }

    private static string BuildAddressLine(string? first, string? second)
    {
        return string.Join(" ", new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string Safe(string? value)
    {
        return value ?? string.Empty;
    }

    private static string SanitizeLineDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return ProjectPrefixRegex().Replace(value.Trim(), string.Empty);
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("0.00") + " EUR";
    }

    private static void AttachZugferdXml(InvoiceDocument invoice, PdfDocument pdf)
    {
        var xmlBytes = BuildZugferdXml(invoice);
        var fileSpec = PdfFileSpec.CreateEmbeddedFileSpec(
            pdf,
            xmlBytes,
            "Factur-X invoice",
            "factur-x.xml",
            new PdfName("application/xml"),
            null,
            PdfName.Alternative);

        pdf.AddAssociatedFile("factur-x.xml", fileSpec);
    }

    private static byte[] BuildZugferdXml(InvoiceDocument invoice)
    {
        XNamespace rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
        XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
        XNamespace udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
        XNamespace qdt = "urn:un:unece:uncefact:data:standard:QualifiedDataType:100";

        var sellerParty = BuildTradeParty(
            invoice.Company.CompanyName,
            invoice.Company.CompanyStreet,
            invoice.Company.CompanyHouseNumber,
            invoice.Company.CompanyPostalCode,
            invoice.Company.CompanyCity,
            invoice.Company.CompanyEmailAddress,
            ram);

        var buyerParty = new XElement(ram + "BuyerTradeParty",
            new XElement(ram + "Name", Safe(invoice.Customer.Name)),
            new XElement(ram + "PostalTradeAddress",
                new XElement(ram + "PostcodeCode", Safe(invoice.Customer.PostalCode)),
                new XElement(ram + "LineOne", BuildAddressLine(invoice.Customer.Street, invoice.Customer.HouseNumber)),
                new XElement(ram + "CityName", Safe(invoice.Customer.City))),
            new XElement(ram + "URIUniversalCommunication",
                new XElement(ram + "URIID", Safe(invoice.Customer.EmailAddress))));

        var netTotal = invoice.NetTotal.ToString("0.00", CultureInfo.InvariantCulture);
        var vatAmount = invoice.VatAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var grossTotal = invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var taxRate = invoice.ApplySmallBusinessRegulation ? "0.00" : "19.00";
        var taxCategory = invoice.ApplySmallBusinessRegulation ? "E" : "S";

        var exchangedDocumentContext = new XElement(rsm + "ExchangedDocumentContext",
            new XElement(ram + "GuidelineSpecifiedDocumentContextParameter",
                new XElement(ram + "ID", "urn:factur-x.eu:1p0:basicwl")),
            new XElement(ram + "BusinessProcessSpecifiedDocumentContextParameter",
                new XElement(ram + "ID", "A1")));

        var exchangedDocument = new XElement(rsm + "ExchangedDocument",
            new XElement(ram + "ID", invoice.InvoiceNumber),
            new XElement(ram + "TypeCode", "380"),
            new XElement(ram + "IssueDateTime",
                new XElement(udt + "DateTimeString",
                    new XAttribute("format", "102"),
                    invoice.InvoiceDate.ToString("yyyyMMdd"))),
            new XElement(ram + "IncludedNote",
                new XElement(ram + "Content", string.IsNullOrWhiteSpace(invoice.Subject) ? "Rechnung" : invoice.Subject)));

        var headerAgreement = new XElement(ram + "ApplicableHeaderTradeAgreement",
            sellerParty,
            buyerParty);

        var headerDelivery = new XElement(ram + "ApplicableHeaderTradeDelivery",
            new XElement(ram + "ActualDeliverySupplyChainEvent",
                new XElement(ram + "OccurrenceDateTime",
                    new XElement(udt + "DateTimeString",
                        new XAttribute("format", "102"),
                        invoice.DeliveryDate.ToString("yyyyMMdd")))));

        var settlement = new XElement(ram + "ApplicableHeaderTradeSettlement",
            new XElement(ram + "InvoiceCurrencyCode", "EUR"),
            new XElement(ram + "SpecifiedTradeSettlementPaymentMeans",
                new XElement(ram + "TypeCode", "58"),
                new XElement(ram + "PayeePartyCreditorFinancialAccount",
                    new XElement(ram + "IBANID", Safe(invoice.Company.BankIban))),
                new XElement(ram + "PayeeSpecifiedCreditorFinancialInstitution",
                    new XElement(ram + "BICID", Safe(invoice.Company.BankBic)))),
            new XElement(ram + "ApplicableTradeTax",
                new XElement(ram + "CalculatedAmount", vatAmount),
                new XElement(ram + "TypeCode", "VAT"),
                new XElement(ram + "BasisAmount", netTotal),
                new XElement(ram + "CategoryCode", taxCategory),
                new XElement(ram + "RateApplicablePercent", taxRate)),
            new XElement(ram + "SpecifiedTradeSettlementHeaderMonetarySummation",
                new XElement(ram + "LineTotalAmount", netTotal),
                new XElement(ram + "TaxBasisTotalAmount", netTotal),
                new XElement(ram + "TaxTotalAmount",
                    new XAttribute("currencyID", "EUR"),
                    vatAmount),
                new XElement(ram + "GrandTotalAmount", grossTotal),
                new XElement(ram + "DuePayableAmount", grossTotal)));

        var tradeTransaction = new XElement(rsm + "SupplyChainTradeTransaction",
            invoice.Lines.Select(line => BuildTradeLine(invoice, line, ram, qdt)),
            headerAgreement,
            headerDelivery,
            settlement);

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(rsm + "CrossIndustryInvoice",
                new XAttribute(XNamespace.Xmlns + "rsm", rsm),
                new XAttribute(XNamespace.Xmlns + "ram", ram),
                new XAttribute(XNamespace.Xmlns + "udt", udt),
                new XAttribute(XNamespace.Xmlns + "qdt", qdt),
                exchangedDocumentContext,
                exchangedDocument,
                tradeTransaction));

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static XElement BuildTradeLine(InvoiceDocument invoice, InvoiceLine line, XNamespace ram, XNamespace qdt)
    {
        return new XElement(ram + "IncludedSupplyChainTradeLineItem",
            new XElement(ram + "AssociatedDocumentLineDocument",
                new XElement(ram + "LineID", line.Position)),
            new XElement(ram + "SpecifiedTradeProduct",
                new XElement(ram + "Name", SanitizeLineDescription(line.Description))),
            new XElement(ram + "SpecifiedLineTradeAgreement",
                new XElement(ram + "GrossPriceProductTradePrice",
                    new XElement(ram + "ChargeAmount", line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture))),
                new XElement(ram + "NetPriceProductTradePrice",
                    new XElement(ram + "ChargeAmount", line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)))),
            new XElement(ram + "SpecifiedLineTradeDelivery",
                new XElement(ram + "BilledQuantity",
                    new XAttribute("unitCode", MapUnitCode(line.Unit)),
                    line.Quantity.ToString("0.##", CultureInfo.InvariantCulture))),
            new XElement(ram + "SpecifiedLineTradeSettlement",
                new XElement(ram + "ApplicableTradeTax",
                    new XElement(ram + "TypeCode", "VAT"),
                    new XElement(ram + "CategoryCode", invoice.ApplySmallBusinessRegulation ? "E" : "S"),
                    new XElement(ram + "RateApplicablePercent", invoice.ApplySmallBusinessRegulation ? "0.00" : "19.00")),
                new XElement(ram + "SpecifiedTradeSettlementLineMonetarySummation",
                    new XElement(ram + "LineTotalAmount", line.LineTotal.ToString("0.00", CultureInfo.InvariantCulture)))));
    }

    private static XElement BuildTradeParty(string name, string street, string houseNumber, string postalCode, string city, string email, XNamespace ram)
    {
        return new XElement(ram + "SellerTradeParty",
            new XElement(ram + "Name", Safe(name)),
            new XElement(ram + "PostalTradeAddress",
                new XElement(ram + "PostcodeCode", Safe(postalCode)),
                new XElement(ram + "LineOne", BuildAddressLine(street, houseNumber)),
                new XElement(ram + "CityName", Safe(city))),
            new XElement(ram + "URIUniversalCommunication",
                new XElement(ram + "URIID", Safe(email))));
    }

    private static string MapUnitCode(string? unit)
    {
        var normalized = (unit ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "ST" => "H87",
            "H" => "HUR",
            "M" => "MTR",
            "KG" => "KGM",
            _ => "C62"
        };
    }

    private static List<InvoicePage> PaginateLines(IReadOnlyList<InvoiceLine> lines)
    {
        const int firstPageCapacity = 16;
        const int followingPageCapacity = 24;

        var pages = new List<InvoicePage>();
        var current = new List<InvoiceLine>();
        var capacity = firstPageCapacity;
        var usage = 0;

        foreach (var line in lines)
        {
            var rowUnits = EstimateRowUnits(line);
            if (current.Count > 0 && usage + rowUnits > capacity)
            {
                pages.Add(new InvoicePage(current));
                current = [];
                capacity = followingPageCapacity;
                usage = 0;
            }

            current.Add(line);
            usage += rowUnits;
        }

        if (current.Count > 0)
        {
            pages.Add(new InvoicePage(current));
        }

        return pages.Count == 0 ? [new InvoicePage([])] : pages;
    }

    private static int EstimateRowUnits(InvoiceLine line)
    {
        var sanitized = SanitizeLineDescription(line.Description);
        var lineBreaks = sanitized.Count(c => c == '\n');
        var textLength = sanitized.Replace("\r", string.Empty).Replace("\n", string.Empty).Length;
        var wrappedLines = Math.Max(1, (int)Math.Ceiling(textLength / 34d));
        return Math.Max(1, wrappedLines + lineBreaks);
    }

    [GeneratedRegex(@"^\[[^\]]+\]\s*", RegexOptions.Compiled)]
    private static partial Regex ProjectPrefixRegex();

    private sealed class InvoicePage(List<InvoiceLine> lines)
    {
        public List<InvoiceLine> Lines { get; } = lines;
        public decimal PageSubtotal => Lines.Sum(x => x.LineTotal);
    }
}

internal sealed class DraftWatermarkHandler : iText.Kernel.Events.IEventHandler
{
    public void HandleEvent(iText.Kernel.Events.Event @event)
    {
        var documentEvent = (iText.Kernel.Events.PdfDocumentEvent)@event;
        var page = documentEvent.GetPage();
        var pageSize = page.GetPageSize();
        var pdfCanvas = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), documentEvent.GetDocument());
        var font = CustomerInvoicePdfService.CreateBoldPdfFont();
        var gs = new PdfExtGState().SetFillOpacity(0.22f);

        pdfCanvas.SaveState();
        pdfCanvas.SetExtGState(gs);
        pdfCanvas.BeginText();
        pdfCanvas.SetFontAndSize(font, 96);
        pdfCanvas.SetColor(new DeviceRgb(180, 40, 40), true);
        pdfCanvas.SetTextMatrix((float)Math.Cos(Math.PI / 4), (float)Math.Sin(Math.PI / 4), (float)-Math.Sin(Math.PI / 4), (float)Math.Cos(Math.PI / 4), pageSize.GetWidth() / 2 - 170, pageSize.GetHeight() / 2 - 40);
        pdfCanvas.ShowText("ENTWURF");
        pdfCanvas.EndText();
        pdfCanvas.RestoreState();
    }
}

internal sealed class InvoiceFooterHandler(CustomerInvoicePdfService.InvoiceDocument invoice) : iText.Kernel.Events.IEventHandler
{
    public void HandleEvent(iText.Kernel.Events.Event @event)
    {
        var documentEvent = (iText.Kernel.Events.PdfDocumentEvent)@event;
        var pdfDocument = documentEvent.GetDocument();
        var page = documentEvent.GetPage();
        var pageSize = page.GetPageSize();
        var pdfCanvas = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdfDocument);
        using var canvas = new Canvas(pdfCanvas, pageSize);
        var font = CustomerInvoicePdfService.CreateRegularPdfFont();
        var bold = CustomerInvoicePdfService.CreateBoldPdfFont();
        var muted = new DeviceRgb(96, 108, 128);
        var currentPage = pdfDocument.GetPageNumber(page);
        var totalPages = pdfDocument.GetNumberOfPages();

        pdfCanvas.SaveState();
        pdfCanvas.SetStrokeColor(muted);
        pdfCanvas.MoveTo(pageSize.GetLeft() + 42, pageSize.GetBottom() + 56);
        pdfCanvas.LineTo(pageSize.GetRight() - 42, pageSize.GetBottom() + 56);
        pdfCanvas.Stroke();
        pdfCanvas.RestoreState();

        canvas.Add(new Paragraph()
            .Add(new Text("Steuernummer\n").SetFont(bold))
            .Add(string.IsNullOrWhiteSpace(invoice.Company.TaxNumber) ? "-" : invoice.Company.TaxNumber)
            .SetFont(font)
            .SetFontSize(8)
            .SetFontColor(muted)
            .SetFixedPosition(pageSize.GetLeft() + 42, pageSize.GetBottom() + 18, 220));

        canvas.Add(new Paragraph()
            .Add(new Text("Bankverbindung\n").SetFont(bold))
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankName) ? "-" : invoice.Company.BankName)
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankIban) ? string.Empty : $"\nIBAN {invoice.Company.BankIban}")
            .Add(string.IsNullOrWhiteSpace(invoice.Company.BankBic) ? string.Empty : $"\nBIC {invoice.Company.BankBic}")
            .SetFont(font)
            .SetFontSize(8)
            .SetFontColor(muted)
            .SetTextAlignment(TextAlignment.RIGHT)
            .SetFixedPosition(pageSize.GetRight() - 262, pageSize.GetBottom() + 18, 220));

        canvas.Add(new Paragraph($"Seite {currentPage} von {totalPages}")
            .SetFont(font)
            .SetFontSize(8)
            .SetFontColor(muted)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFixedPosition(pageSize.GetLeft() + 220, pageSize.GetBottom() + 30, pageSize.GetWidth() - 440));
    }
}
