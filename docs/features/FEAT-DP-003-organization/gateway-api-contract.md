# Organization Gateway API contract

Start with the [frontend integration playbook](frontend-integration.md) for implementation order and cache/security recipes. This reference covers the **30 implemented FEAT-DP-003 operations** through YARP, not internal Identity URLs.

Verified against backend revision `dd66c28` plus the existing uncommitted tasks 011–015/014 implementation, on 2026-11-05 (Asia/Bangkok). Frontend conventions were inspected read-only in sibling DeliveryPilot-Web at `c149a24`. Examples are illustrative, fictional, and source-checked; they are not live captured responses or new execution evidence. Recheck current source when either checkout changes. This document records implementation; it does not approve requirements or authorize FE route/dependency changes.

## navigation

- [Connection and cURL setup](#setup)
- [Common protocol, validation and versions](#protocol)
- [Shared response shapes](#shapes)
- [Errors and frontend recovery](#errors)
- [Registered audit changes](#audit-changes)
- [Source evidence and maintenance](#sources)
- [Operation inventory](#inventory); each operation has its own stable anchor below.

## setup

**Public base:** `$GatewayOrigin/identity/api/v1`. Local default Gateway origin is `http://127.0.0.1:19090`; use the actual deployed origin/port. Identity's development port 19091 is not the public integration target. Do not expose service integration keys to Vite/browser code.

In DeliveryPilot-Web, `VITE_API_BASE_URL=/identity/` and the existing Vite proxy target the Gateway without stripping this prefix. Axios endpoint strings are relative, e.g. `api/v1/me/organizations`, with no leading slash and no second identity prefix. Production should normally serve SPA and Gateway under the same origin. Direct cross-origin access needs the configured exact origin; wildcard/credential assumptions are invalid.

The existing FE sign-in is connected: `POST /identity/api/v1/auth/login` with `{email,password}` returns the common envelope with data `{userId,accessToken,tokenType:"Bearer",expiresAt}` and a Secure/HttpOnly/SameSite=Strict refresh cookie. Registration `POST /identity/api/v1/auth/register` returns 201 `{userId}` inside the envelope and does not sign in; FE registration UI is still a preview. Since 2026-11-09 the backend also publishes `POST /identity/api/v1/auth/refresh` (sends the refresh cookie; returns the same data shape plus a rotated cookie, or a generic 401 that clears the cookie) and `POST /identity/api/v1/auth/logout` (send the bearer and the cookie; always 204 and clears the cookie). See [TASK-DP-002-005](../FEAT-DP-002-user-initialization/tasks/TASK-DP-002-005.md). FE wiring and automatic refresh are not implemented yet. Reuse the FE memory session/login client; ordinary Organization calls use the bearer token, not credentialed cookies. The Gateway CORS policy has AllowCredentials=false, so use the existing same-origin/Vite-proxy topology for cookie-bearing sign-in rather than assuming cross-origin cookie support. Do not weaken cookie flags for local testing.

Run the following once in **PowerShell 7**. Examples use `curl.exe` explicitly (not Windows PowerShell's historical curl alias). JSON bodies go through UTF-8 stdin to avoid native JSON argument quoting differences. Substitute real IDs/versions from authorized responses and a short-lived test access token from sign-in. These are operation examples, not a script to run every mutation sequentially against fictional data.

```powershell
$GatewayOrigin = 'http://127.0.0.1:19090'
$AccessToken = Read-Host 'Paste a disposable test access token (do not commit it)' -MaskInput
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OrganizationId = '019a0000-0000-7000-8000-000000000001'
$MembershipId = '019a0000-0000-7000-8000-000000000002'
$RoleId = '019a0000-0000-7000-8000-000000000005'
$MemberRoleId = '019a0000-0000-7000-8000-000000000006'
$InvitationId = '019a0000-0000-7000-8000-000000000007'
$NominationId = '019a0000-0000-7000-8000-000000000008'
$AttemptId = [guid]::NewGuid().ToString() # retain for THIS creation intent
$ExpectedVersion = 1 # replace with the matching latest role/invitation/nomination version
$ExpectedMembershipVersion = 1 # replace with the target membership.version
$Proof = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' # fictional, fails real verification
```

`--include` prints headers to inspect status, no-store, correlation and Retry-After. Omit it if capturing response JSON into a parser. Do not put proofs/passwords/tokens in shared terminal transcripts or checked-in example data. Never add curl --retry for these mutations. A cURL example uses the signed-in actor appropriate to that operation; recipient and Owner examples require different accounts/tokens.

## protocol

All 30 operations require `Authorization: Bearer <access token>`. JSON mutation bodies use `Content-Type: application/json`; request objects reject unknown fields. Gateway body ceiling is **16,384 bytes** and route timeout **30 seconds**. The existing FE timeout defaults to 15 seconds: a client timeout/cancellation does not establish whether a write committed. The client, Gateway or connection can time out independently; reconcile using documented reads/replays rather than inventing a fresh write.

Private success/error responses are `Cache-Control: no-store`, including Gateway-generated failures. This is an HTTP caching instruction; scoped, nonpersisted TanStack memory state remains a separate concern. Do not persist sensitive query/mutation state. CORS permits GET/POST/PUT and Content-Type, Authorization, Idempotency-Key, X-Correlation-ID, traceparent, tracestate; exposes X-Correlation-ID and Retry-After. A preflight OPTIONS response is not a new business operation. Private integration/dispatch URLs are not published; unknown paths return Gateway 404 and unsupported methods 405. Path identifiers are GUID-constrained; valid GUID format does not imply access.

Optional outbound X-Correlation-ID may be a UUID; the server normalizes it and returns a correlation identifier. Do not fabricate a required workspace header, user ID, actor, sponsor, tenant or role claim. Identity derives subject/session/current authority from server facts. All mandatory scope/action checks are independent of frontend visibility.

**Wire types:** UUIDs are strings; timestamps are ISO-8601 instants (UTC in response, display locally as appropriate). Nullable fields are explicit null in the illustrated default serialization. Versions are positive JSON integer numbers backed by .NET long; FE must reject unsafe integers rather than round/convert a version. No version-as-string wire contract exists. Use Zod/runtime checks on unknown response data; TypeScript generics alone do not validate JSON. Response additions should not silently become request fields.

| Value to submit | Source |
|---|---|
| Idempotency-Key | New UUID once per workspace creation intent; not a response version |
| expectedMembershipVersion (grant/revoke/remove/demote) | Target Member.version or latest membership mutation version |
| expectedVersion (role update/retire) | Role.version, never permissionSetVersion |
| expectedVersion (resend/reissue/cancel) | Invitation.version |
| expectedVersion (recipient accept/decline) | InvitationPreview.version |
| expectedVersion (nomination responses) | Nomination.version |

Do not increment versions locally: one operation can stage several membership/role revisions. A nomination response lacks the new membership version; refetch member-me. RoleBadge has no version. creationSnapshot records historical versions, not current edit intent.

Safe audit reasons: use a concise business explanation, at most 1,000 characters, nonblank when supplied. Required/optional rules are operation-specific. Current policy rejects URLs, email-like text, `Bearer ` and `token=`, `password=`, `secret=` material, case-insensitively. Never ask the user to paste invitations/credentials into a reason. Final server validation remains authoritative.

Source-checked edge: ordinary grant/revoke validation currently checks reason length/nonblank before the handler but does not run the full sensitive-content policy there. On a changed assignment, unsafe reason content can fail during required audit construction and surface as a sanitized 500 rather than uniform 400; an unchanged no-audit replay can follow a different path. Keep client reasons safe, do not depend on one status for every invalid reason, and track any desired backend harmonization separately. This documentation does not change that implementation.

Lists use `{items,nextCursor}`. Default limit is 50, maximum 100; request only positive limits. Omit a missing cursor instead of sending an empty string. Cursors are opaque, up to 1,024 characters: do not decode, edit or reuse across scopes/filters/modes. Preserve all filters when continuing and reset pages on filter changes. Stop when nextCursor is null. Generic lists use created-time/ID keysets; audit has its own mode/filter-bound cursor. Continuation is not a durable snapshot while concurrent changes occur.

## shapes

Every success response is `{success:true,data:<shape>,error:null}`. Every normal error is `{success:false,data:null,error:{code,message,errors,traceId}}`. errors and traceId are nullable; errors is a dictionary of string arrays, not guaranteed to be keyed by a form field (CQRS validation can use message text as keys). Map only known fields and use safe local general copy. Do not depend on the error.message wording or log raw transport bodies. Page<T> wraps the relevant shape as items, with nullable string nextCursor. Each endpoint below includes a full illustrative envelope.

| Shape | Exact fields and important distinctions |
|---|---|
| RoleBadge | id:UUID, code:string, name:string. Built-in codes owner/admin/member; display names Owner/Admin/Member. Custom code is server supplied. A badge is not a comprehensive capability claim. |
| Workspace | organizationId:UUID, name:string, roles:RoleBadge[], membershipId:UUID, version:positive integer (workspace), membershipVersion:positive integer. No slug, counters or status field. |
| CreationSnapshot | organizationId, name, status:"active", membershipId, roles:string[] (names, e.g. Owner), version, membershipVersion, resultKind:"creationSnapshot", snapshotAt:timestamp. Different shape from Workspace; historical, exact authorized replay. |
| Member | membershipId:UUID, userId:UUID, displayName:string, primaryEmail:string, status:active/removed/suspended, roles:RoleBadge[], joinedAt:timestamp, version:positive integer. Email requires the relevant member/self read admission. |
| Role | id:UUID, code:string, name:string, description:string/null, origin:system/custom, permissionCodes:string[], version, permissionSetVersion, retiredAt:timestamp/null. No templateVersion in this response. Built-ins may have an empty permissionCodes array; creating custom roles requires a nonempty one. |
| DelegablePermissions | codes:string[]: currently held delegable subset; not all effective permissions or all action permissions. |
| AssignmentChange | membershipId:UUID, version, roles:RoleBadge[], changed:boolean. No status or membershipVersion property. |
| MembershipChange | membershipId:UUID, status:string, roles:RoleBadge[], version, changed:boolean. Removed response roles is empty; never merge in historical grants. |
| OfferedRole | roleId:UUID, name:string (frozen offer name), permissionSetVersion:positive integer. Different keys from RoleBadge. |
| Delivery | status:string, failureCode:string/null, updatedAt:timestamp. No provider address, proof or payload. |
| Invitation | id:UUID, organizationId:UUID, recipientEmail:string, status:string, offeredRoles:OfferedRole[], expiresAt:timestamp, version, delivery:Delivery. No proof, verifier, createdAt, issuer or replacement-link field. |
| InvitationPreview | invitationId:UUID, organizationId:UUID, workspaceName:string, offeredRoles:OfferedRole[], expiresAt:timestamp, version, status:string, canAccept:boolean, requiresReissue:boolean. No recipient email or membership version. |
| InvitationAcceptance | outcome:accepted/alreadyAccepted, membershipId:UUID, organizationId:UUID, roles:RoleBadge[], membershipVersion:positive integer. No invitation.version or changed property. |
| InvitationDecline | outcome:"declined", status:"declined", version (invitation). |
| Nomination | id:UUID, organizationId:UUID, membershipId:UUID, nominatedByUserId:UUID, status:string, createdAt:timestamp, expiresAt:timestamp, version, canAccept:boolean. No member version, email, proof or Owner role snapshot. |
| AuditEvent | eventId:UUID, actorUserId:UUID/null, actorDisplayName:string/null, organizationId:UUID, targetType:string, targetId:UUID, action:string, occurredAt:timestamp, outcome:"succeeded", aggregateVersion:positive integer, membershipVersion:positive integer/null, eventOrdinal:positive integer, reason:string/null, changes:typed object. No detailsSchemaVersion, correlation ID or raw Details. |

Invitation and nomination persisted status values: pending, accepted, declined, cancelled, expired. Deadline passage does not itself guarantee a persisted expired status. Delivery values: queued, retrying, provider-accepted, delivered, bounced, complained, failed, suppressed, expired, obsolete. Delivery/provider acceptance is not membership acceptance, and queued is not proof of email delivery. A failed delivery can coexist with a valid pending invitation.

### Delegable permission codes

```json
["organization.members.read","organization.members.remove","organization.invitations.read","organization.invitations.issue","organization.invitations.resend","organization.invitations.reissue","organization.invitations.cancel","organization.roles.manage","organization.roles.assign","organization.audit.read"]
```

`organization.ownership.manage` is protected and is not delegated through custom roles. Membership roles, role-catalog read, invitation-read, member-read and audit-read are separate facts; do not derive a full permission map from this list or default role names.

## errors

Use the FE ApiError normalizer: preserve bounded code/trace ID/known validation fields and retryAfterMs, show local safe messages, and distinguish authorization from authentication. Correlation information can be shown as support text without displaying hidden server details. All operations can fail at the Gateway before Identity runs.

| Status | Representative codes | FE handling |
|---|---|---|
| 400 | Validation.Failure, Membership.InvalidStatus, Invitation.InvalidRoles, Invitation.InvalidRequest | Fix known fields; generic validation summary for non-field keys. Invalid/oversized cursor is 400. Bad route GUID format may never enter the handler. |
| 401 | Request.Unauthorized; Authentication.Required / Authentication.InvalidSession when raised by scoped services | Let existing session-generation-aware handling clear the matching memory session. No automatic refresh or request replay. |
| 403 | Organization.AccessDenied; recipient Invitation.Unavailable | No scope/target enumeration. Do not clear the global session solely for 403; clear affected scoped views and offer permitted navigation. |
| 404 | Membership.NotFound, Organization.RoleNotFound, Invitation.NotFound; Gateway.NotFound | Only show target absence where authorized. Denied callers may instead receive 403. |
| 405 | Gateway.MethodNotAllowed | Contract/method mismatch; do not change to a guessed method. |
| 409 | Database.Concurrency; Organization.CreationAttemptConflict / CreationAttemptExpired / MembershipStale / MembershipInvalidTransition / OwnerContinuityRequired / RoleStale / RoleImmutable / RoleConflict / RoleNameReserved / RoleInUse; Invitation.PendingOfferExists / RecipientUnavailable / OfferChanged / Closed; OwnershipNomination.PendingExists / AlreadyOwner / RecipientUnavailable / SponsorUnavailable / Stale / Closed | Reload the relevant authorized state and reconcile intent, not blind resubmit. RecipientUnavailable deliberately does not identify a prior offer or member state. Owner continuity needs an additional eligible Owner before step-down. |
| 413 | Gateway.PayloadTooLarge, Request.TooLarge (HTTP body boundary) | Report request too large; do not retry automatically or display raw exception detail. Existing FE may normalize this as generic http; branch safely on details.status. |
| 429 | Invitation.RateLimited | Positive Retry-After seconds, rounded up. FE already exposes retryAfterMs; disable the issuance action until then and require deliberate retry. No proof/deadline/ledger mutation on a quota-blocked attempt. Defaults for workspace + normalized recipient: cooldown 60 seconds and rolling-24-hour limit 10, workspace rolling-hour limit 100; server configuration can change these, so honor the header. |
| 500 | Server.Unhandled, Database.UpdateFailed | Safe failure; no fabricated success. Missing invitation staging/configuration currently produces Server.Unhandled, not an invented delivery-unavailable code. An interrupted response can leave the client uncertain; use documented read/replay recovery. |
| 502/503/504 | Gateway.ForwardingFailed / Gateway.Unavailable / Gateway.Timeout; Organization.AuthorityUnavailable | Safe dependency/outage presentation. No authority inference or automatic mutation retry. Exact API status/code depends on the originating boundary. |

Illustrative conflict envelope (the message is not a client matching key):

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Organization.MembershipStale",
    "message": "The membership has changed; refresh before editing.",
    "errors": null,
    "traceId": "019a0000-0000-7000-8000-000000000030"
  }
}
```


## inventory

All operations below require a currently valid bearer identity/session. Workspace admission and action authority are rechecked by Identity; IDs, role names, cached UI state and token platform claims grant no authority.

| Operation | Method | Public Gateway path |
|---|---|---|
| [Create a workspace](#org-create) | POST | `/identity/api/v1/organizations` |
| [Discover my eligible workspaces](#org-list-mine) | GET | `/identity/api/v1/me/organizations` |
| [Read my current membership](#member-me) | GET | `/identity/api/v1/organizations/{organizationId}/members/me` |
| [List workspace memberships](#member-list) | GET | `/identity/api/v1/organizations/{organizationId}/members` |
| [Read one membership](#member-detail) | GET | `/identity/api/v1/organizations/{organizationId}/members/{membershipId}` |
| [Grant an ordinary role](#member-grant-role) | POST | `/identity/api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/grant` |
| [Revoke an ordinary role](#member-revoke-role) | POST | `/identity/api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/revoke` |
| [Remove a membership](#member-remove) | POST | `/identity/api/v1/organizations/{organizationId}/members/{membershipId}/remove` |
| [Demote an Owner](#member-demote-owner) | POST | `/identity/api/v1/organizations/{organizationId}/members/{membershipId}/demote-owner` |
| [Read the delegable permission subset](#permissions-delegable) | GET | `/identity/api/v1/organizations/{organizationId}/permissions` |
| [List role definitions](#role-list) | GET | `/identity/api/v1/organizations/{organizationId}/roles` |
| [Create a custom role](#role-create) | POST | `/identity/api/v1/organizations/{organizationId}/roles` |
| [Update a custom role](#role-update) | PUT | `/identity/api/v1/organizations/{organizationId}/roles/{roleId}` |
| [Retire a custom role](#role-retire) | POST | `/identity/api/v1/organizations/{organizationId}/roles/{roleId}/retire` |
| [Issue an invitation](#invitation-issue) | POST | `/identity/api/v1/organizations/{organizationId}/invitations` |
| [List manager-visible invitations](#invitation-list) | GET | `/identity/api/v1/organizations/{organizationId}/invitations` |
| [Read manager invitation detail](#invitation-detail) | GET | `/identity/api/v1/organizations/{organizationId}/invitations/{invitationId}` |
| [Resend the unchanged offer](#invitation-resend) | POST | `/identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/resend` |
| [Replace an invitation with a fresh offer](#invitation-reissue) | POST | `/identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/reissue` |
| [Cancel a pending invitation](#invitation-cancel) | POST | `/identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/cancel` |
| [Preview my invitation](#recipient-preview) | POST | `/identity/api/v1/organization-invitations/{invitationId}/preview` |
| [Accept my invitation](#recipient-accept) | POST | `/identity/api/v1/organization-invitations/{invitationId}/accept` |
| [Decline my invitation](#recipient-decline) | POST | `/identity/api/v1/organization-invitations/{invitationId}/decline` |
| [Nominate an additional Owner](#nomination-create) | POST | `/identity/api/v1/organizations/{organizationId}/ownership-nominations` |
| [List workspace ownership nominations](#nomination-list) | GET | `/identity/api/v1/organizations/{organizationId}/ownership-nominations` |
| [Discover my ownership nominations](#nomination-list-mine) | GET | `/identity/api/v1/me/ownership-nominations` |
| [Accept my Owner nomination](#nomination-accept) | POST | `/identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/accept` |
| [Decline my Owner nomination](#nomination-decline) | POST | `/identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/decline` |
| [Cancel an Owner nomination](#nomination-cancel) | POST | `/identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/cancel` |
| [Read permitted workspace audit history](#audit-list) | GET | `/identity/api/v1/organizations/{organizationId}/audit-events` |

## org-create

**Create a workspace**

- **Method / public URL:** `POST /identity/api/v1/organizations`
- **FE endpoint:** `api/v1/organizations`
- **Authorization:** Active authenticated account; no existing membership required. Replay additionally requires current membership and `organization.audit.read` in the created workspace.
- **Query:** None.
- **Request:** Required nonempty UUID `Idempotency-Key` header. Trimmed name 1–160 characters; duplicate workspace names are allowed. Body contains only `name`.
- **Success:** `201` envelope data `CreationSnapshot` (or 200 for matching authorized creation replay).
- **Errors:** Common [error handling](#errors). 409 `Organization.CreationAttemptConflict` for changed content; `Organization.CreationAttemptExpired` at/after the 24-hour replay boundary. 403 if current replay disclosure is no longer authorized.

A new committed result is 201; a matching authorized replay is 200 with the identical historical snapshot. Keep the same key and matching normalized name for the same uncertain attempt. Never treat snapshot roles/versions as current authority or silently create a new attempt after timeout/expiry.

**Illustrative request JSON:**

```json
{
  "name": "North Team"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ name = 'North Team' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations" --header "Authorization: Bearer $AccessToken" --header "Idempotency-Key: $AttemptId" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "organizationId": "019a0000-0000-7000-8000-000000000009",
    "name": "North Team",
    "status": "active",
    "membershipId": "019a0000-0000-7000-8000-000000000011",
    "roles": [
      "Owner"
    ],
    "version": 1,
    "membershipVersion": 1,
    "resultKind": "creationSnapshot",
    "snapshotAt": "2026-11-05T09:00:00Z"
  },
  "error": null
}
```


## org-list-mine

**Discover my eligible workspaces**

- **Method / public URL:** `GET /identity/api/v1/me/organizations`
- **FE endpoint:** `api/v1/me/organizations`
- **Authorization:** Authenticated active account; no existing workspace membership required.
- **Query:** `limit` default 50, 1–100; optional opaque `cursor` (up to 1,024 characters).
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Workspace>`.
- **Errors:** Common [error handling](#errors).

Only the subject’s active memberships in active workspaces. Empty items is valid and creates nothing. Response has no slug, member count, project count or last-active timestamp.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/me/organizations?limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "organizationId": "019a0000-0000-7000-8000-000000000001",
        "name": "North Team",
        "roles": [
          {
            "id": "019a0000-0000-7000-8000-000000000010",
            "code": "owner",
            "name": "Owner"
          }
        ],
        "membershipId": "019a0000-0000-7000-8000-000000000011",
        "version": 1,
        "membershipVersion": 1
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## member-me

**Read my current membership**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/members/me`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/me`
- **Authorization:** Current eligible active membership in this workspace; no general member-read grant required.
- **Query:** None.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Member`.
- **Errors:** Common [error handling](#errors).

Use for current workspace entry and own membership version. A URL identifier alone is insufficient. A valid token with removed/suspended/ineligible membership gets generic 403; do not globally sign out for that.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/me" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000011",
    "userId": "019a0000-0000-7000-8000-000000000003",
    "displayName": "Alex",
    "primaryEmail": "alex@example.test",
    "status": "active",
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000010",
        "code": "owner",
        "name": "Owner"
      }
    ],
    "joinedAt": "2026-11-05T09:00:00Z",
    "version": 1
  },
  "error": null
}
```


## member-list

**List workspace memberships**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/members`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members`
- **Authorization:** Current scoped `organization.members.read`.
- **Query:** `status` default `active`, one of `active|removed|suspended`; `limit` default 50, 1–100; optional cursor up to 1,024 characters.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Member>`.
- **Errors:** Common [error handling](#errors). 400 `Membership.InvalidStatus` for unsupported status; page validation failures are 400.

Historical status filters require the same read authority. `joinedAt` retains original join time after rejoin. Roles are current permitted roles, not an audit history.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members?status=active&limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "membershipId": "019a0000-0000-7000-8000-000000000002",
        "userId": "019a0000-0000-7000-8000-000000000004",
        "displayName": "Morgan",
        "primaryEmail": "morgan@example.test",
        "status": "active",
        "roles": [
          {
            "id": "019a0000-0000-7000-8000-000000000006",
            "code": "member",
            "name": "Member"
          }
        ],
        "joinedAt": "2026-11-05T09:00:00Z",
        "version": 1
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## member-detail

**Read one membership**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/members/{membershipId}`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/{membershipId}`
- **Authorization:** Current scoped `organization.members.read`; self-read without that grant uses member-me instead.
- **Query:** None.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Member`.
- **Errors:** Common [error handling](#errors). 404 `Membership.NotFound` only after scope/read authorization; denied scope is generic 403.



**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/$MembershipId" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "userId": "019a0000-0000-7000-8000-000000000004",
    "displayName": "Morgan",
    "primaryEmail": "morgan@example.test",
    "status": "active",
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000006",
        "code": "member",
        "name": "Member"
      }
    ],
    "joinedAt": "2026-11-05T09:00:00Z",
    "version": 1
  },
  "error": null
}
```


## member-grant-role

**Grant an ordinary role**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/grant`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/grant`
- **Authorization:** Current `organization.roles.assign`, eligible same-workspace target and delegation ceiling; Owner cannot be granted here.
- **Query:** None.
- **Request:** Positive `expectedMembershipVersion` from the target Member.version. Optional reason: null or nonblank, bounded to 1,000 characters; use the safe-reason policy. The target cannot be a protected Owner controlled by a non-Owner.
- **Success:** `200` envelope data `AssignmentChange`.
- **Errors:** Common [error handling](#errors). 409 `Organization.MembershipStale` for differing stale intent; 404 `Organization.RoleNotFound` after authorization; prohibited roles/ceiling get generic 403.

Regrant preserves previous grant periods and advances current membership version. An authorized already-current grant returns 200 changed:false without another grant/version/audit, even for a stale positive version.

**Illustrative request JSON:**

```json
{
  "expectedMembershipVersion": 1,
  "reason": null
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedMembershipVersion = $ExpectedMembershipVersion; reason = $null } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/$MembershipId/roles/$RoleId/grant" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "version": 2,
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000006",
        "code": "member",
        "name": "Member"
      },
      {
        "id": "019a0000-0000-7000-8000-000000000005",
        "code": "role-019a0000000070008000000000000005",
        "name": "Reviewer"
      }
    ],
    "changed": true
  },
  "error": null
}
```


## member-revoke-role

**Revoke an ordinary role**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/revoke`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/{membershipId}/roles/{roleId}/revoke`
- **Authorization:** Current `organization.roles.assign` and target-role permission ceiling. Revoking Admin or reducing an Owner target requires current system Owner authority. Owner role uses demotion, not this route.
- **Query:** None.
- **Request:** Positive target membership version. Reason optional for ordinary roles but required for Admin revocation; nonblank when supplied and at most 1,000 characters; safe audit reasons only.
- **Success:** `200` envelope data `AssignmentChange`.
- **Errors:** Common [error handling](#errors). 409 `Organization.MembershipStale`; 400 missing required Admin reason; unauthorized/protected reductions are 403.

An authorized already-revoked ordinary role is changed:false without repeating history. The response version belongs to the membership, not the role definition.

**Illustrative request JSON:**

```json
{
  "expectedMembershipVersion": 1,
  "reason": "Responsibility ended"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedMembershipVersion = $ExpectedMembershipVersion; reason = 'Responsibility ended' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/$MembershipId/roles/$RoleId/revoke" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "version": 2,
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000006",
        "code": "member",
        "name": "Member"
      }
    ],
    "changed": true
  },
  "error": null
}
```


## member-remove

**Remove a membership**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/members/{membershipId}/remove`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/{membershipId}/remove`
- **Authorization:** Current `organization.members.remove`. Only Owners may remove Owner/Admin targets; ordinary target effective permissions must be within the actor’s ceiling. Eligible Owner continuity must remain.
- **Query:** None.
- **Request:** Positive target Member.version and required safe nonblank reason up to 1,000 characters.
- **Success:** `200` envelope data `MembershipChange`.
- **Errors:** Common [error handling](#errors). 409 `Organization.MembershipStale`, `Organization.OwnerContinuityRequired`, readable `Organization.MembershipInvalidTransition`; 404 `Membership.NotFound` requires action/scope and member-read authority, otherwise 403.

Closes all unrevoked grant periods, preserves identity/join time/history and does not globally revoke the identity session. Already-removed replay requires current removal AND member-read authority; changed:false ignores a stale positive version. Self-removal cannot authorize its own replay.

**Illustrative request JSON:**

```json
{
  "expectedMembershipVersion": 1,
  "reason": "Contract completed"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedMembershipVersion = $ExpectedMembershipVersion; reason = 'Contract completed' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/$MembershipId/remove" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "status": "removed",
    "roles": [],
    "version": 2,
    "changed": true
  },
  "error": null
}
```


## member-demote-owner

**Demote an Owner**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/members/{membershipId}/demote-owner`
- **FE endpoint:** `api/v1/organizations/{organizationId}/members/{membershipId}/demote-owner`
- **Authorization:** Current system Owner authority, same workspace, and at least one eligible Owner remaining after a changed transition.
- **Query:** None.
- **Request:** Positive target Member.version; required safe reason up to 1,000 characters. No replacement role field is accepted.
- **Success:** `200` envelope data `MembershipChange`.
- **Errors:** Common [error handling](#errors). 409 `Organization.MembershipStale`, `Organization.OwnerContinuityRequired`, readable `Organization.MembershipInvalidTransition`; missing-target disclosure follows member-remove.

Revokes only Owner, keeps other valid roles, or creates/regrants built-in Member when none remain. A target that is no longer Owner is a conflict, not successful demotion replay. Additional-Owner acceptance and step-down are separate transactions.

**Illustrative request JSON:**

```json
{
  "expectedMembershipVersion": 1,
  "reason": "Explicit step down"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedMembershipVersion = $ExpectedMembershipVersion; reason = 'Explicit step down' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/members/$MembershipId/demote-owner" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "status": "active",
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000006",
        "code": "member",
        "name": "Member"
      }
    ],
    "version": 2,
    "changed": true
  },
  "error": null
}
```


## permissions-delegable

**Read the delegable permission subset**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/permissions`
- **FE endpoint:** `api/v1/organizations/{organizationId}/permissions`
- **Authorization:** Current active workspace membership plus at least one of `organization.roles.manage`, `organization.roles.assign`, `organization.invitations.read`, `organization.invitations.issue`, `organization.invitations.reissue` (role-catalog read admission).
- **Query:** None.
- **Request:** No JSON body.
- **Success:** `200` envelope data `DelegablePermissions`.
- **Errors:** Common [error handling](#errors).

This is the caller-held subset of catalog-delegable permissions for selectors. It is NOT a comprehensive effective-permission endpoint, and excludes protected ownership authority. Catalog read admission does not imply ability to create roles or assign them.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/permissions" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "codes": [
      "organization.members.read"
    ]
  },
  "error": null
}
```


## role-list

**List role definitions**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/roles`
- **FE endpoint:** `api/v1/organizations/{organizationId}/roles`
- **Authorization:** Current active workspace membership plus at least one of `organization.roles.manage`, `organization.roles.assign`, `organization.invitations.read`, `organization.invitations.issue`, `organization.invitations.reissue` (role-catalog read admission).
- **Query:** `limit` default 50, 1–100; optional cursor up to 1,024 characters.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Role>`.
- **Errors:** Common [error handling](#errors).

Includes retained retired definitions with retiredAt; do not offer them for new grants/invitations. Membership role badges use RoleBadge rather than this richer shape. Example page is abbreviated; always use server nextCursor.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/roles?limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "019a0000-0000-7000-8000-000000000005",
        "code": "role-019a0000000070008000000000000005",
        "name": "Reviewer",
        "description": null,
        "origin": "custom",
        "permissionCodes": [
          "organization.members.read"
        ],
        "version": 1,
        "permissionSetVersion": 1,
        "retiredAt": null
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## role-create

**Create a custom role**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/roles`
- **FE endpoint:** `api/v1/organizations/{organizationId}/roles`
- **Authorization:** Current `organization.roles.manage` and delegation ceiling.
- **Query:** None.
- **Request:** Nonempty distinct registered delegable `permissionCodes`. Only permissions currently held by the actor may be added. Name is trimmed, 1–160 characters and case-insensitively unique within the workspace; built-in names/codes are reserved. Description is nullable, at most 1,000 characters.
- **Success:** `201` envelope data `Role`.
- **Errors:** Common [error handling](#errors). 409 `Organization.RoleNameReserved` or `Organization.RoleConflict` for name/code collision; forbidden escalation is 403.

The server supplies stable id/code; never generate a role code from a display name or submit origin/template fields.

**Illustrative request JSON:**

```json
{
  "name": "Reviewer",
  "description": null,
  "permissionCodes": [
    "organization.members.read"
  ]
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ name = 'Reviewer'; description = $null; permissionCodes = @('organization.members.read') } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/roles" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000005",
    "code": "role-019a0000000070008000000000000005",
    "name": "Reviewer",
    "description": null,
    "origin": "custom",
    "permissionCodes": [
      "organization.members.read"
    ],
    "version": 1,
    "permissionSetVersion": 1,
    "retiredAt": null
  },
  "error": null
}
```


## role-update

**Update a custom role**

- **Method / public URL:** `PUT /identity/api/v1/organizations/{organizationId}/roles/{roleId}`
- **FE endpoint:** `api/v1/organizations/{organizationId}/roles/{roleId}`
- **Authorization:** Current `organization.roles.manage`; any added permission must be currently held and delegable.
- **Query:** None.
- **Request:** Nonempty distinct registered delegable `permissionCodes`. Only permissions currently held by the actor may be added. Name is trimmed, 1–160 characters and case-insensitively unique within the workspace; built-in names/codes are reserved. Description is nullable, at most 1,000 characters. Positive expectedVersion from Role.version. This sends the intended full name/description/permission set, not a sparse PATCH.
- **Success:** `200` envelope data `Role`.
- **Errors:** Common [error handling](#errors). 409 `Organization.RoleStale`, `Organization.RoleImmutable`, `Organization.RoleNameReserved`, `Organization.RoleConflict`; authorized missing role 404 `Organization.RoleNotFound`.

Identical authorized content is a no-change result. Metadata and permission changes may advance version separately; consume the returned version instead of adding one locally. Permission changes advance permissionSetVersion and invalidate old offers even after change-and-revert; renaming alone preserves offered names and permission meaning.

**Illustrative request JSON:**

```json
{
  "name": "Reviewer updated",
  "description": null,
  "permissionCodes": [
    "organization.members.read"
  ],
  "expectedVersion": 1
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ name = 'Reviewer updated'; description = $null; permissionCodes = @('organization.members.read'); expectedVersion = $ExpectedVersion } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request PUT "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/roles/$RoleId" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000005",
    "code": "role-019a0000000070008000000000000005",
    "name": "Reviewer updated",
    "description": null,
    "origin": "custom",
    "permissionCodes": [
      "organization.members.read"
    ],
    "version": 2,
    "permissionSetVersion": 1,
    "retiredAt": null
  },
  "error": null
}
```


## role-retire

**Retire a custom role**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/roles/{roleId}/retire`
- **FE endpoint:** `api/v1/organizations/{organizationId}/roles/{roleId}/retire`
- **Authorization:** Current `organization.roles.manage` and privileged-change admission.
- **Query:** None.
- **Request:** Positive Role.version; required safe reason up to 1,000 characters. Only mutable custom roles.
- **Success:** `200` envelope data `Role`.
- **Errors:** Common [error handling](#errors). 409 `Organization.RoleInUse`, `Organization.RoleStale`, `Organization.RoleImmutable`; authorized missing role 404 `Organization.RoleNotFound`.

Blocked by a current unrevoked/unexpired assignment OR any stored pending offer, including expired-but-still-pending offers. No automatic revocation/cancellation/deletion. Later grant/offer use is rejected; history remains.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1,
  "reason": "No longer needed"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion; reason = 'No longer needed' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/roles/$RoleId/retire" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000005",
    "code": "role-019a0000000070008000000000000005",
    "name": "Reviewer",
    "description": null,
    "origin": "custom",
    "permissionCodes": [
      "organization.members.read"
    ],
    "version": 2,
    "permissionSetVersion": 1,
    "retiredAt": "2026-11-05T09:00:00Z"
  },
  "error": null
}
```


## invitation-issue

**Issue an invitation**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/invitations`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations`
- **Authorization:** Current `organization.invitations.issue` AND `organization.roles.manage`, delegation ceiling and eligible recipient.
- **Query:** None.
- **Request:** recipientEmail required, trimmed valid address, 3–320 characters, normalized by server. Distinct nonempty UUID `roleIds`, at least one; same-workspace, unretired roles within the current delegation ceiling, excluding Owner. Recipient eligibility and current authority are checked on the server.
- **Success:** `201` envelope data `Invitation`.
- **Errors:** Common [error handling](#errors). 409 readable `Invitation.PendingOfferExists`; otherwise generic `Invitation.RecipientUnavailable` for duplicate/eligibility feedback. 429 `Invitation.RateLimited` with Retry-After; invalid role list 400; unauthorized roles 403.

Issuing queues delivery; it grants no membership and proves no provider delivery. Duplicate Issue never silently resends. Read authority is separate; action-only actors must not infer undisclosed recipient state.

**Illustrative request JSON:**

```json
{
  "recipientEmail": "morgan@example.test",
  "roleIds": [
    "019a0000-0000-7000-8000-000000000005"
  ]
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ recipientEmail = 'morgan@example.test'; roleIds = @($RoleId) } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000007",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "recipientEmail": "morgan@example.test",
    "status": "pending",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000005",
        "name": "Reviewer",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 1,
    "delivery": {
      "status": "queued",
      "failureCode": null,
      "updatedAt": "2026-11-05T09:00:00Z"
    }
  },
  "error": null
}
```


## invitation-list

**List manager-visible invitations**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/invitations`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations`
- **Authorization:** Current `organization.invitations.read`.
- **Query:** `status` default pending; pending|accepted|declined|cancelled|expired. limit default 50, 1–100; optional cursor up to 1,024 characters.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Invitation>`.
- **Errors:** Common [error handling](#errors).

Persisted status plus expiresAt are returned; pending may be past its deadline until lifecycle closure. offeredRoles.name is an immutable offer snapshot. delivery describes the current delivery generation only.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations?status=pending&limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "019a0000-0000-7000-8000-000000000007",
        "organizationId": "019a0000-0000-7000-8000-000000000001",
        "recipientEmail": "morgan@example.test",
        "status": "pending",
        "offeredRoles": [
          {
            "roleId": "019a0000-0000-7000-8000-000000000005",
            "name": "Reviewer",
            "permissionSetVersion": 1
          }
        ],
        "expiresAt": "2026-11-12T09:00:00Z",
        "version": 1,
        "delivery": {
          "status": "queued",
          "failureCode": null,
          "updatedAt": "2026-11-05T09:00:00Z"
        }
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## invitation-detail

**Read manager invitation detail**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/invitations/{invitationId}`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations/{invitationId}`
- **Authorization:** Current `organization.invitations.read`.
- **Query:** None.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Invitation`.
- **Errors:** Common [error handling](#errors). 404 `Invitation.NotFound` after read authorization; otherwise generic 403.



**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations/$InvitationId" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000007",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "recipientEmail": "morgan@example.test",
    "status": "pending",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000005",
        "name": "Reviewer",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 1,
    "delivery": {
      "status": "queued",
      "failureCode": null,
      "updatedAt": "2026-11-05T09:00:00Z"
    }
  },
  "error": null
}
```


## invitation-resend

**Resend the unchanged offer**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/resend`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations/{invitationId}/resend`
- **Authorization:** Current `organization.invitations.resend` AND `organization.roles.manage`; original sponsor must remain eligible with current issue/reissue authority and ceiling.
- **Query:** None.
- **Request:** Positive Invitation.version. Optional safe reason, nonblank when supplied, up to 1,000 characters.
- **Success:** `200` envelope data `Invitation`.
- **Errors:** Common [error handling](#errors). 409 `Invitation.OfferChanged` requires authorized reissue; `Invitation.Closed` for closed/expired; 429 `Invitation.RateLimited`.

Preserves original sponsor, role names and permission-set snapshots; rotates proof/delivery generation and extends deadline. Existing links become unusable. Never use Issue as a substitute for Resend.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1,
  "reason": null
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion; reason = $null } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations/$InvitationId/resend" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000007",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "recipientEmail": "morgan@example.test",
    "status": "pending",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000005",
        "name": "Reviewer",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 2,
    "delivery": {
      "status": "queued",
      "failureCode": null,
      "updatedAt": "2026-11-05T09:00:00Z"
    }
  },
  "error": null
}
```


## invitation-reissue

**Replace an invitation with a fresh offer**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/reissue`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations/{invitationId}/reissue`
- **Authorization:** Current `organization.invitations.reissue` AND `organization.roles.manage`, current recipient eligibility and role ceiling.
- **Query:** None.
- **Request:** Positive old Invitation.version, required safe reason up to 1,000 characters. Distinct nonempty UUID `roleIds`, at least one; same-workspace, unretired roles within the current delegation ceiling, excluding Owner. Recipient eligibility and current authority are checked on the server.
- **Success:** `200` envelope data `Invitation`.
- **Errors:** Common [error handling](#errors). 409 `Invitation.OfferChanged`, `Invitation.Closed`, privacy-safe `Invitation.RecipientUnavailable`; 429 `Invitation.RateLimited`.

Atomically closes the old offer and returns a NEW invitation id with current sponsor/snapshots/version. Update selected ID from the response; the public summary has no replacesInvitationId field. Old links cannot redeem the replacement.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1,
  "roleIds": [
    "019a0000-0000-7000-8000-000000000006"
  ],
  "reason": "Offer revised"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion; roleIds = @($MemberRoleId); reason = 'Offer revised' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations/$InvitationId/reissue" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000012",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "recipientEmail": "morgan@example.test",
    "status": "pending",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000006",
        "name": "Member",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 1,
    "delivery": {
      "status": "queued",
      "failureCode": null,
      "updatedAt": "2026-11-05T09:00:00Z"
    }
  },
  "error": null
}
```


