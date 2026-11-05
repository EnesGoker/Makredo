[CmdletBinding()]
param(
    [string] $ResultsDirectory = 'artifacts/test-results',
    [ValidateSet('Linux', 'Windows')]
    [string] $Platform = 'Linux',
    [switch] $InventoryOnly,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$existingUnixMethods = @(
    'SymbolicLinksInSelectedSourceDataAbortMigration',
    'LinkedSourceOrDestinationRootsPreserveTheirOldStagingTargets',
    'ProtectedChildLinksPreventCleanupOfReferencedStagingData',
    'StagingOrLeaseSymbolicLinksDoNotExposeExternalDataToCleanup',
    'CleanupDoesNotFollowLinksInsideAnAbandonedDirectory'
)

function Get-TestInventory {
    param([string] $ProjectName, [bool] $OnWindows)

    $directory = Join-Path $repositoryRoot "tests/$ProjectName"
    $tests = @{}
    foreach ($file in Get-ChildItem $directory -Filter '*.cs' -Recurse -File) {
        if ($file.FullName -match '[/\\](obj|bin)[/\\]') { continue }
        $attributes = [System.Collections.Generic.List[string]]::new()
        $namespace = ''
        $className = ''
        foreach ($line in Get-Content $file.FullName) {
            if ($line -match '^\s*namespace\s+([\w.]+);') { $namespace = $Matches[1] }
            if ($line -match '^\s*public\s+(?:sealed\s+)?class\s+(\w+)') { $className = $Matches[1] }
            $stripped = $line.Trim()
            if ($stripped.StartsWith('[')) {
                $attributes.Add($stripped)
                continue
            }
            if ($line -match '^\s*public\s+(?:(?:async|static)\s+)*(?:Task(?:<[^>]+>)?|void)\s+(\w+)\s*\(') {
                $method = $Matches[1]
                $testAttributes = @($attributes | Where-Object { $_ -match '^\[(Fact|Theory|UnixTheory)\b' })
                if ($testAttributes.Count -gt 0) {
                    if (-not $namespace -or -not $className) { throw "Missing test type in $($file.FullName)" }
                    if ($testAttributes.Count -ne 1) { throw "Ambiguous test attributes: $method" }
                    if (@($attributes | Where-Object { $_ -match '\b(Skip\s*=|MemberData|ClassData)' }).Count -gt 0) {
                        throw "Explicit skip or unsupported data source in $method; update the CI inventory intentionally."
                    }
                    $isUnix = $testAttributes[0] -match '^\[UnixTheory\b'
                    if ($isUnix -and $method -notin $existingUnixMethods) {
                        throw "New platform exclusion is not in the existing UnixTheory allowlist: $method"
                    }
                    $sourceCases = @($attributes | Where-Object { $_ -match '^\[InlineData\(' }).Count
                    if ($sourceCases -eq 0) { $sourceCases = 1 }
                    $skip = $OnWindows -and $isUnix
                    $cases = if ($skip) { 1 } else { $sourceCases }
                    $identity = "$namespace.$className.$method"
                    if ($tests.ContainsKey($identity)) { throw "Duplicate source test identity: $identity" }
                    $tests[$identity] = [pscustomobject]@{
                        Identity = $identity
                        Cases = $cases
                        SourceCases = $sourceCases
                        MustSkip = $skip
                    }
                }
            }
            if ($stripped) { $attributes.Clear() }
        }
    }
    if ($tests.Count -eq 0) { throw "No tests found in source project $ProjectName" }
    return $tests
}

function Assert-TrxCounters {
    param([System.Xml.XmlElement] $Counters, [int] $Total, [int] $Passed, [int] $Skipped, [int] $Failed, [string] $FileName)

    # VSTest leaves notExecuted at zero (or omits it) for xUnit skips. Individual
    # NotExecuted results and total - executed are authoritative for those skips.
    $reportedSkipped = [int]$Counters.GetAttribute('notExecuted')
    if ([int]$Counters.total -ne $Total -or [int]$Counters.passed -ne $Passed -or
        [int]$Counters.failed -ne $Failed -or [int]$Counters.executed -ne ($Total - $Skipped) -or
        ($reportedSkipped -ne 0 -and $reportedSkipped -ne $Skipped)) {
        throw "TRX counters disagree with individual results: $FileName"
    }
}

function Read-TestResults {
    param([string] $Directory)

    $files = @(Get-ChildItem $Directory -Filter '*.trx' -Recurse -File)
    if ($files.Count -eq 0) { throw "No TRX files in $Directory" }
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($file in $files) {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($file.FullName)
        $manager = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
        $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $definitions = @{}
        foreach ($definition in $document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager)) {
            $method = $definition.SelectSingleNode('t:TestMethod', $manager)
            if ($null -eq $method) { throw "Test definition without method: $($file.Name)" }
            if ($definitions.ContainsKey($definition.id)) { throw "Duplicate TRX test definition: $($definition.id)" }
            $definitions[$definition.id] = "$($method.className).$($method.name)"
        }
        $nodes = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
        $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $manager)
        if ($null -eq $counters -or $nodes.Count -eq 0) { throw "Empty/missing TRX results: $($file.Name)" }
        $passed = @($nodes | Where-Object { $_.outcome -eq 'Passed' }).Count
        $skipped = @($nodes | Where-Object { $_.outcome -eq 'NotExecuted' }).Count
        $failed = @($nodes | Where-Object { $_.outcome -eq 'Failed' }).Count
        Assert-TrxCounters $counters $nodes.Count $passed $skipped $failed $file.Name
        foreach ($node in $nodes) {
            if (-not $definitions.ContainsKey($node.testId)) { throw "TRX result without definition: $($node.testId)" }
            $message = $node.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $manager)
            $results.Add([pscustomobject]@{
                Identity = $definitions[$node.testId]
                Name = [string]$node.testName
                Outcome = [string]$node.outcome
                Message = if ($null -eq $message) { '' } else { $message.InnerText }
            })
        }
    }
    return $results.ToArray()
}

