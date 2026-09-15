using System.Net.Http.Headers;
using InvoicePoc;
using InvoicePoc.Html;

/// <summary>Append an existing T&amp;Cs PDF to the generated invoice (Gotenberg's PDF engines route).</summary>
static class MergeDocuments
{
    public static async Task GenerateAsync(HttpClient http, Invoice invoice, string path)
    {
        // 1) Render the invoice HTML to PDF as usual.
        using var htmlForm = new MultipartFormDataContent
        {
            { new StringContent(await InvoiceHtml.RenderAsync(invoice), System.Text.Encoding.UTF8, "text/html"), "files", "index.html" },
            { new StringContent("true"), "preferCssPageSize" },
            { new StringContent("true"), "printBackground" },
        };
        using var rendered = await http.PostAsync("/forms/chromium/convert/html", htmlForm);
        rendered.EnsureSuccessStatusCode();
        var invoiceBytes = await rendered.Content.ReadAsByteArrayAsync();

        // 2) Merge it with the terms. Files are merged in alphabetical order of the file name,
        //    so they are named 1-... and 2-... to force the order.
        using var mergeForm = new MultipartFormDataContent();
        void AddPdf(byte[] bytes, string name)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            mergeForm.Add(part, "files", name);
        }
        AddPdf(invoiceBytes, "1-invoice.pdf");
        AddPdf(await File.ReadAllBytesAsync(MergeSupport.TermsPath), "2-terms.pdf");

        using var merged = await http.PostAsync("/forms/pdfengines/merge", mergeForm);
        if (!merged.IsSuccessStatusCode)
            throw new HttpRequestException($"Gotenberg returned {(int)merged.StatusCode}: {await merged.Content.ReadAsStringAsync()}");

        await using var file = File.Create(path);
        await merged.Content.CopyToAsync(file);
    }
}