## invitation-cancel

**Cancel a pending invitation**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/invitations/{invitationId}/cancel`
- **FE endpoint:** `api/v1/organizations/{organizationId}/invitations/{invitationId}/cancel`
- **Authorization:** Current `organization.invitations.cancel` and privileged-change admission; role-management capability is not separately required for cancel.
- **Query:** None.
- **Request:** Positive Invitation.version and required safe reason up to 1,000 characters.
- **Success:** `200` envelope data `Invitation`.
- **Errors:** Common [error handling](#errors). 409 `Invitation.OfferChanged` or `Invitation.Closed`; missing target is 404 only with scoped invitation-read authority, otherwise generic 403.

Closes the pending offer and prevents later delivery eligibility. Does not revoke an accepted membership. A repeated cancellation is a closed-state conflict, not a general replay endpoint.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1,
  "reason": "Offer withdrawn"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion; reason = 'Offer withdrawn' } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/invitations/$InvitationId/cancel" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000007",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "recipientEmail": "morgan@example.test",
    "status": "cancelled",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000005",
        "name": "Reviewer",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 2,
    "delivery": {
      "status": "queued",
      "failureCode": null,
      "updatedAt": "2026-11-05T09:00:00Z"
    }
  },
  "error": null
}
```


## recipient-preview

