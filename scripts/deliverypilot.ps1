[CmdletBinding()]
param(
    [Parameter(Position = 0)][ValidateSet('init','check','up','down','status','logs')][string]$Action = 'up',
    [ValidateSet('local','server')][string]$Environment = 'local',
    [ValidateSet('all','identity','notification','gateway')][string]$Module = 'all',
    [ValidateSet('docker','host')][string]$Mode = 'docker',
    [switch]$Telemetry,
    [string]$Config
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modules = @(if ($Module -eq 'all') { 'identity'; 'notification' } elseif ($Module -ne 'gateway') { $Module })
$gateway = $Module -in @('all','gateway')
$projects = @($modules) + @(if ($gateway) { 'gateway' })
if ($Environment -eq 'server' -and $Mode -eq 'host') { throw 'Server deployments use prebuilt Docker images; host mode is local only.' }
if (-not $Config) { $Config = Join-Path $repoRoot ('deploy/env/' + $Environment + '.env') }
if (-not [IO.Path]::IsPathRooted($Config)) { $Config = Join-Path $repoRoot $Config }
$Config = [IO.Path]::GetFullPath($Config)
$settings = @{}

function Invoke-Docker([string[]]$Arguments, [string]$Operation = 'Docker operation') {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed (exit $LASTEXITCODE). Review the Docker diagnostic above. Command arguments, configuration and secret values are omitted from this error." }
}
function Compose([ValidateSet('platform','identity','notification','gateway','telemetry')][string]$Project, [string[]]$Arguments) {
    # Report only known operation/service names, never raw arguments or env values.
    $operation = "Docker Compose $Project"
    if ($Arguments.Count -gt 0 -and $Arguments[0] -in @('build','config','run','up','down','ps','logs','pull')) {
        $operation += ' ' + $Arguments[0]
    }
    $services = @($Arguments | Where-Object { $_ -in @('network-init','secret-stage','rabbitmq','broker-bootstrap','topology','mailpit','postgres','db-bootstrap','migration','db-grants','api','worker','gateway') })
    if ($services.Count -gt 0) { $operation += ' (' + ($services -join ', ') + ')' }
    if ($Environment -eq 'local' -and $Arguments.Count -gt 0 -and $Arguments[0] -eq 'build' -and $settings.DELIVERYPILOT_BUILD_BUILDER) {
        $Arguments = @('build','--builder',$settings.DELIVERYPILOT_BUILD_BUILDER) + @($Arguments | Select-Object -Skip 1)
    }
    $composeArguments = @('compose','--env-file',$Config,'--project-name',($settings.DELIVERYPILOT_STACK + '-' + $Project),'-f',(Join-Path $repoRoot ('deploy/compose/' + $Project + '/compose.yaml')))
    if ($Environment -eq 'local' -and $Project -ne 'telemetry') {
        $composeArguments += @('-f',(Join-Path $repoRoot ('deploy/compose/' + $Project + '/compose.local.yaml')))
    }
    if ($Environment -eq 'local' -and $Project -eq 'platform') { $composeArguments += @('--profile','mailpit') }
    Invoke-Docker -Arguments ($composeArguments + $Arguments) -Operation $operation
}
function Load-Settings {
    if (-not (Test-Path -LiteralPath $Config -PathType Leaf)) { throw "Configuration missing: $Config. Run init first; server values must then be supplied by the operator." }
    foreach ($line in [IO.File]::ReadAllLines($Config)) {
        if ($line -match '^\s*(#|$)') { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') { throw 'Invalid env-file syntax. Use literal unquoted KEY=value lines; shell expansion is unsupported.' }
        $key = $Matches[1]; $value = $Matches[2].Trim()
        if ($settings.ContainsKey($key)) { throw "Duplicate configuration key: $key" }
        if ($value -match '\$|^[''"'']|[\x00-\x1f]') { throw "Use a literal unquoted value without shell expansion for $key." }
        $settings[$key] = $value
        [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
    if ($settings.DELIVERYPILOT_STACK -notmatch '^deliverypilot-[a-z0-9][a-z0-9-]{0,33}$') { throw 'DELIVERYPILOT_STACK must start with deliverypilot- and contain lowercase letters, digits, and hyphens (maximum 48 characters).' }
    if ($settings.DELIVERYPILOT_BUILD_BUILDER -and $settings.DELIVERYPILOT_BUILD_BUILDER -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw 'DELIVERYPILOT_BUILD_BUILDER must be a Docker builder name containing only letters, digits, dots, underscores or hyphens.' }
    if (-not $settings.DELIVERYPILOT_SECRETS_DIR) { throw 'DELIVERYPILOT_SECRETS_DIR is required.' }
    $secretRoot = $settings.DELIVERYPILOT_SECRETS_DIR
    if (-not [IO.Path]::IsPathRooted($secretRoot)) { $secretRoot = Join-Path $repoRoot $secretRoot }
    $settings.DELIVERYPILOT_SECRETS_DIR = [IO.Path]::GetFullPath($secretRoot).Replace('\','/')
    [Environment]::SetEnvironmentVariable('DELIVERYPILOT_SECRETS_DIR', $settings.DELIVERYPILOT_SECRETS_DIR, 'Process')
    $settings.DELIVERYPILOT_OBSERVABILITY_ENABLED = if ($Telemetry) { 'true' } else { 'false' }
    $defaults = @{ DELIVERYPILOT_HTTP_SUBNET='172.31.250.0/24'; DELIVERYPILOT_GATEWAY_IP='172.31.250.10'; DELIVERYPILOT_GATEWAY_PORT='19090' }
    if ($Environment -eq 'local') {
        $defaults.DELIVERYPILOT_GATEWAY_IMAGE='deliverypilot-gateway:local'
        $defaults.DELIVERYPILOT_GATEWAY_PUBLIC_HOST='localhost'
        $defaults.DELIVERYPILOT_INGRESS_PROXY_IP='172.31.250.1'
    }
    foreach ($key in $defaults.Keys) {
        if (-not $settings[$key]) { $settings[$key] = $defaults[$key] }
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    [Environment]::SetEnvironmentVariable('DELIVERYPILOT_OBSERVABILITY_ENABLED', $settings.DELIVERYPILOT_OBSERVABILITY_ENABLED, 'Process')
    if ($Environment -eq 'local' -and $settings.DELIVERYPILOT_ENVIRONMENT -ne 'Development') { throw 'Local configuration must use DELIVERYPILOT_ENVIRONMENT=Development.' }
    if ($Environment -eq 'server' -and $modules.Count -gt 0) {
        if ($settings.DELIVERYPILOT_ENVIRONMENT -ne 'Production' -or $settings.DELIVERYPILOT_MESSAGING_DEVELOPMENT -ne 'false' -or $settings.DELIVERYPILOT_MESSAGING_USE_TLS -ne 'true' -or $settings.DELIVERYPILOT_MESSAGING_QUORUM_SIZE -notmatch '^[3-7]$') {
            throw 'Server configuration must retain Production, messaging development=false, TLS=true, and quorum size 3-7.'
        }
        if ($settings.DELIVERYPILOT_MESSAGING_HOST -in @('rabbitmq','localhost','127.0.0.1')) { throw 'Server deployments require an operator-provided TLS RabbitMQ cluster with a trusted certificate.' }
        if ($modules -contains 'notification' -and $settings.DELIVERYPILOT_EMAIL_MODE -eq 'Mailpit') { throw 'Mailpit is local only. Server email mode must be Disabled or Resend.' }
    }
}
function Build-Tools {
    $buildArguments = @('build','--load')
    if ($settings.DELIVERYPILOT_BUILD_BUILDER) { $buildArguments += @('--builder',$settings.DELIVERYPILOT_BUILD_BUILDER) }
    $buildArguments += @('-f',(Join-Path $repoRoot 'Dockerfile.deploy'),'--build-arg',('POSTGRES_IMAGE=' + $settings.DELIVERYPILOT_POSTGRES_IMAGE),'-t',$settings.DELIVERYPILOT_SETUP_IMAGE,$repoRoot)
    Invoke-Docker -Arguments $buildArguments -Operation 'Docker setup-image build'
}
function Check-Prerequisites {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Install Docker Engine/Desktop and the Docker Compose plugin first.' }
    Invoke-Docker -Arguments @('info','--format','Docker engine: {{.ServerVersion}}') -Operation 'Docker engine check'
    $composeVersion = & docker compose version --short
    if ($LASTEXITCODE -ne 0 -or $composeVersion -notmatch '(\d+)\.(\d+)\.(\d+)') { throw 'Docker Compose >=2.24.4 is required.' }
    if ([version]$Matches[0] -lt [version]'2.24.4') { throw 'Docker Compose >=2.24.4 is required.' }
    if ($Environment -eq 'local' -and $Action -in @('init','check','up') -and $settings.DELIVERYPILOT_BUILD_BUILDER) {
        Invoke-Docker -Arguments @('buildx','inspect',$settings.DELIVERYPILOT_BUILD_BUILDER) -Operation 'Configured Docker builder check' | Out-Null
    }
    if ($Mode -eq 'host') {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Host mode requires the .NET 10 SDK from global.json.' }
        & dotnet --version
        if ($LASTEXITCODE -ne 0) { throw 'Install a compatible .NET 10 SDK.' }
    }
}
function Check-Secrets {
    $required = @()
    $activeKey = if ($settings.DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID) { $settings.DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID } else { 'local-v1' }
    if ($activeKey -notmatch '^[A-Za-z0-9-]{1,64}$') { throw 'Invalid DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID; use 1-64 ASCII letters, digits, or hyphens.' }
    if ($Environment -eq 'local' -and $modules.Count -gt 0) { $required += 'platform/broker-admin-password.secret' }
    foreach ($name in $modules) {
        $required += "platform/$name-broker-password.secret"
        if ($name -eq 'identity') {
            $required += @('admin-password','migrator-password','api-password','worker-password','jwt-key','integration-key','invitation-dispatch-key') | ForEach-Object { "identity/$_.secret" }
            $required += "notification/keys/$activeKey.public.pem"
        } else {
            $required += @('admin-password','migrator-password','runtime-password') | ForEach-Object { "notification/$_.secret" }
            $required += 'identity/invitation-dispatch-key.secret'
            $required += @("notification/keys/$activeKey.private.pem","notification/keys/$activeKey.public.pem",'notification/keys/recipient-hmac.secret')
            if ($settings.DELIVERYPILOT_EMAIL_MODE -eq 'Resend') { $required += @('notification/keys/resend-api.secret','notification/keys/resend-webhook.secret') }
        }
    }
    if ($Telemetry) { $required += 'platform/grafana-admin-password.secret' }
    foreach ($relative in $required) {
        $path = Join-Path $settings.DELIVERYPILOT_SECRETS_DIR $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required secret file missing: $relative. Run local init or provision server secrets." }
        try { $content = [IO.File]::ReadAllText($path) } catch { throw "Secret file is unreadable: $relative" }
        if ([string]::IsNullOrWhiteSpace($content) -or $content -match 'CHANGE_ME|REPLACE_WITH') { throw "Secret file is empty or a placeholder: $relative" }
        if ($relative -eq 'identity/jwt-key.secret' -and $content.Trim().Length -lt 32) { throw 'JWT signing key must be at least 32 characters.' }
        if ($relative -eq 'identity/integration-key.secret' -and $content.Trim().Length -gt 4096) { throw 'Integration key must be at most 4096 characters.' }
        if ($relative -match '^(identity|notification)/(admin|migrator|api|worker|runtime)-password.secret$' -and $content.Trim() -notmatch '^[0-9a-fA-F]{64}$') { throw "Database password must be 32 random bytes encoded as hex: $relative" }
        $content = $null
    }
}
function Check-Configuration {
    foreach ($entry in $settings.GetEnumerator()) {
        if ($entry.Key -in @('DELIVERYPILOT_GATEWAY_IMAGE','DELIVERYPILOT_GATEWAY_PUBLIC_HOST','DELIVERYPILOT_INGRESS_PROXY_IP') -and -not $gateway) { continue }
        if ($modules.Count -eq 0 -and $entry.Key -match '^DELIVERYPILOT_(MESSAGING|JWT|EMAIL)_') { continue }
        if ($entry.Key -match '^DELIVERYPILOT_(IDENTITY|NOTIFICATION)_(API|WORKER|MIGRATION)_IMAGE$' -and $modules -notcontains $Matches[1].ToLowerInvariant()) { continue }
        if ($entry.Value -match 'CHANGE_ME|REPLACE_WITH|registry\.example') { throw "Replace the placeholder in $($entry.Key) before proceeding." }
        if ($entry.Key -match '_PORT$' -and ($entry.Value -notmatch '^\d+$' -or [int]$entry.Value -lt 1 -or [int]$entry.Value -gt 65535)) { throw "Invalid TCP port: $($entry.Key)" }
    }
    if ($Environment -eq 'server') {
        $imageKeys = @('DELIVERYPILOT_SETUP_IMAGE')
        if ($gateway) { $imageKeys += 'DELIVERYPILOT_GATEWAY_IMAGE' }
        foreach ($name in $modules) { foreach ($kind in @('API','WORKER','MIGRATION')) { $imageKeys += ('DELIVERYPILOT_' + $name.ToUpperInvariant() + '_' + $kind + '_IMAGE') } }
        foreach ($key in $imageKeys) {
            $value = $settings[$key]
            if (-not $value -or $value -match ':(latest|local|dev|main)$' -or $value -notmatch '(@sha256:[a-f0-9]{64}$|:[A-Za-z0-9][A-Za-z0-9_.-]+$)') { throw "Use an immutable release tag or digest for $key." }
        }
    }
    Compose 'platform' @('config','--quiet')
    foreach ($name in $projects) { Compose $name @('config','--quiet') }
    if ($Telemetry) { Compose 'telemetry' @('config','--quiet') }
    if ($gateway -and $Environment -eq 'server' -and (-not $settings.DELIVERYPILOT_INGRESS_PROXY_IP -or -not $settings.DELIVERYPILOT_GATEWAY_PUBLIC_HOST -or $settings.DELIVERYPILOT_GATEWAY_PUBLIC_HOST -match '\*|[/:;\s]')) {
        throw 'Server gateway requires an explicit ingress peer IP and a public API hostname.'
    }
    if ($gateway -and $Environment -eq 'server') {
        $origin = $null
        if (-not [Uri]::TryCreate($settings.DELIVERYPILOT_FRONTEND_ORIGIN, [UriKind]::Absolute, [ref]$origin) -or $origin.Scheme -ne 'https' -or $origin.UserInfo -or $origin.GetLeftPart([UriPartial]::Authority) -ne $settings.DELIVERYPILOT_FRONTEND_ORIGIN) {
            throw 'Server frontend origin must be an exact HTTPS origin without credentials, path, query, fragment, or trailing slash.'
        }
    }
    $ids = @(& docker network ls --quiet)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker networks.' }
    $networks = if ($ids.Count -gt 0) { & docker network inspect @ids } else { '[]' }
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker network subnets.' }
    $networks | & docker run --rm -i --env-file $Config --env ('DELIVERYPILOT_STACK=' + $settings.DELIVERYPILOT_STACK) --env ('DELIVERYPILOT_HTTP_SUBNET=' + $settings.DELIVERYPILOT_HTTP_SUBNET) --env ('DELIVERYPILOT_GATEWAY_IP=' + $settings.DELIVERYPILOT_GATEWAY_IP) --env ('DELIVERYPILOT_INGRESS_PROXY_IP=' + $(if ($gateway) { $settings.DELIVERYPILOT_INGRESS_PROXY_IP } else { '' })) $settings.DELIVERYPILOT_SETUP_IMAGE check-network
    if ($LASTEXITCODE -ne 0) { throw 'HTTP network preflight failed.' }
    Check-Secrets
}
function Check-Ports {
    $ports = @{}
    if ($Environment -eq 'local' -and $modules.Count -gt 0) {
        foreach ($key in @('DELIVERYPILOT_RABBITMQ_AMQP_PORT','DELIVERYPILOT_RABBITMQ_MANAGEMENT_PORT')) { $ports[$key] = 'platform' }
        if ($modules -contains 'notification' -and $settings.DELIVERYPILOT_EMAIL_MODE -eq 'Mailpit') {
            foreach ($key in @('DELIVERYPILOT_MAILPIT_HTTP_PORT','DELIVERYPILOT_MAILPIT_SMTP_PORT')) { $ports[$key] = 'platform' }
        }
        foreach ($name in $modules) { $ports[('DELIVERYPILOT_' + $name.ToUpperInvariant() + '_POSTGRES_PORT')] = $name }
    }
    if ($Mode -eq 'docker' -and $Environment -eq 'local') { foreach ($name in $modules) { $ports[('DELIVERYPILOT_' + $name.ToUpperInvariant() + '_API_PORT')] = $name } }
    if ($gateway -and $Mode -eq 'docker') { $ports.DELIVERYPILOT_GATEWAY_PORT = 'gateway' }
    if ($Telemetry) { foreach ($key in @('DELIVERYPILOT_GRAFANA_PORT','DELIVERYPILOT_OTLP_PORT','DELIVERYPILOT_OTLP_HTTP_PORT')) { $ports[$key] = 'telemetry' } }
    $seen = @{}
    $running = & docker ps --format '{{.Names}}|{{.Ports}}'
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect existing Docker port bindings.' }
    foreach ($key in $ports.Keys) {
        $port = [int]$settings[$key]
        if ($seen.ContainsKey($port)) { throw "Port $port is assigned more than once ($key and $($seen[$port]))." }
        $seen[$port] = $key
        $expectedProject = $settings.DELIVERYPILOT_STACK + '-' + $ports[$key]
        $owned = @($running | Where-Object { $_.StartsWith($expectedProject + '-') -and $_ -match (':' + $port + '->') }).Count -gt 0
        if ($owned) { continue }
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, $port)
        try { $listener.Start() } catch { throw "Port $port ($key) is occupied by another process/project. Change it in $Config." } finally { $listener.Stop() }
    }
}
function Set-HostSecrets {
    if ($gateway) {
        $gatewaySettings = & docker run --rm --env-file $Config --env ('DELIVERYPILOT_HOST_SECRETS_DIR=' + $settings.DELIVERYPILOT_SECRETS_DIR) --env ('DELIVERYPILOT_OBSERVABILITY_ENABLED=' + $settings.DELIVERYPILOT_OBSERVABILITY_ENABLED) --env ('DELIVERYPILOT_GATEWAY_PORT=' + $settings.DELIVERYPILOT_GATEWAY_PORT) $settings.DELIVERYPILOT_SETUP_IMAGE host-settings gateway
        if ($LASTEXITCODE -ne 0) { throw 'Cannot prepare gateway IDE configuration.' }
        $gatewaySettings | & dotnet user-secrets set --project (Join-Path $repoRoot 'src/Gateway/DeliveryPilot.Gateway') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot configure gateway IDE settings.' }
    }
    foreach ($name in $modules) {
        foreach ($executable in @('api','worker','migrationrunner')) {
            $hostKind = "$name-$executable"
            $moduleTitle = [Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($name)
            $projectName = 'DeliveryPilot.' + $moduleTitle + '.' + @{api='API';worker='Worker';migrationrunner='MigrationRunner'}[$executable]
            $project = Join-Path $repoRoot ('src/Modules/DeliveryPilot.' + $moduleTitle + '/' + $projectName)
            $settingsJson = & docker run --rm --mount ('type=bind,source=' + $settings.DELIVERYPILOT_SECRETS_DIR + ',target=/source,readonly') --env-file $Config --env ('DELIVERYPILOT_HOST_SECRETS_DIR=' + $settings.DELIVERYPILOT_SECRETS_DIR) --env ('DELIVERYPILOT_OBSERVABILITY_ENABLED=' + $settings.DELIVERYPILOT_OBSERVABILITY_ENABLED) $settings.DELIVERYPILOT_SETUP_IMAGE host-settings $hostKind
            if ($LASTEXITCODE -ne 0) { throw "Cannot prepare IDE configuration for $hostKind." }
            $settingsJson | & dotnet user-secrets set --project $project | Out-Null
            $settingsJson = $null
            if ($LASTEXITCODE -ne 0) { throw "Cannot configure the user-secrets store for $hostKind." }
        }
    }
    Write-Output 'Selected IDE user-secrets stores configured. Use the Local launch profiles (Development).'
}

Push-Location $repoRoot
try {
    if ($Action -eq 'init') {
        if (-not (Test-Path -LiteralPath $Config)) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Config)) | Out-Null
            Copy-Item -LiteralPath (Join-Path $repoRoot ('deploy/env/' + $Environment + '.env.example')) -Destination $Config
        }
        Load-Settings
        Check-Prerequisites
        [IO.Directory]::CreateDirectory($settings.DELIVERYPILOT_SECRETS_DIR) | Out-Null
        if ($Environment -eq 'local') {
            Build-Tools
            if ($modules.Count -gt 0) { Invoke-Docker -Arguments @('run','--rm','--mount',('type=bind,source=' + $settings.DELIVERYPILOT_SECRETS_DIR + ',target=/source'),$settings.DELIVERYPILOT_SETUP_IMAGE,'init-secrets') -Operation 'Docker local secret initialization' }
            if ($Mode -eq 'host') { Set-HostSecrets }
        } else { Write-Output 'Server template created. Supply immutable image references and operator-provisioned secret files before check/up.' }
        Write-Output "Configuration ready: $Config. Existing secrets and data were preserved."
    } else {
        Load-Settings
        Check-Prerequisites
        if ($Action -in @('check','up')) {
            Check-Configuration
            Check-Ports
        }
        switch ($Action) {
            'check' { Write-Output 'Configuration, Compose models, secret files, and selected port bindings passed preflight.' }
            'up' {
                if ($Environment -eq 'local') {
                    Build-Tools
                    Compose 'platform' @('run','--rm','--no-deps','network-init')
                    if ($modules.Count -gt 0) {
                    Compose 'platform' @('build','topology')
                    Compose 'platform' @('up','-d','--wait','--wait-timeout','180','rabbitmq')
                    Compose 'platform' @('run','--rm','--no-deps','broker-bootstrap')
                    Compose 'platform' @('run','--rm','--no-deps','topology')
                    if ($modules -contains 'notification' -and $settings.DELIVERYPILOT_EMAIL_MODE -eq 'Mailpit') { Compose 'platform' @('up','-d','--wait','mailpit') }
                    }
                } else {
                    Compose 'platform' @('pull','network-init')
                    Compose 'platform' @('run','--rm','--no-deps','network-init')
                }
                if ($Telemetry) { Compose 'telemetry' @('up','-d','--wait','--wait-timeout','180') }
                foreach ($name in $modules) {
                    if ($Environment -eq 'local') {
                        $buildTargets = if ($Mode -eq 'docker') { @('migration','api','worker') } else { @('migration') }
                        Compose $name (@('build') + $buildTargets)
                    } else { Compose $name @('pull','secret-stage','postgres','db-bootstrap','migration','db-grants','api','worker') }
                    Compose $name @('run','--rm','--no-deps','secret-stage')
                    Compose $name @('up','-d','--wait','--wait-timeout','120','postgres')
                    Compose $name @('run','--rm','--no-deps','db-bootstrap')
                    Compose $name @('run','--rm','--no-deps','migration')
                    Compose $name @('run','--rm','--no-deps','db-grants')
                    if ($Mode -eq 'docker') { Compose $name @('up','-d','--no-deps','--force-recreate','--wait','--wait-timeout','180','api','worker') }
                }
                if ($Mode -eq 'host') { Set-HostSecrets }
                if ($gateway -and $Mode -eq 'docker') {
                    if ($Environment -eq 'local') { Compose 'gateway' @('build','gateway') } else { Compose 'gateway' @('pull','gateway') }
                    Compose 'gateway' @('up','-d','--wait','--wait-timeout','180','gateway')
                }
                Write-Output "Selected modules ready ($Environment/$Mode). Existing data and credentials were preserved."
            }
            'down' {
                if ($gateway) { Compose 'gateway' @('down','--remove-orphans') }
                foreach ($name in $modules) { Compose $name @('down','--remove-orphans') }
                if ($Module -eq 'all') {
                    Compose 'telemetry' @('down','--remove-orphans')
                    Compose 'platform' @('down','--remove-orphans')
                }
                Write-Output 'Selected containers stopped; volumes and source secret files retained.'
            }
            'status' { foreach ($name in (@('platform') + $projects + @('telemetry'))) { Compose $name @('ps','--all') } }
            'logs' { foreach ($name in (@('platform') + $projects)) { Compose $name @('logs','--tail','100') } }
        }
    }
} finally { Pop-Location }
