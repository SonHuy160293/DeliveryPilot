[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]+(-[a-z0-9]+)*$')]
    [string]$Slug,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^FEAT-[A-Z0-9]+-[0-9]{3,}$')]
    [string]$FeatureId,

    [Parameter(Mandatory = $false)]
    [string]$Module = "identity",

    [Parameter(Mandatory = $false)]
    [string]$SeedIdea = "User-provided feature candidate; detailed scope and policies pending discovery and owner review.",

    [Parameter(Mandatory = $false)]
    [switch]$Minimal,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$featuresRoot = Join-Path $repoRoot "docs\features"

if (-not (Test-Path -LiteralPath $featuresRoot)) {
    throw "Features directory not found at $featuresRoot"
}

if (-not $FeatureId) {
    $maxNum = 0
    Get-ChildItem -Path $featuresRoot -Directory | ForEach-Object {
        if ($_.Name -match '^FEAT-DP-(\d{3,})') {
            $n = [int]$Matches[1]
            if ($n -gt $maxNum) { $maxNum = $n }
        }
    }
    $nextNum = $maxNum + 1
    $FeatureId = ("FEAT-DP-{0:D3}" -f $nextNum)
}

if ($FeatureId -match '^FEAT-([A-Z0-9]+-\d{3,})$') {
    $shortKey = $Matches[1] # e.g. DP-002
} else {
    $shortKey = $FeatureId -replace '^FEAT-', ''
}

$featureFolderName = "$FeatureId-$Slug"
$featureDir = Join-Path $featuresRoot $featureFolderName

if ((Test-Path -LiteralPath $featureDir) -and -not $Force) {
    throw "Feature directory already exists: $featureDir (pass -Force to overwrite)"
}

$subDirs = @("discovery", "stories", "use-cases", "design", "tasks", "evidence")
foreach ($sd in $subDirs) {
    New-Item -ItemType Directory -Path (Join-Path $featureDir $sd) -Force | Out-Null
}

$designLinks = @("- [Solution](design/solution.md)")
if (-not $Minimal) {
    $designLinks += "- [Feature API design](design/api-design.md)"
    $designLinks += "- [Feature data impact](design/data-impact.md)"
    $designLinks += "- [Sequence](design/sequence.md)"
}

$tokens = @{
    '{{FeatureId}}'   = $FeatureId
    '{{Slug}}'        = $Slug
    '{{Title}}'       = $Title
    '{{Module}}'      = $Module
    '{{SeedIdea}}'    = $SeedIdea
    '{{DesignLinks}}' = ($designLinks -join "`n")
    '{{Date}}'        = (Get-Date -Format 'yyyy-MM-dd')
    '{{FR1}}'         = "FR-$shortKey-01"
    '{{FR2}}'         = "FR-$shortKey-02"
    '{{US1}}'         = "US-$shortKey-01"
    '{{UC1}}'         = "UC-$shortKey-01"
    '{{TASK1}}'       = "TASK-$shortKey-01"
    '{{ASSUMP1}}'     = "A-$shortKey-01"
}

function Write-Template([string]$RelPath, [string]$Template) {
    $rendered = $Template
    foreach ($k in $tokens.Keys) {
        $rendered = $rendered.Replace($k, $tokens[$k])
    }
    $fullPath = Join-Path $featureDir $RelPath
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($fullPath, $rendered, $utf8NoBom)
}

# 1. Feature README.md
Write-Template "README.md" @'
---
id: "{{FeatureId}}"
title: "{{FeatureId}}: {{Title}}"
doc_type: "feature-index"
status: "hypothesis"
owner_role: "Senior BA / Product Owner"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# {{FeatureId}}: {{Title}}

> **Status: hypothesis.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Navigate this feature end-to-end. All product behavior remains a hypothesis until explicitly accepted.

## Feature intent and status

**Original idea:** {{SeedIdea}}

**Primary module candidate:** `{{Module}}`

**Feature status:** Hypothesis; no production authorization implied.

## Documentation navigation

