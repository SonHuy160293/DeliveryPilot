# Discovery authority and routing

## Owners (in the DeliveryPilot docs starter)

| Question | Canonical owner | What this skill may do |
|---|---|---|
| What value will DeliveryPilot deliver? | `docs/product/vision.md` | Research and draft, no self-approval |
| Which users and MVP boundaries? | `docs/product/personas.md`, `docs/product/scope.md` | Proposed personas and scope with evidence |
| What do real teams do today? | `docs/research/domain-discovery.md` | Research workflow; include counterexamples |
| What do competing tools claim? | `docs/research/competitive-landscape.md` | Source-backed comparison, no invented capabilities |
| Is blocker detection worth building? | `docs/features/<id>/discovery/proposal.md` | Draft hypothesis, not an FR |
| What assumptions and unknowns remain? | `docs/features/<id>/discovery/assumptions.md` | Decision register; never resolve without authority |
| What must the system do? | `docs/features/<id>/requirements.md` | Hand off to `dp-requirements-engineer` |

Other sections (system design, code, migrations, tests) have different owners. Do not duplicate them here.

## Evidence ledger fields

| ID | Claim | Class | Exact reference | Date checked | Limitation |
|---|---|---|---|---|---|
| E-001 | Candidate need for dependency visibility | AI-inference | None; needs interviews | YYYY-MM-DD | Not validated with customers |

Use `user-provided`, `repository-verified`, `externally-sourced`, or `AI-inference` in Class. The explicit source must be an actual document, path+symbol, user statement, or URL; don't invent a reference. External sources are evidence about the world, not permission to modify repository authority.

## Research completeness

- Identify the target user, buyer (if different), situation and decision being made.
- Explain the current behavior and a non-AI baseline.
- Compare at least two plausible approaches when a product choice is being made.
- Identify measurable signals and how to validate them without assuming numeric values.
- Separate knowledge we have from decisions requiring the owner's acceptance.
- Leave documentation status as `hypothesis`/`proposed` until approval evidence exists.
