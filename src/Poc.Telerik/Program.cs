using InvoicePoc;
using Telerik.Documents.Common.Model;
using Telerik.Documents.Core.Fonts;
using Telerik.Documents.Extensibility;
using Telerik.Documents.Fixed.FormatProviders.Pdf.Export;
using Telerik.Documents.Flow.FormatProviders.Pdf;
using Telerik.Documents.Flow.Model;
using Telerik.Documents.Flow.Model.Editing;
using Telerik.Documents.Flow.Model.Styles;
using Telerik.Documents.Media;
using Telerik.Documents.Model;
using Telerik.Documents.Primitives;

// Telerik Document Processing (RadWordsProcessing "Flow" model): a Word-like document of
// sections, paragraphs, tables and fields, exported to PDF. Commercial (bundled with Telerik UI).
// Note: the 2026 packages use Telerik.Documents.* namespaces (older samples use Telerik.Windows.Documents.*).

// On .NET Core the PDF exporter can't enumerate system fonts, so a FontsProvider must supply font bytes.
if (args.Contains("merge"))
{
    FixedExtensibilityManager.FontsProvider = new LatoFontsProvider();
    return await PocRunner.RunAsync("Telerik (merge)", "merged-telerik.pdf", MergeDocuments.Generate, warmRuns: 2);
}

