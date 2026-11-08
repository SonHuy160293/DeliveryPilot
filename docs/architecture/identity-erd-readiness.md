# Identity ERD Readiness

> **Historical reference — reviewed 2026-11-08:** Original alternatives/readiness are retained. Use the [Identity overview](modules/identity/README.md#verified-implementation-baseline) and [current schema](modules/identity/current-schema.md) for later evidence. Superseded module boundaries and unfinished-work statements below are not current instructions.


> Status: Draft conceptual/logical groundwork. Not a finalized physical ERD, accepted ADR, migration specification, or implementation authorization.
> Recorded: 2026-11-03, Asia/Bangkok.
> Primary analysis: [Identity Domain Discovery](identity-domain-discovery.md).
> Primary behavioral evidence: [Workflow discovery v1.1](../deliverypilot-workflow-discovery.md), read completely; especially §§2, 4, 14, 18–19.

> **Design reconciliation — 2026-11-03:** The user selected combined Authentication, Organization Management and protected Platform Access ownership inside Identity. Use the [Identity module workspace](modules/identity/README.md) and its decision log for the current selected scope and ERD readiness. The earlier separate-Organization ownership, external User-reference policy and B1 placement assumptions below are historical inputs to reconcile, not constraints on the combined design. User references inside this module may use local FKs; Project/Delivery references stay external. Multiple Owners, durable membership/history and newly authorized rejoin roles are now selected; other unanswered details remain proposals. No physical schema, host consolidation or canonical artifact approval follows automatically.

## Readiness conclusion and scope

**Ready for architectural review and conceptual modeling; not ready to finalize the physical ERD.** Identity should retain authentication/account ownership. Organization and Project should own their respective memberships, invitations, roles and assignments. Platform Access should own administrator assignments as a separately protected logical boundary. Its minimal future implementation could be packaged inside the existing Identity host, but that placement is a proposal requiring an accepted decision.

Logical context separation does not mandate separate deployments or databases. Current [solution](../../DeliveryPilot.slnx), [architecture](../../ARCHITECTURE.md) and [Identity deployment configuration](../../deploy/compose/identity/compose.yaml) show independent Identity, Notification and Gateway components. The [target monolith](../architecture.md) remains a proposal. No host consolidation, new module implementation, table, EF mapping, migration or production ERD is introduced here.

Evidence labels follow the main report: **Confirmed** means recorded workflow/current-request direction, not automatic canonical artifact approval; **Implemented** means source evidence, not an applied live schema; **Proposed** means this review's defaults; **Open** means a decision remains. All schema candidates below are Proposed unless explicitly marked Implemented. B/I/F/O references are the open-decision references in the main report §19, not new canonical requirement IDs.

## Verified Identity baseline

| Evidence | What it establishes | Limit |
|---|---|---|
| [IdentityDbContext](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/DbContexts/IdentityDbContext.cs) and [model inventory tests](../../tests/DeliveryPilot.Identity.Tests/Persistence/IdentityModelInventoryTests.cs) | User, PasswordCredential, AuthSession, plus operational reliability/messaging persistence | Tests inspected, not run here; no current Organization/Project/RBAC tables established |
| [User configuration](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs) and [migration history](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/) | Existing UUIDv7 User identity, status constraints, case-insensitive email uniqueness in migration history | Expression index is not fully represented by ordinary Fluent-model inspection; email reuse policy remains unresolved |
| [PasswordCredential configuration](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/PasswordCredentialConfiguration.cs) | UserId is PK and local restricted User FK | One current password credential per User, not a generalized federation/MFA model |
| [AuthSession configuration](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/AuthSessionConfiguration.cs) | Local restricted User FK, User lookup index, refresh-verifier length and deadline ordering constraints | Session validity does not establish tenant grants |
| [Auth controller](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/AuthController.cs), [session service](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/AuthSessionService.cs), [context validator](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/AuthenticatedContextValidator.cs) | Public register/login, session issuance and per-request account/session validation; refresh/revoke service behavior | Refresh/logout HTTP endpoints remain planned; organization/project revocation and privileged MFA are not established |

`RetainUsersOnly` is historical and was followed by credential/session additions. Do not reconstruct dropped tables from its rollback as a new ERD. Source indicates active registration without verified email; invitation proof is a new proposal. Existing registration/login durable security-audit expansion was deferred in [User Initialization](../features/FEAT-DP-002-user-initialization/feature.md). This report's future membership/platform audit requirements do not claim it already exists.

## Candidate entities, keys and aggregate boundaries

IDs below mean stable logical identifiers. Exact PostgreSQL types, lengths, names, enum representation, partitioning and DDL remain unfinalized. Preserve repository plural PascalCase tables/columns and quoted custom PostgreSQL identifiers in later implementation; persist instants as UTC DateTimeOffset according to repository guidance. Existing types/indexes require migration-aware inspection before any change.

| Candidate / owner | Root or child | Candidate keys / important attributes | Relationships and constraint candidates | Inclusion decision |
|---|---|---|---|---|
| User / Identity | Root; Implemented | UserId PK; normalized PrimaryEmail alternate uniqueness; status, display metadata, verification/login instants | Local credential/session references; external scalar UserId in other owners | Keep existing model; security version/tombstone details need B9/B10 |
| PasswordCredential / Identity | User-associated credential; Implemented | UserId PK/FK; protected password verifier and algorithm metadata | User 1 to 0..1 credential; restricted deletion | Keep concrete current model; UserCredential is a domain concept, not a required extra table |
| AuthSession / Identity | Independent root; Implemented | SessionId PK, UserId, verifier, issued/deadline/revoked instants | User 1 to 0..N sessions; same-row deadline/length checks | Keep current model; elevation evidence is proposed and B8-dependent |
| IdentityChallenge / Identity | Conditional root | ChallengeId, UserId or intended address binding, purpose, verifier, expiry, consumed/attempt state | Purpose-bound single-use proof; uniqueness selected for chosen proof flow | Needed only after B5/B10 chooses verification/recovery mechanism |
| Authenticator / external identity link / Identity | Conditional credential/factor root | FactorId or issuer+subject binding; assurance/enrollment/revocation metadata | User 1 to many if selected; unique issuer+subject for federation | No speculative generic credential table; B8/B10 |
| Organization / Organization | Root | OrganizationId PK, name, lifecycle, policy/security version | Local memberships/roles/invitations; remote Projects by scalar OrganizationId | Outside narrow Identity ERD; B1 |
| OrganizationMembership / Organization | Root | MembershipId PK; unique OrganizationId+UserId; state/version/times | Local Organization FK; scalar UserId; candidate key OrganizationId+MembershipId for scoped references | Durable membership default; episodes alternative changes references, B6 |
| OrganizationRole / Organization | Root | RoleId PK; scope+normalized name/code unique; origin/template version; protected designation; permission-set version | Candidate key OrganizationId+RoleId; local Organization FK | Separate system-derived scoped roles/custom roles; B4 |
| OrganizationRolePermission / Organization | Role child | Unique RoleId+PermissionCode | Local Role FK; catalog code validated, no external Permission FK | Relationship row if roles are persisted |
| OrganizationRoleAssignment / Organization | Membership child | Durable unique MembershipId+RoleId; OrganizationId; grantor/validity/revocation/version | Composite local FKs to scoped membership and role prevent cross-org assignment | Separate from project/platform assignments; B3/B4/B6 |
| OrganizationInvitation / Organization | Root | InvitationId PK; recipient normalization; verifier; status/expiry/version; sponsor/acceptor; intent | Local Organization; one pending scope+recipient offer proposed; no User FK across modules | Acceptance transaction owns membership and grants; B5 |
| OrganizationInvitationRoleGrant / Organization | Invitation child | InviteId+RoleId unique; offered role/permission version | Local scoped invitation/role reference | Only if invitation offers multiple roles; no extra general grant engine |
| Project / Project | Root | ProjectId PK; OrganizationId; name/purpose; lifecycle/delivery mode; security version | Scalar OrganizationId; local unique OrganizationId+ProjectId if children carry both | Exactly one owning workspace, no co-ownership/transfer default |
| ProjectMembership / Project | Root | MembershipId PK; unique ProjectId+UserId; participation kind/state/version; eligibility provenance | Local Project FK; scalar UserId and conditional OrganizationMembershipId; candidate key ProjectId+MembershipId | Internal eligibility is application contract; guests do not require org membership; B2/B6 |
| ProjectRole / Project | Root | RoleId PK; ProjectId+normalized name/code unique; origin/template version/protected flag | Candidate key ProjectId+RoleId | Project templates independent from org roles; B4 |
| ProjectRolePermission / Project | Role child | Unique RoleId+PermissionCode | Local Role FK, registered scope-compatible code | No shared mutable role-permission table across contexts |
| ProjectRoleAssignment / Project | Membership child | Durable unique MembershipId+RoleId; ProjectId; grant provenance/validity | Composite local references to membership and role in same project | Multiple roles supported as proposal; no cross-scope inheritance |
| ProjectInvitation / Project | Root | InvitationId PK; ProjectId; recipient; Internal/External intent; verifier/status/expiry/version | Local Project; role-intent children if needed; no implicit org membership creation | Separate owner/transaction from org invitation; B2/B5 |
| ProjectInvitationRoleGrant / Project | Invitation child | InviteId+RoleId; offered version | Same-project local references | Optional relationship, not shared polymorphic invitation table |
| PlatformRoleAssignment / Platform Access | Root with shared admin guard | AssignmentId PK; UserId+system role uniqueness policy; status/validity; grantor/approver/evidence/version | Scalar UserId; no org/project membership FK; system-defined platform role code | Placement B1; security/recovery B8; no SystemAdmin User table |
| Permission / defining capability | Catalog definition/value | Unique stable namespaced code; allowed scope, semantics, delegability/sensitivity | Referenced by codes; optional local lookup projection | Does not necessarily need a database table |
| ProjectTitle / Project | Membership value | Proposed descriptive text; optional effective dates | Scoped to participation; no global uniqueness, no access effect | No separate catalog initially; multiple values I3 |
| ProjectResponsibility / Project | Membership value collection | Descriptive duty/category; optional attribution | Resource-specific work/reviewer duties stay with resource owner | Rich relationship entity only when validated, I3 |
| ResourceShareGrant / resource owner | Root or resource child | GrantId; exact target/revision/publication; recipient; action set; validity/provenance | Local resource/version integrity; scalar project/User references | Required concept for selected guest access; physical representation B2 |
| ReviewerAssignment / Approval/resource owner | Review child | AssignmentId; exact target/reviewer/policy; required/optional state | Separate share, assignment and decision; User reference scalar | Dependency only; outside Identity ERD |
| AuditRecord / producing context | Append-only record | EventId; actor/scope/action/target/version/outcome/time/correlation/reason | Cross-owner IDs scalar; projection unique source+event | Local audit durability; retention/visibility B9/I5 |
| MembershipHistory / membership owner | Record or audit projection | EventId or MembershipId+sequence; prior/new state/version, actor/reason | Local relationship to durable membership | Separate table only if audit cannot serve required history |
| SupportAccessGrant / future Platform Administration | Deferred root | GrantId; operator; exact target/actions; justification/approval; validity/revocation | Target owner evaluates; no implicit rights from platform assignment | F1 blocks enabling support; not a mandatory Identity table |

One User can have zero organization memberships yet be a platform administrator or restricted client participant. One Organization can own many Projects; Project has exactly one owning Organization. Each membership can hold zero/many roles. RolePermission supplies many-to-many role/permission association; titles do not participate in permission resolution. A pending invitation is not a membership. Several historical invitations can concern the same durable membership.

## Integrity classification

Database integrity, domain invariants and authorization policy are complementary. A valid FK says that a row exists; it does not say the caller may use it.

| Invariant | Database-enforced candidate | Domain/application-enforced | Security authorization policy |
|---|---|---|---|
| Stable membership uniqueness | Unique scope+UserId across lifecycle | Explicit rejoin transition; history preserved | Reactivation requires current authority; stale invite insufficient |
| Same-scope role assignment | Local composite FK scope+membership and scope+role; NOT NULL | Correct role origin/version and allowed lifecycle | Caller can delegate all assigned permissions in this scope |
| Correct project tenant | Local Project ownership key and matching child FKs if tenant duplicated | Foreign organization existence/status via contract | Actual resource ownership matches current authorized request |
| External identity integrity | No cross-owner FK | Validate stable UserId through contract; reconcile deactivation/deletion | Session active; cannot substitute another person's identity |
| Protected Owner/Admin | Supported row values, versions | Serialize shared guard; count usable holders, handle recovery/expiry | Transfer/removal/provisioning require specific powers and evidence |
| Invitation uniqueness | Persisted Pending-state partial unique candidate; verifier uniqueness | Expire stale pending rows before replacement; prove recipient, consume once | Authorized inviter/delegator and intended authenticated recipient |
| Time validity | Same-row expiry-after-issue and coherent revocation timestamps | Evaluate current instant; legal transitions | Expired grants/privilege deny even before cleanup |
| Multiple roles | Unique membership+role; unique role+permission | Union compatible current allows; no conflicting scope | Lifecycle/visibility/independent-review restrictions override allows |
| Share/reviewer distinction | Local target/version references and unique assignment semantics | Required review gates and immutable revision rules | Read access alone never grants decision authority |
| Audit and historical attribution | Unique event identity, restricted local deletes and append-only storage permissions where selected | Record action atomically; retain/purge under reviewed policy | Limit tenant/platform audit viewers; no tokens/verifiers/private payloads |
| Platform separation | Separate assignment model; no tenant ownership prerequisite | Only registered system-controlled role codes | Customer cannot assign/edit platform privilege; no tenant bypass |

PostgreSQL CHECK constraints cannot safely encode general cross-row last-owner counts; nullable uniqueness and FK target uniqueness need deliberate design. Composite local keys are therefore candidates, while cross-row policy uses transactions. See [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html).

## Candidate indexes and uniqueness decisions

These are logical access paths to validate with expected workload, not a finalized index list or SQL migration.

| Area | Candidate index / constraint | Reason and unresolved choice |
|---|---|---|
| User | Existing case-insensitive email expression unique index | Preserve existing behavior; B9 decides tombstones/email reuse, B5 address proof normalization |
| Membership | Unique scope+UserId; lookup UserId+Status; scope+Status | Support context selection and authorized membership lists; durable pair avoids multiple historical rows becoming active |
| Scoped role | Unique scope+normalized role name/code; scope+RoleId candidate key | Allow same name in different projects/orgs, enforce local scoped references |
| Role assignment | Unique durable MembershipId+RoleId; current assignments by MembershipId | Explicit history records repeated grants; alternative current-only row uniqueness needs episode policy B6 |
| RolePermission | Unique RoleId+PermissionCode | No duplicated grants; lookup reverse permission-to-role only if needed for impact analysis |
| Invitation | Unique protected verifier; one Pending scope+normalized recipient; expiry cleanup path | Exact purpose/participation-kind dimension must be decided; recommend one offer per scope/recipient to avoid hidden stacked offers |
| Platform assignment | UserId+role unique policy, current expiry lookup | Durable pair versus historical assignment episodes must be chosen; grant/admin lookup independent of tenant |
| Resource grant | Recipient+target/purpose uniqueness; target and recipient lookup | Exact version/publication and action overlap semantics decided by resource owner |
| Audit/history | Unique event ID; scope+OccurredAt, actor+OccurredAt; membership+sequence where needed | Tenant-filtered review and traceability; retention/volume determines partitioning later |
| Sessions | Preserve current User lookup and verifier-related checks | Expired cleanup/elevated-session lookup requires chosen auth policy; no speculative schema rewrite |

Do not use `ExpiresAt > now()` as a partial-index predicate. Index expressions/predicates require immutable functions, while time passes without a row update. Use persisted Pending/current state for uniqueness and enforce expiry during the transactional use case; periodic cleanup is secondary. See [PostgreSQL CREATE INDEX](https://www.postgresql.org/docs/current/sql-createindex.html).

Separate assignment tables avoid nullable polymorphic `ScopeId`/`ScopeType` combinations and make same-scope local FKs straightforward. A shared role-assignment read projection is acceptable; it must not become an independent source of authority. If nullable keys or scoped global templates are later selected, explicitly settle NULL uniqueness and referential semantics before physical design.

## Concurrency and transaction design

| Race | Proposed consistency mechanism | Expected outcome |
|---|---|---|
| Duplicate registration | Preserve existing normalized-email/advisory-lock/index behavior | One identity per existing email policy; do not redesign via documentation |
| Two invitations issued to same recipient/scope | Scope/recipient serialization plus pending uniqueness and idempotency fingerprint | One pending offer or explicit conflict/reissue |
| Accept versus cancel/resend/expiry | Lock or conditional Invitation status+version transition; membership/grants in same owner transaction | One winner; failed redemption creates no partial grants |
| Repeated acceptance | Idempotency record plus accepted subject/outcome | Same subject receives consistent prior result; other subject never receives access |
| Concurrent membership edits | Membership version and conditional write | Stale writer re-reads and re-authorizes; no silent overwrite |
| Two different Owners removed | Common Organization guard lock/version or serializable transaction | Last-owner invariant protected across rows; per-member versions alone insufficient |
| Last platform administrator changes | Platform guard plus usable-account/expiry evaluation and recovery procedure | Normal orphaning prevented; compromise containment still allowed |
| Role permission edit while grant/invite accepts | Role version check, delegation re-evaluation, appropriate local serialization | No unreviewed expansion from stale intent |
| Organization membership disabled while project action runs | Authoritative eligibility contract; explicit admitted-request policy | New decisions deny; cross-database check alone does not guarantee commit ordering |
| Project/share revoked while job queued | Reauthorize at execution and materialization/delivery; versioned invalidation | No stale permissive job context; bounded download/stream policy needed |

Serializable isolation is an alternative for selected local invariants, but conflicts can require retrying the whole transaction. Retry must re-read and re-authorize using stable operation identity, without repeating external side effects. See [PostgreSQL transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html).

Creation transactions are deliberately bounded: Organization+initial membership+Owner; Project+initial participation+Project Admin; invitation consumption+local membership+role grants; role/privilege update+local audit+outbox. Do not pretend these transactions include Identity, Notification or independently owned resource databases. A transaction can coordinate several local aggregate roots when protecting a shared invariant without turning every member into one giant aggregate.

Revocation baseline proposal: authoritative current reads, no positive grant cache initially, fail closed when required facts cannot be established. Events carry source sequence/security versions for deduplicated propagation and cleanup. They cannot alone establish immediate revocation. B7 must decide admitted requests, sensitive commit fencing, cached reads, active streams, export URLs and external notifications before claiming a measurable guarantee.

## Cross-module reference policy

| Reference | Representation | Validation and lifecycle handling |
|---|---|---|
| Membership → Identity User | Scalar UserId | Authenticated Identity contract, minimal profile projection if justified; preserve pseudonymous attribution after deactivation |
| Project → Organization | Scalar OrganizationId | Authoritative ownership/status contract; no implicit cross-module cascade or tenant transfer |
| Internal project participant → OrganizationMembership | Optional scalar provenance plus Internal classification | Active membership in owning organization required at access evaluation; external branch independently authorized |
| Delivery resource → Project/Organization | Scalar owner IDs, authoritative owner relationship | Validate creation/reads/links through contracts; duplicate tenant IDs must be consistency-checked, never caller-trusted |
| Platform assignment → User | Scalar UserId | Account and elevated-assurance validation; no membership prerequisite |
| Share/reviewer assignment → User/participation | Scalar reference, local exact resource/version reference | Check recipient eligibility and current resource policy; preserve historical decisions |
| Notification → Invitation | Opaque delivery/correlation ID and protected required delivery material | Notification reports delivery outcome; cannot consume proof, mutate membership or mint authority |
| Audit projection → producing context | Source+EventId and scoped scalar references | Deduplicate/reconcile; source retains authoritative local event and privacy policy |

Only local owner relationships may use database FKs. Even if modules later share a PostgreSQL database, shared hosting is not permission to introduce cross-module FKs. No direct cross-module Domain/Infrastructure/DbContext references; use Contracts or Application-owned interfaces according to [repository guidance](../../AGENTS.md). If physical co-location is selected, review module ownership and transaction contracts explicitly instead of assuming a distributed workflow became atomic.

## Security invariants required before schema approval

- Platform, organization and project scopes remain separate; no platform wildcard tenant grant, role copying, or org membership dependency for administrators.
- System-controlled platform roles cannot be edited/assigned by customer administrators. Holding a permission is distinct from being authorized to delegate it.
- Common User is the default identity recommendation; privileged assignments and expiring elevated-session evidence are separate. A dedicated admin realm remains a risk-based alternative, not a required second user table.
- Internal participation depends on active membership in the owning organization; external participation needs explicit admission/share. Removal cannot silently convert internal access to guest access or restore previous grants on rejoin.
- Role union is within one exact scope. Suspension, expiry, publication, assigned reviewer duties, independence rules and business gates override positive grants.
- Invitation acceptance requires subject/recipient proof; existing active registration is not email verification. Store only protected proof, bind scope/purpose, rotate on resend and consume once.
- Context selection, titles, responsibilities, mentions, email domains, links and notification receipt grant no authority.
- Resource owners enforce actual ownership and visibility across APIs, jobs, search, exports, AI and attachments. Tenant filtering alone does not implement permission policy.
- Preserve audit actor/version evidence without retaining secrets. Administrative mutations need durable audit before becoming effective; runtime bootstrap/recovery controls must exist before enabling platform authority.
- Support access is separately justified, approved, time-limited and audited; ordinary System Administrator assignment cannot read customer content or make customer delivery decisions.

The main report §§9, 15–17 contains resolution, administrator bootstrap/recovery, action matrix and complete workflow contracts. Optional PostgreSQL RLS would be additional protection only; see the main report's [RLS source](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) and privilege considerations.

## Deletion, retention and history

| Data | Proposed default | Decision needed |
|---|---|---|
| User/profile/address | Deactivate without removing attribution ID; separate personal-data erasure from identity continuity | Email reuse, alias history, display snapshots, tombstone privacy and legal holds, B9 |
| Credential/challenge/session | Revoke when applicable; purge secret/verifier material on reviewed expiry/retention policy | Recovery evidence versus minimization; admin elevation persistence, B8/B9/B10 |
| Membership/role assignment | Durable relationship, state changes and explicit history; no business evidence cascade | Rejoin/episodes and prior-role restoration rules, B6 |
| Organization/project | Archive/suspend distinct from deletion; block future access according to policy | Allowed read/export/unarchive, closure and deletion orchestration, I1/B9 |
| Invitations | Retain outcome/provenance minimally; remove obsolete proof and unnecessary recipient PII | Exact retention and replay/idempotency window, B5/B9 |
| Audit/membership history | Append-only authoritative events; protected visibility; optional consolidated read model | Retention periods, immutable storage needs, erasure handling, source-of-truth history, B9/I5 |
| Resource shares/reviews | Revoke future access; retain version-bound prior decision attribution | Whether revoked reviewer evidence remains sufficient for future acceptance is resource policy, I4 |
| Reliability/outbox/idempotency | Separate operational retention from business history | Replay/deduplication window and delivery reconciliation, I5 |

Do not apply blanket soft deletion or indefinite retention to every concept. Do not choose cascade deletion merely to make an ERD tidy. No legal retention duration, data-residency rule or compliance claim is invented by this document.

## Outstanding design blockers and acceptance checklist

| Review gate | Status | Resolution needed |
|---|---|---|
| B1 ownership/placement | Open, blocks physical ownership/FK design | Accepted recommendation or selected alternative, including Platform Access packaging and host strategy |
| B2 guest participation | Open, blocks collaboration relationship model | Internal/external eligibility, resource-only envelope, overlapping grants and conversion behavior |
| B3 owner/admin continuity | Open, blocks protected-role governance | Owner cardinality, transfer/acceptance, shared guard, last usable admin and emergency recovery |
| B4 roles/delegation | Open, blocks assignment/candidate-key finalization | Scoped definitions, template instantiation, role union and permission-change semantics |
| B5 invitations | Open, blocks proof/recipient/uniqueness details | Verified recipient mechanism, address changes, pending uniqueness, expiry/resend/idempotency and grant drift |
| B6 membership permanence | Open, blocks history/reference semantics | Durable relationship versus episodes and fresh grants on restoration |
| B7 revocation | Open, blocks security consistency contract | Authoritative reads, in-flight boundary, cross-host failure and any cache/fence protocol |
| B8 admin identity/assurance | Open for privileged foundation | Bootstrap, grant approval, MFA/elevation, expiry, revocation and recovery procedures |
| B9 retention/erasure | Open before final physical design | PII/attribution, email reuse, secret/audit retention, deletion and legal holds |
| B10 auth extension scope | Open for changed Identity auth schema | Which proof/factor/recovery concepts are actually required; preserve existing contracts |
| I1–I6 operational/detail refinements | Important | Archive policy, project-admin continuity, titles/duties, exact catalog, audit implementation and measurable NFRs |
| F1 support access | Blocks future support capability only | Consent/emergency and precise action scope; do not enable by default |
| O1–O3 optional scope | Deferred | SSO/SCIM unless needed, advanced policy, billing/entitlements, service extraction and broader delivery modes |

Before final ERD design, reviewers should walk through: multi-workspace/multi-project roles; a resource-only client; two concurrent owner removals; invitation cancellation versus acceptance; role expansion during pending invitation; internal membership removal versus guest conversion; deactivated user with historical approval; suspended organization with active client share; System Admin denied customer UR; last administrator compromise; and a revoked grant while a job/export is queued. Expected results are in the main report §§14–17. These scenarios are proposed validation inputs, not executed tests.

Once decisions are accepted: reconcile affected canonical requirements without changing historical approval evidence; establish selected logical ownership and cardinalities; review the actual deployed PostgreSQL version and existing migrations; choose physical keys/types/indexes and retention; then derive reviewed implementation tasks. Structural document validation cannot establish business approval or security correctness.

## Verification record

This is a documentation-only change. The primary workflow was read completely; current architecture, source, project/deployment configuration and relevant feature evidence were inspected. No .NET tests, runtime integration tests, migration execution or live-database inspection were performed. Application architecture references were not changed, so the architecture-boundary script was not required.

The repository documentation validator was run before writing these files and reported two existing unresolved-template-placeholder errors in non-draft artifacts: [FR-DP-002-003](../features/FEAT-DP-002-user-initialization/fr/FR-DP-002-003.md) and [TASK-DP-002-004](../features/FEAT-DP-002-user-initialization/tasks/TASK-DP-002-004.md). These unrelated artifacts are preserved. The final validator run returned the same two errors across 37 canonical artifacts, with no additional errors. A separate scoped check passed for local Markdown link targets, balanced code fences, all 20 required discovery sections, and UTF-8/CRLF/final-newline formatting in both new documents. Mermaid source was reviewed but not rendered; no diagram-rendering result is claimed. A failed repository-wide validator is not represented as a pass.
