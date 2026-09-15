# Invoice PDF proof of concepts for .NET 10

Ten console projects that each generate **the same UK VAT invoice** with a different PDF library, so you can
compare output, code style, speed and licensing side by side. All of them share one invoice model, one set of
sample data and one font (Lato, SIL Open Font Licence), so the differences you see come from the libraries.

The sample invoice has 21 lines. It spills onto a second page to test pagination, repeating table headers and
"Page X of Y". It also includes a zero-rated line and a tax point that differs from the invoice date, both of
which HMRC requires you to show.

`output/comparison-page1.png` shows page 1 of every PDF side by side.

**Layout stress test.** A second, deliberately awkward document tests custom fonts, right-to-left Arabic, glyph
fallback, SVG and other images, headers that skip page 1, a different last-page footer, repeating table headers
and two-column text. See [STRESS-TEST.md](STRESS-TEST.md) for the results matrix, and run it with
`.\run-all.ps1 -Stress`.

**Appending an existing PDF.** A third test appends a supplied terms-and-conditions PDF to the generated
invoice, checking bookmarks, links and mixed page sizes. See [MERGE.md](MERGE.md), and run it with
`.\run-all.ps1 -Merge`.

## Results

| Library (version) | Approach | Licence | Size | First doc | Warm | Outcome in this run |
|---|---|---|---|---|---|---|
| QuestPDF 2026.8.0 | Fluent C# layout | Free under US$1M revenue, else paid | 47 KB | 153 ms | 12 ms | Clean |
| PDFsharp + MigraDoc 6.2.4 | Document object model | MIT | 49 KB | 230 ms | 15 ms | Clean |
| iText Core 9.7.0 | Layout API | AGPL or commercial | 41 KB | 1,631 ms | 87 ms | Clean (AGPL noted in PDF metadata) |
| Syncfusion 34.2.7 | Coordinates + `PdfGrid` | Commercial (free community tier) | 41 KB | 1,830 ms | 49 ms | Trial marks (no key) |
| Aspose.PDF 26.8.0 | Document object model | Commercial | 352 KB | 2,261 ms | 281 ms | Evaluation watermark |
| Telerik 2026.3.826 | Word-like "Flow" model → PDF | Commercial | 321 KB | 1,077 ms | 77 ms | Trial banner; header row not repeated |
| Playwright 1.62.0 | Razor HTML → Chromium | Apache-2.0 | 77 KB | 1,320 ms | 416 ms | Clean, tagged PDF |
| PuppeteerSharp 25.10.0 | Razor HTML → Chrome | MIT | 80 KB | 1,370 ms | 336 ms | Clean, tagged PDF |
| Gotenberg 8.36 | Razor HTML → Chromium service over HTTP | MIT | 90 KB | 7,698 ms* | 2,201 ms* | Clean, tagged PDF |
| IronPDF 2026.9.2 | Razor HTML → embedded Chromium | Commercial | – | – | – | Not run: refuses to start without a key here |

**How to read the timings.** They come from a single-core Linux sandbox and are the median of three runs, so
compare them against each other; your machine will be faster. "First doc" is a fresh process, including JIT,
font parsing and (for Chromium) launching the browser. "Warm" is the average of five further documents with the
engine kept alive, which is what a long-running service sees. *Gotenberg was called on its public demo
instance over the internet with a 1.8 MB request, so its numbers are mostly network time; a local container is
far quicker.

## What the POCs turned up

**QuestPDF.** Needed no workarounds: pagination, repeating header rows and page numbers are built in, and the
code reads like the layout. `FontManager` lives in `QuestPDF.Drawing`.

**PDFsharp + MigraDoc.** The cross-platform build needs an `IFontResolver` (on Windows you can instead set
`GlobalFontSettings.UseWindowsFontsUnderWindows = true`). Tables are outdented by the cell padding unless you set
`Rows.LeftIndent`. Cells can't nest tables or have their own padding, so the amount-due panel uses a `MergeDown`
trick.

**iText.** To stamp "Page X of Y" you keep pages open (`immediateFlush: false`) and draw footers once the page
count is known. It needs the `itext.bouncy-castle-adapter` package at runtime, and the AGPL edition writes
"(AGPL version)" into the PDF's Producer field.

**Syncfusion.** Outside `PdfGrid`, you position everything yourself and handle page breaks, so there's more code.
The footer template must be set before pages are added. Without a key it prints red trial text plus a diagonal
watermark.

**Aspose.PDF.** Three gotchas:
- `Table.IsBroken = false` does not mean "keep together"; it silently truncates the table at the page edge
  (the first attempt lost the totals and payment section). Wrapping content in a row with `IsRowBroken = false`
  works.