- [Proposal](discovery/proposal.md)
- [Research](discovery/research.md)
- [Assumptions](discovery/assumptions.md)
- [Requirements](requirements.md)
- [User story](stories/{{US1}}-{{Slug}}.md)
- [Use case](use-cases/{{UC1}}-{{Slug}}.md)
{{DesignLinks}}
- [Agent tasks](tasks/README.md)
- [Verification evidence](evidence/{{TASK1}}-verification.md)

## Impacted modules (candidate)

- Primary candidate module: `{{Module}}` (verify boundaries in [src/Modules/AGENTS.md](../../../src/Modules/AGENTS.md) and [module boundaries](../../architecture/module-boundaries.md)).

## Readiness and owner decisions

- [ ] Discovery assumptions reviewed by Product Owner
- [ ] Feature requirements accepted with `approval_ref` (Gate 1)
- [ ] Solution design accepted with `approval_ref` (Gate 2)

## Evidence, review and unresolved questions

- **Confirmed source references:** Scaffolded via `dp-scaffold-feature`; inspect repository before claiming implementation.
- **Outstanding decisions:** Complete discovery and requirements review before authorizing tasks.
- **Review / approval reference:** Not recorded.
'@

# 2. discovery/proposal.md
Write-Template "discovery\proposal.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DISCOVERY-PROPOSAL"
title: "Feature proposal: {{Title}}"
doc_type: "feature-proposal"
status: "hypothesis"
owner_role: "Senior BA / Product Owner"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Feature proposal: {{Title}}

> **Status: hypothesis.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Record problem hypothesis, affected users, alternatives, and decisions needed before approving requirements.

## Origin and authority

- **Origin:** User-provided seed idea: {{SeedIdea}}
- **Authority:** Candidate feature proposal; not an accepted customer specification.

## Problem hypothesis

_[To be completed during discovery (`dp-product-discovery`). State who experiences the problem, when, and what workaround exists today.]_

## Candidate outcome and users

- **Target actor(s):** [Specify primary actor from [personas](../../../product/personas.md)]
- **Desired outcome:** [Observable user decision or capability improved]

## Potential solution paths

| Option | Summary | Pros | Cons / Risks |
|---|---|---|---|
| Option A (MVP candidate) | [Smallest viable workflow] | [Fast validation] | [Scope limits] |
| Option B (Manual / baseline) | [Existing workaround or manual setup] | [Zero new code] | [Operational friction] |

## Decisions required

- Resolve open items in [assumptions.md](assumptions.md) before marking [requirements.md](../requirements.md) as `accepted`.

## Evidence, review and unresolved questions

- **Confirmed source references:** [Link verified repository paths or primary research]
- **Outstanding decisions:** [Specify owner and question before accepting.]
- **Review / approval reference:** Not recorded.
'@

# 3. discovery/research.md
Write-Template "discovery\research.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DISCOVERY-RESEARCH"
title: "Feature research: {{Title}}"
doc_type: "feature-research"
status: "hypothesis"
owner_role: "Senior BA / Research Analyst"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Feature research: {{Title}}

> **Status: hypothesis.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Gather repository-verified facts and external evidence relevant to {{Title}}.

## Research questions and method

1. What already exists in the repository for module `{{Module}}`?
2. What domain invariants, security boundaries, and failure cases apply?

## Source ledger

| ID | Claim | Class | Exact reference | Date checked | Limitation |
|---|---|---|---|---|---|
| E-001 | Seed feature intent for {{Title}} | user-provided | User request | {{Date}} | Needs detailed acceptance policy |

## Findings and counterexamples

_[To be completed using `dp-product-discovery`.]_

## Implications and open research

- Map unresolved policy choices to [assumptions.md](assumptions.md).

## Evidence, review and unresolved questions

- **Confirmed source references:** Not yet verified against implementation files.
- **Outstanding decisions:** [Specify owner and question before accepting.]
- **Review / approval reference:** Not recorded.
'@

