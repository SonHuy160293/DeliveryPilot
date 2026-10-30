# DeliveryPilot

**AI-Powered Software Delivery Lifecycle Management Platform**

DeliveryPilot is a multi-tenant B2B SaaS product for software agencies and engineering teams to manage software delivery from **product discovery and client requirements through backlog planning, implementation, QA/UAT and release**. Its differentiators are versioned client approvals, requirement-to-release traceability, delivery governance, and explainable AI recommendations—not simply another task board.

> **Product status:** Blueprint agreed; detailed requirements and MVP assumptions are being validated.<br>
> **Repository status:** The supplied backend documentation describes Identity, Notification and a YARP Gateway; product roadmap capabilities are not necessarily implemented.<br>
> **North star:** Every software delivery commitment should be understandable, traceable, measurable and proactively manageable.

## Who it serves

Product Owners and Business Analysts work from vision and requirements to reviewed backlog items. Project/Delivery Managers track dependencies, milestones, risks and capacity. Developers, QA and DevOps deliver verifiable work. External client stakeholders can participate in controlled reviews and approvals. Executives obtain delivery and portfolio visibility.

**Initial market hypothesis:** Software outsourcing companies coordinating multiple teams and client projects. This customer focus and commercial assumptions still require validation.

## Product scope and MVP

The MVP targets one end-to-end, client-inclusive workflow:

```text
Organization → Project → Vision/Scope → Requirement Version → Review/Approval
    → Feature/User Story → Scrum or Kanban Work → Delivery Evidence → Traceability
```

Requirements and backlog items are separate but linked business artifacts. Approvals are configurable and reference specific versions; not every Story requires client approval. Scrum and Kanban have different work policies and metrics. AI begins with explainable, rule-based signals and supports human decisions rather than silently changing commitments.

For planned features and release priorities, see [Product Roadmap](docs/roadmap.md). The full product intent and non-goals live in [Product Blueprint](docs/product.md).

## Documentation — source of truth

| Document | Purpose |
|---|---|
| [Product Blueprint](docs/product.md) | Vision, audiences, business value, lifecycle, MVP |
| [Cross-feature Requirements](docs/requirements.md) | Shared business rules, governance, NFRs |
| [Product Roadmap](docs/roadmap.md) | Feature registry, priorities and planned releases |
| [Context Map](docs/context-map.md) | Efficient context routing for people and AI agents |
| [Authoring Policy](docs/authoring.md) | Stable artifact IDs, statuses, traceability and review rules |
| [Feature Artifacts](docs/features/) | One file per Feature, User Requirement, Functional Requirement, Story and Task |
| [Artifact Templates](docs/templates/) | Canonical Feature/UR/FR/US/Task formats |
| [Current Backend Architecture](ARCHITECTURE.md) | Existing Identity, Notification, Gateway, messaging and deployment boundaries |
| [Target Architecture Proposal](docs/architecture.md) | Proposed future domain boundaries and technology decisions; not an implemented topology |

**Critical distinction:** `ARCHITECTURE.md` describes the current backend **as documented**; `docs/architecture.md` describes a prospective target. The target document's modular-monolith proposal must be reconciled with the existing separate runtime modules through an explicit technical decision before implementation. Code, tests and deployment files remain authoritative for actual behavior.

## Feature → Requirements → Coding Tasks

For an active feature, documentation is organized as:

```text
docs/features/FEAT-DP-003-organization/
├── feature.md
├── ur/       # one User Requirement per file
├── fr/       # one Functional Requirement per file
├── us/       # one User Story per file, with acceptance criteria
└── tasks/    # one implementation deliverable per file
```

Stable identifiers connect artifacts: `FEAT-DP-003`, `UR-DP-003-001`, `FR-DP-003-001`, `US-DP-003-001`, `TASK-DP-003-001`.

FRs reference source URs, Stories reference FRs, and Tasks reference Stories/FRs/NFRs. These are many-to-many relationships, not a forced one-to-one hierarchy. Proposed UR/FR/US content remains a **draft** until reviewed; approval requires real authorized decision evidence.

## Work with Codex / AI agents

Three repository-local skills are provided in the AI engineering package:

- **`$dp-feature-engineer`** — discover, create, refine, validate and impact-analyze features and separate UR/FR/US documents. It loads relevant context via `docs/context-map.md` and creates drafts, not approved contracts.
- **`$dp-task-planner`** — inspect actual code and turn ready Stories, FRs or NFRs into coding-ready task documents including API/input/output/validation/security/persistence/tests where applicable. It plans rather than implements code.
- **`$add-cqrs-api-feature`** — executes eligible CQRS/API Tasks using the actual DeliveryPilot source, task preflight, module conventions, and focused tests. It does not override business decisions or execute blocked plans.

Example prompts:

```text
$dp-feature-engineer create FEAT-DP-007
Create Approval Workflow draft UR, FR and US artifacts; review gaps and references. Do not implement code.

$dp-task-planner plan-story US-DP-003-001
Inspect the current Identity module; plan coding tasks with API, authorization, validation and tests. Do not implement code.
```

An implementation agent then reads an individual `TASK-DP-...md`, its linked requirements, applicable `AGENTS.md` instructions and existing code. It must not invent unapproved business rules or report success without executing checks.

## Documentation tooling

When the accompanying `tools/docs.py` and tests have been integrated into the repository, run from repository root:

```bash
python tools/docs.py validate
python tools/docs.py coverage --feature FEAT-DP-003
python tools/docs.py check-task --id TASK-DP-003-001  # expected to fail: draft/blocked sample
python -m unittest discover -s tools/docs_validation -p "test_*.py" -v
```

**Test ownership:** Your root `tests/` directory stays reserved for existing .NET unit/integration tests. The Python documentation CLI's regression suite lives only under `tools/docs_validation/`, is executed separately, and does not change `dotnet test` configuration or test project references.

**Safe upgrade from v2/v3:** If you already installed an earlier docs workflow, it may have added `tests/test_docs.py`. The new archive never contains anything under root `tests/`, so extraction will not change your tests. To inspect the old file, run `python tools/docs_validation/migrate_legacy_tests.py`; run it with `--apply` only if the checksum check identifies the original unmodified generated file. Unknown/modified files are left untouched. The helper never removes the root `tests/` directory.

Documentation checks validate references, IDs and structure; they **do not** approve requirements or establish technical correctness. See [Authoring Policy](docs/authoring.md) for rules on versions, review status and IDs.

## Existing backend and development

The supplied architecture documents a **.NET 10** backend with separate Identity and Notification API/Worker/MigrationRunner hosts, YARP Gateway, PostgreSQL per module, RabbitMQ and optional observability components. The Users integration lookup and retained outbox/delivery workflow are described in [Current Backend Architecture](ARCHITECTURE.md). Do not infer that a proposed Product/Project/Requirements/Backlog frontend or API already runs.

Identity workspace and member list reads use opaque keyset cursors, with a default limit of 50 and a maximum of 100. Workspaces are ordered by creation time and organization ID; members by original join time and membership ID, both ascending. Each page rechecks current authority. Pagination does not provide a snapshot, so concurrent changes may affect later pages.

For actual local commands, inspect `scripts/`, `deploy/`, `deploy/env/local.env.example`, `global.json` and the checked-in solution. The source tree and its module instructions should determine run/build/test procedures; avoid copying unverified commands from planning docs. For project-reference changes, the documented architecture check is `powershell -File scripts/Test-Architecture.ps1`.

Never commit credentials, customer data, real tokens or environment secrets.

## Contribution and governance

Read [AGENTS.md](AGENTS.md) and any deeper applicable module instructions. Changes to a requirement must preserve traceable IDs and approval/version history. Significant changes to architecture, integration contracts or tenant isolation require review. Separate drafted product direction from accepted requirements and independently verified runtime behavior.

**License:** To be confirmed by the project owner.

## CQRS task execution handoff

The planning workflow now integrates the repository skill `.agents/skills/add-cqrs-api-feature/`. A Task may include optional `implementation_skill: add-cqrs-api-feature`. This is a recommendation only; it never authorizes code changes.

For CQRS/API work, the task planner checks relevant implementer conventions during planning, then the implementer **re-reads** the Task, requirements, module instructions and current source before making changes. A Task marked `blocked` remains non-executable regardless of any skill recommendation or previous `codebase_verified` flag.

```text
$dp-task-planner review-task TASK-DP-003-001
# After actual codebase inspection and required business review:
python tools/docs.py check-task --id TASK-DP-003-001
$add-cqrs-api-feature Implement TASK-DP-003-001; re-check readiness and run relevant tests.
```

`check-task` is a structural preflight only; client approvals, code freshness, and runtime correctness still require human/source verification.
