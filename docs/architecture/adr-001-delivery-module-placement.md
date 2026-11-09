# ADR-001: Placement of the MVP delivery capabilities

> **Status: accepted** — 2026-11-09. Proposed earlier the same day; decisions D-01…D-10 below were selected by the user in this conversation (decision reference: user's answers to ADR-001 Q1–Q8 and follow-ups, 2026-11-09). "ADR-001" and "D-xx" are local architecture-record labels, not canonical requirement artifact IDs.
>
> Acceptance authorizes **planning** feature contracts and tasks against this placement. It does not by itself create projects, databases, routes or migrations, and it changes no feature, requirement or task approval status. Each implementation step still needs a reviewed task.

| Field | Value |
|---|---|
| Decision owner | User (product owner / architect) |
| Decided placement | **Projects module:** FEAT-DP-025, 004, 008, 009, 011, 006, 007, 017, 019, plus project/guest access used by 015. **Risk module:** FEAT-DP-001, 010, 027 |
| Roadmap slices unblocked | [VS-02, VS-03, VS-04, VS-06, VS-07](../roadmap.md#3-mvp-vertical-slices) in Projects; VS-05 and VS-08 need the Risk module as well |
| Resolves | Outstanding decisions 7 and 8 (placement part) in the [target architecture proposal](../architecture.md#12-outstanding-architecture-decisions); the "new business modules require an explicit architecture decision" rule in [ARCHITECTURE.md](../../ARCHITECTURE.md) |
| Does not decide | Host consolidation into a modular monolith; AI insights (028) hosting; Client Portal (015) frontend shape; exact endpoints, tables, ports and message schemas (task-level) |

## 1. Context

**Implemented today** (source-verified in [ARCHITECTURE.md](../../ARCHITECTURE.md), 2026-11-05): independently hosted Identity (authentication + Organization Management, FEAT-DP-002/003) and Notification modules, each with API / Application / Domain / Infrastructure / Worker / MigrationRunner projects and its own PostgreSQL database, plus a YARP Gateway publishing an explicit route allowlist ([`proxy.json`](../../src/Gateway/DeliveryPilot.Gateway/proxy.json)). Cross-module integration uses `src/Contracts/` and RabbitMQ through the BuildingBlocks outbox/inbox. No Engagement, Project, Backlog, Sprint, Capacity, Requirements or Risk code exists.

**Product direction** ([D1–D8](../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions)): post-contract, fixed-price software outsourcing, delivered only with Agile Scrum.

**Constraints already recorded:**

- Identity keeps project participation, project roles/invitations, requirements and approvals **outside** Identity ([SCOPE-04](modules/identity/design-decisions.md#selected-direction)).
- The [target architecture](../architecture.md) modular-monolith option is a proposal and must not override the current independent hosts.
- Dependency direction, CQRS pipeline, transaction ownership, UTC instants and naming rules from [AGENTS.md](../../AGENTS.md) and [module guidance](../../src/Modules/AGENTS.md).
- BR-003: organization membership never grants project access; restricted guests may get project access without membership. Glossary: a Client Organization "does not implicitly gain ownership or access" ([requirements](../requirements.md#2-domain-glossary)).

**Source facts checked for this decision (2026-11-09):**

| Fact | Evidence |
|---|---|
| Access tokens are HS256 (symmetric) with issuer/audience pinning and zero clock skew | `AuthExtensions.AcceptedSigningAlgorithm`, `JwtAccessTokenIssuer.SigningAlgorithm` |
| Access-token lifetime is 10 minutes; tokens carry only `sub`, `sid`, `jti` | Identity `appsettings.json` (`AccessTokenExpirationMinutes`), ARCHITECTURE.md |
| Workspace permissions are code-controlled `organization.*` codes evaluated per request | `OrganizationCapabilityCatalog`, `OrganizationAuthorizationService` |
| Internal service-to-service HTTP has existing building blocks | `IntegrationApiController`, `IntegrationApiKeyFilter`, `IntegrationClientOptions` in `DeliveryPilot.AspNetCore` |
| "Delivery" already names email delivery | Notification `EmailDelivery*`, Identity `OrganizationInvitationDelivery`, `deploy/delivery/alerts.yaml` |

**Coupling between the capabilities.** Engagement → Project → Backlog item → Sprint selection, with capacity feeding planning and version-bound sign-off binding to exact backlog/requirement revisions, are invariants that should commit in local transactions. Risk detection, by contrast, reads those facts after the fact and benefits from isolation (AI later), so it tolerates eventual consistency.

## 2. Decision drivers

1. Correctness of tight delivery invariants (Sprint selection, carry-over, velocity, version-bound sign-off) with local transactions.
2. Tenant isolation and scoped guest access, enforced in the owning module.
3. Preserve current topology and conventions; no silent move toward a monolith or microservice sprawl.
4. Low operational cost for a small team.
5. Isolate risk intelligence (and later AI) from transactional delivery paths.

## 3. Options considered

| Option | Transactions across 025/004/008/009/011 | New module hosts / DBs | Verdict |
|---|---|---|---|
| A. One module for all six original capabilities | Local | 1 module / 1 DB | Basis of the decision; risk part split out (D-05) |
| B. One module per capability | Distributed / eventual | ~5 modules / 5 DBs | Rejected for MVP: atomic Sprint selection and sign-off would cross databases; 5× deploy and auth surface |
| C. Project inside Identity | Local with Identity | 0 | Rejected: conflicts with SCOPE-04 and loads Identity's security guard with delivery writes |
| Modular monolith consolidation | n/a | n/a | Not evaluated; separate decision. Both chosen modules remain compatible with it |

## 4. Decision

### D-01 — Projects module

Create one new independently hosted module **`DeliveryPilot.Projects`** mirroring `src/Modules/DeliveryPilot.Identity`:

```text
src/Modules/DeliveryPilot.Projects/
  DeliveryPilot.Projects.API              HTTP transport, auth adapter, composition
  DeliveryPilot.Projects.Application      Features/<Context>/{Commands,Queries}/<UseCase>/
  DeliveryPilot.Projects.Domain           aggregates per internal context
  DeliveryPilot.Projects.Infrastructure   ProjectsDbContext, stores, Identity client adapter
  DeliveryPilot.Projects.Worker           outbox relay, consumers
  DeliveryPilot.Projects.MigrationRunner  applies ProjectsDbContext migrations
src/Contracts/DeliveryPilot.Projects.Contracts/   versioned messages and internal read DTOs
```

Own PostgreSQL database `DeliveryPilotProjects`; Gateway cluster `projects` with an explicit allowlist under `/projects/api/v1/…`. The name avoids the existing "delivery" vocabulary (Q1).

Internal bounded contexts (separate Application feature areas, Domain aggregates and DB schemas):

| Context / schema | Features | Core aggregates (indicative; designed per feature) |
|---|---|---|
| `engagement` | 025 | Client details, optional client organization link (D-09), Engagement, fixed-price agreement reference, planned budget/effort, milestones, client PO assignment |
| `project` | 004, 015 access | Project, Product Goal/brief, stakeholders, project participation and roles, guest admission/sharing grants |
| `backlog` | 008 | Backlog item (epic/feature/story/bug/spike/technical), ordering, acceptance criteria, DoR, work-item blocked state |
| `sprint` | 009, 011 | Sprint, Sprint Goal, original timebox/selection, execution events, carry-over; capacity, availability, estimation method, velocity history |
| `approval` | 006, 007, 017, 019 | Requirement and revisions, traceability links, sign-off requests/decisions, scope baselines, change requests (D-04) |

FEAT-DP-026 (Sprint Review/Retrospective records) naturally belongs to the `sprint` context; confirm when its feature contract is created.

### D-02 — Authentication on the Projects API: shared HS256 key plus Identity check (Q2)

- The Projects API validates bearer tokens locally with the same configuration Identity uses (`AddConfiguredAuthentication`: HS256 only, pinned issuer and audience, zero skew).
- A valid signature is **never** authorization. Each request then obtains session validity and organization facts from Identity (D-03, D-04). Token claims other than `sub`/`sid` are ignored.
- **Safeguards (mandatory):** the key is mounted as a secret only into the Projects API container (never Worker, MigrationRunner, Gateway or logs); Projects must not reference or contain token-issuing code, enforced by `scripts/Test-Architecture.ps1`; one rotation procedure covers Identity and Projects together.
- **Known trade-off:** with a symmetric key, Projects could technically mint tokens, so a Projects compromise is equivalent to an Identity signing-key compromise. Accepted for one additional host.
- **Gate:** before a **third** host (the Risk module, D-05) needs bearer validation, decide asymmetric signing (RS256/ES256 with a published key set) or an Identity introspection endpoint. The shared symmetric key must not be copied to more hosts without that decision.

### D-03 — Permission ownership split by scope (Q3)

- **Workspace-level entry points** (indicative: create engagement, read all engagements, create project) are registered as new codes in Identity's code-controlled `OrganizationCapabilityCatalog` and granted through existing Owner/Admin/custom roles. Exact codes are a FEAT-DP-025/004 + Identity task decision.
- **Everything inside a project** (backlog edits, Sprint planning, sign-off authority, guest admission, sharing) is owned by Projects' project roles and grants. Identity knows nothing about projects (SCOPE-04).
- Identity exposes an **internal, non-Gateway** integration query (existing `IntegrationApiController` pattern, integration API key): `(userId, sessionId, organizationId)` → session valid?, membership state, organization lifecycle, effective organization permission codes, authority version. It fails closed and does not let callers enumerate memberships. This is the first contract task to plan.

### D-04 — Freshness of Identity facts (Q4)

- **Commands:** always call Identity, uncached, and recheck authority inside the pipeline transaction before commit (mirrors Identity ACCESS-02).
- **Queries:** may cache the D-03 result per `(sessionId, organizationId)` for at most **30 seconds**, evicted early by Identity access-change events (membership removed/suspended, role revoked, session revoked, organization lifecycle).
- Identity unavailable → requests requiring its facts fail closed (`ServiceUnavailableException`), never default-allow.
- Project-level authority is local to Projects and never cached from elsewhere.

### D-05 — Separate Risk module for signals, register and health (Q5 + follow-up)

- A second new module, working name **`DeliveryPilot.Risk`** (Gateway prefix `/risk`), owns FEAT-DP-001 signal definitions/evaluations/candidate findings, FEAT-DP-010 risk register (manual and confirmed risks, issues, blockers, exposure, owners, responses) and FEAT-DP-027 health read models. The name may be refined in its design workspace before scaffolding without a new ADR.
- Confirm-finding → risk stays local to the Risk module.
- **Blocked state of a board item stays in Projects** (`backlog`/`sprint`) as delivery state. The register references items, Sprints, engagements and projects by **scalar ID** and may hold a blocker record linked to a blocked item.
- Risk builds its inputs from **Projects outbox events** into its own database and, where needed, from Projects internal read contracts. It never reads `ProjectsDbContext`.
- Risk API authorization needs Identity facts (as D-03/D-04) **and** project-level facts from Projects: Projects exposes an internal project-access query `(userId, organizationId, projectId)` → participation, project permissions, guest sharing scope. Client-shared health (BR-055) is filtered by these facts.
- Sequencing: Projects is scaffolded first (VS-02…VS-04, VS-06, VS-07). The Risk module is scaffolded before VS-05/VS-08 work and after the D-02 gate decision.

### D-06 — Dependency direction

Unchanged from [AGENTS.md](../../AGENTS.md), applied to each new module `<M>` ∈ {Projects, Risk}:

```text
<M>.API             -> <M>.Application + <M>.Infrastructure + Contracts + BuildingBlocks
<M>.Worker          -> <M>.Application + <M>.Infrastructure + Contracts + BuildingBlocks
<M>.MigrationRunner -> <M>.Infrastructure + BuildingBlocks
<M>.Infrastructure  -> <M>.Application + <M>.Domain + Contracts + BuildingBlocks
<M>.Application     -> <M>.Domain + Contracts + BuildingBlocks
<M>.Domain          -> BuildingBlocks only
DeliveryPilot.<M>.Contracts -> BuildingBlocks only when needed
```

- Projects may reference `DeliveryPilot.Identity.Contracts` (and Notification contracts if it sends email). Risk may reference Identity and Projects Contracts. No module references another module's API, Application, Domain, Infrastructure, Worker or DbContext.
- Identity does not reference Projects or Risk. Projects does not reference Risk; if Projects ever needs risk facts, Risk publishes contracts.
- Internal contexts within Projects may reference each other through Domain types or Application abstractions; keep each context's Application code in its own `Features/<Context>/` area.
- Controllers send commands/queries via `ISender`; pipeline order and transaction ownership stay as today. Extend `scripts/Test-Architecture.ps1` with rules for both modules.

### D-07 — Tenant isolation

- Every Projects aggregate root carries `OrganizationId` (the single owning vendor workspace, ARCH-003). Child records inherit it through their root or carry it directly where queried independently.
- Every store query filters by `OrganizationId` and, for project-scoped data, `ProjectId`, after authorization. Route IDs never imply tenant.
- Client PO/stakeholder access is a Projects-owned project guest grant (BR-003). It never requires or implies membership in the vendor workspace, and a client organization link (D-09) grants nothing by itself.
- Workers and consumers run with the tenant and actor from the message and never fall back to an unrestricted context. The Risk module applies the same rules to its own data.
- PostgreSQL-backed tenant-isolation tests are required for each module, as Identity has.

### D-08 — Database layout (Q6)

- One PostgreSQL database and one EF `DbContext` per module.
- Projects: schemas `engagement`, `project`, `backlog`, `sprint`, `approval` plus `reliability` for outbox/inbox. Plural PascalCase tables, PascalCase columns, quoted identifiers, UTC `DateTimeOffset`.
- Local FKs are allowed inside the module, including across its schemas (e.g. Project → Engagement). References to Identity or Risk are scalar IDs only, with no cross-database FKs.
- No Row-Level Security in MVP; application-level filtering plus isolation tests. RLS remains optional defense-in-depth later.

### D-09 — Client representation (Q8 + follow-up)

- An Engagement stores the client's business details locally and an **optional `ClientOrganizationId`** (scalar Identity organization ID).
- The link is set only when that client organization already exists in Identity, created and owned by the client's own user under Identity's existing creator-is-Owner rule, and the link is confirmed by an authorized member of that client organization. The vendor never provisions or owns the client's Identity organization.
- The link is informational and enables future cross-workspace experiences. It grants **no** access to vendor projects, does not make the project co-owned (still out of MVP), and does not replace guest grants.
- The exact confirmation flow, and whether it needs a new Identity contract, is a FEAT-DP-025 refinement item.

### D-10 — Outbox and events

- Both modules use the existing BuildingBlocks outbox/inbox: business state and outbox records commit in the same pipeline transaction; each Worker relays to RabbitMQ and runs consumers with inbox deduplication.
- Versioned messages live in `DeliveryPilot.Projects.Contracts/<Area>/V1/` and are added when a consumer exists. Because D-05 makes the Risk module the first consumer, its MVP inputs define the initial catalog. Candidates: Sprint started/closed, Sprint scope changed, backlog item blocked/unblocked, item carried over, capacity declared/changed, engagement milestone changed, sign-off decided, change request decided.
- Events carry `OrganizationId`, `ProjectId`, aggregate version, occurred-at (UTC), actor and correlation IDs; consumers reject stale versions. Payloads exclude sensitive client content unless required, and never include secrets or tokens.
- Projects consumes Identity access-change events for D-04 cache eviction and cleanup. These events are not the only way revocation takes effect.
- Risk also runs scheduled sweeps for time-based conditions (blocked age, overdue sign-off) against its own projections.

## 5. Consequences

### Positive

- Delivery invariants and version-bound sign-off commit locally in one database.
- Identity stays focused on authentication and workspace authority.
- Risk/AI workloads cannot slow or break transactional delivery paths, and evolve independently.
- Both modules follow the proven Identity shape and remain compatible with a later consolidation decision.

### Negative / accepted risks

- Two more module host sets and databases to operate.
- Shared HS256 key widens the signing trust boundary to Projects (mitigated by D-02 safeguards and gate).
- Every Projects request depends on Identity availability (fail closed) and adds one internal call for commands.
- Risk signals and health are eventually consistent with Projects; event catalog design becomes MVP-critical.
- Projects is a large module; internal context boundaries must be enforced in review and architecture tests.

### Deployment impact

| Area | Projects (first) | Risk (before VS-05) |
|---|---|---|
| Solution | Register 6 projects + Contracts in `DeliveryPilot.slnx`; architecture-test rules | Same |
| `Dockerfile.modules` | csproj `COPY`/restore lines; `projects-api`, `projects-worker`, `projects-migration` stages | `risk-*` stages |
| `deploy/compose/` | `deploy/compose/projects/compose.yaml` + `compose.local.yaml` (PostgreSQL, migration job, API, worker) | `deploy/compose/risk/` |
| `deploy/setup/` | `db-bootstrap.sh` (database `DeliveryPilotProjects`, roles `projects_migrator`, `projects_api`, `projects_worker`), `db-grants.sh`, `broker-bootstrap.sh`, secrets staging including the shared JWT key for the API only and the Identity integration key | Same pattern; auth per D-02 gate |
| `deploy/env/` | Images, API/PostgreSQL ports, Identity integration endpoint in `local.env.example`, `server.env.example`, `verify.env` | Same |
| Scripts | `projects` in `-Module`/`--module` of `scripts/deliverypilot.ps1`/`.sh`; extend `Test-Deployment.ps1`, `Test-GatewayDeployment.ps1` | `risk` |
| Gateway | `projects` cluster and explicit allowlist in `proxy.json`; GUID-constrained IDs, timeout/body/no-store/CORS conventions; route tests | `risk` cluster |
| Identity | New `organization.*` delivery entry-point codes (D-03); internal authority query; access-change events | Same query reused |
| Observability | Outbox metrics/alerts for Projects. `deploy/delivery/alerts.yaml` stays the email/outbox delivery alert file | Same |
| Documentation | Update `ARCHITECTURE.md` and `src/Modules/AGENTS.md` **when code lands**; `docs/Deploy.md` service/port table, host-mode terminals, readiness checks; module design workspace | Same |

## 6. Follow-up work (planning order)

1. **Identity integration contract tasks** (D-02/D-03/D-04), planned 2026-11-09: [TASK-DP-003-019](../features/FEAT-DP-003-organization/tasks/TASK-DP-003-019.md) internal session/authority checks (done 2026-11-09), [TASK-DP-003-020](../features/FEAT-DP-003-organization/tasks/TASK-DP-003-020.md) workspace entry-point codes `organization.engagements.create`, `organization.engagements.read-all`, `organization.projects.create` (done 2026-11-09), [TASK-DP-003-021](../features/FEAT-DP-003-organization/tasks/TASK-DP-003-021.md) access-change events (blocked until a consumer exists).
2. **Projects module design workspace** under `docs/architecture/modules/projects/` (README, glossary, design decisions, ERD per context).
3. **Projects scaffolding + deployment task** covering the Projects column of §5. Code skeleton landed 2026-11-09 without a separate TASK file, at the user's choice: six module projects, `DeliveryPilot.Projects.Contracts`, `tests/DeliveryPilot.Projects.Tests`, the Identity session/authority adapter, a reliability-only initial migration, and a D-02 token-issuing guard in `scripts/Test-Architecture.ps1`. Deployment wiring is still pending.
4. **Feature contracts** in order: FEAT-DP-025 and 004 (VS-02, including the D-09 link flow), 008 + 006/007/017 (VS-03), 009/011 (VS-04), 019 (VS-06), 026 (VS-07).
5. **D-02 gate decision** (asymmetric signing or introspection), then **Risk module design workspace and scaffolding**, then FEAT-DP-001/010/027 contracts with the initial Projects event catalog (D-10).

Items deliberately left to those tasks (not open architecture questions): exact routes, permission code strings, table/column design, ports, message schemas, cache implementation, Risk module final name.

## References

- [Current backend architecture](../../ARCHITECTURE.md) and [module guidance](../../src/Modules/AGENTS.md)
- [Identity design decisions](modules/identity/design-decisions.md) (SCOPE-01, SCOPE-04, DATA-03, ACCESS-02, minimum external contracts)
- [Requirements](../requirements.md) (BR-003, BR-046, BR-055, glossary)
- [Target architecture proposal](../architecture.md) §4, §5, §9, §12 (proposal, not a decision)
- [Roadmap](../roadmap.md) §2–§5, especially [dependencies](../roadmap.md#5-dependencies-and-critical-sequencing)
- [Deploy guide](../Deploy.md)
