---
id: FEAT-DP-015
title: "Client Portal"
status: ready
priority: P0
release: "MVP"
depends_on: ["FEAT-DP-004", "FEAT-DP-025"]
---
# FEAT-DP-015 — Client Portal

> **Status:** Feature contract drafted 2026-11-10 with `dp-feature-engineer`; the user resolved all nine business questions the same day (decision record below). UR/FR/US are `ready`: internally complete for task planning, **not** client-approved (`approval_ref: null`) and **not implemented**. [ADR-001](../../architecture/adr-001-delivery-module-placement.md) places project/guest access and sharing grants in `DeliveryPilot.Projects`, `project` context; the portal's frontend shape is not decided there. The sharing model is `project.ProjectGuestShares` in the [Projects ERD](../../architecture/modules/projects/erd.md#projectguestshares-what-guests-may-read-beyond-the-default-view) ([PRJ-22](../../architecture/modules/projects/design-decisions.md#prj-22), user-selected 2026-11-10); nothing is implemented.

## Problem and business outcome
Client Product Owners and stakeholders take part in a vendor's project as restricted guests ([FEAT-DP-004](../FEAT-DP-004-project-initialization/feature.md)). Being admitted to a project must not show them the vendor's internal working material, and the vendor needs one deliberate, attributable way to decide what the client sees (BR-003, BR-004, BR-055). **Agreed direction:** workflow Q3 and Q12, workflow §4.3, product definition ("restricted guests with resource-scoped access through the Client Portal"). That vendors and clients want this exact split is a hypothesis to validate with pilots.

**Intended outcome:** a guest signs in, finds the projects they were admitted to, and sees a defined default view plus only what the vendor explicitly shared; a vendor Project Administrator or PM shares and withdraws items and can always see what the client currently sees; nothing unshared is reachable by any path.

## Actors and scope
| Actor | Role in this feature |
|---|---|
| Client guest (any guest role) | Uses the portal; sees the default view and shared items of projects they are admitted to |
| Client Product Owner (guest) | Same view; decision authority comes from the FEAT-DP-004 assignment, not from visibility |
| Project Administrator / member PM | Shares and withdraws items; reviews what guests see |
| Other vendor participants | Unaffected; they keep their role-based access |
| Workspace Owner / security reviewer | Relies on the no-leak guarantee; has no extra access |

**In scope (this contract):** the guest's portal home and default project view; one common sharing rule set (internal by default, explicit share and withdraw, attributable, enforced on every read); its first use on what exists in VS-02: brief items, stakeholder entries and an engagement summary; the vendor's view of what is shared.

**Out of scope, owned by later feature contracts that must plug into these rules:** publication of requirement revisions and client-visible discussions (FEAT-DP-006/007, workflow Q10/Q12); backlog items (FEAT-DP-008); Sprint Review acceptance (FEAT-DP-026); delivery health and risks (FEAT-DP-027/010/001, BR-055); traceability navigation (FEAT-DP-017); change requests raised by clients (FEAT-DP-019). Also out: guest admission, roles and revocation (FEAT-DP-004); a client organization's own workspace features (ADR-001 D-09: a client link grants nothing); notifications about shared items; exports.

## Feature policy and constraints
- BR-003, BR-004, BR-008, BR-019, BR-055; requirements validation use case "Agency A / Client B"; critical journeys 2 and 9.
- Visibility, review assignment and approval authority are separate facts (workflow §4.3, product definition). This feature grants visibility only.
- ADR-001 D-07: tenant and project filters first; guests never need workspace membership. FEAT-DP-004 access rule: an active guest participation is required for everything here.
- A guest's project role still gives the work permissions of the FEAT-DP-004 role matrix (a guest PM or BA on the brief, a guest PM on stakeholders), independent of sharing.
- NFR-001, NFR-002, NFR-003, NFR-008.

## Key journeys and edge cases
1. Guest signs in → sees their projects → opens one → sees overview, Product Goal, the team and the shared items.
2. PM shares two brief items and a stakeholder entry → guest sees them on the next request → PM withdraws one → it disappears.
3. PM with full engagement detail shares the engagement summary → guests see client name, engagement name, agreement reference and milestones, never money, effort or contacts.
4. PM opens "what the client sees" and the sharing history.
5. Security: guessing IDs of unshared items, other projects or other engagements; a revoked guest; a guest of project P asking about project Q; counts, search and derived summaries revealing unshared content; mentions granting nothing.

