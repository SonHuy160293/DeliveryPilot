# Organization backend acceptance evidence

This matrix maps all 88 criteria of US-DP-003-001 through US-DP-003-009 to named backend tests and explicit external handoffs. References identify executable evidence, not business approval or a claim that a backend test renders a frontend or implements a Project module. Read this with the execution results in [TASK-DP-003-014](tasks/TASK-DP-003-014.md).

Verification environment: checkout at dd66c28 plus the preserved uncommitted tasks 011–015/014 changes, .NET 10, real local Kestrel Gateway/Identity, disposable PostgreSQL 18, and separate disposable RabbitMQ/Mailpit/deployed hosts. Current full Identity result: 218 passed, zero skips; Gateway: 97 passed, zero skips. Full Notification suite: 48 passed, zero skips with deployed smoke prerequisites enabled. Disposable full deployment acceptance passed, including production-mode Gateway checks, outages, failure injection, host readiness and retained restart. One earlier Notification run failed the existing relay-recovery test; its isolated rerun and the subsequent full 48-test rerun passed.

## [US-DP-003-001](us/US-DP-003-001.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Create with initial Owner | [OrganizationGatewayJourneyTests.CreationAndReplayThroughGatewayPreserveIdempotencyAndWorkspaceIsolation](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-002 — No implicit access | [OrganizationAuthorizationAndGuardTests.WorkspaceIsolationIgnoresForgedClaimsJobTitlesAndPlatformAssignmentsAndNeverGrantsProjectAccess](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |
| AC-003 — Duplicate name is allowed | [OrganizationCreationHttpTests.AuthenticatedCreationReplayValidationAndDisclosureUseHttpContract](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationHttpTests.cs) | Backend regression |
| AC-004 — Name boundaries | [OrganizationCreationTests.ValidatorChecksNameAndAttemptBoundaries](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationTests.cs) | Backend regression |
| AC-005 — Atomic establishment | [OrganizationCreationTests.AuditAndReceiptPersistenceFailuresRollBackAllCreationState](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationTests.cs) | Backend regression |
| AC-006 — Unauthenticated caller | [OrganizationGatewayJourneyTests.EveryPublishedActionReachesBearerAuthenticationAndCurrentSessionsAndScopesRemainAuthoritative](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-007 — Same creation attempt replay | [OrganizationCreationTests.SnapshotReplaysOnlyWithCurrentAuditAuthorityAndNeverRestoresOwner](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationTests.cs) | Backend regression |
| AC-008 — Conflicting creation attempt content | [OrganizationCreationHttpTests.AuthenticatedCreationReplayValidationAndDisclosureUseHttpContract](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationHttpTests.cs) | Backend regression |
| AC-009 — Expired creation attempt | [OrganizationCreationTests.SnapshotReplaysOnlyWithCurrentAuditAuthorityAndNeverRestoresOwner](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationCreationTests.cs) | Backend regression |

## [US-DP-003-002](us/US-DP-003-002.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Issue role offer | [InvitationManagementHttpTests.ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-002 — No invitation authority | [InvitationManagementHttpTests.DelegatedActionsPreservePrivacyAndReadsReauthorizeEachPage](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-003 — Invalid recipient proof or expired offer | [InvitationResponseHttpTests.DisclosureOrderingValidationAndBodyLimitsApplyToEveryRecipientRoute](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-004 — Concurrent duplicate offers | [InvitationManagementHttpTests.FailuresRollbackAllStagedStateAndRacesKeepOneCurrentOffer](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-005 — Resend unchanged offer | [InvitationManagementHttpTests.ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-006 — Permission change requires reissue | [InvitationManagementHttpTests.ResendRequiresCurrentOriginalSponsorAndReissueRepairsLostAuthority](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-007 — Delegation ceiling | [InvitationResponseHttpTests.EveryCurrentSponsorCapabilityAndDelegationCeilingIsRequired](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-008 — Duplicate issue requires explicit action | [InvitationManagementHttpTests.ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-009 — Delivery failure preserves valid offer | [OrganizationGatewayJourneyTests.ResendReissueDeclineCancelAndRateLimitRemainSafeAcrossGateway](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-010 — Closed offer requires new issue | [InvitationManagementHttpTests.ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-011 — Issuance abuse limits | [InvitationManagementHttpTests.RateLimitsReturnSafeRetryAfterAndDoNotChangeProofOrDeadline](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |
| AC-012 — Cancel pending without undoing membership | [InvitationManagementHttpTests.ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationManagementHttpTests.cs) | Backend regression |

## [US-DP-003-003](us/US-DP-003-003.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Select A without mixing B | [OrganizationReadPostgresHttpTests.WorkspacePagesRecheckMembershipAndRolesWithTimestampTies](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |
| AC-002 — Revoked selection | [OrganizationReadPostgresHttpTests.RealReadPipelineEnforcesScopeStatusesAndPermissionChangesBetweenPages](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |
| AC-003 — No implicit project access | [OrganizationAuthorizationAndGuardTests.WorkspaceIsolationIgnoresForgedClaimsJobTitlesAndPlatformAssignmentsAndNeverGrantsProjectAccess](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend boundary verified; actual private Project resource access is an external FEAT-DP-004/015 handoff |
| AC-004 — Zero memberships | [OrganizationReadPostgresHttpTests.WorkspacePagesRecheckMembershipAndRolesWithTimestampTies](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |
| AC-005 — Guest boundary | [OrganizationAuthorizationAndGuardTests.WorkspaceIsolationIgnoresForgedClaimsJobTitlesAndPlatformAssignmentsAndNeverGrantsProjectAccess](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Workspace non-inference verified; independently valid project-guest fixture belongs to FEAT-DP-004/015 |
| AC-006 — Unknown workspace | [OrganizationReadAuthorizationTests.GeneralReadsDenyBeforeLookingUpTarget](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadAuthorizationTests.cs) | Backend regression |
| AC-007 — Invalid remembered workspace | [OrganizationReadPostgresHttpTests.WorkspacePagesRecheckMembershipAndRolesWithTimestampTies](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | API eligibility/roles verified; remembered context, selector rendering and no silent frontend switch remain a frontend handoff |

## [US-DP-003-004](us/US-DP-003-004.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Remove ordinary member | [MembershipChangeHttpTests.RemovalClosesAllGrantsAndPreservesHistoryIdentitySessionsAndOtherWorkspaces](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-002 — Unauthorized removal | [MembershipChangeHttpTests.ProtectedTargetsAndDelegationCeilingCannotBeBypassedIncludingInactiveAccounts](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-003 — Historical attribution | [OrganizationAuditHttpTests.RealLifecycleHistoryIncludesRejoinRegrantAndNominationWhileHistoricalActorsRemainAttributable](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-004 — Last active Owner | [MembershipChangeHttpTests.LastOwnerAndNoEligibleOwnerCannotMutateWhileOrdinaryReadsRemainAuthorized](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-005 — Concurrent Owner reductions | [MembershipChangeHttpTests.TwoOwnersCanStepDownOrRemoveConcurrentlyButCannotOrphanTheWorkspace](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-006 — Independent organizations | [MembershipChangeHttpTests.RemovalClosesAllGrantsAndPreservesHistoryIdentitySessionsAndOtherWorkspaces](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-007 — Admin membership target protection | [MembershipChangeHttpTests.ProtectedTargetsAndDelegationCeilingCannotBeBypassedIncludingInactiveAccounts](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-008 — Ordinary-removal target ceiling | [MembershipChangeHttpTests.ProtectedTargetsAndDelegationCeilingCannotBeBypassedIncludingInactiveAccounts](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-009 — Revocation admission and queued work | [MembershipChangeHttpTests.AdmittedReadMayFinishButQueuedWorkAndNewDecisionsReauthorizeAfterRemoval](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-010 — Mutation commit recheck | [MembershipChangeHttpTests.AccountAndSessionEligibilityAreLockedThroughCommitInBothOrders](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-011 — Required audit failure | [MembershipChangeHttpTests.AuditAndCommitFailureRollbackRemovalDemotionAndFallbackTogether](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-012 — Missing reduction reason | [MembershipChangeHttpTests.ValidationPrivacyAndKestrelBodyLimitsApplyToBothActions](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-013 — Owner removes an Admin | [MembershipChangeHttpTests.OwnerCanRemoveAdminAndInactiveOwnerProtectionDoesNotConferActorAuthority](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |

## [US-DP-003-005](us/US-DP-003-005.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — First join | [InvitationResponseHttpTests.FirstJoinPreviewAndReplayArePrivateAtomicAndDoNotVerifyEmail](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-002 — Unverified account with valid invitation proof | [InvitationResponseHttpTests.FirstJoinPreviewAndReplayArePrivateAtomicAndDoNotVerifyEmail](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-003 — Missing proof or wrong account | [InvitationResponseHttpTests.DisclosureOrderingValidationAndBodyLimitsApplyToEveryRecipientRoute](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-004 — Deadline and old links | [InvitationResponseHttpTests.PendingAcceptanceDeniesNewlyActiveOrSuspendedMembershipsAndSupersededProofs](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-005 — Inviter authority lost | [InvitationResponseHttpTests.SponsorLossAndChangedOffersRequireReissueButStillAllowDecline](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-006 — Offer changed | [InvitationResponseHttpTests.SponsorLossAndChangedOffersRequireReissueButStillAllowDecline](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-007 — Fresh-role rejoin invariant | [InvitationResponseHttpTests.RemovedAdminRejoinsWithOnlyFreshMemberGrantAndPreservesHistory](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-008 — Concurrent redemption | [InvitationResponseHttpTests.ConcurrentRecipientAndManagerTransitionsHaveOneWinner](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-009 — Existing member state restriction | [InvitationResponseHttpTests.PendingAcceptanceDeniesNewlyActiveOrSuspendedMembershipsAndSupersededProofs](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-010 — Accepted response replay | [InvitationResponseHttpTests.FirstJoinPreviewAndReplayArePrivateAtomicAndDoNotVerifyEmail](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-011 — Preview or decline proof | [InvitationResponseHttpTests.SponsorLossAndChangedOffersRequireReissueButStillAllowDecline](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |

## [US-DP-003-006](us/US-DP-003-006.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Delegated manager | [OrganizationGatewayJourneyTests.RoleInputBoundariesBuiltInsAndDelegatedSelfEscalationAreEnforcedAcrossGateway](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-002 — Built-ins protected | [OrganizationAuthorizationAndGuardTests.CapabilityCatalogAndBuiltInTemplatesEnforceProtectedOwnerAndDelegationCeiling](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |
| AC-003 — Self-escalation blocked | [OrganizationGatewayJourneyTests.RoleInputBoundariesBuiltInsAndDelegatedSelfEscalationAreEnforcedAcrossGateway](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-004 — Combined roles | [OrganizationAuthorizationAndGuardTests.CapabilityCatalogAndBuiltInTemplatesEnforceProtectedOwnerAndDelegationCeiling](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |
| AC-005 — Foreign or protected role | [OrganizationAuthorizationAndGuardTests.WorkspaceIsolationIgnoresForgedClaimsJobTitlesAndPlatformAssignmentsAndNeverGrantsProjectAccess](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |
| AC-006 — Regrant history | [OrganizationAuditHttpTests.RealLifecycleHistoryIncludesRejoinRegrantAndNominationWhileHistoricalActorsRemainAttributable](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-007 — No demote-then-remove bypass | [OrganizationAuthorizationAndGuardTests.AdminOrDelegateCannotDemoteAdminThenRemoveAndTargetCeilingAndMultiRoleOwnerProtectionHold](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |
| AC-008 — Role input boundaries | [OrganizationGatewayJourneyTests.RoleInputBoundariesBuiltInsAndDelegatedSelfEscalationAreEnforcedAcrossGateway](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-009 — Retirement preconditions | [RoleRetirementHttpTests.RetirementAndAssignmentIssueOrReissueSerializeInBothOrders](../../../tests/DeliveryPilot.Identity.Tests/Organizations/RoleRetirementHttpTests.cs) | Backend regression |
| AC-010 — Stale edit rejection | [OrganizationGatewayJourneyTests.ManagerRecipientRejoinOwnershipStepDownAndAuditJourneyUsesOnlyPublicGatewayRoutes](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-011 — Repeated ordinary role operation | [OrganizationReadPostgresHttpTests.CreateRoleChecksCeilingAndPersistsAuditAtomically](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |
| AC-012 — Built-in upgrades are explicit | [OrganizationAuthorizationAndGuardTests.BuiltInAuthorityUsesOnlyMatchingPersistedAllowedPermissions](../../../tests/DeliveryPilot.Identity.Tests/Security/OrganizationAuthorizationAndGuardTests.cs) | Backend regression |

## [US-DP-003-007](us/US-DP-003-007.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Nominate without granting | [OwnershipNominationHttpTests.NominateAcceptAndSeparateStepDownPreserveRolesAndAuditAttribution](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-002 — Accept additional responsibility | [OwnershipNominationHttpTests.NominateAcceptAndSeparateStepDownPreserveRolesAndAuditAttribution](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-003 — Wrong accepting subject | [OwnershipNominationHttpTests.RemovedSuspendedAndForeignNomineesCannotRespondOrSeeRestrictedNominations](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-004 — No nomination authority | [OwnershipNominationHttpTests.ValidationDisclosureAndKestrelLimitsApplyToEveryOperation](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-005 — Normal continuity | [OwnershipNominationHttpTests.NominateAcceptAndSeparateStepDownPreserveRolesAndAuditAttribution](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-006 — Expired or closed nomination | [OwnershipNominationHttpTests.ExpiryBoundaryAndExpiredReplacementAreAtomicAndHistoryIsRetained](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-007 — Eligibility recheck at acceptance | [OwnershipNominationHttpTests.AcceptanceRacingSponsorDemotionAndNomineeRemovalCannotRestoreRevokedAccess](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-008 — Explicit step-down after additional acceptance | [OrganizationGatewayJourneyTests.ManagerRecipientRejoinOwnershipStepDownAndAuditJourneyUsesOnlyPublicGatewayRoutes](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationGatewayJourneyTests.cs) | Backend regression |
| AC-009 — Nomination terminal race | [OwnershipNominationHttpTests.DuplicateAndAlreadyOwnerNominationAndTerminalRacesHaveNoRepeatedEffects](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-010 — Compromised sole Owner | [MembershipChangeHttpTests.LastOwnerAndNoEligibleOwnerCannotMutateWhileOrdinaryReadsRemainAuthorized](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Protected normal commands verified; operator recovery and global compromise response remain explicitly deferred |
| AC-011 — Duplicate or already-Owner nomination | [OwnershipNominationHttpTests.DuplicateAndAlreadyOwnerNominationAndTerminalRacesHaveNoRepeatedEffects](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OwnershipNominationHttpTests.cs) | Backend regression |
| AC-012 — Owner controls another Owner reduction | [MembershipChangeHttpTests.TwoOwnersCanStepDownOrRemoveConcurrentlyButCannotOrphanTheWorkspace](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |

## [US-DP-003-008](us/US-DP-003-008.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Scoped inspection | [OrganizationReadPostgresHttpTests.RealReadPipelineEnforcesScopeStatusesAndPermissionChangesBetweenPages](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |
| AC-002 — No member-read authority | [OrganizationReadAuthorizationTests.GeneralReadsDenyBeforeLookingUpTarget](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadAuthorizationTests.cs) | Backend regression |
| AC-003 — Invitation is not membership | [InvitationResponseHttpTests.FirstJoinPreviewAndReplayArePrivateAtomicAndDoNotVerifyEmail](../../../tests/DeliveryPilot.Identity.Tests/Organizations/InvitationResponseHttpTests.cs) | Backend regression |
| AC-004 — Reading grants no management | [MembershipChangeHttpTests.ProtectedTargetsAndDelegationCeilingCannotBeBypassedIncludingInactiveAccounts](../../../tests/DeliveryPilot.Identity.Tests/Organizations/MembershipChangeHttpTests.cs) | Backend regression |
| AC-005 — Own membership without a general list grant | [OrganizationReadAuthorizationTests.OwnReadUsesAuthorizedMembershipAndDoesNotRequireGeneralRead](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadAuthorizationTests.cs) | Backend regression |
| AC-006 — Original join time and historical filter | [OrganizationReadPostgresHttpTests.RealReadPipelineEnforcesScopeStatusesAndPermissionChangesBetweenPages](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationReadPostgresHttpTests.cs) | Backend regression |

## [US-DP-003-009](us/US-DP-003-009.md)

| Criterion | Named evidence | Disposition |
|---|---|---|
| AC-001 — Inspect recorded transitions | [OrganizationAuditHttpTests.RealLifecycleHistoryIncludesRejoinRegrantAndNominationWhileHistoricalActorsRemainAttributable](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-002 — Cross-workspace history denied | [OrganizationAuditHttpTests.ReadersNeedCurrentAuditGrantAndEveryPageReauthorizesWithoutCrossScopeLeakage](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-003 — Secrets excluded | [OrganizationAuditHttpTests.ProjectionRedactsLegacyPersonalTextAndFailsSafelyForCorruptOrUnsupportedDetails](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-004 — Read grant revoked | [OrganizationAuditHttpTests.ReadersNeedCurrentAuditGrantAndEveryPageReauthorizesWithoutCrossScopeLeakage](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-005 — No false success evidence | [OrganizationAuditHttpTests.SystemActorsHaveNullUserFieldsAndRolledBackSuccessEventsAreAbsent](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
| AC-006 — Audit fields and reduction reason | [OrganizationAuditHttpTests.RealLifecycleHistoryIncludesRejoinRegrantAndNominationWhileHistoricalActorsRemainAttributable](../../../tests/DeliveryPilot.Identity.Tests/Organizations/OrganizationAuditHttpTests.cs) | Backend regression |
