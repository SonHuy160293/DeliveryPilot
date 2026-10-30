---
id: "FEAT-DP-003"
title: "Organization & Membership"
status: "ready"
priority: "P0"
release: "MVP"
depends_on: ["FEAT-DP-002"]
---
# FEAT-DP-003 — Organization & Membership

Frontend integration handoff for the implemented backend: start with the [workflow playbook](frontend-integration.md), then use the [Gateway API contract and cURL reference](gateway-api-contract.md). These describe verified implementation and integration gaps; they do not change requirement approvals or authorize frontend route/dependency changes.

> **Migration provenance:** Preserves the previous candidate feature discovery. The migrated contracts were draft; the subsequent joint readiness review below changes internal completeness status without customer approval. Initial candidate IDs `*-DP-002-01` were previously zero-padded to `*-DP-002-001`. On 2026-11-01, User Initialization was inserted as FEAT-DP-002; Organization moved to FEAT-DP-003 and its artifacts to `*-DP-003-NNN` at the user's request. Historical Organization IDs are superseded by this mapping; statuses and approval evidence are unchanged.

## 1. Problem and outcome

Solo practitioners and agency teams need a workspace where participation and authority can be managed without leaking another workspace’s data. This business problem remains a proposed need; the recorded workflow and selected design supply the intended behavior, not customer validation.

**Outcome:** a registered person separately establishes a workspace and its initial Owner; permitted operators manage invitations, roles and membership; authorized readers inspect current participation and attributable history. Project access remains separately authorized.

**Reconciliation — 2026-11-03, Asia/Saigon:** Existing draft IDs are preserved and missing coverage is added using the canonical templates. [Workflow discovery](../../deliverypilot-workflow-discovery.md) Q1–Q6, the [Identity decisions](../../architecture/modules/identity/design-decisions.md) and this conversation’s five selected choices are the evidence. The subsequent joint review uses U11–U21 to resolve the selected scope; approval references remain null. No model, API, migration, deployment or runtime test is established by this update.

## 2. Actors and scope

| Actor | Responsibility and boundary |
|---|---|
| Registered person | May create a workspace independently of registration and becomes its initial Owner. |
| Organization Owner | Holds protected ownership; may nominate an existing member for additional ownership. |
| Authorized inviter / membership operator | Uses specifically granted invitation, member-read or removal powers; those are distinct permissions. |
| Authorized organization role manager | Owner/Admin or explicitly delegated manager, subject to current management authority and delegation ceiling. |
| Organization Member | Selects permitted workspaces; membership alone grants no project access. |
| Invited user / former member | Accepts an offer for their signed-in account; rejoin uses fresh authorization. |
| Authorized audit reader | Reads only permitted history; recording events creates no read grant. |
| Project guest / platform operator | Separate scoped authority; neither requires nor implies general customer-content access. |

U11/AUTHZ-07 selects member-list and audit readers: Owners/Admins plus explicitly delegated readers, with separate read grants. Each member may inspect their own membership/roles. Owners/Admins and delegated membership managers are eligible for ordinary non-Owner removal. U12 selects Owner-only removal of Admin memberships, Owner-controlled normal Owner reduction with continuity, and the member field set. U13 selects Owner-only Admin-role demotion/revocation, the ordinary-removal permission ceiling, and Owner/Admin management defaults. U18/U20 settle join-time/history and audit fields; the planned code-catalog mapping is now in the backend task index.

### In scope for this release

- Separate workspace creation, initial Owner establishment and permitted workspace selection.
- Recipient-specific invitations, multiple authorized offered roles, proof/expiry checks, resend and reissue.
- Authorized invitation inspection/cancellation and proof-protected recipient preview/decline, with closed-offer and delivery/replay policies.
- Protected built-in roles, custom roles, additive ordinary assignments and constrained delegation.
- Additional-Owner nomination/acceptance and normal last-owner continuity.
- Scoped member inspection, membership removal and fresh-authorized rejoin with durable history.
- Protected change recording and separately authorized audit retrieval.

