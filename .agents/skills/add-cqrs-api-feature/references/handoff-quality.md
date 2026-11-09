# TASK handoff & completion quality

## Planner-to-executor contract

- Task source: `docs/features/<feature>/tasks/TASK-*.md`.
- Stable source links: `us_refs`, `fr_refs`, `nfr_refs`; `depends_on` contains Task IDs. Re-read referenced files and actual project rules.
- `implementation_skill` is an optional **routing hint**, not authorization. If missing, choose implementation workflow from user intent and verified task type; if mismatched, stop and report.
- `codebase_verified` records planning-time inspection, not current truth. Executor performs fresh inspection.
- `status: pending` is eligible for `start-task` (which runs the preflight); `blocked`/`cancelled` are not. `in_progress` may be resumed only by an authorized actor and after rechecking context (`check-task --resume`). Status after that changes only through `submit-task`, `return-task` and `complete-task`.
- Check-task automation cannot prove client approval, correct content, or current runtime behavior. Confirm those semantically.

## Readiness checklist

1. Concrete expected behavior and scope; source requirement status ready/approved, with the actual project approval policy satisfied.
2. No unresolved decision controlling authorization, tenant scope, input/output contract, persistence invariants or external integration.
3. Dependency tasks are done (or genuinely unnecessary because requirements changed—send back for replan).
4. Task design matches real module, current code and registered dependencies; no stale DevFlow paths.
5. Tests have verifiable assertions for acceptance criteria, errors, tenancy and applicable failure/concurrency cases.

## Completion report

Return: `Task ID`, `source UR/FR/US/NFR`, `actual files changed`, `API method/route if any`, `tests executed + results`, `conditions not tested`, `new risks/decisions`, `task status`. State explicitly when no code was changed due to a gate. Do not claim formal business approval or completion of an entire feature from one task.

## Existing unit/integration tests

Inspect root `tests/` and current solution test references before writing application tests. Preserve and extend existing .NET test projects and fixtures; never reorganize root tests to accommodate the documentation-validator suite, which is isolated under `tools/docs_validation/`.
