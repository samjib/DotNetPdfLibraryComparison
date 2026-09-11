using InvoicePoc;
using InvoicePoc.Stress;
using Aspose.Pdf;
using Aspose.Pdf.Text;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    static double Mm(double mm) => mm * 72 / 25.4;
    static readonly Font Lato = Open(Brand.LatoRegular), LatoBold = Open(Brand.LatoBold), Serif = Open(StressAssets.SerifDisplay),
        Mono = Open(StressAssets.Mono), Arabic = Open(StressAssets.Arabic), Fallback = Open(StressAssets.Fallback);
    static readonly Color Accent = Rgb(Brand.Accent), Tint = Rgb(Brand.AccentTint), Ink = Rgb(Brand.Ink), Muted = Rgb(Brand.Muted),
        RuleColour = Rgb(Brand.Rule), Zebra = Rgb("#F4F8F7");

    public static void Generate(string path)
    {
        using var doc = new Document();
        doc.Info.Title = StressData.Title;
        var page = doc.Pages.Add();
        page.PageInfo.Width = PageSize.A4.Width; page.PageInfo.Height = PageSize.A4.Height;
        page.PageInfo.Margin = new MarginInfo(Mm(16), Mm(18), Mm(16), Mm(20));

        page.Paragraphs.Add(TitleBlock());
        page.Paragraphs.Add(Customer());
        // Gotcha: FixWidth alone does not keep the aspect ratio (the photo came out stretched), so the
        // height is calculated from the pixel size.
        var photo = new Image { ImageStream = new MemoryStream(StressAssets.Photo), FixWidth = Mm(178), FixHeight = Mm(178 * 900 / 1600.0) };
        page.Paragraphs.Add(KeepTogether(6, photo, T(StressData.PhotoCaption, size: 8, color: Muted, topMm: 1.5)));
        page.Paragraphs.Add(Heading(StressData.TransactionsHeading, 6));
        page.Paragraphs.Add(Transactions());
        page.Paragraphs.Add(T($"Balance due {Fmt.Money(StressData.Balance)}", LatoBold, 11, align: HorizontalAlignment.Right, topMm: 3));
        page.Paragraphs.Add(Heading(StressData.TermsHeading, 8));
        page.Paragraphs.Add(TwoColumnTerms());
        page.Paragraphs.Add(Remittance());

        // Lay out first, then decorate each page: skip-first header and last-page footer are simple loops.
        doc.ProcessParagraphs();
        var total = doc.Pages.Count;
        for (var i = 1; i <= total; i++)
        {
            var p = doc.Pages[i];
            if (i > 1)
            {
                var header = Grid([8, 170]);
                var hr = header.Rows.Add();
                hr.Border = new BorderInfo(BorderSide.Bottom, 0.5f, RuleColour);
                hr.Cells.Add().Paragraphs.Add(new Image { ImageStream = new MemoryStream(StressAssets.LogoPng), FixHeight = Mm(6), FixWidth = Mm(6) });
                hr.Cells.Add().Paragraphs.Add(T(StressData.HeaderText, size: 8, color: Muted, topMm: 1.5));
                p.Header = new HeaderFooter { Margin = new MarginInfo(Mm(16), 0, Mm(16), Mm(9)) };
                p.Header.Paragraphs.Add(header);
            }
            var footer = Grid([148, 30]);
            var fr = footer.Rows.Add();
            fr.Border = new BorderInfo(BorderSide.Top, 0.5f, RuleColour);
            fr.Cells.Add().Paragraphs.Add(T(i == total ? StressData.EndText : StressData.ContinuedText, size: 7.5f, color: Muted, topMm: 2));
            fr.Cells.Add().Paragraphs.Add(T(InvoiceText.PageOf(i, total), size: 7.5f, color: Muted, align: HorizontalAlignment.Right, topMm: 2));
            p.Footer = new HeaderFooter { Margin = new MarginInfo(Mm(16), Mm(6), Mm(16), 0) };
            p.Footer.Paragraphs.Add(footer);
        }
        doc.Save(path);
    }

    static Table TitleBlock()
    {
        var t = Grid([18, 105, 55]);
        var r = t.Rows.Add();
        // SVG is a native image type in Aspose.PDF.
        r.Cells.Add().Paragraphs.Add(new Image { ImageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(StressAssets.LogoSvg)),
            FileType = ImageFileType.Svg, FixWidth = Mm(18), FixHeight = Mm(18) });
        var mid = r.Cells.Add();
        mid.Paragraphs.Add(T(StressData.Title, Serif, 26, Accent, leftMm: 3));
        mid.Paragraphs.Add(T($"Statement date {StressData.StatementDate}", color: Muted, leftMm: 3));
        var right = r.Cells.Add();
        right.Paragraphs.Add(T("Account reference", color: Muted, align: HorizontalAlignment.Right));
        right.Paragraphs.Add(T(StressData.AccountRef, Mono, 11, align: HorizontalAlignment.Right));
        return t;
    }

    static Table Customer()
    {
        var t = Grid([98, 80]);
        t.Margin = new MarginInfo(0, 0, 0, Mm(6));
        var r = t.Rows.Add();
        var left = r.Cells.Add();
        left.Paragraphs.Add(T(StressData.CustomerName, LatoBold));
        foreach (var line in StressData.BranchAddress) left.Paragraphs.Add(T(line));
        var right = r.Cells.Add();
        right.Paragraphs.Add(T(StressData.ArabicTradingName, Arabic, 16, align: HorizontalAlignment.Right));
        right.Paragraphs.Add(T(StressData.ArabicLabel, color: Muted, align: HorizontalAlignment.Right));
        return t;
    }

    static Table Transactions()
    {
        double[] widths = [22, 30, 10, 74, 18, 24];
        var table = new Table
        {
            ColumnWidths = string.Join(' ', widths.Select(w => Mm(w).ToString("0.##"))),
            DefaultCellPadding = new MarginInfo(Mm(1.5), Mm(1.5), Mm(1.5), Mm(1.5)),
            RepeatingRowsCount = 1,                                               // header repeats on every page
        };
        var head = table.Rows.Add();
        head.BackgroundColor = Tint;
        for (var i = 0; i < StressData.Columns.Length; i++)
            head.Cells.Add().Paragraphs.Add(T(StressData.Columns[i], LatoBold, align: i == 5 ? HorizontalAlignment.Right : HorizontalAlignment.Left));

        var n = 0;
        foreach (var r in StressData.Rows())
        {
            var row = table.Rows.Add();
            row.IsRowBroken = false;
            if (n++ % 2 == 1) row.BackgroundColor = Zebra;
            row.Cells.Add().Paragraphs.Add(T(StressData.ShortDate(r.Date)));
            row.Cells.Add().Paragraphs.Add(T(r.Reference, Mono, 8));
            var thumb = row.Cells.Add();
            if (r.Thumbnail is not null)
                thumb.Paragraphs.Add(new Image { ImageStream = new MemoryStream(StressAssets.Thumbnail(r.Thumbnail)), FixHeight = Mm(6), FixWidth = Mm(6) });
            row.Cells.Add().Paragraphs.Add(T(r.Description));
            // FINDING: a glyph missing from the font (✓ isn't in Lato) crashed ProcessParagraphs with a
            // NullReferenceException instead of falling back. Workaround: set a font that has the glyph.
            row.Cells.Add().Paragraphs.Add(T(r.Paid ? StressData.PaidMark : "Due", r.Paid ? Fallback : Lato, 8.5f, color: r.Paid ? Accent : Ink));
            row.Cells.Add().Paragraphs.Add(T(Fmt.Money(r.Amount), align: HorizontalAlignment.Right));
        }
        return table;
    }

    static FloatingBox TwoColumnTerms()
    {
        // FloatingBox.ColumnInfo flows paragraphs across columns.
        var box = new FloatingBox((float)Mm(178), (float)Mm(70));
        box.ColumnInfo.ColumnCount = 2;
        box.ColumnInfo.ColumnSpacing = Mm(8).ToString("0.##");
        box.ColumnInfo.ColumnWidths = $"{Mm(85):0.##} {Mm(85):0.##}";
        foreach (var para in StressData.Terms)
        {
            var t = T(para, bottomMm: 2);
            t.TextState.HorizontalAlignment = HorizontalAlignment.Justify;
            box.Paragraphs.Add(t);
        }
        return box;
    }

    static Table Remittance()
    {
        var fields = Grid([40, 130]);
        foreach (var (label, value) in StressData.RemittanceFields)
        {
            var r = fields.Rows.Add();
            r.Cells.Add().Paragraphs.Add(T(label, color: Muted, bottomMm: 2));
            r.Cells.Add().Paragraphs.Add(T(value, Mono, bottomMm: 2));
        }
        var outer = KeepTogether(8, T(StressData.RemittanceHeading, Serif, 13, bottomMm: 2), fields);
        var cell = outer.Rows[0].Cells[0];
        cell.Border = new BorderInfo(BorderSide.All, 1f, Accent);
        cell.Margin = new MarginInfo(Mm(4), Mm(4), Mm(4), Mm(4));
        return outer;
    }

    // Single unsplittable row = keep-together (IsBroken = false would truncate instead).
    static Table KeepTogether(double topMm, params BaseParagraph[] content)
    {
        var outer = Grid([178]);
        outer.Margin = new MarginInfo(0, 0, 0, Mm(topMm));
        var row = outer.Rows.Add();
        row.IsRowBroken = false;
        var cell = row.Cells.Add();
        foreach (var p in content) cell.Paragraphs.Add(p);
        return outer;
    }

    static TextFragment Heading(string text, double topMm)
    {
        var h = T(text, Serif, 15, topMm: topMm, bottomMm: 2);
        h.IsKeptWithNext = true;
        return h;
    }

    static Table Grid(double[] widthsMm) => new()
    {
        ColumnWidths = string.Join(' ', widthsMm.Select(w => Mm(w).ToString("0.##"))),
        DefaultCellPadding = new MarginInfo(0, 0, 0, 0),
    };

    static TextFragment T(string text, Font? font = null, float size = 9, Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left, double topMm = 0, double bottomMm = 0, double leftMm = 0)
    {
        var f = new TextFragment(text) { HorizontalAlignment = align, Margin = new MarginInfo(Mm(leftMm), Mm(bottomMm), 0, Mm(topMm)) };
        f.TextState.Font = font ?? Lato; f.TextState.FontSize = size; f.TextState.ForegroundColor = color ?? Ink;
        return f;
    }

    static Font Open(byte[] data) { var f = FontRepository.OpenFont(new MemoryStream(data), FontTypes.TTF); f.IsEmbedded = true; return f; }
    static Color Rgb(string hex) { var (r, g, b) = Brand.Rgb(hex); return Color.FromRgb(r / 255.0, g / 255.0, b / 255.0); }
}
