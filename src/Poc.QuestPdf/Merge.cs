using InvoicePoc;
using QuestPDF.Fluent;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice.</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        var temp = MergeSupport.TempInvoice();
        new InvoiceDocument(invoice).GeneratePdf(temp);

        // DocumentOperation works on existing PDF files (separate from Document.Merge, which only
        // combines QuestPDF-generated documents). It can also take page ranges, overlay/underlay
        // content, attach files and encrypt.
        DocumentOperation
            .LoadFile(temp)
            .MergeFile(MergeSupport.TermsPath)
            .Save(path);

        File.Delete(temp);
    }
}
