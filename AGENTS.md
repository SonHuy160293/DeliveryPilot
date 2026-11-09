# AGENTS.md — DeliveryPilot

Instructions in this file apply across the repository. A deeper `AGENTS.md` may narrow or supplement them for its subtree; read all applicable instructions before modifying code.

## Source of truth and context routing

Use the smallest sufficient context for the request:

- `README.md`: repository entry point; `ARCHITECTURE.md`: **current backend as documented**, including deployed module boundaries.
- `docs/context-map.md`: route to relevant product context without loading all documents.
- `docs/product.md`: product direction; `docs/requirements.md`: cross-feature business rules, approval principles, and NFRs; `docs/roadmap.md`: feature registry and priorities.
- `docs/architecture.md`: **proposed target architecture**, not proof of current implementation. Its modular-monolith proposal must not be applied over the current independently hosted modules without an explicit architecture decision.
- `docs/authoring.md`: artifact IDs, lifecycle, references, and governance; `docs/templates/`: canonical document formats.
- `docs/features/FEAT-DP-XXX-<slug>/`: feature-specific `feature.md`, `ur/`, `fr/`, `us/`, and `tasks/` documents.
- `src/Modules/AGENTS.md`: module-specific development conventions; the current source, tests, project files and migrations determine what is actually implemented.

**Authority rule:** Approved business requirements define intended behavior; accepted architecture decisions constrain design; source/tests/contracts establish implementation reality. Drafts, AI outputs, roadmap entries, and architecture proposals are **not** approved features or evidence that code exists. Report conflicts instead of silently resolving them.

## AI-assisted feature-to-code workflow

