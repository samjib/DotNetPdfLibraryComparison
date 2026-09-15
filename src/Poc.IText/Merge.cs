using InvoicePoc;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (iText).</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        var temp = MergeSupport.TempInvoice();
        InvoiceDocument.Write(invoice, temp);

        using var target = new PdfDocument(new PdfWriter(path));
        // Outlines (bookmarks) and structure tags are merged when asked for.
        var merger = new PdfMerger(target, new PdfMergerProperties().SetMergeOutlines(true).SetMergeTags(true));
        foreach (var source in new[] { temp, MergeSupport.TermsPath })
        {
            using var pdf = new PdfDocument(new PdfReader(source));
            merger.Merge(pdf, 1, pdf.GetNumberOfPages());
        }
        merger.Close();
        File.Delete(temp);
    }
}
