# FEAT-DP-003 backend implementation plan

Planned 2026-11-03, Asia/Saigon using dp-task-planner. The user selected backend tasks only after repository inspection found no frontend project/package manifest. This plan writes documentation only; no application source, migration, test or deployment was executed or changed.

Source contract: 8 UR, 14 FR and 9 US are internally ready with null approval references and 88 acceptance scenarios. The [feature decision disposition](feature.md#7-open-decisions-and-task-planning-blockers) and Identity U9–U21 own business intent. Ready is internal completeness, not client approval. Planned APIs/types/tables below are new technical proposals consistent with inspected source, never claims they currently exist.

## Task order and dependency gates

The table is generated from canonical TASK metadata at planning time; the Task files own their contracts and forward dependencies. No reverse metadata is added to UR/FR/US. TASK-DP-003-001 is the existing stable creation task, now source-inspected/replanned rather than duplicated. No previously approved/in-progress task was overwritten.

| Task | Deliverable | Current status | Prerequisites | Routing hint |
|---|---|---|---|---|
| [TASK-DP-003-001](tasks/TASK-DP-003-001.md) | Create workspace with initial Owner and replay protection | done | TASK-DP-003-002, TASK-DP-003-003, TASK-DP-003-004 | add-cqrs-api-feature |
| [TASK-DP-003-002](tasks/TASK-DP-003-002.md) | Persist workspace membership and role foundations | done |  | create-ef-entity |
| [TASK-DP-003-003](tasks/TASK-DP-003-003.md) | Record atomic membership and role audit history | done | TASK-DP-003-002 | general agent |
| [TASK-DP-003-004](tasks/TASK-DP-003-004.md) | Enforce current workspace authority and protected-owner concurrency | done | TASK-DP-003-002 | general agent |
| [TASK-DP-003-005](tasks/TASK-DP-003-005.md) | Expose current workspace and member projections | done | TASK-DP-003-004 | add-cqrs-api-feature |
| [TASK-DP-003-006](tasks/TASK-DP-003-006.md) | Manage custom roles and constrained assignments | done | TASK-DP-003-003, TASK-DP-003-004, TASK-DP-003-016, TASK-DP-003-017, TASK-DP-003-018 | add-cqrs-api-feature |
| [TASK-DP-003-007](tasks/TASK-DP-003-007.md) | Persist invitation lifecycle proof and issuance limits | done | TASK-DP-003-003, TASK-DP-003-004, TASK-DP-003-006 | general agent |
| [TASK-DP-003-008](tasks/TASK-DP-003-008.md) | Deliver current invitation proof through Notification safely | done | TASK-DP-003-007 | add-cqrs-api-feature |
| [TASK-DP-003-009](tasks/TASK-DP-003-009.md) | Expose invitation management APIs | done | TASK-DP-003-006, TASK-DP-003-007, TASK-DP-003-008 | add-cqrs-api-feature |
| [TASK-DP-003-010](tasks/TASK-DP-003-010.md) | Accept decline and preview invitations with fresh-role rejoin | done | TASK-DP-003-009 | add-cqrs-api-feature |
| [TASK-DP-003-011](tasks/TASK-DP-003-011.md) | Remove memberships and reduce protected roles safely | blocked: dependencies | TASK-DP-003-003, TASK-DP-003-004, TASK-DP-003-005, TASK-DP-003-006 | add-cqrs-api-feature |
| [TASK-DP-003-012](tasks/TASK-DP-003-012.md) | Nominate and accept additional Owners | blocked: dependencies | TASK-DP-003-003, TASK-DP-003-004, TASK-DP-003-005, TASK-DP-003-011 | add-cqrs-api-feature |
| [TASK-DP-003-013](tasks/TASK-DP-003-013.md) | Query permitted workspace audit history | pending | TASK-DP-003-003, TASK-DP-003-004 | add-cqrs-api-feature |
| [TASK-DP-003-014](tasks/TASK-DP-003-014.md) | Publish Organization routes and verify backend journeys | blocked: dependencies | TASK-DP-003-001, TASK-DP-003-005, TASK-DP-003-006, TASK-DP-003-008, TASK-DP-003-009, TASK-DP-003-010, TASK-DP-003-011, TASK-DP-003-012, TASK-DP-003-013, TASK-DP-003-015 | general agent |
| [TASK-DP-003-015](tasks/TASK-DP-003-015.md) | Retire custom roles with invitation and assignment protection | blocked: dependencies | TASK-DP-003-006, TASK-DP-003-007, TASK-DP-003-009 | add-cqrs-api-feature |
| [TASK-DP-003-016](tasks/TASK-DP-003-016.md) | Honor persisted built-in role permission versions | done | TASK-DP-003-002, TASK-DP-003-004 | general agent |
| [TASK-DP-003-017](tasks/TASK-DP-003-017.md) | Pin permission classification to grant policy version | done | TASK-DP-003-016 | general agent |
| [TASK-DP-003-018](tasks/TASK-DP-003-018.md) | Serialize workspace mutation and session revocation | done | TASK-DP-003-004 | general agent |

Execution order: 002 first; then 003/004; 016 after 002/004, 017 after 016, and 018 after 004; 001/005/013 as their dependencies finish; 006 after 003/004/016/017/018 and a fresh readiness review; 007 then 008 then 009 then 010; 015 after 006/007/009; 011 after its foundations, then 012; 014 last after 015. Task 006 does not expose retirement; task 015 supplies the complete guarded retirement endpoint after invitation persistence and issuance exist. Branches may overlap only when dependencies permit; coordinate shared DbContext/snapshot/DI/controller/test edits rather than claiming file-level independence. Blocked tasks contain concrete plans but are not executable until dependencies are done and a fresh review changes their status to pending. The executor cannot silently flip a blocked task.

## Shared backend contract conventions

These are selected technical planning defaults for new endpoints, not existing routes or product SLO promises. Each API Task owns its exact action/DTO contract.

- Public organization APIs require explicit bearer Authorize; the inspected ApiController base has Authorize commented out. Use ICurrentUserProvider only for server subject/session identity, then current scoped database authority. Do not trust supplied actor IDs, JWT tenant/permission claims or platform assignments as workspace access.
- API-local closed JSON requests use the existing JsonUnmappedMemberHandling.Disallow pattern, camelCase fields and separate Application messages. Unknown fields, empty GUIDs, invalid versions, duplicate identifiers/codes where a set is required and malformed bodies fail 400. Timestamps/deadlines/issuers come from the server. Body ceiling 16,384 bytes follows current auth/Gateway convention; no client chooses tenant or initial Owner.
- Reuse ApiResponse {success,data,error} and ApiErrorResponse with safe code/message/validation/traceId. Do not double-wrap. Throw current supported DomainException types for the intended safe status; never access failed Result.Value or let base HandleFailure convert every policy failure to 400. 200 responses are used for mutations instead of 204 because the existing envelope filter rewrites 204 to 200. Creation uses 201 without a fake CreatedAtAction.
- Every private result uses Cache-Control no-store. No caller identity ->401. Unestablished scope/action ->generic 403 Organization.AccessDenied with no existence/title/email/role leakage; 404 only after scope and target-read authorization. Current required store unavailable ->503; existing bearer-validation fail-closed behavior remains unchanged. Stale differing intent/closed state ->409. Rate limit ->429 Invitation.RateLimited with Retry-After; explicitly construct the safe error envelope since there is no existing rate-limit exception mapping. Unexpected persistence failure ->safe 500, no partial success.
- Example illustrative envelope: {"success":true,"data":{"changed":false},"error":null}. Example safe denial: {"success":false,"data":null,"error":{"code":"Organization.AccessDenied","message":"Access is not permitted.","traceId":"example-correlation"}}. Actual data fields are specified per API Task. Never put proofs, credentials or caller-submitted sensitive values in errors/logs/examples.
- Identity paths start /api/v1; Gateway publishes only explicitly listed method/path entries under /identity. No private dispatch/integration or wildcard module forwarding. New controller/type/path names in the tasks are planned additions under the verified project directories, not existing source files.
- Lists use limit default50/max100; a bounded base64url keyset cursor over UTC time plus stable ID, parsed/validated as untrusted input (max1,024 characters). Per-page authorization and OrganizationId predicates always apply. Stable sort is not a snapshot/causal-order guarantee. Member list filters/fields and history version ordering follow selected rules. No unrestricted search/sort/export endpoint is added.
- expectedVersion/expectedMembershipVersion are positive long request fields for mutable existing intent. Reauthorize current facts before deciding no-op, stale conflict or transition. Identical already-applied intent may return currently permitted no-change; differing stale intent conflicts. Hashes/versions never grant authority by themselves.
- Pipeline transaction owns save/commit. All workspace security writers use the same organization serialization guard; new workspace creation instead uses the user/creation-attempt guard before initial ownership exists. Audit and local encrypted outbox staging share the same commit. No provider/network call while holding a business mutation transaction.

## Capability catalog mapping

Planned protected built-in role codes are owner, admin and member, with display names Owner, Admin and Member and system origin; custom roles use the stable generated code specified in task 006. These codes are technical planning names, not claims of existing seeded data. Planned code-controlled permission names map to already selected capabilities; no Permission table or extra user policy is introduced. Built-in Owner/Admin get all ordinary codes; Member has own-membership/roles and current workspace discovery intrinsically. Explicit delegates receive only the relevant ordinary codes through valid custom roles and the held/delegable ceiling.

| Capability | Planned registered code / restriction |
|---|---|
| Member list/detail | organization.members.read |
| Ordinary membership removal | organization.members.remove; protected Admin/Owner and target-power restrictions override it |
| Invitation inspect/issue/resend/reissue/cancel | organization.invitations.read / issue / resend / reissue / cancel, separately checked |
| Custom role definitions | organization.roles.manage |
| Ordinary assignments/revocations | organization.roles.assign, with Admin reduction Owner-only |
| Audit history | organization.audit.read |
| Protected ownership | organization.ownership.manage is nondelegable, system-Owner-only; do not infer it from arbitrary code/name |

Offering/granting roles also requires role-management authority and held, catalog-delegable powers. Browsing role definitions/catalog for management/offers is limited to those authorized operations, not a new public directory. No project-content or platform code enters this catalog. Future Project creation/resource policy stays in FEAT-DP-004/015.

## Important implementation boundaries

- At initial planning, mapped Identity business state was User/PasswordCredential/AuthSession. Completed foundation tasks now map the core organization, audit and invitation models; task 009 implements the manager invitation workflow. Additive migrations preserve old history and the Users expression index; root namespace configuration discovery is exact. Never auto-map the deferred PlatformRoleAssignment simply because a class exists.
- New physical needs are explicit per task: creation receipt in 001, core mappings in 002, audit in 003, invitation generation/status ledger in 007 and nomination storage in 012. Existing dictionary diagrams remain proposals, and these task additions are not deployed schema.
- Existing EmailDeliveryRequestedV1 accepts verification purpose/template and at most one day. 008 adds a distinct versioned invitation contract and private eligibility check; it does not loosen V1. An intake receipt is not provider delivery. Provider budgets/idempotency windows remain distinct from invitation issuance limits and seven-day proof validity.
- Before each delivery dispatch, validate current proof generation; a provider call already admitted cannot be recalled after a later cancellation. Such old proof still cannot grant membership. Monotonic per-delivery outcomes cannot overwrite newer-generation state.
- Frontend storage/selector rendering, the reserved email-link browser page and actual Project/guest admission are external handoffs. Backend tasks supply eligible projections and no implicit grants, but do not claim these future user interfaces/resource modules are implemented or fully tested. The aggregate final task maps all 88 ACs to backend evidence or an explicit external handoff.
- Lifecycle restoration/deletion, operator recovery, platform administration, global account email reuse and automatic history purge/erasure are excluded as selected, with denied-state/containment rules enforced. Production operating assurance remains separate from coding-plan readiness.

## Verification and execution gates

Executor reopens the selected Task, current source and referenced ready requirements, then runs python tools/docs.py check-task --id TASK-DP-003-NNN using the real selected ID. The original dependency analysis below is historical; current task statuses and current source determine readiness. Re-run source review before unblocking dependent tasks. No approval_ref or source requirement status is changed by this plan.

Task 009 completion was verified on 2026-11-04 against disposable PostgreSQL 18: the full Identity suite passed 143 tests with zero skips, Gateway regression passed 57 tests, the Identity API build passed, and architecture checks passed for 21 projects. Its controller exposes direct Identity manager routes; Gateway publication remains task 014. Its task file records the final contract and detailed evidence. No new migration or broker contract was needed.

Existing test/build targets verified in the checkout:

~~~text
dotnet build src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/DeliveryPilot.Identity.API.csproj
dotnet test tests/DeliveryPilot.Identity.Tests/DeliveryPilot.Identity.Tests.csproj
dotnet test tests/DeliveryPilot.Notification.Tests/DeliveryPilot.Notification.Tests.csproj
dotnet test tests/DeliveryPilot.Gateway.Tests/DeliveryPilot.Gateway.Tests.csproj
powershell -File scripts/Test-Architecture.ps1
powershell -File scripts/Test-GatewayDeployment.ps1
python tools/docs.py validate --feature FEAT-DP-003
python tools/docs.py coverage --feature FEAT-DP-003
~~~

Use the existing tests projects/namespaces; do not create Python tests under root tests. Real PostgreSQL tests require DELIVERYPILOT_TEST_POSTGRES pointed to a provisioned isolated PostgreSQL18 database with reviewed migrations. Never point destructive fixtures at production; do not count skipped database/race tests as passing constraints. Message/provider integration uses isolated RabbitMQ/Mailpit or purpose-specific fakes plus the actual owning-host composition. No application build/test, provider send or database migration was run while producing this plan.

## Inspected source navigation

All paths below were present and inspected before task writing. CodeGraph reported no usable index; no indexing was performed. Future output paths in Task bodies are explicitly proposed.

| Evidence key | Existing repository source |
|---|---|
| models | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Models/](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Domain/Models/) |
| db | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/DbContexts/IdentityDbContext.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/DbContexts/IdentityDbContext.cs) |
| uow | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Core/IdentityUnitOfWork.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Core/IdentityUnitOfWork.cs) |
| map | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs) |
| inventory | [tests/DeliveryPilot.Identity.Tests/Persistence/IdentityModelInventoryTests.cs](../../../tests/DeliveryPilot.Identity.Tests/Persistence/IdentityModelInventoryTests.cs) |
| pg | [tests/DeliveryPilot.Identity.Tests/Persistence/PostgreSqlUserTests.cs](../../../tests/DeliveryPilot.Identity.Tests/Persistence/PostgreSqlUserTests.cs) |
| auth | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/AuthController.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/Controllers/AuthController.cs) |
| current | [src/BuildingBlocks/DeliveryPilot.AspNetCore/Security/HttpContextCurrentUserProvider.cs](../../../src/BuildingBlocks/DeliveryPilot.AspNetCore/Security/HttpContextCurrentUserProvider.cs) |
| bearer | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/AuthenticatedContextValidator.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Security/AuthenticatedContextValidator.cs) |
| infra | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/DepedencyInjection/ServiceCollectionExtensions.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/DepedencyInjection/ServiceCollectionExtensions.cs) |
| app | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/DependencyInjection/ServiceCollectionExtensions.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/DependencyInjection/ServiceCollectionExtensions.cs) |
| apiBase | [src/BuildingBlocks/DeliveryPilot.AspNetCore/Controllers/ApiController.cs](../../../src/BuildingBlocks/DeliveryPilot.AspNetCore/Controllers/ApiController.cs) |
| envelope | [src/BuildingBlocks/DeliveryPilot.AspNetCore/Filters/ApiResponseEnvelopeFilter.cs](../../../src/BuildingBlocks/DeliveryPilot.AspNetCore/Filters/ApiResponseEnvelopeFilter.cs) |
| errors | [src/BuildingBlocks/DeliveryPilot.AspNetCore/Middleware/GlobalExceptionHandlingMiddleware.cs](../../../src/BuildingBlocks/DeliveryPilot.AspNetCore/Middleware/GlobalExceptionHandlingMiddleware.cs) |
| loginTests | [tests/DeliveryPilot.Identity.Tests/Users/LoginHttpTests.cs](../../../tests/DeliveryPilot.Identity.Tests/Users/LoginHttpTests.cs) |
| gateway | [src/Gateway/DeliveryPilot.Gateway/proxy.json](../../../src/Gateway/DeliveryPilot.Gateway/proxy.json) |
| gatewayTests | [tests/DeliveryPilot.Gateway.Tests/GatewayHttpTests.cs](../../../tests/DeliveryPilot.Gateway.Tests/GatewayHttpTests.cs) |
| requestV1 | [src/Contracts/DeliveryPilot.Notification.Contracts/EmailDelivery/V1/EmailDeliveryRequestedV1.cs](../../../src/Contracts/DeliveryPilot.Notification.Contracts/EmailDelivery/V1/EmailDeliveryRequestedV1.cs) |
| outbox | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Messaging/IdentityEmailOutbox.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Messaging/IdentityEmailOutbox.cs) |
| dispatch | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Outbox/IdentityOutboxDispatchStore.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/Persistence/Outbox/IdentityOutboxDispatchStore.cs) |
| messaging | [src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/DepedencyInjection/IdentityMessagingExtensions.cs](../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Infrastructure/DepedencyInjection/IdentityMessagingExtensions.cs) |
| delivery | [src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Domain/Models/EmailDelivery.cs](../../../src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Domain/Models/EmailDelivery.cs) |
| worker | [src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Email/Workers/EmailDeliveryWorker.cs](../../../src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Email/Workers/EmailDeliveryWorker.cs) |
| intake | [src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Persistence/EmailDeliveryRepository.cs](../../../src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Persistence/EmailDeliveryRepository.cs) |
| template | [src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Email/Templates/VerificationTemplate.cs](../../../src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Email/Templates/VerificationTemplate.cs) |
| protection | [src/BuildingBlocks/DeliveryPilot.Messaging/Security/EnvelopeEncryption.cs](../../../src/BuildingBlocks/DeliveryPilot.Messaging/Security/EnvelopeEncryption.cs) |
| topology | [src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Messaging/NotificationTopology.cs](../../../src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Infrastructure/Messaging/NotificationTopology.cs) |
| secrets | [deploy/setup/stage-secrets.sh](../../../deploy/setup/stage-secrets.sh) |
| tests | [tests/DeliveryPilot.Identity.Tests/DeliveryPilot.Identity.Tests.csproj](../../../tests/DeliveryPilot.Identity.Tests/DeliveryPilot.Identity.Tests.csproj) |

