[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Config, [switch]$SkipFailureInjection)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($Config)) { $Config = Join-Path $repoRoot $Config }
$values = @{}
foreach ($line in [IO.File]::ReadAllLines($Config)) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
}
if ($values.DELIVERYPILOT_STACK -notmatch '^deliverypilot-verify-[a-z0-9-]+$' -or $values.DELIVERYPILOT_ENVIRONMENT -ne 'Development') {
    throw 'Projects acceptance requires an explicitly disposable deliverypilot-verify-* Development stack.'
}
$stack = $values.DELIVERYPILOT_STACK
$base = 'http://127.0.0.1:' + $values.DELIVERYPILOT_PROJECTS_API_PORT
$identityBase = 'http://127.0.0.1:' + $values.DELIVERYPILOT_IDENTITY_API_PORT
$log = Join-Path ([IO.Path]::GetTempPath()) ($stack + '-projects-acceptance.log')
$secretRoot = $values.DELIVERYPILOT_SECRETS_DIR
if (-not [IO.Path]::IsPathRooted($secretRoot)) { $secretRoot = Join-Path $repoRoot $secretRoot }
Add-Type -AssemblyName System.Net.Http
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(60)
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Http-Json([string]$Address, [string]$Method = 'GET', [object]$Body = $null, [hashtable]$Headers = @{}, [switch]$Chunked) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), $Address)
    try {
        if ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json') }
        if ($Chunked) { $request.Headers.TransferEncodingChunked = $true }
        foreach ($key in $Headers.Keys) { $null = $request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key]) }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $envelope = if ($text) { $text | ConvertFrom-Json } else { $null }
            return @{ Status = [int]$response.StatusCode; Envelope = $envelope; NoStore = [bool]($response.Headers.CacheControl -and $response.Headers.CacheControl.NoStore); Headers = $response.Headers.ToString() }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Run-ProjectsCli([string]$Action, [bool]$ShouldFail = $false) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'deliverypilot.ps1') $Action -Module projects -Config $Config *> $log
        $result = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    Assert-True (($result -ne 0) -eq $ShouldFail) ('Unexpected Projects deployment result; inspect ' + $log)
}
function Sql([string]$Statement) {
    $result = $Statement | & docker exec -i ($stack + '-projects-postgres-1') psql -X -U projects_admin -d DeliveryPilotProjects -At -v ON_ERROR_STOP=1
    Assert-True ($LASTEXITCODE -eq 0) 'Projects database acceptance query failed.'
    return (($result | Out-String).Trim())
}
function Ready {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try { if ((Http-Json ($base + '/health/ready')).Status -eq 200) { return } } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'Projects API readiness timed out.'
}
try {
    Ready
    Assert-True ((Http-Json ($base + '/health/live')).Status -eq 200) 'Projects liveness failed.'
    $suffix = [Guid]::NewGuid().ToString('N')
    $email = 'projects-' + $suffix + '@example.test'
    $password = 'Local-test!' + [Guid]::NewGuid().ToString('N')
    $registered = Http-Json ($identityBase + '/api/v1/auth/register') 'POST' @{ displayName = 'Projects acceptance'; email = $email; password = $password }
    Assert-True ($registered.Status -eq 201) 'Cannot register disposable acceptance user.'
    $login = Http-Json ($identityBase + '/api/v1/auth/login') 'POST' @{ email = $email; password = $password }
    Assert-True ($login.Status -eq 200) 'Cannot sign in disposable acceptance user.'
    $authorization = 'Bearer ' + $login.Envelope.data.accessToken
    $headers = @{ Authorization = $authorization; 'Idempotency-Key' = [Guid]::NewGuid().ToString() }
    $workspace = Http-Json ($identityBase + '/api/v1/organizations') 'POST' @{ name = 'Projects acceptance ' + $suffix } $headers
    Assert-True ($workspace.Status -eq 201) 'Cannot create disposable workspace.'
    $organizationId = $workspace.Envelope.data.organizationId
    if (-not $organizationId) { $organizationId = $workspace.Envelope.data.id }
    $parsedOrganizationId = [Guid]::Empty
    Assert-True ([Guid]::TryParse([string]$organizationId, [ref]$parsedOrganizationId)) 'Workspace result has no identifier.'
    $route = $base + '/api/v1/organizations/' + $organizationId + '/engagements'
    $headers['Idempotency-Key'] = [Guid]::NewGuid().ToString()
    $body = @{ clientDisplayName = 'Acceptance client'; agreementReference = 'ACCEPT-' + $suffix; agreementSignedOn = '2026-11-01' }
    $created = Http-Json $route 'POST' $body $headers
    Assert-True ($created.Status -eq 201 -and $created.NoStore) 'Deployed engagement creation failed or omitted no-store.'
    $replay = Http-Json $route 'POST' $body $headers
    Assert-True ($replay.Status -eq 200 -and $replay.Envelope.data.id -eq $created.Envelope.data.id) 'Deployed creation replay changed its resource.'
    $body.clientDisplayName = 'Changed content'
    Assert-True ((Http-Json $route 'POST' $body $headers).Status -eq 409) 'Deployed key reuse was accepted.'
    $headers['Idempotency-Key'] = [Guid]::NewGuid().ToString()
    $duplicate = Http-Json $route 'POST' $body $headers
    Assert-True ($duplicate.Status -eq 201 -and $duplicate.Envelope.data.duplicateAgreementReference) 'Deployed duplicate warning failed.'
    Assert-True ((Sql ('SELECT count(*) FROM engagement."EngagementEvents" WHERE "OrganizationId" = ''' + $organizationId + ''';')) -eq '2') 'Creation did not append exactly one event per new resource.'
    $body.clientDisplayName = 'x' * 17000
    foreach ($chunked in @($false, $true)) {
        $oversized = Http-Json $route 'POST' $body $headers -Chunked:$chunked
        Assert-True ($oversized.Status -eq 413 -and $oversized.NoStore -and $oversized.Envelope.error.code -eq 'Request.PayloadTooLarge') 'Kestrel did not enforce the 16 KB ceiling for fixed-length and chunked requests.'
    }
    $preflight = Http-Json $route 'OPTIONS' $null @{ Origin = $values.DELIVERYPILOT_FRONTEND_ORIGIN; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'authorization,content-type,idempotency-key' }
    Assert-True ($preflight.Status -eq 204 -and $preflight.Headers -match '(?i)Access-Control-Allow-Headers:.*idempotency-key') 'Projects CORS rejected the creation header.'
    $putPreflight = Http-Json ($route + '/' + $created.Envelope.data.id + '/contract-value') 'OPTIONS' $null @{ Origin = $values.DELIVERYPILOT_FRONTEND_ORIGIN; 'Access-Control-Request-Method' = 'PUT'; 'Access-Control-Request-Headers' = 'authorization,content-type' }
    Assert-True ($putPreflight.Status -eq 204 -and $putPreflight.Headers -match '(?i)Access-Control-Allow-Methods:.*PUT') 'Projects CORS rejected engagement edits.'
    Write-Output 'PASS: real Identity sessions, creation/replay/conflict, duplicate warning, request ceiling, and CORS.'

    $permissions = Sql @'
SELECT NOT has_database_privilege('projects_api', 'DeliveryPilotProjects', 'CREATE')
 AND NOT has_schema_privilege('projects_api', 'engagement', 'CREATE')
 AND has_table_privilege('projects_api', 'engagement."EngagementEvents"', 'SELECT')
 AND has_table_privilege('projects_api', 'engagement."EngagementEvents"', 'INSERT')
 AND NOT has_table_privilege('projects_api', 'engagement."EngagementEvents"', 'UPDATE')
 AND NOT has_table_privilege('projects_api', 'engagement."EngagementEvents"', 'DELETE')
 AND NOT has_table_privilege('projects_api', 'engagement."EngagementEvents"', 'TRUNCATE')
 AND has_schema_privilege('projects_api', 'project', 'USAGE')
 AND NOT has_schema_privilege('projects_api', 'project', 'CREATE')
 AND has_table_privilege('projects_api', 'project."Projects"', 'SELECT')
 AND has_table_privilege('projects_api', 'project."Projects"', 'INSERT')
 AND has_table_privilege('projects_api', 'project."ProjectBriefs"', 'UPDATE')
 AND has_table_privilege('projects_api', 'project."ProjectEvents"', 'SELECT')
 AND has_table_privilege('projects_api', 'project."ProjectEvents"', 'INSERT')
 AND NOT has_table_privilege('projects_api', 'project."ProjectEvents"', 'UPDATE')
 AND NOT has_table_privilege('projects_api', 'project."ProjectEvents"', 'DELETE')
 AND NOT has_table_privilege('projects_api', 'project."ProjectEvents"', 'TRUNCATE')
 AND NOT has_schema_privilege('projects_worker', 'project', 'USAGE')
 AND NOT has_table_privilege('projects_worker', 'project."Projects"', 'SELECT')
 AND NOT EXISTS (
   SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
   WHERE n.nspname = 'project' AND c.relkind IN ('r', 'p')
     AND has_table_privilege('projects_api', c.oid, 'TRUNCATE')
 )
 AND NOT has_table_privilege('projects_worker', 'engagement."Engagements"', 'SELECT')
 AND NOT has_table_privilege('projects_worker', 'reliability."OutboxOperations"', 'UPDATE');
'@
    Assert-True ($permissions -eq 't') 'Projects runtime permissions exceed their intended scope.'
    $ownership = Sql 'SELECT bool_and(pg_get_userbyid(relowner) = ''projects_migrator'') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN (''engagement'',''project'',''reliability'') AND c.relkind = ''r'';'
    Assert-True ($ownership -eq 't') 'Projects schema objects are not owned by the migrator.'
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $appendOnly = @'
SET ROLE projects_api;
UPDATE project."ProjectEvents" SET "EventType" = "EventType" WHERE false;
'@ | & docker exec -i ($stack + '-projects-postgres-1') psql -X -U projects_admin -d DeliveryPilotProjects -v ON_ERROR_STOP=1 2>&1
        $appendOnlyExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    Assert-True ($appendOnlyExit -ne 0 -and (($appendOnly | Out-String) -match 'permission denied')) 'Projects API can update append-only project history.'

    $headers['Idempotency-Key'] = [Guid]::NewGuid().ToString()
    $project = Http-Json ($base + '/api/v1/organizations/' + $organizationId + '/projects') 'POST' @{ name = 'Deployment project'; purpose = 'Runtime grant acceptance'; kind = 'Client'; engagementId = $created.Envelope.data.id } $headers
    Assert-True ($project.Status -eq 201) 'Deployed project creation failed.'
    $gatewayRoute = 'http://127.0.0.1:' + $values.DELIVERYPILOT_GATEWAY_PORT + '/projects/api/v1/organizations/' + $organizationId + '/engagements'
    $gatewayDetail = Http-Json ($gatewayRoute + '/' + $created.Envelope.data.id) 'GET' $null $headers
    Assert-True ($gatewayDetail.Status -eq 200 -and @($gatewayDetail.Envelope.data.projects | Where-Object { $_.id -eq $project.Envelope.data.id }).Count -eq 1) 'Gateway engagement detail failed or omitted its project.'
    Assert-True ((Http-Json $gatewayRoute 'GET' $null $headers).Status -eq 200) 'Gateway engagement list failed.'
    Write-Output 'PASS: project creation and Gateway engagement detail/list with a linked project.'
    & docker exec ($stack + '-projects-worker-1') sh -c 'test ! -d /run/secrets/config && test -f /run/secrets/database-password && test -f /run/secrets/broker-password' *> $log
    Assert-True ($LASTEXITCODE -eq 0) 'Projects worker received API secrets or lacks its own credentials.'
    & docker exec ($stack + '-projects-api-1') sh -c 'test -f /run/secrets/config/Jwt__SecretKey && test -f /run/secrets/config/Integrations__Identity__ApiKey && test -f /run/secrets/config/IntegrationApi__ApiKeys__0' *> $log
    Assert-True ($LASTEXITCODE -eq 0) 'Projects API file-based keys are unavailable.'
    $composeArgs = @('compose','--env-file',$Config,'-p',($stack + '-projects'),'-f',(Join-Path $repoRoot 'deploy/compose/projects/compose.yaml'))
    $serverModel = (& docker @composeArgs config --format json) | ConvertFrom-Json
    Assert-True (-not $serverModel.services.api.ports -and -not $serverModel.services.api.build) 'Projects server model publishes a port or requires a source build.'
    $localArgs = $composeArgs + @('-f',(Join-Path $repoRoot 'deploy/compose/projects/compose.local.yaml'))
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker @localArgs run --rm --no-deps --entrypoint sh migration -c 'test ! -d /run/secrets/config && test -f /run/secrets/database-password' *> $log
        $migrationSecretExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    Assert-True ($migrationSecretExit -eq 0) 'Projects migrator received API secrets or lacks its credential.'
    Write-Output 'PASS: runtime grants, migrator ownership, consumer-specific secrets, and private server profile.'

    $ids = @{}
    foreach ($module in @('identity','notification')) { $ids[$module] = & docker inspect --format '{{.Id}}' ($stack + '-' + $module + '-api-1') }
    $ids.gateway = & docker inspect --format '{{.Id}}' ($stack + '-gateway-gateway-1')
    Run-ProjectsCli 'up'
    foreach ($module in @('identity','notification')) { Assert-True ($ids[$module] -eq (& docker inspect --format '{{.Id}}' ($stack + '-' + $module + '-api-1'))) 'Projects redeploy replaced another module.' }
    Assert-True ($ids.gateway -eq (& docker inspect --format '{{.Id}}' ($stack + '-gateway-gateway-1'))) 'Projects redeploy replaced Gateway.'
    Run-ProjectsCli 'status'
    Run-ProjectsCli 'down'
    Run-ProjectsCli 'up'
    Assert-True ((Sql ('SELECT count(*) FROM engagement."Engagements" WHERE "OrganizationId" = ''' + $organizationId + ''';')) -eq '2') 'Projects restart lost persisted engagements.'
    if (-not $SkipFailureInjection) {
        $path = Join-Path $secretRoot 'projects/migrator-password.secret'
        $original = [IO.File]::ReadAllBytes($path)
        $apiId = & docker inspect --format '{{.Id}}' ($stack + '-projects-api-1')
        try {
            [IO.File]::WriteAllText($path, ('f' * 64))
            Run-ProjectsCli 'up' $true
            Assert-True ($apiId -eq (& docker inspect --format '{{.Id}}' ($stack + '-projects-api-1'))) 'A failed Projects migration replaced the running API.'
        } finally { [IO.File]::WriteAllBytes($path, $original) }
        Run-ProjectsCli 'up'
    }
    Write-Output 'PASS: independent Projects redeploy, retained-volume restart, and migration failure gating.'

    & docker stop ($stack + '-identity-api-1') *> $log
    Assert-True ($LASTEXITCODE -eq 0) 'Cannot stop disposable Identity API.'
    try {
        $headers['Idempotency-Key'] = [Guid]::NewGuid().ToString()
        $body.clientDisplayName = 'Acceptance client'
        $outage = Http-Json $route 'POST' $body $headers
        Assert-True ($outage.Status -eq 503 -and $outage.NoStore) 'Identity outage did not fail closed with 503.'
    } finally {
        & docker start ($stack + '-identity-api-1') *> $log
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            try { if ((Http-Json ($identityBase + '/health/ready')).Status -eq 200) { break } } catch { }
            Start-Sleep -Milliseconds 500
        }
        Assert-True ((Http-Json ($identityBase + '/health/ready')).Status -eq 200) 'Identity API did not recover.'
    }
    $logout = Http-Json ($identityBase + '/api/v1/auth/logout') 'POST' $null @{ Authorization = $authorization }
    Assert-True ($logout.Status -eq 204) 'Disposable session logout failed.'
    Assert-True ((Http-Json $route 'POST' $body $headers).Status -eq 401) 'Revoked Identity session was still accepted by Projects.'
    Write-Output 'PASS: Identity outage rejection and session revocation on the deployed Projects host.'
} finally {
    $authorization = $null
    $password = $null
    $login = $null
    $client.Dispose()
}
