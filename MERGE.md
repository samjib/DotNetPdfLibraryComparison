# Appending an existing PDF (terms and conditions)

Most of these libraries can append an existing PDF, but it is a **separate capability from generation** and two
of them can't do it at all. This tests appending a third-party T&Cs PDF to the generated invoice.

Run it with `.\run-all.ps1 -Merge`, or one library with
`dotnet run --project src\Poc.QuestPdf -c Release -- merge`.

The file being appended (`assets/terms-and-conditions.pdf`) is deliberately awkward, so the merge has something
to get wrong: **3 pages of US Letter** (the invoice is A4), its own embedded fonts, **8 bookmarks**, an external
**hyperlink**, and its own "page 1 of 3" footers. Regenerate it with
`dotnet run --project src\Poc.IText -c Release -- make-terms`.

## Results

| Library | API | Bookmarks kept | Link kept | Page sizes | Warm time | Size |
|---|---|---|---|---|---|---|
| QuestPDF | `DocumentOperation.LoadFile(a).MergeFile(b).Save(c)` | **No** (0 of 8) | Yes | Both kept | 16 ms | 86 KB |
| PDFsharp | `PdfReader.Open(path, Import)` + `AddPage` | **No** (0 of 8) | Yes | Both kept | 23 ms | 88 KB |
| iText | `PdfMerger` with `SetMergeOutlines(true)` | Yes (8 of 8) | Yes | Both kept | 88 ms | 81 KB |
| Syncfusion | `PdfDocumentBase.Merge(target, sources)` | Yes (8 of 8) | Yes | Both kept | 62 ms | 82 KB |
| Aspose.PDF | `doc.Pages.Add(other.Pages)` | not tested (1) | – | – | – | – |
| Telerik | `provider.Import(...)` + `doc.Merge(...)` (2) | Yes (8 of 8) | Yes | Both kept | 133 ms | 370 KB |
| Gotenberg | `POST /forms/pdfengines/merge` | **No** (0 of 8) | Yes | Both kept | 2.5 s (3) | 82 KB |
| Playwright / PuppeteerSharp | **None** | – | – | – | – | – |
| IronPDF | `PdfDocument.Merge(a, b)` | not run (no key) | – | – | – | – |

Every merge produced the same 5 pages (2 × A4 then 3 × US Letter) with both documents' text intact, and every
one kept the hyperlink with its URL working.

1. The Aspose call is correct, but unlicensed evaluation mode throws `IndexOutOfRangeException` ("At most 4
   elements ... in evaluation mode") once the result exceeds 4 pages. I confirmed the code works by merging
   fewer pages: 4 pages succeeded, 5 failed.
2. Moving pages between Telerik documents throws "The document element is associated with another parent".
   `RadFixedDocument.Merge()` copies them properly and also resolves clashing form-field and attachment names.
3. Gotenberg ran against the public demo over the internet, so most of that is network time. A local container
   will be far quicker.

## Things to watch

**Page numbers go stale.** This catches people out. The invoice footer still says "Page 1 of 2" because it was
painted before the merge, while the file now has 5 pages, and the appended terms carry their own "page 1 of 3".
There's no way round it at merge time: either scope the wording to each document ("Page 1 of 2 of this
invoice"), or leave numbers out when generating and stamp them across the merged file afterwards. The PDFsharp
sample (`src/Poc.MigraDoc/Merge.cs`) does the stamping, drawn bottom-left so you can see both in
`output/merge-comparison.png`.

**Bookmarks are dropped by QuestPDF, PDFsharp and Gotenberg.** For a T&Cs annexe that usually doesn't matter.
If the appended document is a long contract whose navigation pane you care about, use iText, Syncfusion or
Telerik.

**Mixed page sizes survive**, which is correct but can look untidy: a reader shows A4 then Letter, and printers
may scale one of them. If you need a uniform size, ask whoever supplies the terms for A4, or scale the pages
during the copy (iText can draw each imported page onto a new A4 page as a form XObject).

**File sizes are roughly additive.** None of them deduplicate fonts common to both documents. Telerik's 370 KB
comes from the font-embedding issue noted in the main README, not from the merge itself.

**Chromium-only tools can't merge.** Playwright and PuppeteerSharp drive a browser; they have no PDF
manipulation API. If you render with them, pair them with PDFsharp (MIT, about 25 lines) or switch to
Gotenberg, which exposes a merge route. Gotenberg's PDF-engines routes also split, convert to PDF/A and read
metadata.

## If you go with QuestPDF

`DocumentOperation` covers more than merging, and it's the same fluent style as the rest of the library:

```csharp
DocumentOperation
    .LoadFile("invoice.pdf", password: null)   // opens password-protected files too
    .MergeFile("terms.pdf")                    // or MergeFile("terms.pdf", "1,3-5") for page ranges
    .Save("invoice-with-terms.pdf");
```

It can also overlay or underlay content (watermarks, a "COPY" stamp, pre-printed stationery), take page ranges,
attach files and encrypt the result. Note this is separate from `Document.Merge(...)`, which only combines
documents QuestPDF generated in the same run, but does give you continuous page numbers across them via
`UseContinuousPageNumbers()`.