## Artifact index
- URs: `ur/UR-DP-015-001` … `UR-DP-015-004`
- FRs: `fr/FR-DP-015-001` … `FR-DP-015-009`
- USs: `us/US-DP-015-001` … `US-DP-015-007`

Run `python tools/docs.py coverage --feature FEAT-DP-015` for traceability. No tasks are planned yet; the data model passed gate G3 on 2026-11-10 (PRJ-22).

## Dependencies and decisions
**Depends on:** FEAT-DP-004 (guest participation, role matrix, project reads; its tasks TASK-DP-004-004, 005 and 013 define the member-side reads this feature narrows for guests) and FEAT-DP-025 (engagement summary; FR-DP-025-006 reserves guest visibility of engagement data to this feature). **Used by:** FEAT-DP-006/007, 008, 010, 017, 026, 027, which must declare their shareable item types against FR-DP-015-009.

### Decision record — 2026-11-10
User-selected answers in this conversation. They are decision evidence for the stated choices only; they do not set `approval_ref`.

| ID | Decision | Affects |
|---|---|---|
| OQ-015-01 | This contract owns the portal view and one common sharing rule set, applied now to VS-02 content; later features plug their item types into the same rules. | All |
| OQ-015-02 | Default guest view: project name, purpose, status, Sprint length, Product Goal, own role, client PO. The rest of the brief and the stakeholder register are internal until shared, except for the access a guest's role gives under FEAT-DP-004. | FR-002, FEAT-DP-004 tasks 005 and 013 |
| OQ-015-03 | A guest sees the whole team: every active participant's name and roles, including other guests. | FR-002 |
| OQ-015-04 | A vendor user may share with a project's guests the engagement's client display name, engagement name, agreement reference and milestones. Contract value, effort, contacts, legal name, country, document link, link state and history are never shareable. | FR-006, FR-DP-025-006 |
| OQ-015-05 | Sharing granularity: each of the six brief items separately; each stakeholder entry separately; the engagement summary as one unit per project. | FR-003, FR-006 |
| OQ-015-06 | Share authority: Project Administrator and member PM; never a guest. The engagement summary additionally needs full-detail visibility of the engagement. | FR-003, FR-006 |
| OQ-015-07 | A shared VS-02 item shows its current content (live), not a frozen copy. Frozen, revision-bound publication stays with requirements (workflow Q10). | FR-005 |
| OQ-015-08 | A share reaches every current and future guest of the project; no per-guest sharing in MVP. | FR-003 |
| OQ-015-09 | No email or in-app notification when something is shared or withdrawn in MVP. | Scope |

No open business question blocks task planning. Smaller rules written by this contract as drafting choices, shown to the user but not separately selected: guests are not shown who shared an item; a shared stakeholder entry hides its email and participant link; milestone date-change reasons are not shown to guests; the vendor workspace name is not shown in the portal. Still open outside this feature, from [requirements](../../requirements.md) §11: question 3 (which additional resources are shared), 11 (which monetary and effort fields are client-shared; this contract shares none) and 12 (default sharing of risk and health; owned by the Risk features under BR-055). The portal's frontend shape is undecided (ADR-001 "does not decide").

## Evidence and review
[Requirements](../../requirements.md) glossary, BR-003, BR-004, BR-055, validation use case and critical journeys 2 and 9; [roadmap](../../roadmap.md) FEAT-DP-015 row, VS-02 and VS-08; [workflow discovery](../../deliverypilot-workflow-discovery.md) §4.3, Q3, Q12 and the feature mapping; [product definition](../../product.md); [ADR-001](../../architecture/adr-001-delivery-module-placement.md) D-01, D-07, D-09.

No customer interview is claimed.

**Readiness review — 2026-11-10:** the user resolved OQ-015-01…09 in this conversation. Internal review checked needs → behaviors → stories, default visibility against the FEAT-DP-004 role matrix, share and withdraw lifecycle, negative and security scenarios and one-way references. `ready` is requirements completeness for source-aware task planning, not client approval, implementation or production readiness. The sharing entity was added to the Projects ERD on 2026-11-10 (PRJ-22), so gate G3 is passed.
