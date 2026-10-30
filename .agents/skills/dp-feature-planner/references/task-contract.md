# Agent task file contract (DeliveryPilot)

Use actual repository template when present; metadata below adds optional `spec_refs` and `acceptance_refs` to increase traceability. Use valid YAML frontmatter; paths in `spec_refs` are **relative to the task file**.

```yaml
---
id: TASK-DP-004
title: Assess dependency risk from normalized evidence
doc_type: agent-task
feature_id: FEAT-DP-001
module: delivery-intelligence
kind: implementation
status: proposed
priority: P1
delivery_mode: production
approval_ref: null
readiness: 'BLOCKED: requirements and solution are proposed'
depends_on:
  - TASK-DP-001
spec_refs:
  - ../requirements.md
  - ../design/solution.md
acceptance_refs:
  - FR-DP-001
evidence_ref: ../evidence/TASK-DP-004-verification.md
---
```

For a newly proposed feature, never force status `ready` by inventing approval references. Reuse existing feature task IDs; the example ID above is **illustrative** and must be checked for collisions.

## Required prose sections

1. **Objective**: one behavior/outcome, grounded in FR IDs.
2. **Preconditions / read first**: canonical specs, validated code entry points, prerequisites.
3. **In scope**: smallest allowed set of operations/files/modules; distinguish existing from proposed paths.
4. **Out of scope**: unrelated endpoints/migrations, new product policy, broad refactors, unapproved integrations.
5. **Implementation constraints**: links to established transactional, tenancy, idempotency and integration rules.
6. **Acceptance / test plan**: realistic, measurable cases with negative, race, failure and authorization scenarios; no fabricated pass records.
7. **Definition of done / evidence**: what the implementer must run and where to record actual results.

## State semantics

- `proposed`: design for discussion; cannot be treated as implementation authorization.
- `ready`: authorized, executable, and prerequisite tasks satisfied.
- `in_progress`: actual work underway, recorded by the implementer.
- `blocked`: cannot advance; state reason in `readiness`.
- `completed`: implemented and independently verifiable evidence exists.

Task dependency references go to task `id`, not file basename. Maintain task state in frontmatter only; a task index should link to files, not duplicate status columns manually.
