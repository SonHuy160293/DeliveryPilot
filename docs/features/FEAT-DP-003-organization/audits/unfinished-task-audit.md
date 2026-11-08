# FEAT-DP-003 unfinished-task audit

> **Historical snapshot — reviewed 2026-11-08:** Counts, blockers and tests below describe their recorded checkout. All 18 Organization Task files now record done; see [backend acceptance](../backend-acceptance.md) and [Gateway contract](../gateway-api-contract.md). Frontend and Project/guest handoffs remain separate. Original findings/reconciliation notes are preserved as history.


## Reconciliation — 2026-11-04

This is a historical audit; later changes supersede its point-in-time status counts and test descriptions. Task 013 has been reviewed and is pending. Tasks 017 and 018 were completed to pin permission classification and serialize session revocation. Tasks 005 and 006 are complete within their scopes. The full Identity test project passed 129 tests without skips on disposable PostgreSQL 18. See the [resolution reconciliation](blocker-resolution-plan.md#implementation-reconciliation--2026-11-04) and [session decision](audit-decisions-needed.md#reconciliation--2026-11-04). No requirement approval or production state was changed.

Audited 2026-11-04, Asia/Bangkok. This is an evidence-based review of the checkout, including uncommitted/untracked work; it is not a release, security certification, client acceptance or proof of deployment. No application code, requirement status or task status was changed by this audit. [Resolution plan](blocker-resolution-plan.md) · [Execution sequence](implementation-sequence.md) · [Decisions](audit-decisions-needed.md).

## Executive summary

There are **16** task files: **11 unfinished** (two `pending`, nine `blocked`) and five marked `done`. Of the 11, this audit classifies **one READY** (006), **two CONDITIONAL** (005, 013), and **eight BLOCKED** (007–012, 014–015). Task 005 has substantial local implementation but remains unfinished: its task acceptance boxes are unchecked and its HTTP tests use a fake sender, not the full database/authorization path. Task 006 has no role-management endpoint implementation and passes structural preflight. Task 013 is marked blocked although both declared dependencies are done; it needs fresh review and status reconciliation, not a new business policy. Tasks 007–012 and 014–015 remain gated by unfinished deliverables. Task 016 fixes the known unconditional built-in grant path in source and records a 16-test isolated PostgreSQL run; do not infer deployed database state.

No dependency cycle was found in the declared task graph. The offer-time role-name representation in tasks 007/009 is a **confirmed contract/design gap**; permission snapshot fields do not include a name. Resolve before implementing invitation persistence. Other high-impact items below are testability/security risks, not observed breaches. No client-approved Organization UR/FR/US was found: 8 UR, 14 FR and 9 US are `ready` with null approval references, which denotes internal completeness rather than client approval. Product policy and legal release approval remain distinct from this technical audit.

## Scope and authority

Read root [AGENTS](../../../../AGENTS.md), [module conventions](../../../../src/Modules/AGENTS.md), [authoring rules](../../../authoring.md), [context map](../../../context-map.md), [current backend](../../../../ARCHITECTURE.md), [cross-feature requirements](../../../requirements.md), [feature](../feature.md), [backend plan](../backend-plan.md), [Identity decisions](../../../architecture/modules/identity/design-decisions.md), [Identity dictionary](../../../architecture/modules/identity/data-dictionary.md), all 16 task documents and linked Organization UR/FR/US metadata/acceptance criteria, relevant source/migrations/tests and Gateway/Notification contracts. `docs/architecture.md` is a target proposal, not implemented topology. CodeGraph was consulted but reported stale source index for some files; current source was inspected directly.

Authority is approved requirements, accepted architecture decisions, then source/tests for actual behavior; recorded user-selected decisions and `ready` artifacts provide traceable intended scope but are not client approval. No live deployment, production database, external contract, customer interviews or independent task executions were inspected. The `done` evidence below is documentary and source-based, not a re-run.

## Status and readiness inventory

`READY` means eligible for a fresh executor preflight with no identified material contract blocker; `CONDITIONAL` means explicit review, test coverage or low-risk follow-up remains; `BLOCKED` means an unfinished hard prerequisite or material unresolved contract. Complexity/risk are qualitative engineering judgments, not estimates of duration. All paths in the task-ID links contain the full contract and frontmatter.

| Task | Declared / verified state | Readiness | Sources and applicable AC | Hard unfinished prerequisites | Existing / missing evidence; completion focus |
|---|---|---|---|---|---|
| [005](../tasks/TASK-DP-003-005.md) | pending / partially implemented locally | CONDITIONAL | US-003 AC-001–007; US-008 AC-001–006; FR-004/007/011 | none | Four Identity reads and paged store exist; complete real HTTP + database tenant/authority/outage/page tests and acceptance evidence. No Gateway publication until 014. |
| [006](../tasks/TASK-DP-003-006.md) | pending / not implemented | READY | US-006 AC-001–008, 010–012; FR-008/009 | none | Guard/catalog/audit/domain primitives exist; role APIs, bounded catalog/role queries, mutations and atomic audit do not. Retirement AC-009 belongs to 015. |
| [007](../tasks/TASK-DP-003-007.md) | blocked / not implemented | BLOCKED | FR-002/003/012/014; technical prerequisite to US-002 AC-003–011 and US-005 | 006 | Invitation/offer domain objects exist but are unmapped; lifecycle, verifier, ledger and limits absent. Resolve offer-name snapshot F-01 before persistence design. |
| [008](../tasks/TASK-DP-003-008.md) | blocked / not implemented | BLOCKED | US-002 AC-001, 003, 005–006, 009; FR-002/003/012 | 007 | V1 verification-only encrypted delivery/outbox exists; invitation contracts, intake, dispatch eligibility, status projection and deployment configuration are planned. |
| [009](../tasks/TASK-DP-003-009.md) | blocked / not implemented | BLOCKED | US-002 AC-001–002, 004–012; FR-002/012 | 006, 007, 008 | Authorized issue/list/resend/reissue/cancel handlers absent. Require shared guard and F-01 resolution. |
| [010](../tasks/TASK-DP-003-010.md) | blocked / not implemented | BLOCKED | US-005 AC-001–011; FR-003/014 | 009 | Preview/accept/decline and proof consumption absent; verify generation, sponsor and membership under guard. |
| [011](../tasks/TASK-DP-003-011.md) | blocked / not implemented | BLOCKED | US-004 AC-001–013; US-007 AC-005, 008, 010, 012; FR-005/006/009/010 | 005, 006 | Removal/demotion API absent; domain transitions, guard and audit staging exist. Real concurrent Owner and audit rollback tests required. |
| [012](../tasks/TASK-DP-003-012.md) | blocked / not implemented | BLOCKED | US-007 AC-001–012; FR-010/005/006 | 005, 011 | Nomination persistence/API absent; owner assignment/guard/audit primitives reusable. Nomination acceptance and step-down remain separate transitions. |
| [013](../tasks/TASK-DP-003-013.md) | blocked / no audit query API | CONDITIONAL | US-009 AC-001–006; FR-006/013 | none declared unfinished | Audit event storage/writer and authorization exist; review status and define membership-specific keyset contract F-02 before execution. |
| [014](../tasks/TASK-DP-003-014.md) | blocked / not implemented | BLOCKED | US-001–009, all 88 ACs; FR-001–014 | 005, 006, 008–013, 015 | Gateway only publishes registration/login and Notification webhook; exact allowlist and end-to-end evidence missing. UI/Project handoffs remain explicit. |
| [015](../tasks/TASK-DP-003-015.md) | blocked / not implemented | BLOCKED | US-006 AC-009; FR-008 | 006, 007, 009 | Domain `Retire` alone does not establish guarded assignment/offer preconditions; endpoint absent. Test races with actual grant/issue paths. |

All 11 tasks link ready FRs or explicitly justified technical work, and their frontmatter links validate. The AC ranges above identify where behavior is expected, not proof every scenario is executable or fully covered. [US files](../us/) own exact scenario texts; 014 owns integration evidence rather than replacing individual verification. Across 8 UR and 14 FR, coverage reports **zero structurally unlinked downstream artifacts**. No missing FR was established; a structural edge does not prove semantic scenario coverage. Cross-feature UI, Project and guest resource checks are handoffs, not implemented modules.

## Requirement and acceptance traceability

The matrix maps each story's actual AC headings to the planned owning deliverables. The checked-in requirements are `ready` and `approval_ref: null`; this mapping is not approval or a claim that each case passed. Task 014 provides aggregate integration evidence for all rows.

| Story and linked FRs | ACs | Task coverage and boundaries |
|---|---|---|
| [US-DP-003-001](../us/US-DP-003-001.md) · FR-DP-003-001 | AC-001–009 | 001 creation and replay; 014 Gateway/end-to-end. |
| [US-DP-003-002](../us/US-DP-003-002.md) · FR-DP-003-002/003/012 | AC-001–012 | 007 persistence/proof/limits, 008 delivery, 009 management, 014 integration. Proof revalidation also intersects 010; F-01 affects offer representation. |
| [US-DP-003-003](../us/US-DP-003-003.md) · FR-DP-003-004/007 | AC-001–007 | 005 scoped backend reads, 014 integration; remembered selection, frontend guidance and independently authorized Project/guest navigation are external handoffs. |
| [US-DP-003-004](../us/US-DP-003-004.md) · FR-DP-003-005/006 | AC-001–013 | 011 removal/Owner continuity, 003 audit and 004 guard foundations, 014 integration. |
| [US-DP-003-005](../us/US-DP-003-005.md) · FR-DP-003-003/014 | AC-001–011 | 010 recipient proof/acceptance/rejoin; 007–009 provide lifecycle, delivery and manager prerequisites; 014 integrates. |
| [US-DP-003-006](../us/US-DP-003-006.md) · FR-DP-003-008/009 | AC-001–012 | 006 roles/assignments except retirement; 015 owns AC-009 retirement; 016 covers no-silent-template-upgrade AC-012; 014 integrates. |
| [US-DP-003-007](../us/US-DP-003-007.md) · FR-DP-003-010/005 | AC-001–012 | 012 nomination/acceptance; 011 Owner reduction and continuity; 014 integrates. |
| [US-DP-003-008](../us/US-DP-003-008.md) · FR-DP-003-011 | AC-001–006 | 005 scoped members/own read, 014 integration; F-05 identifies missing verification. |
| [US-DP-003-009](../us/US-DP-003-009.md) · FR-DP-003-006/013 | AC-001–006 | 003 writer foundation and 013 audit read; 014 integration; F-02/F-03 apply. |

## Per-task delivery and compatibility assessment

| Task | Reuse and missing work | Migration, compatibility and independent verification |
|---|---|---|
| 005 | Reuse current authorization, read store and DTOs; complete F-05's real HTTP/DB matrix, including foreign scope, inactive records, permission loss and safe 503. | No new migration planned; Identity endpoints are not Gateway-published. Independent of 006 after 004; coordinate its members controller with 006. Real PostgreSQL is required by its DoD. |
| 006 | Reuse scoped guard, role/assignment domain methods and audit writer. Add role/catalog reads, create/edit and assignment actions, metadata audit factory, conflict translation and fresh-authority/no-op tests. | No schema change assumed; role factory/code change must preserve existing system seeding and named email-conflict handling. Independent after completed 003/004/016; retirement is excluded and belongs to 015. Real PostgreSQL and HTTP failure tests required. |
| 007 | Reuse domain invitation/offer types and Identity unit of work; missing mappings, lifecycle, proof verifier and issuance ledger/governor. Review F-01 before schema freeze. | Additive Identity migration and snapshot update planned. Unfinished 006 is a declared hard prerequisite. Verify concurrent issue/expiry, normalized uniqueness, no proof leakage and rollback on real PostgreSQL. |
| 008 | Reuse existing encrypted V1/outbox and Notification reliability components; new event types, template, private eligibility endpoint, status sequence and deployment bindings are missing. | Additive Notification migration plus versioned Contracts/broker/configuration changes planned; old V1 behavior must remain compatible. Depends on 007 and isolated keys/broker/provider fixtures. Tests must cover retries, cancellation, tampering and reordered statuses. |
| 009 | Reuse 006/007/008 role, lifecycle, delivery and guard seams when completed; management endpoints and handlers are missing. Reconcile F-04 and preserve F-01 offer-time data. | No separate migration stated; coordinates with 007/008 contracts, and Identity delivery staging must commit atomically. Depends on all three; verify no unauthorized enumeration and issue/retire races. |
| 010 | Reuse 007 proof/governor and 009 issue state; recipient preview/accept/decline handlers and current-authority checks are missing. | No additional migration stated; proof/body and fresh-rejoin response are new contracts, with frontend link handling outside this task. Depends on 009; test real redemption races and rollback. |
| 011 | Reuse domain membership transitions, shared guard and audit writer; protected removal/demotion actions and complete state-change logic are missing. | No migration specified; no global logout, project cascade or hard delete. Depends on 005/006 and done 003/004; test PostgreSQL owner races, current authorization and historical attribution. |
| 012 | Reuse Owner guard, assignments and audit; nomination persistence and endpoints are missing. | New Identity-owned nomination mapping/migration planned; no implicit ownership transfer. Depends on 005/011; test sponsor/nominee revalidation, deadline and concurrent terminal state. |
| 013 | Reuse audit storage/writer and current audit-read authority; safe query/projection and deterministic membership cursor are missing. | No migration assumed; review actual indexes before choosing the query. Declared 003/004 done, but current `blocked` status and F-02 need review; can verify independently once cleared. |
| 014 | Reuse existing Gateway and independent hosts; public Organization route allowlist and cross-service acceptance evidence are missing. | Gateway route/configuration rollout is a client-visible change; no new database specified. All declared unfinished dependencies, including 015, must finish; test exact public/private routes and integration in isolated services. |
| 015 | Reuse role `Retire`, 003 audit and 004 guard; guarded reference checks and endpoint are missing. | No new schema stated beyond 007; deploy only with compatible 006/009 writers. Depends on 006/007/009; test assignment/offer/retirement races and failure rollback on real PostgreSQL. |

## Completed-task evidence audit

| Task | Status supported by local evidence | Limitation |
|---|---|---|
| [001](../tasks/TASK-DP-003-001.md) | Creation handler/controller, snapshot/receipt migration and tests exist; task records 104 tests/zero skips on isolated PostgreSQL. | Current source/migration and tests are uncommitted; historical review text under “Unresolved decisions” is superseded by its explicit resolution. Gateway route remains 014. |
| [002](../tasks/TASK-DP-003-002.md) | Core organization/membership/role mappings and migration exist; checkboxes are complete. | Task provides no attributable command output/counts; independently revalidate if dependency acceptance requires stronger evidence. |
| [003](../tasks/TASK-DP-003-003.md) | Audit event mapping/writer and tests exist; checkboxes are complete. | Task provides no attributable command output/counts. Metadata-only `RoleUpdated` schema exists but typed creation factory is a planned 006 addition. |
| [004](../tasks/TASK-DP-003-004.md) | Current authorization, capability catalog and serialized guard exist with tests; checkboxes are complete. | No attributable command/counts in task; session-lock concern R-02 needs targeted verification. |
| [016](../tasks/TASK-DP-003-016.md) | Evaluator now filters matching stored rows; task records 16 passing focused tests and isolated role inspection. | Evidence describes a disposable test database only; catalog-policy drift R-01 remains a future-change risk. |

These evidence gaps do not prove the five tasks incorrectly completed. They require targeted verification rather than silently changing their statuses.

## Findings and requirements consistency

See [blocker plan](blocker-resolution-plan.md) for conflict IDs, severity, ownership and actions. The material confirmed gaps are F-01 (offer-time role name), F-02 (audit membership-order cursor contract), and F-03 (013 stale `blocked` metadata despite done prerequisites). F-04 and F-07 record stale source claims in 009/013; F-05 records incomplete 005 test evidence. None justifies inventing client policy or approval. A requirement may cover several tasks: FR-008 retirement is expressly owned by 015 and AC-012 by completed 016, so 006 alone must not be declared the whole story complete.

Security findings: [capability evaluator](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Authorization/OrganizationCapabilityCatalog.cs) now intersects persisted scoped rows with the known/delegable catalog and separates Owner identity from effective codes; no current blanket Owner/Admin dynamic grant remains. R-01 is a future catalog-classification risk, not proof of an unauthorized production grant. [mutation guard](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/OrganizationMutationGuard.cs) serializes on the workspace and locks users/memberships; session revocation uses a separate lock in [AuthSessionService](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/AuthSessionService.cs), while the guard checks session state without locking it. R-02 requires a race test and contract review before extending guarded writes; a session race is not evidence that membership revocation or existing session policy was violated. Required audit staging, forbidden cross-module data access, proof secrecy and commit ownership must be retested in each future handler.

## Checks run for this audit

`python tools/docs.py validate` passed (67 artifacts, zero errors); `python tools/docs.py coverage --feature FEAT-DP-003` passed (zero structurally unlinked); task preflights 005/006 passed structurally and 013 rejected its blocked status. The 88 AC count was independently counted from story headings. No application build, .NET tests, database, broker, provider, migration or deployment was run during this audit. Historical task evidence is attributed to its source, not represented as a new run.
