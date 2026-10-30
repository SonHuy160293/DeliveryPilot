# Identity & Organization Module Design

> Status: Working design with the existing authentication baseline and a proposed nine-table physical column draft. Organization business choices are jointly resolved for task planning; proposed physical design and operational gates still prevent production finalization.
> Recorded: 2026-11-03, Asia/Bangkok.
> Module name: Identity. Internal areas: Authentication, Organization Management, and Platform Access.
> Authority: User-selected design scope and recorded decisions are distinguished from implemented behavior and unreviewed proposals. This documentation does not approve canonical requirements or itself authorize application implementation; the user's later model/mapping implementation instruction has its own scope.

## Selected scope

Identity will own one PostgreSQL database containing authentication, organization management, and a protected platform-assignment foundation. Users remain global identities. Organization membership is a separate relationship; a User may have none or participate in several organizations.

| Internal area | Owns | Design scope |
|---|---|---|
| Authentication | User, PasswordCredential, AuthSession; invitation-recipient verification | Preserve current registration/login/session behavior; design the additional proof needed for invitation redemption. |
| Organization Management | Organization, OrganizationMembership, organization roles/assignments and invitations | Workspace creation, membership lifecycle, scoped delegation and ownership continuity. |
| Platform Access | System-controlled PlatformRoleAssignment and privilege governance | Assignment foundation inside Identity, isolated from organization role administration. No customer-content bypass. |

Project, ProjectMembership, ProjectRole, ProjectInvitation, titles, project responsibilities, requirements, reviewer assignments and approvals remain outside this module. A guest's User can exist without OrganizationMembership. Future Project/Delivery modules own guest admission and resource sharing.

MFA, account recovery, SSO, support-access grants, billing/entitlements and platform operational features are deferred. Platform assignment modeling does not make privileged operations safe to enable before assurance, bootstrap, recovery and audit procedures are reviewed.

## How to use this workspace

| Document | Purpose |
|---|---|
| [Glossary](GLOSSARY.md) | Business language used consistently throughout the design. |
| [Design decisions](design-decisions.md) | Selected scope, user answers, proposed defaults, decision rounds and evidence. |
| [ERD](erd.md) | Conceptual ownership, logical relationships, existing physical baseline and new proposed column diagrams. |
| [Data dictionary](data-dictionary.md) | Existing columns plus types, nullability/defaults, integrity and purpose for nine proposed tables. |
| [Review scenarios](review-scenarios.md) | Expected behavior, decision dependencies and validation results; not application test claims. |

## Evidence and reconciliation

