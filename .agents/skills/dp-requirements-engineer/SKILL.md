---
name: dp-requirements-engineer
description: Turn DeliveryPilot product discovery and feature proposals into traceable functional requirements, business rules, use cases, user stories, and Given/When/Then acceptance criteria. Use when authoring or reviewing feature requirements from research; not for architecture decisions, coding, or directly approving AI-proposed product policy.
---

# DeliveryPilot requirements engineering

Produce a **business-facing, testable specification** grounded in existing intent and evidence. Treat AI research as a proposed input, never as accepted product authority.

## Intake and scope

1. Identify feature ID (e.g., `FEAT-DP-001`), actor, decision owner, product goal, and whether the user requests requirements only or also stories/use cases. If no feature ID exists, inspect `docs/features/` and assign a collision-free candidate ID without rewriting other IDs.
2. Read applicable `AGENTS.md`, root README, `docs/README.md`, `docs/WORKFLOW.md`, `docs/governance/documentation-policy.md`, and product vision/scope/personas/business rules/NFRs. Read the feature's `discovery/{proposal,research,assumptions}.md`, existing `requirements.md`, stories and use cases; inspect code/contracts only to identify existing behavior, not to redefine business intent.
3. Use `docs/templates/feature-requirements.md`, `docs/templates/user-story.md`, and `docs/templates/use-case.md` if present. Reuse adjacent `*-prompt.md` structures when they match current evidence. Edit *only* user-requested feature documents; preserve human corrections and approved contracts.

## Transform discovery into specification

1. Separate user-provided goals, verified observations, proposed hypotheses and unresolved product decisions. Link to the discovery owner rather than pasting the research into the specification.
2. Define scope, actors and permissions. Draft observable, individually identifiable `FR-...` requirements (IDs stable within the feature). Include rationale/provenance and proposed/accepted status of each consequential policy.
3. State business invariants, ownership, tenant boundaries, visibility/access rules, state transitions, notifications or AI intervention limits **only** when grounded or explicitly marked proposed. Do not invent routes, database tables, DTOs, model choices or SLO numbers.
4. Write user stories in `stories/US-...md` with actor, need, business value and linked FR IDs. Write use cases in `use-cases/UC-...md` with trigger, preconditions, main flow, alternatives, exceptions and postconditions. Avoid mirroring every FR into a redundant story; group by real user outcome.
5. For each FR, provide observable acceptance scenarios tied back to its ID. Include happy path, invalid inputs, missing data, authorization/tenant negative paths, duplicate/concurrent actions, data freshness and explainability where relevant. Write Given/When/Then without assuming implementation details.
6. Separate NFR references, business decisions pending owner review, out-of-scope work, and dependencies on integrations/permissions. If unclear, mark `proposed` and list validation/decision owner; never manufacture approval evidence.
7. Check cross-references (FR <-> stories/use cases/scenarios), duplicate IDs and contradictions with approved scope. Run `python .agents/skills/dp-requirements-engineer/scripts/check_requirements.py docs/features/<feature-id>/requirements.md` where Python is available. For incomplete drafts, report unmet validation findings instead of hiding them or changing approvals.

## Handoff and limits

Provide updated paths, a traceability summary, missing acceptance cases, open owner decisions, and validator results. Requirements are **not ready for production planning** until accepted with real `approval_ref` where the repo uses that field. Do not change implementation files, task completion state, or system architecture. If an approved requirement conflicts with the source code, flag drift; do not silently prefer either side.

Read [references/traceability-and-acceptance.md](references/traceability-and-acceptance.md) for the expected testability bar; read [references/scope-and-decisions.md](references/scope-and-decisions.md) for authority boundaries when authorization, tenant access, or AI actions are involved.
