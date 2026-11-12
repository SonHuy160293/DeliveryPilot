---
id: FEAT-DP-002
title: "User Initialization"
status: ready
priority: P0
release: "MVP"
depends_on: []
---
# FEAT-DP-002 — User Initialization

## Problem and business outcome
Prospective and returning agency users and external collaborators need to establish a platform identity, authenticate as that identity and end authenticated access before they can safely participate in delivery work. The roadmap identifies registration, login and logout as MVP/P0 scope; it does not settle the detailed authentication or session policy.

**Intended outcome:** An eligible person can register, a returning eligible user can sign in under the correct identity, and a user can end the reviewed access scope with a clear result. Acceptance must include rejected attempts, duplicate/concurrent submissions, ended-context replay and resource-authorization boundaries. These outcomes are reviewed for internal completeness; `ready` implies neither client approval nor implementation completion.

## Actors and scope
| Actor | Need / boundary |
|---|---|
| Prospective platform user | Establish an identity through the reviewed enrollment method |
| Returning agency user or external collaborator | Prove control of an eligible identity and reach a permitted next step |
| Authenticated user | End the reviewed authenticated scope without changing another user's identity |
| Authorized security/support reviewer | Investigate reviewed outcomes without access to raw credentials |

**In scope:** Registration, login and logout; required input/eligibility feedback; duplicate/retry behavior; authentication-context validity; privacy and the safeguards needed for these journeys. Registration and login remain separate from organization, project and approver authorization.

**Outside the current scope:** Organization creation/membership, invitation acceptance, project roles, client sharing, approval decisions, account deletion, identity merge/linking and user-profile administration. Password/account recovery, MFA, enterprise SSO/SCIM, social login and all-device session management require separate scope decisions. The confirmed registration phase uses local email/password credentials.

