[CmdletBinding()]
param(
    [ValidateRange(1, 1000)]
    [int] $Iterations = 100,
    [Parameter(Mandatory)]
    [ValidateRange(1, 10000)]
    [int] $ExpectedCases,
    [string] $ResultsDirectory = 'artifacts/concurrency-results'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$resultsPath = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
New-Item -ItemType Directory -Force $resultsPath | Out-Null
$completed = 0
$expectedNames = $null
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$failure = $null

try {
    Push-Location $repositoryRoot
    try {
        for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
            $label = $iteration.ToString('D3', [System.Globalization.CultureInfo]::InvariantCulture)
            $trxPath = Join-Path $resultsPath "iteration-$label.trx"
            if (Test-Path $trxPath) { throw "Refusing stale results: $trxPath" }
            & dotnet test tests/Makredo.Core.Tests -c Release --no-build --filter 'Category=Concurrency' --logger "trx;LogFileName=iteration-$label.trx" --results-directory $resultsPath *> (Join-Path $resultsPath "iteration-$label.log")
            if ($LASTEXITCODE -ne 0) { throw "Concurrency iteration $iteration failed; see iteration-$label.log." }

            $document = [System.Xml.XmlDocument]::new()
            $document.XmlResolver = $null
            $document.Load($trxPath)
            $manager = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
            $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
            $results = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
            $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $manager)
            if ($null -eq $counters -or $results.Count -ne $ExpectedCases -or
                [int]$counters.total -ne $ExpectedCases -or [int]$counters.executed -ne $ExpectedCases -or
                [int]$counters.passed -ne $ExpectedCases -or [int]$counters.failed -ne 0 -or
                @($results | Where-Object outcome -ne 'Passed').Count -ne 0) {
                throw "Iteration $iteration did not pass all $ExpectedCases concurrency cases."
            }
            $names = @($results | ForEach-Object { [string]$_.testName } | Sort-Object -Unique)
            if ($names.Count -ne $ExpectedCases) { throw "Duplicate test names in iteration $iteration." }
            if ($null -eq $expectedNames) { $expectedNames = $names }
            elseif (@(Compare-Object $expectedNames $names).Count -ne 0) { throw "Test identities changed in iteration $iteration." }
            $completed++
            if ($iteration % 10 -eq 0 -or $iteration -eq $Iterations) {
                Write-Host "PASS: $completed/$Iterations iterations; $ExpectedCases cases per iteration."
            }
        }
    }
    finally {
        Pop-Location
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $watch.Stop()
    $summary = [ordered]@{
        commit = $env:GITHUB_SHA
        runId = $env:GITHUB_RUN_ID
        requestedIterations = $Iterations
        completedIterations = $completed
        casesPerIteration = $ExpectedCases
        passedCases = $completed * $ExpectedCases
        elapsedSeconds = $watch.Elapsed.TotalSeconds
        failure = $failure
        tests = $expectedNames
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $resultsPath 'summary.json')
}

if ($failure) { throw $failure }
$text = "Concurrency regressions: **$completed/$Iterations iterations passed**, **$ExpectedCases cases each**, **$($completed * $ExpectedCases) passes**, zero skipped/failed cases."
Write-Host $text
if ($env:GITHUB_STEP_SUMMARY) { Add-Content $env:GITHUB_STEP_SUMMARY -Value $text }
