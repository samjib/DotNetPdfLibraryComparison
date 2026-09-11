using InvoicePoc;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

// PDFsharp + MigraDoc: you build a document object model (sections, paragraphs, tables)
// and MigraDoc lays it out. MIT-licensed, no revenue thresholds.

// The cross-platform (Core) build of PDFsharp needs a font resolver; on Windows you could
// alternatively set GlobalFontSettings.UseWindowsFontsUnderWindows = true.
if (args.Contains("stress"))
{
    GlobalFontSettings.FontResolver = new StressFontResolver();
    return await PocRunner.RunAsync("PDFsharp + MigraDoc (stress)", "stress-pdfsharp-migradoc.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);
}

GlobalFontSettings.FontResolver = new LatoFontResolver();

return await PocRunner.RunAsync("PDFsharp + MigraDoc", "pdfsharp-migradoc.pdf", (invoice, path) =>
{
    var renderer = new PdfDocumentRenderer { Document = InvoiceDocument.Build(invoice) };
    renderer.RenderDocument();
    renderer.PdfDocument.Save(path);
});

static class InvoiceDocument
{
    static readonly Color Accent = Hex(Brand.Accent), Tint = Hex(Brand.AccentTint), Ink = Hex(Brand.Ink),
        Muted = Hex(Brand.Muted), RuleColor = Hex(Brand.Rule), White = Hex(Brand.OnAccent);

    static Unit Mm(double mm) => Unit.FromMillimeter(mm);

    public static Document Build(Invoice inv)
    {
        var doc = new Document();
        doc.Info.Title = $"Invoice {inv.Number}";
        doc.Info.Author = inv.Supplier.Name;
        doc.Info.Subject = $"Invoice for {inv.Customer.Name}";

        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Brand.FontFamily;
        normal.Font.Size = Brand.BodySize;
        normal.Font.Color = Ink;

        var section = doc.AddSection();
        var setup = section.PageSetup;
        setup.PageWidth = Mm(210);
        setup.PageHeight = Mm(297);
        setup.TopMargin = Mm(Brand.MarginTopMm);
        setup.LeftMargin = setup.RightMargin = Mm(Brand.MarginSideMm);
        setup.BottomMargin = Mm(24);
        setup.FooterDistance = Mm(8);

        AddHeader(section, inv);
        AddParties(section, inv);
        AddLines(section, inv);
        AddTotals(section, inv);
        AddPayment(section, inv);
        AddFooter(section, inv);
        return doc;
    }

    static void AddHeader(Section section, Invoice inv)
    {
        var table = NoPaddingTable(section, 98, 80);
        var row = table.AddRow();

        var left = row.Cells[0];
        var wordmark = left.AddParagraph(Brand.Wordmark);
        wordmark.Format.Font.Size = Brand.WordmarkSize;
        wordmark.Format.Font.Bold = true;
        wordmark.Format.Font.Color = Accent;
        wordmark.Format.SpaceAfter = Mm(2);
        foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) left.AddParagraph(line);
        MutedParagraph(left.AddParagraph(InvoiceText.SupplierVatLine(inv)));
        MutedParagraph(left.AddParagraph(inv.Supplier.Email ?? ""));

        var right = row.Cells[1];
        var title = right.AddParagraph("Invoice");
        title.Format.Alignment = ParagraphAlignment.Right;
        title.Format.Font.Size = Brand.TitleSize;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Mm(2);
        foreach (var (label, value) in InvoiceText.Meta(inv))
        {
            // Label on the left, value pushed to the right edge with a right-aligned tab stop.
            var p = right.AddParagraph();
            p.Format.TabStops.AddTabStop(Mm(80), TabAlignment.Right);
            p.AddFormattedText(label).Color = Muted;
            p.AddTab();
            p.AddText(value);
        }
    }

    static void AddParties(Section section, Invoice inv)
    {
        Spacer(section, 8);
        var table = NoPaddingTable(section, 114, 64);

        // Two rows: the "Bill to" cell spans both, so the teal panel is only as tall as its content.
        var top = table.AddRow();
        table.AddRow();
        var billTo = top.Cells[0];
        billTo.MergeDown = 1;
        var heading = billTo.AddParagraph("Bill to");
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = Accent;
        heading.Format.SpaceAfter = Mm(1.5);
        billTo.AddParagraph().AddFormattedText(inv.Customer.Name, TextFormat.Bold);
        if (inv.Customer.Attention is not null) billTo.AddParagraph($"For the attention of {inv.Customer.Attention}");
        foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) billTo.AddParagraph(line);

        var due = top.Cells[1];
        due.Shading.Color = Accent;
        due.Format.Font.Color = White;
        due.Format.Alignment = ParagraphAlignment.Right;
        due.Format.RightIndent = Mm(5);
        var label = due.AddParagraph("Amount due");
        label.Format.SpaceBefore = Mm(4);
        var amount = due.AddParagraph(Fmt.Money(inv.Total));
        amount.Format.Font.Size = Brand.AmountDueSize;
        amount.Format.Font.Bold = true;
        var by = due.AddParagraph($"by {Fmt.Date(inv.DueDate)}");
        by.Format.SpaceAfter = Mm(4);
        Spacer(section, 7);
    }

    static void AddLines(Section section, Invoice inv)
    {
        var table = section.AddTable();
        table.TopPadding = table.BottomPadding = Mm(1.8);
        table.LeftPadding = table.RightPadding = Mm(2);
        table.Rows.LeftIndent = Mm(2); // MigraDoc outdents tables by the cell padding; realign with the text above
        double[] widths = [92, 22, 24, 14, 26];
        for (var i = 0; i < widths.Length; i++)
        {
            var column = table.AddColumn(Mm(widths[i]));
            if (i > 0) column.Format.Alignment = ParagraphAlignment.Right;
        }

        var header = table.AddRow();
        header.HeadingFormat = true; // repeated at the top of every page
        header.Shading.Color = Tint;
        header.Format.Font.Bold = true;
        for (var i = 0; i < InvoiceText.ColumnHeaders.Length; i++) header.Cells[i].AddParagraph(InvoiceText.ColumnHeaders[i]);

        foreach (var line in inv.Lines)
        {
            var row = table.AddRow();
            row.Borders.Bottom.Width = 0.5;
            row.Borders.Bottom.Color = RuleColor;
            var cells = InvoiceText.Row(line);
            for (var i = 0; i < cells.Length; i++) row.Cells[i].AddParagraph(cells[i]);
        }
    }

    static void AddTotals(Section section, Invoice inv)
    {
        Spacer(section, 4);
        var table = section.AddTable();
        table.Rows.Alignment = RowAlignment.Right;
        table.LeftPadding = table.RightPadding = Mm(2);
        table.TopPadding = table.BottomPadding = Mm(1.1);
        table.AddColumn(Mm(60));
        table.AddColumn(Mm(24)).Format.Alignment = ParagraphAlignment.Right;

        void Line(string label, string value, bool grand = false)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(label);
            row.Cells[1].AddParagraph(value);
            if (!grand) return;
            row.Borders.Top.Width = 1;
            row.Borders.Top.Color = Ink;
            row.TopPadding = Mm(2);
            row.Format.Font.Bold = true;
            row.Format.Font.Size = Brand.GrandTotalSize;
        }

        Line("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal));
        foreach (var band in inv.VatBands) Line(InvoiceText.VatLabel(band), Fmt.Money(band.Vat));
        Line("Total due", Fmt.Money(inv.Total), grand: true);
        table.Rows[0].KeepWith = table.Rows.Count - 1;
    }

    static void AddPayment(Section section, Invoice inv)
    {
        var heading = section.AddParagraph("How to pay");
        heading.Format.SpaceBefore = Mm(8);
        heading.Format.SpaceAfter = Mm(1.5);
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = Accent;
        heading.Format.KeepWithNext = true;

        var table = NoPaddingTable(section, 30, 148);
        var details = InvoiceText.Payment(inv);
        foreach (var (label, value) in details)
        {
            var row = table.AddRow();
            MutedParagraph(row.Cells[0].AddParagraph(label));
            row.Cells[1].AddParagraph(value);
        }
        table.Rows[0].KeepWith = details.Count - 1;

        var notes = MutedParagraph(section.AddParagraph(inv.Notes));
        notes.Format.SpaceBefore = Mm(3);
    }

    static void AddFooter(Section section, Invoice inv)
    {
        var table = section.Footers.Primary.AddTable();
        table.LeftPadding = table.RightPadding = 0;
        table.AddColumn(Mm(148));
        table.AddColumn(Mm(30));
        table.Format.Font.Size = Brand.SmallSize;
        table.Format.Font.Color = Muted;

        var row = table.AddRow();
        row.Borders.Top.Width = 0.5;
        row.Borders.Top.Color = RuleColor;
        row.TopPadding = Mm(2.5);
        row.Cells[0].AddParagraph(InvoiceText.LegalFooter(inv));
        var page = row.Cells[1].AddParagraph();
        page.Format.Alignment = ParagraphAlignment.Right;
        page.AddText("Page ");
        page.AddPageField();
        page.AddText(" of ");
        page.AddNumPagesField();
    }

    static Table NoPaddingTable(Section section, params double[] widthsMm)
    {
        var table = section.AddTable();
        table.LeftPadding = table.RightPadding = table.TopPadding = table.BottomPadding = 0;
        foreach (var w in widthsMm) table.AddColumn(Mm(w));
        return table;
    }

    static void Spacer(Section section, double mm)
    {
        var p = section.AddParagraph();
        p.Format.Font.Size = 1;
        p.Format.SpaceAfter = Mm(mm);
    }

    static Paragraph MutedParagraph(Paragraph p)
    {
        p.Format.Font.Color = Muted;
        return p;
    }

    static Color Hex(string hex)
    {
        var (r, g, b) = Brand.Rgb(hex);
        return new Color(r, g, b);
    }
}

/// <summary>Maps every font request to the embedded Lato files.</summary>
sealed class LatoFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "Lato-Bold" : "Lato-Regular", false, isItalic);

    public byte[]? GetFont(string faceName) => faceName == "Lato-Bold" ? Brand.LatoBold : Brand.LatoRegular;
}