# 4. discovery/assumptions.md
Write-Template "discovery\assumptions.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DISCOVERY-ASSUMPTIONS"
title: "Assumptions and decision register: {{Title}}"
doc_type: "assumption-register"
status: "hypothesis"
owner_role: "Senior BA / Product Owner"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Assumptions and decision register: {{Title}}

> **Status: hypothesis.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Track uncertain business or technical choices before converting them into requirements or code.

## Assumption register

| ID | Hypothesis / assumption | Class / Origin | Risk / Impact | Validation step | Decision owner | State |
|---|---|---|---|---|---|---|
| {{ASSUMP1}} | Initial workflow and actor permissions for {{Title}} match MVP scope in [scope.md](../../../product/scope.md) | AI-inference | High | Product Owner review | Product Owner | Open |

## Decisions blocking production

- Confirm actor permissions, tenant isolation rules, and state transitions in [requirements.md](../requirements.md).

## Evidence, review and unresolved questions

- **Confirmed source references:** Linked to [proposal.md](proposal.md).
- **Outstanding decisions:** Product Owner resolution of `{{ASSUMP1}}`.
- **Review / approval reference:** Not recorded.
'@

# 5. requirements.md
Write-Template "requirements.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-REQUIREMENTS"
title: "{{FeatureId}} feature requirements: {{Title}}"
doc_type: "feature-requirements"
status: "proposed"
owner_role: "Senior BA / Product Owner"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# {{FeatureId}} feature requirements: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Define observable behavior, state lifecycle, and testable acceptance criteria for {{Title}}.

## Problem, users and scope

Source: [Proposal](discovery/proposal.md) and [Product scope](../../product/scope.md). Status: proposed until approved.

## Input signals and data prerequisites

| Input / signal | Required? | Freshness expectation | Missing / stale behavior | Source owner |
|---|---|---|---|---|
| Primary request payload / actor context | Yes | Real-time at request time | Reject invalid or incomplete input with explicit validation error | `{{Module}}` |
| Tenant & authorization context | Yes | Real-time at request time | Deny unauthorized or cross-tenant operations | Identity (`DeliveryPilot.Identity`) |

## Functional requirements

| ID | Proposed behavior | Rationale | Priority | Approval |
|---|---|---|---|---|
| {{FR1}} | Authorized actor can execute the primary {{Title}} workflow with validated inputs and durable state | Core feature outcome | P1 | Proposed |
| {{FR2}} | System enforces uniqueness, authorization, and tenant isolation invariants for {{Title}} | Security and domain integrity | P1 | Proposed |

## Entity lifecycle and state transitions

| From state | Trigger / action | Authorized actor | To state | Invariant / side effect |
|---|---|---|---|---|
| `[None]` | Valid creation / registration request (`{{FR1}}`) | Authorized actor | `Active` (or `PendingVerification`) | Persists entity and emits domain/integration event within module transaction [Proposed] |
| `Active` | Invalid or duplicate mutation attempt (`{{FR2}}`) | Any actor | `Active` (unchanged) | Rejects mutation; preserves existing state [Proposed] |

## Business and access rules

- Link global policies in [business-rules.md](../../product/business-rules.md).
- Specify exact permission and tenant boundary rules before setting `status: "accepted"`.

## Acceptance scenarios

### AC-01: {{FR1}} - Happy path execution
- **Given** an eligible actor and valid input satisfying all preconditions
- **When** the actor submits the request for {{Title}} (`{{FR1}}`)
- **Then** the system completes the workflow, persists the expected state transition, and returns a deterministic confirmation. [Proposed]

### AC-02: {{FR2}} - Duplicate, invalid, or unauthorized rejection
- **Given** a request with duplicate keys, invalid fields, or an unauthorized actor
- **When** the actor attempts the operation (`{{FR2}}`)
- **Then** the system rejects the request with the appropriate validation/authorization error without mutating state or leaking cross-tenant information. [Proposed]

## NFR and analytics references

- Reference system-wide targets in [nfr.md](../../product/nfr.md).

## Out of scope and unresolved decisions

