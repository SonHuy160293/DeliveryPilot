[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FeatureDir
)

$ErrorActionPreference = 'Stop'
$resolvedFeature = (Resolve-Path $FeatureDir).Path

function Parse-Frontmatter([string]$filePath) {
    $text = Get-Content -LiteralPath $filePath -Raw -Encoding UTF8
    $m = [regex]::Match($text, '(?s)\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n')
    if (-not $m.Success) { throw "$filePath : missing YAML frontmatter" }
    $meta = @{}
    $listKey = $null
    foreach ($line in ($m.Groups[1].Value -split '\r?\n')) {
        $f = [regex]::Match($line, '^([A-Za-z][A-Za-z0-9_]*):\s*(.*)$')
        if ($f.Success) {
            $k = $f.Groups[1].Value
            $rawVal = $f.Groups[2].Value.Trim()
            $v = ($rawVal -split ' #')[0].Trim()
            if ($rawVal -eq '') {
                $meta[$k] = [System.Collections.Generic.List[string]]::new()
                $listKey = $k
            } elseif ($v -in @('null', '~')) {
                $meta[$k] = $null
                $listKey = $null
            } elseif ($v -eq '[]') {
                $meta[$k] = [System.Collections.Generic.List[string]]::new()
                $listKey = $null
            } elseif ($v.StartsWith('[') -and $v.EndsWith(']')) {
                $inner = $v.Substring(1, $v.Length - 2).Trim()
                $list = [System.Collections.Generic.List[string]]::new()
                if ($inner) {
                    foreach ($item in $inner.Split(',')) { $list.Add($item.Trim().Trim('"', "'")) }
                }
                $meta[$k] = $list
                $listKey = $null
            } else {
                $meta[$k] = $v.Trim('"', "'")
                $listKey = $null
            }
        } elseif ($listKey) {
            $li = [regex]::Match($line, '^\s+-\s+(.+?)\s*$')
            if ($li.Success) {
                $meta[$listKey].Add($li.Groups[1].Value.Trim().Trim('"', "'"))
            }
        }
    }
    return @{ Meta = $meta; Body = $text.Substring($m.Length); FullText = $text }
}

$errors = 0
$warnings = 0

# 1. Link check across all files in FeatureDir + docs/features/README.md
$filesToCheck = @(Get-ChildItem -Path $resolvedFeature -Recurse -Filter "*.md")
$parentIndex = Join-Path (Split-Path $resolvedFeature -Parent) "README.md"
if (Test-Path -LiteralPath $parentIndex) {
    $filesToCheck += (Get-Item -LiteralPath $parentIndex)
}

foreach ($f in $filesToCheck) {
    $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
    foreach ($m in [regex]::Matches($text, '(?<!!)\[[^\]]+\]\(([^)]+)\)')) {
        $raw = $m.Groups[1].Value.Trim().Split(' ')[0].Trim('<','>')
        if ($raw -match '^(http:|https:|mailto:|#|data:)') { continue }
        $clean = (($raw -split '#')[0] -split '\?')[0]
        $part = [uri]::UnescapeDataString($clean)
        if ($part -and -not (Test-Path -LiteralPath (Join-Path $f.DirectoryName $part))) {
            Write-Host "ERROR: $($f.FullName): broken link $raw"
            $errors++
        }
    }
}

# 2. Discovery check (check_discovery.py rules)
$discDir = Join-Path $resolvedFeature "discovery"
if (Test-Path -LiteralPath $discDir) {
    Get-ChildItem -Path $discDir -Filter "*.md" | Where-Object { $_.Name -notlike "*-prompt.md" } | ForEach-Object {
        $p = Parse-Frontmatter $_.FullName
        foreach ($req in @('id','title','status')) {
            if (-not $p.Meta[$req]) { Write-Host "ERROR: $($_.FullName) missing $req"; $errors++ }
        }
        $st = ([string]$p.Meta['status']).ToLower()
        if ($st -notin @('draft','hypothesis','proposed','accepted','rejected','superseded')) {
            Write-Host "ERROR: $($_.FullName) invalid discovery status $st"; $errors++
        }
        if ($st -eq 'accepted' -and -not $p.Meta['approval_ref']) {
            Write-Host "ERROR: $($_.FullName) accepted document needs approval_ref"; $errors++
        }
    }
}

