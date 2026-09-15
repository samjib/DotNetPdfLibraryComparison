using InvoicePoc;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (Syncfusion).</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        var temp = MergeSupport.TempInvoice();
        InvoiceDocument.Write(invoice, temp);

        using var merged = new PdfDocument();
        using var invoiceStream = File.OpenRead(temp);
        using var termsStream = File.OpenRead(MergeSupport.TermsPath);
        // Static Merge takes the destination document plus the sources (streams, paths or loaded documents).
        PdfDocumentBase.Merge(merged, [invoiceStream, termsStream]);

        using (var file = File.Create(path)) merged.Save(file);
        File.Delete(temp);
    }
}
