---
id: FEAT-DP-004
title: "Project Initialization"
status: ready
priority: P0
release: "MVP"
depends_on: ["FEAT-DP-002", "FEAT-DP-003", "FEAT-DP-025"]
---
# FEAT-DP-004 — Project Initialization

> **Status:** Feature contract drafted 2026-11-09 with `dp-feature-engineer`; all open business questions were resolved by the user in the joint review on 2026-11-09 (decision record below). UR/FR/US are `ready`: internally complete for task planning, **not** client-approved (`approval_ref: null`) and **not implemented**. [ADR-001](../../architecture/adr-001-delivery-module-placement.md) places this feature in `DeliveryPilot.Projects`, `project` context, including project/guest access used by FEAT-DP-015; only the module skeleton exists. Roadmap slice VS-02 with [FEAT-DP-025](../FEAT-DP-025-client-engagement/feature.md).

## Problem and business outcome
Vendors need to start a Scrum project for a signed fixed-price engagement with a clear owner workspace, a known administrator, the right team and the client PO admitted as a scoped guest. Workspace membership must not expose every project, and client people must not become vendor workspace members (BR-003). **User-confirmed:** Q2/Q3/Q5/Q6, Q7/Q8/Q24/Q25, D7, V3/BR-046. The underlying vendor pain is a hypothesis to validate with pilots.

**Intended outcome:** an authorized member creates a Draft Scrum project under an engagement (or internal), becomes its administrator, captures the brief and Product Goal, adds the team with project roles, admits the client PO as a guest who sees only that project, assigns them as client PO and marks the project Active.

## Actors and scope
| Actor | Role in this feature |
|---|---|
| Workspace member with `organization.projects.create` | Creates projects; becomes initial Project Administrator |
| Project Administrator | All project management, client-PO assignment, initialization, administrator delegation |
| Member PM | Brief, cadence, stakeholders, participants and guests |
| BA / Developer / QA / DevOps / Viewer | Work roles; brief edit for BA; others read until backlog/Sprint features add permissions |
| Client guest | Any non-administrator role's work permissions; never manages people or configuration |
| Client Product Owner (guest) | Client decision authority through the project assignment |
| Workspace Owner | No implicit project access; audited recovery of orphaned projects only |

**In scope:** project creation (Scrum only); engagement association; brief and Product Goal; Sprint length; role matrix; participants; visibility; guest invitation, acceptance, revocation and leaving; client PO assignment; stakeholder register; Draft→Active; administrator delegation and recovery.

**Out of scope:** guest sharing inside a project (FEAT-DP-015); backlog (FEAT-DP-008); Sprints and the one-active-Sprint rule (FEAT-DP-009); sign-off (FEAT-DP-007); structured discovery (FEAT-DP-005); project closure (FEAT-DP-030); custom project roles; multiple teams per project; Product entity; RACI, communication calendar and kickoff record; project co-ownership.

## Feature policy and constraints
- BR-001, BR-003, BR-004, BR-005, BR-006, BR-007, BR-008, BR-019, BR-027, BR-028, BR-046; requirements "Project creation/access" rule.
- ADR-001 D-03 (`organization.projects.create` exists, [TASK-DP-003-020](../FEAT-DP-003-organization/tasks/TASK-DP-003-020.md); project roles and guests are Projects-owned), D-04, D-07, D-08 (local FK Project→Engagement), D-09.
- NFR-001, NFR-002, NFR-003, NFR-004, NFR-008.

## Key journeys and edge cases
1. Create Draft project under engagement → brief and Product Goal → Sprint length → add team → invite client PO → client PO accepts → assign client PO → mark Active.
2. Internal project without engagement.
3. Participant removal, guest revocation and guest leaving keep history; workspace removal denies access immediately.
4. Last-administrator protection and Owner recovery of an orphaned project.
5. Security: member/Owner without participation, engagement reader, cross-tenant ID guessing, wrong account redeeming an invitation, guest PM trying to manage people, Identity unavailable.

## Artifact index
- URs: `ur/UR-DP-004-001` … `UR-DP-004-006`
- FRs: `fr/FR-DP-004-001` … `FR-DP-004-013`
- USs: `us/US-DP-004-001` … `US-DP-004-012`
- Tasks: `tasks/TASK-DP-004-001` … `TASK-DP-004-017`, planned 2026-11-10 with `dp-task-planner`. TASK-DP-004-001 is the Notification prerequisite (PRJ-21); the Identity prerequisites are TASK-DP-003-023 and TASK-DP-003-024; TASK-DP-004-002 also depends on TASK-DP-025-001

