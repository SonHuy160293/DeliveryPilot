# Artifact Authoring & Lifecycle Policy

## Canonical owners

- `product.md`: product intent, audience, scope and non-goals.
- `requirements.md`: global BR/NFRs, governance and quality rules.
- `../ARCHITECTURE.md`: current backend as documented; source/tests/deployments verify implementation claims.
- `architecture.md`: target architecture alternatives and proposed trade-offs; not an accepted runtime-consolidation decision.
- `architecture/README.md`: entry point to module design, evidence and reconciliation records.
- `architecture/modules/<module>/`: selected module scope, local terminology, decision provenance, ERD, dictionary and review scenarios. Physical proposals do not prove schema deployment.
- `deliverypilot-workflow-discovery.md`: user-confirmed Q1–Q44/W1 workflow evidence, derived clarifications and deferred details; an input to canonical contracts, not their approval.
- `roadmap.md`: registered feature IDs, priorities, dependencies and release intent.
- `context-map.md`: selective context routing; do not duplicate content.
- `features/<feature>/feature.md`: specific problem, actors, scope, evidence, open decisions and artifact navigation.
- `features/<feature>/{ur,fr,us,tasks}/*.md`: one artifact per file; only this artifact owns its full contract.
- `.agents/skills/`: authoring method and decision gates, never independent product facts.

Approved business requirements define intended behavior, accepted architecture decisions constrain design, and source/tests/contracts establish implementation reality. Keep the [alignment audit](architecture/documentation-alignment-audit.md) as a record of cross-document differences and follow-up work, not a second source of business policy.

## Stable identifiers and references

- Features: `FEAT-DP-XXX` (e.g. `FEAT-DP-003`).
- Artifacts: `UR-DP-XXX-NNN`, `FR-DP-XXX-NNN`, `US-DP-XXX-NNN`, `TASK-DP-XXX-NNN`.
- Use the canonical exact ID as filename, e.g. `ur/UR-DP-003-001.md`; the feature directory holds the slug.
- Use only full IDs in frontmatter references. FR declares `ur_refs`; US declares `fr_refs`; TASK declares `us_refs`, `fr_refs`, and/or `nfr_refs`.
- Do not maintain reverse references manually; derive them with `python tools/docs.py coverage --feature FEAT-DP-003`.
- `depends_on` is a directed relationship, same-type artifact references for US/TASK; do not introduce cycles or use nonexistent IDs.
- IDs are never reused. `next-id` proposes a candidate; `new-artifact` uses exclusive creation. Concurrent branches must still resolve collisions at merge/CI.

## Scope and quality

- UR: a human's need and desired outcome, not technical design.
- FR: observable, testable system behavior derived from one or more URs. An FR may be justified by global BR/NFR policy; record exceptions rather than inventing a user need.
- US: testable user-valued increment linked to relevant FRs with concrete acceptance criteria. Do not invent artificial stories for DevOps/security-only work.
- TASK: source-linked coding deliverable; backend/API contracts, DTOs, validation, authorization, persistence, errors, transactions, idempotency, correlation and tests **when applicable**.
- `feature.md` is a navigational summary, not a second inventory of every story body.
- Tasks may declare `implementation_skill: <skill-name>` or `null`. This is optional routing metadata; when provided it must match a local `.agents/skills/<skill-name>/SKILL.md`. It does not confer approval, prove current code readiness, or trigger automatic execution. For a CQRS/API task, `add-cqrs-api-feature` is the preferred executor when its conventions match the actual source.
- Use `docs/templates/` as the single source for artifact format. Generated placeholders must be filled or clearly marked unresolved before status moves beyond draft.

## Status and approvals

- Feature: `discovery`, `draft`, `ready`, `in_progress`, `delivered`, `retired`.
- UR/FR/US: `draft`, `in_review`, `ready`, `approved`, `superseded`, `retired`.
- Task: `pending`, `blocked`, `in_progress`, `in_review`, `done`, `cancelled`.
- `ready` is an internal requirements-quality state, not client approval. `approved` requires an actual decision and nonempty `approval_ref` pointing to verifiable evidence; Git commits and AI confidence do not count.
- Approval is version-specific in product runtime; source-controlled planning drafts must not impersonate runtime approval decisions.
- Task planning from draft requirements is allowed as **provisional exploration**, but status must be `blocked` even if a preliminary source inspection occurred. `codebase_verified: true` is only evidence of earlier code inspection; it is **not** an approval or freshness guarantee.
- Before task-first coding, run `python tools/docs.py check-task --id TASK-DP-XXX-NNN` (or `--resume` for an authorized in-progress continuation); the executor must also inspect current code, satisfy the actual approval policy and ensure blocking decisions are resolved. A validator's green result is only structural readiness.
- Changing an approved contract requires a new version/revision and impact analysis; never silently overwrite approval evidence.

