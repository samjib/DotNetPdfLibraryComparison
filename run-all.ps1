<#
  Builds the solution and runs every POC. PDFs are written to .\output\ and timings to output\results.csv.
  Examples:
    .\run-all.ps1
    .\run-all.ps1 -Only QuestPdf,MigraDoc
    .\run-all.ps1 -Stress        # the layout stress test (see STRESS-TEST.md)
    .\run-all.ps1 -Merge         # append an existing T&Cs PDF (see MERGE.md)
#>
param([string[]] $Only, [switch] $Stress, [switch] $Merge)

$pocs = 'QuestPdf', 'MigraDoc', 'IText', 'Syncfusion', 'Aspose', 'Telerik', 'Playwright', 'PuppeteerSharp', 'Gotenberg', 'IronPdf'
if ($Stress) { $pocs = 'QuestPdf', 'MigraDoc', 'IText', 'Syncfusion', 'Aspose', 'Telerik', 'Playwright' }
if ($Merge)  { $pocs = 'QuestPdf', 'MigraDoc', 'IText', 'Syncfusion', 'Aspose', 'Telerik', 'Gotenberg' }
if ($Only) { $pocs = $pocs | Where-Object { $Only -contains $_ } }
$runArgs = if ($Stress) { @('--', 'stress') } elseif ($Merge) { @('--', 'merge') } else { @() }

if ($Merge -and -not (Test-Path "$PSScriptRoot\assets\terms-and-conditions.pdf")) {
    dotnet run --project "$PSScriptRoot\src\Poc.IText" -c Release -- make-terms
}

dotnet build "$PSScriptRoot\InvoicePdfPoc.sln" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

foreach ($poc in $pocs) {
    Write-Host "`n=== $poc" -ForegroundColor Cyan
    dotnet run --project "$PSScriptRoot\src\Poc.$poc" -c Release --no-build @runArgs
    if ($LASTEXITCODE -ne 0) { Write-Warning "$poc failed (exit code $LASTEXITCODE). Gotenberg needs Docker; IronPDF needs a key - see README.md." }
}

$results = Join-Path $PSScriptRoot 'output\results.csv'
if (Test-Path $results) { Import-Csv $results | Format-Table library, file, bytes, first_ms, warm_ms }