- Resolve open assumptions in [assumptions.md](discovery/assumptions.md) before Product Owner acceptance.

## Approval record

- **Status:** `proposed` (`approval_ref: null`).
- **Gate criteria:** Product Owner reviews `{{FR1}}`–`{{FR2}}`, state lifecycle, and `AC-01`–`AC-02`.

## Evidence, review and unresolved questions

- **Confirmed source references:** [Proposal](discovery/proposal.md), [Assumptions](discovery/assumptions.md).
- **Outstanding decisions:** Product Owner sign-off required.
- **Review / approval reference:** Not recorded.
'@

# 6. stories/US-...
Write-Template "stories\$($tokens['{{US1}}'])-$Slug.md" @'
---
id: "{{US1}}"
title: "{{US1}}: {{Title}}"
doc_type: "user-story"
status: "proposed"
owner_role: "Senior BA / Product Owner"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# {{US1}}: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Specify the user outcome and directly verifiable acceptance conditions for {{Title}}.

## Story

> As an authorized user, I want to complete {{Title}}, so that I can achieve the intended delivery outcome safely and predictably. [Proposed]

## Context and business rationale

- **Feature ID:** `{{FeatureId}}`
- **Covered FR IDs:** [Requirements](../requirements.md) (`{{FR1}}`, `{{FR2}}`)
- **Provenance:** User-provided seed idea ({{SeedIdea}})

## Acceptance criteria

```gherkin
Scenario: Successfully complete {{Title}} ({{FR1}})
  Given an authorized actor with valid input
  When the actor initiates {{Title}}
  Then the expected state is recorded and confirmed

Scenario: Reject invalid or duplicate attempt ({{FR2}})
  Given invalid input, a duplicate identifier, or an unauthorized actor
  When the actor initiates {{Title}}
  Then the operation is rejected with a clear error and no side effects occur
```

## Out of scope

- Unapproved cross-module mutations or autonomous policy changes.

## Evidence, review and unresolved questions

- **Confirmed source references:** Linked to [requirements.md](../requirements.md).
- **Outstanding decisions:** Confirm actor role and exact validation rules.
- **Review / approval reference:** Not recorded.
'@

# 7. use-cases/UC-...
Write-Template "use-cases\$($tokens['{{UC1}}'])-$Slug.md" @'
---
id: "{{UC1}}"
title: "{{UC1}}: {{Title}}"
doc_type: "use-case"
status: "proposed"
owner_role: "Senior BA / Domain Expert"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# {{UC1}}: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Describe the trigger-to-outcome path, interaction channel, and alternate/exception flows for {{Title}}.

## Goal, actors and trigger

- **Primary actor:** [Specify actor]
- **Trigger:** Actor initiates {{Title}}.

## Preconditions

- Required inputs and authorization context are available as specified in [requirements.md](../requirements.md).

## Interaction surface and channel

- **Primary channel / surface:** HTTP API endpoint / Web UI workflow (proposed)
- **Mandatory co-displayed context:** Validation feedback, resulting entity identifier/state (`{{FR1}}`), and error correlation on failure (`{{FR2}}`).

## Main success flow

1. Actor submits the request for {{Title}}.
2. System validates input structure, uniqueness, and authorization (`{{FR1}}`, `{{FR2}}`).
3. System applies the domain state transition and records any outbox/integration event.
4. System returns the result to the actor.

All steps are proposed, not approved.

## Alternative and exception flows

- **E1 – Invalid or duplicate input (`{{FR2}}`):** System halts before persistence and returns a validation/conflict error.
- **E2 – Unauthorized or cross-tenant access (`{{FR2}}`):** System denies the request without exposing protected state.

## Postconditions and invariants

- Module data ownership and transaction boundaries in [src/Modules/AGENTS.md](../../../../src/Modules/AGENTS.md) are preserved.

## Acceptance mapping and decisions

- **Requirements covered:** [Requirements](../requirements.md) (`{{FR1}}`, `{{FR2}}`)
- **Related user story:** [{{US1}}](../stories/{{US1}}-{{Slug}}.md)

