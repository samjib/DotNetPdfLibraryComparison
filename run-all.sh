#!/usr/bin/env bash
# Builds the solution and runs every POC. PDFs go to ./output and timings to output/results.csv.
# Usage: ./run-all.sh   |   ./run-all.sh QuestPdf MigraDoc   |   ./run-all.sh --stress   (see STRESS-TEST.md)
set -u
cd "$(dirname "$0")"
extra=()
if [ "${1:-}" = "--stress" ]; then shift; extra=(-- stress); [ $# -eq 0 ] && set -- QuestPdf MigraDoc IText Syncfusion Aspose Telerik Playwright; fi
pocs=("$@")
[ ${#pocs[@]} -eq 0 ] && pocs=(QuestPdf MigraDoc IText Syncfusion Aspose Telerik Playwright PuppeteerSharp Gotenberg IronPdf)

dotnet build InvoicePdfPoc.sln -c Release || exit 1
for poc in "${pocs[@]}"; do
  echo; echo "=== $poc"
  dotnet run --project "src/Poc.$poc" -c Release --no-build "${extra[@]}" || echo "WARNING: $poc failed (Gotenberg needs Docker; IronPDF needs a key - see README.md)"
done
[ -f output/results.csv ] && cat output/results.csv
