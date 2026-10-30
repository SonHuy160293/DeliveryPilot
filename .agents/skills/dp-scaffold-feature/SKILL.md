---
name: dp-scaffold-feature
description: Scaffold a new DeliveryPilot feature folder under docs/features/FEAT-DP-XXX-<slug> with validator-compliant templates, collision-free IDs, and feature registry links. Use when starting or initializing a new feature before discovery, requirements, or task planning.
---

# DeliveryPilot feature scaffolder

Initialize a new feature workspace under `docs/features/<feature-id>-<slug>/` that is immediately navigable, traceable, and compliant with repository documentation gates.

## Intake

1. Determine the **slug** (kebab-case capability name, e.g., `organization-registration`), **title**, **owning module** (`identity`, `notification`, `work-management`, `delivery-intelligence`, or `integrations`), and **seed idea**.
2. Inspect `docs/features/` to assign the next collision-free `FEAT-DP-XXX` ID (e.g., `FEAT-DP-002`) unless the user specified an unused ID. Derive scoped child prefixes from the feature number (`FR-DP-XXX-01`, `US-DP-XXX-01`, `UC-DP-XXX-01`, `TASK-DP-XXX-01`) so IDs never collide across features.
3. Inspect `src/Modules/AGENTS.md` to confirm whether the owning module is repository-verified (`DeliveryPilot.Identity`, `DeliveryPilot.Notification`) or proposed.

## Scaffold execution

1. Run the scaffolding script from the repository root:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .agents/skills/dp-scaffold-feature/scripts/New-FeatureScaffold.ps1 `
     -Slug "<slug>" `
     -Title "<title>" `
     -Module "<module>" `
     -SeedIdea "<seed-idea>"
   ```
   Pass `-FeatureId "FEAT-DP-XXX"` if a specific ID was requested, or `-Minimal` to omit `design/{api-design,data-impact,sequence}.md` when the feature does not need separate API/DB/sequence design files.
2. Verify that `docs/features/README.md` includes a link to the newly created `<feature-id>-<slug>/README.md` under `## Feature navigation`.
3. Tailor the initial seed context in `discovery/proposal.md`, `discovery/assumptions.md`, and `requirements.md` using only user-provided intent and repository-verified facts. Keep `discovery/*` at `status: "hypothesis"`, `requirements.md`/`design/*`/`tasks/*` at `status: "proposed"`, `evidence/*` at `status: "not_run"`, and all `approval_ref: null`.

## Verification gate

Run the feature documentation validator before finishing:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/dp-scaffold-feature/scripts/Test-FeatureDocs.ps1 `
  -FeatureDir "docs/features/<feature-id>-<slug>"
```

If Python is installed, also run:
- `python .agents/skills/dp-product-discovery/scripts/check_discovery.py docs/features/<feature-id>-<slug>/discovery/*.md`
- `python .agents/skills/dp-requirements-engineer/scripts/check_requirements.py docs/features/<feature-id>-<slug>/requirements.md --strict`
- `python .agents/skills/dp-feature-planner/scripts/check_task_graph.py docs/features/<feature-id>-<slug>`

Completion requires **0 errors and 0 warnings** across link, discovery, strict requirements, and task graph checks.

## Next-step routing

After scaffolding passes verification, hand off in order:
1. **`dp-product-discovery`** to flesh out `discovery/{proposal,research,assumptions}.md`.
2. **`dp-requirements-engineer`** to specify `requirements.md`, `stories/US-*.md`, and `use-cases/UC-*.md` for Product Owner review (Gate 1).
3. **`dp-feature-planner`** to decompose `tasks/TASK-*.md` once requirements and `design/solution.md` are reviewed (Gate 2).
