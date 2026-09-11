using InvoicePoc;
using InvoicePoc.Html;
using PuppeteerSharp;

// PuppeteerSharp: the .NET port of Puppeteer. Same approach as Playwright (HTML -> headless Chrome
// -> PDF), with a slightly lower-level API and a smaller dependency footprint.

// One-off setup: download the Chrome for Testing build pinned by this PuppeteerSharp version
// into a per-user cache (skipped if it's already there).
var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PuppeteerSharp");
var chrome = await new BrowserFetcher(new BrowserFetcherOptions { Path = cache }).DownloadAsync();

IBrowser? browser = null;
try
{
    return await PocRunner.RunAsync("PuppeteerSharp", "puppeteersharp.pdf", async (invoice, path) =>
    {
        browser ??= await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = chrome.GetExecutablePath(),
            // Chrome refuses to run sandboxed as root (typical in containers). Don't disable the
            // sandbox on a normal Windows/Linux account.
            Args = OperatingSystem.IsLinux() && Environment.IsPrivilegedProcess ? ["--no-sandbox"] : [],
        });

        var html = await InvoiceHtml.RenderAsync(invoice);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        await page.EvaluateExpressionAsync("document.fonts.ready");
        await page.PdfAsync(path, new PdfOptions
        {
            PreferCSSPageSize = true, // take A4 + margins from the @page rule
            PrintBackground = true,
            Tagged = true,
        });
    });
}
finally
{
    if (browser is not null) await browser.CloseAsync();
}
