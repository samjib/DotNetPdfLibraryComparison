using InvoicePoc;
using InvoicePoc.Html;
using Microsoft.Playwright;

// Playwright for .NET: render the Razor/HTML template, then "print to PDF" with headless Chromium.
// Full modern CSS (flex/grid, @page margin boxes for the footer and page numbers).

// One-off setup: download the Chromium build this Playwright version is pinned to (no-op if present).
// On Linux servers also install OS dependencies once, as root: Program.Main(["install-deps", "chromium"]).
if (Microsoft.Playwright.Program.Main(["install", "--only-shell", "chromium"]) != 0)
    throw new InvalidOperationException("Playwright could not install Chromium.");

IPlaywright? playwright = null;
IBrowser? browser = null;
try
{
    var stress = args.Contains("stress");
    return await PocRunner.RunAsync(stress ? "Playwright (stress)" : "Playwright", stress ? "stress-chromium.pdf" : "playwright.pdf", async (invoice, path) =>
    {
        // Start the browser once and reuse it; the first document therefore includes start-up cost.
        playwright ??= await Playwright.CreateAsync();
        browser ??= await playwright.Chromium.LaunchAsync(new() { Headless = true });

        var html = stress ? await InvoiceHtml.RenderStressAsync() : await InvoiceHtml.RenderAsync(invoice);
        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load });
            await page.EvaluateAsync("document.fonts.ready");
            await page.PdfAsync(new PagePdfOptions
            {
                Path = path,
                PreferCSSPageSize = true, // take A4 + margins from the @page rule
                PrintBackground = true,   // keep the teal panel and table header fill
                Tagged = true,            // tagged PDF (better accessibility / screen readers)
            });
        }
        finally
        {
            await page.CloseAsync();
        }
    }, warmRuns: stress ? 2 : 5);
}
finally
{
    if (browser is not null) await browser.CloseAsync();
    playwright?.Dispose();
}
