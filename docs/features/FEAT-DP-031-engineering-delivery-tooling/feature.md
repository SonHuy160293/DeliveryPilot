---
id: FEAT-DP-031
title: "Engineering Delivery Tooling"
status: ready
priority: P1
release: "Enabler"
depends_on: []
---
# FEAT-DP-031 — Engineering Delivery Tooling

> **Status:** Internal engineering enabler registered on 2026-11-10 from the technical lead's process review. It is **not** a product capability and has no URs, FRs or user stories. Its tasks reference NFR-011 (maintainability) and NFR-004 (testable data integrity) only. `ready` means the scope below is complete enough to plan. It is not client approval.

## Problem and business outcome
Repository TASKs are implemented through a loop that is mostly manual. Evidence from 2026-11-10:

- **Statuses are skipped.** None of the 59 TASK files has ever been `in_progress` or `in_review`. Every finished task went straight from `pending` to `done`. The status, the "Execution evidence" section and the DoD checkboxes are all edited by hand. `check-task --resume` exists but nothing ever sets `in_progress`.
- **No record of what the plan was based on.** `codebase_verified: true` doesn't say which commit was inspected. The preflight doesn't look at `## Unresolved decisions`.
- **Test results can look green when the tests didn't run.** PostgreSQL-backed tests skip silently when their variable is unset. Example: the TASK-DP-002-005 re-run reported "112 passed and 126 skipped". Each module uses a different variable (`DELIVERYPILOT_TEST_POSTGRES`, `DELIVERYPILOT_TEST_PROJECTS_POSTGRES`, `DELIVERYPILOT_DELIVERY_TEST_*`), so each executor works out the commands again. Running `scripts/Test-Architecture.ps1` through a nested Windows PowerShell hit the execution policy (TASK-DP-025-001). There is no CI.

**Intended outcome:**
- `tools/docs.py` moves a task through `pending → in_progress → in_review → done` with checked preconditions.
- One script verifies a module and fails when required database tests were skipped.
- Evidence is written by the tools, not typed by hand.

## Actors and scope
| Actor | Role |
|---|---|
| Implementer (human or agent) | Starts, verifies, submits and completes a task |
| Technical lead / reviewer | Reviews `in_review` tasks; owns this tooling |
| Task planner (`dp-task-planner`) | Unchanged in this feature. It keeps producing `pending`/`blocked` tasks |

**In scope:**
- Lifecycle commands in `tools/docs.py` (TASK-DP-031-001).
- `scripts/Verify-Module.ps1` (TASK-DP-031-002).
- Lifecycle-recorded verification evidence (TASK-DP-031-003).
- The template and authoring-policy changes these need.

**Out of scope** (later workstreams of the same review):
- Consolidating skill and convention text into one source.
- New executor skills (migrations, Gateway publication, workers).
- The checklist of docs to update after a code change.
- A code-review gate inside `in_review`.
- Branch and commit conventions.
- CI workflows.
- Changing existing `done` tasks, or any product behavior.

## Feature policy and constraints
- NFR-011: tooling must not change module contracts, test discovery or .NET test projects.
- [Authoring policy](../../authoring.md#status-and-approvals): statuses, `approval_ref` and "structural readiness is not approval" stay as they are. The tools never change UR/FR/US status.
- AGENTS.md test ownership: Python tests stay under `tools/docs_validation/`, and root `tests/` stays .NET-only.
- `tools/docs.py` stays dependency-free (standard library only).

## Key journeys and edge cases
1. `start-task` → implement → `verify-task` → `submit-task` → review → `complete-task`.
2. An attempt to start a blocked task, a task with unfinished dependencies, or a task with an open decision is refused, and the file is not touched.
3. When the database variable is unset, a required database run fails instead of passing on skipped tests.
4. Existing `done`/`pending` tasks without the new optional fields keep validating.

## Artifact index
Tasks: [TASK-DP-031-001](tasks/TASK-DP-031-001.md), [TASK-DP-031-002](tasks/TASK-DP-031-002.md), [TASK-DP-031-003](tasks/TASK-DP-031-003.md). Coverage: `python tools/docs.py coverage --feature FEAT-DP-031`.

## Dependencies and decisions
- **Decided (technical lead, 2026-11-10):**
  - Register this as an internal enabler feature rather than attaching the tasks to a product feature.
  - Keep separate database variables per module. The Projects test database must stay isolated because Identity and Projects both own a `reliability` schema (`tests/DeliveryPilot.Projects.Tests/Support/PostgreSqlFactAttribute.cs`). The script maps them instead of merging them.
- **Order:** 001 and 002 are independent; 003 depends on both.

## Evidence and review
- Process review, 2026-11-10 (Claude Code session, plan "Tighten the TASK → code → done process").
- Inspected source: `tools/docs.py`, `tools/docs_validation/test_docs.py`, `scripts/Test-Architecture.ps1`, test projects under `tests/`.
