# Coding-ready task quality gates

Load only for `plan-*`, `review-task` or `validate` operations.

## Evidence
- Required source UR/FR/US/NFR references exist; approved/ready policy is understood.
- Exact repository paths, API names, persistence patterns and build commands are verified, or explicitly labeled unknown/proposed.
- Duplicate implementation and related tasks have been checked.
- `codebase_verified` is false when code is unavailable or not inspected.

## Contracts
- API tasks: method/route, auth, tenant resolution, request DTO details, response status and DTO, validation, errors, examples.
- Data tasks: entity ownership, transaction boundaries, concurrency, migrations, rollback and security.
- Messaging tasks: delivery guarantee, idempotency, outbox, retries and correlation ID when relevant.
- Frontend tasks: loading/errors/accessibility/security and API contracts when relevant.
- DevOps tasks: deployment changes, secrets handling, configuration, rollback, operational verification.
- NFR tasks: measurable performance/security/availability/observability targets and verification.

## Task boundaries
- One independently verifiable outcome; avoid one-file one-task fragmentation.
- No invented independence; `depends_on` names real prerequisite TASK IDs.
- Failure modes, tenant isolation, information leakage, idempotency and concurrency considered when relevant.
- Tests cover positive, negative, authorization, state and integration paths as applicable. Reuse current .NET projects under root `tests/`; documentation-tool tests belong under `tools/docs_validation/`.
- Acceptance criteria are machine/human checkable, not only "code compiles".

## Readiness and report
- Blocking business/contract decisions unresolved => status `blocked`.
- Unsourced API decisions are proposed, never disguised as existing behavior.
- Plan must not claim tests ran or code was modified.
- Report task order, source coverage, what is provisional and what is ready.
- `python tools/docs.py validate --feature ...` succeeds.

## Executor handoff compatibility

- For CQRS/API implementation Tasks, read the available `add-cqrs-api-feature` skill as a **conditional** implementation-specific reference, not the source of business requirements.
- Set `implementation_skill: add-cqrs-api-feature` only when it exists and fits the target module/task; otherwise `null`. Never invoke the implementation skill while planning.
- Include task source links, actual inspected code evidence, explicit contract/validation/security/tenancy, meaningful tests, and true dependencies. Do not restate all executor implementation conventions in each TASK document.
- If Task is blocked, or a requirement remains draft or requires unresolved approval, document the blocker. `codebase_verified: true` alone does not make it implementable.
- The future executor must revalidate current code and ask for task revision on drift; it cannot silently change an approved contract.