if (args.Contains("stress"))
{
    FixedExtensibilityManager.FontsProvider = new StressFontsProvider();
    // .NET Core has no built-in image decoding: images need Telerik.Documents.ImageUtils (SkiaSharp).
    FixedExtensibilityManager.ImagePropertiesResolver = new Telerik.Documents.ImageUtils.ImagePropertiesResolver();
    FixedExtensibilityManager.JpegImageConverter = new Telerik.Documents.ImageUtils.JpegImageConverter();
    return await PocRunner.RunAsync("Telerik (stress)", "stress-telerik.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);
}

FixedExtensibilityManager.FontsProvider = new LatoFontsProvider();

return await PocRunner.RunAsync("Telerik", "telerik.pdf", (invoice, path) =>
{
    var document = InvoiceDocument.Build(invoice);
    var provider = new PdfFormatProvider();
    // Without this the full font programs were embedded (~320 KB file instead of ~50 KB).
    provider.ExportSettings.FontEmbeddingType = FontEmbeddingType.Subset;
    using var file = File.Create(path);
    provider.Export(document, file, TimeSpan.FromSeconds(30));
});

static class InvoiceDocument
{
    static double Mm(double mm) => Unit.MmToDip(mm);   // Telerik works in device-independent pixels (1/96")
    static double Pt(double pt) => Unit.PointToDip(pt);
    static readonly Border None = new(BorderStyle.None);

    public static RadFlowDocument Build(Invoice inv)
    {
        var document = new RadFlowDocument();
        var section = document.Sections.AddSection();
        section.PageSize = PaperTypeConverter.ToSize(PaperTypes.A4);
        section.PageMargins = new Padding(Mm(Brand.MarginSideMm), Mm(Brand.MarginTopMm), Mm(Brand.MarginSideMm), Mm(24));
        section.FooterBottomMargin = Mm(7);

        Header(section, inv);
        Parties(section, inv);
        Lines(section, inv);
        Totals(section, inv);
        Payment(section, inv);
        Footer(document, section, inv);
        return document;
    }

    static void Header(Section section, Invoice inv)
    {
        var table = NewTable(section, 98, 80);
        var row = table.Rows.AddTableRow();

        var left = AddCell(row, 98);
        Para(left, Brand.Wordmark, Brand.WordmarkSize, bold: true, color: Brand.Accent, afterMm: 2);
        foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) Para(left, line);
        Para(left, InvoiceText.SupplierVatLine(inv), color: Brand.Muted);
        Para(left, inv.Supplier.Email ?? "", color: Brand.Muted);

        var right = AddCell(row, 80);
        Para(right, "Invoice", Brand.TitleSize, bold: true, align: Alignment.Right, afterMm: 2);
        var meta = NewTable(right, 36, 44);
        foreach (var (label, value) in InvoiceText.Meta(inv))
        {
            var r = meta.Rows.AddTableRow();
            Para(AddCell(r, 36), label, color: Brand.Muted);
            Para(AddCell(r, 44), value, align: Alignment.Right);
        }
        Para(right, "", 1); // a cell's last block must be a paragraph (Word rule)
    }

    static void Parties(Section section, Invoice inv)
    {
        Para(section, "", 1, afterMm: 8);
        var table = NewTable(section, 114, 64);
        var row = table.Rows.AddTableRow();

        var billTo = AddCell(row, 114);
        Para(billTo, "Bill to", bold: true, color: Brand.Accent, afterMm: 1.5);
        Para(billTo, inv.Customer.Name, bold: true);
        if (inv.Customer.Attention is not null) Para(billTo, $"For the attention of {inv.Customer.Attention}");
        foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) Para(billTo, line);

        // Nested one-cell table carries the shading, so the panel is only as tall as its content.
        var holder = AddCell(row, 64);
        var panel = NewTable(holder, 64);
        var cell = AddCell(panel.Rows.AddTableRow(), 64);
        cell.Shading.BackgroundColor = Colour(Brand.Accent);
        cell.Padding = new Padding(Mm(5), Mm(4), Mm(5), Mm(4));
        Para(cell, "Amount due", color: Brand.OnAccent, align: Alignment.Right);
        Para(cell, Fmt.Money(inv.Total), Brand.AmountDueSize, bold: true, color: Brand.OnAccent, align: Alignment.Right);
        Para(cell, $"by {Fmt.Date(inv.DueDate)}", color: Brand.OnAccent, align: Alignment.Right);
        Para(holder, "", 1);
        Para(section, "", 1, afterMm: 7);
    }

    static void Lines(Section section, Invoice inv)
    {
        double[] widths = [92, 22, 24, 14, 26];
        var table = NewTable(section, widths);

        var header = table.Rows.AddTableRow();
        // Finding: RepeatOnEveryPage is honoured by Word/DOCX, but in 2026.3.826 the PDF exporter did not
        // repeat the row (reproduced with a minimal 80-row table). Kept here so the intent is clear.
        header.RepeatOnEveryPage = true;
        for (var i = 0; i < widths.Length; i++)
        {
            var cell = LineCell(header, widths[i]);
            cell.Shading.BackgroundColor = Colour(Brand.AccentTint);
            Para(cell, InvoiceText.ColumnHeaders[i], bold: true, align: Align(i));
        }

        var rule = new Border(Pt(0.5), BorderStyle.Single, Colour(Brand.Rule));
        foreach (var line in inv.Lines)
        {
            var row = table.Rows.AddTableRow();
            row.CanSplit = false;
            var cells = InvoiceText.Row(line);
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = LineCell(row, widths[i]);
                cell.Borders = new TableCellBorders(None, None, None, rule);
                Para(cell, cells[i], align: Align(i));
            }
        }
    }

    static TableCell LineCell(TableRow row, double widthMm)
    {
        var cell = AddCell(row, widthMm);
        cell.Padding = new Padding(Mm(2), Mm(1.8), Mm(2), Mm(1.8));
        return cell;
    }

    static void Totals(Section section, Invoice inv)
    {
        Para(section, "", 1, afterMm: 4);
        var table = NewTable(section, 60, 24);
        table.Alignment = Alignment.Right;
        var lines = new List<(string Label, string Value, bool Grand)> { ("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal), false) };
        lines.AddRange(inv.VatBands.Select(b => (InvoiceText.VatLabel(b), Fmt.Money(b.Vat), false)));
        lines.Add(("Total due", Fmt.Money(inv.Total), true));

        for (var i = 0; i < lines.Count; i++)
        {
            var (label, value, grand) = lines[i];
            var row = table.Rows.AddTableRow();
            row.CanSplit = false;
            var size = grand ? Brand.GrandTotalSize : Brand.BodySize;
            var top = grand ? new Border(Pt(1), BorderStyle.Single, Colour(Brand.Ink)) : None;
            foreach (var (text, width, align) in new[] { (label, 60.0, Alignment.Left), (value, 24.0, Alignment.Right) })
            {
                var cell = AddCell(row, width);
                cell.Padding = new Padding(Mm(2), Mm(grand ? 2 : 1.1), Mm(2), Mm(1.1));
                cell.Borders = new TableCellBorders(None, top, None, None);
                // "Keep with next" on every row but the last keeps the whole block on one page.
                Para(cell, text, size, bold: grand, align: align).KeepWithNextParagraph = i < lines.Count - 1;
            }
        }
    }

    static void Payment(Section section, Invoice inv)
    {
        var heading = Para(section, "How to pay", bold: true, color: Brand.Accent, beforeMm: 8, afterMm: 1.5);
        heading.KeepWithNextParagraph = true;
        var table = NewTable(section, 30, 148);
        foreach (var (label, value) in InvoiceText.Payment(inv))
        {
            var row = table.Rows.AddTableRow();
            Para(AddCell(row, 30), label, color: Brand.Muted).KeepWithNextParagraph = true;
            Para(AddCell(row, 148), value).KeepWithNextParagraph = true;
        }
        Para(section, inv.Notes, color: Brand.Muted, beforeMm: 3);
    }

    static void Footer(RadFlowDocument document, Section section, Invoice inv)
    {
        var footer = section.Footers.Add();
        var table = NewTable(footer, 148, 30);
        var row = table.Rows.AddTableRow();
        var ruleTop = new TableCellBorders(None, new Border(Pt(0.5), BorderStyle.Single, Colour(Brand.Rule)), None, None);

        var left = AddCell(row, 148);
        left.Borders = ruleTop;
        left.Padding = new Padding(0, Mm(2.5), 0, 0);
        Para(left, InvoiceText.LegalFooter(inv), Brand.SmallSize, color: Brand.Muted);

        var right = AddCell(row, 30);
        right.Borders = ruleTop;
        right.Padding = new Padding(0, Mm(2.5), 0, 0);
        var page = Para(right, "", Brand.SmallSize, align: Alignment.Right);

        // PAGE / NUMPAGES fields are evaluated per page during PDF export.
        var editor = new RadFlowDocumentEditor(document);
        editor.MoveToParagraphStart(page);
        editor.CharacterFormatting.FontFamily.LocalValue = new ThemableFontFamily(new FontFamily(Brand.FontFamily));
        editor.CharacterFormatting.FontSize.LocalValue = Pt(Brand.SmallSize);
        editor.CharacterFormatting.ForegroundColor.LocalValue = Colour(Brand.Muted);
        editor.InsertText("Page ");
        editor.InsertField("PAGE", "1");
        editor.InsertText(" of ");
        editor.InsertField("NUMPAGES", "1");
        Para(footer, "", 1);
    }

    static Table NewTable(BlockContainerBase container, params double[] widthsMm)
    {
        var table = container.Blocks.AddTable();
        table.LayoutType = TableLayoutType.FixedWidth;
        table.PreferredWidth = new TableWidthUnit(TableWidthUnitType.Fixed, Mm(widthsMm.Sum()));
        table.Borders = new TableBorders(None);
        table.TableCellPadding = new Padding(0, 0, 0, 0);
        return table;
    }

    static TableCell AddCell(TableRow row, double widthMm)
    {
        var cell = row.Cells.AddTableCell();
        cell.PreferredWidth = new TableWidthUnit(TableWidthUnitType.Fixed, Mm(widthMm));
        cell.Padding = new Padding(0, 0, 0, 0); // otherwise Word's default ~1.9 mm cell margins apply
        return cell;
    }

    static Paragraph Para(BlockContainerBase container, string text, double sizePt = Brand.BodySize, bool bold = false,
        string color = Brand.Ink, Alignment align = Alignment.Left, double beforeMm = 0, double afterMm = 0)
    {
        var p = container.Blocks.AddParagraph();
        p.TextAlignment = align;
        p.Spacing.SpacingBefore = Mm(beforeMm);
        p.Spacing.SpacingAfter = Mm(afterMm);
        p.Spacing.LineSpacingType = HeightType.Auto;
        p.Spacing.LineSpacing = 1.1;
        if (text.Length == 0) return p;
        var run = p.Inlines.AddRun(text);
        run.FontFamily = new ThemableFontFamily(new FontFamily(Brand.FontFamily));
        run.FontSize = Pt(sizePt);
        run.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;
        run.ForegroundColor = Colour(color);
        return p;
    }

    static Alignment Align(int column) => column == 0 ? Alignment.Left : Alignment.Right;

    static ThemableColor Colour(string hex)
    {
        var (r, g, b) = Brand.Rgb(hex);
        return new ThemableColor(Color.FromRgb(r, g, b));
    }
}

/// <summary>Supplies Lato for every font request (the exporter asks by family, weight and style).</summary>
sealed class LatoFontsProvider : FontsProviderBase
{
    public override byte[] GetFontData(FontProperties fontProperties) =>
        fontProperties.FontWeight == FontWeights.Bold ? Brand.LatoBold : Brand.LatoRegular;
}
