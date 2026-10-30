---
name: add-cqrs-api-feature
description: Implement or refactor DeliveryPilot .NET CQRS commands, queries, controllers, workers, or consumers from a TASK-DP-XXX-NNN coding task. Use for task-first backend/API implementation, including DTOs, validation, authorization, persistence, transactions, errors and tests. Verify the real repository and task readiness before edits; preserve DeliveryPilot's custom CQRS conventions and requirement traceability.
---

# DeliveryPilot CQRS Implementation Executor

Implement code; **do not** write a second plan or silently change business requirements. `dp-feature-engineer` creates UR/FR/US; `dp-task-planner` creates TASK files; this skill executes an eligible TASK, then verifies behavior. Read [references/cqrs-conventions.md](references/cqrs-conventions.md) only for CQRS/API work. Read [references/handoff-quality.md](references/handoff-quality.md) when assessing readiness, blocking, or finishing a task.

## 1. Resolve the task and authority

1. If the request names `TASK-DP-XXX-NNN`, locate exactly one `docs/features/FEAT-DP-XXX-*/tasks/TASK-DP-XXX-NNN.md`. Never infer a task's content from its ID. If the request is ad-hoc without a task, follow the explicit user request, but identify missing traceability; do not invent approvals.
2. Read repository `AGENTS.md`, any applicable nested `AGENTS.md`, `ARCHITECTURE.md`, the task, `docs/authoring.md`, relevant `docs/context-map.md` sections, and the task's referenced UR/FR/US/NFR. Load only relevant documents and sections; do not read the entire product repository by default.
3. Check `status`, `codebase_verified`, `depends_on`, `implementation_skill`, unresolved decisions, acceptance criteria and required approval policy. If `implementation_skill` is set to another skill, report a routing mismatch and stop unless the user explicitly authorizes a compatible handoff.
4. Run `python tools/docs.py check-task --id TASK-DP-XXX-NNN` **if the current checkout supports it**. For an authorized continuation of `in_progress`, use `--resume` (never silently start another agent's work). Treat this as a structural gate, not proof of client approval or current code correctness.
5. **Stop source edits** if task is `blocked`, `cancelled`, incomplete/uncleared dependencies exist, source requirements are draft/not ready, approval policy is unmet, or critical business/security/API contracts are unresolved. Report precise blockers and recommend `$dp-task-planner review-task <ID>`. Do not flip status or fabricate approval evidence to bypass the gate.

## 2. Verify the real implementation environment

1. Inspect `DeliveryPilot.slnx`, project references, module registrations, interfaces, API base controllers, exception handling, DbContexts/migrations, tests and the relevant source. Source owns **current behavior**; approved requirements own **intended behavior**. Report contradictions instead of choosing silently.
2. Check whether the task is still accurate. `codebase_verified: true` means the planner previously inspected the codebase, not that it remains current. If stale or incompatible, stop and request task revision before implementation.
3. Resolve the actual target module and module boundaries. Do not assume DevFlow paths, an unimplemented Tasks module, a database, a unit of work or an auth policy. Do not automatically restructure independently hosted Identity/Notification/Gateway into the proposed modular-monolith target.
4. Determine whether the requested capability already exists. Prefer a scoped modification or reuse over duplicate handlers/controllers or infrastructure.

## 3. Implement only the task contract

- Apply the local CQRS/API conventions in [references/cqrs-conventions.md](references/cqrs-conventions.md) after checking actual signatures. Preserve source UR/FR/US/NFR links in the task.
- Resolve API method/route, DTO fields, validation, success/error envelope, authorization, tenant isolation and any idempotency/concurrency expectations **from confirmed task requirements and current code**. Never guess unknown business rules.
- Keep source edits inside the smallest necessary module(s); add DB migrations, messages, workers or infrastructure only when the task and verified architecture require them.
- For scope change, conflicting requirements, new security policy or unverified contract: **stop and report**, do not update approved requirements or widen implementation scope.

## 4. Verification and handoff

1. Verify positive behavior, invalid inputs, returned failure Results, authorization and cross-tenant access, concurrency/rollback and integration contracts where applicable.
2. Build/test affected .NET projects and run tests using existing root `tests/` projects and their established solution references. Preserve .NET test names, projects, and fixtures; documentation-tool Python tests must live only in `tools/docs_validation/` and run via `python -m unittest discover -s tools/docs_validation -p "test_*.py" -v`. Run `powershell -File scripts/Test-Architecture.ps1` if it exists and architectural boundaries changed. If changing docs, run `python tools/docs.py validate`. Never claim unrun tests or unavailable environments passed.
3. Report changed files, route(s), requirement/AC coverage, tests and observed results, plus unmet conditions and any assumptions.
4. Set Task status to `done` **only when** all acceptance criteria/DoD checks are satisfied and local governance allows status updates; otherwise retain its status and report why. Never mark UR/FR/US `approved` or `implemented` merely because one task passed.

## Invocations

```text
$add-cqrs-api-feature Implement TASK-DP-003-001 after verifying readiness and actual DeliveryPilot source. Preserve business scope and run relevant tests.

$add-cqrs-api-feature Fix the specified CQRS handler using current code. Identify any missing task contract before changing requirements.
```