## Evidence, review and unresolved questions

- **Confirmed source references:** Linked to [requirements.md](../requirements.md).
- **Outstanding decisions:** Confirm exact status codes and async follow-up semantics.
- **Review / approval reference:** Not recorded.
'@

# 8. design/solution.md
Write-Template "design\solution.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DESIGN-SOLUTION"
title: "Solution design: {{Title}}"
doc_type: "feature-solution"
status: "proposed"
owner_role: "Senior Solution Architect / DBA / Backend Lead"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Solution design: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Explain module ownership, CQRS slice placement, transaction boundaries, and trade-offs for {{Title}}.

## Inputs and authorized requirements

- Upstream specification: [requirements.md](../requirements.md) (`{{FR1}}`, `{{FR2}}`).
- Architectural rules: [AGENTS.md](../../../../AGENTS.md) and [src/Modules/AGENTS.md](../../../../src/Modules/AGENTS.md).

## Module responsibilities

- **Owning module:** `{{Module}}`
- Follow Clean Architecture + custom CQRS (`ISender`) layout under `Features/<Area>/Commands/<UseCase>/` or `Features/<Area>/Queries/<UseCase>/`.

## Transaction and consistency boundaries

- Commands commit via the registered transaction pipeline behavior; handlers and repositories must not call `SaveChangesAsync` directly when the pipeline owns the transaction.
- Cross-module side effects (e.g., notifications) integrate via contracts/outbox events, never via direct cross-module Domain/Infrastructure references.

## Security / multi-tenancy

- Enforce organization/tenant isolation and role checks at the Application/Domain boundary.
- Never log credentials, tokens, secrets, or raw sensitive payloads.

## Verification and open decisions

- Requires Product Owner acceptance of [requirements.md](../requirements.md) and Architect acceptance of this file before setting tasks in [tasks/README.md](../tasks/README.md) to `ready`.

## Evidence, review and unresolved questions

- **Confirmed source references:** [AGENTS.md](../../../../AGENTS.md), [src/Modules/AGENTS.md](../../../../src/Modules/AGENTS.md).
- **Outstanding decisions:** Verify exact Domain aggregates and EF Core mappings in the repository.
- **Review / approval reference:** Not recorded.
'@

