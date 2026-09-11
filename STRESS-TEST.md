# Layout stress test

A second document, a 4-page statement of account, built to hit the layouts that are usually awkward. Each engine
renders identical content (`src/Invoice.Shared/Stress/StressData.cs`), and `output/stress-comparison.png` shows
every page side by side.

Run it with `.\run-all.ps1 -Stress`, or run one library with
`dotnet run --project src\Poc.QuestPdf -c Release -- stress`.

The Chromium column covers Playwright, PuppeteerSharp and Gotenberg, which render the same HTML with the same
engine. IronPDF also uses Chromium but couldn't be run without a key.

## Results

| Test | QuestPDF | PDFsharp + MigraDoc | iText | Syncfusion | Aspose.PDF | Telerik | Chromium |
|---|---|---|---|---|---|---|---|
| Several custom fonts | Native | Native (1) | Native | Native | Native | Native (1) | Native |
| Missing glyph falls back (✓) | Native | Manual (2) | Native | Manual (2) | **Crashed** (3) | **Silently dropped** | Native |
| Right-to-left Arabic | Native | **No**: reversed, unjoined | **No** (4) | Font-dependent (5) | Native | **Wrong**: letters lost | Native |
| SVG logo with gradient | Native | No, PNG used | Native | No, PNG used | Native | **Crashed**, PNG used | Native |
| Photo scaled to width | Native | Native | Native | Manual maths | Manual maths (6) | Manual size | Native |
| Transparent PNGs in table cells | Native | Native | Native | Native | Native | Extra packages (7) | Native |
| Header skipped on page 1 | Native | Native | Page loop | Page loop | Page loop | Native | Native |
| Different footer on last page | Native | Post-process | Page loop | Page loop | Page loop | Post-process | **No** (8) |
| Page X of Y | Native | Native | Page loop | Page loop | Page loop | Native | Native |
| Table header repeats on each page | Native | Native | Native | Native | Native | **No** | Native |
| Table rows never split | Native | Native | Native | Native | Native | Native | Native |
| Two-column flowing text | Native (9) | No: split by hand | Native (10) | Manual (11) | Native, fixed height | No: split by hand | Native |
| Keep blocks and headings together | Native | Native | Native | Manual measuring | Workaround (12) | Native | Native |
| Output | 4 pages, 142 KB | 4 pages, 171 KB | 5 pages, 160 KB | 4 pages, 150 KB | 4 pages, 1,165 KB | 4 pages, 509 KB | 4 pages, 262 KB |
| Warm render time | 28 ms | 279 ms | 449 ms | 123 ms | 1,850 ms | 192 ms | 1,171 ms |

**Key.** *Native*: one property or element does it. *Page loop*: lay the document out, then loop over the
finished pages drawing the header or footer; easy, because the page count is known by then. *Post-process*: the
layout engine can't do it, so the finished PDF is stamped with a second API. *Manual*: you write the layout logic
yourself. Timings are single runs on the same single-core sandbox, so compare them against each other only.

1. PDFsharp's cross-platform build needs an `IFontResolver`, and Telerik on .NET Core needs a `FontsProviderBase`.
   Each is a one-off class that hands over font bytes.
2. There's no per-glyph fallback. The ✓ must be set in a font that contains it: MigraDoc uses a separate text
   run, and Syncfusion switches the whole cell, because a grid cell has one font.
3. `ProcessParagraphs()` threw a `NullReferenceException` because ✓ isn't in Lato. **One unexpected character in
   customer data can stop document generation**, so check that your fonts cover your data.
4. Arabic shaping in iText needs the commercial pdfCalligraph add-on. Without it, the letters come out in
   reverse order and unjoined.
5. With DejaVu Sans the Arabic was shaped correctly. With Amiri, whose OpenType rules are complex, only two
   glyphs rendered. Test with the fonts you'll actually use.
6. Setting only `FixWidth` stretched the photo; you must calculate `FixHeight` from the aspect ratio.
7. On .NET Core, Telerik can't decode images without `Telerik.Documents.ImageUtils` (SkiaSharp), plus
   `SkiaSharp.NativeAssets.Linux` on Linux. Without them it throws "Not supported image format" and leaves a
   corrupt partial file.
8. CSS has `:first` but no `:last` page selector, and Chromium doesn't support `:nth()`. "End of statement" is
   therefore placed in the normal flow; putting it in the footer would mean stamping the PDF afterwards.
9. Needs `BalanceHeight()`, otherwise column one fills first, and `EnsureSpace()` to stop the heading being
   orphaned.
10. `MulticolContainer` throws unless it has exactly one block child. The keep-together box after it also
    moved to a new page despite room remaining on page 4 (observed; cause not investigated).
11. You draw into the first column with a one-page layout and continue the returned `Remainder` in the second.
    With the default layout, the overflow silently paginates onto a new page instead.
12. `IsBroken = false` truncates rather than keeps together, so use a single row with `IsRowBroken = false`.

## What this means in practice

- **QuestPDF and Chromium passed nearly everything natively.** Chromium's one gap is the different last-page
  footer. Everyone else needed workarounds somewhere.
- **The most dangerous failures were in Aspose and Telerik**, and they're not about layout. Aspose crashed on a
  character missing from the font. Telerik silently dropped it, and its Arabic looked plausible but was missing
  letters and couldn't be searched or copied. If customer names or addresses can contain non-Latin text, test
  with real data.
- **Right-to-left text is a clear dividing line.** QuestPDF, Aspose and Chromium got it right, and Syncfusion
  did with a compatible font. iText (without its paid add-on), MigraDoc and Telerik produced incorrect text.
- **Page-aware headers and footers** are easy where you can loop over finished pages (iText, Syncfusion, Aspose)
  or where the engine exposes the page count (QuestPDF). They're awkward in Word-style models (MigraDoc,
  Telerik) and in CSS, which all need a second pass over the PDF.
- **Syncfusion can do almost anything, but you write the layout engine yourself.** That means measuring text,
  deciding page breaks and positioning columns; its grid and text-overflow helpers are good building blocks.