function Test-Coverage {
    param([hashtable] $Expected, [object[]] $Actual)

    $issues = [System.Collections.Generic.List[string]]::new()
    $seen = @{}
    $byMethod = @{}
    foreach ($result in $Actual) {
        $caseIdentity = "$($result.Identity)|$($result.Name)"
        if ($seen.ContainsKey($caseIdentity)) { $issues.Add("Duplicate test case: $caseIdentity") }
        $seen[$caseIdentity] = $true
        if (-not $Expected.ContainsKey($result.Identity)) { $issues.Add("Unexpected result: $($result.Identity)"); continue }
        if (-not $byMethod.ContainsKey($result.Identity)) { $byMethod[$result.Identity] = [System.Collections.Generic.List[object]]::new() }
        $byMethod[$result.Identity].Add($result)
        if ($Expected[$result.Identity].MustSkip) {
            if ($result.Outcome -ne 'NotExecuted') { $issues.Add("Expected existing UnixTheory exclusion: $($result.Identity)") }
        }
        elseif ($result.Outcome -ne 'Passed') {
            $issues.Add("$($result.Outcome): $($result.Name) -- $($result.Message)")
        }
    }
    foreach ($test in $Expected.Values) {
        $count = if ($byMethod.ContainsKey($test.Identity)) { $byMethod[$test.Identity].Count } else { 0 }
        if ($count -ne $test.Cases) { $issues.Add("Case count for $($test.Identity): expected $($test.Cases), actual $count") }
    }
    if ($issues.Count -gt 0) { throw ($issues -join [Environment]::NewLine) }
}

