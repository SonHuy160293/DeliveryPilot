# Projects Module — Design Decisions

> Status: Decision log. PRJ-01…PRJ-04 were chosen by the user in the ERD design session on 2026-11-09; PRJ-05…PRJ-16 were proposed in that session and confirmed by the user on 2026-11-10. PRJ-17 was selected by the user on 2026-11-10 and closes former open item O-01. PRJ-18…PRJ-22 were selected by the user on 2026-11-10; PRJ-18…PRJ-21 close former open items O-02, O-03, O-06 and O-07, and PRJ-22 adds the FEAT-DP-015 sharing model; each still needs its own prerequisite task in the owning module before the Projects task that uses it (gate G4). None of this sets requirement `approval_ref` or proves implementation.

| ID | Decision | Status |
|---|---|---|
| PRJ-01 | Scope: detail `engagement` and `project` now; `backlog`, `sprint`, `approval` as boundaries only | User-selected |
| PRJ-02 | One participant table for members and guests | User-selected |
| PRJ-03 | One append-only event table per context for history | User-selected |
| PRJ-04 | Durable participant row, reactivated on rejoin | User-selected |
| PRJ-05 | `OrganizationId` on every row, composite FKs | User-selected (confirmed 2026-11-10) |
| PRJ-06 | Brief in a 1:1 table with its own `Version` | User-selected (confirmed 2026-11-10) |
| PRJ-07 | Explicit project `Kind` | User-selected (confirmed 2026-11-10) |
| PRJ-08 | Client PO as a column on Projects | User-selected (confirmed 2026-11-10) |
| PRJ-09 | Engagement client-PO designation as a contact role | User-selected (confirmed 2026-11-10) |
| PRJ-10 | Link codes in their own table, verifier globally unique | User-selected (confirmed 2026-11-10) |
| PRJ-11 | Guest invitations mirror Identity invitations | User-selected (confirmed 2026-11-10) |
| PRJ-12 | No copies of Identity user data | User-selected (confirmed 2026-11-10) |
| PRJ-13 | Idempotent creation via `reliability.IdempotencyRecords` | User-selected (confirmed 2026-11-10) |
| PRJ-14 | Fixed role catalog in code, no role table | User-selected (confirmed 2026-11-10) |
| PRJ-15 | "No active administrator" derived at read time in MVP | User-selected (confirmed 2026-11-10) |
| PRJ-16 | Physical types and index names decided by persistence tasks | User-selected (confirmed 2026-11-10) |
| PRJ-17 | Failed link-code attempts counted in a database table | User-selected (2026-11-10) |
| PRJ-18 | Identity batch membership check for users other than the caller | User-selected (2026-11-10) |
| PRJ-19 | Identity email-match check for guest-invitation acceptance | User-selected (2026-11-10) |
| PRJ-20 | Identity batch user lookup for display names | User-selected (2026-11-10) |
| PRJ-21 | Guest-invitation email mirrors the organization-invitation flow | User-selected (2026-11-10) |
| PRJ-22 | One `ProjectGuestShares` table for everything shared with guests | User-selected (2026-11-10) |

## PRJ-01

