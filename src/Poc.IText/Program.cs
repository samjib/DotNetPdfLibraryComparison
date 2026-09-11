using InvoicePoc;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

// iText Core (layout module): Document / Table / Cell / Paragraph objects with automatic
// pagination. Free under AGPLv3 (your app must then be AGPL too) or via a commercial licence.

if (args.Contains("stress"))
    return await PocRunner.RunAsync("iText Core (stress)", "stress-itext.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);

return await PocRunner.RunAsync("iText Core", "itext.pdf", (invoice, path) => InvoiceDocument.Write(invoice, path));

static class InvoiceDocument
{
    // Parsed font programs are reusable; PdfFont instances are per document.
    static readonly FontProgram RegularProgram = FontProgramFactory.CreateFont(Brand.LatoRegular);
    static readonly FontProgram BoldProgram = FontProgramFactory.CreateFont(Brand.LatoBold);

    static readonly Color Accent = Rgb(Brand.Accent), Tint = Rgb(Brand.AccentTint), Ink = Rgb(Brand.Ink),
        Muted = Rgb(Brand.Muted), RuleColor = Rgb(Brand.Rule), White = Rgb(Brand.OnAccent);

    static float Mm(float mm) => Brand.Mm(mm);

    public static void Write(Invoice inv, string path)
    {
        var pdf = new PdfDocument(new PdfWriter(path));
        pdf.GetDocumentInfo().SetTitle($"Invoice {inv.Number}").SetAuthor(inv.Supplier.Name).SetSubject($"Invoice for {inv.Customer.Name}");

        var regular = PdfFontFactory.CreateFont(RegularProgram, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        var bold = PdfFontFactory.CreateFont(BoldProgram, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        var f = new Fonts(regular, bold);

        // immediateFlush: false keeps pages open so we can stamp "Page X of Y" once the total is known.
        using var doc = new Document(pdf, PageSize.A4, false);
        doc.SetMargins(Mm(Brand.MarginTopMm), Mm(Brand.MarginSideMm), Mm(24), Mm(Brand.MarginSideMm));
        doc.SetFont(regular).SetFontSize(Brand.BodySize).SetFontColor(Ink);

        doc.Add(Header(inv, f));
        doc.Add(Parties(inv, f));
        doc.Add(Lines(inv, f));
        doc.Add(Totals(inv, f));
        doc.Add(Payment(inv, f));

        var total = pdf.GetNumberOfPages();
        for (var i = 1; i <= total; i++)
        {
            var area = new Rectangle(Mm(Brand.MarginSideMm), Mm(6), Mm(178), Mm(14));
            using var canvas = new Canvas(new PdfCanvas(pdf.GetPage(i)), area);
            canvas.SetFont(regular).SetFontSize(Brand.SmallSize).SetFontColor(Muted);
            canvas.Add(Footer(inv, i, total));
        }
    }

    sealed record Fonts(PdfFont Regular, PdfFont Bold);

    static Table Header(Invoice inv, Fonts f)
    {
        var left = Plain().Add(P(Brand.Wordmark).SetFont(f.Bold).SetFontSize(Brand.WordmarkSize).SetFontColor(Accent).SetMarginBottom(Mm(2)));
        foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) left.Add(P(line));
        left.Add(P(InvoiceText.SupplierVatLine(inv)).SetFontColor(Muted));
        left.Add(P(inv.Supplier.Email ?? "").SetFontColor(Muted));

        var meta = new Table(UnitValue.CreatePercentArray([45, 55])).UseAllAvailableWidth();
        foreach (var (label, value) in InvoiceText.Meta(inv))
        {
            meta.AddCell(Plain().Add(P(label).SetFontColor(Muted)));
            meta.AddCell(Plain().Add(P(value).SetTextAlignment(TextAlignment.RIGHT)));
        }
        var right = Plain()
            .Add(P("Invoice").SetFont(f.Bold).SetFontSize(Brand.TitleSize).SetTextAlignment(TextAlignment.RIGHT).SetMarginBottom(Mm(2)))
            .Add(meta);

        return new Table([Mm(98), Mm(80)]).AddCell(left).AddCell(right);
    }

    static Table Parties(Invoice inv, Fonts f)
    {
        var billTo = Plain().Add(P("Bill to").SetFont(f.Bold).SetFontColor(Accent).SetMarginBottom(Mm(1.5f)))
            .Add(P(inv.Customer.Name).SetFont(f.Bold));
        if (inv.Customer.Attention is not null) billTo.Add(P($"For the attention of {inv.Customer.Attention}"));
        foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) billTo.Add(P(line));

        // A Div (not the cell) carries the background so the panel is only as tall as its content.
        var panel = new Div().SetBackgroundColor(Accent).SetFontColor(White).SetTextAlignment(TextAlignment.RIGHT)
            .SetPaddingTop(Mm(4)).SetPaddingBottom(Mm(4)).SetPaddingLeft(Mm(5)).SetPaddingRight(Mm(5))
            .Add(P("Amount due"))
            .Add(P(Fmt.Money(inv.Total)).SetFont(f.Bold).SetFontSize(Brand.AmountDueSize))
            .Add(P($"by {Fmt.Date(inv.DueDate)}"));

        return new Table([Mm(114), Mm(64)]).SetMarginTop(Mm(8)).SetMarginBottom(Mm(7))
            .AddCell(billTo).AddCell(Plain().Add(panel));
    }

    static Table Lines(Invoice inv, Fonts f)
    {
        var table = new Table([Mm(92), Mm(22), Mm(24), Mm(14), Mm(26)]);
        for (var i = 0; i < InvoiceText.ColumnHeaders.Length; i++)
        {
            // Header cells repeat automatically when the table breaks across pages.
            table.AddHeaderCell(LineCell(InvoiceText.ColumnHeaders[i], i).SetFont(f.Bold).SetBackgroundColor(Tint).SetBorder(Border.NO_BORDER));
        }
        foreach (var line in inv.Lines)
        {
            var cells = InvoiceText.Row(line);
            for (var i = 0; i < cells.Length; i++) table.AddCell(LineCell(cells[i], i));
        }
        return table;
    }

    static Cell LineCell(string text, int column) =>
        new Cell().Add(P(text))
            .SetTextAlignment(column == 0 ? TextAlignment.LEFT : TextAlignment.RIGHT)
            .SetBorder(Border.NO_BORDER).SetBorderBottom(new SolidBorder(RuleColor, 0.5f))
            .SetPaddingTop(Mm(1.8f)).SetPaddingBottom(Mm(1.8f)).SetPaddingLeft(Mm(2)).SetPaddingRight(Mm(2))
            .SetKeepTogether(true);

    static Table Totals(Invoice inv, Fonts f)
    {
        var table = new Table([Mm(60), Mm(24)]).SetHorizontalAlignment(HorizontalAlignment.RIGHT)
            .SetMarginTop(Mm(4)).SetKeepTogether(true);

        void Line(string label, string value, bool grand = false)
        {
            foreach (var (text, align) in new[] { (label, TextAlignment.LEFT), (value, TextAlignment.RIGHT) })
            {
                var cell = new Cell().Add(P(text)).SetTextAlignment(align).SetBorder(Border.NO_BORDER)
                    .SetPaddingTop(Mm(1.1f)).SetPaddingBottom(Mm(1.1f)).SetPaddingLeft(Mm(2)).SetPaddingRight(Mm(2));
                if (grand)
                {
                    cell.SetFont(f.Bold).SetFontSize(Brand.GrandTotalSize).SetPaddingTop(Mm(2))
                        .SetBorderTop(new SolidBorder(Ink, 1));
                }
                table.AddCell(cell);
            }
        }

        Line("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal));
        foreach (var band in inv.VatBands) Line(InvoiceText.VatLabel(band), Fmt.Money(band.Vat));
        Line("Total due", Fmt.Money(inv.Total), grand: true);
        return table;
    }

    static Div Payment(Invoice inv, Fonts f)
    {
        var details = new Table([Mm(30), Mm(148)]);
        foreach (var (label, value) in InvoiceText.Payment(inv))
        {
            details.AddCell(Plain().Add(P(label).SetFontColor(Muted)));
            details.AddCell(Plain().Add(P(value)));
        }
        return new Div().SetKeepTogether(true).SetMarginTop(Mm(8))
            .Add(P("How to pay").SetFont(f.Bold).SetFontColor(Accent).SetMarginBottom(Mm(1.5f)))
            .Add(details)
            .Add(P(inv.Notes).SetFontColor(Muted).SetMarginTop(Mm(3)));
    }

    static Table Footer(Invoice inv, int page, int total) =>
        new Table([Mm(148), Mm(30)])
            .AddCell(new Cell().Add(P(InvoiceText.LegalFooter(inv))).SetBorder(Border.NO_BORDER)
                .SetBorderTop(new SolidBorder(RuleColor, 0.5f)).SetPadding(0).SetPaddingTop(Mm(2.5f)))
            .AddCell(new Cell().Add(P(InvoiceText.PageOf(page, total)).SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER)
                .SetBorderTop(new SolidBorder(RuleColor, 0.5f)).SetPadding(0).SetPaddingTop(Mm(2.5f)));

    static Paragraph P(string text) => new Paragraph(text).SetMargin(0).SetMultipliedLeading(1.1f);

    static Cell Plain() => new Cell().SetBorder(Border.NO_BORDER).SetPadding(0);

    static DeviceRgb Rgb(string hex)
    {
        var (r, g, b) = Brand.Rgb(hex);
        return new DeviceRgb(r, g, b);
    }
}
