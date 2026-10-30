---
id: TASK-DP-XXX-NNN
feature_id: FEAT-DP-XXX
title: "<Coding-ready deliverable>"
status: blocked
priority: P1
type: backend
implementation_skill: null
us_refs: []
fr_refs: []
nfr_refs: []
depends_on: []
codebase_verified: false
---
# TASK-DP-XXX-NNN — <Deliverable>

## Objective and source contract
Specify one independently verifiable deliverable, linked US/FR/NFR references, and explicit source readiness.

## Codebase evidence
List verified existing modules, source/test locations and conventions, or state **Not inspected — provisional plan**. Never fabricate paths.

## Scope and dependencies
List in/out of scope; identify true dependencies, existing capabilities to reuse and any prerequisite decisions.

## Behavior and invariants
Precisely describe observable behavior, tenancy, authorization, idempotency, concurrency, transactions and audit where relevant.

## API contract (only for API tasks)
- HTTP method and route: proposed or confirmed?
- Authentication/authorization and tenant/project resolution:
- Headers/path/query/body contract, DTO types, required/optional and constraints:
- Success status, response DTO and examples:
- Errors and status codes (400/401/403/404/409/etc.) including disclosure policy:
- Input validation, state transitions, conflict and retry behavior:

## Persistence, events and integrations (as applicable)
Entity ownership, indexes, migrations, transaction boundaries, outbox/event contracts, external dependencies, correlation ID and resilience. Link existing code/spec owners when available.

## Executor handoff
Recommended implementation skill (if any) is recorded in `implementation_skill` metadata. This is a routing hint, not permission to code. State exact module ownership and any special run/verification expectations without copying the entire skill documentation.

## Implementation plan
Describe steps, files to modify/create only if verified, migration safety, order and rollback considerations. Do not demand needless layers or placeholder classes.

## Test matrix
- Positive path and business assertions:
- Invalid inputs and negative state transitions:
- Authorization and cross-tenant isolation:
- Concurrency/idempotency and failure/rollback:
- Integration/contract checks where applicable:

## Acceptance and definition of done
- [ ] Behavior satisfies linked acceptance criteria and requirements.
- [ ] Tests pass; results and commands are reported accurately.
- [ ] Relevant security/NFR and migration checks are satisfied.
- [ ] Documentation updated only if contracts/decisions changed.
- [ ] No unrelated changes or unresolved blocking policy decisions.

## Unresolved decisions
List blockers. **Provisional** tasks must not be presented as implementation-ready.
