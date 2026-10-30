# DeliveryPilot - Architecture and Technical Decisions

> **Status:** Proposed target architecture, not an implemented system.  
> **Owner:** CTO / Solution Architect  
> **Related documents:** [Product](product.md) | [Requirements](requirements.md) | [Roadmap](roadmap.md)

> **Reconciliation — 2026-11-03:** Use the [architecture workspace](architecture/README.md) for selected module design and [current backend architecture](../ARCHITECTURE.md) for the existing independent hosts. The user selected one Identity module/database for Authentication, Organization Management and protected Platform Access. That ownership choice does not select this document's monolith deployment, remove Gateway/broker infrastructure, or settle other module placements. Workflow Q1–Q44/W1 and [Identity decisions](architecture/modules/identity/design-decisions.md) refine the earlier conceptual guidance below.

## 1. Architectural drivers

DeliveryPilot must reconcile complex, closely related workflows: requirements revision, approvals, backlog, resource planning, Scrum/Kanban delivery, verification, and release traceability. Its initial engineering priorities are **correctness, tenant isolation, auditability, changeability, and low operational complexity**, not service count.

Constraints from product direction:

- Multiple organizations and projects, with a single owning tenant per MVP project.
- External client collaborators with strictly scoped access.
- Immutable references to approved versions and baseline history.
- Cross-domain traceability without forcing a single parent-child data hierarchy.
- Configurable approval and delivery policies.
- Event-driven notifications and delivery intelligence when useful.
- Uncertain early scale and still-evolving business requirements.

## 2. Recommended architecture

**Decision candidate: Modular Monolith on ASP.NET Core with PostgreSQL.** Deploy one application boundary initially, with explicit modules and data ownership. The UI can communicate through a normal API edge; introduce a separate YARP gateway only when the deployment architecture benefits from it.

```text
React + TypeScript web app / Client Portal
                 |
          HTTPS API edge
                 |
   ASP.NET Core Modular Monolith
   +-------------------------------------+
   | Identity & Organization             |
   | Project Governance                  |
   | Discovery & Documentation           |
   | Requirements & Approval             |
   | Backlog & Delivery                  |
   | Capacity & Planning                 |
   | Risk & Quality / Release            |
   | AI Intelligence                     |
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
| Project Governance | Project, stakeholders, team membership, configuration | Organization and client reference |
| Discovery & Documentation | Vision, scope, MVP documents, revisions | Project, selected baselines |
| Requirements Management | Requirement, revision, rules, traceability links | Project, review and backlog artifacts |
| Approval / Governance | Approval requests and decisions, scope baselines, change proposals | Versioned artifacts and authorized approvers |
| Product Backlog | Epic, feature, story, technical work item, priority, links | Requirements and projects |
| Delivery Management | Sprint, workflow, execution state, delivery history | Backlog and assignments |
| Team & Planning | Availability, allocation, estimates, calendar, forecast | People, projects and backlog |
| Risk & Quality | Risks, issues, blockers, tests, defects, UAT and release records | Work items, milestones |
| AI Intelligence | Signals, findings, evaluation and feedback | Authorized source references only |
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
- Internal module calls should generally be direct application interfaces, not routed through a public API gateway.
- Integrations (GitHub/GitLab/Azure DevOps, communication tools, AI providers) use replaceable adapters, least-privilege credentials, retry/backoff and explicit failure policies.
- Carry correlation IDs, actor, tenant, and causation context through approved async boundaries; revalidate credentials/permissions as appropriate.

### Background work and events

Start with an internal worker or scheduled jobs for reminders, rule checks and AI analysis. Add a broker if throughput and operational needs justify it. If outbox events are used:

- Persist business state and outbox record in the same transaction.
- Consumers must handle duplicate delivery and retries safely.
- External notification and AI calls must not make business approval transactions block.
- Jobs must never fall back to an unrestricted tenant context.

## 7. Delivery and policy state machines

Keep the following as separate concepts:

- **Requirement lifecycle:** draft/review/approval/baseline/revision history.
- **Approval lifecycle:** pending/approved/rejected/withdrawn/expired (exact policies pending validation).
- **Work item workflow:** configurable statuses and permitted transitions.
- **Sprint lifecycle:** planning/active/completed, with Sprint Goal and history.
- **Release and UAT lifecycle:** evidence collection, readiness, acceptance and deployment.

Transitions should be authorized, validated, auditable when material and protected against concurrent conflicting decisions.

## 8. Planning and forecasting architecture

The planning engine should consume:

- Requirements/backlog scope and active work states.
- Task or story estimates with estimation method and uncertainty metadata.
- Member allocation, availability, holidays and project working calendar.
- Dependency graph and milestone constraints.
- Historical throughput and cycle time, when representative.

Separate **estimation**, **capacity computation**, **delivery forecast**, and **commitment/baseline** as different concerns. A forecast update must not automatically rewrite an approved date. Surface assumptions and scenario differences, and avoid fake statistical certainty.

## 9. AI intelligence architecture

```text
Project changes + work events + calendars + requirements
                      |
              Deterministic signals
                      |
            Scoped context retrieval
             (tenant + user ACL)
                      |
           Optional AI interpretation
                      |
           Validated structured finding
                      |
       Human review / feedback / action
```

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

## 12. Outstanding architecture decisions

1. Product entity separated from Project at MVP or evolved later?
2. How will selected restricted project/guest access, scoped sharing and revocation be represented? General organization membership is not required; organization-to-organization project co-ownership remains outside MVP.
3. How will selected all-required-reviewer, internal/client and explicit acceptance gates be represented? Elaborate chains, quorum and advanced delegation are deferred; legal signatures/external verification have not been selected.
4. Which attachments must be accessible across tenant boundaries, and with what revocation rules?
5. What are the concrete workload, backup, data residency, and AI data-processing targets?
6. Which release evidence and external source-control integrations are required for the pilot?

**Status rule:** This architecture is a recommended design direction. Do not describe any module, infrastructure component, or integration as implemented until source and deployment evidence confirms it.
