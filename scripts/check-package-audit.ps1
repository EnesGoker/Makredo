[CmdletBinding()]
param(
    [string] $ReportPath,
    [ValidateRange(0, 10000)]
    [int] $ExpectedProjects = 0,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ObjectShape {
    param($Value, [string[]] $AllowedKeys, [string] $Location)

    if ($Value -isnot [System.Collections.IDictionary]) {
        throw "$Location must be a JSON object."
    }
    foreach ($key in $Value.Keys) {
        if ($key -notin $AllowedKeys) {
            throw "Unexpected property '$key' in $Location."
        }
    }
    foreach ($key in @('errors', 'problems')) {
        if ($Value.Contains($key) -and @($Value[$key]).Count -gt 0) {
            throw "NuGet reported $key in ${Location}: $($Value[$key] | ConvertTo-Json -Compress -Depth 20)"
        }
    }
}

function Assert-CleanPackageAudit {
    param([string] $Json, [int] $ExpectedProjectCount = 0)

    $report = ConvertFrom-Json -InputObject $Json -AsHashtable -Depth 100 -ErrorAction Stop
    Assert-ObjectShape $report @('version', 'parameters', 'sources', 'projects', 'errors', 'problems') 'report'
    if (-not $report.Contains('version') -or $report['version'] -ne 1) {
        throw 'The NuGet audit report must use output version 1.'
    }
    if (-not $report.Contains('parameters') -or $report['parameters'] -isnot [string] -or
        $report['parameters'] -notmatch '(?:^|\s)--vulnerable(?:\s|$)' -or
        $report['parameters'] -notmatch '(?:^|\s)--include-transitive(?:\s|$)') {
        throw 'The NuGet audit report must include both --vulnerable and --include-transitive.'
    }
    if (-not $report.Contains('projects') -or $report['projects'] -isnot [System.Array] -or $report['projects'].Count -eq 0) {
        throw 'The NuGet audit report must contain a nonempty projects array.'
    }
    if ($ExpectedProjectCount -gt 0 -and $report['projects'].Count -ne $ExpectedProjectCount) {
        throw "Expected $ExpectedProjectCount audited projects; found $($report['projects'].Count)."
    }

    $findings = [System.Collections.Generic.List[string]]::new()
    $projectPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $report['projects']) {
        Assert-ObjectShape $project @('path', 'frameworks', 'errors', 'problems') 'project'
        if (-not $project.Contains('path') -or $project['path'] -isnot [string] -or [string]::IsNullOrWhiteSpace($project['path'])) {
            throw 'An audited project is missing its path.'
        }
        $path = $project['path']
        if (-not $projectPaths.Add($path)) {
            throw "Duplicate audited project: $path"
        }
        # NuGet omits frameworks entirely when the project has no vulnerable packages.
        if (-not $project.Contains('frameworks')) {
            continue
        }
        if ($project['frameworks'] -isnot [System.Array] -or $project['frameworks'].Count -eq 0) {
            throw "Project '$path' has no audited frameworks."
        }
        foreach ($framework in $project['frameworks']) {
            Assert-ObjectShape $framework @('framework', 'topLevelPackages', 'transitivePackages', 'errors', 'problems') "framework in $path"
            if (-not $framework.Contains('framework') -or $framework['framework'] -isnot [string] -or [string]::IsNullOrWhiteSpace($framework['framework'])) {
                throw "Project '$path' has an unnamed framework."
            }
            foreach ($kind in @('topLevelPackages', 'transitivePackages')) {
                if (-not $framework.Contains($kind)) {
                    continue
                }
                if ($framework[$kind] -isnot [System.Array]) {
                    throw "$kind in '$path' must be an array."
                }
                foreach ($package in $framework[$kind]) {
                    Assert-ObjectShape $package @('id', 'requestedVersion', 'resolvedVersion', 'vulnerabilities', 'errors', 'problems') "package in $path"
                    if (-not $package.Contains('id') -or $package['id'] -isnot [string] -or [string]::IsNullOrWhiteSpace($package['id'])) {
                        throw "An audited package in '$path' has no ID."
                    }
                    if (-not $package.Contains('resolvedVersion') -or $package['resolvedVersion'] -isnot [string] -or [string]::IsNullOrWhiteSpace($package['resolvedVersion'])) {
                        throw "Package '$($package['id'])' has no resolved version."
                    }
                    if (-not $package.Contains('vulnerabilities') -or $package['vulnerabilities'] -isnot [System.Array]) {
                        throw "Package '$($package['id'])' has no vulnerability array in a vulnerability-only report."
                    }
                    foreach ($vulnerability in $package['vulnerabilities']) {
                        Assert-ObjectShape $vulnerability @('severity', 'advisoryurl') "advisory for $($package['id'])"
                        if (-not $vulnerability.Contains('severity') -or $vulnerability['severity'] -notin @('Low', 'Moderate', 'High', 'Critical')) {
                            throw "Package '$($package['id'])' has an invalid vulnerability severity."
                        }
                        if (-not $vulnerability.Contains('advisoryurl') -or $vulnerability['advisoryurl'] -isnot [string] -or [string]::IsNullOrWhiteSpace($vulnerability['advisoryurl'])) {
                            throw "Package '$($package['id'])' has an advisory without a URL."
                        }
                        $findings.Add("$path [$($framework['framework'])] $($package['id']) $($package['resolvedVersion']) ($kind): $($vulnerability['severity']) $($vulnerability['advisoryurl'])")
                    }
                }
            }
        }
    }
    if ($findings.Count -gt 0) {
        throw "Vulnerable NuGet packages found:`n$($findings -join "`n")"
    }
    return $report['projects'].Count
}

if ($SelfTest) {
    $clean = '{"version":1,"parameters":"--vulnerable --include-transitive","projects":[{"path":"test.csproj","frameworks":[{"framework":"net10.0"}]}]}'
    if ((Assert-CleanPackageAudit $clean 1) -ne 1) {
        throw 'The clean audit fixture was not accepted.'
    }
    $cleanPathOnly = '{"version":1,"parameters":"--vulnerable --include-transitive","projects":[{"path":"test.csproj"}]}'
    if ((Assert-CleanPackageAudit $cleanPathOnly 1) -ne 1) {
        throw 'The normal NuGet path-only clean fixture was not accepted.'
    }
    $package = '{"id":"Synthetic.Package","requestedVersion":"1.0.0","resolvedVersion":"1.0.0","vulnerabilities":[{"severity":"High","advisoryurl":"https://example.invalid/CVE-SYNTHETIC"}]}'
    $fixtures = @(
        @{ Name = 'top-level vulnerability'; Json = $clean.Replace('"framework":"net10.0"', '"framework":"net10.0","topLevelPackages":[' + $package + ']') },
        @{ Name = 'transitive vulnerability'; Json = $clean.Replace('"framework":"net10.0"', '"framework":"net10.0","transitivePackages":[' + $package + ']') },
        @{ Name = 'malformed JSON'; Json = '{' },
        @{ Name = 'NuGet errors'; Json = $clean.Replace('"version":1', '"version":1,"errors":["Synthetic error"]') },
        @{ Name = 'NuGet problems'; Json = $clean.Replace('"version":1', '"version":1,"problems":["Synthetic problem"]') },
        @{ Name = 'empty projects'; Json = '{"version":1,"parameters":"--vulnerable --include-transitive","projects":[]}' },
        @{ Name = 'missing framework'; Json = '{"version":1,"parameters":"--vulnerable --include-transitive","projects":[{"path":"test.csproj","frameworks":[]}]}' },
        @{ Name = 'missing transitive scope'; Json = $clean.Replace('--vulnerable --include-transitive', '--vulnerable') },
        @{ Name = 'unexpected shape'; Json = $clean.Replace('"version":1', '"version":1,"unknown":true') }
    )
    foreach ($fixture in $fixtures) {
        $rejected = $false
        try {
            $null = Assert-CleanPackageAudit $fixture.Json
        }
        catch {
            $rejected = $true
        }
        if (-not $rejected) {
            throw "Invalid audit fixture was accepted: $($fixture.Name)"
        }
    }
    Write-Host 'PASS: clean audit accepted; vulnerable, malformed, error, empty, and unexpected reports rejected.'
    exit 0
}

try {
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        throw 'Pass -ReportPath with the JSON report produced by dotnet list package --vulnerable --include-transitive.'
    }
    $count = Assert-CleanPackageAudit (Get-Content -LiteralPath $ReportPath -Raw) $ExpectedProjects
    Write-Host "PASS: $count projects audited; no vulnerable direct or transitive NuGet packages."
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