- Alignment on a nested table is ignored, so the totals use an empty spacer column.
- Files are about seven times larger because the embedded fonts keep their 210 KB kerning (GPOS) table.
  `FontUtilities.SubsetFonts` threw before saving and made the file bigger after reloading in evaluation mode.
  Worth re-testing with a licence.

**Telerik.** The 2026 packages moved to `Telerik.Documents.*` namespaces, so older samples won't compile. On
.NET Core you must supply fonts through a `FontsProviderBase`, and Word's default cell margins apply unless you
zero cell padding. `TableRow.RepeatOnEveryPage` was **not honoured by the PDF export**; I reproduced this with a
minimal 80-row table. `FontEmbeddingType.Subset` still kept the kerning table (321 KB).

**Chromium engines (Playwright, PuppeteerSharp, Gotenberg, IronPDF).** All four share one Razor component
(`src/Invoice.Shared/Html`) rendered with .NET's `HtmlRenderer`, plus one print stylesheet. CSS paged-media
margin boxes (`@page { @bottom-right { ... } }`) produce the footer and "Page X of Y" with no engine-specific
header templates. Tagged (accessible) PDF is a single flag. I hit a CSS specificity bug that left-aligned the
numeric headers, so check print output as you would a web page.
- **Playwright** downloads a pinned Chrome Headless Shell (about 115 MB) on first run. Linux servers also need
  its `install-deps` step.
- **PuppeteerSharp** downloads a pinned Chrome for Testing into your local app data folder. `--no-sandbox` is
  only added when running as root in a Linux container.
- **Gotenberg** adds no PDF package to your app. The HTML must be uploaded as `index.html`, and large assets are
  better sent as separate files than inlined. Don't send real invoices to the public demo.
- **IronPDF** threw "Production License Required" in this Linux container, whether as a Release or Debug build
  and with `DOTNET_ENVIRONMENT=Development`. Its message offers a 7-day development grace, which may apply on a
  Windows developer machine; otherwise get a free trial key from ironpdf.com.

**Font size finding.** Aspose and Telerik keep each font's GPOS (kerning) table when subsetting, while the other
libraries strip it. With a large modern font like Lato 2.0, that is the whole reason for their bigger files.

## Running it on Windows

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Docker Desktop is only needed for Gotenberg.

```powershell
cd InvoicePdfPoc
.\run-all.ps1                            # builds, runs every POC, PDFs go to .\output
.\run-all.ps1 -Only QuestPdf,MigraDoc    # just some of them
dotnet run --project src\Poc.QuestPdf -c Release
```

The commercial libraries run in trial mode without keys. To test them licensed, set these first (never commit
keys):

```powershell
$env:SYNCFUSION_LICENSE_KEY = "your-key"               # free community key if you're eligible
$env:ASPOSE_LICENSE_PATH    = "C:\keys\Aspose.PDF.NET.lic"
$env:IRONPDF_LICENSE_KEY    = "your-trial-key"
# Telerik: put telerik-license.txt in the solution folder (the build lists every location it checks)
docker run --rm -p 3000:3000 gotenberg/gotenberg:8     # then run Poc.Gotenberg (or set $env:GOTENBERG_URL)
```

`dotnet list package --vulnerable --include-transitive` reported no known vulnerable packages in any project
(September 2026), and NuGet audit will warn on restore if that changes.

On Linux or macOS use `./run-all.sh`. For Playwright on Linux, also run once as root:
`pwsh src/Poc.Playwright/bin/Release/net10.0/playwright.ps1 install-deps chromium`.

## Layout

```
InvoicePdfPoc.sln
src/Invoice.Shared/        model, UK sample data, formatting, brand tokens, fonts, timing runner
  Html/                    InvoiceTemplate.razor + invoice.css (used by the Chromium engines)
src/Poc.QuestPdf/          one Program.cs per library
src/Poc.MigraDoc/  src/Poc.IText/  src/Poc.Syncfusion/  src/Poc.Aspose/  src/Poc.Telerik/
src/Poc.Playwright/  src/Poc.PuppeteerSharp/  src/Poc.Gotenberg/  src/Poc.IronPdf/
output/                    generated PDFs, results.csv, comparison-page1.png
```

## About the invoice

The layout covers the fields HMRC expects on a full VAT invoice:
- supplier name, address and VAT number;
- invoice number, invoice date and tax point;
- customer name and address;
- description, quantity and unit price per line, plus the VAT rate per line;
- totals per VAT rate and the total due.

All data is fictional (the IBAN is the standard documentation example).

From 1 April 2029, UK B2B and B2G VAT invoices must be structured e-invoices, so a PDF alone won't be enough.
Because every POC renders from the same `Invoice` model, adding a structured output (likely Peppol UBL) later is
another serialiser rather than a rewrite. If you bill EU customers, QuestPDF can already produce PDF/A-3b with
an embedded XML attachment for ZUGFeRD/Factur-X.
