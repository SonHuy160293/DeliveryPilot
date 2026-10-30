# Decomposition review for DeliveryPilot

## Choosing task boundaries

Prefer independently useful vertical slices and explicit checkpoints. Example for blocker detection (illustrative):

- `TASK-DP-001`: expose trustworthy, tenant-scoped dependency facts (if an existing Work Management owner supports them).
- `TASK-DP-002`: evaluate candidate blockers against normalized evidence; depend on 001 if data interface is required.
- `TASK-DP-003`: end-to-end integration verification; depend on 001 and 002 if a full deployed path is needed.

The concrete scope and source availability must be checked in *this* repository. Do not assume those components exist simply because they appear in a template.

## Backend / database / AI review before a task can be ready

| Domain | Questions that affect task split |
|---|---|
| Boundary | Which module owns state and transactions? Is communication synchronous or via a published event? |
| Persistence | Is a unique constraint required? Will this add a migration or demand data backfill? |
| Concurrency | Is the operation idempotent? What happens with duplicate messages and concurrent requests? |
| Failure | Are outbox/inbox, retries, compensation, timeouts or partial failure relevant? |
| Authorization | Is tenant/project authorization enforced at *every* data access path? |
| AI | Which signal is deterministic? What is model inference? How are provenance, freshness, privacy and fallbacks evaluated? |
| Testability | Is there a targeted isolated integration test for real DB/queue boundaries, not only mocked tests? |
| Parallelism | Do tasks touch the same migration, shared contract or high-contention file? If so, serialize or coordinate. |

## Avoid these patterns

- A single giant task with requirements + design + entire implementation + verification history.
- "Create service" tasks that contain no measurable outcome.
- Marking tasks ready because model confidence is high.
- Inventing API paths or EF entities absent from the repo; clearly mark new paths as proposals.
- Requiring independent tasks to change the same shared interface in parallel without coordination.
- Restating entire acceptance requirements inside each task; link using FR IDs and document paths.