| Evidence | What it establishes |
|---|---|
| User's combined-module preference, scope answers and instruction to implement the ERD design plan in this chat, 2026-11-03 | Identity includes Organization and a protected Platform Access area in one module-owned database. First-pass authentication scope is core plus invitation proof. |
| User's ownership/membership answers in this chat, 2026-11-03 | Multiple Owners with last-owner protection; durable membership with history; newly authorized roles on rejoin. See OWN-01, MEM-01 and MEM-02. |
| User's organization-authorization answers in this chat, 2026-11-03 | Multiple concurrent organization roles with combined allowed permissions; protected built-in roles plus customer-defined custom roles. See AUTHZ-01/02. |
| User's follow-up delegation/invitation answers in this chat, 2026-11-03 | Ordinary role management includes explicitly delegated managers; changed offered permissions require invitation reissue; single-use link plus matching signed-in email supplies recipient proof. See AUTHZ-04/05 and INV-01. |
| User's invitation-uniqueness answer in this chat, 2026-11-03 | One pending invitation per organization and normalized email. Lifetime/resend were pending in that round; U10 now selects seven days and resend with rotated proof/restarted lifetime. See INV-02/05/06. |
| User's column-design answers and implementation instruction in this chat, 2026-11-03 | Code-defined catalog; stable assignments with GrantVersion; shared audit history; own-permissions-plus-management delegation ceiling. Supplied columns are documented as a review draft, not an implemented or approved production schema. See U7/U8 in the decision log. |
| User's FEAT-DP-003 refinement answers in this conversation, 2026-11-03, Asia/Saigon | Name-only creation (trimmed/nonblank, at most 160 characters, duplicates allowed, no slug); existing-Owner nomination and nominee acceptance for additional ownership; seven-day invitations, restarted lifetime on resend with old proof invalidation, and current inviter authority at acceptance. See CREATE-01, OWN-05, INV-04/05/06 and U9/U10. These are selected behaviors, not artifact approval. |
| [Workflow discovery](../../../deliverypilot-workflow-discovery.md), §§2, 4, 14, 18–19; Q1–Q6 | Registration and workspace creation are separate; creator becomes Owner; organization/project authority differs; guests need not join a workspace. Later user refinements have separate U9–U21 provenance; this row does not approve derived artifacts. |
| [Original domain discovery](../../identity-domain-discovery.md) and [readiness analysis](../../identity-erd-readiness.md) | Alternatives, invariants and unresolved decisions. Their separate Identity/Organization recommendation is superseded for this design by the user's combined-module choice. |
| [Cross-feature requirements](../../../requirements.md), BR-001–008 and NFR-001–004/008/011 | Existing agreed direction and proposed rules, with their original authority unchanged. |
| [User Initialization](../../../features/FEAT-DP-002-user-initialization/feature.md), D-001–D-007 | Recorded registration/session decisions. Its summary was reconciled during the documentation audit; some task planning/status text still needs a separate evidence review. Source establishes what exists. |
| [Organization discovery](../../../features/FEAT-DP-003-organization/feature.md) | Existing Organization feature and draft contracts; design notes here do not change their statuses or approval evidence. |
| [Current backend architecture](../../../../ARCHITECTURE.md), [solution](../../../../DeliveryPilot.slnx), [Identity deployment](../../../../deploy/compose/identity/compose.yaml) | Independently hosted current components. This module design does not consolidate executables or change deployment. |

The important ownership correction is local integrity: OrganizationMembership can reference Users through a local FK because both are Identity-owned. Invitation issuer/acceptor and platform-assignee User references can also be local. Project/Delivery references remain scalar external IDs with no cross-module FK. Different aggregates inside Identity retain their own rules even when a transaction spans several of them.

## Verified implementation baseline

Source and migration history were inspected; no live database was queried.

**Implementation reconciliation — 2026-11-03:** The later model/mapping task is incomplete. Eight new plain domain classes are present in [Models](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Models/): Organization, OrganizationMembership, OrganizationRole, OrganizationRolePermission, OrganizationRoleAssignment, OrganizationInvitation, OrganizationInvitationRoleOffer and PlatformRoleAssignment. IdentityAuditEvent is absent, and none of the eight has an EF configuration or DbContext registration. The mapped business model remains User, PasswordCredential and AuthSession; no new collaboration migration/schema or authorization workflow is established. The [alignment audit](../../documentation-alignment-audit.md) records the handoff; this documentation audit does not verify factory completeness or rerun application tests.

| Existing object | Source evidence | Implication |
|---|---|---|
| `shared.Users` | [User mapping](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs) and [InitialIdentity](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261025052513_InitialIdentity.cs) | Preserve identity, fields and case-insensitive email uniqueness. |
| `shared.PasswordCredentials` | [Credential mapping](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/PasswordCredentialConfiguration.cs) and [credential migration](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261101173630_AddRegistrationCredentials.cs) | One password credential at most per User; local restricted User FK. |
| `shared.AuthSessions` | [Session mapping](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/AuthSessionConfiguration.cs) and [session migration](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261102041840_AddAuthSessions.cs) | Multiple independent sessions; verifier, fixed refresh deadline and revocation. |
| Operational messaging/reliability records | [IdentityDbContext](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/DbContexts/IdentityDbContext.cs) | Retain separately from business entities; history of outbox/delivery work is not a membership or security-audit model. |

