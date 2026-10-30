---
name: dp-feature-engineer
description: "DeliveryPilot feature discovery and requirements engineering. Use when creating or revising roadmap features, drafting one-file-per-user-requirement (UR), functional requirement (FR), or user story (US), reviewing requirement completeness, or analyzing downstream change impact. Resolves relevant product/architecture context and generates traceable Markdown drafts. Not for writing application code or generating coding-ready implementation tasks."
---
# DeliveryPilot Feature Engineer

Create or revise evidence-based, traceable **feature/UR/FR/US** Markdown artifacts for DeliveryPilot. Treat product documents as the source of truth, not this skill's memory. Generated output is always **draft** unless an authorized approval is externally verified; never claim approval because the agent reviewed its own work.

## Route operation

- `discover FEAT-DP-XXX`: investigate actors, problem, alternatives, constraints, source evidence and unknowns; report proposed decomposition without creating downstream artifacts unless requested.
- `create FEAT-DP-XXX`: create missing feature folder and individual UR/FR/US drafts; may be done in **one pass** with a concise review summary.
- `refine <artifact ID>`: update only requested draft content, preserve ID and linked evidence. For approved/baselined artifacts, propose a new version/change-request path; do not silently rewrite them.
- `validate FEAT-DP-XXX`: run structural validator and semantic review.
- `impact <artifact ID>`: traverse all downstream links, identify affected FRs, Stories, Tasks and assumptions; propose changes, do not overwrite anything automatically.

## Context discovery (progressive, economical)

1. Find repository root by locating `AGENTS.md` and `docs/context-map.md`. Read root rules first.
2. Read `docs/context-map.md`, then **only relevant sections** of `docs/product.md`, `docs/requirements.md`, `docs/architecture.md`; search `docs/roadmap.md` for the selected ID (or propose a registry entry for a new feature). Expand if security/cross-feature integrity requires more context.
3. Inspect related `feature.md` summaries and *only* referenced UR/FR/US artifacts; inspect code only for claims that require present implementation facts, not to make UR/FR technical.
4. Distinguish agreed direction, research-supported observations, proposals and unresolved assumptions. Do not invent interviews, client decisions or authorizations.

## Generation contract

1. Resolve feature scope, actors, business problem, outcome, in/out of scope and cross-feature overlap. Ask only for a **blocking business decision**; otherwise capture questions in draft.
2. If folder absent and feature is in roadmap, run `python tools/docs.py scaffold-feature --id FEAT-DP-XXX --title "..." --slug ... --release MVP --priority P0`; never overwrite existing feature. Complete `feature.md` from real context.
3. Read **canonical** `docs/templates/{ur,fr,us}.md` and `docs/authoring.md`; do not keep duplicate templates in skill. Generate each artifact with `python tools/docs.py new-artifact --type UR --feature FEAT-DP-XXX --title "..." --actor "..."`, then fill its contents. Repeat for FR/US in source order. Inspect all existing artifacts before allocating new IDs; reuse/refine instead of duplicating. Do not produce a fixed quota.
4. Each UR states user problem/outcome without premature implementation design. Each FR expresses observable system behavior with source `ur_refs`; FRs justified directly by security/BR/NFR need explicit rationale. Each US captures a valuable testable increment and `fr_refs`, including conditional negative/security/edge scenarios.
5. Keep cross-references **one direction only** (FR→UR, US→FR). Link IDs, not guessed filenames. Leave `approval_ref: null` and `status: draft` unless actual evidence exists.
6. Apply semantic quality checks from [references/feature-quality.md](references/feature-quality.md). Run `python tools/docs.py validate --feature FEAT-DP-XXX` and `python tools/docs.py coverage --feature FEAT-DP-XXX` from repository root. Correct structural errors.
7. Report files created/edited, UR→FR→US coverage, duplicate candidates, unresolved decisions, impacted future work and next safe action. Do not create Tasks or implement code in this skill.

## Change safety

- On `create` rerun, inspect and update **only** requested or demonstrably incomplete drafts. Never delete existing approved/reviewed artifacts or generate a second copy with a new ID.
- On impact analysis, use `fr_refs` and `ur_refs` (then task `us_refs`/`fr_refs`) to derive reverse dependents; do not hand-maintain reverse links.
- An open requirement can be drafted while product discovery continues. **Ready** is internal quality review, **approved** requires decision evidence. User stories need client approval only when the policy says so.
- Verify tenant/project visibility and client collaboration constraints wherever relevant.

## Example invocations

```text
$dp-feature-engineer create FEAT-DP-007
Use roadmap context, review Approval Workflow, generate separate draft UR/FR/US files and report coverage. Do not create coding tasks.

$dp-feature-engineer impact UR-DP-003-001
Identify downstream contracts and propose non-destructive revisions.
```