function Invoke-SelfTest {
    $expected = @{
        'Example.Tests.Active' = [pscustomobject]@{ Identity = 'Example.Tests.Active'; Cases = 2; SourceCases = 2; MustSkip = $false }
        'Example.Tests.Unix' = [pscustomobject]@{ Identity = 'Example.Tests.Unix'; Cases = 1; SourceCases = 3; MustSkip = $true }
    }
    $activeOne = [pscustomobject]@{ Identity = 'Example.Tests.Active'; Name = 'row one'; Outcome = 'Passed'; Message = '' }
    $activeTwo = [pscustomobject]@{ Identity = 'Example.Tests.Active'; Name = 'row two'; Outcome = 'Passed'; Message = '' }
    $unix = [pscustomobject]@{ Identity = 'Example.Tests.Unix'; Name = 'Unix'; Outcome = 'NotExecuted'; Message = '' }
    Test-Coverage $expected @($activeOne, $activeTwo, $unix)
    $unexpectedSkip = [pscustomobject]@{ Identity = 'Example.Tests.Active'; Name = 'row two'; Outcome = 'NotExecuted'; Message = '' }
    $failure = [pscustomobject]@{ Identity = 'Example.Tests.Active'; Name = 'row two'; Outcome = 'Failed'; Message = 'regression fixture' }
    $unexpected = [pscustomobject]@{ Identity = 'Example.Tests.Extra'; Name = 'extra'; Outcome = 'Passed'; Message = '' }
    $invalid = @(
        @{ Label = 'zero results'; Values = @() },
        @{ Label = 'missing theory row'; Values = @($activeOne, $unix) },
        @{ Label = 'duplicate row'; Values = @($activeOne, $activeOne, $unix) },
        @{ Label = 'unexpected skip'; Values = @($activeOne, $unexpectedSkip, $unix) },
        @{ Label = 'failing result'; Values = @($activeOne, $failure, $unix) },
        @{ Label = 'unexpected method'; Values = @($activeOne, $activeTwo, $unix, $unexpected) }
    )
    foreach ($fixture in $invalid) {
        $rejected = $false
        try { Test-Coverage $expected $fixture.Values } catch { $rejected = $true }
        if (-not $rejected) { throw "Self-test accepted $($fixture.Label)" }
    }
    foreach ($optionalSkipCounter in @('', ' notExecuted="0"', ' notExecuted="5"')) {
        [xml]$counterFixture = '<Counters total="112" executed="107" passed="106" failed="1"' + $optionalSkipCounter + ' />'
        Assert-TrxCounters $counterFixture.DocumentElement 112 106 5 1 'counter fixture'
    }
    foreach ($invalidCounter in @(
        '<Counters total="111" executed="107" passed="106" failed="1" />',
        '<Counters total="112" executed="112" passed="106" failed="1" />',
        '<Counters total="112" executed="107" passed="105" failed="1" />',
        '<Counters total="112" executed="107" passed="106" failed="0" />',
        '<Counters total="112" executed="107" passed="106" failed="1" notExecuted="4" />'
    )) {
        [xml]$counterFixture = $invalidCounter
        $rejected = $false
        try { Assert-TrxCounters $counterFixture.DocumentElement 112 106 5 1 'invalid counter fixture' } catch { $rejected = $true }
        if (-not $rejected) { throw 'Self-test accepted inconsistent TRX counters.' }
    }
    Write-Output 'PASS: coverage guard rejects zero/missing/duplicate/unexpected/failed/skipped results and inconsistent TRX counters.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

$projects = @('Makredo.Core.Tests')
if ($Platform -eq 'Windows') { $projects += 'Makredo.Windows.Tests' }
$inventory = @{}
$combined = @{}
foreach ($project in $projects) {
    $inventory[$project] = Get-TestInventory $project ($Platform -eq 'Windows')
    foreach ($test in $inventory[$project].Values) { $combined[$test.Identity] = $test }
}

if (-not $InventoryOnly) {
    $resultsPath = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
    $results = @(Read-TestResults $resultsPath)
    Test-Coverage $combined $results
}

$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add('## Test coverage verification')
$summary.Add('')
$summary.Add('| Suite | Methods | Source cases | Expected results | Passed | Existing platform skips |')
$summary.Add('|---|---:|---:|---:|---:|---:|')
foreach ($project in $projects) {
    $tests = $inventory[$project]
    $sourceCases = ($tests.Values | Measure-Object -Property SourceCases -Sum).Sum
    $cases = ($tests.Values | Measure-Object -Property Cases -Sum).Sum
    $skips = @($tests.Values | Where-Object MustSkip).Count
    $summary.Add("| $project | $($tests.Count) | $sourceCases | $cases | $($cases - $skips) | $skips |")
}
$summary.Add('')
$summary.Add('Windows: the five pre-existing UnixTheory exclusions collapse 13 source rows into five skipped results. No new exclusion is accepted. All other cases must pass, and every source method must appear in TRX.')
$text = $summary -join [Environment]::NewLine
Write-Output $text
if (-not $InventoryOnly -and $env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $text }
if (-not $InventoryOnly) { Write-Output 'PASS: exact suite/method/case coverage and all expected outcomes verified from TRX.' }
