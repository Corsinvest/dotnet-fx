# --------------------------------------------------------------------------
#       Corsinvest.Fx - Comprehensive Test & Coverage Report
# --------------------------------------------------------------------------
# This script runs all unit tests, collects code coverage, and displays
# a detailed summary report with test results and coverage metrics.
# --------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'

# Change to solution root
$solutionRoot = Split-Path -Parent $PSScriptRoot
Set-Location $solutionRoot

# --- Helper Functions ---

function Write-Header {
    param($title, $color = "Cyan")
    $line = '═' * ($title.Length + 4)
    Write-Host "`n$line" -ForegroundColor $color
    Write-Host "  $title  " -ForegroundColor $color
    Write-Host "$line`n" -ForegroundColor $color
}

function Get-CoverageEvaluation {
    param($percentage)

    if ($percentage -eq 0) { return "❌ NONE" }
    if ($percentage -eq 100) { return "✅ PERFECT 🏆" }
    if ($percentage -ge 95) { return "✅ EXCELLENT" }
    if ($percentage -ge 85) { return "✅ VERY GOOD" }
    if ($percentage -ge 70) { return "✅ GOOD" }
    if ($percentage -ge 50) { return "⚠️ SUFFICIENT" }
    return "⚠️ LOW"
}

function Get-TestStatus {
    param($total, $passed, $failed, $skipped)

    if ($total -eq 0) { return "❌ NO TESTS" }
    if ($failed -gt 0) { return "❌ FAILED" }
    if ($skipped -gt 0) { return "⚠️ WITH SKIP" }
    if ($passed -eq $total) {
        if ($passed -ge 200) { return "✅ EXCELLENT" }
        if ($passed -ge 50) { return "✅ GREAT" }
        if ($passed -ge 10) { return "✅ GOOD" }
        return "✅ OK"
    }
    return "⚠️ PARTIAL"
}

# --- Auto-discover Projects ---

# Find all source projects (excluding test and generator projects)
$srcDir = Join-Path $solutionRoot "src"
$allProjects = @()

if (Test-Path $srcDir) {
    Get-ChildItem -Path $srcDir -Directory | ForEach-Object {
        $projectName = $_.Name
        # Exclude generator projects
        if ($projectName -notmatch '\.Generators$') {
            $allProjects += $projectName
        }
    }
}

# Sort projects alphabetically
$allProjects = $allProjects | Sort-Object

# --- Run Tests ---

Write-Header "📊 Complete Report: Tests & Code Coverage" "Cyan"

Write-Host "🔄 Running tests with code coverage..." -ForegroundColor Yellow
Write-Host ""

# Run tests with coverage
$testOutput = dotnet test --collect:"XPlat Code Coverage" --verbosity minimal 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Tests failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# --- Parse Test Results ---

Write-Header "✅ Test Execution Results" "Green"

$testResults = @()
$totalTests = 0
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0

# Parse test output (handle both English and Italian output)
$testOutput | ForEach-Object {
    # English: "Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9"
    # Italian: "Superato!     - Non superati:     0. Superati:     9. Ignorati:     0. Totale:     9"
    if ($_ -match '(Passed!|Superato!)\s+-\s+(Non superati|Failed):\s+(\d+)\.?\s+(Superati|Passed):\s+(\d+)\.?\s+(Ignorati|Skipped):\s+(\d+)\.?\s+(Totale|Total):\s+(\d+).*?-\s+(.+?\.dll)') {
        $failed = [int]$matches[3]
        $passed = [int]$matches[5]
        $skipped = [int]$matches[7]
        $total = [int]$matches[9]
        $dllName = [System.IO.Path]::GetFileNameWithoutExtension($matches[10])

        $projectName = $dllName -replace '\.Tests$', ''

        $testResults += [PSCustomObject]@{
            Project = $projectName
            Total = $total
            Passed = $passed
            Failed = $failed
            Skipped = $skipped
            Status = Get-TestStatus $total $passed $failed $skipped
        }

        $totalTests += $total
        $totalPassed += $passed
        $totalFailed += $failed
        $totalSkipped += $skipped
    }
}

# Add projects without tests
foreach ($proj in $allProjects) {
    if (-not ($testResults | Where-Object { $_.Project -eq $proj })) {
        $testResults += [PSCustomObject]@{
            Project = $proj
            Total = 0
            Passed = 0
            Failed = 0
            Skipped = 0
            Status = "❌ NO TESTS"
        }
    }
}

# Sort test results
$testResults = $testResults | Sort-Object Project

