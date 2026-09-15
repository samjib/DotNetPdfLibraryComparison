using InvoicePoc;
using Aspose.Pdf;
using Aspose.Pdf.Text;

// Aspose.PDF: a document object model (pages, paragraphs, tables) with automatic pagination.
// Without a licence it runs in evaluation mode (watermark and processing limits).

var licencePath = Environment.GetEnvironmentVariable("ASPOSE_LICENSE_PATH");
if (!string.IsNullOrWhiteSpace(licencePath)) new License().SetLicense(licencePath);

if (args.Contains("merge"))
    return await PocRunner.RunAsync("Aspose.PDF (merge)", "merged-aspose.pdf", MergeDocuments.Generate, warmRuns: 2);

if (args.Contains("stress"))
    return await PocRunner.RunAsync("Aspose.PDF (stress)", "stress-aspose.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);

return await PocRunner.RunAsync("Aspose.PDF", "aspose.pdf", (invoice, path) => InvoiceDocument.Write(invoice, path));

static class InvoiceDocument
{
    static readonly Font Regular = OpenFont(Brand.LatoRegular), Bold = OpenFont(Brand.LatoBold);
    static readonly Color Accent = Rgb(Brand.Accent), Tint = Rgb(Brand.AccentTint), Ink = Rgb(Brand.Ink),
        Muted = Rgb(Brand.Muted), RuleColor = Rgb(Brand.Rule), White = Rgb(Brand.OnAccent);

    static double Mm(double mm) => mm * 72 / 25.4;

    public static void Write(Invoice inv, string path)
    {
        using var doc = new Document();
        doc.Info.Title = $"Invoice {inv.Number}";
        doc.Info.Author = inv.Supplier.Name;
        doc.Info.Subject = $"Invoice for {inv.Customer.Name}";

        var page = doc.Pages.Add();
        page.PageInfo.Width = PageSize.A4.Width;
        page.PageInfo.Height = PageSize.A4.Height;
        page.PageInfo.Margin = new MarginInfo(Mm(Brand.MarginSideMm), Mm(24), Mm(Brand.MarginSideMm), Mm(Brand.MarginTopMm));

        // Footer: $p and $P are replaced with the page number and page count.
        // Pages created by overflow inherit the footer.
        page.Footer = new HeaderFooter { Margin = new MarginInfo(Mm(Brand.MarginSideMm), Mm(6), Mm(Brand.MarginSideMm), 0) };
        page.Footer.Paragraphs.Add(Footer(inv));

        page.Paragraphs.Add(Header(inv));
        page.Paragraphs.Add(Parties(inv));
        page.Paragraphs.Add(Lines(inv));
        page.Paragraphs.Add(Totals(inv));
        page.Paragraphs.Add(Payment(inv));

        // Note: Aspose embedded ~265 KB per Lato face here (vs ~20 KB for the others). In evaluation
        // mode doc.FontUtilities.SubsetFonts(...) did not reduce it; worth re-testing with a licence.
        doc.Save(path);
    }

    static Table Header(Invoice inv)
    {
        var table = Grid([98, 80]);
        var row = table.Rows.Add();

        var left = row.Cells.Add();
        left.Paragraphs.Add(Text(Brand.Wordmark, Bold, Brand.WordmarkSize, Accent, bottomMm: 2));
        foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) left.Paragraphs.Add(Text(line));
        left.Paragraphs.Add(Text(InvoiceText.SupplierVatLine(inv), color: Muted));
        left.Paragraphs.Add(Text(inv.Supplier.Email ?? "", color: Muted));

        var right = row.Cells.Add();
        right.Paragraphs.Add(Text("Invoice", Bold, Brand.TitleSize, align: HorizontalAlignment.Right, bottomMm: 2));
        var meta = Grid([36, 44]);
        foreach (var (label, value) in InvoiceText.Meta(inv))
        {
            var r = meta.Rows.Add();
            r.Cells.Add().Paragraphs.Add(Text(label, color: Muted));
            r.Cells.Add().Paragraphs.Add(Text(value, align: HorizontalAlignment.Right));
        }
        right.Paragraphs.Add(meta);
        return table;
    }

    static Table Parties(Invoice inv)
    {
        var table = Grid([114, 64]);
        table.Margin = new MarginInfo(0, Mm(7), 0, Mm(8));
        var row = table.Rows.Add();

        var billTo = row.Cells.Add();
        billTo.Paragraphs.Add(Text("Bill to", Bold, color: Accent, bottomMm: 1.5));
        billTo.Paragraphs.Add(Text(inv.Customer.Name, Bold));
        if (inv.Customer.Attention is not null) billTo.Paragraphs.Add(Text($"For the attention of {inv.Customer.Attention}"));
        foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) billTo.Paragraphs.Add(Text(line));

        // Nested one-cell table carries the background, so the panel only wraps its own content.
        var panel = Grid([64]);
        var cell = panel.Rows.Add().Cells.Add();
        cell.BackgroundColor = Accent;
        cell.Margin = new MarginInfo(Mm(5), Mm(4), Mm(5), Mm(4));
        cell.Paragraphs.Add(Text("Amount due", color: White, align: HorizontalAlignment.Right));
        cell.Paragraphs.Add(Text(Fmt.Money(inv.Total), Bold, Brand.AmountDueSize, White, HorizontalAlignment.Right));
        cell.Paragraphs.Add(Text($"by {Fmt.Date(inv.DueDate)}", color: White, align: HorizontalAlignment.Right));
        row.Cells.Add().Paragraphs.Add(panel);
        return table;
    }

    static Table Lines(Invoice inv)
    {
        var table = new Table
        {
            ColumnWidths = string.Join(' ', new[] { 92, 22, 24, 14, 26 }.Select(w => Mm(w).ToString("0.##"))),
            DefaultCellPadding = new MarginInfo(Mm(2), Mm(1.8), Mm(2), Mm(1.8)),
            DefaultCellBorder = new BorderInfo(BorderSide.Bottom, 0.5f, RuleColor),
            RepeatingRowsCount = 1, // header row repeats on each page
        };

        var header = table.Rows.Add();
        header.BackgroundColor = Tint;
        header.DefaultCellBorder = new BorderInfo(BorderSide.None);
        for (var i = 0; i < InvoiceText.ColumnHeaders.Length; i++)
            header.Cells.Add().Paragraphs.Add(Text(InvoiceText.ColumnHeaders[i], Bold, align: Align(i)));

        foreach (var line in inv.Lines)
        {
            var row = table.Rows.Add();
            row.IsRowBroken = false;
            var cells = InvoiceText.Row(line);
            for (var i = 0; i < cells.Length; i++) row.Cells.Add().Paragraphs.Add(Text(cells[i], align: Align(i)));
        }
        return table;
    }

    static Table Totals(Invoice inv)
    {
        var table = new Table
        {
            ColumnWidths = $"{Mm(60):0.##} {Mm(24):0.##}",
            DefaultCellPadding = new MarginInfo(Mm(2), Mm(1.1), Mm(2), Mm(1.1)),
        };

        void Line(string label, string value, bool grand = false)
        {
            var row = table.Rows.Add();
            var font = grand ? Bold : Regular;
            var size = grand ? Brand.GrandTotalSize : Brand.BodySize;
            row.Cells.Add().Paragraphs.Add(Text(label, font, size));
            row.Cells.Add().Paragraphs.Add(Text(value, font, size, align: HorizontalAlignment.Right));
            if (grand) row.Border = new BorderInfo(BorderSide.Top, 1f, Ink);
        }

        Line("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal));
        foreach (var band in inv.VatBands) Line(InvoiceText.VatLabel(band), Fmt.Money(band.Vat));
        Line("Total due", Fmt.Money(inv.Total), grand: true);

        // Wrap in a two-cell row that may not split: the empty first cell pushes the totals to the
        // right (nested table alignment is ignored), and IsRowBroken = false keeps the block together.
        // (IsBroken = false does NOT mean "keep together" - it truncates the table at the page edge.)
        var outer = Grid([94, 84]);
        outer.Margin = new MarginInfo(0, 0, 0, Mm(4));
        var row = outer.Rows.Add();
        row.IsRowBroken = false;
        row.Cells.Add();
        row.Cells.Add().Paragraphs.Add(table);
        return outer;
    }

    static Table Payment(Invoice inv)
    {
        var details = Grid([30, 148]);
        foreach (var (label, value) in InvoiceText.Payment(inv))
        {
            var r = details.Rows.Add();
            r.Cells.Add().Paragraphs.Add(Text(label, color: Muted));
            r.Cells.Add().Paragraphs.Add(Text(value));
        }
        var notes = Text(inv.Notes, color: Muted);
        notes.Margin = new MarginInfo(0, 0, 0, Mm(3));
        return KeepTogether(topMm: 8, Text("How to pay", Bold, color: Accent, bottomMm: 1.5), details, notes);
    }

    /// <summary>Single row that may not split across pages, so its content stays together.</summary>
    static Table KeepTogether(double topMm, params BaseParagraph[] content)
    {
        var outer = Grid([178]);
        outer.Margin = new MarginInfo(0, 0, 0, Mm(topMm));
        var row = outer.Rows.Add();
        row.IsRowBroken = false;
        var cell = row.Cells.Add();
        foreach (var paragraph in content) cell.Paragraphs.Add(paragraph);
        return outer;
    }

    static Table Footer(Invoice inv)
    {
        var table = Grid([148, 30]);
        var row = table.Rows.Add();
        row.Border = new BorderInfo(BorderSide.Top, 0.5f, RuleColor);
        row.Cells.Add().Paragraphs.Add(Text(InvoiceText.LegalFooter(inv), size: Brand.SmallSize, color: Muted, topMm: 2.5));
        row.Cells.Add().Paragraphs.Add(Text("Page $p of $P", size: Brand.SmallSize, color: Muted, align: HorizontalAlignment.Right, topMm: 2.5));
        return table;
    }

    static Table Grid(double[] widthsMm) => new()
    {
        ColumnWidths = string.Join(' ', widthsMm.Select(w => Mm(w).ToString("0.##"))),
        DefaultCellPadding = new MarginInfo(0, 0, 0, 0),
    };

    static TextFragment Text(string text, Font? font = null, float size = Brand.BodySize, Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left, double bottomMm = 0, double topMm = 0)
    {
        var fragment = new TextFragment(text) { HorizontalAlignment = align, Margin = new MarginInfo(0, Mm(bottomMm), 0, Mm(topMm)) };
        fragment.TextState.Font = font ?? Regular;
        fragment.TextState.FontSize = size;
        fragment.TextState.ForegroundColor = color ?? Ink;
        fragment.TextState.LineSpacing = 1.5f;
        return fragment;
    }

    static HorizontalAlignment Align(int column) => column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

    static Font OpenFont(byte[] data)
    {
        var font = FontRepository.OpenFont(new MemoryStream(data), FontTypes.TTF);
        font.IsEmbedded = true;
        return font;
    }

    static Color Rgb(string hex)
    {
        var (r, g, b) = Brand.Rgb(hex);
        return Color.FromRgb(r / 255.0, g / 255.0, b / 255.0);
    }
}