**Preview my invitation**

- **Method / public URL:** `POST /identity/api/v1/organization-invitations/{invitationId}/preview`
- **FE endpoint:** `api/v1/organization-invitations/{invitationId}/preview`
- **Authorization:** Signed-in active account whose CURRENT normalized database primary email matches this offer, valid current proof and active workspace; no prior membership required.
- **Query:** None.
- **Request:** Nonempty invitationId. Proof is a 43-character base64url value from the email fragment; missing/wrong/malformed proof goes through generic unavailable authorization, not a proof-detail error.
- **Success:** `200` envelope data `InvitationPreview`.
- **Errors:** Common [error handling](#errors). 403 `Invitation.Unavailable` before disclosure (including wrong account, bad proof, superseded link); after proof authorization 409 `Invitation.Closed` for expired/closed.

Read-only POST, not a membership grant. Only pending unexpired offers can be previewed. requiresReissue reports sponsor/permission changes; canAccept can also be false for active/suspended memberships. Do not cache the proof or treat preview as a guarantee of later acceptance.

**Illustrative request JSON:**

```json
{
  "proof": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ proof = $Proof } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organization-invitations/$InvitationId/preview" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "invitationId": "019a0000-0000-7000-8000-000000000007",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "workspaceName": "North Team",
    "offeredRoles": [
      {
        "roleId": "019a0000-0000-7000-8000-000000000005",
        "name": "Reviewer",
        "permissionSetVersion": 1
      }
    ],
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 1,
    "status": "pending",
    "canAccept": true,
    "requiresReissue": false
  },
  "error": null
}
```


## recipient-accept

**Accept my invitation**

- **Method / public URL:** `POST /identity/api/v1/organization-invitations/{invitationId}/accept`
- **FE endpoint:** `api/v1/organization-invitations/{invitationId}/accept`
- **Authorization:** Same recipient/proof admission as preview. First join/removed rejoin require current sponsor/role snapshots and eligible membership state.
- **Query:** None.
- **Request:** Positive expectedVersion from the current preview.version; proof only in JSON body.
- **Success:** `200` envelope data `InvitationAcceptance`.
- **Errors:** Common [error handling](#errors). 403 `Invitation.Unavailable`; after proof authorization 409 `Invitation.Closed`, `Invitation.OfferChanged`, `Invitation.RecipientUnavailable`.

No email-verification prerequisite is added by this flow. Removed rejoin keeps membership ID/join time and grants only freshly offered roles. A matching authorized accepted replay returns outcome alreadyAccepted and CURRENT permitted roles, ignores stale positive expectedVersion, and makes no new grants/audit; removal/suspension cannot be reversed by replay. Preview is not a terminal-status lookup.

**Illustrative request JSON:**

```json
{
  "proof": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "expectedVersion": 1
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ proof = $Proof; expectedVersion = $ExpectedVersion } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organization-invitations/$InvitationId/accept" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "outcome": "accepted",
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "roles": [
      {
        "id": "019a0000-0000-7000-8000-000000000005",
        "code": "role-019a0000000070008000000000000005",
        "name": "Reviewer"
      }
    ],
    "membershipVersion": 2
  },
  "error": null
}
```


## recipient-decline

**Decline my invitation**

- **Method / public URL:** `POST /identity/api/v1/organization-invitations/{invitationId}/decline`
- **FE endpoint:** `api/v1/organization-invitations/{invitationId}/decline`
- **Authorization:** Same current signed-in account/proof/workspace admission as preview; lost sponsor authority does not by itself prevent decline.
- **Query:** None.
- **Request:** Positive expectedVersion from preview.version; valid current proof.
- **Success:** `200` envelope data `InvitationDecline`.
- **Errors:** Common [error handling](#errors). 403 `Invitation.Unavailable`; after proof authorization 409 `Invitation.Closed` or `Invitation.OfferChanged`.

Closes the current pending generation without creating membership or changing existing grants. A second decline is a closed-state conflict; unlike nomination terminal replays, invitation decline has no no-change replay path.

**Illustrative request JSON:**

```json
{
  "proof": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "expectedVersion": 1
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ proof = $Proof; expectedVersion = $ExpectedVersion } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organization-invitations/$InvitationId/decline" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "outcome": "declined",
    "status": "declined",
    "version": 2
  },
  "error": null
}
```


## nomination-create

**Nominate an additional Owner**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/ownership-nominations`
- **FE endpoint:** `api/v1/organizations/{organizationId}/ownership-nominations`
- **Authorization:** Current eligible system Owner; active same-workspace non-Owner membership target.
- **Query:** None.
- **Request:** Nonempty membershipId; no actor, role, email, proof or expiry fields.
- **Success:** `201` envelope data `Nomination`.
- **Errors:** Common [error handling](#errors). 409 `OwnershipNomination.PendingExists`, `OwnershipNomination.AlreadyOwner`, `OwnershipNomination.RecipientUnavailable`; wrong subject/workspace/target scope generic 403.

Exactly seven days, in-app, no Owner grant or email at nomination. Mutation summary returns canAccept:false for the nominating actor. Nominee reads their personal list to discover current canAccept.

**Illustrative request JSON:**

```json
{
  "membershipId": "019a0000-0000-7000-8000-000000000002"
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ membershipId = $MembershipId } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/ownership-nominations" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000008",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
    "status": "pending",
    "createdAt": "2026-11-05T09:00:00Z",
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 1,
    "canAccept": false
  },
  "error": null
}
```


## nomination-list

**List workspace ownership nominations**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/ownership-nominations`
- **FE endpoint:** `api/v1/organizations/{organizationId}/ownership-nominations`
- **Authorization:** Current active system Owner, not merely a similarly named custom role or platform claim.
- **Query:** status default pending: pending|accepted|declined|cancelled|expired; limit default 50, 1–100; optional cursor up to 1,024 characters.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Nomination>`.
- **Errors:** Common [error handling](#errors).

Reauthorizes each page. canAccept is evaluated for the CURRENT caller, not a property meaning the nominee can definitely accept later.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/ownership-nominations?status=pending&limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "019a0000-0000-7000-8000-000000000008",
        "organizationId": "019a0000-0000-7000-8000-000000000001",
        "membershipId": "019a0000-0000-7000-8000-000000000002",
        "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
        "status": "pending",
        "createdAt": "2026-11-05T09:00:00Z",
        "expiresAt": "2026-11-12T09:00:00Z",
        "version": 1,
        "canAccept": false
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## nomination-list-mine

**Discover my ownership nominations**

- **Method / public URL:** `GET /identity/api/v1/me/ownership-nominations`
- **FE endpoint:** `api/v1/me/ownership-nominations`
- **Authorization:** Authenticated active subject; rows limited to their currently eligible memberships in active workspaces.
- **Query:** status default pending: pending|accepted|declined|cancelled|expired; limit default 50, 1–100; optional cursor up to 1,024 characters.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<Nomination>`.
- **Errors:** Common [error handling](#errors).

Pending canAccept uses current nominee, sponsor, membership, status and expiry facts. It does not grant Owner or bypass the later locked acceptance check.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/me/ownership-nominations?status=pending&limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "019a0000-0000-7000-8000-000000000008",
        "organizationId": "019a0000-0000-7000-8000-000000000001",
        "membershipId": "019a0000-0000-7000-8000-000000000002",
        "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
        "status": "pending",
        "createdAt": "2026-11-05T09:00:00Z",
        "expiresAt": "2026-11-12T09:00:00Z",
        "version": 1,
        "canAccept": true
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## nomination-accept

**Accept my Owner nomination**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/accept`
- **FE endpoint:** `api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/accept`
- **Authorization:** Exact current eligible nominee; sponsor remains an eligible system Owner; nomination is pending and unexpired.
- **Query:** None.
- **Request:** Positive expectedVersion from the current Nomination.version.
- **Success:** `200` envelope data `Nomination`.
- **Errors:** Common [error handling](#errors). 409 `OwnershipNomination.Stale`, `OwnershipNomination.Closed`, `OwnershipNomination.SponsorUnavailable`, `OwnershipNomination.AlreadyOwner`; generic 403 before authorized disclosure.

Adds/regrants Owner, retaining other roles and existing Owners, and advances membership once; that new membership version is NOT in this summary, so refetch member-me. Matching accepted replay requires original closing subject still be an eligible Owner and makes no mutation. This is not automatic ownership transfer.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/ownership-nominations/$NominationId/accept" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000008",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
    "status": "accepted",
    "createdAt": "2026-11-05T09:00:00Z",
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 2,
    "canAccept": false
  },
  "error": null
}
```


## nomination-decline

**Decline my Owner nomination**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/decline`
- **FE endpoint:** `api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/decline`
- **Authorization:** Exact currently eligible nominee; sponsor authority may have been lost.
- **Query:** None.
- **Request:** Positive Nomination.version.
- **Success:** `200` envelope data `Nomination`.
- **Errors:** Common [error handling](#errors). 409 `OwnershipNomination.Stale` or `OwnershipNomination.Closed`; wrong subject/scope generic 403.

Matching terminal replay by the original closing subject, with current authorization, returns the summary without version/audit changes despite a stale positive version.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/ownership-nominations/$NominationId/decline" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000008",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
    "status": "declined",
    "createdAt": "2026-11-05T09:00:00Z",
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 2,
    "canAccept": false
  },
  "error": null
}
```


## nomination-cancel

**Cancel an Owner nomination**

- **Method / public URL:** `POST /identity/api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/cancel`
- **FE endpoint:** `api/v1/organizations/{organizationId}/ownership-nominations/{nominationId}/cancel`
- **Authorization:** Any current active system Owner may cancel; replay still requires the original closing subject and current authority.
- **Query:** None.
- **Request:** Positive Nomination.version. Optional safe reason: null or nonblank, at most 1,000 characters.
- **Success:** `200` envelope data `Nomination`.
- **Errors:** Common [error handling](#errors). 409 `OwnershipNomination.Stale` or `OwnershipNomination.Closed`; unauthorized subject/scope generic 403.

Matching authorized cancellation replay by its closing actor is no-change. Does not remove an already accepted Owner; use the separately authorized membership action.

**Illustrative request JSON:**

```json
{
  "expectedVersion": 1,
  "reason": null
}
```

**PowerShell 7 / cURL:**

```powershell
$RequestJson = @{ expectedVersion = $ExpectedVersion; reason = $null } | ConvertTo-Json -Compress
$RequestJson | curl.exe --silent --show-error --include --request POST "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/ownership-nominations/$NominationId/cancel" --header "Authorization: Bearer $AccessToken" --header 'Content-Type: application/json' --data-binary '@-'
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "id": "019a0000-0000-7000-8000-000000000008",
    "organizationId": "019a0000-0000-7000-8000-000000000001",
    "membershipId": "019a0000-0000-7000-8000-000000000002",
    "nominatedByUserId": "019a0000-0000-7000-8000-000000000003",
    "status": "cancelled",
    "createdAt": "2026-11-05T09:00:00Z",
    "expiresAt": "2026-11-12T09:00:00Z",
    "version": 2,
    "canAccept": false
  },
  "error": null
}
```


## audit-list

**Read permitted workspace audit history**

- **Method / public URL:** `GET /identity/api/v1/organizations/{organizationId}/audit-events`
- **FE endpoint:** `api/v1/organizations/{organizationId}/audit-events`
- **Authorization:** Current `organization.audit.read` on EVERY page; member-read, role labels and historical grants do not suffice.
- **Query:** Optional actorUserId, membershipId, targetId (nonempty UUIDs); action from the registered action table; from/to explicitly offset ISO timestamps including seconds (`YYYY-MM-DDTHH:mm:ss[.fffffff]Z` or a `±HH:MM` offset, at most seven fractional digits and 40 characters). from inclusive, to exclusive, from < to. limit default 50, 1–100; optional cursor up to 1,024 characters. All supplied predicates combine with AND.
- **Request:** No JSON body.
- **Success:** `200` envelope data `Page<AuditEvent>`.
- **Errors:** Common [error handling](#errors). 400 invalid/mismatched cursor, changed filters, IDs, action or time range; generic scoped 403; 503 authoritative-store outage; sanitized 500 corrupt/unsupported audit details.

targetId filters aggregate ID; membershipId filters the persisted membership link. General order is ascending (occurredAt,eventId); membership mode ascending (membershipVersion,eventOrdinal,eventId). Preserve server order and cursor; pages are not a cross-request snapshot. Only committed succeeded events. No proof/email/correlation ID/raw Details is returned. Actor names/reasons can be null after privacy-safe projection.

**PowerShell 7 / cURL:**

```powershell
curl.exe --silent --show-error --include --request GET "$GatewayOrigin/identity/api/v1/organizations/$OrganizationId/audit-events?limit=50" --header "Authorization: Bearer $AccessToken"
```

**Illustrative success response:**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "eventId": "019a0000-0000-7000-8000-000000000020",
        "actorUserId": "019a0000-0000-7000-8000-000000000003",
        "actorDisplayName": "Alex",
        "organizationId": "019a0000-0000-7000-8000-000000000001",
        "targetType": "OrganizationMembership",
        "targetId": "019a0000-0000-7000-8000-000000000002",
        "action": "organization.membership.status-changed",
        "occurredAt": "2026-11-05T09:00:00Z",
        "outcome": "succeeded",
        "aggregateVersion": 2,
        "membershipVersion": 2,
        "eventOrdinal": 1,
        "reason": "Contract completed",
        "changes": {
          "userId": "019a0000-0000-7000-8000-000000000004",
          "previousStatus": "active",
          "newStatus": "removed"
        }
      }
    ],
    "nextCursor": null
  },
  "error": null
}
```


## audit-changes

Use these registered values for action filters and discriminate changes by action/targetType. The server uses explicit typed projections, not arbitrary stored properties. Render only these fields with human labels; never dump unknown JSON or render strings as HTML. Membership links are persisted, not inferred from changes. Names are bounded to 160 and may be null if unsafe; reasons are at most 1,000 and may be null for legacy privacy safety. Arrays are bounded to 512, details to 32 KB internally; no details payload is exposed. Unsafe/corrupt unsupported schema produces sanitized 500, not a successful arbitrary object.

| action | targetType | Exact changes fields |
|---|---|---|
| `organization.created` | `Organization` | name:string/null, status:string, createdByUserId:UUID |
| `organization.membership.created` | `OrganizationMembership` | userId:UUID, previousStatus:null, newStatus:string |
| `organization.membership.status-changed` | `OrganizationMembership` | userId:UUID, previousStatus:string, newStatus:string |
| `organization.role-assignment.granted` | `OrganizationRoleAssignment` | roleId:UUID, grantVersion:integer, grantedByUserId:UUID, grantedAt:timestamp, expiresAt:timestamp/null, permissionSetVersion:integer |
| `organization.role-assignment.revoked` | `OrganizationRoleAssignment` | roleId:UUID, grantVersion:integer, grantedAt:timestamp, expiresAt:timestamp/null, revokedAt:timestamp |
| `organization.role-assignment.regranted` | `OrganizationRoleAssignment` | roleId:UUID, previousGrantVersion:integer, grantVersion:integer, grantedByUserId:UUID, grantedAt:timestamp, expiresAt:timestamp/null, permissionSetVersion:integer |
| `organization.role.created` | `OrganizationRole` | origin:string, templateVersion:integer/null, previousPermissionSetVersion:integer/null, permissionSetVersion:integer, addedPermissionCodes:string[], removedPermissionCodes:string[], permissionCodes:string[] |
| `organization.role.permissions-changed` | `OrganizationRole` | Same fields as organization.role.created; versions retain the recorded revision. |
| `organization.role.updated` | `OrganizationRole` | previousName:string/null, newName:string/null, descriptionChanged:boolean |
| `organization.role.retired` | `OrganizationRole` | code:string, name:string/null, retiredAt:timestamp, permissionSetVersion:integer |
| `organization.invitation.issued` | `OrganizationInvitation` | issuedByUserId:UUID, offeredRoleIds:UUID[], expiresAt:timestamp, replacesInvitationId:UUID/null |
| `organization.invitation.reissued` | `OrganizationInvitation` | Same fields as organization.invitation.issued. |
| `organization.invitation.resent` | `OrganizationInvitation` | Same fields as organization.invitation.issued. |
| `organization.invitation.closed` | `OrganizationInvitation` | previousStatus:string, newStatus:string, closedByUserId:UUID/null, closedAt:timestamp |
| `organization.ownership-nomination.created` | `OrganizationOwnershipNomination` | nominatedByUserId:UUID, previousStatus:null, newStatus:string, expiresAt:timestamp, closedByUserId:UUID/null, closedAt:timestamp/null |
| `organization.ownership-nomination.closed` | `OrganizationOwnershipNomination` | Same fields as nomination.created, but previousStatus:string. |

## sources

Verified against the current on-disk implementation, including existing uncommitted work, rather than relying on historical planning prose:

- [Gateway allowlist](../../../src/Gateway/DeliveryPilot.Gateway/proxy.json), [CORS settings](../../../src/Gateway/DeliveryPilot.Gateway/appsettings.json), [safe private-response boundary](../../../src/Gateway/DeliveryPilot.Gateway/GatewayRequestMiddleware.cs).
- [Identity controllers](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/) and [closed API request contracts](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Contracts/).
- [Application handlers, validators and response types](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/Features/); [authorization and delegation checks](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/Abstractions/Security/OrganizationMutationScope.cs), [role-catalog read admission](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/OrganizationAuthorizationService.cs).
- [Audit read projection](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/Features/OrganizationAudit/Queries/ListOrganizationAuditEvents/OrganizationAuditProjection.cs), [reason/event schema policy](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Auditing/IdentityAuditSchemaCatalog.cs), [constraint error translation](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Core/IdentityUnitOfWork.cs), [exception-to-envelope mapping](../../../src/BuildingBlocks/DeliveryPilot.AspNetCore/Middleware/GlobalExceptionHandlingMiddleware.cs).
- [Gateway route coverage](../../../tests/DeliveryPilot.Gateway.Tests/GatewayOrganizationRouteTests.cs), [real Gateway/Identity journeys](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs), [acceptance matrix](backend-acceptance.md), [task 014 execution evidence](tasks/TASK-DP-003-014.md#implementation-and-verification--2026-11-05). Their recorded runs are prior evidence, not a claim that cURL was executed while writing this guide.

Maintenance: update this reference and its playbook when changing a published route, body/response projection, validation, permission, retry or error contract. Compare the 30-entry inventory against proxy.json and inspect the owning handler/test. Keep example IDs fictional; parse JSON snippets and check links. Do not add an API by copying a task proposal or an internal service method. OpenAPI/Orval remains a future option after a verified Gateway-scoped spec includes accurate envelope/error/response schemas; no SDK generation pipeline is supplied by this document.

Documentation verification on 2026-11-05: the 30 method/path pairs matched the current Gateway allowlist; all 51 JSON examples parsed; 19 request property sets and every response/nested DTO property set matched source records; all 16 audit actions matched the schema catalog. All 31 PowerShell blocks passed syntax parsing without making HTTP requests. The playbook's two TypeScript examples typechecked in a virtual compiler host against the actual FE tsconfig, imports and installed dependencies, without writing FE files. Repository documentation validation/coverage and relative-link checks passed. These checks do not claim live execution of the illustrative cURL responses or a completed FE implementation.
