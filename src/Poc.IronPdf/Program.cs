using InvoicePoc;
using InvoicePoc.Html;
using IronPdf;
using IronPdf.Rendering;

// IronPDF: a commercial wrapper around an embedded Chromium engine. Same HTML template as the
// other Chromium-based POCs; the difference is packaging, API convenience and support.

var key = Environment.GetEnvironmentVariable("IRONPDF_LICENSE_KEY");
if (!string.IsNullOrWhiteSpace(key)) License.LicenseKey = key; // otherwise trial mode (watermark)

if (OperatingSystem.IsLinux())
{
    Installation.LinuxAndDockerDependenciesAutoConfig = true; // installs missing OS packages on first run
    Installation.ChromeGpuMode = IronPdf.Engines.Chrome.ChromeGpuModes.Disabled;
}

ChromePdfRenderer? renderer = null;

return await PocRunner.RunAsync("IronPDF", "ironpdf.pdf", async (invoice, path) =>
{
    renderer ??= new ChromePdfRenderer
    {
        RenderingOptions =
        {
            PaperSize = PdfPaperSize.A4,
            CssMediaType = PdfCssMediaType.Print,
            PrintHtmlBackgrounds = true,
            // Let the stylesheet's @page rule control the margins, footer and page numbers.
            MarginTop = 0, MarginBottom = 0, MarginLeft = 0, MarginRight = 0,
        },
    };

    var html = await InvoiceHtml.RenderAsync(invoice);
    using var pdf = await renderer.RenderHtmlAsPdfAsync(html);
    pdf.MetaData.Title = $"Invoice {invoice.Number}";
    pdf.MetaData.Author = invoice.Supplier.Name;
    pdf.SaveAs(path);
});
