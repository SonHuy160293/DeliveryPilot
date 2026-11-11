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
    throw 'Engagement acceptance requires an explicitly disposable deliverypilot-verify-* Development stack.'
}
$base = 'http://127.0.0.1:' + $values.DELIVERYPILOT_GATEWAY_PORT
Add-Type -AssemblyName System.Net.Http
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Http-Json([string]$Path, [string]$Method, [object]$Body = $null, [hashtable]$Headers = @{}) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), ($base + $Path))
    try {
        if ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json') }
        foreach ($key in $Headers.Keys) { $null = $request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key]) }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return @{ Headers = $response.Headers.ToString(); Status = [int]$response.StatusCode; Envelope = if ($text) { $text | ConvertFrom-Json } else { $null }; NoStore = [bool]($response.Headers.CacheControl -and $response.Headers.CacheControl.NoStore) }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Create-Workspace([string]$Label) {
    $suffix = [Guid]::NewGuid().ToString('N')
    $email = 'engagement-' + $suffix + '@example.test'
    $password = 'Local-test!' + [Guid]::NewGuid().ToString('N')
    $registered = Http-Json '/identity/api/v1/auth/register' 'POST' @{ displayName = $Label; email = $email; password = $password }
    Assert-True ($registered.Status -eq 201) 'Cannot register disposable journey user.'
    $login = Http-Json '/identity/api/v1/auth/login' 'POST' @{ email = $email; password = $password }
    Assert-True ($login.Status -eq 200) 'Cannot sign in journey user.'
    $headers = @{ Authorization = 'Bearer ' + $login.Envelope.data.accessToken; 'Idempotency-Key' = [Guid]::NewGuid().ToString() }
    $workspace = Http-Json '/identity/api/v1/organizations' 'POST' @{ name = $Label + ' ' + $suffix } $headers
    Assert-True ($workspace.Status -eq 201) 'Cannot create disposable journey workspace.'
    $id = $workspace.Envelope.data.organizationId
    if (-not $id) { $id = $workspace.Envelope.data.id }
    Assert-True ([bool]$id) 'Workspace response has no identifier.'
    return @{ Id = $id; Headers = $headers }
}
function Check-Result([object]$Result, [int]$Status, [string]$Action) {
    Assert-True ($Result.Status -eq $Status -and $Result.NoStore) ($Action + ' failed or omitted no-store; HTTP ' + $Result.Status)
}
function Check-Isolation([string]$Path, [string]$Method, [object]$Body, [hashtable]$Headers) {
    $denied = Http-Json $Path $Method $Body $Headers
    Assert-True ($denied.Status -in @(403,404) -and $denied.NoStore) 'A different workspace user gained access to a vendor operation.'
}
try {
    $vendor = Create-Workspace 'Gateway vendor'
    $customer = Create-Workspace 'Gateway client'
    $route = '/projects/api/v1/organizations/' + $vendor.Id + '/engagements'
    $cors = Http-Json $route 'OPTIONS' $null @{ Origin = $values.DELIVERYPILOT_FRONTEND_ORIGIN; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'authorization,content-type,idempotency-key' }
    Assert-True ($cors.Status -eq 204 -and $cors.Headers -match '(?i)Access-Control-Allow-Headers:.*idempotency-key') 'Deployed Gateway CORS rejected Idempotency-Key.'
    $rejectedCors = Http-Json $route 'OPTIONS' $null @{ Origin = 'https://untrusted.example.test'; 'Access-Control-Request-Method' = 'POST' }
    Assert-True ($rejectedCors.Headers -notmatch '(?i)Access-Control-Allow-Origin:') 'Deployed Gateway allowed an unconfigured origin.'
    $body = @{ clientDisplayName = 'Gateway client'; engagementName = 'Gateway journey'; agreementReference = 'GW-' + [Guid]::NewGuid().ToString('N'); agreementSignedOn = '2026-11-01' }
    $vendor.Headers['Idempotency-Key'] = [Guid]::NewGuid().ToString()
    Check-Isolation $route 'POST' $body $customer.Headers
    $created = Http-Json $route 'POST' $body $vendor.Headers
    Check-Result $created 201 'Engagement creation'
    $item = $route + '/' + $created.Envelope.data.id
    Check-Result (Http-Json $item 'GET' $null $vendor.Headers) 200 'Engagement detail'
    Check-Isolation $item 'GET' $null $customer.Headers
    $body.expectedVersion = 1
    $body.clientDisplayName = 'Updated gateway client'
    Check-Isolation $item 'PUT' $body $customer.Headers
    Check-Result (Http-Json $item 'PUT' $body $vendor.Headers) 200 'Detail update'
    $milestone = @{ name = 'Delivery'; targetDate = '2027-01-01' }
    Check-Isolation ($item + '/milestones') 'POST' $milestone $customer.Headers
    Check-Result (Http-Json ($item + '/milestones') 'POST' $milestone $vendor.Headers) 201 'Milestone creation'
    $contact = @{ name = 'Client PO'; email = 'contact@example.test'; contactRole = 'ClientProductOwner' }
    Check-Isolation ($item + '/contacts') 'POST' $contact $customer.Headers
    Check-Result (Http-Json ($item + '/contacts') 'POST' $contact $vendor.Headers) 201 'Contact creation'
    Check-Isolation ($item + '/link-codes') 'POST' @{ expectedVersion = 4 } $customer.Headers
    $issued = Http-Json ($item + '/link-codes') 'POST' @{ expectedVersion = 4 } $vendor.Headers
    Check-Result $issued 201 'Code issuance'
    $clientRoute = '/projects/api/v1/organizations/' + $customer.Id + '/engagement-links'
    $proof = @{ code = $issued.Envelope.data.code }
    $preview = Http-Json ($clientRoute + '/preview') 'POST' $proof $customer.Headers
    Check-Result $preview 200 'Link preview'
    Assert-True (@($preview.Envelope.data.PSObject.Properties).Count -eq 2) 'Preview disclosed fields beyond the two display fields.'
    Check-Result (Http-Json ($clientRoute + '/confirm') 'POST' $proof $customer.Headers) 200 'Link confirmation'
    $linked = Http-Json $item 'GET' $null $vendor.Headers
    Check-Result $linked 200 'Confirmed detail'
    Assert-True ($linked.Envelope.data.clientOrganizationId -eq $customer.Id) 'Confirmation linked the wrong workspace.'
    Check-Isolation $item 'GET' $null $customer.Headers
    Check-Isolation ($item + '/history') 'GET' $null $customer.Headers
    Check-Result (Http-Json $clientRoute 'GET' $null $customer.Headers) 200 'Client link list'
    Check-Isolation ($item + '/close') 'POST' @{ expectedVersion = 6; reason = 'Completed' } $customer.Headers
    Check-Result (Http-Json ($item + '/close') 'POST' @{ expectedVersion = 6; reason = 'Completed' } $vendor.Headers) 200 'Engagement close'
    Check-Result (Http-Json ($clientRoute + '/' + $created.Envelope.data.id + '/remove') 'POST' @{ reason = 'Unlink' } $customer.Headers) 200 'Client removal while closed'
    Check-Isolation ($item + '/reopen') 'POST' @{ expectedVersion = 8; reason = 'Resume' } $customer.Headers
    Check-Result (Http-Json ($item + '/reopen') 'POST' @{ expectedVersion = 8; reason = 'Resume' } $vendor.Headers) 200 'Engagement reopen'
    $history = Http-Json ($item + '/history') 'GET' $null $vendor.Headers
    Check-Result $history 200 'Engagement history'
    Assert-True (@($history.Envelope.data.items).Count -eq 9) 'Journey changes were not recorded exactly once.'
    Write-Output 'PASS: Gateway engagement create/read/update, milestone, contact, code preview/confirmation, closed-link removal, close/reopen, history and workspace isolation.'
} finally {
    $client.Dispose()
}
