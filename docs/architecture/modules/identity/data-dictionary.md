# Identity Data Dictionary

> Status: Historical physical-design proposal (2026-11-03), retained for rationale; superseded for implementation inventory.
> Recorded: 2026-11-03, Asia/Bangkok.
> Companion views: [ERD](erd.md), [decisions](design-decisions.md), [review scenarios](review-scenarios.md).

> **Current reference — 2026-11-08:** [Current mapped schema](current-schema.md) documents fourteen mapped business tables, including later replay, invitation-delivery and nomination persistence. The physical diagrams/columns and technical gates below are the original proposal, not a current implementation checklist. Selected conceptual ownership remains valid; PlatformRoleAssignment is not a mapped table.

## Evidence and notation

Existing mappings: [UserConfiguration](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs), [PasswordCredentialConfiguration](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/PasswordCredentialConfiguration.cs), [AuthSessionConfiguration](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/AuthSessionConfiguration.cs).

Migration evidence: [InitialIdentity](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261025052513_InitialIdentity.cs), [RetainUsersOnly](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261101162318_RetainUsersOnly.cs), [AddRegistrationCredentials](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261101173630_AddRegistrationCredentials.cs), [AddAuthSessions](../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Migrations/20261102041840_AddAuthSessions.cs).

Implemented observations do not prove migration application in any deployment. The three Implemented sections retain their existing source-verified definitions. The nine Proposed sections specify draft physical names, columns and integrity under the user's column-design plan. UTC instants use repository DateTimeOffset/timestamptz conventions. No token, hash or other actual secret value is reproduced here.

## Implemented: shared.Users

Aggregate: User. Purpose: stable global identity and profile/account state. No organization or platform grants are stored on User. Primary key: `PkUsers` on Id.

