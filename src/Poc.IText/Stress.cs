using InvoicePoc;
using InvoicePoc.Stress;
using iText.IO.Font;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Font;
using iText.Layout.Properties;
using iText.Svg.Converter;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    static float Mm(float mm) => Brand.Mm(mm);
    static readonly Color Accent = Rgb(Brand.Accent), Tint = Rgb(Brand.AccentTint), Ink = Rgb(Brand.Ink),
        Muted = Rgb(Brand.Muted), RuleColour = Rgb(Brand.Rule), Zebra = Rgb("#F4F8F7");
    static readonly ImageData Photo = ImageDataFactory.Create(StressAssets.Photo);

    public static void Generate(string path)
    {
        var pdf = new PdfDocument(new PdfWriter(path));
        pdf.GetDocumentInfo().SetTitle(StressData.Title);
        using var doc = new Document(pdf, PageSize.A4, false);
        doc.SetMargins(Mm(20), Mm(Brand.MarginSideMm), Mm(18), Mm(Brand.MarginSideMm));

        // A FontProvider plus a family list gives per-glyph fallback (✓ comes from DejaVu Sans).
        var fonts = new FontProvider();
        foreach (var data in new[] { Brand.LatoRegular, Brand.LatoBold, StressAssets.SerifDisplay, StressAssets.Mono, StressAssets.Arabic, StressAssets.Fallback })
            fonts.AddFont(FontProgramFactory.CreateFont(data), PdfEncodings.IDENTITY_H);
        doc.SetFontProvider(fonts);
        doc.SetFontFamily(Brand.FontFamily, StressAssets.FallbackFamily).SetFontSize(9).SetFontColor(Ink);

        var logo = SvgConverter.ConvertToXObject(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(StressAssets.LogoSvg)), pdf); // SVG incl. gradient
        var thumbs = new Dictionary<string, PdfImageXObject>();                  // embed each thumbnail once

        doc.Add(TitleBlock(logo));
        doc.Add(Customer());
        doc.Add(new Div().SetKeepTogether(true).SetMarginTop(Mm(6))              // photo + caption never split
            .Add(new Image(Photo).SetAutoScaleWidth(true))
            .Add(P(StressData.PhotoCaption).SetFontSize(8).SetFontColor(Muted).SetMarginTop(Mm(1.5f))));
        doc.Add(Heading(StressData.TransactionsHeading));
        doc.Add(Transactions(pdf, thumbs));
        doc.Add(P($"Balance due {Fmt.Money(StressData.Balance)}").Bold().SetFontSize(11).SetTextAlignment(TextAlignment.RIGHT).SetMarginTop(Mm(3)));
        doc.Add(Heading(StressData.TermsHeading).SetMarginTop(Mm(8)));
        doc.Add(TwoColumnTerms());
        doc.Add(Remittance());

        // Header/footer pass: the page count is known now, so "skip page 1" and "different last page" are simple.
        var total = pdf.GetNumberOfPages();
        for (var i = 1; i <= total; i++)
        {
            var page = pdf.GetPage(i);
            if (i > 1)
            {
                using var header = new Canvas(new PdfCanvas(page), new Rectangle(Mm(16), Mm(297 - 18), Mm(178), Mm(10)));
                header.SetFontProvider(fonts);
                header.SetFontFamily(Brand.FontFamily);
                header.Add(new Table([Mm(8), Mm(170)])
                    .AddCell(Plain().Add(new Image(logo).ScaleToFit(Mm(6), Mm(6))))
                    .AddCell(Plain().SetVerticalAlignment(VerticalAlignment.MIDDLE).Add(P(StressData.HeaderText).SetFontSize(8).SetFontColor(Muted)))
                    .SetBorderBottom(new SolidBorder(RuleColour, 0.5f)));
            }
            using var footer = new Canvas(new PdfCanvas(page), new Rectangle(Mm(16), Mm(4), Mm(178), Mm(8)));
            footer.SetFontProvider(fonts);                                          // SetFontProvider returns void
            footer.SetFontFamily(Brand.FontFamily).SetFontSize(7.5f).SetFontColor(Muted);
            footer.Add(new Table([Mm(148), Mm(30)])
                .AddCell(Plain().SetBorderTop(new SolidBorder(RuleColour, 0.5f)).SetPaddingTop(Mm(2))
                    .Add(P(i == total ? StressData.EndText : StressData.ContinuedText)))
                .AddCell(Plain().SetBorderTop(new SolidBorder(RuleColour, 0.5f)).SetPaddingTop(Mm(2))
                    .Add(P(InvoiceText.PageOf(i, total)).SetTextAlignment(TextAlignment.RIGHT))));
        }
    }

    static Table TitleBlock(PdfFormXObject logo) =>
        new Table([Mm(18), Mm(105), Mm(55)])
            .AddCell(Plain().Add(new Image(logo).ScaleToFit(Mm(18), Mm(18))))
            .AddCell(Plain().SetPaddingLeft(Mm(3))
                .Add(P(StressData.Title).SetFontFamily(StressAssets.SerifFamily).SetFontSize(26).SetFontColor(Accent))
                .Add(P($"Statement date {StressData.StatementDate}").SetFontColor(Muted)))
            .AddCell(Plain().SetTextAlignment(TextAlignment.RIGHT)
                .Add(P("Account reference").SetFontColor(Muted))
                .Add(P(StressData.AccountRef).SetFontFamily(StressAssets.MonoFamily).SetFontSize(11)));

    static Table Customer()
    {
        var left = Plain().Add(P(StressData.CustomerName).Bold());
        foreach (var line in StressData.BranchAddress) left.Add(P(line));
        // Arabic shaping/bidi needs the commercial pdfCalligraph add-on; without it expect broken letterforms.
        var right = Plain().SetTextAlignment(TextAlignment.RIGHT)
            .Add(P(StressData.ArabicTradingName).SetFontFamily(StressAssets.ArabicFamily).SetFontSize(16).SetBaseDirection(BaseDirection.RIGHT_TO_LEFT))
            .Add(P(StressData.ArabicLabel).SetFontColor(Muted));
        return new Table([Mm(98), Mm(80)]).SetMarginTop(Mm(6)).AddCell(left).AddCell(right);
    }

    static Table Transactions(PdfDocument pdf, Dictionary<string, PdfImageXObject> thumbs)
    {
        var table = new Table([Mm(22), Mm(30), Mm(10), Mm(74), Mm(18), Mm(24)]);
        for (var i = 0; i < StressData.Columns.Length; i++)                     // header cells repeat on every page
            table.AddHeaderCell(Cell(StressData.Columns[i], i == 5).Bold().SetBackgroundColor(Tint));

        var n = 0;
        foreach (var row in StressData.Rows())
        {
            var bg = n++ % 2 == 1 ? Zebra : ColorConstants.WHITE;
            table.AddCell(Cell(StressData.ShortDate(row.Date)).SetBackgroundColor(bg));
            table.AddCell(Cell(row.Reference).SetFontFamily(StressAssets.MonoFamily).SetFontSize(8).SetBackgroundColor(bg));
            var thumbCell = Cell("").SetBackgroundColor(bg);
            if (row.Thumbnail is not null)
            {
                if (!thumbs.TryGetValue(row.Thumbnail, out var x))
                    thumbs[row.Thumbnail] = x = new PdfImageXObject(ImageDataFactory.Create(StressAssets.Thumbnail(row.Thumbnail)));
                thumbCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(Mm(1)).SetKeepTogether(true).SetBackgroundColor(bg)
                    .Add(new Image(x).ScaleToFit(Mm(6), Mm(6)));                   // PNG with alpha in a cell
            }
            table.AddCell(thumbCell);
            table.AddCell(Cell(row.Description).SetBackgroundColor(bg));
            table.AddCell(Cell(row.Paid ? StressData.PaidMark : "Due").SetFontColor(row.Paid ? Accent : Ink).SetBackgroundColor(bg));
            table.AddCell(Cell(Fmt.Money(row.Amount), right: true).SetBackgroundColor(bg));
        }
        return table;
    }

    static IBlockElement TwoColumnTerms()
    {
        // MulticolContainer (iText 8+) flows content across columns, like CSS columns.
        var columns = new MulticolContainer();
        columns.SetProperty(Property.COLUMN_COUNT, 2);
        columns.SetProperty(Property.COLUMN_GAP, Mm(8));
        // Gotcha: it must have exactly one block child, otherwise "Invalid child renderers" is thrown.
        var body = new Div();
        foreach (var para in StressData.Terms) body.Add(P(para).SetTextAlignment(TextAlignment.JUSTIFIED).SetMarginBottom(Mm(2)));
        columns.Add(body);
        return columns;
    }

    static Div Remittance()
    {
        var box = new Div().SetKeepTogether(true).SetMarginTop(Mm(8)).SetBorder(new SolidBorder(Accent, 1)).SetPadding(Mm(4))
            .Add(P(StressData.RemittanceHeading).SetFontFamily(StressAssets.SerifFamily).SetFontSize(13).SetMarginBottom(Mm(2)));
        var fields = new Table([Mm(40), Mm(128)]);
        foreach (var (label, value) in StressData.RemittanceFields)
        {
            fields.AddCell(Plain().SetPaddingBottom(Mm(2)).Add(P(label).SetFontColor(Muted)));
            fields.AddCell(Plain().SetPaddingBottom(Mm(2)).Add(P(value).SetFontFamily(StressAssets.MonoFamily)));
        }
        return box.Add(fields);
    }

    static Paragraph Heading(string text) =>
        P(text).SetFontFamily(StressAssets.SerifFamily).SetFontSize(15).SetMarginTop(Mm(6)).SetMarginBottom(Mm(2)).SetKeepWithNext(true);

    static Cell Cell(string text, bool right = false) =>
        new Cell().Add(P(text)).SetTextAlignment(right ? TextAlignment.RIGHT : TextAlignment.LEFT)
            .SetBorder(Border.NO_BORDER).SetPadding(Mm(1.5f)).SetKeepTogether(true);

    // iText 9 removed SetBold(). With a FontProvider, asking for weight 700 selects the real Lato Bold
    // face; SimulateBold() would instead fake it by thickening the regular glyphs.
    static T Bold<T>(this T element) where T : IPropertyContainer { element.SetProperty(Property.FONT_WEIGHT, "bold"); return element; }

    static Cell Plain() => new Cell().SetBorder(Border.NO_BORDER).SetPadding(0);
    static Paragraph P(string text) => new Paragraph(text).SetMargin(0).SetMultipliedLeading(1.1f);
    static DeviceRgb Rgb(string hex) { var (r, g, b) = Brand.Rgb(hex); return new DeviceRgb(r, g, b); }
}
