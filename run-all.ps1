<#
  Builds the solution and runs every POC. PDFs are written to .\output\ and timings to output\results.csv.
  Examples:
    .\run-all.ps1
    .\run-all.ps1 -Only QuestPdf,MigraDoc
    .\run-all.ps1 -Stress        # the layout stress test (see STRESS-TEST.md)
#>
param([string[]] $Only, [switch] $Stress)

$pocs = 'QuestPdf', 'MigraDoc', 'IText', 'Syncfusion', 'Aspose', 'Telerik', 'Playwright', 'PuppeteerSharp', 'Gotenberg', 'IronPdf'
if ($Stress) { $pocs = 'QuestPdf', 'MigraDoc', 'IText', 'Syncfusion', 'Aspose', 'Telerik', 'Playwright' }
if ($Only) { $pocs = $pocs | Where-Object { $Only -contains $_ } }
$runArgs = if ($Stress) { @('--', 'stress') } else { @() }

dotnet build "$PSScriptRoot\InvoicePdfPoc.sln" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

foreach ($poc in $pocs) {
    Write-Host "`n=== $poc" -ForegroundColor Cyan
    dotnet run --project "$PSScriptRoot\src\Poc.$poc" -c Release --no-build @runArgs
    if ($LASTEXITCODE -ne 0) { Write-Warning "$poc failed (exit code $LASTEXITCODE). Gotenberg needs Docker; IronPDF needs a key - see README.md." }
}

$results = Join-Path $PSScriptRoot 'output\results.csv'
if (Test-Path $results) { Import-Csv $results | Format-Table library, file, bytes, first_ms, warm_ms }
