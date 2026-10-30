[CmdletBinding()]
param()

# Exercise argument routing and safe failures without running Docker or loading secrets.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'deliverypilot.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'Deployment CLI contains PowerShell syntax errors.' }
foreach ($name in @('Invoke-Docker','Compose','Build-Tools','Check-Prerequisites')) {
    $definition = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true) | Where-Object { $_.Name -eq $name }
    if (@($definition).Count -ne 1) { throw "Expected one deployment function: $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}

$script:DockerCalls = @()
$script:DockerExitCode = 0
$script:FailBuilder = $false
function docker {
    $script:DockerCalls += ,@($args)
    $global:LASTEXITCODE = $script:DockerExitCode
    if ($script:FailBuilder -and $args[0] -eq 'buildx') { $global:LASTEXITCODE = 19 }
    if ($args[0] -eq 'compose' -and $args[1] -eq 'version') { '2.24.4' }
}
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Arguments([string[]]$Actual, [string[]]$Expected) {
    Assert-True (($Actual -join "`0") -ceq ($Expected -join "`0")) 'Docker argument routing differs from the expected operation.'
}
function Assert-Failure([scriptblock]$Run, [string]$ExpectedMessage) {
    $failure = $null
    try { & $Run | Out-Null } catch { $failure = $_.Exception.Message }
    Assert-True ($null -ne $failure -and $failure.Contains($ExpectedMessage)) 'Docker failure lost its operation or exit code.'
    foreach ($sentinel in @('PRIVATE_CONFIG_SENTINEL','PRIVATE_ARGUMENT_SENTINEL','PRIVATE_BUILDER_SENTINEL')) {
        Assert-True (-not $failure.Contains($sentinel)) 'Docker failure disclosed a raw argument or configuration value.'
    }
}

$Environment = 'local'
$Mode = 'docker'
$Action = 'check'
$Config = 'PRIVATE_CONFIG_SENTINEL'
$settings = @{
    DELIVERYPILOT_STACK = 'deliverypilot-verify-cli'
    DELIVERYPILOT_SETUP_IMAGE = 'deliverypilot-setup:local'
    DELIVERYPILOT_POSTGRES_IMAGE = 'postgres:18-bookworm'
}

Compose 'platform' @('build','topology')
$prefix = @('compose','--env-file',$Config,'--project-name','deliverypilot-verify-cli-platform','-f',(Join-Path $repoRoot 'deploy/compose/platform/compose.yaml'),'-f',(Join-Path $repoRoot 'deploy/compose/platform/compose.local.yaml'),'--profile','mailpit')
Assert-Arguments $script:DockerCalls[-1] ($prefix + @('build','topology'))

$settings.DELIVERYPILOT_BUILD_BUILDER = 'PRIVATE_BUILDER_SENTINEL'
Compose 'platform' @('build','topology')
Assert-Arguments $script:DockerCalls[-1] ($prefix + @('build','--builder','PRIVATE_BUILDER_SENTINEL','topology'))
Compose 'platform' @('run','--rm','--no-deps','network-init')
Assert-Arguments $script:DockerCalls[-1] ($prefix + @('run','--rm','--no-deps','network-init'))
Compose 'platform' @('config','--quiet')
Assert-Arguments $script:DockerCalls[-1] ($prefix + @('config','--quiet'))
Write-Output 'PASS: local build override, default routing, Compose profiles and non-build isolation.'

Build-Tools
$buildTail = @('-f',(Join-Path $repoRoot 'Dockerfile.deploy'),'--build-arg','POSTGRES_IMAGE=postgres:18-bookworm','-t','deliverypilot-setup:local',$repoRoot)
Assert-Arguments $script:DockerCalls[-1] (@('build','--load','--builder','PRIVATE_BUILDER_SENTINEL') + $buildTail)
$settings.Remove('DELIVERYPILOT_BUILD_BUILDER')
Build-Tools
Assert-Arguments $script:DockerCalls[-1] (@('build','--load') + $buildTail)
$settings.DELIVERYPILOT_BUILD_BUILDER = 'PRIVATE_BUILDER_SENTINEL'
Write-Output 'PASS: setup builds use the configured builder and explicitly load their image.'

$Environment = 'server'
Compose 'identity' @('pull','api','worker')
Assert-Arguments $script:DockerCalls[-1] @('compose','--env-file',$Config,'--project-name','deliverypilot-verify-cli-identity','-f',(Join-Path $repoRoot 'deploy/compose/identity/compose.yaml'),'pull','api','worker')
$Environment = 'local'
Write-Output 'PASS: server image pulls remain independent of the local builder.'

$script:DockerExitCode = 17
Assert-Failure { Compose 'platform' @('build','topology','PRIVATE_ARGUMENT_SENTINEL') } 'Docker Compose platform build (topology) failed (exit 17)'
Assert-Failure { Compose 'identity' @('run','--rm','migration','PRIVATE_ARGUMENT_SENTINEL') } 'Docker Compose identity run (migration) failed (exit 17)'
Assert-Failure { Build-Tools } 'Docker setup-image build failed (exit 17)'
$script:DockerExitCode = 0
Write-Output 'PASS: failures identify the stage and exit code without disclosing arguments.'

$script:DockerCalls = @()
Check-Prerequisites
Assert-Arguments $script:DockerCalls[-1] @('buildx','inspect','PRIVATE_BUILDER_SENTINEL')
$script:FailBuilder = $true
Assert-Failure { Check-Prerequisites } 'Configured Docker builder check failed (exit 19)'
$Action = 'status'
$script:DockerCalls = @()
Check-Prerequisites
Assert-True (@($script:DockerCalls | Where-Object { $_[0] -eq 'buildx' }).Count -eq 0) 'Status should work even when the optional build cache is unavailable.'
Write-Output 'PASS: configured builders are checked before builds without blocking status.'