The selected business scope is jointly reviewed for technical task planning. Deferred lifecycle/operational capabilities are not included by implication, and no production release is claimed.

### Out of scope for this update

- Project CRUD, project roles/invitations and guest admission (FEAT-DP-004); client sharing (FEAT-DP-015).
- Billing, SSO/SCIM, organization co-ownership of projects, platform bootstrap/support bypass.
- Application implementation, actual schema application and deployment. The subsequent backend planning pass defines proposed API/DTO/migration deliverables in tasks without implementing them.
- Membership suspension/restoration commands/UI; workspace suspension/archive/delete; global account deletion/email reuse; automated audit purge/erasure; privileged platform/bootstrap/recovery operations. Selected denial, containment and history-retention behavior still apply.

## 3. Selected rules and decision provenance

| Area | Selected behavior | Evidence |
|---|---|---|
| Naming / creation | Only a trimmed nonblank name, at most 160 characters; duplicates allowed; no slug. A registered creator becomes initial Owner separately from registration. | Q1; CREATE-01/U9. |
| Ownership | Multiple Owners; normal changes preserve an active eligible Owner. An existing Owner nominates an existing member; nominee acceptance is required for additional ownership. Transfer is add/accept then separate step-down; demotion retains other roles or assigns Member if none remain; operator recovery is deferred. | OWN-01/04/05; U9. |
| Membership | One durable User/workspace relationship; rejoin uses freshly authorized roles, never implicit old grants. | MEM-01/02. |
| Roles | Additive allowed organization roles; protected Owner/Admin/Member definitions plus custom roles. Delegated role managers need management authority and held, catalog-delegable powers. | AUTHZ-01–06; DATA-03. |
| Recipient proof | Valid single-use link and matching current signed-in primary email; no separate fresh code or silent global email verification. | INV-01. |
| Pending offers | At most one pending invitation per workspace/normalized recipient email, regardless of role combination. | INV-02. |
| Invitation timing | Seven days after issue. Explicit resend rotates proof, invalidates the old link and restarts seven days. | INV-05/06; U10. |
| Acceptance authority | Offered permissions must remain unchanged and inviter authority must still hold. Changed permissions or lost authority requires current authorized reissue. | AUTHZ-04; INV-04/U10. |
| Reader/removal eligibility | Member-list and audit reads: Owners/Admins plus explicitly delegated readers; each member may inspect their own membership/roles. Ordinary removal eligibility: Owners/Admins plus delegated membership managers. Only Owners may remove Admin memberships; Owners control normal Owner reduction with active-Owner continuity. | AUTHZ-07/08; U11/U12. |
| Member fields | Scoped readers see display name, primary email, membership state, current workspace roles and join time. No public/global directory is implied. Original join time persists across rejoin; active default list and explicit removed/suspended filters apply, with historical roles restricted to audit readers. | DATA-04; U12. |
| Boundaries / history | Workspace roles do not supply project/platform authority. Membership/grant history is retained separately from current access. | Q2/Q3; SCOPE-04; DATA-01/02. |

U9–U21 are local provenance labels for the user’s explicit answers in this conversation, 2026-11-03, Asia/Saigon; they are not approval references. U11–U21 jointly settle reader/default/target policy, retries, lifecycle boundaries, revocation, retention, audit consistency and abuse limits.

### Selected built-in action defaults — U11–U13

