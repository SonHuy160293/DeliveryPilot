[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Config)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not [IO.Path]::IsPathRooted($Config)) { $Config = Join-Path $repoRoot $Config }
$values = @{}
foreach ($line in [IO.File]::ReadAllLines($Config)) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
}
if ($values.DELIVERYPILOT_STACK -notmatch '^deliverypilot-verify-[a-z0-9-]+$' -or $values.DELIVERYPILOT_ENVIRONMENT -ne 'Development') {
    throw 'Gateway acceptance requires an explicitly disposable deliverypilot-verify-* Development stack.'
}
foreach ($key in $values.Keys) { [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process') }
$stack = $values.DELIVERYPILOT_STACK
$gatewayPort = if ($values.DELIVERYPILOT_GATEWAY_PORT) { $values.DELIVERYPILOT_GATEWAY_PORT } else { '19090' }
$base = 'http://127.0.0.1:' + $gatewayPort
$override = Join-Path ([IO.Path]::GetDirectoryName($Config)) 'gateway-acceptance.override.yaml'
$log = Join-Path ([IO.Path]::GetTempPath()) ($stack + '-gateway-acceptance.log')
Add-Type -AssemblyName System.Net.Http
$handler = New-Object Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(10)
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Http-Status([string]$Path, [string]$Method = 'GET', [hashtable]$Headers = @{}) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), ($base + $Path))
    try {
        if ($Method -eq 'POST') { $request.Content = [Net.Http.StringContent]::new('{}', [Text.Encoding]::UTF8, 'application/json') }
        foreach ($key in $Headers.Keys) { $null = $request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key]) }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try { return [int]$response.StatusCode } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Compose-Model([string]$Module) {
    $json = & docker compose --env-file $Config -p ($stack + '-' + $Module) -f (Join-Path $repoRoot ('deploy/compose/' + $Module + '/compose.yaml')) config --format json
    Assert-True ($LASTEXITCODE -eq 0) 'Cannot parse server Compose model.'
    return ($json | ConvertFrom-Json)
}
function Recreate([string]$Module, [string]$Service, [bool]$Production) {
    $args = @('compose','--env-file',$Config,'-p',($stack + '-' + $Module),'-f',(Join-Path $repoRoot ('deploy/compose/' + $Module + '/compose.yaml')),'-f',(Join-Path $repoRoot ('deploy/compose/' + $Module + '/compose.local.yaml')))
    if ($Production) {
        $content = "services:`n  ${Service}:`n    environment:`n      DOTNET_ENVIRONMENT: Production`n      ASPNETCORE_ENVIRONMENT: Production`n"
        if ($Module -eq 'gateway') {
            $content += "      AllowedHosts: api.example.test;localhost;127.0.0.1`n      ForwardedHeaders__AllowedHosts__0: api.example.test`n      Cors__AllowedOrigins__0: https://frontend.example.test`n"
        }
        [IO.File]::WriteAllText($override, $content)
        $args += @('-f',$override)
    }
    $preference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & docker @args up -d --no-deps --force-recreate --wait --wait-timeout 120 $Service *> $log; $result = $LASTEXITCODE }
    finally { $ErrorActionPreference = $preference }
    Assert-True ($result -eq 0) ('Cannot recreate acceptance service; inspect ' + $log)
}
$restore = $false
try {
    Assert-True ((Http-Status '/health/live') -eq 200 -and (Http-Status '/health/ready') -eq 200) 'Gateway health failed.'
    Assert-True ((Http-Status '/identity/api/v1/auth/register' 'POST') -eq 400) 'Registration route did not reach Identity.'
    Assert-True ((Http-Status '/identity/api/v1/auth/login' 'POST') -eq 400) 'Login route did not reach Identity.'
    Assert-True ((Http-Status '/notification/api/v1/webhooks/email/resend' 'POST') -eq 400) 'Webhook route did not reach Notification.'
    Assert-True ((Http-Status '/identity/api/v1/integrations/users/123' 'POST') -eq 404) 'Integration path became public.'
    Assert-True ((Http-Status '/notification/api/v1/webhooks/email/resend') -eq 405) 'Wrong method did not return 405.'
    foreach ($module in @('identity','notification')) {
        $model = Compose-Model $module
        Assert-True (-not $model.services.api.ports) 'Server API model publishes a host port.'
        & docker exec ($stack + '-gateway-gateway-1') curl --fail --silent --max-time 5 ('http://' + $module + '-api:8080/health/ready') *> $log
        Assert-True ($LASTEXITCODE -eq 0) 'Internal API alias/readiness failed.'
        $container = $stack + '-' + $module + '-api-1'
        & docker stop $container *> $log
        Assert-True ($LASTEXITCODE -eq 0) 'Cannot stop disposable API for outage verification.'
        try {
            Assert-True ((Http-Status '/health/ready') -eq 200) 'Backend outage made gateway unready.'
            if ($module -eq 'notification') {
                Assert-True ((Http-Status '/notification/api/v1/webhooks/email/resend' 'POST') -in @(502,504)) 'Unavailable Notification destination did not return a forwarding failure.'
            } else {
                Assert-True ((Http-Status '/notification/api/v1/webhooks/email/resend' 'POST') -eq 400) 'Stopping Identity interrupted Notification.'
                Assert-True ((Http-Status '/identity/api/v1/auth/register' 'POST') -in @(502,504)) 'Unavailable Identity destination did not return a forwarding failure.'
                Assert-True ((Http-Status '/identity/api/v1/auth/login' 'POST') -in @(502,504)) 'Unavailable Identity login destination did not return a forwarding failure.'
            }
        } finally {
            & docker start $container *> $log
            Assert-True ($LASTEXITCODE -eq 0) 'Cannot restore disposable API.'
            for ($attempt = 0; $attempt -lt 60; $attempt++) {
                & docker exec $container curl --fail --silent --max-time 2 http://localhost:8080/health/ready *> $log
                if ($LASTEXITCODE -eq 0) { break }
                Start-Sleep -Milliseconds 500
            }
            Assert-True ($LASTEXITCODE -eq 0) 'Restored API did not become ready.'
        }
    }
    Write-Output 'PASS: public routes, private server models, internal aliases, and outage isolation.'
    # Override only the service that belongs to the selected project.
    $restore = $true
    Recreate 'gateway' 'gateway' $true
    $headers = @{ Host='api.example.test'; 'X-Forwarded-Host'='api.example.test'; 'X-Forwarded-Proto'='https'; 'X-Forwarded-For'='198.51.100.51' }
    Assert-True ((Http-Status '/notification/api/v1/webhooks/email/resend' 'POST' $headers) -eq 400) 'Production webhook forwarding failed.'
    Assert-True ((Http-Status '/identity/api/v1/auth/register' 'POST' $headers) -eq 400) 'Production registration forwarding failed.'
    Assert-True ((Http-Status '/identity/api/v1/auth/login' 'POST' $headers) -eq 400) 'Production login forwarding failed.'
    Write-Output 'PASS: production webhook, registration, and login forwarding.'
} finally {
    if ($restore) {
        Recreate 'gateway' 'gateway' $false
    }
    $client.Dispose()
    if (Test-Path -LiteralPath $override) { Remove-Item -LiteralPath $override }
}