## Repository artifacts versus product runtime

The repository's artifact lifecycle and the product's runtime workflow serve different purposes:

| Repository convention | Runtime product meaning / boundary |
|---|---|
| FR declares `ur_refs`; US declares `fr_refs`; Task declares relevant source references | Runtime traceability permits many-to-many relationships and direct UR-to-Story links under workflow Q17. Do not invent an FR to hide a tooling gap, add unsupported frontmatter, or change the validator implicitly. A separate authoring/tooling change is needed if repository artifacts must encode that direct path. |
| `ready` records internal contract completeness | Runtime DoR concerns selected delivery work and is rechecked before commitment/pull. Neither is client approval. |
| `approved` requires version-specific `approval_ref` | Reviewer approval, explicit final acceptance, scope-baseline establishment, plan application and plan-baseline establishment are distinct runtime decisions. A repository status does not collapse those gates. |
| `TASK-...` is an implementation-planning artifact | Runtime Dev/QA Tasks, technical work, Bugs and Spikes are product work types; they do not have to mirror repository file structure. |
| `FEAT-DP-...` is a stable product-capability registry ID | A customer's Epic/Feature/Story grouping is runtime data, not a new roadmap ID or mandatory folder hierarchy. |

Runtime field/state representation remains feature design work. Keep existing templates, stable IDs and one-way references until an explicit tooling change is reviewed.

## Change impact and review gates

1. New feature: register in roadmap or explicitly propose registration; locate existing functionality and overlapping contracts.
2. Generate one or many UR/FR/US drafts in a single run; present gaps and review questions without claiming approval.
3. Gate UR→FR: needs and sources clear enough. Gate FR→US: observable behavior and key boundaries testable. Gate US→Task: linked FRs and AC testable, policy satisfied, architecture relevant.
4. Task planning inspects live code; if unavailable, output clearly provisional tasks without invented paths or exact contracts.
5. On change, find downstream dependents via IDs and propose diffs; do not overwrite reviewed material or regenerate everything indiscriminately. Reconcile summaries and context links, record the exact Q/W/design-decision provenance, and leave unresolved choices visible.
6. Run `python tools/docs.py validate` and affected-feature coverage; check relative links and relevant diagrams. Run applicable implementation/tooling tests when those change. Distinguish pre-existing validator failures from new errors; passing structure does not establish business approval.

## Evidence policy

Label statements **Agreed direction**, **User-confirmed workflow**, **User-selected design**, **Research-supported**, **Proposed**, **Open**, or **Implemented** as appropriate. User confirmation is evidence for the recorded choice only; it does not change downstream `status` or `approval_ref`. Q/W labels and Identity decision labels are local evidence references, not new canonical artifact IDs or accepted ADRs.

Never cite absent stakeholder interviews. Future API/source paths must be marked **proposed** until inspected. For implementation claims distinguish a domain-class draft, an EF-mapped model, a migration, a deployed schema and a tested user journey. Preserve superseded analysis with a reconciliation notice instead of rewriting it as though it always contained the selected decision. CI checks structural quality; semantic correctness requires review.

## Test ownership

- Root `tests/` is owned by existing .NET unit/integration test projects, not AI documentation tooling.
- Docs regression tests are under `tools/docs_validation/`, run with `python -m unittest discover -s tools/docs_validation -p "test_*.py" -v`.
- For application tasks, inspect the existing solution/test projects and extend the appropriate test project rather than adding conflicting root test infrastructure.
- Older docs tooling placed `tests/test_docs.py` in root `tests/`. The optional checksum-guarded `tools/docs_validation/migrate_legacy_tests.py` defaults to a dry run and only removes exact known generated files with `--apply`; never remove an unknown/modified test or the containing directory.