Run `python tools/docs.py coverage --feature FEAT-DP-004` for traceability. Cross-feature dependency: US-DP-004-001 needs FEAT-DP-025 US-DP-025-001 (not expressible in `depends_on`).

## Dependencies and decisions
**Depends on:** FEAT-DP-002 (guest accounts), FEAT-DP-003 (workspace, membership, entry codes, Identity authority check TASK-DP-003-019), FEAT-DP-025 (engagement; the engagement-change rule uses the new `organization.engagements.manage` code), Notification (invitation email). **Used by:** FEAT-DP-015, FEAT-DP-007/026, FEAT-DP-008/009/011, Risk module project-access query (ADR-001 D-05). Participation cleanup on workspace removal waits for TASK-DP-003-021; access is already denied by the per-request check.

### Decision record — joint review 2026-11-09
User-selected answers in this conversation. They are decision evidence for the stated choices only; they do not set `approval_ref`.

| ID | Decision | Affects |
|---|---|---|
| OQ-004-01/02 | Engagement chosen at creation from engagements the creator sees with full detail; change/removal only while Draft by a Project Administrator who holds `organization.engagements.manage`; fixed once Active. | FR-002 |
| OQ-004-03/13 | Name trimmed 1–160, duplicates allowed; purpose required ≤500; project-first, no Product entity. | FR-001 |
| OQ-004-04 | Fixed template roles, additive, no custom roles; matrix in FR-DP-004-005. | FR-003…006, 011…013 |
| OQ-004-05 | Guest invitations mirror Identity invitations (single-use link, matching email, 7 days, rotation, reissue on role change, inviter recheck, guests may leave). | FR-008, FR-010 |
| OQ-004-06 + follow-up | Guests may hold any role except Project Administrator; they never get participant, guest, role, client-PO, cadence, initialization or administrator powers. Members always join as participants. | FR-005, FR-008 |
| OQ-004-07 | Exactly one client PO per project, assigned from admitted guests; engagement designation is only the default. | FR-009 |
| OQ-004-08 | Engagement read-all/manage holders see project name, status and administrator names only. | FR-007 |
| OQ-004-09/14 | Workspace removal denies access on the next request; last administrator protected; audited Owner recovery appoints a member participant without giving the Owner content access. | FR-006, FR-013 |
| OQ-004-10 | Draft/Active; requires Product Goal, Sprint length, a PM, and an engagement for client projects; missing client PO waivable with reason. | FR-012 |
| OQ-004-11/12 | Basic stakeholder register (no RACI/calendar/kickoff); Sprint length 1–4 weeks, default 2, changes affect only unstarted Sprints. | FR-004, FR-011 |
| OQ-004-15 | Follow-up, user-selected 2026-11-10: when a former workspace member who still has an active member participation accepts a guest invitation, acceptance ends that participation and admits them as a guest with the offered role in one step. | FR-008 |
| OQ-004-16 | Follow-up, user-selected 2026-11-10: a Draft project can switch between client and internal under the same authority as an engagement change, with a reason; switching to internal removes the engagement; fixed once Active. | FR-002 |
| OQ-004-17 | Follow-up, user-selected 2026-11-10: an admitted guest's non-administrator roles are changed directly by a Project Administrator or member PM; a new invitation is needed only to change a pending offer. | FR-005, FR-008 |
| OQ-004-18 | Task planning, user-selected 2026-11-10: the full project change history is read only by Project Administrators and member PMs; every participant still sees previous Product Goal values and the activation waiver. | FR-005 |
| OQ-004-19 | Task planning, user-selected 2026-11-10: the guest-invitation email shows the project name, the offered role and the inviter's display name, looked up from Identity when the invitation is issued and not stored. | FR-008 |

No open business question blocks task planning.

## Evidence and review
[Requirements](../../requirements.md) glossary, BR-003, BR-046 and "Project creation/access"; [roadmap](../../roadmap.md) VS-02; [workflow discovery](../../deliverypilot-workflow-discovery.md) §2.2, §4, §5, D7; [SDLC workflow](../../sdlc-workflow.md) LC01; [ADR-001](../../architecture/adr-001-delivery-module-placement.md) D-01, D-03, D-04, D-05, D-07, D-08, D-09.

**Readiness review — 2026-11-09:** the user resolved OQ-004-01…14 and the guest-PM follow-up in this conversation; OQ-004-15…17 were added in the design alignment review on 2026-11-10, and OQ-004-18/19 during task planning on the same day. Internal review checked needs → behaviors → stories, the role matrix, lifecycle, negative/security/concurrency scenarios and one-way references. `ready` is requirements completeness for source-aware task planning, not client approval, implementation or production readiness.
