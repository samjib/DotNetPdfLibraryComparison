using InvoicePoc;
using InvoicePoc.Stress;
using QuestPDF.Drawing;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>Layout stress test (see StressData for the feature list).</summary>
static class StressDocument
{
    const Unit Mm = Unit.Millimetre;
    static readonly Image Photo = Image.FromBinaryData(StressAssets.Photo);
    static readonly Dictionary<string, Image> Thumbs = new();

    public static void RegisterFonts()
    {
        foreach (var font in new[] { StressAssets.SerifDisplay, StressAssets.Mono, StressAssets.Arabic, StressAssets.Fallback })
            FontManager.RegisterFont(new MemoryStream(font));
    }

    public static void Generate(string path) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(Brand.MarginSideMm, Mm);
            page.MarginTop(10, Mm);
            page.MarginBottom(8, Mm);
            // Fallback chain: glyphs missing from Lato (✓, Arabic) come from the next fonts automatically.
            page.DefaultTextStyle(t => t.FontFamily(Brand.FontFamily, StressAssets.FallbackFamily, StressAssets.ArabicFamily)
                .FontSize(9).FontColor(Brand.Ink));

            page.Header().SkipOnce().Element(RunningHeader);      // every page except the first
            page.Content().Element(Content);
            page.Footer().Dynamic(new StressFooter());             // knows PageNumber and TotalPages
        }))
        .WithMetadata(new DocumentMetadata { Title = StressData.Title, Author = "Brightwater Digital Ltd" })
        .GeneratePdf(path);

    static void RunningHeader(IContainer c) =>
        c.PaddingBottom(4, Mm).BorderBottom(0.5f).BorderColor(Brand.Rule).PaddingBottom(2, Mm).Row(r =>
        {
            r.ConstantItem(6, Mm).Height(6, Mm).Svg(StressAssets.LogoSvg);
            r.RelativeItem().PaddingLeft(2, Mm).AlignMiddle().Text(StressData.HeaderText).FontSize(8).FontColor(Brand.Muted);
        });

    static void Content(IContainer c) => c.Column(col =>
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(18, Mm).Height(18, Mm).Svg(StressAssets.LogoSvg);        // vector logo with gradient
            r.RelativeItem().PaddingLeft(4, Mm).Column(t =>
            {
                t.Item().Text(StressData.Title).FontFamily(StressAssets.SerifFamily).FontSize(26).FontColor(Brand.Accent);
                t.Item().Text($"Statement date {StressData.StatementDate}").FontColor(Brand.Muted);
            });
            r.ConstantItem(55, Mm).AlignRight().Column(t =>
            {
                t.Item().AlignRight().Text("Account reference").FontColor(Brand.Muted);
                t.Item().AlignRight().Text(StressData.AccountRef).FontFamily(StressAssets.MonoFamily).FontSize(11);
            });
        });

        col.Item().PaddingTop(6, Mm).Row(r =>
        {
            r.RelativeItem().Column(t =>
            {
                t.Item().Text(StressData.CustomerName).Bold();
                foreach (var line in StressData.BranchAddress) t.Item().Text(line);   // Welsh ŵ / ŷ
            });
            r.ConstantItem(80, Mm).Column(t =>
            {
                // Shaping and right-to-left ordering are automatic.
                t.Item().AlignRight().Text(StressData.ArabicTradingName).FontFamily(StressAssets.ArabicFamily).FontSize(16);
                t.Item().AlignRight().Text(StressData.ArabicLabel).FontColor(Brand.Muted);
            });
        });

        col.Item().PaddingTop(6, Mm).ShowEntire().Column(p =>                       // photo + caption never split
        {
            p.Item().Image(Photo).FitWidth();
            p.Item().PaddingTop(1.5f, Mm).Text(StressData.PhotoCaption).FontSize(8).FontColor(Brand.Muted);
        });

        col.Item().PaddingTop(6, Mm).Text(StressData.TransactionsHeading).FontFamily(StressAssets.SerifFamily).FontSize(15);
        col.Item().PaddingTop(2, Mm).Element(Transactions);
        col.Item().PaddingTop(3, Mm).AlignRight().Text($"Balance due {Fmt.Money(StressData.Balance)}").Bold().FontSize(11);

        // EnsureSpace moves the block to a new page if less than ~45 mm is left, so the heading is never orphaned.
        col.Item().PaddingTop(8, Mm).EnsureSpace(Brand.Mm(45)).Column(terms =>
        {
            terms.Item().Text(StressData.TermsHeading).FontFamily(StressAssets.SerifFamily).FontSize(15);
            terms.Item().PaddingTop(2, Mm).MultiColumn(mc =>                       // text flows column to column
            {
                mc.Columns(2);
                mc.Spacing(Brand.Mm(8));
                mc.BalanceHeight();                                                 // otherwise column 1 fills first
                mc.Content().Column(t =>
                {
                    t.Spacing(2, Mm);
                    foreach (var para in StressData.Terms) t.Item().Text(para).Justify();
                });
            });
        });

        col.Item().PaddingTop(8, Mm).ShowEntire().Border(1).BorderColor(Brand.Accent).Padding(4, Mm).Column(box =>
        {
            box.Spacing(3, Mm);
            box.Item().Text(StressData.RemittanceHeading).FontFamily(StressAssets.SerifFamily).FontSize(13);
            foreach (var (label, value) in StressData.RemittanceFields)
                box.Item().Row(r =>
                {
                    r.ConstantItem(40, Mm).Text(label).FontColor(Brand.Muted);
                    r.RelativeItem().Text(value).FontFamily(StressAssets.MonoFamily);
                });
        });
    });

    static void Transactions(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.ConstantColumn(22, Mm); cd.ConstantColumn(30, Mm); cd.ConstantColumn(10, Mm);
            cd.RelativeColumn(); cd.ConstantColumn(18, Mm); cd.ConstantColumn(24, Mm);
        });
        t.Header(h =>                                                               // repeats on every page
        {
            for (var i = 0; i < StressData.Columns.Length; i++)
            {
                var cell = h.Cell().Background(Brand.AccentTint).PaddingVertical(1.5f, Mm).PaddingHorizontal(1.5f, Mm);
                (i == 5 ? cell.AlignRight() : cell).Text(StressData.Columns[i]).Bold();
            }
        });

        var n = 0;
        foreach (var row in StressData.Rows())
        {
            var background = n++ % 2 == 1 ? "#F4F8F7" : "#FFFFFF";
            IContainer Cell() => t.Cell().ShowEntire().Background(background).PaddingVertical(1.5f, Mm).PaddingHorizontal(1.5f, Mm);

            Cell().Text(StressData.ShortDate(row.Date));
            Cell().Text(row.Reference).FontFamily(StressAssets.MonoFamily).FontSize(8);
            if (row.Thumbnail is null) Cell();
            else Cell().Height(6, Mm).AlignCenter().Image(Thumb(row.Thumbnail)).FitArea();  // PNG with alpha in a cell
            Cell().Text(row.Description);
            Cell().Text(row.Paid ? StressData.PaidMark : "Due").FontColor(row.Paid ? Brand.Accent : Brand.Ink);
            Cell().AlignRight().Text(Fmt.Money(row.Amount));
        }
    });

    static Image Thumb(string name)
    {
        lock (Thumbs)
        {
            if (!Thumbs.TryGetValue(name, out var image)) Thumbs[name] = image = Image.FromBinaryData(StressAssets.Thumbnail(name));
            return image;
        }
    }
}

/// <summary>Footer text differs on the last page; possible because dynamic components know TotalPages.</summary>
sealed class StressFooter : IDynamicComponent
{
    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        var last = context.PageNumber == context.TotalPages;
        var content = context.CreateElement(e => e
            .BorderTop(0.5f).BorderColor(Brand.Rule).PaddingTop(2, Unit.Millimetre)
            .DefaultTextStyle(t => t.FontSize(7.5f).FontColor(Brand.Muted))
            .Row(r =>
            {
                r.RelativeItem().Text(last ? StressData.EndText : StressData.ContinuedText);
                r.AutoItem().Text(InvoiceText.PageOf(context.PageNumber, context.TotalPages));
            }));
        return new DynamicComponentComposeResult { Content = content, HasMoreContent = false };
    }
}
