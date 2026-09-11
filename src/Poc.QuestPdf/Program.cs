using InvoicePoc;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// QuestPDF: layout is described with a fluent C# API; pagination, repeating table
// headers and page numbers are handled by the engine.

// Community licence covers individuals and businesses under US$1M revenue.
// Set LicenseType.Professional / Enterprise if you hold a paid licence.
QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.UseEnvironmentFonts = false; // only use fonts we register (deterministic output)
FontManager.RegisterFont(new MemoryStream(Brand.LatoRegular));
FontManager.RegisterFont(new MemoryStream(Brand.LatoBold));

if (args.Contains("stress"))
{
    StressDocument.RegisterFonts();
    return await PocRunner.RunAsync("QuestPDF (stress)", "stress-questpdf.pdf", (_, path) => StressDocument.Generate(path), warmRuns: 2);
}

return await PocRunner.RunAsync("QuestPDF", "questpdf.pdf", (invoice, path) =>
    new InvoiceDocument(invoice).GeneratePdf(path));

sealed class InvoiceDocument(Invoice inv) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Invoice {inv.Number}",
        Author = inv.Supplier.Name,
        Subject = $"Invoice for {inv.Customer.Name}",
    };

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(Brand.MarginTopMm, Unit.Millimetre);
            page.MarginHorizontal(Brand.MarginSideMm, Unit.Millimetre);
            page.MarginBottom(10, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontFamily(Brand.FontFamily).FontSize(Brand.BodySize).FontColor(Brand.Ink).LineHeight(1.35f));

            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });

    private void ComposeContent(IContainer container) =>
        container.Column(col =>
        {
            col.Item().Element(ComposeHeader);
            col.Item().PaddingTop(8, Unit.Millimetre).PaddingBottom(7, Unit.Millimetre).Element(ComposeParties);
            col.Item().Element(ComposeLines);
            col.Item().PaddingTop(4, Unit.Millimetre).AlignRight().Width(84, Unit.Millimetre).Element(ComposeTotals);
            col.Item().PaddingTop(8, Unit.Millimetre).ShowEntire().Element(ComposePayment);
        });

    private void ComposeHeader(IContainer container) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().PaddingBottom(2, Unit.Millimetre).Text(Brand.Wordmark).FontSize(Brand.WordmarkSize).Bold().FontColor(Brand.Accent);
                foreach (var line in InvoiceText.AddressLines(inv.Supplier.Address)) c.Item().Text(line);
                c.Item().Text(InvoiceText.SupplierVatLine(inv)).FontColor(Brand.Muted);
                c.Item().Text(inv.Supplier.Email).FontColor(Brand.Muted);
            });

            row.ConstantItem(80, Unit.Millimetre).Column(c =>
            {
                c.Item().PaddingBottom(2, Unit.Millimetre).AlignRight().Text("Invoice").FontSize(Brand.TitleSize).Bold();
                foreach (var (label, value) in InvoiceText.Meta(inv))
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text(label).FontColor(Brand.Muted);
                        r.AutoItem().Text(value);
                    });
                }
            });
        });

    private void ComposeParties(IContainer container) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().PaddingBottom(1.5f, Unit.Millimetre).Text("Bill to").Bold().FontColor(Brand.Accent);
                c.Item().Text(inv.Customer.Name).Bold();
                if (inv.Customer.Attention is not null) c.Item().Text($"For the attention of {inv.Customer.Attention}");
                foreach (var line in InvoiceText.AddressLines(inv.Customer.Address)) c.Item().Text(line);
            });

            row.ConstantItem(64, Unit.Millimetre).Background(Brand.Accent)
                .PaddingVertical(4, Unit.Millimetre).PaddingHorizontal(5, Unit.Millimetre)
                .DefaultTextStyle(t => t.FontColor(Brand.OnAccent))
                .Column(c =>
                {
                    c.Item().AlignRight().Text("Amount due");
                    c.Item().AlignRight().Text(Fmt.Money(inv.Total)).FontSize(Brand.AmountDueSize).Bold();
                    c.Item().AlignRight().Text($"by {Fmt.Date(inv.DueDate)}");
                });
        });

    private void ComposeLines(IContainer container) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.ConstantColumn(22, Unit.Millimetre);
                cols.ConstantColumn(24, Unit.Millimetre);
                cols.ConstantColumn(14, Unit.Millimetre);
                cols.ConstantColumn(26, Unit.Millimetre);
            });

            // Header rows repeat automatically on every page the table spans.
            table.Header(header =>
            {
                for (var i = 0; i < InvoiceText.ColumnHeaders.Length; i++)
                {
                    var cell = header.Cell().Background(Brand.AccentTint).PaddingVertical(1.8f, Unit.Millimetre).PaddingHorizontal(2, Unit.Millimetre);
                    (i == 0 ? cell : cell.AlignRight()).Text(InvoiceText.ColumnHeaders[i]).Bold();
                }
            });

            foreach (var line in inv.Lines)
            {
                var cells = InvoiceText.Row(line);
                for (var i = 0; i < cells.Length; i++)
                {
                    var cell = table.Cell().BorderBottom(0.5f).BorderColor(Brand.Rule)
                        .PaddingVertical(1.8f, Unit.Millimetre).PaddingHorizontal(2, Unit.Millimetre);
                    (i == 0 ? cell : cell.AlignRight()).Text(cells[i]);
                }
            }
        });

    private void ComposeTotals(IContainer container) =>
        container.Column(col =>
        {
            void Line(string label, string value, bool grand = false)
            {
                var item = grand
                    ? col.Item().PaddingTop(1, Unit.Millimetre).BorderTop(1).BorderColor(Brand.Ink).PaddingTop(2, Unit.Millimetre)
                    : col.Item().PaddingVertical(1.1f, Unit.Millimetre);
                item.PaddingHorizontal(2, Unit.Millimetre).Row(r =>
                {
                    var l = r.RelativeItem().Text(label);
                    var v = r.AutoItem().Text(value);
                    if (grand) { l.Bold().FontSize(Brand.GrandTotalSize); v.Bold().FontSize(Brand.GrandTotalSize); }
                });
            }

            Line("Subtotal (excluding VAT)", Fmt.Money(inv.Subtotal));
            foreach (var band in inv.VatBands) Line(InvoiceText.VatLabel(band), Fmt.Money(band.Vat));
            Line("Total due", Fmt.Money(inv.Total), grand: true);
        });

    private void ComposePayment(IContainer container) =>
        container.Column(col =>
        {
            col.Item().PaddingBottom(1.5f, Unit.Millimetre).Text("How to pay").Bold().FontColor(Brand.Accent);
            foreach (var (label, value) in InvoiceText.Payment(inv))
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(30, Unit.Millimetre).Text(label).FontColor(Brand.Muted);
                    r.RelativeItem().Text(value);
                });
            }
            col.Item().PaddingTop(3, Unit.Millimetre).Text(inv.Notes).FontColor(Brand.Muted);
        });

    private void ComposeFooter(IContainer container) =>
        container.BorderTop(0.5f).BorderColor(Brand.Rule).PaddingTop(2.5f, Unit.Millimetre)
            .DefaultTextStyle(t => t.FontSize(Brand.SmallSize).FontColor(Brand.Muted))
            .Row(row =>
            {
                row.RelativeItem().Text(InvoiceText.LegalFooter(inv));
                row.ConstantItem(30, Unit.Millimetre).AlignRight().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
}