| Action | Owner | Admin | Member / delegated actor |
|---|---|---|---|
| Inspect own membership/roles; select permitted workspace | Yes | Yes | Yes, while eligible. |
| Read workspace member list/current roles | Yes | Yes | Explicit member-read grant required. |
| Read membership/role audit | Yes | Yes | Separate explicit audit-read grant required. |
| Issue/inspect/resend/reissue/cancel invitations | Yes | Yes | Relevant explicit invitation grant; role offers also meet delegation ceiling. |
| Manage custom definitions / ordinary role assignments | Yes | Yes, subject to protected targets | Explicit role-management authority plus held/delegable permissions for grants. |
| Remove ordinary non-Admin/non-Owner member | Yes | Removal permission + target powers within actor powers | Delegated removal permission + same target-power ceiling. |
| Remove Admin membership; revoke/demote Admin role | Yes | No | No. |
| Nominate additional Owner; reduce Owner responsibility | Yes, nomination acceptance / continuity rules apply | No | No. |
| Grant platform or implicit project access | No through workspace powers | No | No. |

Customer editing of built-in definitions remains prohibited even for Owners. Admin-target protection takes precedence over ordinary management and removal grants; Owner-target protection takes precedence for multi-role targets. This matrix is selected business behavior; permission-code representation is still refinement work.

## 4. Security and NFR considerations

BR-002/003/005/006/008 constrain identity, resource scope and delegation. BR-007 remains a proposed project-removal rule, not an automatic organization-to-project cascade. NFR-001/002 apply scoped isolation and authorization; NFR-003/004 attribution and consistent changes; NFR-008 data minimization/retention. Detailed applicability is owned by each FR. Invocation limits, deadlines and input boundaries are explicitly user-selected; performance/availability SLAs are not inferred from them.

Current identity/session validity is insufficient to establish workspace action authority. Do not leak membership, invitation or event existence across scope, log proof material, or infer grants from a caller-supplied workspace ID.

## 5. Model and implementation boundary

Identity owns Authentication and Organization Management in the selected combined module. The [Identity workspace](../../architecture/modules/identity/README.md) owns its glossary, ERD, dictionary and implementation baseline. This feature specifies observable behavior, not tables. Shared audit history replaces the original standalone MembershipAuditEvent modeling suggestion.

Additional-Owner nomination needs a separately reviewed representation; this update adds no nomination table or messaging design. Physical diagrams remain proposals. The joint review inspected the current Identity DbContext/model drafts and Application inventory read-only to distinguish requirements from implementation; no build, application test, migration or live-deployment check was run.

## 6. Dependencies and related work

- FEAT-DP-002 supplies identity/session prerequisites. Its known validation/readiness issues remain separate.
- FEAT-DP-004 owns projects, project participation and resource authorization; FEAT-DP-015 owns client sharing.
- FEAT-DP-007 consumes independently established reviewer authority; FEAT-DP-017 must authorize traceability retrieval.
- The Notification capability may deliver invitation material, but delivery is neither acceptance nor a membership grant. Delivery/retry behavior remains OD-09.

## 7. Open decisions and task-planning blockers

**Disposition after joint review — 2026-11-03:** no unresolved business-policy blocker remains for the selected Organization release. The historical heading/OD labels remain stable for references. Each former open group is resolved by a selected rule, an explicit release exclusion, or a technical handoff that must preserve the selected behavior. No unnamed approver is invented.