**Scope.** Only FEAT-DP-025 and FEAT-DP-004 have `ready` contracts. Designing backlog/sprint/approval tables from business rules alone would fix a schema before its requirements are reviewed. **Consequence:** those contexts appear only as boundary rows in [erd.md](erd.md#future-contexts-boundary-only), naming what they will reference.

## PRJ-02

**One `ProjectParticipants` table with `Kind = Member | Guest`.** Visibility, roles, client-PO assignment and stakeholder links all point at "a person in this project"; one table gives one access check and one FK target. Guest limits (no `ProjectAdministrator`, no management permissions) are enforced in the domain from `Kind`. **Rejected:** separate member and guest tables, which would double every access check and link. **Consequence:** guest-specific columns (`AdmittedViaInvitationId`) are nullable on the shared table.

## PRJ-03

**Append-only `engagement.EngagementEvents` and `project.ProjectEvents`** with event type, subject, actor, UTC time, reason and JSON previous/new details, written in the command transaction (same idea as Identity's `IdentityAuditEvents`). **Rejected:** typed history tables per change kind (many small tables) and row versioning (every query must filter the current row; reasons fit poorly). **Consequence:** history queries filter by event type; event JSON shape per type is defined by the task that writes it. Milestone original date and Product Goal history are answered from columns and events, not extra tables.

## PRJ-04

**One durable row per `(OrganizationId, ProjectId, UserId)`.** Removal, revocation and leaving end the row by status; adding the person again reactivates it and grants fresh roles, while earlier role grants stay revoked. Attribution keeps a stable participant ID. Matches Identity's durable-membership rule (MEM-01). **Rejected:** a new row per stint.

## PRJ-05

**Tenant column everywhere.** Every table carries `OrganizationId`; parents expose `(OrganizationId, Id)` and children use composite FKs, and project children include `ProjectId`. The database then rejects cross-tenant or cross-project links even if application code is wrong (NFR-001, D-07). Cost: wider keys and indexes. Matches Identity.

## PRJ-06

**`ProjectBriefs` 1:1 table**, created with the project. Seven text items up to 4000 characters each would make the `Projects` row wide, and brief edits by BA/PM would conflict on `Projects.Version` with team and status changes. A separate `Version` keeps brief editing independent.

## PRJ-07

**`Projects.Kind = Client | Internal`.** A client project may lose its engagement while Draft (FR-DP-004-002) but must have one to become Active (FR-DP-004-012), so "client" cannot be derived from `EngagementId IS NOT NULL`.

## PRJ-08

**`Projects.ClientProductOwnerParticipantId`** with a composite FK to the participant in the same project. Exactly one client PO (OQ-004-07) is a single nullable column; a flag on participants would need a partial unique index and a cross-row Kind check.

## PRJ-09

**Engagement client-PO designation is `EngagementContacts.ContactRole = 'ClientProductOwner'`** with a partial unique index on active contacts. Designating another contact changes the previous one to another role in the same transaction.

## PRJ-10

**`EngagementLinkCodes` table** keeps every code with its outcome for history and rate-limit review, and a partial unique index keeps one outstanding code per engagement. The verifier is globally unique because the confirming client user does not know the engagement.

## PRJ-11

**Guest invitations reuse Identity's proven invitation shape:** verifier-only proof, `Version`, partial unique pending invitation per email, 7-day expiry, rotation on resend, separate delivery rows for Notification status and issuance limits. The email flow is decided in [PRJ-21](#prj-21).

## PRJ-12

**No copies of Identity user data.** Store `UserId` only; names and emails of users come from Identity when displayed. Avoids stale personal data (NFR-008). Consequence: engagement pages that list administrator names use the batch user lookup of [PRJ-20](#prj-20).

## PRJ-13

**Create-engagement and create-project retries** use the BuildingBlocks `reliability.IdempotencyRecords` table already in the module's initial migration, instead of a per-feature attempt table like Identity's `OrganizationCreationAttempts`.

## PRJ-14

**Project roles are a fixed catalog in code** (`RoleCode` text with a check constraint). Custom project roles are out of scope (OQ-004-04); a role table would add joins without behavior.

## PRJ-15

**"No active administrator" is computed at read time** from active administrator grants and Identity membership. Storing it needs Identity access-change events (TASK-DP-003-021, blocked until a consumer exists). Revisit when that consumer is built (open item O-04).

## PRJ-16

**Physical details belong to the persistence tasks:** PostgreSQL types (e.g. `text` vs `varchar(n)`, `numeric` precision), index and constraint names, and migration order. This document fixes keys, nullability, relationships and named constraints; the first persistence task per context turns them into EF configuration and a migration.

## PRJ-17

**Failed link-code confirmations are counted in `engagement.EngagementLinkAttempts`**, one row per failed attempt, for the per-user rate limit (FR-DP-025-009). The count is correct across several API instances and restarts and matches Identity's database-backed invitation limits. **Rejected:** an in-memory limiter, which resets on restart and counts per instance. **Consequence:** the row must be committed even though the confirmation fails, so it cannot be written inside a command transaction that rolls back on the failure; the table holds no code, verifier or engagement reference.

## PRJ-18

**New internal Identity query: batch membership check.** Input: organization ID and 1–100 user IDs. Output: for each requested user, whether they are an active member of that organization; nothing else (no roles, permissions, membership IDs or reasons), so Projects cannot enumerate members or learn other users' authority. Integration API key, never published. Used for: adding participants and the acceptor-is-a-member rule (FR-DP-004-006/008), the inviter recheck at acceptance (FR-DP-004-008), administrator recovery and the PRJ-15 flag (FR-DP-004-013), and converting a former member on acceptance (OQ-004-15). **Rejected:** calling the existing organization check with a null session, which returns the target's full permission set and is documented only for reauthorizing queued work; it also needs one call per user. Caller authority keeps using the existing organization check. **Task:** [TASK-DP-003-023](../../../features/FEAT-DP-003-organization/tasks/TASK-DP-003-023.md).

## PRJ-19

**New internal Identity query: email match.** Input: user ID and an email. Output: whether the user exists, is active and their primary email equals the email under Identity's own normalization (`User.NormalizeEmail`), the same rule Identity applies to its own invitations; email verification is not required, as in Identity. Projects never receives the user's email (NFR-008). Unknown, inactive and non-matching users give the same `false`. **Rejected:** adding the email to the user lookup DTO, which would hand emails to every caller and duplicate normalization in Projects. **Task:** [TASK-DP-003-023](../../../features/FEAT-DP-003-organization/tasks/TASK-DP-003-023.md).

## PRJ-20

**New internal Identity query: batch user lookup.** Input: 1–100 user IDs. Output: the existing `UserIntegrationDto` (ID, display name, avatar URL, status) for each user found; unknown IDs are omitted rather than failing the batch. No email. Projects calls it at read time and never stores names (PRJ-12). **Rejected:** one call per user against the existing `GET integrations/users/{id}`, and showing IDs only, which would contradict FR-DP-025-006 and OQ-004-08. **Task:** [TASK-DP-003-024](../../../features/FEAT-DP-003-organization/tasks/TASK-DP-003-024.md).

## PRJ-21

**Project guest-invitation email mirrors the organization-invitation flow** ([PRJ-11](#prj-11)):
- new versioned contracts `ProjectGuestInvitationEmailRequestedV1` and `ProjectGuestInvitationEmailStatusV1` in `DeliveryPilot.Notification.Contracts`, producer `projects`;
- the payload (recipient, project name, offered role, inviter's display name, proof) is envelope-encrypted with Notification's public key and staged in the Projects outbox in the invitation's transaction;
- Notification adds a request kind and template, and sends delivery status back to Projects, which updates `ProjectGuestInvitationDeliveries`;
- before sending, Notification asks a Projects internal eligibility endpoint, guarded by a dispatch key, so a cancelled, rotated or expired invitation is not emailed.

**Rejected:** dropping the eligibility check (stale emails may go out), and a generic invitation contract that would rework the delivered organization-invitation flow. **Consequence:** prerequisite work in Notification (contracts, consumer, template, eligibility client) and in Projects (outbox staging, status consumer, eligibility endpoint, dispatch key secret). **Task:** Notification side in [TASK-DP-004-001](../../../features/FEAT-DP-004-project-initialization/tasks/TASK-DP-004-001.md); the Projects side is planned with the FEAT-DP-004 guest-invitation tasks.

## PRJ-22

**One `project.ProjectGuestShares` table** records which items are shared with a project's guests ([ERD](erd.md#projectguestshares-what-guests-may-read-beyond-the-default-view), FEAT-DP-015). Each row has an `ItemType` and a typed nullable target column per type (`BriefItemCode`, `StakeholderId`, `EngagementId`) with a check that exactly the type's column is set, so the database still enforces same-project foreign keys. One durable row per item, reactivated on re-share like participants ([PRJ-04](#prj-04)); history lives in `ProjectEvents` ([PRJ-03](#prj-03)). **Rejected:** `SharedAt`/`SharedBy` columns on each entity, which would bump the brief or stakeholder `Version` on every share and conflict with edits, need a union of tables for "what the client sees", and add columns to each later feature's tables. **Consequence:** a later feature that makes its items shareable (FR-DP-015-009) adds an `ItemType` value, one nullable target column with its foreign key, and one partial unique index here.

## Open items

| ID | Item | Affects | Blocks |
|---|---|---|---|
| O-01 | Resolved 2026-11-10 by [PRJ-17](#prj-17): failed link-code attempts are stored in a table | `engagement.EngagementLinkAttempts` | Nothing |
| O-02 | Resolved 2026-11-10 by [PRJ-21](#prj-21) | Notification contracts and worker; Projects outbox, status consumer, eligibility endpoint | Guest-invitation task until the Notification prerequisite task is done |
| O-03 | Resolved 2026-11-10 by [PRJ-20](#prj-20) | Identity integration contract | Read tasks that show names until the Identity prerequisite task is done |
| O-04 | Stored "no active administrator" flag once Identity access-change events exist | Projects column or projection | Nothing in MVP |
| O-05 | Retention of event history | Both event tables | Nothing now (NFR-008 decision) |
| O-06 | Resolved 2026-11-10 by [PRJ-19](#prj-19) | Identity integration contract | Guest-invitation acceptance task until the Identity prerequisite task is done |
| O-07 | Resolved 2026-11-10 by [PRJ-18](#prj-18) | Identity integration contract | Participant, guest-acceptance and recovery tasks until the Identity prerequisite task is done |
