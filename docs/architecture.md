# DeliveryPilot - Architecture and Technical Decisions

> **Current product alignment — 2026-11-08:** D1–D6 establish post-contract fixed-price delivery across eight lifecycle stages. The capability map below is a target proposal; current independent hosts, Gateway, outbox and broker remain the implementation baseline. Topology-specific advice applies only if that alternative is explicitly selected. Source-backed [Identity implementation](architecture/modules/identity/README.md#verified-implementation-baseline) and [SDLC workflow](sdlc-workflow.md) take precedence over older inventory and lifecycle summaries.

> **Status:** Proposed target architecture, not an implemented system.  
> **Owner:** CTO / Solution Architect  
> **Related documents:** [Product](product.md) | [Requirements](requirements.md) | [Roadmap](roadmap.md)

> **Reconciliation — 2026-11-03:** Use the [architecture workspace](architecture/README.md) for selected module design and [current backend architecture](../ARCHITECTURE.md) for the existing independent hosts. The user selected one Identity module/database for Authentication, Organization Management and protected Platform Access. That ownership choice does not select this document's monolith deployment, remove Gateway/broker infrastructure, or settle other module placements. Workflow Q1–Q44/W1 and [Identity decisions](architecture/modules/identity/design-decisions.md) refine the earlier conceptual guidance below.

> **Vision repositioning — 2026-11-06:** The product now focuses on Scrum delivery for software outsourcing with AI-powered delivery risk management ([V1–V5](deliverypilot-workflow-discovery.md#196-vision-repositioning-decisions)). The capability map below adds Engagement & Client and promotes Delivery Risk Intelligence to a core context. This remains a proposal: no topology change, host consolidation or module placement is decided here.


## 1. Architectural drivers

DeliveryPilot must reconcile closely related workflows: client engagements, backlog and scope baselines, Scrum delivery, capacity/velocity, change control and continuous delivery-risk detection, with verification, release, warranty and closure traceability. Its initial engineering priorities are **correctness, tenant isolation, client-scoped visibility, auditability, explainable risk signals, changeability, and low operational complexity**, not service count.

Constraints from product direction:

- Multiple organizations, engagements and projects, with a single owning tenant per MVP project.
- Delivery-risk signals that are deterministic, explainable and independent of LLM availability.
- External client collaborators with strictly scoped access.
- Immutable references to approved versions and baseline history.
- Cross-domain traceability without forcing a single parent-child data hierarchy.
- Configurable approval and delivery policies.
- Event-driven notifications and delivery intelligence when useful.
- Uncertain early scale and still-evolving business requirements.

## 2. Recommended architecture

**Unselected topology alternative: Modular Monolith on ASP.NET Core with PostgreSQL.** The following diagram explores a single application boundary. It does not instruct a migration from the existing independently hosted components. A consolidation decision would require its own architecture review and migration plan; current Gateway and messaging remain in place.

```text
React + TypeScript web app / Client Portal
                 |
          HTTPS API edge
                 |
   ASP.NET Core Modular Monolith
   +-------------------------------------+
   | Identity & Organization             |
   | Engagement & Client                 |
   | Project Governance                  |
   | Requirements & Approval             |
   | Scrum Delivery (Backlog + Sprints)  |
   | Capacity & Velocity                 |
   | Delivery Risk Intelligence          |
   | Quality / Release                   |
   | Portfolio & Analytics               |
   +-------------------------------------+
       |             |             |
    PostgreSQL    Object Store   Background Jobs
       |                            |
    Outbox                     Optional Queue
       |
  Audit / Event records

External integrations / LLM provider(s): adapter boundaries only
```

**Why not microservices immediately?** Version approvals, requirement-to-story links, project permissions and planning interact frequently. Splitting them into network services early adds failure modes, distributed transactions, authorization complexity and deployment cost without validated demand. Modules can be extracted later when load, team ownership or independence justifies it.

## 3. Technology choices (recommendations, not validated inventory)

| Area | Preferred option | Decision rationale |
|---|---|---|
| Web | React + TypeScript + Vite | Familiar, modular UI stack; internal workspace and Client Portal |
| UI | Tailwind CSS + shadcn/ui | Consistent accessible component foundation |
| Server state | TanStack Query | API caching and invalidation patterns |
| Backend | ASP.NET Core / .NET | Aligns with planned backend development skills |
| Modularity | Modular Monolith + Clean Architecture | Clear business ownership without distributed deployment overhead |
| Persistence | PostgreSQL + EF Core | Strong relational modeling and transactional consistency |
| Object storage | S3-compatible or Azure Blob | Document attachments, exports and binary evidence |
| Cache | Redis only for identified needs | Avoid premature cache invalidation and consistency issues |
| Messaging | Transactional outbox; optional RabbitMQ/MassTransit | Reliable async side effects without mandating a broker at MVP start |
| Edge | Direct API first; YARP when justified | Avoid duplicate routing/auth layers before needed |
| Observability | OpenTelemetry + suitable backends | Trace API and background-job behavior |
| Deployment | Docker + CI/CD | Repeatable environment provisioning |
| AI | Provider-agnostic adapter with validated structured output | Swap providers, protect inputs, audit findings |

Do not treat framework/library versions as fixed until implementation planning validates current compatibility.

## 4. Bounded contexts and ownership

Each module has explicit application contracts and persistence ownership; cross-module reads should use public contracts or designed read models, not direct access to another module's internal entities.

| Context | Owns | Important outbound references |
|---|---|---|
| Identity (selected combined design) | Authentication, organizations, organization memberships/roles/invitations and protected platform assignments | Minimal identity/organization facts through contracts; project/resource authority stays with its owner |
| Engagement & Client | Client organization record, engagement, contract type, planned budget/effort, milestones, client PO assignment | Organization ID; projects by scalar ID |
| Project Governance | Project, Product Goal/brief, stakeholders, team participation, configuration | Organization, engagement and client references |
| Requirements Management | Requirement, revision, rules, traceability links | Project, review and backlog artifacts |
| Approval / Governance | Sign-off requests and decisions, scope baselines, change requests | Versioned artifacts and authorized approvers |
| Scrum Delivery | Product Backlog items, Sprints, Sprint Goals, original timebox/selection, actual execution events, carry-over and changes, board state, Sprint Review and Retrospective records | Requirements, projects, assignments |
| Capacity & Velocity | Availability, allocation, estimation method, declared capacity, velocity history, later forecasts | People, projects and Sprints |
| Delivery Risk Intelligence | Signal definitions/thresholds, signal evaluations, candidate findings, risk register (risks, issues, blockers, exposure, responses), health read models, AI explanations, evaluation and feedback | Authorized source references only, via contracts/events |
| Quality / Release | Tests, defects, UAT, deployment/build/environment evidence and release records (V1) | Work items, milestones, external test provenance |
| API collaboration | API contract revisions, environment availability and consumer integration evidence (proposed FEAT-DP-029) | Requirements/work optional; external specifications, builds and test runs |
| Warranty / Closure | Coverage obligations, hypercare, claims, handover recipients, residual ownership and closure evidence (proposed FEAT-DP-030) | Releases, accepted scope and commercial evidence references |
| Portfolio & Analytics | Aggregated read models and reporting definitions | Project-scoped aggregates |

Except for selected Identity ownership, this is a conceptual capability map, not accepted code-module placement. The exact number of code modules may be smaller; avoid forcing one deployable package per context. Project versus Requirements/Approval packaging remains unselected.

## 5. Persistence and transactional invariants

### Organization and tenant boundaries

- Each tenant-owned aggregate carries its effective tenant ownership, either directly or through an authoritative owner relationship.
- A valid login does **not** imply the user can access a given project.
- Resolve workspace and project/resource eligibility through their own authority. Restricted project guests need not have organization membership; authenticated context and request parameters alone are not access grants.
- Apply authorization to all API operations, query projections, attachments, search, exports, background jobs and AI retrieval.
- Consider database Row-Level Security as defense-in-depth if operationally maintainable; application authorization remains necessary.

### Requirement/approval consistency

- A requirement's stable ID differs from its revision ID.
- Approval decisions reference immutable artifact/version identifiers, with actor, policy and decision time.
- Updating an approved requirement creates a new proposal; never overwrite the approved revision content.
- Baselines should be reproducible snapshots of approved references and associated policy/context.
- Use transactional writes for tightly coupled state changes, including approval decisions and corresponding transition constraints.
- Use optimistic concurrency on multi-author revisions and mutable state to detect lost updates.

### Cross-context links

- Model many-to-many traceability through explicit relationship records with source/target types, IDs, project scope and relation semantics.
- Enforce valid ownership and authorization before creating links.
- Not every work relationship is blocking; cyclic blocking dependencies should be rejected.
- Preserve links to historical, superseded versions where needed for audit and release evidence.

### Database evolution

- The selected Identity design owns one PostgreSQL database, retaining `shared` for the proposed business tables. Organization-to-User references inside Identity may be local FKs; cross-module references remain scalar IDs through contracts.
- Separate schemas in a shared database remain an alternative for undecided contexts, not a replacement for selected Identity ownership or the current independent module databases.
- Make data migrations explicit and reversible where feasible; test migration and restore paths.
- Other contexts' database placement needs an explicit architecture decision; a conceptual context or roadmap feature does not automatically require its own database.

## 6. API and integration boundaries

- Browser interactions use project-scoped APIs with consistent authorization and error contracts.
- Expose typed IDs/references and pagination for large histories, work items, and audit trails.
- Use idempotency where client retries could create duplicate invitations, approvals or event handling.
- Within one host, use application-owned interfaces. Across independent hosts, use explicit Contracts and supported transport; do not infer in-process calls or bypass deployment/security boundaries from the monolith alternative.
- Integrations (GitHub/GitLab/Azure DevOps, communication tools, AI providers) use replaceable adapters, least-privilege credentials, retry/backoff and explicit failure policies.
- Carry correlation IDs, actor, tenant, and causation context through approved async boundaries; revalidate credentials/permissions as appropriate.

### Background work and events

Use the current outbox/broker/worker infrastructure where applicable. An in-process scheduler is an alternative for new local jobs, not a directive to remove existing messaging. For outbox events:

- Persist business state and outbox record in the same transaction.
- Consumers must handle duplicate delivery and retries safely.
- External notification and AI calls must not make business approval transactions block.
- Jobs must never fall back to an unrestricted tenant context.

## 7. Delivery and policy state machines

Keep the following as separate concepts:

- **Requirement lifecycle:** draft/review/approval/baseline/revision history.
- **Approval lifecycle:** pending/approved/rejected/withdrawn/expired (exact policies pending validation).
- **Work item workflow:** configurable statuses and permitted transitions.
- **Sprint lifecycle:** planned/active/closed or cancelled (proposed product vocabulary), with fixed original timebox, selection, actual outcome and carry-over history.
- **Release and UAT lifecycle:** evidence collection, readiness, acceptance and deployment.

Transitions should be authorized, validated, auditable when material and protected against concurrent conflicting decisions.

## 8. Planning and forecasting architecture

The planning engine should consume:

- Requirements/backlog scope and active work states.
- Task or story estimates with estimation method and uncertainty metadata.
- Member allocation, availability, holidays and project working calendar.
- Dependency graph and milestone constraints.
- Historical throughput and cycle time, when representative.

Separate **original Sprint snapshot**, **actual execution**, **estimation**, **capacity computation**, **delivery forecast/scenario**, and **commitment/baseline** as different concerns. Missing actual events remain unknown; forecasts cannot extend the original Sprint. A forecast update must not automatically rewrite an approved date. Surface assumptions and scenario differences, and avoid fake statistical certainty.

## 9. Delivery risk intelligence architecture

```text
Sprint/work events + approvals + capacity + engagement/burn + scope changes
                      |
     Signal engine (deterministic, configurable per contract type)
                      |
     Candidate findings (evidence refs, dedup, last-checked)
                      |
            Scoped context retrieval
             (tenant + user ACL)
                      |
       Optional AI interpretation (MVP+1)
                      |
         Validated structured explanation
                      |
  Human review -> risk register (owner, exposure, response)
                      |
   Delivery health read models (internal / client-shared)
```

### Signal engine

- Each signal is a versioned definition: identifier, inputs, criteria/thresholds (configurable per project and contract type), evidence query and severity mapping.
- Evaluation is triggered by relevant domain events (via outbox) and by a scheduled sweep for time-based conditions such as overdue approvals and blocked age. NFR-014 defines freshness.
- Evaluations are idempotent and deduplicated per signal and affected target. Unchanged dismissed findings do not resurface; new or worsening evidence does.
- The engine reads source facts through owning-module contracts or read models, never another module's DbContext.

### Risk register and health

- Confirmed risks, issues and blockers are owned records linked to findings, CRs, Sprints, releases and engagements by scalar IDs.
- Delivery health read models aggregate signals and reviewed risks, keep contributing factors (BR-057) and carry a sharing flag for client views (BR-055).

### Safeguards

- Evidence references include artifact IDs and versions where relevant.
- Retrieved context is filtered by the requesting actor's effective permission.
- External documents/comments are treated as untrusted; never execute their instructions as system policy.
- Findings include rule/model version, input reference, processing timestamp and review state.
- Introduce rate limits, timeouts, token budgets, privacy policy, prompt/response retention rules and evaluation datasets before broad rollout.
- Do not allow the AI worker to approve baselines, reassign staff, promise clients dates, or release software autonomously.
- Rules-first checks should function even if the LLM provider is unavailable.

## 10. Cross-cutting NFR and operating design

Target NFR categories are maintained in [requirements.md](requirements.md#9-non-functional-requirements-to-refine). Implementation planning must specify numeric targets based on actual expected load, support commitments and budget.

Initial operating requirements to validate:

- Automated tests for tenant isolation, project authorization, approval versioning, optimistic concurrency and cross-module contracts.
- Backup/restore rehearsal and evidence retention policy before external pilots.
- Logs, metrics and distributed traces with correlation IDs; exclude secrets and sensitive client content by default.
- CI/CD with repeatable migrations, deploy health checks and rollback strategy.
- Performance tests for requirement histories, task boards, dependency queries and cross-project reporting before committing SLAs.
- Least-privilege deployment identities and secret storage.

## 11. Decision register

| Decision | Current recommendation | Status / trade-off |
|---|---|---|
| ARCH-001 | Modular Monolith first | Proposed: simpler correctness/ops; later extraction possible |
| ARCH-002 | PostgreSQL relational source of truth | Proposed: transactional, graph-like relationships via link tables |
| ARCH-003 | Project with single owning tenant in MVP | Product direction agreed; client collaboration needs policy validation |
| ARCH-004 | Version-bound approvals and immutable baselines | Product direction agreed; exact workflow still to validate |
| ARCH-005 | Rules before AI for objective delivery alerts | Product direction agreed; reduces cost and improves explainability |
| ARCH-006 | Direct module interfaces; avoid internal calls through API gateway | Proposed; aligns with modular monolith coupling constraints |
| ARCH-007 | Introduce Redis/broker/search engine only on measured need | Proposed; reduces premature operational complexity |
| ARCH-008 | Rules-first risk signal engine that works without the LLM provider; AI explanation is an optional downstream step | Product direction agreed (V4, BR-053); engine placement and hosting not decided |
| ARCH-009 | Delivery health and client-shared views as read models built from signals and the risk register | Proposed; keeps dashboards off transactional paths and enforces sharing at projection time |

## 12. Outstanding architecture decisions

1. Product entity separated from Project at MVP or evolved later?
2. How will selected restricted project/guest access, scoped sharing and revocation be represented? General organization membership is not required; organization-to-organization project co-ownership remains outside MVP.
3. How will selected all-required-reviewer, internal/client and explicit acceptance gates be represented? Elaborate chains, quorum and advanced delegation are deferred; legal signatures/external verification have not been selected.
4. Which attachments must be accessible across tenant boundaries, and with what revocation rules?
5. What are the concrete workload, backup, data residency, and AI data-processing targets?
6. Which release evidence and external source-control integrations are required for the pilot?
7. Where do Engagement & Client and Delivery Risk Intelligence live relative to the current independently hosted modules (new module, worker host, or shared host)?
8. Which events and read contracts must Scrum Delivery, Capacity, Approval and Engagement publish for the signal engine?

**Status rule:** This architecture is a recommended design direction. Do not describe any module, infrastructure component, or integration as implemented until source and deployment evidence confirms it.
