using InvoicePoc;
using InvoicePoc.Stress;
using Telerik.Documents.Common.Model;
using Telerik.Documents.Core.Fonts;
using Telerik.Documents.Extensibility;
using Telerik.Documents.Fixed.FormatProviders.Pdf.Export;
using Telerik.Documents.Fixed.Model.ColorSpaces;
using FixedContentEditor = Telerik.Documents.Fixed.Model.Editing.FixedContentEditor;
using Telerik.Documents.Flow.Model;
using Telerik.Documents.Flow.Model.Styles;
using Telerik.Documents.Media;
using Telerik.Documents.Model;
using Telerik.Documents.Primitives;
using FlowPdf = Telerik.Documents.Flow.FormatProviders.Pdf.PdfFormatProvider;
using FixedPdf = Telerik.Documents.Fixed.FormatProviders.Pdf.PdfFormatProvider;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    static double Mm(double mm) => Unit.MmToDip(mm);
    static double Pt(double pt) => Unit.PointToDip(pt);
    static readonly Border None = new(BorderStyle.None);

    public static void Generate(string path)
    {
        var flow = Build();
        // WORKAROUND: a Word-like model has no "last page" concept, so the footer text is stamped after
        // layout via the fixed-document API (RadPdfProcessing), then that document is saved.
        var fixedDoc = new FlowPdf().ExportToFixedDocument(flow, TimeSpan.FromSeconds(60));
        for (var i = 0; i < fixedDoc.Pages.Count; i++)
        {
            var editor = new FixedContentEditor(fixedDoc.Pages[i]);
            editor.Position.Translate(Mm(16), Mm(297 - 11.2));
            editor.TextProperties.FontSize = Pt(7.5);
            editor.TextProperties.TrySetFont(new FontFamily(Brand.FontFamily));
            editor.GraphicProperties.FillColor = new RgbColor(0x5B, 0x6B, 0x6D);
            editor.DrawText(i == fixedDoc.Pages.Count - 1 ? StressData.EndText : StressData.ContinuedText);
        }
        var provider = new FixedPdf();
        provider.ExportSettings.FontEmbeddingType = FontEmbeddingType.Subset;
        using var file = File.Create(path);
        provider.Export(fixedDoc, file, TimeSpan.FromSeconds(60));
    }

    static RadFlowDocument Build()
    {
        var doc = new RadFlowDocument();
        var section = doc.Sections.AddSection();
        section.PageSize = PaperTypeConverter.ToSize(PaperTypes.A4);
        section.PageMargins = new Padding(Mm(16), Mm(20), Mm(16), Mm(18));
        section.HeaderTopMargin = Mm(9);
        section.FooterBottomMargin = Mm(6);
        section.HasDifferentFirstPageHeaderFooter = true;          // native: page 1 gets its own (empty) header

        Para(section.Headers.Add(HeaderFooterType.First), "", 1);
        var header = NewTable(section.Headers.Add(), 8, 170);
        var hr = header.Rows.AddTableRow();
        var hc1 = Cell(hr, 8); hc1.Borders = Bottom(Brand.Rule, 0.5);
        Picture(Para(hc1, "", 1), StressAssets.LogoPng, "png", 6, 6);
        var hc2 = Cell(hr, 170); hc2.Borders = Bottom(Brand.Rule, 0.5); hc2.VerticalAlignment = VerticalAlignment.Center;
        Para(hc2, StressData.HeaderText, 8, color: Brand.Muted);
        Para(section.Headers.Default!, "", 1);

        foreach (var type in new[] { HeaderFooterType.First, HeaderFooterType.Default })  // page numbers on every footer
        {
            var footer = section.Footers.Add(type);
            var p = Para(footer, "", 7.5, align: Alignment.Right);
            p.Borders = new ParagraphBorders(None, new Border(Pt(0.5), BorderStyle.Single, Colour(Brand.Rule)), None, None);
            var editor = new Telerik.Documents.Flow.Model.Editing.RadFlowDocumentEditor(doc);
            editor.MoveToParagraphStart(p);
            editor.CharacterFormatting.FontFamily.LocalValue = new ThemableFontFamily(new FontFamily(Brand.FontFamily));
            editor.CharacterFormatting.FontSize.LocalValue = Pt(7.5);
            editor.CharacterFormatting.ForegroundColor.LocalValue = Colour(Brand.Muted);
            editor.InsertText("Page "); editor.InsertField("PAGE", "1"); editor.InsertText(" of "); editor.InsertField("NUMPAGES", "1");
        }

        // Title block. FINDING: ImageSource accepts "svg", but PDF export crashed with an ArithmeticException
        // (NaN) in its arc geometry on this logo's rounded rectangle/circle, so the PNG version is used.
        var title = NewTable(section, 18, 105, 55);
        var tr = title.Rows.AddTableRow();
        Picture(Para(Cell(tr, 18), "", 1), StressAssets.LogoPng, "png", 18, 18);
        var mid = Cell(tr, 105);
        Para(mid, StressData.Title, 26, StressAssets.SerifFamily, Brand.Accent).Indentation.LeftIndent = Mm(3);
        Para(mid, $"Statement date {StressData.StatementDate}", color: Brand.Muted).Indentation.LeftIndent = Mm(3);
        var acct = Cell(tr, 55);
        Para(acct, "Account reference", color: Brand.Muted, align: Alignment.Right);
        Para(acct, StressData.AccountRef, 11, StressAssets.MonoFamily, align: Alignment.Right);

        Para(section, "", 1, after: 6);
        var cust = NewTable(section, 98, 80);
        var cr = cust.Rows.AddTableRow();
        var left = Cell(cr, 98);
        Para(left, StressData.CustomerName, bold: true);
        foreach (var line in StressData.BranchAddress) Para(left, line);
        var right = Cell(cr, 80);
        var ar = Para(right, StressData.ArabicTradingName, 16, StressAssets.ArabicFamily, align: Alignment.Right);
        ar.FlowDirection = FlowDirection.RightToLeft;              // RTL paragraph
        Para(right, StressData.ArabicLabel, color: Brand.Muted, align: Alignment.Right);

        var photo = Para(section, "", 1, before: 6);
        photo.KeepWithNextParagraph = true;
        Picture(photo, StressAssets.Photo, "jpeg", 178, 178 * 900 / 1600.0);
        Para(section, StressData.PhotoCaption, 8, color: Brand.Muted, before: 1.5);

        Heading(section, StressData.TransactionsHeading, 6);
        Transactions(section);
        Para(section, $"Balance due {Fmt.Money(StressData.Balance)}", 11, bold: true, align: Alignment.Right, before: 3);

        Heading(section, StressData.TermsHeading, 8);
        // WORKAROUND: no section columns, so two table cells with the paragraphs split by hand (no flow).
        var terms = NewTable(section, 85, 8, 85);
        var row = terms.Rows.AddTableRow();
        TableCell c1 = Cell(row, 85), gap = Cell(row, 8), c2 = Cell(row, 85);
        Para(gap, "", 1);
        for (var i = 0; i < StressData.Terms.Length; i++)
            Para(i < 3 ? c1 : c2, StressData.Terms[i], align: Alignment.Justified, after: 2);

        Para(section, "", 1, after: 8);
        var box = NewTable(section, 178);
        var br = box.Rows.AddTableRow();
        br.CanSplit = false;                                        // single unsplittable row = keep together
        var bc = Cell(br, 178);
        bc.Borders = new TableCellBorders(new Border(Pt(1), BorderStyle.Single, Colour(Brand.Accent)));
        bc.Padding = new Padding(Mm(4), Mm(4), Mm(4), Mm(4));
        Para(bc, StressData.RemittanceHeading, 13, StressAssets.SerifFamily, after: 2);
        foreach (var (label, value) in StressData.RemittanceFields)
        {
            var p = Para(bc, label, color: Brand.Muted, after: 2);
            p.TabStops = p.TabStops.Insert(new TabStop(Mm(40)));
            Run(p, "\t" + value, 9, StressAssets.MonoFamily);
        }
        Para(section, "", 1);
        return doc;
    }

    static void Transactions(Section section)
    {
        double[] widths = [22, 30, 10, 74, 18, 24];
        var table = NewTable(section, widths);
        var head = table.Rows.AddTableRow();
        head.RepeatOnEveryPage = true;                              // known: not honoured by the PDF export
        for (var i = 0; i < widths.Length; i++)
        {
            var c = Cell(head, widths[i], Brand.AccentTint);
            Para(c, StressData.Columns[i], bold: true, align: i == 5 ? Alignment.Right : Alignment.Left);
        }
        var n = 0;
        foreach (var r in StressData.Rows())
        {
            var row = table.Rows.AddTableRow();
            row.CanSplit = false;
            var bg = n++ % 2 == 1 ? "#F4F8F7" : "#FFFFFF";
            Para(Cell(row, 22, bg), StressData.ShortDate(r.Date));
            Para(Cell(row, 30, bg), r.Reference, 8, StressAssets.MonoFamily);
            var t = Para(Cell(row, 10, bg), "", 1);
            if (r.Thumbnail is not null) Picture(t, StressAssets.Thumbnail(r.Thumbnail), "png", 6, 6);
            Para(Cell(row, 74, bg), r.Description);
            Para(Cell(row, 18, bg), r.Paid ? StressData.PaidMark : "Due", color: r.Paid ? Brand.Accent : Brand.Ink);  // ✓ not in Lato
            Para(Cell(row, 24, bg), Fmt.Money(r.Amount), align: Alignment.Right);
        }
    }

    static void Heading(Section section, string text, double before) =>
        Para(section, text, 15, StressAssets.SerifFamily, before: before, after: 2).KeepWithNextParagraph = true;

    static Table NewTable(BlockContainerBase container, params double[] widthsMm)
    {
        var t = container.Blocks.AddTable();
        t.LayoutType = TableLayoutType.FixedWidth;
        t.PreferredWidth = new TableWidthUnit(TableWidthUnitType.Fixed, Mm(widthsMm.Sum()));
        t.Borders = new TableBorders(None);
        return t;
    }

    static TableCell Cell(TableRow row, double widthMm, string? background = null)
    {
        var c = row.Cells.AddTableCell();
        c.PreferredWidth = new TableWidthUnit(TableWidthUnitType.Fixed, Mm(widthMm));
        c.Padding = background is null ? new Padding(0, 0, 0, 0) : new Padding(Mm(1.5), Mm(1.5), Mm(1.5), Mm(1.5));
        if (background is not null) c.Shading.BackgroundColor = Colour(background);
        return c;
    }

    static TableCellBorders Bottom(string hex, double pt) => new(None, None, None, new Border(Pt(pt), BorderStyle.Single, Colour(hex)));

    static Paragraph Para(BlockContainerBase container, string text, double size = 9, string? family = null,
        string color = Brand.Ink, Alignment align = Alignment.Left, bool bold = false, double before = 0, double after = 0)
    {
        var p = container.Blocks.AddParagraph();
        p.TextAlignment = align;
        p.Spacing.SpacingBefore = Mm(before); p.Spacing.SpacingAfter = Mm(after);
        p.Spacing.LineSpacingType = HeightType.Auto; p.Spacing.LineSpacing = 1.1;
        if (text.Length > 0) Run(p, text, size, family, color, bold);
        return p;
    }

    static void Run(Paragraph p, string text, double size = 9, string? family = null, string color = Brand.Ink, bool bold = false)
    {
        var run = p.Inlines.AddRun(text);
        run.FontFamily = new ThemableFontFamily(new FontFamily(family ?? Brand.FontFamily));
        run.FontSize = Pt(size);
        run.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;
        run.ForegroundColor = Colour(color);
    }

    static void Picture(Paragraph p, byte[] data, string extension, double widthMm, double heightMm)
    {
        var image = p.Inlines.AddImageInline();
        image.Image.ImageSource = new ImageSource(data, extension);
        image.Image.Size = new Size(Mm(widthMm), Mm(heightMm));
    }

    static ThemableColor Colour(string hex) { var (r, g, b) = Brand.Rgb(hex); return new ThemableColor(Color.FromRgb(r, g, b)); }
}

/// <summary>Supplies font bytes by family name for the stress test.</summary>
sealed class StressFontsProvider : FontsProviderBase
{
    public override byte[] GetFontData(FontProperties p) => p.FontFamilyName switch
    {
        StressAssets.SerifFamily => StressAssets.SerifDisplay,
        StressAssets.MonoFamily => StressAssets.Mono,
        StressAssets.ArabicFamily => StressAssets.Arabic,
        StressAssets.FallbackFamily => StressAssets.Fallback,
        _ => p.FontWeight == FontWeights.Bold ? Brand.LatoBold : Brand.LatoRegular,
    };
}