# 3. Requirements --strict check (check_requirements.py rules)
$reqFile = Join-Path $resolvedFeature "requirements.md"
$reqParsed = Parse-Frontmatter $reqFile
$featureId = [string]$reqParsed.Meta['feature_id']
if (-not $featureId -or $featureId -notmatch '^FEAT-[A-Z0-9-]+$') {
    Write-Host "ERROR: requirements metadata must contain feature_id: FEAT-..."; $errors++
}
$folderBaseName = Split-Path $resolvedFeature -Leaf
if ($featureId -and -not $folderBaseName.StartsWith($featureId)) {
    Write-Host "ERROR: feature_id $featureId does not match directory $folderBaseName"; $errors++
}
$frs = @([regex]::Matches($reqParsed.Body, '(?m)^\|\s*(FR-[A-Z0-9-]+)\s*\|') | ForEach-Object { $_.Groups[1].Value })
if ($frs.Count -eq 0) {
    Write-Host "ERROR: no FR-* declaration table rows found in requirements.md"; $errors++
}
$acMatch = [regex]::Match($reqParsed.Body, '(?msi)^##\s+Acceptance[^\n]*\r?\n(.*?)(?=^##\s+|\z)')
$covered = if ($acMatch.Success) { @([regex]::Matches($acMatch.Groups[1].Value, '\bFR-[A-Z0-9-]+\b') | ForEach-Object { $_.Value } | Select-Object -Unique) } else { @() }
$uncovered = @($frs | Where-Object { $_ -notin $covered })
if ($uncovered.Count -gt 0) {
    Write-Host "ERROR: Uncovered FRs in ## Acceptance scenarios: $($uncovered -join ', ')"; $errors++
}

foreach ($sub in @( @{Dir='stories';Prefix='US-'}, @{Dir='use-cases';Prefix='UC-'} )) {
    $folder = Join-Path $resolvedFeature $sub.Dir
    if (Test-Path -LiteralPath $folder) {
        Get-ChildItem -Path $folder -Filter "*.md" | Where-Object { $_.Name -notlike "*-prompt.md" -and $_.Name -ne "README.md" } | ForEach-Object {
            $p = Parse-Frontmatter $_.FullName
            if (-not ([string]$p.Meta['id']).StartsWith($sub.Prefix)) { Write-Host "ERROR: $($_.FullName) needs $($sub.Prefix) ID"; $errors++ }
            if ($p.Meta['feature_id'] -ne $featureId) { Write-Host "ERROR: $($_.FullName) mismatched feature_id"; $errors++ }
            $docFrs = @([regex]::Matches($p.Body, '\bFR-[A-Z0-9-]+\b') | ForEach-Object { $_.Value })
            if (-not ($docFrs | Where-Object { $_ -in $frs })) { Write-Host "WARN: $($_.FullName) no recognized FR reference"; $warnings++ }
        }
    }
}

# 4. Task graph check (check_task_graph.py rules)
$taskDir = Join-Path $resolvedFeature "tasks"
$tasks = @{}
Get-ChildItem -Path $taskDir -Filter "TASK-*.md" | Where-Object { $_.Name -notlike "*-prompt.md" } | ForEach-Object {
    $p = Parse-Frontmatter $_.FullName
    $tid = [string]$p.Meta['id']
    if (-not $tid -or $tid -notmatch '^TASK-[A-Z0-9-]+$') { Write-Host "ERROR: $($_.FullName) malformed TASK id"; $errors++ }
    if (-not $_.BaseName.StartsWith($tid)) { Write-Host "ERROR: $($_.Name) basename must start with $tid"; $errors++ }
    if ($p.Meta['feature_id'] -ne $featureId) { Write-Host "ERROR: $($_.Name) feature_id must be $featureId"; $errors++ }
    $tasks[$tid] = $p.Meta
    if (-not $p.Meta['spec_refs'] -or $p.Meta['spec_refs'].Count -eq 0) { Write-Host "WARN: $($_.Name) missing spec_refs"; $warnings++ }
    foreach ($sr in $p.Meta['spec_refs']) {
        if (-not (Test-Path -LiteralPath (Join-Path $_.DirectoryName $sr))) { Write-Host "ERROR: $($_.Name) missing spec_ref $sr"; $errors++ }
    }
    foreach ($ar in $p.Meta['acceptance_refs']) {
        if ($ar -notin $frs) { Write-Host "ERROR: $($_.Name) acceptance_ref $ar not in requirements FRs"; $errors++ }
    }
}
foreach ($tid in $tasks.Keys) {
    foreach ($dep in $tasks[$tid]['depends_on']) {
        if ($dep -notin $tasks.Keys) { Write-Host "ERROR: $tid unknown dependency $dep"; $errors++ }
        if ($dep -eq $tid) { Write-Host "ERROR: $tid self-dependency"; $errors++ }
    }
}

Write-Host "Validated feature '$folderBaseName': $($filesToCheck.Count) file(s), $($frs.Count) FR(s), $($tasks.Count) task(s) -> $errors error(s), $warnings warning(s)."
if ($errors -gt 0 -or $warnings -gt 0) { exit 1 }
exit 0
