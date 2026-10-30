---
name: dp-feature-planner
description: Break a DeliveryPilot feature's reviewed requirements and technical design into small, dependency-aware Codex implementation task Markdown files with bounded scope, test plans, and readiness gates. Use when creating or refining docs/features/*/tasks; not for coding, self-approving proposals, or declaring test results.
---

# DeliveryPilot feature-to-task planner

Create **execution-ready task specifications when authorized** or clearly blocked/proposed tasks when upstream requirements or design are not approved. A task is not a replacement for its feature requirements or architecture design.

## Intake and evidence

1. Identify feature ID and the requested implementation milestone. Read applicable root/parent `AGENTS.md`, root README, `docs/README.md`, `docs/WORKFLOW.md`, `docs/governance/{definition-of-ready,documentation-policy}.md`, the feature's `requirements.md`, user stories/use cases, `design/`, `tasks/README.md`, and any existing tasks. Read relevant module boundaries, tenancy policy, source code, tests, generated API contracts and EF Core migrations to establish **actual** extension points.
2. Inspect `docs/templates/agent-task.md` and the target's adjacent `*-prompt.md` for local conventions; prefer existing task naming/metadata instead of inventing a second backlog.
3. Determine authority. For production, check that consequential requirements and solution design are `accepted` with actual approval references. A user can explicitly authorize an isolated prototype; record that scope/authority separately and retain its limitations. Otherwise generate **proposed** task drafts only and state what blocks readiness.

## Decomposition procedure

1. Map accepted or proposed FR IDs and acceptance scenarios to implementation outcomes. Identify cross-module dependencies (Identity/Organization, Work Management, Delivery Intelligence, integrations) without assigning data ownership twice.
2. Decompose into small vertical outcomes that can be validated independently: typically a bounded data/integration prerequisite, a behavior slice, and feature-level verification. Avoid splitting every class into separate tasks or bundling an entire subsystem into one task. Explicitly allow true dependencies; false parallelism creates race conditions.
3. For each task, define unique stable `TASK-...` ID, `feature_id`, target module, priority, `depends_on` IDs, `status`, `approval_ref`, `delivery_mode`, and `readiness`. Reference canonical requirement and design files instead of copying rules. Identify confirmed source paths or mark proposed locations distinctly; never claim an uninspected API/class/DB table exists.
4. Specify **one outcome**, read-first sources, preconditions, permitted file areas, forbidden scope, interactions/interfaces to preserve, tests including negative/security/concurrency cases, acceptance signals, and what to put in the later verification record. Include migration impact, rollback/retry concerns and tenant boundaries when relevant.
5. Build an acyclic dependency graph with no unresolved task IDs; dependencies must reflect real prerequisite delivery. Flag shared migrations/contracts/files across parallel tasks and require serial or coordinated ownership.
6. Write only `docs/features/<feature-id>/tasks/*.md` and its existing `README.md` if requested. Do not modify implementation, architectural authority, approvals, historical verification reports or task status on behalf of an implementer.
7. Run `python .agents/skills/dp-feature-planner/scripts/check_task_graph.py docs/features/<feature-id>` where Python is available. Report failures as blockers; never artificially set approval/verification data to make checks pass.

## Definition of ready

Mark `status: ready` only when: accepted production feature contract and relevant design have independently verifiable approval references (or explicit prototype authorization), security/tenant policy is resolved, referenced paths and preconditions are real, tests and environments are executable or explicitly bounded, and prerequisites are completed or not needed. `completed` additionally requires actual verification evidence from implementation; *this skill never declares a task completed*.

## Output contract

Return a task summary with IDs, owners/modules, outcome, `depends_on`, candidate execution order, parallelizable vs conflicting work, readiness blockers, exact files changed, and validation results. If requirements/design remain proposed, provide a **planning draft**, not a fake ready backlog.

Read [references/task-contract.md](references/task-contract.md) for precise file shape and [references/decomposition-and-review.md](references/decomposition-and-review.md) for design hazards and test cases. Never place secrets or personal records in tasks, plans or example payloads.
