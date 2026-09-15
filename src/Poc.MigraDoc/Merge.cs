using InvoicePoc;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (PDFsharp, MIT).</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        var renderer = new PdfDocumentRenderer { Document = InvoiceDocument.Build(invoice) };
        renderer.RenderDocument();
        var merged = renderer.PdfDocument;

        // Import mode is required to copy pages between documents.
        using var terms = PdfReader.Open(MergeSupport.TermsPath, PdfDocumentOpenMode.Import);
        foreach (var page in terms.Pages) merged.AddPage(page);

        StampPageNumbers(merged);
        merged.Save(path);
    }

    /// <summary>
    /// The invoice's own footer still says "Page 1 of 2", because it was painted before the merge.
    /// This is the usual fix: omit page numbers when generating, then stamp them across the merged
    /// file. Drawn bottom-left here so the demo doesn't overlap the numbering already there.
    /// </summary>
    static void StampPageNumbers(PdfDocument pdf)
    {
        var font = new XFont(Brand.FontFamily, 7.5);
        var brush = new XSolidBrush(XColor.FromArgb(0x5B, 0x6B, 0x6D));
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var page = pdf.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString($"{i + 1} of {pdf.PageCount}", font, brush, new XPoint(Brand.Mm(10), page.Height.Point - Brand.Mm(6)));
        }
    }
}
