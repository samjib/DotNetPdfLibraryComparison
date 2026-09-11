using InvoicePoc;
using InvoicePoc.Stress;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    static Unit Mm(double mm) => Unit.FromMillimeter(mm);
    static readonly Color Accent = Hex(Brand.Accent), Tint = Hex(Brand.AccentTint), Ink = Hex(Brand.Ink),
        Muted = Hex(Brand.Muted), RuleColour = Hex(Brand.Rule), Zebra = Hex("#F4F8F7");

    public static void Generate(string path)
    {
        var renderer = new PdfDocumentRenderer { Document = Build() };
        renderer.RenderDocument();
        // WORKAROUND: MigraDoc footers are identical on every page (apart from a different first page),
        // so the "continued / end of statement" text is stamped afterwards with PDFsharp.
        StampFooterText(renderer.PdfDocument);
        renderer.PdfDocument.Save(path);
    }

    static Document Build()
    {
        var doc = new Document();
        doc.Info.Title = StressData.Title;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Brand.FontFamily;
        normal.Font.Size = 9;
        normal.Font.Color = Ink;

        var section = doc.AddSection();
        var setup = section.PageSetup;
        setup.PageWidth = Mm(210); setup.PageHeight = Mm(297);
        setup.LeftMargin = setup.RightMargin = Mm(Brand.MarginSideMm);
        setup.TopMargin = Mm(20); setup.BottomMargin = Mm(18);
        setup.HeaderDistance = Mm(10); setup.FooterDistance = Mm(8);
        setup.DifferentFirstPageHeaderFooter = true;   // native: page 1 gets its own (empty) header

        // Running header (pages 2+). No SVG support, so the logo is the PNG version.
        var header = section.Headers.Primary.AddTable();
        header.AddColumn(Mm(8)); header.AddColumn(Mm(170));
        var hr = header.AddRow();
        hr.Borders.Bottom.Width = 0.5; hr.Borders.Bottom.Color = RuleColour; hr.BottomPadding = Mm(2);
        Picture(hr.Cells[0].AddParagraph(), StressAssets.LogoPng, heightMm: 6);
        hr.Cells[1].VerticalAlignment = VerticalAlignment.Center;
        var ht = hr.Cells[1].AddParagraph(StressData.HeaderText);
        ht.Format.Font.Size = 8; ht.Format.Font.Color = Muted;

        // Footer: page numbers are native fields; needed on both the first-page and primary footers.
        foreach (var footer in new[] { section.Footers.Primary, section.Footers.FirstPage })
        {
            var p = footer.AddParagraph();
            p.Format.Borders.Top.Width = 0.5; p.Format.Borders.Top.Color = RuleColour;
            p.Format.Alignment = ParagraphAlignment.Right;
            p.Format.Font.Size = 7.5; p.Format.Font.Color = Muted;
            p.AddText("Page "); p.AddPageField(); p.AddText(" of "); p.AddNumPagesField();
        }

        TitleAndCustomer(section);

        // Photo scaled to the content width, kept with its caption.
        var photo = section.AddParagraph();
        photo.Format.SpaceBefore = Mm(6);
        photo.Format.KeepWithNext = true;
        var image = photo.AddImage(Base64(StressAssets.Photo));
        image.Width = Mm(178); image.LockAspectRatio = true;
        var caption = section.AddParagraph(StressData.PhotoCaption);
        caption.Format.Font.Size = 8; caption.Format.Font.Color = Muted; caption.Format.SpaceBefore = Mm(1.5);

        Heading(section, StressData.TransactionsHeading, beforeMm: 6);
        Transactions(section);
        var balance = section.AddParagraph($"Balance due {Fmt.Money(StressData.Balance)}");
        balance.Format.Alignment = ParagraphAlignment.Right; balance.Format.Font.Bold = true;
        balance.Format.Font.Size = 11; balance.Format.SpaceBefore = Mm(3);

        Heading(section, StressData.TermsHeading, beforeMm: 8);
        TwoColumnTerms(section);
        Remittance(section);
        return doc;
    }

    static void TitleAndCustomer(Section section)
    {
        var title = section.AddTable();
        title.AddColumn(Mm(18)); title.AddColumn(Mm(105)); title.AddColumn(Mm(55));
        var r = title.AddRow();
        Picture(r.Cells[0].AddParagraph(), StressAssets.LogoPng, heightMm: 18);   // PNG: no SVG support
        var t = r.Cells[1].AddParagraph(StressData.Title);
        t.Format.Font.Name = StressAssets.SerifFamily; t.Format.Font.Size = 26; t.Format.Font.Color = Accent;
        t.Format.LeftIndent = Mm(3);
        var d = r.Cells[1].AddParagraph($"Statement date {StressData.StatementDate}");
        d.Format.Font.Color = Muted; d.Format.LeftIndent = Mm(3);
        var a = r.Cells[2].AddParagraph("Account reference");
        a.Format.Alignment = ParagraphAlignment.Right; a.Format.Font.Color = Muted;
        var aref = r.Cells[2].AddParagraph(StressData.AccountRef);
        aref.Format.Alignment = ParagraphAlignment.Right; aref.Format.Font.Name = StressAssets.MonoFamily; aref.Format.Font.Size = 11;

        var cust = section.AddTable();
        cust.AddColumn(Mm(98)); cust.AddColumn(Mm(80));
        cust.TopPadding = Mm(6);
        var c = cust.AddRow();
        c.Cells[0].AddParagraph().AddFormattedText(StressData.CustomerName, TextFormat.Bold);
        foreach (var line in StressData.BranchAddress) c.Cells[0].AddParagraph(line);
        // No complex-script shaping or right-to-left support: expect disconnected, reversed letters.
        var ar = c.Cells[1].AddParagraph(StressData.ArabicTradingName);
        ar.Format.Font.Name = StressAssets.ArabicFamily; ar.Format.Font.Size = 16; ar.Format.Alignment = ParagraphAlignment.Right;
        var al = c.Cells[1].AddParagraph(StressData.ArabicLabel);
        al.Format.Alignment = ParagraphAlignment.Right; al.Format.Font.Color = Muted;
    }

    static void Transactions(Section section)
    {
        var table = section.AddTable();
        table.TopPadding = table.BottomPadding = Mm(1.5);
        table.LeftPadding = table.RightPadding = Mm(1.5);
        table.Rows.LeftIndent = Mm(1.5);
        double[] widths = [22, 30, 10, 74, 18, 24];
        for (var i = 0; i < widths.Length; i++) table.AddColumn(Mm(widths[i]));
        table.Columns[5].Format.Alignment = ParagraphAlignment.Right;

        var head = table.AddRow();
        head.HeadingFormat = true;                   // native: repeats on every page; rows never split
        head.Shading.Color = Tint; head.Format.Font.Bold = true;
        for (var i = 0; i < StressData.Columns.Length; i++) head.Cells[i].AddParagraph(StressData.Columns[i]);

        var n = 0;
        foreach (var row in StressData.Rows())
        {
            var r = table.AddRow();
            if (n++ % 2 == 1) r.Shading.Color = Zebra;
            r.Cells[0].AddParagraph(StressData.ShortDate(row.Date));
            var reference = r.Cells[1].AddParagraph(row.Reference);
            reference.Format.Font.Name = StressAssets.MonoFamily; reference.Format.Font.Size = 8;
            if (row.Thumbnail is not null) Picture(r.Cells[2].AddParagraph(), StressAssets.Thumbnail(row.Thumbnail), heightMm: 6);
            r.Cells[3].AddParagraph(row.Description);
            var status = r.Cells[4].AddParagraph();
            if (row.Paid)
            {
                status.Format.Font.Color = Accent;
                status.AddText("Paid ");
                // WORKAROUND: no automatic font fallback, so the ✓ must be put in a font that has it.
                status.AddFormattedText("✓").Font.Name = StressAssets.FallbackFamily;
            }
            else status.AddText("Due");
            r.Cells[5].AddParagraph(Fmt.Money(row.Amount));
        }
    }

    static void TwoColumnTerms(Section section)
    {
        // WORKAROUND: MigraDoc has no multi-column flow. A two-cell table with the paragraphs split
        // by hand looks similar, but text doesn't flow from one column into the next.
        var table = section.AddTable();
        table.AddColumn(Mm(85)); table.AddColumn(Mm(8)); table.AddColumn(Mm(85));
        var row = table.AddRow();
        var half = (StressData.Terms.Length + 1) / 2;
        for (var i = 0; i < StressData.Terms.Length; i++)
        {
            var p = row.Cells[i < half ? 0 : 2].AddParagraph(StressData.Terms[i]);
            p.Format.Alignment = ParagraphAlignment.Justify; p.Format.SpaceAfter = Mm(2);
        }
    }

    static void Remittance(Section section)
    {
        var spacer = section.AddParagraph();
        spacer.Format.Font.Size = 1; spacer.Format.SpaceAfter = Mm(8);
        var box = section.AddTable();                // a single-row table never splits across pages
        box.AddColumn(Mm(178));
        box.Borders.Width = 1; box.Borders.Color = Accent;
        box.TopPadding = box.BottomPadding = box.LeftPadding = box.RightPadding = Mm(4);
        var cell = box.AddRow().Cells[0];
        var h = cell.AddParagraph(StressData.RemittanceHeading);
        h.Format.Font.Name = StressAssets.SerifFamily; h.Format.Font.Size = 13; h.Format.SpaceAfter = Mm(2);
        foreach (var (label, value) in StressData.RemittanceFields)
        {
            var p = cell.AddParagraph();
            p.Format.TabStops.AddTabStop(Mm(40)); p.Format.SpaceAfter = Mm(2);
            p.AddFormattedText(label).Color = Muted;
            p.AddTab();
            p.AddFormattedText(value).Font.Name = StressAssets.MonoFamily;
        }
    }

    static void Heading(Section section, string text, double beforeMm)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Name = StressAssets.SerifFamily; p.Format.Font.Size = 15;
        p.Format.SpaceBefore = Mm(beforeMm); p.Format.SpaceAfter = Mm(2);
        p.Format.KeepWithNext = true;                 // native: heading never orphaned
    }

    static void Picture(Paragraph p, byte[] png, double heightMm)
    {
        var image = p.AddImage(Base64(png));          // MigraDoc accepts "base64:" image strings
        image.Height = Mm(heightMm); image.LockAspectRatio = true;
    }

    static string Base64(byte[] bytes) => "base64:" + Convert.ToBase64String(bytes);

    static void StampFooterText(PdfDocument pdf)
    {
        var font = new XFont(Brand.FontFamily, 7.5);
        var brush = new XSolidBrush(XColor.FromArgb(0x5B, 0x6B, 0x6D));
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var page = pdf.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page);
            var text = i == pdf.PageCount - 1 ? StressData.EndText : StressData.ContinuedText;
            gfx.DrawString(text, font, brush, Brand.Mm(Brand.MarginSideMm), page.Height.Point - Brand.Mm(8) - 2.2);
        }
    }

    static Color Hex(string hex) { var (r, g, b) = Brand.Rgb(hex); return new Color(r, g, b); }
}

/// <summary>Maps each family used by the stress test to its embedded font file.</summary>
sealed class StressFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) => familyName switch
    {
        StressAssets.SerifFamily => new("serif"),
        StressAssets.MonoFamily => new("mono"),
        StressAssets.ArabicFamily => new("arabic"),
        StressAssets.FallbackFamily => new("fallback"),
        _ => new(isBold ? "lato-bold" : "lato", false, isItalic),
    };

    public byte[]? GetFont(string faceName) => faceName switch
    {
        "serif" => StressAssets.SerifDisplay,
        "mono" => StressAssets.Mono,
        "arabic" => StressAssets.Arabic,
        "fallback" => StressAssets.Fallback,
        "lato-bold" => Brand.LatoBold,
        _ => Brand.LatoRegular,
    };
}
