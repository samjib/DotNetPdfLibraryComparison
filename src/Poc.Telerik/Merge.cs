using InvoicePoc;
using Telerik.Documents.Fixed.Model;
using FlowPdfProvider = Telerik.Documents.Flow.FormatProviders.Pdf.PdfFormatProvider;
using FixedPdfProvider = Telerik.Documents.Fixed.FormatProviders.Pdf.PdfFormatProvider;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (Telerik).</summary>
static class MergeDocuments
{
    public static void Generate(Invoice invoice, string path)
    {
        // Export the flow (Word-like) invoice to a fixed document, then import and append the terms.
        var merged = new FlowPdfProvider().ExportToFixedDocument(InvoiceDocument.Build(invoice), TimeSpan.FromSeconds(30));

        var provider = new FixedPdfProvider();
        using (var termsStream = File.OpenRead(MergeSupport.TermsPath))
        {
            RadFixedDocument terms = provider.Import(termsStream, TimeSpan.FromSeconds(30));
            // Gotcha: moving pages between documents throws ("associated with another parent").
            // Merge() copies them properly, and also resolves clashing field/attachment names.
            merged.Merge(terms);
        }

        using var file = File.Create(path);
        provider.Export(merged, file, TimeSpan.FromSeconds(30));
    }
}
