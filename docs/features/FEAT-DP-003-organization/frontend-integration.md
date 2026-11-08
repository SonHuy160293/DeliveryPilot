# Organization frontend integration playbook

Use this file to connect real user journeys; use the [Gateway API contract](gateway-api-contract.md) for exact methods, requests, responses, permissions and cURL examples. Begin with one vertical slice and verify it before adding another. No page, mock label or client cache grants access.

Verified on 2026-11-05 (Asia/Bangkok): backend `dd66c28` plus the existing uncommitted Organization implementation; sibling `DeliveryPilot-Web` `c149a24`, inspected read-only. This is a backend-owned handoff for implemented behavior, not authorization to change FE routes/dependencies or a claim that the frontend integration is already complete. Recheck both checkouts when starting implementation. The [backend acceptance evidence](backend-acceptance.md) records prior tests separately from this documentation work.

## Start here

The inspected frontend is a React 19/Vite SPA using strict TypeScript, React Router, Axios, TanStack Query 5, Zod 4, React Hook Form and MSW/Vitest. Its transport, in-memory auth/session generation, safe ApiError handling, query provider and scope cleanup already exist. Reuse them; do not introduce another fetch client, auth framework, persisted token store, cache or SDK generator.

Read the FE repository's `AGENTS.md`, `docs/ui/UI-RULES.md`, `docs/ui/AGENT-BRIEF.md`, `docs/frontend/architecture.md`, `api-integration.md`, `authentication.md` and testing guidance before editing it. Its route/dependency/theme/generated-component approval rules remain authoritative. This guide does not copy or replace that rulebook.

Read order for the agent:

1. Check [integration gaps](#integration-gaps) against the current mounted route tree.
2. Establish the [shared client pattern](#client-pattern) and [cache boundaries](#cache-boundaries).
3. Implement the workflows below in order, opening the linked operation contracts as needed.
4. Run the [integration checklist](#integration-checklist), then report real behavior and remaining gates.

| Slice | First usable outcome | Operation reference |
|---|---|---|
| 1. Connection/session | Existing sign-in reaches Gateway; no duplicate auth | [Setup](gateway-api-contract.md#setup) |
| 2. Discovery | Real eligible workspace list or honest empty state | [My workspaces](gateway-api-contract.md#org-list-mine) |
| 3. Creation | Real creation and safe same-attempt recovery | [Create](gateway-api-contract.md#org-create) |
| 4. Entry | Current own membership admitted before scoped content | [My membership](gateway-api-contract.md#member-me) |
| 5. Members/roles | Permitted reads and versioned, auditable management | [Members](gateway-api-contract.md#member-list), [roles](gateway-api-contract.md#role-list) |
| 6. Manager invitations | Issue and inspect delivery without claiming membership | [Issue](gateway-api-contract.md#invitation-issue) |
| 7. Recipient response | Current-account preview, accept/decline, fresh-role rejoin | [Preview](gateway-api-contract.md#recipient-preview) |
| 8. Ownership | Nominate, accept additional Owner, then explicit step-down | [Nominate](gateway-api-contract.md#nomination-create) |
| 9. Audit | Permitted typed history with stable continuation | [Audit](gateway-api-contract.md#audit-list) |

## Integration gaps

These are facts from the inspected FE checkout, not tasks completed by this guide:

- `src/app/router.tsx` mounts `WorkspaceLanding` for both `/workspaces` and `/w/:workspaceSlug`. The landing currently says membership is not connected. `WorkspacesPage` and `WorkspaceCreateDialog` contain previews; editing an unmounted preview alone will not integrate the mounted app. Reconcile the mounted composition deliberately, preserving the FE's skeleton/route rules.
- Workspace responses supply `organizationId`, membership ID, name, roles and versions; **no slug or member/project/activity counts**. Recommend using the stable organization UUID as the URL segment within the existing `/w/:workspaceSlug` pattern, treating the parameter name as legacy, subject to FE route-policy review. Never slugify names into an invented backend identifier or resolve authorization from fixture slugs. Reconcile the existing internal return-path checks as part of authorized FE integration.
- Notification emails generate `/join-workspace#invitationId=<uuid>&proof=<43-char-base64url>`. That document route is absent from the approved FE route map. Adding it and admitting it as a safe internal sign-in continuation requires the FE's route approval procedure before affected edits. Backend email links must not be silently rewritten to a made-up FE path. API/model tests and the already-approved workspace slices can proceed independently while that route gate is resolved.
- There are no approved dedicated member/role/invitation/nomination/audit routes in that route map. Integrate into an explicitly authorized existing composition or request route approval before adding screens. This handoff defines data flows, not new approved UI navigation.
- `/permissions` is a **caller-held delegable subset**, not a complete effective-capability endpoint. Role-catalog reads can be admitted by several action capabilities. A successful catalog read, role display name or platform claim does not authorize a management action. Use separate read/action errors and server-supplied flags such as canAccept; do not invent `/me/permissions` or hardcode Owner/Admin into an unrestricted permission map. Comprehensive capability discovery, if required for polished action gating, needs a separate backend contract.
- FE docs saying workspace APIs are unavailable are stale relative to the current backend. The FE agent should reconcile those implementation claims when authorized, retaining the existing auth/cache/security rules. This documentation task changes no FE files.
- A refresh cookie exists, but public refresh/server logout do not. Reload loses the memory-only access token; local sign-out does not revoke the server session. Do not add refresh interceptors or cookie-auth assumptions. OpenAPI/Orval stays deferred until a Gateway-scoped spec accurately describes envelopes, response projections and errors.

## Client pattern

Use `VITE_API_BASE_URL=/identity/`, the existing server-only `DEV_API_PROXY_TARGET` pointing at the actual Gateway, and leave prefix stripping false. Ordinary feature requests are relative strings such as `api/v1/me/organizations`, not `/identity/api/v1/...` and not `/api/v1/...`: the shared client rejects leading/absolute paths and destination overrides. VITE values are public; never include service keys or credentials. See [connection setup](gateway-api-contract.md#setup) for same-origin versus CORS constraints.

Own the Organization contracts/API/query definitions in the existing workspace feature, subdividing the area as it grows. Components coordinate forms and hooks; they contain no raw HTTP. Use the current `httpClient`, `unwrapApiResponse`, `ApiError`, `authSession`, `workspaceScopeKey`, `clearWorkspaceCache` and `clearSessionCache`. Reuse feature-owned runtime validation, not Axios generics or casts. Do not copy the backend's domain entities into client types.

The following standalone example shows a real wire decoder, current-membership query and creation mutation pattern. Split production files by responsibility; this is not a new shared infrastructure layer. The sample only covers its named responses; implement other decoders from the [exact shape table](gateway-api-contract.md#shapes), particularly the different role/offer/creation shapes. UUID/version validation below reflects safe FE handling of numeric wire versions.

```typescript
import { z } from "zod";
import { mutationOptions, queryOptions, type QueryClient } from "@tanstack/react-query";
import { httpClient } from "@/lib/api/httpClient";
import { unwrapApiResponse } from "@/lib/api/apiResponse";
import { ApiError } from "@/lib/api/apiError";
import { authSession } from "@/lib/api/authSession";
import { workspaceScopeKey, type WorkspaceScope } from "@/lib/query/queryScope";

const uuid = z.string().regex(/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i)
  .refine((value) => value !== "00000000-0000-0000-0000-000000000000");
const version = z.number().int().positive().refine(Number.isSafeInteger);
const timestamp = z.iso.datetime({ offset: true });
const badge = z.object({ id: uuid, code: z.string(), name: z.string() });
const memberSchema = z.object({
  membershipId: uuid, userId: uuid, displayName: z.string(), primaryEmail: z.string(),
  status: z.enum(["active", "removed", "suspended"]), roles: z.array(badge),
  joinedAt: timestamp, version,
});
const creationSchema = z.object({
  organizationId: uuid, name: z.string(), status: z.literal("active"),
  membershipId: uuid, roles: z.array(z.string()), version,
  membershipVersion: version, resultKind: z.literal("creationSnapshot"), snapshotAt: timestamp,
});
const createInput = z.object({ attemptId: uuid, name: z.string().trim().min(1).max(160) }).strict();

function sameSession(accountId: string, generation: number): boolean {
  const current = authSession.getSnapshot();
  return current.status === "authenticated" && current.userId === accountId
    && authSession.version() === generation;
}

export function myMembershipOptions(scope: WorkspaceScope) {
  const organizationId = uuid.parse(scope.workspaceId);
  return queryOptions({
    queryKey: [...workspaceScopeKey(scope), "membership", "me"],
    queryFn: async ({ signal }) => {
      const response = await httpClient.get<unknown>(
        `api/v1/organizations/${organizationId}/members/me`, { signal },
      );
      const result = memberSchema.safeParse(unwrapApiResponse(response.data));
      if (!result.success) throw new ApiError("protocol", "Membership response is invalid.");
      return result.data;
    },
    staleTime: 0, // revalidate on workspace entry; no change to the global QueryClient
  });
}

export function createWorkspaceOptions(accountId: string, client: QueryClient) {
  return mutationOptions({
    mutationKey: ["account", accountId, "workspaces", "create"],
    retry: false,
    mutationFn: async (input: z.infer<typeof createInput>) => {
      const generation = authSession.version();
      if (!sameSession(accountId, generation)) throw new ApiError("cancelled", "Session changed.");
      const body = createInput.parse(input);
      const response = await httpClient.post<unknown>(
        "api/v1/organizations", { name: body.name },
        { headers: { "Idempotency-Key": body.attemptId } },
      );
      const result = creationSchema.safeParse(unwrapApiResponse(response.data));
      if (!result.success) throw new ApiError("protocol", "Creation response is invalid.");
      return { snapshot: result.data, generation };
    },
    onSuccess: async ({ generation }) => {
      if (!sameSession(accountId, generation)) return;
      await client.invalidateQueries({ queryKey: ["account", accountId, "workspaces"] });
    },
  });
}
```

Only mount scoped observers for the current authenticated session. The UI also checks the captured generation/account before navigation, success notices or publishing a mutation result; onSuccess invalidation alone is not a complete UI boundary. Build full known request objects explicitly: do not spread form/client metadata into closed JSON bodies. Ordinary nonsecret mutations may use TanStack mutation options; sign-in and proof-bearing invitation requests must not put their inputs/results containing secrets into its caches.

Zod objects above permit additive response fields while validating required fields. Reject malformed envelopes/unsafe versions as protocol failures, not empty data. Format returned safe dates for display without using formatted strings for pagination/order. Use locally controlled safe text and render all returned strings as text, not HTML.

## Cache boundaries

Keep cache identity separate from authorization. Follow these account/workspace namespaces and include every response-shaping filter; no raw token, email lookup secret or invitation proof in keys:

| Resource | Key shape |
|---|---|
| My workspaces | `["account", userId, "workspaces", {limit}]` with cursor as infinite-query pageParam |
| Scoped membership | `[...workspaceScopeKey(scope), "membership", "me"]` or `[...root,"members","detail",membershipId]` |
| Scoped lists | `[...root,resource,"list",normalizedFilters]`; cursor is pageParam, not omitted from fetching |
| Role selectors | `[...root,"roles","list",{limit}]` and `[...root,"permissions","delegable"]` |
| Personal nominations | `["account",userId,"ownership-nominations",{status,limit}]` |
| Recipient proof flow | Feature-local imperative state; no TanStack query/mutation entry containing proof |

Use `useInfiniteQuery`/infiniteQueryOptions for pages and pass the pageParam cursor with unchanged filters; set getNextPageParam to nextCursor or undefined. Reset pagination on filters/scope change. Never compute a cursor or client page offset. Read all needed discovery pages before concluding that an eligible workspace is absent. Audit cursors additionally bind normalized filters, ordering mode and workspace.

Before switching workspace, disable/unmount old scoped observers, cancel/remove the old prefix with `clearWorkspaceCache`, then mount the new scope. Before account change/local sign-out use the existing session cache cleanup. Never use previousData across accounts/workspaces. No persisted query store, localStorage/sessionStorage selection/authority, or duplicate global server-state store is introduced.

Pass AbortSignal to reads. Cancellation is not a failure toast and cannot undo a committed server mutation. Settle/block pending writes while changing account/scope, or own an AbortController and ignore callbacks/results from a retired generation. Discard proof state on account changes; after signing in to the wrong recipient account, reopen the email for a deliberate fresh flow instead of carrying a proof silently to another identity.

After a 403 clear the **denied resource** and its privileged detail state. If member-me itself is denied, clear the whole workspace prefix and return to selection. An audit-only 403 does not prove that workspace membership is gone. Never replace a denied protected response with fixtures, stale private content or a global sign-out solely due to 403. Revalidation of one read never guarantees a later mutation; the server checks every operation.

| Successful mutation | Invalidate/refetch for the current session only |
|---|---|
| Create workspace | Account workspace discovery; current member-me/new scope before privileged UI |
| Create/update/retire role | Scoped role lists/catalog-dependent views; affected member badges/own membership; account workspace roles and audit. Pending offers retain frozen names; permission changes require reissue, not a client patch to snapshots. |
| Grant/revoke/remove/demote | Scoped members/list/detail/self, account workspace discovery, relevant ownership/invitation views and audit. Self-removal/demotion may remove read/action access; clear denied views. |
| Issue/resend/reissue/cancel | Scoped invitation lists/details and audit; replace selected invitation ID after reissue. No membership success/cache insertion from issuance. |
| Accept recipient invitation | Account workspaces, returned workspace member-me/member lists/roles and audit; then clear proof. Decline refreshes only relevant allowed invitation views; it creates no membership. |
| Nominate/respond | Owner and personal nomination lists; acceptance also current membership/roles/account workspace discovery, relevant members and audit. Refetch the membership version before later step-down. |

Global defaults already retry only certain GET/HEAD failures and never mutations. Do not add automatic mutation retries or optimistic membership/role/Owner changes. For issuance 429, use normalized retryAfterMs to disable the action/count down, then require deliberate retry. Pending and outcome-unknown are distinct states.

## Workflow 1 — Configure Gateway access and reuse sign-in

**Prerequisite:** FE instructions read; existing auth/client foundation retained. Set the [documented base/proxy](gateway-api-contract.md#setup), restart Vite after env changes, and use the actual Gateway origin. Do not enable credentials on Organization requests or change cookie flags. Use the existing connected login and memory session; no fetch-in-useEffect or second auth provider.

**Sequence:** sign in → validated authSession snapshot → mount account-scoped workspace discovery. `userId` and expiry come from the validated session; no workspace is implied by sign-in. Registration remains a separate API/form workflow and never automatically creates a workspace.

**States/recovery:** distinguish missing configuration, pending sign-in, invalid identity and signed-in/no memberships. Reuse the existing safe ApiError handling. A 401 clears only the session generation that sent that token; a late response must not clear a newer sign-in. No public refresh/server logout route exists. Refetch discovery after a fresh authenticated session; clear old session caches first.

## Workflow 2 — Discover eligible workspaces

**Prerequisite:** current authenticated account; no workspace required. Call [GET my workspaces](gateway-api-contract.md#org-list-mine), following nextCursor as needed. Render returned stable IDs, name and current RoleBadge objects. The API does not return the counters/activity shown by mock cards.

**States:** accessible loading, real list, zero memberships with create action, safe initial error/retry. Authenticated empty is not an error, guest denial or implicit workspace creation. For a background failure retain permitted ordinary content only while no explicit authorization denial has occurred; show a nonblocking error. Remove demonstration preview-state controls and mock records from connected screens.

**Selection/recovery:** choose a returned organizationId, then enter via workflow 4. A remembered invalid/ineligible selection must return to a selector, not silently switch to a different workspace. No new persisted selection policy is introduced. Do not declare an ID absent after inspecting only the first page.

**Invalidate:** successful creation, invitation acceptance, self role/membership changes, nomination acceptance and session changes refresh the relevant account discovery.

## Workflow 3 — Create with safe uncertain-response recovery

**Prerequisite:** active authenticated account and a real create composition. Use [POST create](gateway-api-contract.md#org-create) with only name and a UUID Idempotency-Key. Keep `{accountId,generation,attemptId,name}` in feature-local memory for this user intent; do not create a fresh key inside each mutation execution.

**Sequence:** validate trimmed name → allocate UUID once at deliberate new submission → POST → decode CreationSnapshot → verify same session generation → invalidate discovery → fetch current member-me (and discovery pages for current name/roles) before entering privileged UI. 201 is new creation; 200 is an authorized replay. Both use historical snapshot data, whose roles are strings rather than RoleBadge objects.

**Recovery:** on timeout/network loss or an invalid success-response envelope/schema, the operation may have committed: retain the same attempt key/payload and offer deliberate same-attempt retry. Do not show “nothing was created” or automatically allocate another key. Different content gives CreationAttemptConflict; the 24-hour boundary gives CreationAttemptExpired and needs explicit new-intent guidance. Replays require current audit-read admission; 403 is not evidence that the original workspace never existed. Do not automatically reset the key or retry a write after 401/409/429/5xx.

**States:** edit, pending with duplicate submission disabled, known success, outcome unknown/manual retry, field/permission/conflict error. No preview success toast. A new name/new user intent must not accidentally reuse a possibly completed old key. Do not persist the attempt state under the current FE persistence restrictions; if it is lost on reload, reconcile discovery before inviting another creation and explain that exact same-attempt recovery requires the retained key.

## Workflow 4 — Enter and revalidate a workspace

**Prerequisite:** approved route mapping and a validated organization UUID from real discovery/current creation. Treat the existing workspaceSlug parameter as a navigation input, not authority. A future ID-based segment must be admitted by the FE route-policy review; never invent a slug lookup endpoint.

**Sequence:** disable prior scope → clear prior scoped cache → [GET member-me](gateway-api-contract.md#member-me) for the chosen ID → only then mount that account/workspace’s dependent views. Use membershipId, status, roles and version from the response. Resolve the workspace name from current discovery, not from a slug/fixture. The own read does not require general members.read; do not demand member-list permission to enter.

**Recovery:** member-me 403 clears that workspace and returns to selection; keep the global valid identity. 503 is an authority-store failure and must not fall back to mock/previous workspace data. Role badges are display/current-role facts, not a complete capability map. After a membership-changing mutation use the returned or refetched version, not workspace.version.

## Workflow 5 — Read/manage members and roles

**Read sequence:** [member list](gateway-api-contract.md#member-list) or [detail](gateway-api-contract.md#member-detail) only when the server admits members.read; own read remains separately available. Active is the default filter; removed/suspended are explicit history filters. Preserve original joinedAt and distinguish current badges from retained audit history. Do not show invitation offers as members.

**Role sequence:** [role definitions](gateway-api-contract.md#role-list) + [delegable subset](gateway-api-contract.md#permissions-delegable) for authorized selectors → [create](gateway-api-contract.md#role-create) or [full update](gateway-api-contract.md#role-update) with appropriate Role.version. Keep current description/permissionCodes when only renaming; omitted nullable description can clear it. Filter retired definitions out of new grant/offer selectors. System origin and reserved names prevent ordinary built-in edits; never send origin/code/template fields.

**Membership actions:** select a permitted current target → use target Member.version as expectedMembershipVersion for [grant](gateway-api-contract.md#member-grant-role), [revoke](gateway-api-contract.md#member-revoke-role), [remove](gateway-api-contract.md#member-remove) or [Owner demotion](gateway-api-contract.md#member-demote-owner). Own-role changes do not bypass delegation. Ordinary role operations can return changed:false; confirm already-current state instead of fabricating a new grant/revocation success. Required safe reasons belong next to the action; Admin revocation, removal/demotion and retirement have special reason rules.

**Retirement:** [retire](gateway-api-contract.md#role-retire) only after assignments and stored pending offers are cleared. RoleInUse does not disclose a private offer: use only separately authorized reads to investigate; never invent blocking IDs, auto-revoke grants or auto-cancel invitations. Deadline passage alone does not clear stored pending references.

Expired pending references can remain blocking, and no dedicated expiry-close/sweep endpoint is published. Surface that condition for backend review if the permitted lifecycle actions cannot clear it; do not issue an unsolicited replacement invitation merely as retirement cleanup.

**States/recovery:** pending actions disabled, current server-confirmed success/no-change, safe 403, field error, stale conflict with reload/review. Do not auto-resubmit a changed role or mutate protected Owner/Admin rules in the UI. A read grant does not imply a write grant; an action-only caller may lack broad read authority. Do not impose broad member/audit read as an invented server prerequisite or leak guessed target facts. Invalidate the caches listed above only for the still-current session.

## Workflow 6 — Manage invitations and delivery

**Prerequisite:** specific current action authority; issue/resend/reissue additionally require roles.manage and delegation. [List/detail](gateway-api-contract.md#invitation-list) separately require invitations.read. An action response is not a general permission to enumerate recipient states.

**Sequence:** choose authorized same-workspace role IDs → [Issue](gateway-api-contract.md#invitation-issue) → show the returned offer, expiry and current delivery status. Persist no proof; manager responses contain none. Queued, provider-accepted or failed delivery is never acceptance/membership. Delivery status belongs to the current generation, not whichever old message was last observed.

**Subsequent actions:** duplicate Issue is a conflict. [Resend](gateway-api-contract.md#invitation-resend) preserves original sponsor/snapshots and changes proof/delivery/version; [Reissue](gateway-api-contract.md#invitation-reissue) replaces the old offer and returns a NEW ID and current snapshots; [Cancel](gateway-api-contract.md#invitation-cancel) closes pending state without undoing an accepted membership. Each uses the latest invitation.version; reasons follow the operation contract. Role rename never changes immutable offered names. Sponsor loss or permission change/revert requires authorized reissue; do not patch old offers locally.

**States/recovery:** separate pending offer, past-deadline hint, delivery failure, successful cancellation and closed state. Honor 429 retryAfterMs with a countdown and no automatic resend. RecipientUnavailable is deliberately generic: do not reveal whether email belongs to an active/suspended member or another offer. 409 OfferChanged prompts current authorized reads/reissue review. Refresh invitation detail/list and audit after success, replacing selected ID on reissue. Current API has no delivery-status subscription endpoint: do not invent one; polling, if later authorized, uses the same scoped GET and stops on access loss/unmount.

## Workflow 7 — Respond to an invitation and rejoin

**Route gate:** the separate `DeliveryPilot-Web` checkout implements the approved `/join-workspace` route and a same-tab sign-in continuation. Verify that checkout's current route map before making changes. The initial recipient can have **zero memberships**; do not put this journey behind member-me/workspace-membership admission. Registration creates an account without a session or membership; recipients register, sign in, then reopen the original invitation email because registration retires the in-memory proof.

For an authorized real smoke test, the Notification frontend-origin setting must point to the actual SPA origin (including the development port), and the approved SPA host must serve that document route on direct visits. The local template uses `http://127.0.0.1:5173`, matching the frontend's fixed Vite port; existing local env files must be updated explicitly. Obtain the proof from the disposable recipient's test inbox/email fragment; no manager API exposes it. Use trusted HTTPS where required by the existing auth/cookie setup, not relaxed cookie flags.

**Secret flow:** capture invitationId/proof from the fragment into feature-owned ephemeral memory before auth redirection/analytics; validate bounded format and immediately remove the fragment using history.replaceState. Do not copy the original URL/hash into returnTo, router state, query keys, TanStack mutation variables, logs, telemetry, persistence or clipboard diagnostics. Keep this flow alive only in the current tab; a reload can lose it and should ask the user to reopen the original email. Use the existing memory auth session, not a new global persisted invitation/auth store. Any navigation/analytics framework touching the initial URL must be considered before enabling this route.

**API sequence:** signed-in current recipient → imperative [POST preview](gateway-api-contract.md#recipient-preview) with proof → display permitted workspace/immutable role names, expiry and flags → deliberate [accept](gateway-api-contract.md#recipient-accept) or [decline](gateway-api-contract.md#recipient-decline), using preview.version. Matching the current normalized database primary email and proof is the server’s decision; do not invent an email lookup endpoint or use JWT/display labels as that evidence. Unverified matching accounts can accept. Wrong account/proof is generic Invitation.Unavailable. No pending eligibility proof is a guarantee across later write locks.

**Changed/closed states:** requiresReissue means the manager must provide a fresh offer; canAccept can also be false for active/suspended membership. Lost sponsor authority can still permit decline. Preview only accepts pending/unexpired offers; it is NOT a terminal-status lookup. After an uncertain accept, a deliberate same-proof/same-subject accept can return alreadyAccepted under current eligibility; it grants nothing again and returns current roles. Removal/suspension is not reversed by replay. Invitation decline has no general terminal replay and can return Closed on repetition.

**Success:** accept returns organizationId, membershipId, membershipVersion and current RoleBadge[]; invalidate account discovery and newly permitted scoped data, verify current membership, navigate only if the same session still owns the flow, then erase proof state. Removed-member rejoin retains membership ID/original join time and grants only fresh offered roles—never restore historical Admin/Owner badges from cache. Decline creates no membership. Abandon/account-change/unmount handling must erase the secret, except an explicitly owned same-tab sign-in continuation.

Acceptance does not sign the user in again, issue a new token, or mark the global email as verified. Keep the existing identity session and refetch permitted workspace state instead of inventing those effects.

Use an imperative API function and local pending/error/result state, following existing login’s secret handling. The sample below validates the exact preview shape and never sends proof through TanStack Query. The approved route owns secret lifetime/cancellation; this is not authorization to add that route.

```typescript
import { z } from "zod";
import { httpClient } from "@/lib/api/httpClient";
import { unwrapApiResponse } from "@/lib/api/apiResponse";
import { ApiError } from "@/lib/api/apiError";
import { authSession } from "@/lib/api/authSession";

const id = z.string().regex(/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i)
  .refine((value) => value !== "00000000-0000-0000-0000-000000000000");
const revision = z.number().int().positive().refine(Number.isSafeInteger);
const secretSchema = z.object({ invitationId: id, proof: z.string().regex(/^[A-Za-z0-9_-]{43}$/) });
type InvitationSecret = z.infer<typeof secretSchema>;
const previewSchema = z.object({
  invitationId: id, organizationId: id, workspaceName: z.string(),
  offeredRoles: z.array(z.object({ roleId: id, name: z.string(), permissionSetVersion: revision })),
  expiresAt: z.iso.datetime({ offset: true }), version: revision, status: z.literal("pending"),
  canAccept: z.boolean(), requiresReissue: z.boolean(),
});

export function captureInvitationFragment(): InvitationSecret | undefined {
  const fragment = new URLSearchParams(window.location.hash.slice(1));
  const parsed = secretSchema.safeParse({
    invitationId: fragment.get("invitationId"), proof: fragment.get("proof"),
  });
  window.history.replaceState(window.history.state, "", window.location.pathname + window.location.search);
  return parsed.success ? parsed.data : undefined; // never log parser inputs/errors
}

export async function previewInvitation(secret: InvitationSecret, signal: AbortSignal) {
  const before = authSession.getSnapshot();
  const generation = authSession.version();
  if (before.status !== "authenticated") throw new ApiError("unauthenticated", "Sign in to continue.");
  const response = await httpClient.post<unknown>(
    `api/v1/organization-invitations/${secret.invitationId}/preview`,
    { proof: secret.proof }, { signal },
  );
  const after = authSession.getSnapshot();
  if (authSession.version() !== generation || after.status !== "authenticated" || after.userId !== before.userId)
    throw new ApiError("cancelled", "Session changed.");
  const parsed = previewSchema.safeParse(unwrapApiResponse(response.data));
  if (!parsed.success) throw new ApiError("protocol", "Invitation response is invalid.");
  return parsed.data;
}
```

Keep secret objects in a local ref/owned in-memory flow, never a query/mutation cache; do not put them into thrown error causes. Accept/decline follow the same imperative pattern with the exact request bodies, positive expected version, AbortSignal and generation checks. A generic API helper that forwards proof variables into mutationOptions is not a safe substitute.

## Workflow 8 — Additional Owner and separate step-down

**Prerequisite:** current server-eligible Owner for management; current eligible nominee for response. There are no email proofs in this flow.

**Sequence:** Owner [nominates](gateway-api-contract.md#nomination-create) an active same-workspace non-Owner membership → Owner may inspect [workspace list](gateway-api-contract.md#nomination-list) → nominee checks [personal nominations](gateway-api-contract.md#nomination-list-mine) and current canAccept → deliberate [accept](gateway-api-contract.md#nomination-accept) or [decline](gateway-api-contract.md#nomination-decline). Any current Owner can [cancel](gateway-api-contract.md#nomination-cancel). Nomination itself grants nothing; expiry is exactly seven days.

**After acceptance:** invalidate/refetch current membership, roles, nomination lists, discovery and audit. The summary lacks membershipVersion. Only after additional ownership is accepted may a current Owner request a separate [demotion](gateway-api-contract.md#member-demote-owner) using the latest target membership.version and required reason. Never chain an automatic transfer/step-down on success. Remaining roles or the server’s Member fallback are authoritative.

**Recovery:** handle stale, closed, sponsor-unavailable, already-Owner and last-Owner conflicts with current authorized reads/review. Decline remains possible after sponsor authority loss. Matching terminal replay requires the original closing subject’s current authorization; accepted replay additionally requires current Owner and never restores removed/demoted access. A different Owner cancelling a pending nomination does not become entitled to replay someone else’s terminal response. canAccept is a current caller-specific hint, not an eventual guarantee or universal nominee flag.

## Workflow 9 — Permitted audit history

**Prerequisite:** separate current organization.audit.read; member-read is insufficient. Call [audit list](gateway-api-contract.md#audit-list) with permitted actor/membership/aggregate target/action/time filters. Match filter names exactly: targetId means aggregate ID, not the membership link. Convert local date inputs to explicit-offset instants; from is inclusive and to exclusive, with from < to. Omit absent filters rather than sending empty strings.

**Sequence:** fetch page → validate event envelope/typed changes → render safe actor/time/action/reason and recorded revisions → continue unchanged filters with the opaque cursor. Preserve server ordering: occurredAt/eventId generally, membershipVersion/eventOrdinal/eventId for exact membership history. Do not infer chronology from formatted timestamps or re-sort a membership revision by display time.

**Projection:** implement action-discriminated runtime validation from the [16 registered changes variants](gateway-api-contract.md#audit-changes). Render known fields with meaningful labels; no raw JSON/HTML viewer, emails, proof material or invented schema-version field. Names/reasons may be null; system actors are null, and historical actors need not remain active members. Distinct grant periods and historical roles never confer current access.

**Recovery/cache:** every page reauthorizes. On audit 403 cancel/remove that audit scope, including retained privileged pages; preserve other still-permitted workspace data. Cursor/filter/mode mismatch is 400: reset the paging chain after reviewing filters. Corrupt unsupported stored schema is safe 500, not an empty successful history. Reuse resource-scoped invalidation after successful mutations; pagination is deterministic for stable data, not a snapshot across requests.

## Integration checklist

For each completed slice, use existing MSW/Vitest infrastructure for behavior tests, then perform permitted real-Gateway smoke checks with disposable accounts/data. MSW adapters are test evidence, not production endpoints. Do not write automated tests that perform irreversible actions on an operator’s live workspace.

- [ ] Exact methods/relative URLs, closed bodies, common envelope and all used response fields are runtime-validated against the reference. Include malformed envelope, missing/null/wrong-type fields and unsafe versions.
- [ ] Connected screens have accessible loading, real empty, safe initial/background error, pending, authorized success/no-change and no-permission states. Remove mocked successes, counters and preview query-state switches.
- [ ] Creation tests retain one attempt key after uncertain responses, handle 201/200/409/expiry and never promote the historical snapshot into current authority.
- [ ] Scope tests cover two accounts/workspaces, pagination/filter reset, cancellation, late 401/mutation results, local sign-out, explicit invalid selection and no stale/previous-data cross-scope display.
- [ ] Action tests cover separate member/invitation/audit reads, action-only admission, stale versions, protected Admin/Owner targets, safe reasons, 429 countdown and no automatic/optimistic authority writes.
- [ ] Invitation tests keep proof out of keys/caches/navigation/storage/logs, scrub the fragment, handle sign-in continuation/account change/reload, preview flags, sponsor loss, accept replay versus repeated decline, and fresh-role rejoin.
- [ ] Ownership tests distinguish nomination from grant, current canAccept from commit, acceptance from separate explicit step-down, and authorized replay from restored access.
- [ ] Audit tests cover typed changes/nulls, server order, cursor-bound filters/mode, revoked access between pages and safe handling of unsupported/corrupt data.
- [ ] Real smoke evidence records Gateway origin/environment, operations/statuses and limitations without recording credentials/proofs. At minimum: discovery → create → own membership; permitted manager invite → recipient accept; separate nomination/step-down; authorized audit retrieval, plus a safe unauthorized/cross-scope case.
- [ ] Run FE `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm run test:run`, `npm run build`; report actual results, responsive/keyboard/theme verification and browser limitations. Do not claim UI completion from backend tests.
- [ ] Reconcile stale FE integration/auth/architecture implementation statements as part of authorized FE work. Preserve approved routes/dependencies/theme/component rules and use the canonical UI-ESC-01 process when blocked.

## Copyable FE-agent brief

```text
Implement one approved Organization integration slice in DeliveryPilot-Web.
Read this repository's AGENTS.md and canonical UI/frontend instructions first.
Then read the backend FEAT-DP-003 frontend-integration.md, its selected workflow,
and the linked operations in gateway-api-contract.md. Verify current source/revisions.

Use the existing Axios client, memory auth session, ApiError, Zod, TanStack Query
and account/workspace cache helpers. Components contain no raw transport.
Do not add dependencies/routes/theme/generated component changes without the
repository's required authorization. /join-workspace is currently a route gap.

Implement only the selected real API behavior, with exact bodies/versions,
runtime-checked responses, current-session callbacks and no mocked success.
Keep proofs/tokens out of storage, URLs beyond the incoming scrubbed fragment,
query/mutation caches, logs and navigation state. Recipient joins do not require
existing workspace membership. The delegable permission list is not a capability API.

Add meaningful colocated/MSW behavior tests. Run the FE's required verification
commands and permitted real-Gateway smoke checks. Report changed files, actual
results, unresolved gates and remaining slices. Never infer current authority
from cached roles, fixture slugs, successful delivery or a creation snapshot.
```

## Source and maintenance notes

Backend sources: [published contract and evidence](gateway-api-contract.md#sources), [feature](feature.md), [execution/acceptance](backend-acceptance.md). Frontend source paths above are relative to the separately owned DeliveryPilot-Web checkout, not links to assumed files in this backend repository. The inspected FE commit has no uncommitted changes; this handoff edits no FE code/docs.

Maintain both guides together when the wire contract or verified FE integration conventions change. Neither file is an alternative requirements inventory or an approval record. The document creation check parses examples, checks the 30-route inventory and verifies links; it does not establish that all example calls were executed. Existing task 014 test counts are prior backend evidence, not newly performed FE/browser verification.