if (-not $Minimal) {
    Write-Template "design\api-design.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DESIGN-API"
title: "API design: {{Title}}"
doc_type: "feature-api-design"
status: "proposed"
owner_role: "Senior Backend Lead / Solution Architect"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# API design: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Capture endpoint ownership, request/response contract placement, validation, and status code semantics for {{Title}}.

## Operation and ownership

- **Owning module:** `{{Module}}`
- **API request contracts:** Place HTTP request models under the owning API project's `Contracts/<Area>/<RequestName>.cs` per [src/Modules/AGENTS.md](../../../../src/Modules/AGENTS.md).
- **Related requirements:** [requirements.md](../requirements.md) (`{{FR1}}`, `{{FR2}}`).

## HTTP and error semantics

- Let supported `DomainException` and `CqrsValidationException` failures reach `GlobalExceptionHandlingMiddleware`.
- Preserve existing result-to-response propagation conventions in the owning controller.

## Evidence, review and unresolved questions

- **Confirmed source references:** [api-guidelines.md](../../../engineering/api-guidelines.md).
- **Outstanding decisions:** Confirm exact route, status code (200/201/202), and idempotency semantics.
- **Review / approval reference:** Not recorded.
'@

    Write-Template "design\data-impact.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DESIGN-DATA-IMPACT"
title: "Data impact: {{Title}}"
doc_type: "feature-data-impact"
status: "proposed"
owner_role: "Senior DBA / Backend Lead"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Data impact: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Analyze persistence changes, constraints, indexes, and migration impact for {{Title}}.

## Ownership and schema conventions

- **Owning module:** `{{Module}}`
- Follow plural PascalCase table names, PascalCase columns, and UTC `DateTimeOffset` instants per [AGENTS.md](../../../../AGENTS.md).
- Apply migrations via the module's `MigrationRunner`, never implicitly at API startup.

## Conceptual changes and constraints

- Document new or modified entities, unique indexes, and concurrency tokens supporting [requirements.md](../requirements.md) (`{{FR1}}`, `{{FR2}}`).

## Evidence, review and unresolved questions

- **Confirmed source references:** [data-architecture.md](../../../architecture/data-architecture.md).
- **Outstanding decisions:** Inspect existing module `DbContext` and migrations before proposing schema changes.
- **Review / approval reference:** Not recorded.
'@

    Write-Template "design\sequence.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-DESIGN-SEQUENCE"
title: "Sequence diagram: {{Title}}"
doc_type: "feature-sequence"
status: "proposed"
owner_role: "Senior Backend Engineer / Solution Architect"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Sequence diagram: {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Trace the proposed end-to-end interaction for [{{UC1}}](../use-cases/{{UC1}}-{{Slug}}.md).

## Scenario and scope

- Traces [{{UC1}}](../use-cases/{{UC1}}-{{Slug}}.md) covering `{{FR1}}` and `{{FR2}}` in [requirements.md](../requirements.md).

## Proposed Mermaid sequence

```mermaid
sequenceDiagram
    actor User as Actor
    participant API as Module.API
    participant App as Module.Application (CQRS)
    participant Dom as Module.Domain
    participant DB as Module.Infrastructure (DbContext)

    User->>API: Request ({{Title}})
    API->>App: Send Command via ISender
    App->>Dom: Enforce domain invariants ({{FR1}}, {{FR2}})
    App->>DB: Persist via TransactionBehavior
    DB-->>API: Result
    API-->>User: HTTP Response
```

## Evidence, review and unresolved questions

- **Confirmed source references:** [solution.md](solution.md).
- **Outstanding decisions:** Reconcile participants with actual repository types during design.
- **Review / approval reference:** Not recorded.
'@
}

# 9. tasks/README.md
Write-Template "tasks\README.md" @'
---
id: "DOC-FEATURES-{{FeatureId}}-TASKS-README"
title: "Agent task index for {{FeatureId}}"
doc_type: "task-navigation"
status: "proposed"
owner_role: "Senior Backend Lead"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Agent task index for {{FeatureId}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Track bounded implementation tasks for {{FeatureId}} (`{{Title}}`).

## Task list and dependencies

- [{{TASK1}}: initial implementation slice]({{TASK1}}-{{Slug}}.md)

## Status and approval policy

- Task status lives in each task's YAML frontmatter.
- A production task may move to `status: "ready"` only when [requirements.md](../requirements.md) and [design/solution.md](../design/solution.md) are `accepted` with recorded `approval_ref` values.

## Evidence, review and unresolved questions

- **Confirmed source references:** [definition-of-ready.md](../../../governance/definition-of-ready.md).
- **Outstanding decisions:** Refine task breakdown with `dp-feature-planner` once requirements and solution design are approved.
- **Review / approval reference:** Not recorded.
'@

# 10. tasks/TASK-...
Write-Template "tasks\$($tokens['{{TASK1}}'])-$Slug.md" @'
---
id: "{{TASK1}}"
title: "{{TASK1}}: implement initial slice for {{Title}}"
doc_type: "agent-task"
feature_id: "{{FeatureId}}"
module: "{{Module}}"
kind: "implementation"
status: "proposed"
priority: "P1"
delivery_mode: "production"
owner_role: "Senior Backend Engineer"
approval_ref: null
last_verified: null
readiness: "BLOCKED: feature requirements and solution design are not accepted"
depends_on: []
spec_refs:
  - "../requirements.md"
  - "../design/solution.md"
acceptance_refs:
  - "{{FR1}}"
  - "{{FR2}}"
evidence_ref: "../evidence/{{TASK1}}-verification.md"
---

# {{TASK1}}: implement initial slice for {{Title}}

> **Status: proposed.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Bounded implementation placeholder to be refined by `dp-feature-planner` after requirements and solution design approval.

## Objective

Implement the initial vertical slice for {{Title}} satisfying `{{FR1}}` and `{{FR2}}` in [requirements.md](../requirements.md). [Proposed]

## Read first and preconditions

- [AGENTS.md](../../../../AGENTS.md) and [src/Modules/AGENTS.md](../../../../src/Modules/AGENTS.md)
- [requirements.md](../requirements.md)
- [design/solution.md](../design/solution.md)

## Allowed and forbidden scope

- **Allowed:** Scoped changes within the owning module (`{{Module}}`) and its tests once authorized.
- **Forbidden:** Cross-module Domain/Infrastructure coupling, implicit startup migrations, or unapproved policy changes.

## Engineering constraints

- Preserve the CQRS pipeline order (validation, logging, performance, tracing, transaction, handler).
- Pass `CancellationToken` through async boundaries and use UTC `DateTimeOffset`.

## Acceptance and test plan

- Verify happy path (`AC-01` / `{{FR1}}`) and negative/duplicate/unauthorized paths (`AC-02` / `{{FR2}}`).
- Record actual test execution in [../evidence/{{TASK1}}-verification.md](../evidence/{{TASK1}}-verification.md).

## Evidence, review and unresolved questions

- **Confirmed source references:** Linked to [requirements.md](../requirements.md) and [design/solution.md](../design/solution.md).
- **Outstanding decisions:** Decompose into granular tasks via `dp-feature-planner` once upstream specs are accepted.
- **Review / approval reference:** Not recorded.
'@

# 11. evidence/TASK-...-verification.md
Write-Template "evidence\$($tokens['{{TASK1}}'])-verification.md" @'
---
id: "EVIDENCE-{{TASK1}}"
title: "Verification record for {{TASK1}}"
doc_type: "verification-evidence"
status: "not_run"
owner_role: "Senior QA / Backend Lead"
approval_ref: null
last_verified: null
feature_id: "{{FeatureId}}"
delivery_mode: "production"
---

# Verification record for {{TASK1}}

> **Status: not_run.** Proposed/draft starter. Content below is not a confirmed implementation or approved requirement.
> **Purpose:** Record real test and review evidence for [{{TASK1}}](../tasks/{{TASK1}}-{{Slug}}.md).

## Execution identity

- **Task:** [{{TASK1}}](../tasks/{{TASK1}}-{{Slug}}.md)
- **Execution state:** Not run
- **Commit / PR:** Not available

## Acceptance mapping

- Covers `{{FR1}}` and `{{FR2}}` defined in [requirements.md](../requirements.md).

## Commands and observed results

| Check | Command / CI run | Observed result | Status |
|---|---|---|---|
| Unit & integration verification | Not run | No evidence | Not verified |

## Evidence, review and unresolved questions

- **Confirmed source references:** None executed yet.
- **Outstanding decisions:** Execute only after [{{TASK1}}](../tasks/{{TASK1}}-{{Slug}}.md) is `ready` and implemented.
- **Review / approval reference:** Not recorded.
'@

# 12. Register in docs/features/README.md
$featuresIndexPath = Join-Path $featuresRoot "README.md"
if (Test-Path -LiteralPath $featuresIndexPath) {
    $indexText = Get-Content -LiteralPath $featuresIndexPath -Raw -Encoding UTF8
    $entryLine = "- [${FeatureId}: $Title (hypothesis)]($featureFolderName/README.md)"
    if ($indexText -notmatch [regex]::Escape("$featureFolderName/README.md")) {
        $updatedIndex = [regex]::Replace(
            $indexText,
            '(## Feature navigation\s*\r?\n(?:<!--[^\n]*-->\s*\r?\n)?(?:\s*-\s*\[[^\n]+\r?\n)+)',
            "`$1$entryLine`n"
        )
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($featuresIndexPath, $updatedIndex, $utf8NoBom)
    }
}

Write-Host "Scaffolded feature $FeatureId at: $featureDir"
Write-Host "Registered in: $featuresIndexPath"
