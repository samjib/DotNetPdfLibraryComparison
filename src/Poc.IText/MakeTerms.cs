using InvoicePoc;
using InvoicePoc.Stress;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Navigation;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

/// <summary>
/// Builds the "supplied by someone else" terms and conditions PDF that the merge tests append.
/// Deliberately awkward: US Letter (not A4), its own fonts, PDF outline bookmarks, an external
/// hyperlink and its own footer page numbers, so we can see what each merge keeps.
/// </summary>
static class TermsPdf
{
    public static void Generate(string path)
    {
        var pdf = new PdfDocument(new PdfWriter(path));
        pdf.GetDocumentInfo().SetTitle("Terms and conditions of supply").SetAuthor("Brightwater Digital Ltd");
        var serif = PdfFontFactory.CreateFont(FontProgramFactory.CreateFont(StressAssets.SerifDisplay), PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        var body = PdfFontFactory.CreateFont(FontProgramFactory.CreateFont(StressAssets.Fallback), PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);

        using var doc = new Document(pdf, PageSize.LETTER, false);   // US Letter, not A4
        doc.SetMargins(56, 56, 56, 56);   // SetMargins returns void in iText 9
        doc.SetFont(body).SetFontSize(10);

        doc.Add(new Paragraph("Terms and conditions of supply").SetFont(serif).SetFontSize(22).SetFontColor(new DeviceRgb(0x0F, 0x52, 0x57)));
        doc.Add(new Paragraph("Version 4.2, effective 1 July 2026").SetFontColor(new DeviceRgb(0x5B, 0x6B, 0x6D)).SetMarginBottom(18));

        var outline = pdf.GetOutlines(false);
        var clausePages = new int[8];
        for (var clause = 1; clause <= 8; clause++)
        {
            doc.Add(new Paragraph($"{clause}. {Headings[clause - 1]}").SetFont(serif).SetFontSize(13).SetMarginTop(12).SetKeepWithNext(true));
            clausePages[clause - 1] = pdf.GetNumberOfPages();   // content is laid out as it is added
            for (var para = 0; para < 3; para++)
                doc.Add(new Paragraph(Body[(clause + para) % Body.Length]).SetTextAlignment(TextAlignment.JUSTIFIED).SetMarginBottom(6));
        }

        var link = new Link("www.brightwater.example/terms", PdfAction.CreateURI("https://www.brightwater.example/terms"));
        doc.Add(new Paragraph("The current version is published at ").Add(link.SetFontColor(new DeviceRgb(0x0F, 0x52, 0x57)).SetUnderline()).Add(".").SetMarginTop(14));

        // Bookmarks pointing at the page each clause started on.
        for (var clause = 1; clause <= 8; clause++)
            outline.AddOutline($"{clause}. {Headings[clause - 1]}")
                .AddDestination(PdfExplicitDestination.CreateFit(pdf.GetPage(clausePages[clause - 1])));

        var total = pdf.GetNumberOfPages();
        for (var i = 1; i <= total; i++)
            new Canvas(pdf.GetPage(i), new Rectangle(56, 28, PageSize.LETTER.GetWidth() - 112, 20))
                .SetFont(body).SetFontSize(8).SetFontColor(new DeviceRgb(0x5B, 0x6B, 0x6D))
                .Add(new Paragraph($"Terms and conditions v4.2 — page {i} of {total}").SetTextAlignment(TextAlignment.CENTER))
                .Close();
        Console.WriteLine($"Wrote {path} ({total} pages, US Letter, {8} bookmarks, 1 hyperlink).");
    }

    static readonly string[] Headings =
    [
        "Interpretation", "Supply of services", "Customer obligations", "Charges and payment",
        "Intellectual property", "Limitation of liability", "Data protection", "Termination",
    ];

    static readonly string[] Body =
    [
        "The Supplier shall provide the Services to the Customer in accordance with the Order in all material respects, using reasonable skill and care, and shall use all reasonable endeavours to meet any performance dates specified, but any such dates are estimates only and time is not of the essence.",
        "The Customer shall provide the Supplier with such information and materials as the Supplier may reasonably require in order to supply the Services, and shall ensure that such information is complete and accurate in all material respects.",
        "The Charges for the Services shall be as set out in the Order or, where no charges are specified, calculated on a time and materials basis at the Supplier's standard daily rates in force from time to time.",
        "All intellectual property rights in or arising out of or in connection with the Services shall be owned by the Supplier, save that the Customer shall own all rights in the Customer Materials and in the specific deliverables identified in the Order as Customer Deliverables.",
        "Nothing in this agreement shall limit or exclude the Supplier's liability for death or personal injury caused by its negligence, for fraud or fraudulent misrepresentation, or for any other liability that cannot be limited or excluded by law.",
        "Each party shall comply with all applicable requirements of the Data Protection Legislation, and this clause is in addition to, and does not relieve, remove or replace, a party's obligations or rights under that legislation.",
    ];
}