## Planning validation results

Actual results on 2026-11-03 after task writing:

| Check | Result |
|---|---|
| python tools/docs.py validate --feature FEAT-DP-003 | Exit 0; validator reported 65 artifacts, 0 errors. |
| python tools/docs.py coverage --feature FEAT-DP-003 | Exit 0; 8 UR, 14 FR, 9 US, 14 TASK; 0 unlinked downstream advisories. Every FR and US has task coverage. |
| python tools/docs.py validate | Exit 0; 65 artifacts, 0 errors, including the previously corrected FEAT-DP-002 examples. |
| python tools/docs.py check-task --id TASK-DP-003-002 | Exit 0; structurally eligible, subject to the normal fresh source/acceptance preflight. |
| git diff --check | Exit 0; no whitespace errors. |
| Local Markdown link targets and source preservation | No missing relative targets in the 17 planning files; all 31 source UR/FR/US files byte-for-byte unchanged from the start of planning. |
| Application builds/tests, migrations, provider sends and environment provisioning | Not run; this operation planned documentation only. |

Commands used the existing local Python 3.14.10 executable because the shell Python alias is unavailable. Structural validation and graph coverage do not establish runtime behavior, client approval or production readiness. Task dependency gates remain in effect.

Planning changed 17 documentation files: this backend-plan.md, feature.md, the documentation-alignment-audit.md planning addendum, the replanned TASK-DP-003-001, and new TASK-DP-003-002 through TASK-DP-003-014. The separate earlier requirements-alignment changes already present in the worktree are preserved.

## Recipient invitation execution — 2026-11-04

[TASK-DP-003-010](tasks/TASK-DP-003-010.md#implementation-and-verification--2026-11-04) implements recipient preview, accept and decline, guarded fresh-role rejoin and current-authorized acceptance replay. Preview uses a read-only repeatable-read snapshot; writes retain the shared lock order and pipeline-owned transaction. The task records 15 focused recipient tests and the full 158-test Identity suite passing on disposable PostgreSQL 18 without skips, 57 Gateway regressions passing, and architecture/documentation checks. No migration, host or broker contract was added. Public Gateway publication remains task 014; source requirement statuses and approvals are unchanged.
