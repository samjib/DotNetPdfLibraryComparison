using InvoicePoc;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

// Syncfusion Essential PDF: you draw text and shapes at coordinates, and use PdfGrid for
// tables that paginate. Page templates give repeating footers with page-number fields.

// Free Community Licence key (or paid key) from your Syncfusion account; without one the
// output carries an evaluation notice.
var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
if (!string.IsNullOrWhiteSpace(key)) Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key);

if (args.Contains("stress"))
    return await PocRunner.RunAsync("Syncfusion (stress)", "stress-syncfusion.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);

return await PocRunner.RunAsync("Syncfusion", "syncfusion.pdf", (invoice, path) => InvoiceDocument.Write(invoice, path));

static class InvoiceDocument
{
    static float Mm(float mm) => Brand.Mm(mm);

    public static void Write(Invoice inv, string path)
    {
        using var document = new PdfDocument();
        document.DocumentInformation.Title = $"Invoice {inv.Number}";
        document.DocumentInformation.Author = inv.Supplier.Name;
        document.DocumentInformation.Subject = $"Invoice for {inv.Customer.Name}";
        document.PageSettings.Size = PdfPageSize.A4;
        document.PageSettings.Margins.Top = Mm(Brand.MarginTopMm);
        document.PageSettings.Margins.Left = Mm(Brand.MarginSideMm);
        document.PageSettings.Margins.Right = Mm(Brand.MarginSideMm);
        document.PageSettings.Margins.Bottom = Mm(6);

        var s = new Styles();
        var width = document.PageSettings.Size.Width - 2 * Mm(Brand.MarginSideMm);
        document.Template.Bottom = Footer(inv, s, width); // must be set before pages are added

        var page = document.Pages.Add();
        var g = page.Graphics;
        var y = Header(g, inv, s, width);
        y = Parties(g, inv, s, width, y + Mm(8)) + Mm(7);

        // PdfGrid paginates itself and returns where it finished.
        var result = Lines(inv, s).Draw(page, new PointF(0, y), new PdfGridLayoutFormat { Layout = PdfLayoutType.Paginate });
        page = result.Page;
        y = result.Bounds.Bottom + Mm(4);

        (page, y) = EnsureSpace(document, page, y, TotalsHeight(inv, s));
        y = Totals(page.Graphics, inv, s, width, y) + Mm(8);

        (page, y) = EnsureSpace(document, page, y, PaymentHeight(inv, s));
        Payment(page.Graphics, inv, s, y);

        using var file = File.Create(path);
        document.Save(file);
    }

    static float Header(PdfGraphics g, Invoice inv, Styles s, float width)
    {
        g.DrawString(Brand.Wordmark, s.Wordmark, s.AccentBrush, PointF.Empty);
        var y = s.Wordmark.Height + Mm(2);
        foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) y = Line(g, line, s.Body, s.InkBrush, 0, y, s);
        y = Line(g, InvoiceText.SupplierVatLine(inv), s.Body, s.MutedBrush, 0, y, s);
        y = Line(g, inv.Supplier.Email ?? "", s.Body, s.MutedBrush, 0, y, s);

        var x = width - Mm(80);
        g.DrawString("Invoice", s.Title, s.InkBrush, new RectangleF(x, 0, Mm(80), s.Title.Height), s.Right);
        var ry = s.Title.Height + Mm(2);
        foreach (var (label, value) in InvoiceText.Meta(inv))
        {
            g.DrawString(label, s.Body, s.MutedBrush, new PointF(x, ry));
            g.DrawString(value, s.Body, s.InkBrush, new RectangleF(x, ry, Mm(80), s.LineHeight), s.Right);
            ry += s.LineHeight;
        }
        return Math.Max(y, ry);
    }

    static float Parties(PdfGraphics g, Invoice inv, Styles s, float width, float top)
    {
        var y = Line(g, "Bill to", s.Bold, s.AccentBrush, 0, top, s) + Mm(1.5f);
        y = Line(g, inv.Customer.Name, s.Bold, s.InkBrush, 0, y, s);
        if (inv.Customer.Attention is not null) y = Line(g, $"For the attention of {inv.Customer.Attention}", s.Body, s.InkBrush, 0, y, s);
        foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) y = Line(g, line, s.Body, s.InkBrush, 0, y, s);

        // Amount-due panel: a filled rectangle with right-aligned text inside.
        float panelWidth = Mm(64), x = width - panelWidth, inner = x + Mm(5), innerWidth = panelWidth - Mm(10);
        var height = Mm(8) + 2 * s.LineHeight + s.AmountDue.Height;
        g.DrawRectangle(s.AccentBrush, new RectangleF(x, top, panelWidth, height));
        var py = top + Mm(4);
        g.DrawString("Amount due", s.Body, s.WhiteBrush, new RectangleF(inner, py, innerWidth, s.LineHeight), s.Right);
        py += s.LineHeight;
        g.DrawString(Fmt.Money(inv.Total), s.AmountDue, s.WhiteBrush, new RectangleF(inner, py, innerWidth, s.AmountDue.Height), s.Right);
        py += s.AmountDue.Height;
        g.DrawString($"by {Fmt.Date(inv.DueDate)}", s.Body, s.WhiteBrush, new RectangleF(inner, py, innerWidth, s.LineHeight), s.Right);
        return Math.Max(y, top + height);
    }

    static PdfGrid Lines(Invoice inv, Styles s)
    {
        var grid = new PdfGrid { RepeatHeader = true }; // header row repeats on each page
        grid.Columns.Add(5);
        float[] widths = [Mm(92), Mm(22), Mm(24), Mm(14), Mm(26)];
        for (var i = 0; i < widths.Length; i++)
        {
            grid.Columns[i].Width = widths[i];
            grid.Columns[i].Format = i == 0 ? s.Left : s.Right;
        }
        grid.Style.Font = s.Body;
        grid.Style.TextBrush = s.InkBrush;
        grid.Style.CellPadding = new PdfPaddings(Mm(2), Mm(2), Mm(1.8f), Mm(1.8f));
        grid.AllowRowBreakAcrossPages = false;

        grid.Headers.Add(1);
        var header = grid.Headers[0];
        header.Style.Font = s.Bold;
        header.Style.BackgroundBrush = s.TintBrush;
        for (var i = 0; i < InvoiceText.ColumnHeaders.Length; i++)
        {
            header.Cells[i].Value = InvoiceText.ColumnHeaders[i];
            header.Cells[i].Style.Borders.All = PdfPens.Transparent;
        }

        foreach (var line in inv.Lines)
        {
            var row = grid.Rows.Add();
            var cells = InvoiceText.Row(line);
            for (var i = 0; i < cells.Length; i++)
            {
                row.Cells[i].Value = cells[i];
                row.Cells[i].Style.Borders.All = PdfPens.Transparent;
                row.Cells[i].Style.Borders.Bottom = s.RulePen;
            }
        }
        return grid;
    }

    static float TotalsHeight(Invoice inv, Styles s) => (inv.VatBands.Count + 1) * (s.LineHeight + Mm(2.2f)) + s.GrandTotal.Height + Mm(4);

    static float Totals(PdfGraphics g, Invoice inv, Styles s, float width, float y)
    {
        float boxWidth = Mm(84), x = width - boxWidth, textX = x + Mm(2), textWidth = boxWidth - Mm(4);
        void Row(string label, string value, PdfFont font, bool grand = false)
        {
            if (grand)
            {
                y += Mm(1);
                g.DrawLine(s.InkPen, x, y, width, y);
                y += Mm(1);
            }
            y += Mm(1.1f);
            g.DrawString(label, font, s.InkBrush, new PointF(textX, y));
            g.DrawString(value, font, s.InkBrush, new RectangleF(textX, y, textWidth, font.Height), s.Right);
            y += font.Height + Mm(1.1f);
        }

        Row("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal), s.Body);
        foreach (var band in inv.VatBands) Row(InvoiceText.VatLabel(band), Fmt.Money(band.Vat), s.Body);
        Row("Total due", Fmt.Money(inv.Total), s.GrandTotal, grand: true);
        return y;
    }

    static float PaymentHeight(Invoice inv, Styles s) => (InvoiceText.Payment(inv).Count + 3) * s.LineHeight + Mm(6);

    static void Payment(PdfGraphics g, Invoice inv, Styles s, float y)
    {
        y = Line(g, "How to pay", s.Bold, s.AccentBrush, 0, y, s) + Mm(1.5f);
        foreach (var (label, value) in InvoiceText.Payment(inv))
        {
            g.DrawString(label, s.Body, s.MutedBrush, new PointF(0, y));
            g.DrawString(value, s.Body, s.InkBrush, new PointF(Mm(30), y));
            y += s.LineHeight;
        }
        Line(g, inv.Notes, s.Body, s.MutedBrush, 0, y + Mm(3), s);
    }

    static PdfPageTemplateElement Footer(Invoice inv, Styles s, float width)
    {
        var footer = new PdfPageTemplateElement(new RectangleF(0, 0, width, Mm(14)));
        footer.Graphics.DrawLine(s.RulePen, 0, 0, width, 0);
        footer.Graphics.DrawString(InvoiceText.LegalFooter(inv), s.Small, s.MutedBrush, new RectangleF(0, Mm(2.5f), Mm(145), Mm(10)));

        // Automatic fields are evaluated per page when the document is saved.
        var page = new PdfPageNumberField(s.Small, s.MutedBrush);
        var count = new PdfPageCountField(s.Small, s.MutedBrush);
        var pageOf = new PdfCompositeField(s.Small, s.MutedBrush, "Page {0} of {1}", page, count)
        {
            Bounds = new RectangleF(width - Mm(30), Mm(2.5f), Mm(30), Mm(5)),
            StringFormat = s.Right,
        };
        pageOf.Draw(footer.Graphics);
        return footer;
    }

    static (PdfPage Page, float Y) EnsureSpace(PdfDocument document, PdfPage page, float y, float needed) =>
        y + needed <= page.GetClientSize().Height ? (page, y) : (document.Pages.Add(), 0f);

    static float Line(PdfGraphics g, string text, PdfFont font, PdfBrush brush, float x, float y, Styles s)
    {
        // Measure first so long text wraps inside the available width.
        var width = Mm(178) - x;
        var size = font.MeasureString(text, width);
        g.DrawString(text, font, brush, new RectangleF(x, y, width, size.Height));
        return y + Math.Max(s.LineHeight, size.Height + 1.5f);
    }

    /// <summary>Fonts and brushes. Syncfusion fonts are created per document from the embedded TTFs.</summary>
    sealed class Styles
    {
        public readonly PdfFont Body = Font(Brand.LatoRegular, Brand.BodySize);
        public readonly PdfFont Bold = Font(Brand.LatoBold, Brand.BodySize);
        public readonly PdfFont Small = Font(Brand.LatoRegular, Brand.SmallSize);
        public readonly PdfFont Wordmark = Font(Brand.LatoBold, Brand.WordmarkSize);
        public readonly PdfFont Title = Font(Brand.LatoBold, Brand.TitleSize);
        public readonly PdfFont AmountDue = Font(Brand.LatoBold, Brand.AmountDueSize);
        public readonly PdfFont GrandTotal = Font(Brand.LatoBold, Brand.GrandTotalSize);

        public readonly PdfBrush InkBrush = Brush(Brand.Ink), MutedBrush = Brush(Brand.Muted), AccentBrush = Brush(Brand.Accent),
            TintBrush = Brush(Brand.AccentTint), WhiteBrush = Brush(Brand.OnAccent);
        public readonly PdfPen RulePen = new(Color(Brand.Rule), 0.5f), InkPen = new(Color(Brand.Ink), 1f);
        public readonly PdfStringFormat Left = new(PdfTextAlignment.Left), Right = new(PdfTextAlignment.Right);
        public float LineHeight => Body.Height + 1.5f;

        static PdfFont Font(byte[] data, float size) => new PdfTrueTypeFont(new MemoryStream(data), size);
        static PdfBrush Brush(string hex) => new PdfSolidBrush(Color(hex));
        static PdfColor Color(string hex)
        {
            var (r, g, b) = Brand.Rgb(hex);
            return new PdfColor(r, g, b);
        }
    }
}