Current registration creates an active User without verified email, authentication tokens or workspace membership. Login issues an identity-oriented JWT and rotating refresh proof. Recorded policy uses configurable 10-minute access and fixed seven-day refresh lifetimes. Identity checks active account and unrevoked session per protected bearer request. This is not organization authorization, email ownership proof, MFA or privileged elevation.

Refresh/revoke services exist; refresh/logout HTTP endpoints remain planned. Existing logout policy allows already-admitted requests to finish. Initial registration/login rate limiting, lockout and durable security-event storage were deferred; this design does not silently add them to that contract.

**Joint Organization readiness review — U11–U21:** readers/defaults, protected targets/delegation, fields/reasons, retries, role/nomination lifecycle, revocation, retention, abuse limits and release exclusions are selected in the log and canonical contracts. No open business-policy blocker remains for the selected scope. Physical nomination persistence, authorization interfaces, mappings and runtime evidence still need source-aware task design/implementation.

## Design sequence and gates

| Stage | Work and output | Current state / gate |
|---|---|---|
| A: baseline and ownership | Scope, glossary, implemented inventory and conceptual ownership diagram | Documented; source observations are evidence, not live-deployment verification. |
| B1: ownership and membership | Owner count, membership permanence and rejoin grants | Multiple ownership, durable memberships/fresh rejoin roles and additional-Owner nomination/acceptance selected. U12/U15/U16/U21 settle normal reduction, seven-day in-app nomination, explicit transfer and terminal outcomes. U18/U19 defer suspension/restoration and operator recovery while enforcing denied states/containment. |
| B2: organization authorization | Role combinations, protected templates, delegation and edits | Multiple roles, protected built-ins, eligible managers and own-permissions-plus-management ceiling selected. Stable grants and code-defined catalog selected; snapshot/version representation now drafted. U13/U17 select the capability matrix and lifecycle; code representation is task design. |
| B3: invitations | Recipient proof, duplication, expiration/resend, acceptance and privilege drift | Single-use link plus matching authenticated email and one pending offer per organization/email selected. Seven-day expiry, restarted seven days on rotated resend and current sponsor authority selected under U10. U14/U16/U21 select duplicate/conflict/terminal/replay outcomes and issuance limits; current-address proof has no alias override and a different address needs a new authorized offer. Preserve unverified registration. |
| B4: platform privilege | Assignment lifecycle, grant authority, bootstrap/recovery and expiry | Pending; privileged operations remain deferred. |
| B5: retention and revocation | Attribution, email reuse, deletion, history storage and access-change boundary | Shared audit/history storage selected. U19/U20 select the revocation boundary, required success-audit consistency and workspace-lifetime history; purge/erasure and durable denial history are deferred. |
| C: logical design | Aggregate boundaries, identity/cardinality, local integrity, transaction and external contracts | Candidate view synchronized with the column draft; business review gates remain. |
| D: physical design | Types/nullability, schema, keys, constraints, indexes and compatibility | Nine proposed shared-schema table definitions and column diagrams documented alongside unchanged baseline. Production finalization waits for remaining policy and readiness review. |

Rounds use a concrete scenario, alternatives, a recommendation and its ERD consequence. Only explicit answers are recorded as user-selected. Other recommendations remain proposed even when diagrams illustrate them. Any changed answer requires impact review across the decision log, diagrams, dictionary and scenarios. The FEAT-DP-003 UR/FR/US contracts are internally ready after the joint review, with selected decisions and explicit release exclusions; [feature readiness](../../../features/FEAT-DP-003-organization/feature.md#7-open-decisions-and-task-planning-blockers) remains separate from these physical proposals. Additional-Owner nomination has no selected persistence representation in the nine-table draft.

Physical-design completion requires agreement between diagrams and dictionary, an enforcement owner for each invariant and validated compatibility with existing migrations; joint business readiness does not itself meet those technical checks. MFA/recovery and future platform operations remain separately scoped follow-up work.
