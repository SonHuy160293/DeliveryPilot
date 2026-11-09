# Core documentation alignment audit

> Latest reconciliation: [Cross-document reconciliation — 2026-11-08](#cross-document-reconciliation--2026-11-08). Earlier entries retain their historical scope.

> Recorded: 2026-11-03, Asia/Bangkok.
> Scope: Product, cross-feature requirements, roadmap, context routing and authoring policy against workflow discovery and the architecture workspace.
> Status: Core reconciliation applied; unresolved release, contract and implementation handoffs remain explicit. This is an audit record, not approval evidence or a new implementation task.

## Evidence and boundaries

- [Workflow discovery v1.1](../deliverypilot-workflow-discovery.md): recorded Q1–Q44 decisions and later W1 seven-stage workflow. A1–A4 are derived clarifications, not additional confirmed answers.
- [Identity decisions](modules/identity/design-decisions.md): SCOPE/OWN/MEM/AUTHZ/INV/DATA selections and unresolved follow-through. Original [domain discovery](identity-domain-discovery.md) and [readiness](identity-erd-readiness.md) retain historical alternatives with reconciliation notices.
- [Current backend](../../ARCHITECTURE.md), [IdentityDbContext](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/DbContexts/IdentityDbContext.cs), [AuthController](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/AuthController.cs), [domain model directory](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Models/) and [EF configurations](../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/): checked-out implementation evidence, not live-deployment verification. CodeGraph was consulted before source inspection.
- Existing FEAT-DP-002/003 summaries and reference graphs: downstream impact and stale readiness/implementation statements. Stable IDs, frontmatter statuses, approvals, task dependencies and release allocations are preserved.

The original core audit did not resume the incomplete model/mapping task, create migrations, revise detailed feature contracts, invent customer validation, or select a hosting topology. The initial Organization refinement revised draft contracts/supporting notes. The subsequent joint readiness review below resolves scope and changes internal quality statuses; application source remains untouched. U21 separately authorizes notation-only corrections to two login documents, including the existing done task, without changing behavior or its status.

## Findings and reconciliation

| Finding | Evidence | Applied reconciliation / remaining limit |
|---|---|---|
| Core navigation pointed only to the target architecture and omitted workflow/module evidence. | Context map and authoring owners versus new architecture workspace. | Added an architecture index and separate routes for current runtime, target alternatives, selected module design and discovery. |
| Workspace terminology, solo use and guest access were incomplete. | Q1–Q3/Q6; Identity glossary. | Product/global glossary now distinguish Workspace/Organization/tenant, global User, membership, project participation and platform authority. Creation is separate from registration; guests do not require general membership. |
| Product and discovery described delivery-mode scope differently. | Q8/Q25/Q44, D7. | Scrum-only and one-team/one-active-Sprint limits are explicit; multiple independent teams remain deferred. |
| Generic organization questions lagged selected access decisions. | OWN-01, MEM-01/02, AUTHZ-01–06, INV-01/02, DATA-01/03. | Summarized selections with provenance in global requirements; linked detailed policy to the module log. Duration/resend/sponsor questions were open in the original core audit; later U10 resolves them. Transfer, nomination lifecycle, recovery and revocation boundaries remain open. |
| Broad Identity wording could imply ownership of project/approval authority. | SCOPE-01–04; Q2/Q3. | Selected Identity contains Authentication, Organization Management and protected Platform Access. Project participation, resource grants and business decisions stay outside; no Project/Requirements merger is inferred. |
| Separate-context historical analysis and generic schema recommendations could override the selected Identity database. | Original reports' reconciliation notices; SCOPE-01. | Architecture routing and target-proposal caveats identify which ownership recommendation is superseded. Current independent hosts remain unchanged. |
| The main journey skipped iterative UR/Feature discovery and collapsed approval, acceptance and baseline states. | Q4/Q9–Q18/Q39; W1. | Product journey and requirements now describe immutable submitted revisions, independent review/client gates, explicit acceptance and separate scope/plan baselines. Snapshot representation and A1–A4 refinements stay proposed. |
| Global approval questions reopened already selected modes. | Q10–Q16/Q44. | Captured all-required-reviewer policy, optional nonresponse, Feedback Only/Approval Required, solo exemption and blocking-review clearing. Quorum/chains/advanced delegation remain deferred. |
| Runtime UR-to-Story traceability appeared incompatible with repository US→FR frontmatter. | Q17; existing authoring/templates. | Explicitly separated product data relationships from repository authoring. No fake FR, new frontmatter, reverse-reference field or validator change was introduced. |
| Planning/execution summaries missed readiness, privacy and scenario-application boundaries. | Q20/Q23–Q34/Q40/Q42; W1. | Added selected estimation, no-double-counting, declared capacity/privacy, board action, WIP, freshness and human decision boundaries; no invented thresholds or numerical SLAs. |
| QA/UAT/release and change control appeared both essential to the journey and deferred to V1. | Q18/Q22/Q39/Q41; W1; FEAT-DP-011/012/016/018/019 allocations. | Kept release IDs/targets/priorities unchanged, distinguished the full target journey from MVP slices, and added an explicit lightweight-pilot scope decision table. Full journey coverage is not claimed. |
| Source/class, EF/schema and feature completion were conflated by stale implementation prose. | Context/configuration inventory; existing task records. | Updated current-backend and Identity notes plus authentication summary. Eight new class drafts are unmapped; IdentityAuditEvent and all new mappings remain absent. No new collaboration schema or access workflow is implemented. |
| Requirements pointed to a nonexistent single-file feature layout. | Repository feature tree and authoring policy. | Corrected routing to feature summaries and individual `us/US-...md` acceptance artifacts. |

## Remaining decisions and downstream handoffs

These are bounded follow-up work, not reasons to revert confirmed direction to generic open questions.

| Area / owner | Remaining work | Completion evidence |
|---|---|---|
| Release scope / product owner and delivery lead | Allocate lightweight planning inputs, material-change decisions and QA/UAT/release outcomes needed for the pilot; keep full suites and AI expansion separately scoped. | Explicit decision against the [roadmap coverage table](../roadmap.md#workflow-coverage-and-release-decisions), then affected feature acceptance criteria. |
| Organization contracts / BA and Identity design owner | Joint U9–U21 review resolves OD-01–11 for the selected release; lifecycle/recovery exclusions are explicit. Source-aware task planning is next; existing Organization task stays blocked. | 8 UR, 14 FR and 9 US are internally ready with null approval references; internal quality is not customer approval or task execution readiness. |
| Authentication task records / implementation owner | TASK-DP-002-002 still says `pending`/unimplemented in its planning sections despite implemented registration and a later verification section. Reconcile status and acceptance evidence deliberately. Refresh/logout HTTP contracts remain follow-up work. | Current acceptance evidence and reconciled task record; this audit does not mark it done. |
| Project and review contracts / relevant feature owners | Refine FEAT-DP-004–009/015/017 and material-change interfaces from Q/W evidence. Resolve exact mapping/version representation, specification-only review authority and A1–A4 clarifications. | Canonical contracts and scoped diagrams; no assumption that absent feature folders imply existing behavior. |
| Authoring/tooling owner | If repository US artifacts must directly represent runtime UR links, design a separate compatible authoring/template/validator change. | Reviewed rule and tooling tests; current one-way references stay valid until then. |
| Identity implementation owner | Finish IdentityAuditEvent, nine mappings, DbContext registration and scoped tests; later migrations/index deployment and authorization/lifecycle workflows remain separate. | Source/metadata/DDL and relevant tests, then separate deployment evidence. Eight class files alone satisfy none of those missing layers. |
| Platform security/operations owner | Define bootstrap, assurance/activation, last-admin recovery and audit procedures before enabling privileged operations. | Reviewed security procedures and implementation evidence; foundation modeling does not enable them. |

### Impact on existing Organization artifacts

This table preserves the original core audit’s impact snapshot, not a hand-maintained reverse dependency graph. The subsequent refinement is recorded below. Use `coverage` for the current graph.

| Source area | Existing artifacts to refine |
|---|---|
| Workspace creation and protected ownership | UR-DP-003-001, FR-DP-003-001, US-DP-003-001; TASK-DP-003-001 remains blocked and needs fresh code/task planning. |
| Multi-role invitation offers, recipient proof, reissue and duplicate policy | UR-DP-003-002, FR-DP-003-002/003, US-DP-003-002. |
| Durable memberships, context selection and independent project visibility | UR-DP-003-003, FR-DP-003-004/007, US-DP-003-003. |
| Removal/rejoin, last-owner continuity and history | UR-DP-003-004/005, FR-DP-003-005/006, US-DP-003-004. |

The original audit only reconciled summaries. The subsequent user-authorized Organization refinement has now revised the detailed drafts and added missing coverage while preserving existing IDs/statuses. The initial pass left policy/task readiness open; the subsequent joint review below resolves in-scope policy while retaining the separate technical task gate. References in the historical table are shorthand display labels; canonical frontmatter uses full IDs only.

## Original core-audit validation

| Check | Result |
|---|---|
| `python tools/docs.py validate` before/after | 37 artifacts; the same two pre-existing placeholder diagnostics in `FR-DP-002-003` and `TASK-DP-002-004`. No new validation errors after reconciliation. |
| `python tools/docs.py coverage --feature FEAT-DP-002` | Reference graph inspected; US-DP-002-003 has no downstream task (advisory). The two existing validator diagnostics remain. |
| `python tools/docs.py coverage --feature FEAT-DP-003` | Reference graph inspected; US-DP-003-002/003/004 have no downstream tasks (advisory). TASK-DP-003-001 remains blocked. The two existing validator diagnostics remain. |
| Local links, Markdown heading targets and code-fence balance | Checked across all 14 edited documentation files; no missing local targets or heading anchors. |
| Stable IDs and lifecycle metadata | BR/NFR registry IDs, all 24 roadmap feature rows and edited feature frontmatter compared with Git HEAD and preserved. |
| Whitespace and repository format | Edited documentation normalized to CRLF; scoped `git diff --check` passes. |
| Diagram rendering | No Mermaid blocks changed by this audit. No local `mmdc` command was available; rendering was not run. |
| Application/tooling tests | Not run: this audit changes documentation only. Prior test results are not claimed as new verification. |

Structural validation does not establish approval, implemented behavior or production readiness. The two existing diagnostics require a separate correction to their ready/done artifact examples; no status was downgraded or approval fabricated to make validation green.

## Original core-audit files reconciled

| File / group | Change |
|---|---|
| [Product](../product.md) | Workspace/guest terminology, initial delivery modes, target journey and MVP scope caveat. |
| [Requirements](../requirements.md) | Provenanced access/review/baseline/planning rules, distinct lifecycles and remaining policy. |
| [Roadmap](../roadmap.md) | Workflow-aligned slices and explicit unresolved lightweight-pilot allocation; registry unchanged. |
| [Context map](../context-map.md) and [authoring](../authoring.md) | Evidence routing, canonical owners and runtime-versus-repository boundaries. |
| [Architecture index](README.md) and this audit | New navigation and reconciliation/handoff record. |
| [Target architecture](../architecture.md) and [current backend](../../ARCHITECTURE.md) | Selected ownership versus topology alternatives; corrected implementation summary. |
| [Identity overview](modules/identity/README.md) and [decision log](modules/identity/design-decisions.md) | Explicit incomplete code/mapping state and historical column-design scope. |
| [Workflow discovery](../deliverypilot-workflow-discovery.md) | Reconciliation notes and resolved-versus-open Identity policy; Q/W evidence and diagrams preserved. |
| [User Initialization](../features/FEAT-DP-002-user-initialization/feature.md) and [Organization summary](../features/FEAT-DP-003-organization/feature.md) | Corrected stale summaries and linked detailed-contract follow-ups; frontmatter unchanged. |

## Organization refinement — 2026-11-03

The user explicitly requested implementation of the Align FEAT-DP-003 Organization Documentation plan. Their preceding answers select U9/U10 in the [Identity decision log](modules/identity/design-decisions.md): name-only creation (trimmed/nonblank, at most 160 characters, duplicate names allowed, no slug); existing-Owner nomination/member acceptance for additional ownership; seven-day invitations; rotated resend restarting seven days; current inviter authority at acceptance with reissue after authority loss. These answers do not approve canonical artifacts.

Applied work:

- Refined all existing Organization UR/FR/US bodies and kept their stable IDs and draft/null approval state.
- Added UR-DP-003-006 through UR-DP-003-008, FR-DP-003-008 through FR-DP-003-014, and US-DP-003-005 through US-DP-003-009 using the canonical CLI/templates.
- Separated recipient acceptance from inviter administration, change recording from authorized audit retrieval, ordinary grants from protected ownership, and organization context from guest/project authority.
- Completed explicit evidence references, BR/NFR applicability and Given/When/Then scenarios; proposals/undecided branches remain visibly separate.
- Reconciled Identity overview/log/dictionary/ERD annotations/review scenarios and global requirements/workflow summaries. Original Q1–Q44/W1 records, authentication definitions and physical column sets remain unchanged. No nomination table/field is invented.

### Initial refinement readiness boundary (historical)

[Feature OD-01–11](../features/FEAT-DP-003-organization/feature.md#7-open-decisions-and-task-planning-blockers) records exact choices, proposed responsible roles and consequences: permissions/readership, creation retries, invitation conflicts/responses, ownership lifecycle, membership restoration, role lifecycle, revocation timing, retention/erasure, evidence/delivery failures, resource/navigation handoffs and organization recovery/lifecycle. All contracts remain draft.

[TASK-DP-003-001](../features/FEAT-DP-003-organization/tasks/TASK-DP-003-001.md) remains blocked and byte-for-byte unchanged. Its creation/name questions are superseded by the source requirements; it requires separate source-aware review/replanning. Eight stories have no downstream task, intentionally: this update creates no coding tasks. No app/DTO/schema/migration/test code was changed.

### Organization refinement validation

Checks below were executed for this refinement using the available portable Python 3.14.10 executable because the PATH python alias is not a usable interpreter. Structural results do not establish business correctness or approval.

| Check | Actual result |
|---|---|
| Repository validation baseline before editing | 37 artifacts, 2 pre-existing non-draft placeholder errors: FR-DP-002-003 and TASK-DP-002-004. |
| python tools/docs.py validate --feature FEAT-DP-003 | Exit 1; 52 artifacts, the same 2 FEAT-DP-002 errors; no new diagnostic against this refinement. The command validates repository-wide integrity even with feature coverage selected. |
| python tools/docs.py coverage --feature FEAT-DP-003 | Exit 1 due to the same errors; coverage graph produced: 8 UR, 14 FR, 9 US, 1 blocked TASK. Every UR has an FR; every FR has a US; 8 stories have no task (advisory and intentional). |
| python tools/docs.py validate | Exit 1; 52 artifacts, the same 2 pre-existing errors only. Unrelated ready/done artifacts were not changed. |
| git diff --check | Exit 0. Scoped checks also cover new, untracked Markdown files. Existing intentional Markdown hard-break spaces in the global requirements header are preserved. |
| Affected local Markdown links | 211 relative file-link occurrences checked across 40 changed/new files; no missing local targets. Repository validator checked heading anchors and reported no new link diagnostics. |
| IDs, metadata and scenario structure | CLI allocation and validation preserve unique canonical IDs/references. All 31 UR/FR/US artifacts are draft with null approval references; all 9 US have 53 Given/When/Then scenarios in total. Original ID/status/approval/priority fields on existing artifacts are unchanged. |
| Preserved evidence/boundaries | Existing blocked task is byte-for-byte unchanged; original Q1–Q44/W1 rows, implemented authentication dictionary sections and ERD Mermaid blocks are unchanged. No source, tests, templates, tooling or migrations changed. |
| Application/tooling tests and diagram rendering | Not run: no application/tooling behavior or Mermaid diagram blocks changed. These are documentation checks, not executed product scenarios or live-schema verification. |

**Derived coverage summary:** all 8 URs have declared downstream FR coverage and all 14 FRs have declared story coverage. Only the existing creation story has a task; the other 8 remain without tasks by design. Re-run coverage rather than manually maintaining reverse-reference fields. Drafts remain blocked where feature OD decisions affect their behavior.

### Organization refinement file list

This is a change inventory, not a reverse requirement graph.

| Files | Change |
|---|---|
| [Feature summary](../features/FEAT-DP-003-organization/feature.md) | Selected scope, U9/U10 provenance, explicit decision register and blocked-task handoff. |
| Existing UR-DP-003-001 through UR-DP-003-005; new UR-DP-003-006 through UR-DP-003-008, under [ur/](../features/FEAT-DP-003-organization/ur/) | 8 human needs with source evidence and specific open questions. |
| Existing FR-DP-003-001 through FR-DP-003-007; new FR-DP-003-008 through FR-DP-003-014, under [fr/](../features/FEAT-DP-003-organization/fr/) | 14 observable contracts with distinct security, integrity and policy boundaries. |
| Existing US-DP-003-001 through US-DP-003-004; new US-DP-003-005 through US-DP-003-009, under [us/](../features/FEAT-DP-003-organization/us/) | 9 user-valued drafts and concrete acceptance scenarios. |
| Identity [README](modules/identity/README.md), [decisions](modules/identity/design-decisions.md), [dictionary](modules/identity/data-dictionary.md), [ERD](modules/identity/erd.md), [scenarios](modules/identity/review-scenarios.md) | Selected follow-up behavior and remaining physical/lifecycle gates; original authentication baseline preserved. |
| [Global requirements](../requirements.md), [workflow discovery](../deliverypilot-workflow-discovery.md), this audit | Current summaries and refinement history, without changing original approvals or Q/W answers. |

## Joint Organization readiness review — 2026-11-03

### Current outcome

The user explicitly requested resolving the remaining choices together so the Organization requirements could become coding-ready inputs. User answers U11–U21, retained in the Identity decision log, resolve all OD-01–11 groups for the selected release. The earlier initial-refinement results above are historical, not the current readiness state.

The feature and all 31 UR/FR/US artifacts now have internal status ready after the joint policy/semantic review; approval_ref remains null. Eight URs, 14 FRs and nine stories contain 88 Given/When/Then scenarios. Selected behavior covers readers/fields/defaults, protected targets/removal ceiling, creation/invitation/nomination retries and lifecycle, roles, navigation, revocation, evidence/retention and abuse limits. Scenario conditions distinguish valid reasons/current grants from denial cases. No executed application scenario, customer approval or production release is claimed.

Explicit exclusions resolve scope rather than hide unknown branches: membership suspension/restoration UI/commands, workspace/account deletion/lifecycle changes, automated history erasure/purge, durable denied-attempt history, platform privileges and operator recovery are separately scoped. Denied-state, active-workspace and compromised-owner containment remain enforceable requirements. Built-in upgrades require separate reviewed work. Project/guest authority remains resource-owned.

### Coding-task boundary

TASK-DP-003-001 remains blocked and byte-for-byte unchanged; its old policy questions are superseded by resolved source contracts. Its API, persistence, authorization and dependency/test plan needs source-aware technical replanning. The eight other stories intentionally have no coding tasks; no new TASK document was created. Ready contracts are adequate business input for that planning, not an executable task or evidence the capability exists.

Read-only source inspection used normal tools after CodeGraph reported no usable index. IdentityDbContext currently declares User, PasswordCredential and AuthSession business sets; the OrganizationMembership/OrganizationRole class drafts and lack of Organization Application use cases are not a mapped/implemented Organization workflow. This pass did not change source, project references, database schema, tests or migrations.

### Current validation

| Check | Actual result |
|---|---|
| python tools/docs.py validate --feature FEAT-DP-003 | Exit 0; 52 artifacts, 0 errors. |
| python tools/docs.py coverage --feature FEAT-DP-003 | Exit 0; 8 UR, 14 FR, 9 US, 1 blocked TASK; every UR has FR coverage and every FR has story coverage; eight no-task advisories intentional. |
| python tools/docs.py validate | Exit 0; 52 artifacts, 0 errors. |
| git diff --check | Exit 0; scoped checks also cover new Markdown file formatting. |
| Scoped integrity | 43 documentation files changed/new since the initial alignment baseline; 31 ready contracts with null approvals, 88 unique story-local AC IDs, CRLF/final-newline checks passed. All affected local targets exist; global validator reports no heading-link errors. |
| Preserved boundaries | Original Q1–Q44/W1 rows, implemented authentication dictionary sections, physical column sets/types/nullability/defaults and ERD Mermaid blocks are unchanged; Organization task unchanged. Existing artifact IDs/priorities preserved; only intended Organization quality statuses changed. |
| python tools/docs.py check-task --id TASK-DP-003-001 | Expected exit 1: task is blocked and codebase_verified is false. This is a technical task-planning gate, not a remaining business-policy blocker. |
| Application/tooling tests and diagram rendering | Not run: source/tooling/diagram blocks unchanged. Validation is structural documentation evidence, not runtime verification. |

The two initial FEAT-DP-002 validator diagnostics were corrected with the user's U21 scope authorization. Only notation changed: cookie expiry/value descriptions and the illustrative access-token string. The validator also treated real C# generic notation as placeholders; Markdown entities preserve rendered type signatures without changing types/behavior. FR-DP-002-003 stays ready; TASK-DP-002-004 stays done; no approval or test evidence was altered.

### Derived requirement coverage snapshot

Generated from current canonical frontmatter following the CLI coverage run; this is a review snapshot, not hand-maintained reverse metadata. Re-run coverage after future changes. Acceptance coverage is in the linked stories; all 14 FRs have concrete verification criteria.

| FR | Source UR | Stories | NFR | Internal state / approval_ref |
|---|---|---|---|---|
| FR-DP-003-001 | UR-DP-003-001 | US-DP-003-001 | NFR-001, NFR-002, NFR-003, NFR-004 | ready / null |
| FR-DP-003-002 | UR-DP-003-002 | US-DP-003-002 | NFR-001, NFR-002, NFR-003, NFR-004, NFR-008 | ready / null |
| FR-DP-003-003 | UR-DP-003-002, UR-DP-003-008 | US-DP-003-002, US-DP-003-005 | NFR-001, NFR-002, NFR-003, NFR-004, NFR-008 | ready / null |
| FR-DP-003-004 | UR-DP-003-003 | US-DP-003-003 | NFR-001, NFR-002, NFR-004 | ready / null |
| FR-DP-003-005 | UR-DP-003-004, UR-DP-003-007 | US-DP-003-004, US-DP-003-007 | NFR-001, NFR-002, NFR-003, NFR-004, NFR-008 | ready / null |
| FR-DP-003-006 | UR-DP-003-005 | US-DP-003-004, US-DP-003-009 | NFR-001, NFR-002, NFR-003, NFR-004, NFR-008 | ready / null |
| FR-DP-003-007 | UR-DP-003-002, UR-DP-003-003 | US-DP-003-003 | NFR-001, NFR-002 | ready / null |
| FR-DP-003-008 | UR-DP-003-006 | US-DP-003-006 | NFR-001, NFR-002, NFR-003, NFR-004 | ready / null |
| FR-DP-003-009 | UR-DP-003-006 | US-DP-003-006 | NFR-001, NFR-002, NFR-003, NFR-004 | ready / null |
| FR-DP-003-010 | UR-DP-003-007 | US-DP-003-007 | NFR-001, NFR-002, NFR-003, NFR-004 | ready / null |
| FR-DP-003-011 | UR-DP-003-002 | US-DP-003-008 | NFR-001, NFR-002, NFR-008 | ready / null |
| FR-DP-003-012 | UR-DP-003-002, UR-DP-003-008 | US-DP-003-002 | NFR-001, NFR-002, NFR-003, NFR-004, NFR-008 | ready / null |
| FR-DP-003-013 | UR-DP-003-005 | US-DP-003-009 | NFR-001, NFR-002, NFR-003, NFR-008 | ready / null |
| FR-DP-003-014 | UR-DP-003-004, UR-DP-003-008 | US-DP-003-005 | NFR-001, NFR-002, NFR-003, NFR-004 | ready / null |

### Current file inventory

The original refinement file list above covers the feature, all 8 UR/14 FR/9 US, the five edited Identity documents, global requirements, workflow discovery and this audit. The joint review additionally updates roadmap readiness/navigation and the two user-authorized FEAT-DP-002 notation fixes. Total: 43 documentation files relative to the initial alignment baseline (28 existing files changed and 15 new artifacts); no source/task implementation files were added.

## Subsequent backend task planning — 2026-11-03

The user invoked dp-task-planner plan-feature FEAT-DP-003 and selected backend tasks only. The [backend task index](../features/FEAT-DP-003-organization/backend-plan.md) records inspected source, proposed technical contracts and true dependencies. Fourteen TASK files now cover foundations and all source stories; TASK-DP-003-001 was replanned in place. TASK-DP-003-002 is the initial pending task; dependent tasks remain blocked pending prerequisite acceptance/current-source review. Earlier statements that the creation task was byte-for-byte unchanged describe the earlier requirements-review stage only.

No UR/FR/US, application source, existing test, migration, project reference or deployment was changed by planning. The versioned invitation delivery task explicitly addresses the existing one-day verification-only contract, current-proof retry admission and real delivery status rather than treating intake acknowledgement as Delivered. Frontend implementation and Project/guest resource admission remain external handoffs, without silently selecting the proposed frontend stack or implementing a monolith. Planning validation results are recorded in the task index after the actual commands run.

## Eight-stage lifecycle and execution visibility — 2026-11-07

**Authority:** Product-owner clarification and subsequent request to implement the documentation plan, recorded as [D1–D6](../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions). This authorizes documentation alignment; it is not detailed requirement approval, customer validation or permission to execute application tasks.

| Finding | Reconciliation applied | Remaining handoff |
|---|---|---|
| Definition centered on delivery risk and stopped near release | Product v2.1 and root README now describe post-contract fixed-price SDLC management through warranty/closure; risk remains a core capability | Validate buyer/value hypotheses with real vendor/client participants |
| Seven-stage discovery and engagement-wide research could imply one sequential pipeline including presales | New canonical sdlc-workflow.md defines LC01–LC08, overlapping activities, release-specific gates and named client/vendor authority; historical PH research is mapped explicitly | Refine detailed stage policies and release scope in feature contracts |
| Timeline language could conflate late work with extending a Sprint | Preserved original timebox/selection, actual execution, forecast and scenario are separated; worked carry-over example and missing/stale/split/reopened-work cases added | Select event capture, estimation/split/reopening and forecast presentation policies |
| Feature registry lacked explicit API and warranty/closure ownership | Existing 009 owns actual Sprint execution; 018 owns forecast/scenarios; new proposed 029 API Contract & Integration Evidence and 030 Warranty, Handover & Closure | Discovery and dependency review before generating detailed artifacts; no module or schema decisions inferred |
| Full product scope and MVP were conflated | Product/roadmap distinguish first usable agreement-to-review increment from subsequent full eight-stage coverage; first connectors remain a pilot decision | Validate minimum coherent release and integration effort |
| Original analysis was incomplete and used older phase boundaries | Completed README and chapters 00/01/04/07/08; reconciled 02/03/05/06; evidence limits, qualitative rankings, counterexamples, MoSCoW and interview guide now explicit | No interviews performed; pain frequency, adoption and willingness to pay remain hypotheses |
| Shared constraints lacked current scope/execution/evidence vocabulary | Added BR-058–BR-067 with user-confirmed direction separated from proposed operational rules; vocabulary remains in requirements rather than a competing new global glossary | Detailed rules still require normal feature refinement; cross-organization privacy and optional review policy need scenario review |
| Navigation did not distinguish lifecycle authority from research | Updated context map and authoring owners; research remains evidence/proposals, workflow owns current stage/gate narrative | Target architecture may require future domain-placement review; runtime topology unchanged |

**Preserved:** Existing feature IDs 001–028, FEAT-DP-002/003 contracts and statuses, approval references, source code, tests, migrations and hosting boundaries. Original SDLC research remains industry context; its presales/contracting and fictional Atlas figures do not expand the initial product or estimate DeliveryPilot implementation. Source hosting, full accounting and load execution remain external capabilities.

**Validation:** The required `python tools/docs.py validate` was attempted and could not start: `python.exe` resolves to the WindowsApps alias, with no usable Python interpreter exposed. The discovery checker also requires Python and was not run. Node-based checks passed for 19 changed/new documentation files: 314 local links, 97 tables, 13 fenced blocks, research metadata, stable BR/feature ID uniqueness and preservation of detailed feature artifacts. A repository-wide scan found no broken inbound anchors to those documents. These are a limited fallback, not the canonical validator. `git diff --check` checks whitespace. Mermaid flow definitions were reviewed as text; no diagram renderer or application tests were run for this docs-only change.

**Next:** Use the [validation guide](../research/analysis/08-assumptions-and-interviews.md) and refine the affected roadmap features. Rerun the canonical validator and discovery checker when Python is available. Do not implement source changes or treat research proposals as approved requirements from this refactor alone.

## Cross-document reconciliation — 2026-11-08

Status: Applied documentation corrections, not requirement approval or application implementation. Authority: user-authorized review/correction plan and existing D/Q/V/U decisions. Source inspection used the current Identity DbContext, model snapshot, configurations, migrations, AuthController and canonical Task metadata. CodeGraph was attempted and reported no usable index; text/source inspection followed. No live deployment or database was inspected.

| Finding | Severity | Applied correction / disposition |
|---|---|---|
| C01 Organization implementation described as unmapped/in progress | High | Identity overview, User Initialization summary and roadmap now point to the implemented bounded backend and recorded acceptance. Backend versus frontend/Project/deployed state remains explicit. Resolved. |
| C02 Nine-table physical proposal presented as current schema | High | Added current-schema.md with fourteen shared-schema mapped tables/columns and migration/configuration links. Original ERD/dictionary retained as historical proposal with replacement links; PlatformRoleAssignment remains unmapped. Resolved for inventory; future platform design remains deferred. |
| C03 Contract glossary implies three initial contract models | High | Product/requirements now identify fixed-price as the only contract model (D8); budget questions narrowed to fixed-price pilot disclosure. Resolved. |
| C04 Target architecture trails D1–D6 and implies infrastructure replacement | Medium | Added execution/API/warranty/closure coverage and qualified topology-specific advice. Current outbox/broker/Gateway preserved; consolidation and future module placement remain explicit unresolved architecture decisions. |
| C05 Roadmap integration and AI report allocations differ | Medium | V1 pilot evidence adapters versus V2 broader coverage now consistent; 028 registry includes proposed later report drafts. Existing release proposals/IDs retained. Resolved. |
| C06 Logout scope called open after current-session decision | Medium | UR now cites resolved D-006 scope. Draft FR/US clarify guarded Organization write commit ordering from task 018; refresh/logout HTTP races remain genuinely unresolved. No status/approval change. |
| C07 Historical audits/readiness/plans compete with current evidence | Medium | Added current navigation/replacement notices to original design, audit and planning records; acceptance counts labelled recorded. Original text/results and anchors preserved. Resolved by explicit classification, not deletion. |
| C08 Current reading path unclear | Medium | Added docs/README.md; context map, architecture index and authoring policy route current facts and define currency discipline. Resolved. |

**Scenario consistency review:** Initial fixed-price input links to scope/criteria and controlled change; optional detailed review stays distinct from MVP baseline sign-off. Scope swaps require impact/authority rather than equal points. Original Sprint timebox and noncompletion persist through later delivery; actuals are distinct from forecasts. Acceptance, deployment, payment and warranty/closure remain separate. API revision/build/environment/consumer/test provenance remains explicit. General admitted-request behavior is qualified by Organization guarded-write serialization. These are cross-document reviews, not executed application tests.

**Preserved:** Stable IDs, artifact statuses/approval references, historical test records, application source/tests, migrations and deployment topology. Remaining decisions include first connectors, detailed warranty/closure policies, optional approval configuration, forecast/credit edge policies, future platform assurance and host/module placement. Each routes to its existing feature/architecture owner rather than being silently selected here.

### Reconciliation verification

- Canonical validator: 69 artifacts, zero errors. Python was located outside PATH at the bundled DeepSeek Harness runtime; earlier WindowsApps-alias failures remain historical evidence, not a current blocker.
- Feature coverage: Organization has zero unlinked downstream artifacts; User Initialization has one advisory gap, the draft logout story US-DP-002-003 without an implementation task. This is an explicit planned handoff, not a missing reference or permission to create/execute a task.
- Discovery checker: all ten analysis documents pass, zero errors/warnings. An initial wildcard invocation was corrected to pass expanded file paths.
- Repository-wide Node checks: 124 Markdown files, 1,237 local links (including anchors), 271 tables and 120 fenced blocks; zero structural issues. All 69 feature/artifact metadata records retain their IDs, statuses and approval references; no dependency cycles found.
- Source-backed inventory: fourteen shared-schema tables/columns extracted from the checked-out EF snapshot; mappings, migration files and AuthController inspected. This is static source verification, not application test execution or deployment proof.
- Whitespace: git diff --check passes. Mermaid definitions were reviewed as text and fenced structure checked; no Mermaid renderer was available/run, so rendered diagram validation remains a limitation. No application tests were rerun for these documentation-only corrections.

Remaining validation/decision owners: documentation maintainer for rendered Mermaid review; feature owners for the open policies above; authentication feature owner for draft refresh/logout HTTP contract planning. No unresolved high-priority document conflict remains in the reviewed correction set.

## Scrum-only and fixed-price-only scope — 2026-11-09

The user decided that DeliveryPilot focuses only on Agile Scrum and fixed-price outsourcing, and asked for non-Scrum delivery modes to be deleted from all documents rather than marked as deferred. Recorded as [D7–D8](../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions).

| Area | Change applied |
|---|---|
| Decision record | D7 (Scrum is the only delivery framework) and D8 (fixed-price is the only contract model) added; Q8, Q23 and V1 reworded as Scrum-only; the Q-row about the removed mode's WIP policy deleted. |
| Workflow discovery | Removed non-Scrum delivery modes from the intro, glossary, execution diagram, §5.2, §9.3, §10 (former WIP section deleted; §10.3 is now Scrum execution handoff), scenario seeds, feature mapping, §18.1 and the handoff chain. |
| Product, requirements, roadmap | Scrum-only BR-027 and product model; deleted the removed mode's glossary row, roadmap V2/Phase 08 items and open hypothesis; "contract type" wording replaced by fixed-price agreement; future contract models removed. |
| Architecture and identity analysis | Signal thresholds configurable per project; identity Project root and permission matrix describe Scrum policy only. |
| Navigation | Root and docs README, context map, SDLC workflow and research README reference D1–D8. |

**Not changed:** Research comparisons of commercial models (industry context), FEAT-DP-002/003 artifacts, task statuses, approval references, source code, tests and migrations.