## Feature policy and constraints
- **Agreed direction:** The [roadmap](../../roadmap.md) establishes FEAT-DP-002, MVP/P0 and the three journeys. BR-002 permits one identity across several organizations; BR-004/BR-005/BR-008 preserve resource authorization boundaries.
- **Confirmed registration:** Anonymous DisplayName/Email/Password enrollment creates an active user, returns Created/userId and does not verify email or log the user in. Email is trim/case-insensitive unique; duplicate feedback is exactly "This email is exist".
- **Reviewed login/session policy:** The user confirmed active-account login, configurable JWT and rotating-refresh lifetimes, current-session logout, account revalidation and client-owned onboarding. Login requirements and story are internally ready. *Update 2026-11-12:* the logout FR/US are now `ready` as well (UR-DP-002-003, FR-DP-002-006, US-DP-002-003), and all five tasks are done (TASK-DP-002-005 delivered refresh and logout).
- NFR-001/NFR-002 constrain isolation and authorization; NFR-004 covers integrity/concurrency; NFR-008 covers private authentication data. Durable security evidence, retention and abuse-limit implementation are deferred for initial registration and login.
- **Implementation context (reviewed 2026-11-08):** [Current backend documentation](../../../ARCHITECTURE.md) and [AuthController](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/AuthController.cs) establish registration/login endpoints; User, PasswordCredential and AuthSession are mapped. Session issuance/validation exists; refresh/logout HTTP endpoints remain follow-up work. Organization persistence and bounded authorization workflows are implemented separately under FEAT-DP-003; privileged platform operations remain deferred. Authentication itself supplies no resource grants. See the [Identity implementation note](../../architecture/modules/identity/README.md#verified-implementation-baseline); target host consolidation remains a proposal.

## Key journeys and edge cases
1. **Registration:** Applicant submits policy-required details/proof; the system establishes one eligible identity or returns safe feedback and explains the next permitted step. Include invalid/ineligible submissions, duplicate equivalents, concurrent requests and uncertain retries.
2. **Login:** Returning user supplies valid proof for an eligible identity; the system establishes an authenticated context without adding resource permissions. Include unknown/ineligible identities, invalid proof, abuse controls and dependency failure.
3. **Logout:** User requests the reviewed scope to end; the system reports completion only at the reviewed boundary. Include already-ended contexts, replay, concurrent continuation, other sessions, cross-user targeting and unconfirmed failure.
4. **Authorization handoff:** An authenticated person still needs the owning feature's membership, project, sharing and approver permissions. Login or a caller-supplied TenantId is not an access grant.

## Artifact index
Needs are in [ur/](ur/), observable behavior in [fr/](fr/), and user-valued acceptance scenarios in [us/](us/). The 3 URs, 8 FRs and 3 USs for registration, login and logout are all `ready` for internal completeness, with `approval_ref: null`. The feature status was aligned to `ready` on 2026-11-12 to match these reviewed source artifacts; this records no approval. Each artifact owns its contract. Derive downstream coverage with:

```text
python tools/docs.py coverage --feature FEAT-DP-002
```

Deliverables are in [tasks/](tasks/), recording the user's 2026-11-02 decisions and subsequent confirmation. TASK-DP-002-001/002/003/004 are recorded as done; TASK-DP-002-002 was closed on 2026-11-09 after its PostgreSQL-backed acceptance run. Refresh and logout are implemented by TASK-DP-002-005 (2026-11-09): `POST /api/v1/auth/refresh` and `POST /api/v1/auth/logout`, published through the Gateway under `/identity`. Status changes record implementation evidence, not approval; ready source status records internal completeness only.

## Dependencies and decisions
- **FEAT-DP-003 — Organization & Membership:** Consumes authenticated identity; owns organization/invitation membership and authorization. Review its [existing discovery](../FEAT-DP-003-organization/feature.md) for handoff overlap, without changing its draft contracts here.
- **FEAT-DP-004 — Project Initialization:** Owns project roles and resource scope.
- **FEAT-DP-007 — Approval Workflow:** Reviewed against BR-016 through BR-023 in [global requirements](../../requirements.md). Authentication does not confer approval authority, satisfy an applicable review policy, constitute an approval decision or delete prior approval attribution at logout. No detailed FEAT-DP-007 feature folder currently exists; this review creates no approval artifacts. Approver authority and in-flight decision rules remain with that feature.
- **FEAT-DP-015 — Client Portal:** Owns external sharing. An external client's login grants no broad tenant visibility.
- Notification delivery is a possible dependency only if a reviewed verification/notification policy requires it; existing delivery infrastructure is not evidence of an implemented onboarding journey.

Registration decisions D-001/D-002/D-003 were resolved by the user on 2026-11-02: anonymous email/password registration with DisplayName/Email/Password, case-insensitive unique email, immediate activation, Created/userId, exact duplicate feedback, no verification and no automatic login. D-005/D-007 work is deferred for initial registration and login; password confidentiality is retained. The user has also reviewed the login/session policy below. The refresh and logout HTTP contracts are implemented by TASK-DP-002-005 (D-006 resolved 2026-11-09). Ready source artifacts denote internal completeness, not client approval.

| Decision | Review owner | Needed decision / impact |
|---|---|---|
| D-001 | User confirmation, 2026-11-02 | Public anonymous email/password enrollment; active users later log in with email/password and receive the reviewed authentication context. |
| D-002 | User clarification + technical review, 2026-11-02 | Registration resolved: platform-wide trim/case-insensitive email uniqueness, exact duplicate text "This email is exist", no existing-account update. PostgreSQL lower(PrimaryEmail) remains authoritative. Login unknown-account feedback remains separate. |
| D-003 | User confirmation, 2026-11-02 | Registration uses DisplayName/Email/Password, immediate active status, no verification, Created/userId and no automatic login. Active users can later sign in with email/password without verified email; the client chooses onboarding for users without membership. |
| D-004 | User clarification, 2026-11-02; security review pending | Access JWT lifetime is 10 minutes and refresh-token lifetime has a fixed 7-day maximum from login; lifetime values come from appsettings. There is no additional idle timeout or simultaneous-session count limit. Refresh tokens rotate on use; reuse of old proof is rejected without revoking the current session. Recheck account status on protected requests and refresh. |
| D-005 | User phase deferral, 2026-11-02 | Rate limiting and lockout are deferred for initial registration and login. Atomic rollback and safe failure feedback remain required. |
| D-006 | User clarification, 2026-11-02; security review pending | Logout revokes only the current session's refresh token and immediately rejects its already-issued access tokens. Requests already admitted may finish. User decision 2026-11-09: **logout wins** a race with refresh. Both operations serialize on the session; if refresh commits first, the following logout revokes the newly rotated token and its access tokens, so the session always ends. Logout is **idempotent and always returns 204 No Content** and clears the refresh cookie, including when the session is already ended, expired or absent; only an unexpected server failure returns 5xx so the client can retry. |
| D-007 | User phase deferral, 2026-11-02 | Durable security-event storage and related access/retention are deferred for initial registration and login. Authentication secrets must remain confidential. |

**Duplicate/overlap review:** The existing Organization drafts assume an authenticated user and cover organization creation/invitations, not platform registration/login/logout. Their historical Organization IDs were moved to FEAT-DP-003; do not duplicate those contracts under the new FEAT-DP-002. Session validity and secret handling are shared safeguards referenced by the three stories rather than additional technical stories.

## Evidence and review
- [Roadmap](../../roadmap.md): FEAT-DP-002, VS-00, foundation sequencing and the account-journey acceptance item are the explicit scope source.
- [Product](../../product.md): Agency and client personas and the requirement-to-delivery journey supply business context.
- [Global requirements](../../requirements.md): Identity/resource boundaries, approval principles and NFR categories constrain the drafts; proposed BRs remain proposed.
- [Authoring policy](../../authoring.md): Draft status, approval evidence and one-way traceability apply.
- User confirmed FEAT-DP-002 User Initialization in this conversation after the command text also mentioned Approval Workflow. No interviews, client approval or version-specific approval evidence were supplied or inferred.

**Next review:** Security review of D-004/D-006 remains pending, and FE wiring of refresh/logout is a frontend handoff. Structural validation is not client approval.
