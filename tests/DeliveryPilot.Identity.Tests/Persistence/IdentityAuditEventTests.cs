using System.Text.Json;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class IdentityAuditEventTests
{
    [Fact]
    public void RoleMetadataAuditUsesAllowlistedBeforeAndAfterValues()
    {
        Guid organizationId = Guid.CreateVersion7();
        Guid actorId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var role = OrganizationRole.CreateCustom(organizationId, "Before", actorId, now, "old description");
        var before = OrganizationRoleAuditSnapshot.Capture(role, []);
        Assert.Throws<ArgumentException>(() => IdentityAuditEntry.ForRoleUpdated(before, role, IdentityAuditActor.User(actorId), now, "test"));
        role.UpdateDetails("After", "private description", now.AddSeconds(1));
        IdentityAuditEntry entry = IdentityAuditEntry.ForRoleUpdated(before, role, IdentityAuditActor.User(actorId), now.AddSeconds(1), "test");
        var validated = IdentityAuditSchemaCatalog.ValidateAndCanonicalizeDetails(entry.AggregateType, entry.EventType, 1, entry.DetailsJson);
        using var json = JsonDocument.Parse(validated.CanonicalDetailsJson);
        Assert.Equal("Before", json.RootElement.GetProperty("previousName").GetString());
        Assert.Equal("After", json.RootElement.GetProperty("newName").GetString());
        Assert.True(json.RootElement.GetProperty("descriptionChanged").GetBoolean());
        Assert.DoesNotContain("private description", validated.CanonicalDetailsJson, StringComparison.Ordinal);
        Assert.Equal(1, role.PermissionSetVersion);
    }

    [Fact]
    public void AllowlistedPayloadsRejectForbiddenAndUnknownFieldsAndNeverEchoSensitiveReasonInput()
    {
        Guid orgId = Guid.CreateVersion7();
        Guid memberId = Guid.CreateVersion7();
        Guid userId = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));

        string validDetails = JsonSerializer.Serialize(new
        {
            userId,
            previousStatus = "active",
            newStatus = "removed",
        });

        IdentityAuditEvent created = IdentityAuditEvent.Create(
            orgId,
            memberId,
            2,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            memberId,
            2,
            1,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            "succeeded",
            "user",
            userId,
            null,
            now,
            now,
            "corr-001",
            validDetails,
            " Contract ended ");

        Assert.Equal("Contract ended", created.Reason);
        Assert.Equal(TimeSpan.Zero, created.OccurredAt.Offset);
        Assert.Equal(TimeSpan.Zero, created.RecordedAt.Offset);

        // System/operator actor rules: no fabricated User ID.
        Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            orgId,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationAggregate,
            orgId,
            1,
            1,
            IdentityAuditSchemaCatalog.OrganizationCreated,
            "succeeded",
            "system",
            userId,
            "bootstrap-worker",
            now,
            now,
            "corr-001",
            JsonSerializer.Serialize(new { name = "Org", status = "active", createdByUserId = userId })));

        // Unknown and forbidden fields (email, tokenVerifier, passwordHash, rawPayload) are rejected.
        foreach (string forbiddenField in new[] { "recipientEmail", "primaryEmail", "tokenVerifier", "passwordHash", "rawPayload", "unknownField" })
        {
            string badJson = $$"""{"userId":"{{userId}}","previousStatus":null,"newStatus":"active","{{forbiddenField}}":"secret-value-123"}""";
            ArgumentException ex = Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
                orgId,
                memberId,
                1,
                IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
                memberId,
                1,
                1,
                IdentityAuditSchemaCatalog.MembershipCreated,
                "succeeded",
                "user",
                userId,
                null,
                now,
                now,
                "corr-001",
                badJson));
            Assert.DoesNotContain("secret-value-123", ex.Message, StringComparison.Ordinal);
        }

        // Required reasons on removal, Admin/Owner reduction, invitation cancellation, and role retirement.
        Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            orgId,
            memberId,
            2,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            memberId,
            2,
            1,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            "succeeded",
            "user",
            userId,
            null,
            now,
            now,
            "corr-001",
            validDetails,
            null));

        string sensitiveReason = "https://example.test/invite?token=top-secret-proof-value";
        ArgumentException reasonEx = Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            orgId,
            memberId,
            2,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            memberId,
            2,
            1,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            "succeeded",
            "user",
            userId,
            null,
            now,
            now,
            "corr-001",
            validDetails,
            sensitiveReason));
        Assert.DoesNotContain("top-secret-proof-value", reasonEx.Message, StringComparison.Ordinal);

        string overlongSecretReason = "secret-marker-" + new string('x', 1005);
        ArgumentException overlongEx = Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            orgId,
            memberId,
            2,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            memberId,
            2,
            1,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            "succeeded",
            "user",
            userId,
            null,
            now,
            now,
            "corr-001",
            validDetails,
            overlongSecretReason));
        Assert.DoesNotContain("secret-marker-", overlongEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredReasonsRejectBeforeMutationForProtectedReductionRetirementAndCancellation()
    {
        Guid orgId = Guid.CreateVersion7();
        Guid userId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        OrganizationMembership membership = OrganizationMembership.Create(orgId, userId, userId, now);
        OrganizationRole adminRole = OrganizationRole.Create(orgId, "admin", "Admin", "system", 1, userId, now);
        OrganizationRole customRole = OrganizationRole.Create(orgId, "custom-1", "Custom", "custom", null, userId, now);
        OrganizationRoleAssignment adminAssignment = OrganizationRoleAssignment.Create(orgId, membership.Id, adminRole.Id, userId, now);

        OrganizationRoleAssignmentAuditSnapshot beforeAssignment = OrganizationRoleAssignmentAuditSnapshot.Capture(adminAssignment);
        adminAssignment.Revoke(userId, now.AddMinutes(1), "Reduced by Owner");
        membership.RecordAssignmentChange(now.AddMinutes(1));

        IdentityAuditEntry missingProtectedReason = IdentityAuditEntry.ForRoleAssignmentRevoked(
            beforeAssignment,
            adminAssignment,
            membership.Version,
            IdentityAuditActor.User(userId),
            now.AddMinutes(1),
            "corr-002",
            reason: null,
            isProtectedRoleReduction: true);

        Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            missingProtectedReason.OrganizationId,
            missingProtectedReason.MembershipId,
            missingProtectedReason.MembershipVersion,
            missingProtectedReason.AggregateType,
            missingProtectedReason.AggregateId,
            missingProtectedReason.AggregateVersion,
            1,
            missingProtectedReason.EventType,
            "succeeded",
            missingProtectedReason.Actor.Kind,
            missingProtectedReason.Actor.UserId,
            missingProtectedReason.Actor.Reference,
            missingProtectedReason.OccurredAt,
            now.AddMinutes(1),
            missingProtectedReason.CorrelationId,
            missingProtectedReason.DetailsJson,
            missingProtectedReason.Reason,
            requiresReason: missingProtectedReason.RequiresReason));

        customRole.Retire(now.AddMinutes(2));
        IdentityAuditEntry invalidRetireReason = IdentityAuditEntry.ForRoleRetired(
            customRole,
            IdentityAuditActor.User(userId),
            now.AddMinutes(2),
            "corr-003",
            "   ");
        Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            invalidRetireReason.OrganizationId,
            invalidRetireReason.MembershipId,
            invalidRetireReason.MembershipVersion,
            invalidRetireReason.AggregateType,
            invalidRetireReason.AggregateId,
            invalidRetireReason.AggregateVersion,
            1,
            invalidRetireReason.EventType,
            "succeeded",
            invalidRetireReason.Actor.Kind,
            invalidRetireReason.Actor.UserId,
            invalidRetireReason.Actor.Reference,
            invalidRetireReason.OccurredAt,
            now.AddMinutes(2),
            invalidRetireReason.CorrelationId,
            invalidRetireReason.DetailsJson,
            invalidRetireReason.Reason,
            requiresReason: invalidRetireReason.RequiresReason));

        IdentityAuditEntry cancelledInvitationWithoutReason = IdentityAuditEntry.ForInvitationClosed(
            orgId,
            Guid.CreateVersion7(),
            2,
            "pending",
            "cancelled",
            userId,
            now.AddMinutes(3),
            IdentityAuditActor.User(userId),
            now.AddMinutes(3),
            "corr-004",
            reason: null);
        Assert.Throws<ArgumentException>(() => IdentityAuditEvent.Create(
            cancelledInvitationWithoutReason.OrganizationId,
            cancelledInvitationWithoutReason.MembershipId,
            cancelledInvitationWithoutReason.MembershipVersion,
            cancelledInvitationWithoutReason.AggregateType,
            cancelledInvitationWithoutReason.AggregateId,
            cancelledInvitationWithoutReason.AggregateVersion,
            1,
            cancelledInvitationWithoutReason.EventType,
            "succeeded",
            cancelledInvitationWithoutReason.Actor.Kind,
            cancelledInvitationWithoutReason.Actor.UserId,
            cancelledInvitationWithoutReason.Actor.Reference,
            cancelledInvitationWithoutReason.OccurredAt,
            now.AddMinutes(3),
            cancelledInvitationWithoutReason.CorrelationId,
            cancelledInvitationWithoutReason.DetailsJson,
            cancelledInvitationWithoutReason.Reason,
            requiresReason: cancelledInvitationWithoutReason.RequiresReason));
    }

    [PostgreSqlFact]
    public async Task MultiChangeMembershipRevisionAllocatesDistinctOrdinalsAndRetainsHistoryAcrossRejoinAndRegrant()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        await using var context = new IdentityDbContext(options);
        var unitOfWork = new IdentityUnitOfWork(context);
        var writer = new IdentityAuditWriter(context, TimeProvider.System, NullLogger<IdentityAuditWriter>.Instance);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Audited user");
        Organization organization = Organization.Create("Audited Workspace", user.Id, now);
        OrganizationMembership membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);
        OrganizationRole adminRole = OrganizationRole.Create(organization.Id, "admin", "Admin", "system", 1, user.Id, now);
        OrganizationRole memberRole = OrganizationRole.Create(organization.Id, "member", "Member", "system", 1, user.Id, now);
        OrganizationRoleAssignment adminAssignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, adminRole.Id, user.Id, now);
        OrganizationRoleAssignment memberAssignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, memberRole.Id, user.Id, now);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                context.Users.Add(user);
                context.Organizations.Add(organization);
                context.OrganizationMemberships.Add(membership);
                context.OrganizationRoles.AddRange(adminRole, memberRole);
                context.OrganizationRoleAssignments.AddRange(adminAssignment, memberAssignment);

                await writer.StageAsync(IdentityAuditEntry.ForOrganizationCreated(organization, IdentityAuditActor.User(user.Id), now, "corr-init"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForRoleCreated(adminRole, ["organization.members.read", "organization.roles.assign"], IdentityAuditActor.System("role-template-seed"), now, "corr-init"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForRoleCreated(memberRole, ["organization.members.read"], IdentityAuditActor.System("role-template-seed"), now, "corr-init"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForMembershipCreated(membership, IdentityAuditActor.User(user.Id), now, "corr-init"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForRoleAssignmentGranted(adminAssignment, membership.Version, adminRole.PermissionSetVersion, IdentityAuditActor.User(user.Id), now, "corr-init"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForRoleAssignmentGranted(memberAssignment, membership.Version, memberRole.PermissionSetVersion, IdentityAuditActor.User(user.Id), now, "corr-init"), ct);
                return Result.Success();
            });

            // Remove membership: revokes both assignments and changes membership status in one membership revision (Version 2) with ordinals 1, 2, 3.
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                OrganizationMembershipAuditSnapshot beforeMember = OrganizationMembershipAuditSnapshot.Capture(membership);
                OrganizationRoleAssignmentAuditSnapshot beforeAdmin = OrganizationRoleAssignmentAuditSnapshot.Capture(adminAssignment);
                OrganizationRoleAssignmentAuditSnapshot beforeMemberRole = OrganizationRoleAssignmentAuditSnapshot.Capture(memberAssignment);

                DateTimeOffset removedAt = now.AddMinutes(1);
                membership.Remove(user.Id, "Project completed", removedAt, [adminAssignment, memberAssignment]);

                IdentityAuditEvent e1 = await writer.StageAsync(IdentityAuditEntry.ForRoleAssignmentRevoked(beforeAdmin, adminAssignment, membership.Version, IdentityAuditActor.User(user.Id), removedAt, "corr-remove", "Project completed", isProtectedRoleReduction: true), ct);
                IdentityAuditEvent e2 = await writer.StageAsync(IdentityAuditEntry.ForRoleAssignmentRevoked(beforeMemberRole, memberAssignment, membership.Version, IdentityAuditActor.User(user.Id), removedAt, "corr-remove", "Project completed"), ct);
                IdentityAuditEvent e3 = await writer.StageAsync(IdentityAuditEntry.ForMembershipStatusChanged(beforeMember, membership, IdentityAuditActor.User(user.Id), removedAt, "corr-remove", "Project completed"), ct);

                Assert.Equal(1, e1.EventOrdinal);
                Assert.Equal(2, e2.EventOrdinal);
                Assert.Equal(3, e3.EventOrdinal);
                return Result.Success();
            });

            // Rejoin and regrant only Member role: earlier grant/removal events survive unchanged.
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                OrganizationMembershipAuditSnapshot beforeRejoin = OrganizationMembershipAuditSnapshot.Capture(membership);
                OrganizationRoleAssignmentAuditSnapshot beforeRegrant = OrganizationRoleAssignmentAuditSnapshot.Capture(memberAssignment);
                DateTimeOffset rejoinedAt = now.AddMinutes(2);

                membership.Rejoin(user.Id, rejoinedAt);
                await writer.StageAsync(IdentityAuditEntry.ForMembershipStatusChanged(beforeRejoin, membership, IdentityAuditActor.User(user.Id), rejoinedAt, "corr-rejoin"), ct);

                memberAssignment.Regrant(user.Id, rejoinedAt.AddSeconds(1), null, "Fresh member admission");
                membership.RecordAssignmentChange(rejoinedAt.AddSeconds(1));
                await writer.StageAsync(IdentityAuditEntry.ForRoleAssignmentRegranted(beforeRegrant, memberAssignment, membership.Version, memberRole.PermissionSetVersion, IdentityAuditActor.User(user.Id), rejoinedAt.AddSeconds(1), "corr-rejoin", "Fresh member admission"), ct);
                return Result.Success();
            });

            // Denial telemetry emits warning log only and never writes an IdentityAuditEvent row.
            writer.RecordDenialTelemetry(IdentityAuditSchemaCatalog.RoleAssignmentGranted, organization.Id, user.Id, "corr-denied", "Organization.AccessDenied");

            List<IdentityAuditEvent> memberHistory = await context.IdentityAuditEvents
                .AsNoTracking()
                .Where(x => x.MembershipId == membership.Id)
                .OrderBy(x => x.MembershipVersion)
                .ThenBy(x => x.EventOrdinal)
                .ToListAsync();

            Assert.Equal(8, memberHistory.Count);
            Assert.Equal([1, 2, 3], memberHistory.Where(x => x.MembershipVersion == 1).Select(x => x.EventOrdinal).ToArray());
            Assert.Equal([1, 2, 3], memberHistory.Where(x => x.MembershipVersion == 2).Select(x => x.EventOrdinal).ToArray());
            Assert.Equal(IdentityAuditSchemaCatalog.RoleAssignmentRegranted, memberHistory.Last().EventType);
            using (JsonDocument regrantDetails = JsonDocument.Parse(memberHistory.Last().Details))
            {
                Assert.Equal(1, regrantDetails.RootElement.GetProperty("previousGrantVersion").GetInt32());
                Assert.Equal(2, regrantDetails.RootElement.GetProperty("grantVersion").GetInt32());
            }

            Assert.DoesNotContain(memberHistory, x => x.Outcome != "succeeded");
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.IdentityAuditEvents.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await context.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task AuditFailureOrDuplicateOrdinalRollsBackBothBusinessMutationAndAuditEvents()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        await using var context = new IdentityDbContext(options);
        var unitOfWork = new IdentityUnitOfWork(context);
        var writer = new IdentityAuditWriter(context, TimeProvider.System, NullLogger<IdentityAuditWriter>.Instance);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Rollback test user");
        Organization organization = Organization.Create("Rollback Workspace", user.Id, now);
        OrganizationMembership membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                context.Users.Add(user);
                context.Organizations.Add(organization);
                context.OrganizationMemberships.Add(membership);
                await writer.StageAsync(IdentityAuditEntry.ForOrganizationCreated(organization, IdentityAuditActor.User(user.Id), now, "corr-seed"), ct);
                await writer.StageAsync(IdentityAuditEntry.ForMembershipCreated(membership, IdentityAuditActor.User(user.Id), now, "corr-seed"), ct);
                return Result.Success();
            });

            // 1. In-memory duplicate ordinal rejection before commit rolls back the membership removal.
            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
            {
                OrganizationMembership tracked = await context.OrganizationMemberships.SingleAsync(x => x.Id == membership.Id, ct);
                OrganizationMembershipAuditSnapshot before = OrganizationMembershipAuditSnapshot.Capture(tracked);
                tracked.Remove(user.Id, "Should roll back", now.AddMinutes(1), []);

                IdentityAuditEntry entry = IdentityAuditEntry.ForMembershipStatusChanged(
                    before,
                    tracked,
                    IdentityAuditActor.User(user.Id),
                    now.AddMinutes(1),
                    "corr-dup",
                    "Should roll back") with
                {
                    EventOrdinal = 1,
                };

                await writer.StageAsync(entry, ct);
                await writer.StageAsync(entry, ct);
                return Result.Success();
            }));

            Assert.Equal("active", (await context.OrganizationMemberships.AsNoTracking().SingleAsync(x => x.Id == membership.Id)).Status);
            Assert.Equal(2, await context.IdentityAuditEvents.CountAsync(x => x.OrganizationId == organization.Id));

            // 2. Database-level unique constraint violation on (AggregateType, AggregateId, AggregateVersion, EventOrdinal)
            // rolls back both the business mutation and the staged audit row.
            await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
            {
                OrganizationMembership tracked = await context.OrganizationMemberships.SingleAsync(x => x.Id == membership.Id, ct);
                tracked.RecordAssignmentChange(now.AddMinutes(2));

                // Stage a duplicate of the already-committed Version 1, EventOrdinal 1 membership event.
                IdentityAuditEvent duplicateCommittedOrdinal = IdentityAuditEvent.Create(
                    organization.Id,
                    membership.Id,
                    1,
                    IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
                    membership.Id,
                    1,
                    1,
                    IdentityAuditSchemaCatalog.MembershipCreated,
                    "succeeded",
                    "user",
                    user.Id,
                    null,
                    now,
                    now,
                    "corr-db-dup",
                    JsonSerializer.Serialize(new { userId = user.Id, previousStatus = (string?)null, newStatus = "active" }));

                context.IdentityAuditEvents.Add(duplicateCommittedOrdinal);
                return Result.Success();
            }));

            OrganizationMembership unchanged = await context.OrganizationMemberships.AsNoTracking().SingleAsync(x => x.Id == membership.Id);
            Assert.Equal(1, unchanged.Version);
            Assert.Equal("active", unchanged.Status);
            Assert.Equal(2, await context.IdentityAuditEvents.CountAsync(x => x.OrganizationId == organization.Id));
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.IdentityAuditEvents.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await context.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
        }
    }
}