# Calculate column widths
$maxProjectLen = ($testResults.Project | Measure-Object -Property Length -Maximum).Maximum
$maxProjectLen = [Math]::Max($maxProjectLen, 30)

# Print header
$headerFormat = "{0,-$maxProjectLen} {1,15} {2,12} {3,12} {4,12} {5,20}"
Write-Host ($headerFormat -f "Project", "Tests Run", "✅ Passed", "❌ Failed", "⏭️ Skipped", "Status") -ForegroundColor White
Write-Host ("─" * ($maxProjectLen + 15 + 12 + 12 + 12 + 20 + 8)) -ForegroundColor DarkGray

# Print rows
$rowFormat = "{0,-$maxProjectLen} {1,15} {2,12} {3,12} {4,12} {5,20}"
foreach ($result in $testResults) {
    $color = if ($result.Failed -gt 0) { "Red" }
             elseif ($result.Total -eq 0) { "DarkGray" }
             elseif ($result.Skipped -gt 0) { "Yellow" }
             else { "White" }

    Write-Host ($rowFormat -f $result.Project, $result.Total, $result.Passed, $result.Failed, $result.Skipped, $result.Status) -ForegroundColor $color
}

# Print total
Write-Host ("─" * ($maxProjectLen + 15 + 12 + 12 + 12 + 20 + 8)) -ForegroundColor DarkGray
$totalStatus = if ($totalFailed -eq 0 -and $totalTests -gt 0) { "✅ 100% Success" }
               elseif ($totalTests -eq 0) { "❌ NO TESTS" }
               else { "❌ FAILURES" }
Write-Host ($rowFormat -f "TOTAL", $totalTests, $totalPassed, $totalFailed, $totalSkipped, $totalStatus) -ForegroundColor Cyan

# --- Parse Coverage Results ---

Write-Host ""
Write-Header "📈 Detailed Code Coverage" "Yellow"

$coverageFiles = Get-ChildItem -Path "$PSScriptRoot" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue

if (-not $coverageFiles) {
    Write-Host "⚠️ No coverage files found" -ForegroundColor Yellow
    exit 0
}

# Group coverage files by project
$projectCoverage = @{}
foreach ($coverageFile in $coverageFiles) {
    $testProjectName = $coverageFile.Directory.Parent.Parent.Name
    # Extract source project name (remove .Tests suffix)
    $sourceProjectName = $testProjectName -replace '\.Tests$', ''

    if (-not $projectCoverage.ContainsKey($sourceProjectName)) {
        $projectCoverage[$sourceProjectName] = $coverageFile
    }
}

# Collect coverage data
$coverageResults = @()

foreach ($projectName in $allProjects) {
    if ($projectCoverage.ContainsKey($projectName)) {
        $coverageFile = $projectCoverage[$projectName]
        [xml]$xml = Get-Content $coverageFile.FullName

        # Parse coverage from XML attributes
        $lineRate = [decimal]$xml.coverage.'line-rate'
        $branchRate = [decimal]$xml.coverage.'branch-rate'
        $linesCovered = [int]$xml.coverage.'lines-covered'
        $linesValid = [int]$xml.coverage.'lines-valid'
        $branchesCovered = [int]$xml.coverage.'branches-covered'
        $branchesValid = [int]$xml.coverage.'branches-valid'

        $lineCoveragePercent = [math]::Round($lineRate * 100, 2)
        $branchCoveragePercent = if ($branchesValid -gt 0) { [math]::Round($branchRate * 100, 2) } else { 0 }

        $coverageResults += [PSCustomObject]@{
            Project = $projectName
            LinePercent = $lineCoveragePercent
            BranchPercent = $branchCoveragePercent
            LinesCovered = $linesCovered
            LinesValid = $linesValid
            BranchesCovered = $branchesCovered
            BranchesValid = $branchesValid
            Evaluation = Get-CoverageEvaluation $lineCoveragePercent
        }
    } else {
        # No coverage data
        $coverageResults += [PSCustomObject]@{
            Project = $projectName
            LinePercent = 0
            BranchPercent = 0
            LinesCovered = 0
            LinesValid = 0
            BranchesCovered = 0
            BranchesValid = 0
            Evaluation = "❌ NONE"
        }
    }
}

# Sort coverage results
$coverageResults = $coverageResults | Sort-Object Project

# Print coverage table header
$covHeaderFormat = "{0,-$maxProjectLen} {1,15} {2,17} {3,16} {4,16} {5,20}"
Write-Host ($covHeaderFormat -f "Project", "Line Coverage", "Branch Coverage", "Lines Covered", "Branches Covered", "Evaluation") -ForegroundColor White
Write-Host ("─" * ($maxProjectLen + 15 + 17 + 16 + 16 + 20 + 10)) -ForegroundColor DarkGray

