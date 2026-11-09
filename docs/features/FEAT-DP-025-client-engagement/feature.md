---
id: FEAT-DP-025
title: "Client Engagement Management"
status: ready
priority: P0
release: "MVP"
depends_on: ["FEAT-DP-003"]
---
# FEAT-DP-025 — Client Engagement Management

> **Status:** Feature contract drafted 2026-11-09 with `dp-feature-engineer`; all open business questions were resolved by the user in the joint review on 2026-11-09 (decision record below). UR/FR/US are `ready`: internally complete for task planning, **not** client-approved (`approval_ref: null`) and not delivered as product behavior. [ADR-001](../../architecture/adr-001-delivery-module-placement.md) places this feature in `DeliveryPilot.Projects`, `engagement` context; task 001 adds its mapped persistence foundation, with no engagement API or deployed feature yet. Roadmap slice VS-02 with [FEAT-DP-004](../FEAT-DP-004-project-initialization/feature.md).

## Problem and business outcome
Fixed-price outsourcing vendors start delivery after a contract is signed, but the client, the agreement, the contract value/effort and the milestones live outside the delivery tool. Projects then lack an attributable link to what was agreed, and later risk signals have nothing to compare against. **User-confirmed direction:** V3, D1/D4, D8. The vendor-pain claim itself is a hypothesis to validate with pilots (V5).

**Intended outcome:** an authorized vendor user records one engagement per client agreement; client projects belong to at most one engagement (BR-046); contract value, effort, milestones and the client PO designation are attributable; an optional client-organization link is set only with the client Owner/Admin's confirmation (ADR-001 D-09).

## Actors and scope
| Actor | Role in this feature |
|---|---|
| Vendor delivery manager | Holds `organization.engagements.create`; records engagements |
| Vendor engagement manager | Holds the new `organization.engagements.manage`; edits, closes, manages values, milestones, contacts and links |
| Vendor delivery leadership | Holds `organization.engagements.read-all`; sees all engagements |
| Project participant | Sees a money-free summary of their project's engagement |
| Client Product Owner / contacts | Named on the engagement; gain **no** access from it |
| Client organization Owner/Admin | Confirms or removes a client organization link |

**In scope:** engagement creation and maintenance; client details; agreement reference; contract value and person-days; milestones with original/current dates; client PO and contact designation; tiered visibility; close/reopen; link-code issuance and confirmation; attributable history.

**Out of scope:** projects and guest admission (FEAT-DP-004); client-facing sharing (FEAT-DP-015); change requests (FEAT-DP-019); invoicing, payments, accounting, CRM and presales (D5); other contract models (D8); portfolio analytics (FEAT-DP-014); commercial extensions such as payment milestones or internal cost budgets (FEAT-DP-022).

## Feature policy and constraints
- BR-001, BR-003, BR-004, BR-008, BR-046; BR-021 stays with FEAT-DP-019 (no CR gate here in MVP).
- ADR-001 D-03 workspace entry codes in Identity: `organization.engagements.create` and `organization.engagements.read-all` exist ([TASK-DP-003-020](../FEAT-DP-003-organization/tasks/TASK-DP-003-020.md)); `organization.engagements.manage` was added for this feature by [TASK-DP-003-022](../../features/FEAT-DP-003-organization/tasks/TASK-DP-003-022.md) (done). D-04 commands recheck Identity uncached, queries may cache ≤30 s, fail closed. D-07 tenant filters. D-09 optional scalar `ClientOrganizationId`, client-confirmed, grants nothing.
- NFR-001, NFR-002, NFR-003, NFR-004, NFR-006, NFR-008.

## Key journeys and edge cases
1. Delivery manager records an engagement → manager adds contract value, effort and milestones → designates client PO → FEAT-DP-004 creates a project under it.
2. Manager moves a milestone date with a reason; the original date stays visible.
3. Delivery leader lists all engagements and sees project names/status per engagement; a project participant sees only a money-free summary; another workspace sees nothing.
4. Manager issues a link code; client Owner/Admin confirms; either side removes; no access changes.
5. Edge/security: revoked membership, Identity unavailable (fail closed), concurrent edits, Closed engagement edits, cross-tenant ID guessing, link-code guessing (generic failure + rate limit).

## Artifact index
- URs: `ur/UR-DP-025-001` … `UR-DP-025-005`
- FRs: `fr/FR-DP-025-001` … `FR-DP-025-010`
- USs: `us/US-DP-025-001` … `US-DP-025-008`
- Tasks: `tasks/TASK-DP-025-001` … `TASK-DP-025-013`, planned 2026-11-10 with `dp-task-planner`; TASK-DP-025-011 is blocked until TASK-DP-004-002 (project persistence) and TASK-DP-003-024 (Identity batch user lookup) are done