1. **Create/discover a feature:** Use `.agents/skills/dp-feature-engineer/SKILL.md`; resolve its roadmap ID, load only relevant product/requirements/architecture sections via `docs/context-map.md`, then create/refine **one Markdown file per UR, FR and US** using `docs/templates/`. Keep generated items in `draft` until reviewed.
2. **Refine/impact review:** Preserve existing stable IDs; check existing artifacts before adding new ones; derive downstream impact through references. Never overwrite approved/baselined content or invent approval evidence.
3. **Plan implementation:** Use `.agents/skills/dp-task-planner/SKILL.md`. Read source US/FR/NFR and their status; inspect the **real repository** (module boundaries, controllers, DTOs, validators, mappings, migrations and tests) before defining endpoints, classes or file paths. Generate one coherent testable `TASK-...md` per deliverable. Do not mark provisional tasks implementation-ready.
4. **Implement a task:** For CQRS/API tasks, use `.agents/skills/add-cqrs-api-feature/SKILL.md` when the task's `implementation_skill` matches. Treat `implementation_skill` as a routing hint, not authorization. Start it with `python tools/docs.py start-task --id TASK-DP-XXX-NNN` (runs `check-task`, checks open decisions, sets `in_progress`; use `check-task --resume` to continue an `in_progress` task); inspect the selected Task, referenced requirements, approvals, dependency statuses, module guidance and current source afresh. Do not implement blocked/provisional tasks. On mismatch, report it and route to `$dp-task-planner review-task <ID>` before coding.
5. **Verify and report:** Run focused implementation checks where applicable; for a task, record them with `python tools/docs.py verify-task --id <ID> --module <Module> [--require-db]` per affected module, then `submit-task` and, after review, `complete-task` (see [README](README.md#implementing-a-task)). Validate documentation links, IDs and references when changing artifacts; report which checks actually ran and which did not.

Preserve one-way traceability: FRs declare `ur_refs`, USs declare `fr_refs`, and Tasks declare relevant `us_refs` / `fr_refs` / `nfr_refs`. Do not manually maintain reverse edges. Cross-feature references use full stable IDs. A technical or NFR task does **not** need an invented User Story.

`ready` means reviewed for completeness, **not** client approval. `approved` requires real authorized, version-specific evidence (`approval_ref`); do not change status because AI generated or validated a file. Do not create a duplicate artifact when rerunning a skill.

Use the existing repository CLI for documentation work (from repository root):

```text
python tools/docs.py validate
python tools/docs.py coverage --feature FEAT-DP-003
python tools/docs.py check-task --id TASK-DP-003-001
python -m unittest discover -s tools/docs_validation -p "test_*.py" -v
```

**Test ownership:** Root `tests/` belongs to the repository's existing .NET unit/integration test projects. Do not create or move Python docs-validator tests into this directory, overwrite existing projects, or alter .NET test discovery settings for documentation tooling. Run application tests with the relevant existing `dotnet test` targets after inspecting the solution. Documentation-tool tests are isolated under `tools/docs_validation/` and run with the command above. If an older package placed `tests/test_docs.py`, use the opt-in migration helper described in `README.md`; never delete unknown tests or the `tests/` directory.

Only use commands that exist in the checked-out repository. Structural validation does not establish business correctness or approval.

## Current backend and architecture invariants

The supplied backend architecture documents a .NET 10 solution with independently hosted Identity, Notification and YARP Gateway components, shared BuildingBlocks, and cross-module Contracts. Do not silently convert this implementation into a single deployable modular monolith because `docs/architecture.md` describes that as a target option. Inspect `DeliveryPilot.slnx`, project files, active registrations and deployments before assuming a runtime feature exists.

Keep the existing dependency direction:

```text
Module.API             -> Module.Application + Module.Infrastructure + Contracts + BuildingBlocks
Module.Worker          -> Module.Application + Module.Infrastructure + Contracts + BuildingBlocks
Module.MigrationRunner -> Module.Infrastructure + BuildingBlocks
Module.Infrastructure  -> Module.Application + Module.Domain + Contracts + BuildingBlocks
Module.Application     -> Module.Domain + Contracts + BuildingBlocks
Module.Domain          -> BuildingBlocks only
Contracts              -> BuildingBlocks only when needed
BuildingBlocks         -> other BuildingBlocks, one-way and without cycles
```

- Application must not reference Infrastructure. Modules integrate through Contracts or Application-owned abstractions, never another module's Domain, Infrastructure or DbContext. Preserve module data ownership and scalar cross-module IDs.
- Controllers translate transport input and send commands/queries via `DeliveryPilot.SharedKernel.Cqrs.ISender`; do not add MediatR or move domain/persistence work into controllers.
- Preserve the registered pipeline order: validation, logging, performance, tracing, transaction, handler. Transaction behavior owns command commit/persistence completion; handlers and repositories must not call `SaveChangesAsync` when it owns the commit. Queries do not open command transactions.
- Let supported domain/validation exceptions reach `GlobalExceptionHandlingMiddleware`; returned failed `Result` values are not exceptions. Follow the module guidance for failure propagation.
- Follow `global.json`, `Directory.Build.props`, and `Directory.Packages.props`; keep NuGet versions centralized. Preserve file-scoped namespaces, cancellation propagation and separate transport DTOs.
- Keep business invariants in Domain, EF configuration in Infrastructure. Preserve plural PascalCase table names/PascalCase columns and quote custom PostgreSQL identifiers; do not create cross-module database FKs.
- Persist instants as UTC `DateTimeOffset`; never log secrets, credentials, authentication tokens or sensitive payloads.
- Existing CQRS/EF skills may contain legacy DevFlow examples: verify generated advice against the active source and `src/Modules/AGENTS.md`.

## Verification and change boundaries

- When creating or modifying code, read and follow the applicable `.editorconfig` conventions for each changed file, including naming, formatting and style preferences marked as suggestions. Apply any more specific `.editorconfig` settings, and check changed code for compliance before finishing; respect generated-code exclusions and keep formatting changes scoped to the task.
- Keep source edits within the requested modules and layers; introduce new hosts, databases, broker contracts, or migrations only when justified by a reviewed task and current architecture.
- Run `powershell -File scripts/Test-Architecture.ps1` after project-reference or architecture-boundary changes (when available in the checkout). Run focused build/tests for behavior changes; `scripts/Verify-Module.ps1 -Module <Module> [-RequireDb]` runs build, tests (counting skipped tests), architecture rules and docs validation for one module.
- For docs-only changes, run `python tools/docs.py validate` when available, then review relative links, source claims and any changes to governance or approval state.
- Do not state that planned Project, Requirements, Backlog, AI or Client Portal modules already exist solely because they are on the roadmap.
- Report modified files, test/validation results, missing evidence, blockers and any unmet acceptance criteria. Never report a command as run if it was not executed.

<!-- CODEGRAPH_START -->
## CodeGraph

When a `.codegraph/` index exists at the repository root, use CodeGraph before text search or file reads to locate or understand code: use `codegraph_explore` when available, or `codegraph explore "<symbol names or question>"` in the shell. If no index exists, skip CodeGraph; indexing is the user's decision.
<!-- CODEGRAPH_END -->