# Print coverage rows
$covRowFormat = "{0,-$maxProjectLen} {1,15} {2,17} {3,16} {4,16} {5,20}"
foreach ($cov in $coverageResults) {
    $lineCovText = if ($cov.LinePercent -eq 100) { "$($cov.LinePercent)% 🏆" } else { "$($cov.LinePercent)%" }
    $branchCovText = if ($cov.BranchPercent -eq 100) { "$($cov.BranchPercent)% 🏆" }
                     elseif ($cov.BranchesValid -eq 0) { "N/A" }
                     else { "$($cov.BranchPercent)%" }
    $linesText = "$($cov.LinesCovered)/$($cov.LinesValid)"
    $branchText = if ($cov.BranchesValid -gt 0) { "$($cov.BranchesCovered)/$($cov.BranchesValid)" } else { "N/A" }

    $color = if ($cov.LinePercent -eq 0) { "DarkGray" }
             elseif ($cov.LinePercent -lt 50) { "Red" }
             elseif ($cov.LinePercent -lt 70) { "Yellow" }
             elseif ($cov.LinePercent -lt 90) { "White" }
             else { "Green" }

    Write-Host ($covRowFormat -f $cov.Project, $lineCovText, $branchCovText, $linesText, $branchText, $cov.Evaluation) -ForegroundColor $color
}

# Calculate and display overall coverage
if ($coverageResults.Count -gt 0) {
    $totalLinesCovered = ($coverageResults | Measure-Object -Property LinesCovered -Sum).Sum
    $totalLinesValid = ($coverageResults | Measure-Object -Property LinesValid -Sum).Sum
    $totalBranchesCovered = ($coverageResults | Measure-Object -Property BranchesCovered -Sum).Sum
    $totalBranchesValid = ($coverageResults | Measure-Object -Property BranchesValid -Sum).Sum

    $avgLinePercent = if ($totalLinesValid -gt 0) { [math]::Round(($totalLinesCovered / $totalLinesValid) * 100, 2) } else { 0 }
    $avgBranchPercent = if ($totalBranchesValid -gt 0) { [math]::Round(($totalBranchesCovered / $totalBranchesValid) * 100, 2) } else { 0 }

    Write-Host ("─" * ($maxProjectLen + 15 + 17 + 16 + 16 + 20 + 10)) -ForegroundColor DarkGray
    Write-Host ($covRowFormat -f "PROJECT AVERAGE", "$avgLinePercent%", "$avgBranchPercent%", "$totalLinesCovered/$totalLinesValid", "$totalBranchesCovered/$totalBranchesValid", (Get-CoverageEvaluation $avgLinePercent)) -ForegroundColor Cyan
}

# --- Summary ---

Write-Host ""
Write-Header "📌 Summary" "Magenta"

$readyProjects = ($coverageResults | Where-Object { $_.LinePercent -ge 90 }).Count
$lowCoverageProjects = ($coverageResults | Where-Object { $_.LinePercent -gt 0 -and $_.LinePercent -lt 70 }).Count
$noTestProjects = ($coverageResults | Where-Object { $_.LinePercent -eq 0 }).Count

Write-Host "📊 Tests:" -ForegroundColor White
Write-Host "   • Total tests executed: $totalTests" -ForegroundColor White
Write-Host "   • Tests passed: $totalPassed" -ForegroundColor Green
if ($totalFailed -gt 0) {
    Write-Host "   • Tests failed: $totalFailed" -ForegroundColor Red
}
if ($totalSkipped -gt 0) {
    Write-Host "   • Tests skipped: $totalSkipped" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📈 Coverage:" -ForegroundColor White
Write-Host "   • Average coverage: $avgLinePercent% (line) / $avgBranchPercent% (branch)" -ForegroundColor White
Write-Host "   • Projects ready (>90%): $readyProjects" -ForegroundColor Green
if ($lowCoverageProjects -gt 0) {
    Write-Host "   • Projects with low coverage (<70%): $lowCoverageProjects" -ForegroundColor Yellow
}
if ($noTestProjects -gt 0) {
    Write-Host "   • Projects without tests: $noTestProjects" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ Report generated successfully!" -ForegroundColor Green
Write-Host ""

# Cleanup TestResults
Get-ChildItem -Path "$PSScriptRoot" -Directory | ForEach-Object {
    $testResultsPath = Join-Path $_.FullName "TestResults"
    if (Test-Path $testResultsPath) {
        Remove-Item $testResultsPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