Run `python tools/docs.py coverage --feature FEAT-DP-025` for traceability.

## Dependencies and decisions
**Depends on:** FEAT-DP-003 (workspace, membership, entry codes; Identity authority check TASK-DP-003-019) and [TASK-DP-003-022](../FEAT-DP-003-organization/tasks/TASK-DP-003-022.md), which adds `organization.engagements.manage` (delegable; Owner/Admin by default) to Identity. **Used by:** FEAT-DP-004, FEAT-DP-001/027, FEAT-DP-019, FEAT-DP-030.

### Decision record — joint review 2026-11-09
User-selected answers in this conversation. They are decision evidence for the stated choices only; they do not set `approval_ref`.

| ID | Decision | Affects |
|---|---|---|
| OQ-025-01/02 | Required: client display name (≤200), agreement reference (≤100), signed date. Optional: legal name, country (ISO 3166-1 alpha-2), engagement name, HTTPS document link. No file storage. | FR-001/002 |
| OQ-025-03/04 | One fixed-price contract value (≤2 decimals) in one ISO 4217 currency, changeable only while the amount is empty; planned effort in person-days; both internal. | FR-003 |
| OQ-025-05 | Milestone: name, description, original/current date, status Planned/Achieved/Missed/Retired; date change needs a reason; no CR gate, no project/payment links in MVP. | FR-004 |
| OQ-025-06 | Engagement names contacts with at most one designated client PO (no access); each project assigns its own client PO from guests. | FR-005, FEAT-DP-004 |
| OQ-025-07 | Full detail: read-all, manage, creator. Summary (no money/contacts): participants of a linked project. Others and guests: nothing. | FR-006 |
| OQ-025-08 | New Identity code `organization.engagements.manage` governs edit, values, milestones, contacts, close/reopen and links. | FR-002…005, 007, 008 |
| OQ-025-09 | Active/Closed; closing allowed with active projects, blocks edits and new projects; reopen with reason; no deletion. | FR-007 |
| OQ-025-10 | Vendor-issued single-use link code, 7-day expiry, confirmed by Owner/Admin of the client org via Identity's existing organization check; either side removes. | FR-008/009 |
| OQ-025-11 | Several engagements per client allowed; duplicate active agreement reference → warning only. | FR-001 |
| OQ-025-12 | Agreement-reference change is not a CR; reason mandatory; attributable history. | FR-002 |
| OQ-025-13 | Follow-up, user-selected 2026-11-10: a confirmed client link can be removed by either side while the engagement is Closed; it is the only change a Closed engagement accepts besides reopening. | FR-007, FR-009 |

Follow-up task review, user-selected 2026-11-10: amount and currency may be cleared together, but setting a different currency requires a subsequent request; replacing the designated client PO produces one combined history entry covering both role changes; engagement functions may be published before TASK-DP-025-011, while the feature remains incomplete until its participant summaries and project/administrator visibility ship. See FR-DP-025-003 and tasks 005, 007 and 013.

No open business question blocks task planning. Cross-feature items that remain outside this feature: history retention (NFR-008) and client sharing of engagement data (FEAT-DP-015).

## Evidence and review
**Implementation slice — 2026-11-11:** TASK-DP-025-001 provides persistence; TASK-DP-025-002 adds authorized, idempotent engagement creation with duplicate warnings and atomic history; TASK-DP-025-012 wires independent Projects deployment and passed the full disposable-stack acceptance suite, including real Identity sessions and host mode. Gateway publication remains TASK-DP-025-013. The feature is incomplete until the remaining behavior, including blocked TASK-DP-025-011, ships. This evidence does not change requirement approval statuses.

**Read and maintenance follow-up — 2026-11-11:** TASK-DP-025-003 supplies full-detail list, detail and history; tasks 005 and 006 supply value/effort and milestone changes. Task 008 supplies close/reopen and remains in review until its active-project scenario can be verified against FEAT-DP-004. Project-participant summaries remain blocked under task 011.

[Requirements](../../requirements.md) glossary, BR-003, BR-046, open question 11; [roadmap](../../roadmap.md) VS-02; [workflow discovery](../../deliverypilot-workflow-discovery.md) V3, D1, D4, D5, D8; [SDLC workflow](../../sdlc-workflow.md) LC01; [ADR-001](../../architecture/adr-001-delivery-module-placement.md) D-01, D-03, D-04, D-07, D-08, D-09, D-10.

**Readiness review — 2026-11-09:** the user resolved OQ-025-01…12 in this conversation; OQ-025-13 was added in the design alignment review on 2026-11-10. Internal review checked needs → behaviors → stories, permissions, lifecycle, negative/security/concurrency scenarios and one-way references. `ready` is requirements completeness for source-aware task planning, not client approval, implementation or production readiness.
