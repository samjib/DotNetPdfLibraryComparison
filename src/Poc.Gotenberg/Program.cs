using System.Text;
using InvoicePoc;
using InvoicePoc.Html;

// Gotenberg: Chromium (and LibreOffice) packaged as a Docker service with an HTTP API, so the
// browser runs outside your application process. Start one locally with:
//     docker run --rm -p 3000:3000 gotenberg/gotenberg:8
// Point elsewhere with the GOTENBERG_URL environment variable.
var baseUrl = Environment.GetEnvironmentVariable("GOTENBERG_URL") ?? "http://localhost:3000";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(60) };

if (args.Contains("merge"))
    return await PocRunner.RunAsync("Gotenberg (merge)", "merged-gotenberg.pdf",
        (invoice, path) => MergeDocuments.GenerateAsync(http, invoice, path), warmRuns: 2);

return await PocRunner.RunAsync("Gotenberg", "gotenberg.pdf", async (invoice, path) =>
{
    var html = await InvoiceHtml.RenderAsync(invoice);

    using var form = new MultipartFormDataContent
    {
        // The main document must be uploaded as a file called index.html. Extra assets (images,
        // fonts, CSS) can be uploaded alongside it and referenced by file name.
        { new StringContent(html, Encoding.UTF8, "text/html"), "files", "index.html" },
        { new StringContent("true"), "preferCssPageSize" },
        { new StringContent("true"), "printBackground" },
        { new StringContent("true"), "generateTaggedPdf" },
    };

    using var response = await http.PostAsync("/forms/chromium/convert/html", form);
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"Gotenberg returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    await using var file = File.Create(path);
    await response.Content.CopyToAsync(file);
});