| Column | PostgreSQL type | Nullable | SQL default / integrity | Business meaning |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()`; `CkUsersId` requires UUID version 7 | Stable User identity; application also creates UUIDv7 IDs. |
| PrimaryEmail | varchar(320) | No | Trimmed length 3–320, `CkUsersPrimaryEmail`; expression unique index on lower value | Current primary login address; string alone is not email-control proof. |
| DisplayName | varchar(160) | No | Trimmed length 1–160, `CkUsersDisplayName` | Display identity; not an authorization input. |
| AvatarUrl | text | Yes | No configured SQL default | Optional display metadata. |
| Locale | varchar(16) | No | `en` | Display preference. |
| TimeZone | varchar(64) | No | `UTC` | User display preference; does not change persisted instant semantics. |
| Status | varchar(16) | No | `pending`; `CkUsersStatus` permits pending/active/suspended/deleted | Account state; registration explicitly supplies active. |
| EmailVerifiedAt | timestamptz | Yes | No configured SQL default | Existing optional verification fact; current registration leaves it null. |
| LastLoginAt | timestamptz | Yes | No configured SQL default | Most recent recorded login instant. |
| CreatedAt | timestamptz | No | `now()` | Creation time. |
| UpdatedAt | timestamptz | No | `now()` | Mutation timestamp. Nullable CLR base-property declaration does not make the mapped column nullable. |

Indexes: `UxUsersPrimaryEmailCi` uniquely indexes `lower("PrimaryEmail")`; `IxUsersStatus` indexes Status. The email expression index is created by migration SQL outside ordinary Fluent index metadata. The domain normalizes email with trim/lower-invariant; SQL CHECK does not independently require lower case. Preserve the authoritative case-insensitive uniqueness behavior.

CreatedBy/UpdatedBy are ignored by this entity mapping; do not invent stored audit columns from the base type. Current User has no mapped concurrency/security-version property. A proposed version is an additive design choice, not an existing field. Account erasure, email reuse and tombstone policy remain DATA-02.

## Implemented: shared.PasswordCredentials

Aggregate association: password credential associated with User; registration stages both in one existing command transaction. Purpose: protected password verification, with at most one credential per User.

| Column | PostgreSQL type | Nullable | Integrity | Meaning |
|---|---|---|---|---|
| UserId | uuid | No | `PkPasswordCredentials`; local User FK, delete Restrict | Shared primary key and owning identity. |
| PasswordHash | text | No | `CkPasswordCredentialsPasswordHash`: trimmed nonempty value | Protected encoded verifier; do not log or include in integration DTOs. |
| CreatedAt | timestamptz | No | No configured SQL default | Credential creation instant supplied by application. |

Creation migration names the FK `FK_PasswordCredentials_Users_UserId`; mapping uses EF convention rather than a new explicit FK name. There are no separate algorithm/version/rotation columns in the current table; any verifier-format metadata is not a license to invent columns. Existing Users can have zero credentials. Credential recovery/change and cleanup lifecycles are follow-up scope.

## Implemented: shared.AuthSessions

Aggregate: independent AuthSession. Purpose: authenticated-session identity, current refresh proof, fixed refresh deadline and revocation. User has zero/many sessions.

| Column | PostgreSQL type | Nullable | Integrity | Meaning |
|---|---|---|---|---|
| Id | uuid | No | `PkAuthSessions`; application supplies ID, no configured SQL default | Session identity used by authentication context. |
| UserId | uuid | No | `FkAuthSessionsUsersUserId`; local User FK, delete Restrict | Session subject. |
| CreatedAt | timestamptz | No | No configured SQL default | Session issuance instant. |
| RefreshExpiresAt | timestamptz | No | `CkAuthSessionsRefreshDeadline`: after CreatedAt | Fixed refresh deadline; rotation does not extend it. |
| RefreshTokenVerifier | bytea | No | `CkAuthSessionsRefreshTokenVerifier`: exactly 32 bytes | SHA-256 verifier of current refresh secret. |
| RefreshRotatedAt | timestamptz | Yes | No configured SQL default | Last refresh rotation time. |
| RevokedAt | timestamptz | Yes | No configured SQL default | Revocation time; domain Revoke preserves first value. |

Index: `IxAuthSessionsUserId`. No unique verifier index is established by current mapping/creation migration. IsRevoked is derived and ignored by EF. Current bearer validation checks matching User/session, no revocation and active User; refresh-deadline expiry alone does not revoke a still-valid issued access JWT. Token lifetime validation remains part of bearer authentication.

No OrganizationId, role list, permission list, MFA method or privileged-activation expiry is currently persisted in AuthSession. Do not interpret an existing session as elevated platform assurance.

## Proposed physical design conventions

All nine new tables below belong to the existing Identity database and `shared` schema. This is the technical draft requested by the user, not a deployment change. Root IDs use UUIDv7; new root ID checks can mirror `uuid_extract_version("Id") = 7`. Nullable columns have SQL NULL as their initial value; a dash in Default means no configured SQL default, so required business values must be supplied explicitly. FK denotes a local Identity reference, with delete RESTRICT throughout this draft.

Mutable roots and stable assignment rows have Id, CreatedAt, UpdatedAt and Version, repeated in each table for completeness. SQL `now()` defaults initialize timestamps only; the application updates UpdatedAt and conditionally increments Version on mutation. Version is an application-managed optimistic-concurrency counter, not a PostgreSQL system column or a guarantee of cross-row owner continuity. Relationship children use their parent root's transaction/version; the audit table is append-only.

Identity/scope association columns are immutable after creation; do not move an existing membership, role, assignment or invitation to another scope to bypass its history. Commands supply consistent operation instants when related CreatedAt/LastIssuedAt/grant timestamps are written together; SQL defaults are insertion fallbacks, not an independent business clock.

The column draft adopts code-defined permission identifiers (DATA-03), stable organization member/role assignment rows with grant versions (AUTHZ-06), and one structured audit/history store (DATA-01). AUTHZ-03 requires separate role-management authority plus the actor's own effective, delegable organization permissions. Catalog codes have no Permission-table FK. No Permission, MembershipHistory, separate recipient-verification or role-version table is added.

## Proposed: shared.Organizations

Aggregate: Organization. Purpose: customer workspace ownership, lifecycle and a common owner-continuity guard. OWN-01 permits multiple Owners through role assignments; there is no exclusive OwnerId column.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; stable workspace identity. |
| Name | varchar(160) | No | — | Nonblank trimmed display name, at most 160 characters; duplicate names allowed and no slug required under CREATE-01/U9. |
| Status | varchar(16) | No | `active` | Draft lifecycle values active/suspended/archived; exact allowed operations remain lifecycle policy. |
| CreatedByUserId | uuid | No | — | FK Users.Id; registered creator establishing the initial Owner. |
| StatusChangedAt | timestamptz | No | — | Effective instant of the current lifecycle state, including creation. |
| StatusChangedByUserId | uuid | Yes | NULL | FK Users.Id; nullable for an explicitly audited system/operator action. |
| StatusReason | text | Yes | NULL | Latest lifecycle explanation; required circumstances are domain policy. |
| CreatedAt | timestamptz | No | `now()` | Initial persistence instant. |
| UpdatedAt | timestamptz | No | `now()` | Last mutation instant, application maintained. |
| Version | bigint | No | `1` | Positive root revision; common guard must cover protected Owner changes. |

Checks: supported Status, nonblank Name, positive Version and UUIDv7 Id. PK supports local ownership references. Index `(Status, Id)` supports authorized lifecycle operations; never expose organization lists solely because an actor is authenticated. Creation commits Organization, creator membership, initial Owner assignment and material audit/outbox atomically. Normal owner removal/transfer must serialize on the common Organization guard; compromise containment/recovery remains OWN-03, not a CHECK constraint.

## Proposed: shared.OrganizationMemberships

Aggregate: OrganizationMembership. Purpose: one durable User/Organization relationship across removal and rejoin (MEM-01), with ordered history in IdentityAuditEvents.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; retained across rejoin. |
| OrganizationId | uuid | No | — | FK Organizations.Id; owning workspace. |
| UserId | uuid | No | — | FK Users.Id; global participant identity. |
| Status | varchar(16) | No | `active` | Draft active/suspended/removed state; invitation Pending is not a membership state. |
| StatusChangedAt | timestamptz | No | — | Effective instant of the latest state transition. |
| StatusChangedByUserId | uuid | Yes | NULL | FK Users.Id; system-driven changes require explicit audit actor identity. |
| StatusReason | text | Yes | NULL | Current transition explanation; earlier reasons remain in audit. |
| CreatedAt | timestamptz | No | `now()` | Initial membership establishment, not reset on rejoin. |
| UpdatedAt | timestamptz | No | `now()` | Last state or membership-affecting grant mutation. |
| Version | bigint | No | `1` | Positive ordered membership revision, including role grant/revoke/regrant changes. |

Keys: unique `(OrganizationId, UserId)` across all states; alternate key `(OrganizationId, Id)` for scoped FKs. Indexes `(UserId, Status, Id)` for context selection and `(OrganizationId, Status, Id)` for authorized membership lists. Rejoin reuses Id but cannot restore previous assignments implicitly (MEM-02). Removing membership revokes surviving organization grants in the same local operation; MEM-05/U18 defers temporary suspension/restoration commands/UI while enforcing suspended-state denial. Original membership CreatedAt is the selected join time; rejoin does not reset it. FK existence does not establish current User, membership or organization eligibility.

## Proposed: shared.OrganizationRoles

Aggregate: OrganizationRole. Purpose: scoped, protected built-ins or customer-defined custom role definitions. Physical permission-set versioning is a draft implementation of selected AUTHZ-02/04, not new customer policy.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; stable role identity. |
| OrganizationId | uuid | No | — | FK Organizations.Id; role owner. |
| Code | varchar(80) | No | — | Nonblank stable scoped identifier; built-in codes are reserved by the system. |
| Name | varchar(160) | No | — | Nonblank trimmed display name. |
| Description | text | Yes | NULL | Optional explanation, at most 1,000 characters under AUTHZ-11/U17; no permission effect. |
| Origin | varchar(16) | No | — | Draft values system/custom; customer commands cannot set system origin. |
| TemplateVersion | integer | Yes | NULL | Positive system-template revision for system roles; NULL for custom roles. |
| PermissionSetVersion | bigint | No | `1` | Positive counter incremented whenever permission membership changes, including removals. |
| CreatedByUserId | uuid | Yes | NULL | FK Users.Id; human custom-role creator; system template instantiation can use audited system provenance. |
| RetiredAt | timestamptz | Yes | NULL | Retirement fact; AUTHZ-11/U17 permits retirement only after current assignments/pending offers are cleared, retains history and blocks new use. |
| CreatedAt | timestamptz | No | `now()` | Creation instant. |
| UpdatedAt | timestamptz | No | `now()` | Last definition mutation. |
| Version | bigint | No | `1` | Positive concurrency revision for every mutation, including metadata changes. |

Keys: alternate `(OrganizationId, Id)`; case-insensitive unique `(OrganizationId, lower(Code))` and `(OrganizationId, lower(Name))`. Checks enforce supported Origin, positive counters and system/custom TemplateVersion shape; custom creation has a human creator. Built-in Owner/Admin/Member definitions and reserved codes are system-controlled. Their protection and delegation ceiling require application authorization, not just an Origin column.

Changing only Name/Description increments Version, not PermissionSetVersion. Changing permissions increments both and writes complete new permission-set evidence to audit. Invitations whose offered permission version no longer matches require reissue even if permissions later return to the previous set. AUTHZ-11/12/U17 selects retirement preconditions and rejects stale edits; built-in template changes require separately reviewed upgrades.

## Proposed: shared.OrganizationRolePermissions

Role aggregate child. Purpose: current permission membership of one organization role; catalog semantics remain code-defined.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| OrganizationId | uuid | No | — | Part of scoped Role FK; supports tenant filtering. |
| RoleId | uuid | No | — | PK part; role containing the permission. |
| PermissionCode | varchar(160) | No | — | PK part; registered organization-compatible permission code. |

PK `(RoleId, PermissionCode)` prevents duplicate role permissions. Composite FK `(OrganizationId, RoleId)` references OrganizationRoles `(OrganizationId, Id)`. No extra Id, timestamp or Version; changes use the Role transaction, increment its counters and append audit evidence. No Permission FK or permission table. Unknown, platform-scoped, project-scoped or nondelegable codes are rejected according to the code catalog and caller authority; string storage does not authorize arbitrary executable permission definitions.

## Proposed: shared.OrganizationRoleAssignments

Membership aggregate child with its own conditional-write counter. Purpose: a stable association for one membership/role pair (AUTHZ-06), supporting multiple concurrent roles (AUTHZ-01).

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; stable assignment identity. |
| OrganizationId | uuid | No | — | Scope for both member and role references. |
| MembershipId | uuid | No | — | Scoped FK to the receiving OrganizationMembership. |
| RoleId | uuid | No | — | Scoped FK to OrganizationRole. |
| GrantVersion | bigint | No | `1` | Positive grant-period counter; increment only for a newly authorized regrant. |
| GrantedByUserId | uuid | No | — | FK Users.Id; authorizing grantor/sponsor, including authorized workspace creator or invitation issuer. |
| GrantedAt | timestamptz | No | — | Start of the current grant period. |
| ExpiresAt | timestamptz | Yes | NULL | Optional deadline, strictly after GrantedAt. |
| GrantReason | text | Yes | NULL | Current grant explanation; historical values are audited before replacement. |
| RevokedAt | timestamptz | Yes | NULL | Current-period revocation instant, not before GrantedAt. |
| RevokedByUserId | uuid | Yes | NULL | FK Users.Id; NULL allowed for a separately identified audited system action. |
| RevocationReason | text | Yes | NULL | Current-period revocation explanation. |
| CreatedAt | timestamptz | No | `now()` | First assignment establishment; not reset on regrant. |
| UpdatedAt | timestamptz | No | `now()` | Last grant/revocation mutation. |
| Version | bigint | No | `1` | Positive conditional-write revision for every mutation. |

Unique `(OrganizationId, MembershipId, RoleId)` applies across lifecycle, not only unrevoked grants. Composite FKs `(OrganizationId, MembershipId)` and `(OrganizationId, RoleId)` reference OrganizationMemberships `(OrganizationId, Id)` and OrganizationRoles `(OrganizationId, Id)`. Index `(OrganizationId, RoleId, MembershipId)` supports role-impact and Owner queries. No overlapping episode rows are introduced.

Regrant requires fresh current authorization, increments GrantVersion and Version, writes prior/current evidence to audit, and replaces current grant/revocation metadata. Every assignment mutation also advances Membership.Version and its UpdatedAt in the same transaction. Effective access requires active account/membership, permitted organization lifecycle, eligible role, reached GrantedAt, no revocation and unexpired grant. Expiry is evaluated at use, not dependent on a cleanup job. Regranting an already-effective assignment is not a silent renewal; exact repeated request handling belongs to its idempotent use case.

GrantedByUserId records who authorized the grant. The actual executing actor can differ during invitation acceptance and is recorded separately by the audit envelope. Protected Owner changes use their dedicated continuity policy. An ordinary role-manager permission cannot assign platform power or evade Owner controls.

## Proposed: shared.OrganizationInvitations

Aggregate: OrganizationInvitation. Purpose: one pending offer per organization/normalized email, single-use recipient proof, and terminal acceptance/cancellation history.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; public lookup identifier, not an authorization secret. |
| OrganizationId | uuid | No | — | FK Organizations.Id; intended workspace. |
| RecipientEmail | varchar(320) | No | — | Trim/lower-normalized intended address; no redundant second email column. |
| IssuedByUserId | uuid | No | — | FK Users.Id; offer sponsor/inviter. |
| LastIssuedAt | timestamptz | No | — | Current proof-generation instant; refreshed on resend. |
| ExpiresAt | timestamptz | No | — | Explicit deadline after LastIssuedAt; no assumed seven-day SQL default. |
| TokenVerifier | bytea | No | — | Exactly 32-byte SHA-256 verifier of a random invitation secret; never store raw secret here or in audit. |
| Status | varchar(16) | No | `pending` | Draft pending/accepted/declined/cancelled/expired state. |
| ClosedAt | timestamptz | Yes | NULL | Terminal-transition instant; acceptance time when Status is accepted. |
| ClosedByUserId | uuid | Yes | NULL | FK Users.Id; accepting User for accepted, responder/canceller for other human closures; nullable for expiry/system actions. |
| CloseReason | text | Yes | NULL | Terminal reason, such as reissue; reject secret-bearing content. |
| ReplacesInvitationId | uuid | Yes | NULL | Scoped self-FK to previous invitation when reissued. |
| CreatedAt | timestamptz | No | `now()` | Original issue instant for this offer row. |
| UpdatedAt | timestamptz | No | `now()` | Last proof/lifecycle mutation. |
| Version | bigint | No | `1` | Positive concurrency revision; increment for resend, acceptance and other mutations. |

Keys: alternate `(OrganizationId, Id)`. Partial unique index `(OrganizationId, lower(RecipientEmail)) WHERE Status = 'pending'` enforces selected INV-02 without a clock-dependent predicate. Index `(OrganizationId, Status, ExpiresAt, Id)` supports scoped pending lists/expiry work; `(ReplacesInvitationId)` supports replacement history. Proof lookup uses invitation Id plus a protected verifier comparison; no raw-token index is needed.

Checks: supported Status, trimmed/nonblank bounded address, 32-byte verifier, positive Version, ExpiresAt after LastIssuedAt, LastIssuedAt not before CreatedAt, no self-replacement, and consistent closure shape. Pending has no ClosedAt/ClosedByUserId/CloseReason; terminal states require ClosedAt not before LastIssuedAt. Accepted/declined require ClosedByUserId; expired has no human closer. Cancelled may be human or audited system action. Composite self-FK `(OrganizationId, ReplacesInvitationId)` references `(OrganizationId, Id)` with normal nullable-reference semantics; do not use MATCH FULL, which would reject a root invitation with NULL replacement.

Acceptance requires unexpired pending status, unchanged offered permission versions, valid single-use proof and the authenticated User's matching current normalized primary email. Existing EmailVerifiedAt is not required and is not silently changed. Consume proof by committing terminal status/closure identity together with the authorized membership/grants and audit/outbox. Same-subject retries may return the committed outcome; replay cannot create another grant. INV-04/U10 requires current inviter authority; if lost, block acceptance and require a currently authorized inviter to reissue. INV-08/U16 selects current-authorized already-accepted replay without new grants; INV-07/U14 and REPLAY-01/U21 select duplicate/expired-attempt outcomes.

On reissue, atomically cancel/close the old offer and create a new pending row with a new Id/secret, ReplacesInvitationId and new role offers. The old proof is unusable because the old row is terminal. The replacement must reference an earlier terminal offer, retain the intended organization/recipient and pass fresh authorization; self-FK alone cannot enforce those conditions. ReplacesInvitationId is immutable once created. Resend preserves role intent and rotates TokenVerifier/LastIssuedAt/Version. INV-05/06/U10 select seven days from issue and restart seven days on explicit resend, invalidating prior proof. This behavioral choice adds no SQL lifetime default or new column. Expired-but-pending rows are transitioned before a replacement is inserted; cleanup lag never permits late acceptance.

Delivery status remains Notification/outbox responsibility. LastIssuedAt means proof generation, not successful email delivery. No separate verification challenge table, AcceptedMembershipId or accepted-actor duplicate column is required: ClosedByUserId plus OrganizationId identifies the durable membership when accepted.

## Proposed: shared.OrganizationInvitationRoleOffers

Invitation aggregate child. Earlier discovery called this InvitationRoleOffer. Purpose: preserve each offered role's exact permission intent without a mutable-version FK or a separate role-history table.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| OrganizationId | uuid | No | — | Scope for both invitation and role FKs. |
| InvitationId | uuid | No | — | PK part; parent invitation. |
| RoleId | uuid | No | — | PK part; offered scoped role. |
| OfferedPermissionSetVersion | bigint | No | — | Positive Role.PermissionSetVersion captured at issue; comparison evidence, not a FK to current version. |
| OfferedPermissionCodes | jsonb | No | — | Explicit array snapshot of unique registered permission-code strings offered at issue. |

PK `(InvitationId, RoleId)`; composite FKs `(OrganizationId, InvitationId)` and `(OrganizationId, RoleId)` reference OrganizationInvitations `(OrganizationId, Id)` and OrganizationRoles `(OrganizationId, Id)`. Index `(OrganizationId, RoleId, InvitationId)` supports invitation-impact review. Check JSON array shape and positive offered version; application validates string elements, uniqueness, canonical ordering, catalog scope and exact correspondence to the captured role. JSON syntax/array shape alone does not establish permission validity.

No child Id, timestamps or Version; the parent invitation controls mutation. Permission additions or removals, including change-and-revert, advance the role's PermissionSetVersion and invalidate old offers under AUTHZ-04. Do not FK OfferedPermissionSetVersion to the current role version, since that would prevent legitimate role changes. Keep snapshots immutable for that offer; publish changed intent through reissue. No broad JSON index is proposed for snapshots because normal evaluation uses Id/version and exact scoped references.

## Proposed: shared.PlatformRoleAssignments

Aggregate: PlatformRoleAssignment. Purpose: isolated system-controlled platform authority with common User identity. A stable User/RoleCode pair is a technical proposal in this column draft; detailed platform lifecycle/assurance review remains PLAT-01–03.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; stable platform assignment identity. |
| UserId | uuid | No | — | FK Users.Id; no organization-membership dependency. |
| RoleCode | varchar(80) | No | — | Nonblank code from system-controlled platform role catalog. |
| GrantVersion | bigint | No | `1` | Positive grant-period counter; increment on authorized regrant. |
| GrantedByUserId | uuid | Yes | NULL | FK Users.Id; nullable only when reviewed operator/bootstrap evidence identifies authority. |
| ApprovedByUserId | uuid | Yes | NULL | FK Users.Id; independent approver if required by the reviewed procedure. |
| AuthorizationReference | text | No | — | Nonblank identifier for grant decision/operator authorization; not raw credentials or ticket payload. |
| GrantedAt | timestamptz | No | — | Current grant start. |
| ExpiresAt | timestamptz | Yes | NULL | Optional reviewed expiry after GrantedAt; no invented privileged lifetime. |
| GrantReason | text | No | — | Nonblank justification for the current grant. |
| RevokedAt | timestamptz | Yes | NULL | Revocation at or after GrantedAt. |
| RevokedByUserId | uuid | Yes | NULL | FK Users.Id; external/system revoker must have explicit audit provenance. |
| RevocationReason | text | Yes | NULL | Current revocation explanation. |
| CreatedAt | timestamptz | No | `now()` | First assignment establishment. |
| UpdatedAt | timestamptz | No | `now()` | Last assignment mutation. |
| Version | bigint | No | `1` | Positive concurrency revision on every mutation. |

Unique `(UserId, RoleCode)` across lifecycle; index `(RoleCode, ExpiresAt, Id)` supports restricted privilege/continuity review. Checks enforce positive counters, nonblank required code/evidence/reason and coherent grant/revocation times. Regrant follows the stable-row/audit pattern. Actor references do not prove authorization; bootstrap, independent approval, self-escalation prevention and last-usable-admin controls are domain/security procedures, not optional because a column is nullable.

No OrganizationId, MembershipId, tenant wildcard or platform-role-permission join is included. Platform roles are not customer-customizable. Existing AuthSession is not an elevated session. MFA, activation and recovery remain deferred, so this foundation cannot claim operational privileged-access readiness. Revoking platform authority does not delete legitimate organization memberships.

## Proposed: shared.IdentityAuditEvents

Append-only structured evidence for the new Identity areas. This is the selected membership/role history store (DATA-01), not an event-sourced rebuild model or a replacement for operational outbox persistence. Aggregate identifiers describe history targets; their polymorphic shape is not a generic authorization-grant model.

| Column | PostgreSQL type | Nullable | Default | Key / rule / purpose |
|---|---|---|---|---|
| Id | uuid | No | `uuidv7()` | PK; immutable event identity for deduplication. |
| OrganizationId | uuid | Yes | NULL | FK Organizations.Id for tenant-scoped evidence; NULL for platform/global evidence. |
| MembershipId | uuid | Yes | NULL | Scoped FK to Memberships where an event contributes to membership history. |
| MembershipVersion | bigint | Yes | NULL | Positive parent membership revision for ordering related status/grant events. |
| AggregateType | varchar(80) | No | — | Nonblank allowlisted local target type. |
| AggregateId | uuid | No | — | Stable target identity; no polymorphic database FK. |
| AggregateVersion | bigint | No | — | Positive target revision captured after the committed mutation; never FK to its mutable current Version. |
| EventOrdinal | integer | No | — | Positive event order within that revision; allocated explicitly by application. |
| EventType | varchar(160) | No | — | Nonblank versioned event discriminator defining allowed Details shape. |
| Outcome | varchar(16) | No | — | Draft succeeded/denied/failed result; no fabricated default success. |
| ActorKind | varchar(16) | No | — | Draft user/system/operator authority path. |
| ActorUserId | uuid | Yes | NULL | FK Users.Id; required for user actor, not a fake User for system/operator. |
| ActorReference | varchar(200) | Yes | NULL | Stable nonsecret system/operator identity when ActorUserId is absent. |
| OccurredAt | timestamptz | No | — | Effective event instant supplied by the action. |
| RecordedAt | timestamptz | No | `now()` | Persistence instant; not relied on for causal ordering. |
| CorrelationId | varchar(100) | No | — | Nonblank request/job trace identifier; not a credential. |
| Reason | text | Yes | NULL | Attributable decision explanation where required. |
| DetailsSchemaVersion | smallint | No | `1` | Positive payload-shape revision for EventType. |
| Details | jsonb | No | `'{}'::jsonb` | Allowlisted structured object; never a dump of transport/domain objects. |

Unique `(AggregateType, AggregateId, AggregateVersion, EventOrdinal)` prevents duplicate target events. For membership-linked events, a partial unique `(MembershipId, MembershipVersion, EventOrdinal) WHERE MembershipId IS NOT NULL` gives one explicit parent ordering. Every membership-affecting grant/status mutation advances Membership.Version; an operation changing several assignments allocates distinct ordinals across that parent revision. Allocate ordinals so target and parent uniqueness both hold. A grant event also records its target assignment Version and GrantVersion; these are separate counters with separate meanings.

A transaction can group several child changes into one new parent revision; it cannot change assignments without advancing the parent revision. Role-definition changes have their own Role.Version/PermissionSetVersion events rather than incrementing every assignee's Membership.Version. Effective permission evaluation must therefore read current role definitions too; membership version alone is not a complete permission-cache freshness token.

Checks require positive versions/ordinal, supported actor/outcome values, nonblank discriminators/correlation, JSON object shape, and consistent membership reference/version shape. Either both MembershipId and MembershipVersion are NULL, or both and OrganizationId are explicitly non-NULL. Nullable positive-counter checks do not replace these explicit NULL checks. Composite FK `(OrganizationId, MembershipId)` references OrganizationMemberships `(OrganizationId, Id)`; the separate Organization FK prevents an unknown tenant on organization-only evidence. Platform-only events have NULL OrganizationId, MembershipId and MembershipVersion. User actor requires ActorUserId; system/operator requires nonblank ActorReference and no fabricated User identity. NULL actor references do not mean anonymous privilege.

Indexes `(OrganizationId, OccurredAt, Id)`, `(ActorUserId, OccurredAt, Id)` and `(MembershipId, MembershipVersion, EventOrdinal)` cover scoped audit and ordered history; the last is already provided by the partial unique index for member-linked queries. No duplicate physical index or general Details GIN index is proposed without measured need. Runtime append-only permissions and authoritative write paths protect evidence; append-only is not established by a timestamp or a CHECK alone. DATA-07/U20 selects workspace-lifetime membership/grant history and defers automated purge/erasure; legal/operator request handling is outside this release.

### Audit Details allowlist

These are proposed physical payload contracts for successful material changes. DATA-06/U20 selects allowed actor/display name, scope/target IDs, action/time/outcome/revision and before/after projection, with required nonblank reasons <=1,000 for member removal, Admin/Owner reduction, invitation cancellation and custom-role retirement. AUDIT-01 requires state and required success evidence to fail together; denial telemetry is separate. Use event-specific schemas; prohibit unknown fields, secret/verifier values, raw links, credentials, email bodies and sensitive request payloads. Permission codes and stable local IDs are authorization evidence, not secret authentication material.

| EventType example | Allowed Details fields | Evidence requirement |
|---|---|---|
| `organization.membership.created` / `organization.membership.status-changed` | UserId, PreviousStatus, NewStatus | Initial PreviousStatus can be NULL. Retain actual transition; MembershipId/Version are in envelope. |
| `organization.role-assignment.granted` | RoleId, GrantVersion, GrantedByUserId, GrantedAt, ExpiresAt, PermissionSetVersion | Current grant snapshot and authorizing sponsor; envelope identifies actual actor and assignment Id/Version. |
| `organization.role-assignment.revoked` | RoleId, GrantVersion, GrantedAt, ExpiresAt, RevokedAt | Preserve which period ended; Reason explains action. |
| `organization.role-assignment.regranted` | RoleId, PreviousGrantVersion, GrantVersion, GrantedByUserId, GrantedAt, ExpiresAt, PermissionSetVersion | Earlier grant/revocation events remain unchanged; current assignment row may be replaced in place. |
| `organization.role.created` / `organization.role.permissions-changed` | Origin, TemplateVersion, PreviousPermissionSetVersion, PermissionSetVersion, AddedPermissionCodes, RemovedPermissionCodes, PermissionCodes | Arrays contain unique registered codes. Complete current set enables later interpretation without RoleVersion tables. Initial previous version may be NULL. |
| `organization.invitation.issued` / `organization.invitation.reissued` | IssuedByUserId, OfferedRoleIds, ExpiresAt, ReplacesInvitationId | No raw secret, TokenVerifier or email-body snapshot. Invitation/offer rows hold exact recipient and permission snapshot. |
| `organization.invitation.closed` | PreviousStatus, NewStatus, ClosedByUserId, ClosedAt | Captures acceptance/cancellation/expiry outcome without exposing proof. |
| `platform.role-assignment.granted` / `platform.role-assignment.revoked` / `platform.role-assignment.regranted` | UserId, RoleCode, GrantVersion, PreviousGrantVersion, GrantedAt, ExpiresAt, RevokedAt, AuthorizationReference | Include only fields relevant to that event; independent grant evidence/procedures still required. No tenant membership implied. |

Payload validation distinguishes required fields from event-inapplicable nullable/omitted fields. Store full grant facts at each grant/regrant, not just a pointer to mutable current rows. Role-permission and membership history queries read this one authoritative store; no second MembershipHistory or RoleVersion table is added. Historic evidence availability is bounded by the eventual retention policy, not a promise of indefinite storage.

Material state change, successful audit events and outbox record commit locally together. Denied/failed attempt evidence, if enabled, needs a separate reviewed persistence path because a rolled-back business transaction cannot retain its own failed-attempt audit row. Attempt identity/version/ordinal must not collide with committed domain-mutation events or pretend to advance a membership revision that did not change; operation-scoped attempt evidence requires its own reviewed contract. Initial registration/login durable audit remains deferred. Existing User has no mapped Version, so these new aggregate-version contracts must not be silently applied to existing authentication workflows.

## Enforcement responsibilities

| Invariant | Database candidate | Domain/application rule | Caller authorization |
|---|---|---|---|
| Durable membership | Unique OrganizationId+UserId; local required references | Reactivate same membership; retain ordered history | Joining/restoration needs current authorized intent |
| Owner continuity | Local role/member integrity and concurrency support | Common organization guard across normal removals/demotions/transfers; account changes must consider owner eligibility | Protected Owner operations; compromise containment uses reviewed exceptional recovery |
| Same-organization assignment | Composite role/member scope references | Role lifecycle and current eligibility | Selected AUTHZ-03 requires role-management authority plus held, catalog-delegable organization permissions |
| Fresh role grants on rejoin | Valid assignment references | Former assignments remain ineffective after reactivation until new authorization | Current grantor re-authorizes scope/permissions |
| Invitation acceptance | Persisted state/version, recipient offer uniqueness and local references | Selected single-use proof plus matching signed-in primary email; deadline, one winner and unchanged offer permissions | Current scoped invitation authority and selected sponsor revalidation (INV-04/U10); no stale bypass of suspension/removal |
| Platform separation | Distinct platform assignment representation | Only system-controlled roles; independent validity | Organization APIs cannot grant platform powers; no default content access |
| Revocation | Stored state/version | Authoritative current decision; in-flight policy explicit | Fail closed when required facts unavailable; jobs reauthorize |
| Attribution | Stable identifiers and selected delete restrictions | Preserve historical meaning; secret cleanup distinct from history | Limit audit and identity projection access by scope |

The draft targets the repository's PostgreSQL 18 configuration. PostgreSQL does not support cross-row table-data checks as ordinary CHECK invariants; local unique/FK rules and transaction-protected Owner policy have distinct jobs. See [PostgreSQL 18 constraints](https://www.postgresql.org/docs/18/ddl-constraints.html). Partial uniqueness uses persisted pending state because index predicates cannot depend on the current time; see [CREATE INDEX](https://www.postgresql.org/docs/18/sql-createindex.html). JSON snapshots/details use bounded, event-specific structures rather than arbitrary documents; JSON storage validity alone is not business validation. See [JSON types](https://www.postgresql.org/docs/18/datatype-json.html).

### Scoped local FK map

These seven composite references make scope matching structural. All use delete RESTRICT; direct User/Organization FKs are listed in their table definitions. Candidate target alternate keys are declared on the parent tables above. Version snapshots are deliberately absent from this FK map.

| Source table | Source columns | Target table | Target columns | Nullability rule |
|---|---|---|---|---|
| OrganizationRolePermissions | OrganizationId, RoleId | OrganizationRoles | OrganizationId, Id | Required pair |
| OrganizationRoleAssignments | OrganizationId, MembershipId | OrganizationMemberships | OrganizationId, Id | Required pair |
| OrganizationRoleAssignments | OrganizationId, RoleId | OrganizationRoles | OrganizationId, Id | Required pair |
| OrganizationInvitations | OrganizationId, ReplacesInvitationId | OrganizationInvitations | OrganizationId, Id | Replacement optional; MATCH SIMPLE |
| OrganizationInvitationRoleOffers | OrganizationId, InvitationId | OrganizationInvitations | OrganizationId, Id | Required pair |
| OrganizationInvitationRoleOffers | OrganizationId, RoleId | OrganizationRoles | OrganizationId, Id | Required pair |
| IdentityAuditEvents | OrganizationId, MembershipId | OrganizationMemberships | OrganizationId, Id | Membership optional; reference/version shape checked explicitly |

## Query and index candidates

| Query need | Candidate access path, to review in Stage D |
|---|---|
| User selects one of their organizations | UserId with membership eligibility/state, plus durable pair uniqueness |
| Authorized membership listing | OrganizationId with state and stable pagination key |
| Organization permission evaluation | Current member assignments, scoped role/version and role-permission links |
| Role management | Organization-scoped normalized role code/name uniqueness; PermissionSetVersion for pending-offer comparisons |
| Invitation redemption and pending-list checks | Invitation Id lookup plus protected verifier; pending organization/address uniqueness; expiry cleanup and replacement-history paths |
| Owner continuity | Efficient active eligible Owner lookup, with common concurrency serialization |
| Platform-assignment evaluation | User+system role validity lookup, independent of organization |
| Attributable history | Aggregate version/ordinal and membership version/ordinal uniqueness; organization/actor/time audit paths |

Do not add all possible indexes speculatively. Expired proof cleanup, audit retention and operational deduplication windows affect physical design and must be decided deliberately.

## Compatibility assessment and Stage D checklist

| Area | Assessment / required treatment |
|---|---|
| Existing Users | Preserve Id, fields/defaults/checks and expression index. Registration remains active/unverified; SQL default pending is not changed. Global email reuse/deletion/erasure workflows are deferred in LIFE-01/U19; existing source definitions stay unchanged. |
| Credentials/sessions | Preserve current local FK behavior, fixed refresh deadline, protected verifiers and session checks. Do not add a second credential/session table merely for naming consistency. |
| Migration history | Retain InitialIdentity, reliability/outbox changes, RetainUsersOnly and subsequent credential/session additions. Do not reconstruct removed collaboration tables from rollback as an approved new model. |
| New organization area | Nine proposed new tables now have shared-schema names and column definitions. Same Identity database permits local User FKs. They remain a review draft, with future additive migrations/backfills to plan after policy and architecture review. |
| Platform foundation | Isolated internal authority; local User reference. Protect existing customer sessions from implicit privilege promotion; defer enabling sensitive operations without assurance procedures. |
| External modules | No Project/Delivery/Notification database FK or direct DbContext read. Contracts define availability and revocation behavior. |
| Operational persistence | Preserve existing messaging/reliability tables outside the business ERD. Local audit/outbox consistency does not require calling email providers inside a transaction. |
| Deployment | Source documents separate executables/databases; this design changes neither hosts nor project references. No live deployment inspected. |

The nine table definitions specify proposed names, types, nullability/defaults and integrity. The design is not production-final: physical nomination representation, authorization interfaces, mappings/migrations, privileged bootstrap/assurance/recovery and exact deployment/use-case contracts remain technical/operational gates. U11–U21 resolve Organization business behavior and explicitly defer suspension/restoration, lifecycle deletion and automated purge/erasure. INV-04/05/06 now select current sponsor authority, seven-day expiry and restarted seven-day resend. Compare dictionary, Mermaid and actual migration history before later implementation; preserve the existing email index and credential/session contracts. No existing history is rewritten and no migrations are implemented here.
