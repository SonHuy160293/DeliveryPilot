using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class InvitationIssuanceIntegrationTests
{
    [PostgreSqlFact]
    public async Task GuardedLifecycleRotatesProofAndPreservesSnapshotsAndAtomicity()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        var clock = new TestClock(DateTimeOffset.UtcNow.AddMinutes(-10));
        var owner = User.Create($"issuer-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        var organization = Organization.Create("Workspace", owner.Id, clock.GetUtcNow());
        var seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, clock.GetUtcNow());
        var membership = OrganizationMembership.Create(organization.Id, owner.Id, owner.Id, clock.GetUtcNow());
        var assignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, seed.OwnerRole.Id, owner.Id, clock.GetUtcNow());
        var custom = OrganizationRole.CreateCustom(organization.Id, "Original", owner.Id, clock.GetUtcNow());
        var permission = OrganizationRolePermission.Create(organization.Id, custom.Id, OrganizationCapabilityCatalog.MembersRead);
        var stager = new CapturingStager();
        await using var setup = new IdentityDbContext(options);
        try
        {
            setup.Users.Add(owner);
            setup.Organizations.Add(organization);
            setup.OrganizationMemberships.Add(membership);
            setup.OrganizationRoles.AddRange(seed.Roles);
            setup.OrganizationRoles.Add(custom);
            setup.OrganizationRolePermissions.AddRange(seed.Permissions);
            setup.OrganizationRolePermissions.Add(permission);
            setup.OrganizationRoleAssignments.Add(assignment);
            await setup.SaveChangesAsync();
            async Task<T> Run<T>(Func<InvitationIssuanceService, Task<T>> operation, IInvitationDeliveryStager? selectedStager = null, InvitationIssuanceOptions? limits = null, bool failAudit = false)
            {
                await using var db = new IdentityDbContext(options);
                var principal = new CurrentProvider(new CurrentUser(owner.Id.ToString()));
                var guard = new OrganizationMutationGuard(db, principal, clock);
                var governor = new InvitationIssuanceGovernor(db, Options.Create(limits ?? new InvitationIssuanceOptions()), clock);
                IIdentityAuditWriter audit = failAudit ? new FailingAudit() : new IdentityAuditWriter(db, clock, NullLogger<IdentityAuditWriter>.Instance);
                var service = new InvitationIssuanceService(guard, governor, new InvitationStore(db), new InvitationProofService(), audit, clock, selectedStager);
                return await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(_ => operation(service));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(s => s.IssueAsync(organization.Id, "r@example.test", [custom.Id], "test")));
            Assert.Empty(await setup.OrganizationInvitations.Where(x => x.OrganizationId == organization.Id).ToListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(s => s.IssueAsync(organization.Id, "r@example.test", [custom.Id], "test"), stager, failAudit: true));
            Assert.Empty(await setup.OrganizationInvitations.Where(x => x.OrganizationId == organization.Id).ToListAsync());
            await Assert.ThrowsAsync<ForbiddenException>(() => Run(s => s.IssueAsync(organization.Id, "r@example.test", [seed.OwnerRole.Id], "test"), stager));
            await Assert.ThrowsAsync<ForbiddenException>(() => Run(s => s.IssueAsync(organization.Id, "r@example.test", [Guid.CreateVersion7()], "test"), stager));
            await Assert.ThrowsAsync<ConflictException>(() => Run(s => s.IssueAsync(organization.Id, owner.PrimaryEmail, [custom.Id], "test"), stager));
            OrganizationInvitation first = await Run(s => s.IssueAsync(organization.Id, " R@example.test ", [custom.Id], "test"), stager);
            string oldProof = stager.Proof!;
            Assert.Equal("[REDACTED]", stager.ProofObject!.ToString());
            ConflictException duplicate = await Assert.ThrowsAsync<ConflictException>(() => Run(s => s.IssueAsync(organization.Id, "r@example.test", [custom.Id], "test"), stager));
            Assert.Equal("Invitation.PendingOfferExists", duplicate.Code);
            InvitationRateLimitException cooldown = await Assert.ThrowsAsync<InvitationRateLimitException>(() => Run(s => s.ResendAsync(organization.Id, first.Id, 1, "test"), stager));
            Assert.Equal(60, cooldown.RetryAfterSeconds);
            clock.Now = clock.Now.AddSeconds(60);
            await using (var rename = new IdentityDbContext(options))
            {
                OrganizationRole stored = await rename.OrganizationRoles.SingleAsync(x => x.Id == custom.Id);
                stored.UpdateDetails("Renamed", null, clock.Now);
                await rename.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ConflictException>(() => Run(s => s.ResendAsync(organization.Id, first.Id, 2, "test"), stager));
            OrganizationInvitation resent = await Run(s => s.ResendAsync(organization.Id, first.Id, 1, "test"), stager);
            Assert.False(OrganizationInvitationProofCodec.Verify(organization.Id, first.Id, oldProof, resent.TokenVerifier));
            Assert.True(OrganizationInvitationProofCodec.Verify(organization.Id, first.Id, stager.Proof, resent.TokenVerifier));
            Assert.Equal("Original", stager.OfferName);
            Assert.Equal(2, await setup.OrganizationInvitations.AsNoTracking().Where(x => x.Id == first.Id).Select(x => x.Version).SingleAsync());
            clock.Now = clock.Now.AddSeconds(60);
            await using (var change = new IdentityDbContext(options))
            {
                OrganizationRole stored = await change.OrganizationRoles.SingleAsync(x => x.Id == custom.Id);
                stored.AdvancePermissionSetVersion(clock.Now);
                stored.AdvancePermissionSetVersion(clock.Now);
                await change.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ConflictException>(() => Run(s => s.ResendAsync(organization.Id, first.Id, 2, "test"), stager));
            OrganizationInvitation replacement = await Run(s => s.ReissueAsync(organization.Id, first.Id, 2, [custom.Id], "Permissions changed", "test"), stager);
            Assert.Equal("Permissions changed", await setup.OrganizationInvitations.AsNoTracking().Where(x => x.Id == first.Id).Select(x => x.CloseReason).SingleAsync());
            Assert.Equal(first.Id, replacement.ReplacesInvitationId);
            Assert.Equal("Renamed", stager.OfferName);
            Assert.Equal("cancelled", await setup.OrganizationInvitations.Where(x => x.Id == first.Id).Select(x => x.Status).SingleAsync());
            Assert.Equal(3, await setup.OrganizationInvitationDeliveries.CountAsync(x => x.OrganizationId == organization.Id));
            clock.Now = clock.Now.AddSeconds(60);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(s => s.ResendAsync(organization.Id, replacement.Id, 1, "test"), new ThrowingStager()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(s => s.ReissueAsync(organization.Id, replacement.Id, 1, [custom.Id], "Changed", "test"), new ThrowingStager()));
            Assert.Equal(3, await setup.OrganizationInvitationDeliveries.CountAsync(x => x.OrganizationId == organization.Id));
            Assert.Equal(1, await setup.OrganizationInvitations.Where(x => x.Id == replacement.Id).Select(x => x.Version).SingleAsync());
            Assert.Equal("pending", await setup.OrganizationInvitations.Where(x => x.Id == replacement.Id).Select(x => x.Status).SingleAsync());
            clock.Now = clock.Now.AddDays(8);
            OrganizationInvitation fresh = await Run(s => s.IssueAsync(organization.Id, "r@example.test", [custom.Id], "test"), stager);
            Assert.Equal("expired", await setup.OrganizationInvitations.Where(x => x.Id == replacement.Id).Select(x => x.Status).SingleAsync());
            Assert.Equal("pending", fresh.Status);
            await Assert.ThrowsAsync<ConflictException>(() => Run(s => s.CancelAsync(organization.Id, fresh.Id, fresh.Version + 1, "No longer needed", "test"), stager));
            Assert.Equal("pending", await setup.OrganizationInvitations.AsNoTracking().Where(x => x.Id == fresh.Id).Select(x => x.Status).SingleAsync());
            OrganizationInvitation cancelled = await Run(s => s.CancelAsync(organization.Id, fresh.Id, fresh.Version, "No longer needed", "test"), stager);
            Assert.Equal("cancelled", cancelled.Status);
            Assert.Equal("No longer needed", cancelled.CloseReason);
            async Task<bool> AttemptAsync()
            {
                try
                {
                    await Run(s => s.IssueAsync(organization.Id, "race@example.test", [custom.Id], "test"), new CapturingStager());
                    return true;
                }
                catch (ConflictException)
                {
                    return false;
                }
            }

            bool[] results = await Task.WhenAll(AttemptAsync(), AttemptAsync());
            Assert.Single(results, value => value);
            Assert.Single(results, value => !value);
            Assert.Equal(1, await setup.OrganizationInvitations.CountAsync(x => x.OrganizationId == organization.Id && x.RecipientEmail == "race@example.test" && x.Status == "pending"));
            clock.Now = clock.Now.AddDays(2);
            var limits = new InvitationIssuanceOptions { PairDailyLimit = 2, WorkspaceHourlyLimit = 100, PairCooldownSeconds = 60 };
            OrganizationInvitation limited = await Run(s => s.IssueAsync(organization.Id, "limited@example.test", [custom.Id], "test"), stager, limits);
            clock.Now = clock.Now.AddSeconds(60);
            await Run(s => s.ResendAsync(organization.Id, limited.Id, 1, "test"), stager, limits);
            clock.Now = clock.Now.AddSeconds(60);
            InvitationRateLimitException dailyLimit = await Assert.ThrowsAsync<InvitationRateLimitException>(() => Run(s => s.ResendAsync(organization.Id, limited.Id, 2, "test"), stager, limits));
            Assert.Equal(86280, dailyLimit.RetryAfterSeconds);
            Assert.Equal(2, await setup.OrganizationInvitationDeliveries.CountAsync(x => x.InvitationId == limited.Id));
            clock.Now = clock.Now.AddDays(2);
            await Run(
                async s =>
                {
                    OrganizationInvitation staged = await s.IssueAsync(organization.Id, "staged@example.test", [custom.Id], "test");
                    ConflictException stagedDuplicate = await Assert.ThrowsAsync<ConflictException>(() => s.IssueAsync(organization.Id, "STAGED@example.test", [custom.Id], "test"));
                    Assert.Equal("Invitation.PendingOfferExists", stagedDuplicate.Code);
                    InvitationRateLimitException stagedLimit = await Assert.ThrowsAsync<InvitationRateLimitException>(() => s.IssueAsync(organization.Id, "other-staged@example.test", [custom.Id], "test"));
                    Assert.Equal(3600, stagedLimit.RetryAfterSeconds);
                    return staged;
                },
                stager,
                new InvitationIssuanceOptions { WorkspaceHourlyLimit = 1 });
            Assert.Equal(1, await setup.OrganizationInvitations.CountAsync(x => x.OrganizationId == organization.Id && x.RecipientEmail == "staged@example.test"));
            OrganizationInvitation transition = await Run(s => s.IssueAsync(organization.Id, "transition@example.test", [custom.Id], "test"), stager);
            clock.Now = clock.Now.AddSeconds(60);
            async Task<bool> TransitionAsync(bool resend)
            {
                try
                {
                    await Run(s => resend ? s.ResendAsync(organization.Id, transition.Id, 1, "test") : s.ReissueAsync(organization.Id, transition.Id, 1, [custom.Id], "Changed", "test"), new CapturingStager());
                    return true;
                }
                catch (ConflictException)
                {
                    return false;
                }
            }

            bool[] transitions = await Task.WhenAll(TransitionAsync(true), TransitionAsync(false));
            Assert.Single(transitions, value => value);
            Assert.Single(transitions, value => !value);
            Assert.Equal(1, await setup.OrganizationInvitations.CountAsync(x => x.OrganizationId == organization.Id && x.RecipientEmail == "transition@example.test" && x.Status == "pending"));
            clock.Now = clock.Now.AddDays(2);
            var hourlyLimit = new InvitationIssuanceOptions { PairDailyLimit = 10, WorkspaceHourlyLimit = 2, PairCooldownSeconds = 60 };
            await Run(s => s.IssueAsync(organization.Id, "hourly-one@example.test", [custom.Id], "test"), stager, hourlyLimit);
            await Run(s => s.IssueAsync(organization.Id, "hourly-two@example.test", [custom.Id], "test"), stager, hourlyLimit);
            InvitationRateLimitException workspaceLimit = await Assert.ThrowsAsync<InvitationRateLimitException>(() => Run(s => s.IssueAsync(organization.Id, "hourly-three@example.test", [custom.Id], "test"), stager, hourlyLimit));
            Assert.Equal(3600, workspaceLimit.RetryAfterSeconds);
            clock.Now = clock.Now.AddHours(1);
            await Run(s => s.IssueAsync(organization.Id, "hourly-three@example.test", [custom.Id], "test"), stager, hourlyLimit);
        }
        finally
        {
            setup.ChangeTracker.Clear();
            await setup.IdentityAuditEvents.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationInvitationDeliveries.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationInvitationRoleOffers.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationInvitations.Where(x => x.OrganizationId == organization.Id && x.ReplacesInvitationId != null).ExecuteDeleteAsync();
            await setup.OrganizationInvitations.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationRolePermissions.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await setup.Users.Where(x => x.Id == owner.Id).ExecuteDeleteAsync();
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CurrentProvider(CurrentUser user) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => user;
    }

    private sealed class CapturingStager : IInvitationDeliveryStager
    {
        public string? Proof { get; private set; }
        public InvitationProof? ProofObject { get; private set; }
        public string? OfferName { get; private set; }
        public Task StageAsync(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery, InvitationProof proof, string correlationId, CancellationToken cancellationToken)
        {
            Proof = proof.Value;
            ProofObject = proof;
            OfferName = offers.Single().OfferedRoleName;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStager : IInvitationDeliveryStager
    {
        public Task StageAsync(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery, InvitationProof proof, string correlationId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("staging failed");
    }

    private sealed class FailingAudit : IIdentityAuditWriter
    {
        public Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("audit failed");

        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode)
        {
        }
    }
}
