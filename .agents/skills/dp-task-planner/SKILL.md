---
name: dp-task-planner
description: "DeliveryPilot codebase-aware implementation task planner. Use when converting an existing User Story, Functional Requirement, NFR, or entire feature into separate coding-ready TASK Markdown files covering API methods/routes, request/response contracts, validations, security, database changes, messaging, errors and tests where applicable. Inspects the real repository, respects requirement readiness, checks task dependencies and does not implement code."
---
# DeliveryPilot Task Planner

Produce **one implementation deliverable per Task Markdown file**, with verified codebase context, explicit source references and independently checkable acceptance. This skill plans; another coding agent implements. Do not manufacture current API paths, classes, database mappings or proof of tests.

## Route operation

- `plan-story US-DP-XXX-NNN`: find linked FR/UR and generate coherent task(s).
- `plan-feature FEAT-DP-XXX`: plan only **ready** stories/requirements, or generate explicitly provisional tasks when asked; include technical/NFR work if justified.
- `plan-technical FEAT-DP-XXX <outcome>`: plan infrastructure, security, migration, observability, or NFR work **without** inventing fake user stories; reference source FR/NFR.
- `review-task TASK-DP-XXX-NNN`: evaluate coding readiness against actual code.
- `validate FEAT-DP-XXX`: run repository validator and semantic completeness review.

## Inspect in the correct order

1. Find repo root; read root `AGENTS.md`, `docs/context-map.md`, `docs/authoring.md` and relevant sections of `docs/architecture.md`/`docs/requirements.md`.
2. Locate selected artifact under `docs/features/...`; read referenced UR/FR/NFR, acceptance criteria, relevant `feature.md`, dependencies and decisions. **Check their statuses and approval policy.** If draft, clearly flag provisional and do not present tasks as ready for execution.
3. Inspect actual source tree, module structure, existing controllers/contracts/validators, migrations/DbContexts, test conventions, build scripts and any applicable repository instructions. Use the minimum file scope needed; search by symbols first. Distinguish repository evidence from proposed design. If code unavailable, block readiness and do not invent paths.
4. Check prior TASK docs and existing implementations: don't duplicate tasks or overwrite another agent's review. Identify actual dependencies and reuse opportunities.

## Resolve implementation skill (conditional, read-only)

- **Backend CQRS/API task:** inspect `.agents/skills/add-cqrs-api-feature/SKILL.md` and, only if relevant, its `references/cqrs-conventions.md` to understand established execution boundaries. Use these as *planning constraints*; do not execute that skill or duplicate its full coding rules into every Task.
- **Test projects:** For application tasks, inspect existing root `tests/` unit/integration projects, their namespaces, fixtures, and test commands. Plan tests in the appropriate established project, not a new conflicting test tree. Keep the Python docs-validator suite in `tools/docs_validation/`.
- **Other task types:** select a matching installed repository skill only if it really exists and matches verified task scope. Otherwise set `implementation_skill: null` and hand off to a general implementation agent; never manufacture a skill name or create extra skills without a request.
- The chosen implementation skill is a **routing hint**, not permission to implement, proof of codebase inspection, or approval. Record it in optional `implementation_skill` metadata. Check source/architecture truth separately; legacy examples inside other skills must not override current code or `ARCHITECTURE.md`.
- If a source Task already exists with an implementation skill, preserve its selection unless evidence shows mismatch; record any changed recommendation, and do not overwrite an approved or in-progress Task.

## Create tasks

1. Read canonical `docs/templates/task.md`. Use `python tools/docs.py new-artifact --type TASK --feature FEAT-DP-XXX --title "..."` and fully populate the file. Tasks use `us_refs`, `fr_refs` and/or `nfr_refs`; at least one is required.
2. Describe a coherent testable outcome, not separate bookkeeping tasks for every DTO/interface/class. Split when security/transaction/owner boundaries or meaningful execution dependencies justify it.
3. For API tasks define route/method, auth/tenant context, headers/path/query/body, DTO fields and required/optional rules, validation, success/error codes, conflict disclosure, response examples, idempotency/concurrency, transactions and tests **when supported by actual requirements/code**. Any proposed contract must be clearly labeled until verified.
4. For non-API tasks replace API fields with appropriate migration, worker, UI, deployment or operational contracts; avoid boilerplate irrelevance. Include applicable NFRs, security, audit, observability, failure and rollback paths.
5. List `depends_on` by stable Task IDs; distinguish genuine prerequisites from tasks that can run independently. Never assert independence if a transaction/contract requires coordination.
6. Set `codebase_verified: true` only after verifying referenced code paths and affected conventions. `status: blocked` when critical decisions are unresolved, source requirement review policy is unsatisfied, or code unavailable; `status: pending` only for an implementable plan, not to imply requirements approval. Record the selected `implementation_skill` only if its SKILL.md exists and fits the task.
7. Apply [references/task-quality.md](references/task-quality.md), run `python tools/docs.py validate --feature FEAT-DP-XXX`, `python tools/docs.py coverage --feature FEAT-DP-XXX`. For an eligible task also run `python tools/docs.py check-task --id TASK-DP-XXX-NNN` to check structural execution readiness; explain semantic or approval gaps separately. Report tasks, dependency order, blockers, acceptance/test gates and suggested execution skill.

## Safe handoff and change handling

- Do not modify application source, run migrations or claim implementation completion.
- Current source/tests overrule outdated narrative about what is already implemented; business decisions in approved requirements overrule inferred implementation preferences. Report contradictions instead of hiding them.
- A task is not an authorization to change scope or approved contract. If new requirement discovered, route to feature engineering and record impact.
- Never set `approved` for UR/FR/US; never call a task verified just because its Markdown looks complete.
- **Handoff contract:** give the executor the TASK ID, not a pasted copy of every source artifact. The executor reopens referenced requirements, runs readiness checks, inspects actual code afresh and decides whether implementation can proceed. The planner must never pre-mark a Task done.

## Example invocations

```text
$dp-task-planner plan-story US-DP-003-001
Inspect existing Identity module and generate tasks for organization creation including request/response, validation, authorization, data integrity and tests. Do not implement code.

$dp-task-planner plan-technical FEAT-DP-003 Verify tenant isolation across background jobs
Reference NFR-001, inspect current code, and create a verifiable technical task without inventing a US.
```