| Group | Disposition and selected outcome | Evidence / handoff |
|---|---|---|
| OD-01 — Permissions / fields | Resolved: Owner/Admin and explicit delegate defaults; separate member/audit grants; own inspection; Owner-only Admin reductions; ordinary-removal ceiling; selected member/audit projections and reason rules. | U11–U13/U18/U20. Permission-code names and transport mapping are technical task design, not permission-policy invention. |
| OD-02 — Creation retries | Resolved: same-person/same-attempt matching-content replay for 24 hours; changed content conflicts; expired attempt rejects with explicit new-attempt guidance; new intent may create same-name workspace. | U14/U21. Receipt storage/transport is task design. |
| OD-03 — Invitations / responses | Resolved: one pending offer; duplicate Issue conflicts; explicit Resend/Reissue; matching current sign-in plus valid proof for preview/decline; closed offers need fresh Issue; first valid committed transition wins; accepted-subject replay has no new grants. Changed email cannot redeem the old offer; any new address needs a separately authorized matching-address offer. | U10/U14/U16/U21. No alias/history override or automatic forwarding; global address-change workflows deferred. |
| OD-04 — Ownership | Resolved: active existing non-Owner nominees, seven-day in-app offers, one pending per workspace/member, active sponsor/nominee at acceptance; current Owners may cancel, nominee may decline; duplicate pending conflicts. Additional acceptance leaves existing Owners; transfer then uses separate step-down. Demotion retains other roles or assigns Member; normal continuity and Owner-only control apply. | U9/U12/U15/U16/U21. Nomination persistence/concurrency is task design. |
| OD-05 — Membership | Resolved: active members use role management, removed members may rejoin with fresh roles/durable identity, suspended joins denied. Original join time and active default/historical-filter policy selected. Suspension/restoration commands/UI explicitly deferred. | U14/U18. History retains prior periods, not current entitlement. |
| OD-06 — Roles | Resolved: input/uniqueness bounds and reserved built-ins; system-supplied stable identity; retirement only after current assignments/pending offers clear; retained history; stale edits rejected; repeated unchanged acts have no new grant period; reviewed built-in upgrades only. | U13/U17/U21. Future upgrades are separately reviewed work. |
| OD-07 — Revocation | Resolved: new decisions after commit deny revoked powers; admitted reads may finish; Identity writes recheck at commit and jobs at execution; unavailable required facts fail closed. | U19. External resource owners need explicit technical contracts; no cross-database atomicity claim. |
| OD-08 — Retention | Resolved for release: selected minimized member/audit fields and workspace-lifetime membership/role history; automated purge/erasure and global account deletion/email reuse deferred. | U12/U18/U20. Actual data-request/legal and production operating processes remain separately scoped responsibilities. |
| OD-09 — Evidence / failures | Resolved: required success history and mutation fail together; no false success on rollback; minimal separate denial telemetry. Delivery failures preserve pending offer and show status; retry current proof without deadline extension. Issuance limits are configurable: 60-second pair cooldown, 10 pair issuances/24h, 100 workspace issuances/hour. | U16/U20/U21. Retry/backoff/provider integration is technical design; no durable denied-attempt history claimed. |
| OD-10 — Navigation / resources | Resolved: remember only eligible workspace, otherwise selector with no silent switch; name/stable ID/own roles displayed; zero-membership guidance preserves independent guest access. Project/Portal still owns admission/content; workspace grants supply no wildcard. | U12/U18; Q2/Q3. Client storage and resource contracts are task design; Project functionality is not implemented here. |
| OD-11 — Lifecycle / containment | Resolved release boundary: create active workspaces; normal operations require active state; lifecycle/delete/restore and operator recovery commands deferred. Deny compromised accounts; no eligible Owner blocks privileged changes without auto-promotion; ordinary reads still require valid grants. | U18/U19. Production recovery assurance is a separate operating gate, not an unresolved FR branch. |

**Eligibility clarification:** an active eligible Owner is an active User with active membership and a currently effective Owner assignment in the active workspace. Counting Owners does not require them to be currently signed in; the caller of a protected operation still needs current authenticated authority. This follows the separation of durable responsibility from authentication sessions.

## Artifact directory and readiness

Canonical contracts are in [ur/](ur/), [fr/](fr/), and [us/](us/). The joint policy review resolves all in-scope OD groups; internal readiness review has checked evidence, behavior, negative/concurrent scenarios and one-way references. The feature and 31 source contracts are `ready` for technical task planning. Approval references remain null; no customer approval, code completion or release is claimed. Derive reverse coverage with the CLI.

[Backend task plan](backend-plan.md) now contains 14 source-inspected deliverables under the user’s explicit backend-only choice. TASK-DP-003-001 retains its stable ID and has been technically replanned. TASK-DP-003-002 is pending and has no unfinished coding-task prerequisites; other tasks are blocked on declared dependencies and require fresh source review before execution. Codebase verification records inspection, not implementation or test success. No UR/FR/US status or approval reference changed during task planning.
