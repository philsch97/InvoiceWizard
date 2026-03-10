using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;

namespace InvoiceWizard
{
    public static class ZugferdExtractor
    {
        public static byte[]? ExtractEmbeddedXml(string pdfPath)
        {
            using var reader = new PdfReader(pdfPath);
            using var pdf = new PdfDocument(reader);

            var catalog = pdf.GetCatalog().GetPdfObject();

            // /Names -> /EmbeddedFiles
            var namesDict = catalog.GetAsDictionary(PdfName.Names);
            if (namesDict == null) return null;

            var embeddedFiles = namesDict.GetAsDictionary(PdfName.EmbeddedFiles);
            if (embeddedFiles == null) return null;

            // Häufig: /Names Array [name1, filespec1, name2, filespec2, ...]
            var namesArray = embeddedFiles.GetAsArray(PdfName.Names);
            if (namesArray == null) return null;

            for (int i = 0; i < namesArray.Size(); i += 2)
            {
                var nameObj = namesArray.Get(i);
                var fsObj = namesArray.Get(i + 1);

                var fileName = nameObj?.ToString() ?? "";
                if (!fileName.EndsWith(".xml", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fsObj is PdfDictionary fsDict)
                {
                    var bytes = TryGetEmbeddedFileBytes(fsDict);
                    if (bytes != null && bytes.Length > 0)
                        return bytes;
                }
            }

            return null;
        }

        private static byte[]? TryGetEmbeddedFileBytes(PdfDictionary fileSpecDict)
        {
            // FileSpec: /EF enthält eingebettete Streams (meist /F oder /UF)
            var ef = fileSpecDict.GetAsDictionary(PdfName.EF);
            if (ef == null) return null;

            // Erst /UF probieren, dann /F
            var stream = ef.GetAsStream(PdfName.UF) ?? ef.GetAsStream(PdfName.F);
            if (stream == null) return null;

            return stream.GetBytes();
        }
    }
}
