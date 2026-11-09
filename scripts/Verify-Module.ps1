# Verifies one module for a TASK: build, tests (TRX counts), architecture rules and docs validation.
# Prints step lines and a final "VERIFY-SUMMARY {json}" line. Exit 0 = passed/passed-with-skips, 1 = failed, 2 = usage.
# -RequireDb fails when the module's PostgreSQL variable is unset or any test was skipped because of it.
# Never prints or records connection-string values; only whether the variable is set.
param(
    [string]$Module,
    [switch]$RequireDb,
    [switch]$NoBuild,
    [string]$Filter,
    [switch]$SkipArchitecture,
    [switch]$SkipDocs,
    [string]$OutFile
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$culture = [Globalization.CultureInfo]::InvariantCulture
function Get-UtcStamp { return [DateTime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", $culture) }

# Database variables stay separate on purpose: Identity and Projects both own a "reliability" schema.
$modules = [ordered]@{
    Identity     = @{ Project = 'tests/DeliveryPilot.Identity.Tests/DeliveryPilot.Identity.Tests.csproj'; DbVariable = 'DELIVERYPILOT_TEST_POSTGRES' }
    Projects     = @{ Project = 'tests/DeliveryPilot.Projects.Tests/DeliveryPilot.Projects.Tests.csproj'; DbVariable = 'DELIVERYPILOT_TEST_PROJECTS_POSTGRES' }
    Notification = @{ Project = 'tests/DeliveryPilot.Notification.Tests/DeliveryPilot.Notification.Tests.csproj'; DbVariable = 'DELIVERYPILOT_DELIVERY_TEST_POSTGRES' }
    Gateway      = @{ Project = 'tests/DeliveryPilot.Gateway.Tests/DeliveryPilot.Gateway.Tests.csproj'; DbVariable = $null }
    HttpClient   = @{ Project = 'tests/DeliveryPilot.AspNetCore.HttpClient.Tests/DeliveryPilot.AspNetCore.HttpClient.Tests.csproj'; DbVariable = $null }
}
$moduleName = @($modules.Keys | Where-Object { $_ -eq $Module }) | Select-Object -First 1
if (-not $moduleName) {
    Write-Host ("Usage: Verify-Module.ps1 -Module {0} [-RequireDb] [-NoBuild] [-Filter expr] [-SkipArchitecture] [-SkipDocs] [-OutFile path]" -f ($modules.Keys -join '|'))
    exit 2
}
$config = $modules[$moduleName]
$project = $config.Project
$dbVariable = $config.DbVariable
$dbConfigured = $false
if ($dbVariable) { $dbConfigured = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($dbVariable)) }

$invocation = "scripts/Verify-Module.ps1 -Module $moduleName"
if ($RequireDb) { $invocation += ' -RequireDb' }
if ($NoBuild) { $invocation += ' -NoBuild' }
if ($Filter) { $invocation += " -Filter `"$Filter`"" }
if ($SkipArchitecture) { $invocation += ' -SkipArchitecture' }
if ($SkipDocs) { $invocation += ' -SkipDocs' }

$startedAt = Get-UtcStamp
$commit = $null
try {
    $head = & git -C $repoRoot rev-parse HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $head) { $commit = "$head".Trim() }
} catch { $commit = $null }

$steps = New-Object System.Collections.Generic.List[object]
function Add-Step([string]$Name, [string]$Command, [string]$Status, [int]$ExitCode, [double]$Seconds) {
    $steps.Add([ordered]@{ name = $Name; command = $Command; status = $Status; exitCode = $ExitCode; durationSeconds = [Math]::Round($Seconds, 1) })
    Write-Host ("[{0}] {1} (exit {2}, {3:0.0}s)" -f $Status.ToUpperInvariant(), $Name, $ExitCode, $Seconds)
}
# Runs an action that returns an exit code; any exception is recorded as a failed step instead of stopping the script.
function Invoke-Step([string]$Name, [string]$Command, [scriptblock]$Action) {
    Write-Host "--- $Name"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $code = 1
    try {
        $result = @(& $Action)
        $code = [int]$result[-1]
    } catch {
        Write-Host "    $Name error: $($_.Exception.Message)"
        $code = 1
    }
    $status = 'passed'
    if ($code -ne 0) { $status = 'failed' }
    Add-Step $Name $Command $status $code $watch.Elapsed.TotalSeconds
    return ($code -eq 0)
}

# 1. Database precheck
if ($RequireDb -and -not $dbVariable) {
    Add-Step 'db-precheck' '' 'not-applicable' 0 0
} elseif ($RequireDb -and -not $dbConfigured) {
    Write-Host "    Set $dbVariable to an isolated PostgreSQL 18 database."
    Add-Step 'db-precheck' "require $dbVariable" 'failed' 1 0
} elseif ($RequireDb) {
    Add-Step 'db-precheck' "require $dbVariable" 'passed' 0 0
}

Push-Location $repoRoot
try {
    # 2. Build
    if ($NoBuild) {
        Add-Step 'build' '' 'skipped' 0 0
    } else {
        $buildCommand = "dotnet build $project --nologo --verbosity quiet"
        [void](Invoke-Step 'build' $buildCommand { & dotnet build $project --nologo --verbosity quiet | Out-Host; $LASTEXITCODE })
    }

    # 3. Tests with TRX
    $resultsRel = "artifacts/verify/$moduleName/" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss', $culture)
    $resultsDir = Join-Path $repoRoot $resultsRel
    $testArgs = @('test', $project, '--no-build', '--nologo', '--verbosity', 'quiet', '--logger', 'trx;LogFileName=verify.trx', '--results-directory', $resultsDir)
    $testCommand = "dotnet test $project --no-build --nologo --verbosity quiet --logger `"trx;LogFileName=verify.trx`" --results-directory $resultsRel"
    if ($Filter) { $testArgs += @('--filter', $Filter); $testCommand += " --filter `"$Filter`"" }
    [void](Invoke-Step 'test' $testCommand { & dotnet @testArgs | Out-Host; $LASTEXITCODE })

    $tests = [ordered]@{ total = 0; passed = 0; failed = 0; skipped = 0; dbSkipped = 0; skipReasons = [ordered]@{} }
    $trxPath = $null
    $trxFile = Join-Path $resultsDir 'verify.trx'
    if (Test-Path -LiteralPath $trxFile) {
        $trxPath = "$resultsRel/verify.trx"
        [xml]$trx = Get-Content -LiteralPath $trxFile -Raw
        $ns = New-Object Xml.XmlNamespaceManager($trx.NameTable)
        $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $counters = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $ns)
        if ($counters) {
            $tests.total = [int]$counters.total
            $tests.passed = [int]$counters.passed
            $tests.failed = [int]$counters.failed
            $tests.skipped = [int]$counters.notExecuted
        }
        # xUnit skips appear as NotExecuted results while Counters/@notExecuted can stay 0, so count the nodes.
        $skippedNodes = $trx.SelectNodes("//t:UnitTestResult[@outcome='NotExecuted']", $ns)
        if ($skippedNodes.Count -gt $tests.skipped) { $tests.skipped = $skippedNodes.Count }
        foreach ($result in $skippedNodes) {
            $reason = $null
            foreach ($path in @('t:Output/t:ErrorInfo/t:Message', 't:Output/t:StdOut')) {
                $node = $result.SelectSingleNode($path, $ns)
                if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) { $reason = $node.InnerText.Trim(); break }
            }
            if (-not $reason) { $reason = '(no skip reason)' }
            if ($tests.skipReasons.Contains($reason)) { $tests.skipReasons[$reason]++ } else { $tests.skipReasons[$reason] = 1 }
            if ($dbVariable -and $reason.Contains($dbVariable)) { $tests.dbSkipped++ }
        }
    } else {
        Write-Host "    No TRX produced at $resultsRel/verify.trx"
    }
    Write-Host ("    Tests: {0} passed, {1} failed, {2} skipped ({3} database)" -f $tests.passed, $tests.failed, $tests.skipped, $tests.dbSkipped)

    # 4. Architecture rules, in this process (a nested shell can be blocked by execution policy)
    if ($SkipArchitecture) {
        Add-Step 'architecture' '' 'skipped' 0 0
    } else {
        [void](Invoke-Step 'architecture' 'scripts/Test-Architecture.ps1' { & (Join-Path $PSScriptRoot 'Test-Architecture.ps1') | Out-Host; 0 })
    }

    # 5. Documentation validation
    if ($SkipDocs) {
        Add-Step 'docs' '' 'skipped' 0 0
    } else {
        $python = @('python', 'python3' | ForEach-Object { Get-Command $_ -CommandType Application -ErrorAction SilentlyContinue } | Select-Object -First 1)
        if ($python.Count -eq 0 -or -not $python[0]) {
            Write-Host '    python not found on PATH; docs validation not run.'
            Add-Step 'docs' 'python tools/docs.py validate' 'not-run' 0 0
        } else {
            $pythonPath = $python[0].Source
            [void](Invoke-Step 'docs' 'python tools/docs.py validate' { & $pythonPath (Join-Path $repoRoot 'tools/docs.py') validate | Out-Host; $LASTEXITCODE })
        }
    }
} finally {
    Pop-Location
}

$failedSteps = @($steps | Where-Object { $_.status -eq 'failed' })
$result = 'passed'
if ($failedSteps.Count -gt 0 -or $tests.failed -gt 0 -or ($RequireDb -and $tests.dbSkipped -gt 0)) {
    $result = 'failed'
} elseif ($tests.skipped -gt 0) {
    $result = 'passed-with-skips'
}
if ($RequireDb -and $tests.dbSkipped -gt 0) { Write-Host "    -RequireDb: $($tests.dbSkipped) test(s) skipped for $dbVariable." }

$summary = [ordered]@{
    module       = $moduleName
    commit       = $commit
    startedAt    = $startedAt
    finishedAt   = Get-UtcStamp
    invocation   = $invocation
    requireDb    = [bool]$RequireDb
    dbVariable   = $dbVariable
    dbConfigured = $dbConfigured
    steps        = $steps.ToArray()
    tests        = $tests
    trxPath      = $trxPath
    result       = $result
}
$json = ConvertTo-Json -InputObject $summary -Depth 6 -Compress
if ($OutFile) {
    $outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
    [IO.File]::WriteAllText($outPath, $json, (New-Object Text.UTF8Encoding($false)))
}
Write-Host "RESULT: $result"
Write-Output "VERIFY-SUMMARY $json"
if ($result -eq 'failed') { exit 1 }
exit 0
