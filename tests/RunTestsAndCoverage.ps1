# --------------------------------------------------------------------------
#          Corsinvest.LanguageExt - Test & Coverage Script
# --------------------------------------------------------------------------
# This script runs all unit tests, collects code coverage information,
# and displays a summary report.
# --------------------------------------------------------------------------

$ErrorActionPreference = 'Stop' # Exit script on any error

# Change to solution root (parent of tests directory)
$solutionRoot = Split-Path -Parent $PSScriptRoot
Set-Location $solutionRoot

# --- Main Execution ---

# Header for "Running Unit Tests"
$headerTitle = "Running Unit Tests with Coverage"
$headerColor = "Cyan"
$headerLine = '─' * ($headerTitle.Length + 4)
Write-Host "`n$headerLine" -ForegroundColor $headerColor
Write-Host "  $headerTitle  " -ForegroundColor $headerColor
Write-Host "$headerLine`n" -ForegroundColor $headerColor

# Run tests, collecting coverage data in Cobertura format.
dotnet test --collect:"XPlat Code Coverage" --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Tests failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# --- Coverage Analysis ---

# Coverage files are created in each test project's TestResults folder
$coverageFiles = Get-ChildItem -Path "$PSScriptRoot" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue

if (-not $coverageFiles) {
    # Fallback: search from solution root
    $coverageFiles = Get-ChildItem -Path $solutionRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
}

if (-not $coverageFiles) {
    # Header for "Coverage Summary" (Error case)
    $headerTitle = "Coverage Summary"
    $headerColor = "Yellow"
    $headerLine = '─' * ($headerTitle.Length + 4)
    Write-Host "`n$headerLine" -ForegroundColor $headerColor
    Write-Host "  $headerTitle  " -ForegroundColor $headerColor
    Write-Host "$headerLine`n" -ForegroundColor $headerColor

    Write-Host "❌ WARNING: Coverage file not found." -ForegroundColor Red
    Write-Host "`nTo enable code coverage, add coverlet.collector to your test projects:" -ForegroundColor Yellow
    Write-Host "  dotnet add package coverlet.collector`n" -ForegroundColor Gray
    exit 0
}

# --- Display Coverage Summary per Project ---

# Header for "Coverage Summary"
$headerTitle = "Coverage Summary"
$headerColor = "Yellow"
$headerLine = '─' * ($headerTitle.Length + 4)
Write-Host "`n$headerLine" -ForegroundColor $headerColor
Write-Host "  $headerTitle  " -ForegroundColor $headerColor
Write-Host "$headerLine`n" -ForegroundColor $headerColor

# Helper function to display progress bar
function Show-ProgressBar {
    param($label, $percentage, $covered, $total)

    $coverageText = "$percentage% ($covered / $total)"
    Write-Host "  $($label.PadRight(16)): $($coverageText.PadRight(20))" -NoNewline

    $totalWidth = 30
    $filledBlocks = [math]::Round($percentage / 100 * $totalWidth)
    $emptyBlocks = $totalWidth - $filledBlocks
    $bar = '█' * $filledBlocks + '░' * $emptyBlocks
    $barColor = if ($percentage -ge 85) { "Green" } elseif ($percentage -ge 70) { "Yellow" } else { "Red" }
    Write-Host "[$bar]" -ForegroundColor $barColor
}

# Group coverage files by project (to avoid duplicates)
$projectCoverage = @{}
foreach ($coverageFile in $coverageFiles) {
    $projectName = $coverageFile.Directory.Parent.Parent.Name
    if (-not $projectCoverage.ContainsKey($projectName)) {
        $projectCoverage[$projectName] = $coverageFile
    }
}

# Process each unique project
foreach ($projectName in $projectCoverage.Keys | Sort-Object) {
    $coverageFile = $projectCoverage[$projectName]
    [xml]$xml = Get-Content $coverageFile.FullName

    # Calculate Line Coverage
    $lines = $xml.coverage.packages.package.classes.class.lines.line
    $totalLines = $lines.Count
    $coveredLines = ($lines | Where-Object { $_.hits -gt 0 }).Count
    $lineCoverage = if ($totalLines -gt 0) { [math]::Round(($coveredLines / $totalLines) * 100, 2) } else { 0 }

    # Calculate Branch Coverage
    $totalBranches = 0
    $coveredBranches = 0
    $xml.coverage.packages.package.classes.class.lines.line | ForEach-Object {
        if ($_.branch -eq 'true' -and $_.'condition-coverage') {
            $conditionCoverage = $_.'condition-coverage'
            if ($conditionCoverage -match '\(\s*(\d+)\s*/\s*(\d+)\s*\)') {
                $coveredBranches += [int]$matches[1]
                $totalBranches += [int]$matches[2]
            }
        }
    }
    $branchCoverage = if ($totalBranches -gt 0) { [math]::Round(($coveredBranches / $totalBranches) * 100, 2) } else { 0 }

    # Calculate Method Coverage
    $methods = $xml.coverage.packages.package.classes.class.methods.method
    $totalMethods = $methods.Count
    $coveredMethods = ($methods | Where-Object { ([decimal]$_.'line-rate') -gt 0 }).Count
    $methodCoverage = if ($totalMethods -gt 0) { [math]::Round(($coveredMethods / $totalMethods) * 100, 2) } else { 0 }

    # Display project header
    Write-Host "`n📦 $projectName" -ForegroundColor Cyan
    Write-Host ("─" * 80) -ForegroundColor DarkGray

    # Display metrics
    Show-ProgressBar "Line Coverage" $lineCoverage $coveredLines $totalLines
    Show-ProgressBar "Branch Coverage" $branchCoverage $coveredBranches $totalBranches
    Show-ProgressBar "Method Coverage" $methodCoverage $coveredMethods $totalMethods
}


# --- Cleanup (Silent) ---
# Remove TestResults from each test project directory
Get-ChildItem -Path "$PSScriptRoot" -Directory | ForEach-Object {
    $testResultsPath = Join-Path $_.FullName "TestResults"
    if (Test-Path $testResultsPath) {
        Remove-Item $testResultsPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n✅ Tests and coverage analysis completed successfully!`n" -ForegroundColor Green
