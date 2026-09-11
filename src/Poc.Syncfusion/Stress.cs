using InvoicePoc;
using InvoicePoc.Stress;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    static float Mm(float mm) => Brand.Mm(mm);
    // Coordinate-based: we own the page geometry. Zero PDF margins, our own content box.
    static readonly float Left = Mm(16), Width = Mm(178), PageH = PdfPageSize.A4.Height, Top = Mm(20), Bottom = Mm(18);

    public static void Generate(string path)
    {
        using var document = new PdfDocument();
        document.PageSettings.Size = PdfPageSize.A4;
        document.PageSettings.Margins.All = 0;
        document.DocumentInformation.Title = StressData.Title;
        var s = new Styles();

        var page = document.Pages.Add();
        var g = page.Graphics;
        var y = Mm(12);                                           // page 1 has no running header

        // Title block: no SVG support in Syncfusion.Pdf, so the PNG logo is used.
        g.DrawImage(s.Logo, Left, y, Mm(18), Mm(18));
        g.DrawString(StressData.Title, s.Serif26, s.Accent, new PointF(Left + Mm(22), y - 1));
        g.DrawString($"Statement date {StressData.StatementDate}", s.Body, s.Muted, new PointF(Left + Mm(22), y + s.Serif26.Height));
        g.DrawString("Account reference", s.Body, s.Muted, new RectangleF(Left, y, Width, s.Body.Height), s.Right);
        g.DrawString(StressData.AccountRef, s.Mono11, s.Ink, new RectangleF(Left, y + s.Body.Height + 1, Width, s.Mono11.Height), s.Right);
        y += Mm(24);

        // Customer: Lato has ŵ/ŷ; Arabic uses right-to-left + complex-script shaping options.
        var cy = y;
        g.DrawString(StressData.CustomerName, s.Bold, s.Ink, new PointF(Left, cy)); cy += s.Line;
        foreach (var line in StressData.BranchAddress) { g.DrawString(line, s.Body, s.Ink, new PointF(Left, cy)); cy += s.Line; }
        g.DrawString(StressData.ArabicTradingName, s.Arabic16, s.Ink, new RectangleF(Left + Width - Mm(80), y - 2, Mm(80), s.Arabic16.Height * 2), s.RightToLeft);
        g.DrawString(StressData.ArabicLabel, s.Body, s.Muted, new RectangleF(Left, y + s.Arabic16.Height + 4, Width, s.Line), s.Right);
        y = cy + Mm(6);

        // Photo scaled to width (aspect ratio by hand) and kept with its caption.
        var photoH = Width * s.Photo.Height / s.Photo.Width;
        g.DrawImage(s.Photo, Left, y, Width, photoH);
        g.DrawString(StressData.PhotoCaption, s.Small, s.Muted, new PointF(Left, y + photoH + Mm(1.5f)));
        y += photoH + Mm(8);

        g.DrawString(StressData.TransactionsHeading, s.Serif15, s.Ink, new PointF(Left, y));
        y += s.Serif15.Height + Mm(2);

        // PdfGrid paginates itself; PaginateBounds reserves room for our header/footer on later pages.
        var grid = Transactions(s);
        var format = new PdfGridLayoutFormat
        {
            Layout = PdfLayoutType.Paginate,
            PaginateBounds = new RectangleF(Left, Top, Width, PageH - Top - Bottom),
        };
        var result = grid.Draw(page, new RectangleF(Left, y, Width, PageH - y - Bottom), format);
        page = result.Page;
        y = result.Bounds.Bottom + Mm(3);
        page.Graphics.DrawString($"Balance due {Fmt.Money(StressData.Balance)}", s.Bold11, s.Ink, new RectangleF(Left, y, Width, s.Bold11.Height), s.Right);
        y += s.Bold11.Height + Mm(8);

        // Two columns: draw into column 1, and PdfTextLayoutResult.Remainder continues in column 2.
        (page, y) = EnsureSpace(document, page, y, Mm(60));
        page.Graphics.DrawString(StressData.TermsHeading, s.Serif15, s.Ink, new PointF(Left, y));
        y += s.Serif15.Height + Mm(2);
        var colW = (Width - Mm(8)) / 2;
        var text = string.Join("\n\n", StressData.Terms);
        var colH = s.Body.MeasureString(text, colW).Height / 2 + s.Line * 2;      // aim for balanced columns
        var element = new PdfTextElement(text, s.Body) { Brush = s.Ink, StringFormat = s.Justify };
        // Draw() returns the base PdfLayoutResult; the overflow text is only on PdfTextLayoutResult.
        // Gotcha: the default layout paginates onto new pages by itself; OnePage stops at the bounds
        // so that Remainder is filled and can continue in the next column.
        var onePage = new PdfLayoutFormat { Layout = PdfLayoutType.OnePage };
        var first = (PdfTextLayoutResult)element.Draw(page, new RectangleF(Left, y, colW, colH), onePage);
        if (!string.IsNullOrEmpty(first.Remainder))
            new PdfTextElement(first.Remainder, s.Body) { Brush = s.Ink, StringFormat = s.Justify }
                .Draw(page, new RectangleF(Left + colW + Mm(8), y, colW, colH), onePage);
        y += colH + Mm(8);

        // Remittance box: keep-together means measuring and starting a new page yourself.
        var boxH = Mm(8) + s.Serif13.Height + StressData.RemittanceFields.Length * (s.Line + Mm(2));
        (page, y) = EnsureSpace(document, page, y, boxH);
        var bg = page.Graphics;
        bg.DrawRectangle(new PdfPen(s.AccentColour, 1), new RectangleF(Left, y, Width, boxH));
        var by = y + Mm(4);
        bg.DrawString(StressData.RemittanceHeading, s.Serif13, s.Ink, new PointF(Left + Mm(4), by)); by += s.Serif13.Height + Mm(2);
        foreach (var (label, value) in StressData.RemittanceFields)
        {
            bg.DrawString(label, s.Body, s.Muted, new PointF(Left + Mm(4), by));
            bg.DrawString(value, s.Mono9, s.Ink, new PointF(Left + Mm(44), by));
            by += s.Line + Mm(2);
        }

        // Header/footer pass: we know the page count, so skip-first and last-page text are just ifs.
        var total = document.Pages.Count;
        for (var i = 0; i < total; i++)
        {
            var pg = document.Pages[i].Graphics;
            if (i > 0)
            {
                pg.DrawImage(s.Logo, Left, Mm(9), Mm(6), Mm(6));
                pg.DrawString(StressData.HeaderText, s.Small, s.Muted, new PointF(Left + Mm(8), Mm(10.5f)));
                pg.DrawLine(s.RulePen, Left, Mm(17), Left + Width, Mm(17));
            }
            var fy = PageH - Mm(12);
            pg.DrawLine(s.RulePen, Left, fy, Left + Width, fy);
            pg.DrawString(i == total - 1 ? StressData.EndText : StressData.ContinuedText, s.Small, s.Muted, new PointF(Left, fy + Mm(2)));
            pg.DrawString(InvoiceText.PageOf(i + 1, total), s.Small, s.Muted, new RectangleF(Left, fy + Mm(2), Width, s.Small.Height), s.Right);
        }

        using var file = File.Create(path);
        document.Save(file);
    }

    static PdfGrid Transactions(Styles s)
    {
        var grid = new PdfGrid { RepeatHeader = true, AllowRowBreakAcrossPages = false };
        grid.Columns.Add(6);
        float[] widths = [Mm(22), Mm(30), Mm(10), Mm(74), Mm(18), Mm(24)];
        for (var i = 0; i < widths.Length; i++) grid.Columns[i].Width = widths[i];
        grid.Columns[5].Format = s.Right;
        grid.Style.Font = s.Body;
        grid.Style.CellPadding = new PdfPaddings(Mm(1.5f), Mm(1.5f), Mm(1.5f), Mm(1.5f));

        grid.Headers.Add(1);
        var header = grid.Headers[0];
        header.Style.Font = s.Bold;
        header.Style.BackgroundBrush = s.Tint;
        for (var i = 0; i < StressData.Columns.Length; i++) { header.Cells[i].Value = StressData.Columns[i]; header.Cells[i].Style.Borders.All = PdfPens.Transparent; }

        var n = 0;
        foreach (var r in StressData.Rows())
        {
            var row = grid.Rows.Add();
            if (n++ % 2 == 1) row.Style.BackgroundBrush = s.Zebra;
            string[] values = [StressData.ShortDate(r.Date), r.Reference, "", r.Description, r.Paid ? StressData.PaidMark : "Due", Fmt.Money(r.Amount)];
            for (var i = 0; i < values.Length; i++) { row.Cells[i].Value = values[i]; row.Cells[i].Style.Borders.All = PdfPens.Transparent; }
            row.Cells[1].Style.Font = s.Mono8;
            // WORKAROUND: no per-glyph fallback and one font per cell, so the whole status cell uses
            // DejaVu Sans (which has both the letters and ✓).
            row.Cells[4].Style.Font = r.Paid ? s.Fallback9 : s.Body;
            row.Cells[4].Style.TextBrush = r.Paid ? s.Accent : s.Ink;
            if (r.Thumbnail is not null)
            {
                row.Cells[2].Style.BackgroundImage = s.Thumb(r.Thumbnail);            // PNG with alpha in a cell
                row.Cells[2].ImagePosition = PdfGridImagePosition.Fit;
                // Gotcha: row.Height is a fixed height (no minimum), so measure the wrapped text ourselves
                // or long descriptions get cut off.
                row.Height = Math.Max(Mm(9), s.Body.MeasureString(r.Description, Mm(74) - Mm(3)).Height + Mm(3.5f));
            }
        }
        return grid;
    }

    static (PdfPage, float) EnsureSpace(PdfDocument document, PdfPage page, float y, float needed) =>
        y + needed <= PageH - Bottom ? (page, y) : (document.Pages.Add(), Top);

    sealed class Styles
    {
        public readonly PdfFont Body = F(Brand.LatoRegular, 9), Bold = F(Brand.LatoBold, 9), Bold11 = F(Brand.LatoBold, 11),
            Small = F(Brand.LatoRegular, 7.5f), Serif26 = F(StressAssets.SerifDisplay, 26), Serif15 = F(StressAssets.SerifDisplay, 15),
            Serif13 = F(StressAssets.SerifDisplay, 13), Mono11 = F(StressAssets.Mono, 11), Mono9 = F(StressAssets.Mono, 9),
            Mono8 = F(StressAssets.Mono, 8),
            // Finding: with Amiri (heavy OpenType rules) only 2 glyphs rendered; DejaVu Sans shapes correctly.
            Arabic16 = F(StressAssets.Fallback, 16), Fallback9 = F(StressAssets.Fallback, 8.5f);
        public readonly PdfColor AccentColour = C(Brand.Accent);
        public readonly PdfBrush Ink = B(Brand.Ink), Muted = B(Brand.Muted), Accent = B(Brand.Accent), Tint = B(Brand.AccentTint), Zebra = B("#F4F8F7");
        public readonly PdfPen RulePen = new(C(Brand.Rule), 0.5f);
        public readonly PdfStringFormat Right = new(PdfTextAlignment.Right), Justify = new(PdfTextAlignment.Justify);
        public readonly PdfStringFormat RightToLeft = new(PdfTextAlignment.Right) { TextDirection = PdfTextDirection.RightToLeft, ComplexScript = true };
        public readonly PdfBitmap Logo = new(new MemoryStream(StressAssets.LogoPng)), Photo = new(new MemoryStream(StressAssets.Photo));
        private readonly Dictionary<string, PdfBitmap> thumbs = new();
        public float Line => Body.Height + 1.5f;
        public PdfBitmap Thumb(string name) => thumbs.TryGetValue(name, out var b) ? b : thumbs[name] = new PdfBitmap(new MemoryStream(StressAssets.Thumbnail(name)));

        static PdfFont F(byte[] data, float size) => new PdfTrueTypeFont(new MemoryStream(data), size);
        static PdfColor C(string hex) { var (r, g, b) = Brand.Rgb(hex); return new PdfColor(r, g, b); }
        static PdfBrush B(string hex) => new PdfSolidBrush(C(hex));
    }
}
