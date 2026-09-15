using InvoicePoc;
using Aspose.Pdf;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (Aspose.PDF).</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        var temp = MergeSupport.TempInvoice();
        InvoiceDocument.Write(invoice, temp);

        using var merged = new Document(temp);
        using var terms = new Document(MergeSupport.TermsPath);
        // NOTE: the API is right, but unlicensed evaluation mode throws IndexOutOfRangeException
        // ("At most 4 elements ... in evaluation mode") once the result exceeds 4 pages.
        // Verified working when the merged document is 4 pages or fewer.
        merged.Pages.Add(terms.Pages);          // appends every page
        merged.Save(path);
        File.Delete(temp);
    }
}
