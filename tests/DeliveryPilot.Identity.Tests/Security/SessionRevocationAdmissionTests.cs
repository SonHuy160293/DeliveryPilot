using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class SessionRevocationAdmissionTests
{
    [PostgreSqlFact]
    public Task RevocationFirstPreventsMutationAsync() => RunRaceAsync(revocationFirst: true, rollbackMutation: false);

    [PostgreSqlFact]
    public Task MutationCommitPrecedesWaitingRevocationAsync() => RunRaceAsync(revocationFirst: false, rollbackMutation: false);

    [PostgreSqlFact]
    public Task MutationRollbackReleasesWaitingRevocationAsync() => RunRaceAsync(revocationFirst: false, rollbackMutation: true);

    private static async Task RunRaceAsync(bool revocationFirst, bool rollbackMutation)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"session-race-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        var organization = Organization.Create("Before", user.Id, now);
        var seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        var membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);
        var assignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, seed.OwnerRole.Id, user.Id, now);
        var session = AuthSession.Create(Guid.NewGuid(), user.Id, new byte[AuthSession.RefreshVerifierLength], now, now.AddDays(1));
        var otherUser = User.Create($"session-other-{Guid.NewGuid():N}@example.test", "Other", status: "active");
        var otherSession = AuthSession.Create(Guid.NewGuid(), otherUser.Id, new byte[AuthSession.RefreshVerifierLength], now, now.AddDays(1));
        await using var setup = new IdentityDbContext(options);
        try
        {
            setup.Users.Add(user);
            setup.Users.Add(otherUser);
            setup.Organizations.Add(organization);
            setup.OrganizationRoles.AddRange(seed.Roles);
            setup.OrganizationRolePermissions.AddRange(seed.Permissions);
            setup.OrganizationMemberships.Add(membership);
            setup.OrganizationRoleAssignments.Add(assignment);
            setup.AuthSessions.Add(session);
            setup.AuthSessions.Add(otherSession);
            await setup.SaveChangesAsync();

            await using var writer = new IdentityDbContext(options);
            await using var revoker = new IdentityDbContext(options);
            await using var writerTransaction = await writer.Database.BeginTransactionAsync();
            await using var revokeTransaction = await revoker.Database.BeginTransactionAsync();
            var principal = new StubCurrentUserProvider(new CurrentUser(user.Id.ToString(), SessionId: session.Id.ToString()));
            var guard = new OrganizationMutationGuard(writer, principal);
            var mismatched = new OrganizationMutationGuard(writer, new StubCurrentUserProvider(new CurrentUser(user.Id.ToString(), SessionId: otherSession.Id.ToString())));
            await Assert.ThrowsAsync<ForbiddenException>(() => mismatched.AcquireAsync(organization.Id));
            var tokenOptions = Options.Create(new AuthTokenOptions { SecretKey = new string('x', 32) });
            var sessionService = new AuthSessionService(revoker, new JwtAccessTokenIssuer(tokenOptions), tokenOptions, TimeProvider.System);
            if (revocationFirst)
            {
                Assert.True(await sessionService.RevokeAsync(user.Id, session.Id, CancellationToken.None));
                await revoker.SaveChangesAsync();
                Task<OrganizationMutationScope> pendingGuard = guard.AcquireAsync(organization.Id);
                await AssertWaitingForLockAsync(writer, options);
                await revokeTransaction.CommitAsync();
                await Assert.ThrowsAsync<ForbiddenException>(async () => await pendingGuard);
                await writerTransaction.RollbackAsync();
            }
            else
            {
                OrganizationMutationScope scope = await guard.AcquireAsync(organization.Id);
                scope.EnsureCapability(OrganizationCapabilityCatalog.RolesManage);
                await writer.Organizations.Where(x => x.Id == organization.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.Name, "After"));
                Task<bool> pendingRevocation = sessionService.RevokeAsync(user.Id, session.Id, CancellationToken.None);
                await AssertWaitingForLockAsync(revoker, options);
                if (rollbackMutation)
                {
                    await writerTransaction.RollbackAsync();
                }
                else
                {
                    await writerTransaction.CommitAsync();
                }

                Assert.True(await pendingRevocation);
                await revoker.SaveChangesAsync();
                await revokeTransaction.CommitAsync();
            }

            await using var verify = new IdentityDbContext(options);
            string name = await verify.Organizations.Where(x => x.Id == organization.Id).Select(x => x.Name).SingleAsync();
            Assert.Equal(revocationFirst || rollbackMutation ? "Before" : "After", name);
            Assert.NotNull((await verify.AuthSessions.SingleAsync(x => x.Id == session.Id)).RevokedAt);
        }
        finally
        {
            setup.ChangeTracker.Clear();
            await setup.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationRolePermissions.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setup.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await setup.AuthSessions.Where(x => x.Id == session.Id).ExecuteDeleteAsync();
            await setup.AuthSessions.Where(x => x.Id == otherSession.Id).ExecuteDeleteAsync();
            await setup.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
            await setup.Users.Where(x => x.Id == otherUser.Id).ExecuteDeleteAsync();
        }
    }

    private static async Task AssertWaitingForLockAsync(IdentityDbContext waiting, DbContextOptions<IdentityDbContext> options)
    {
        int pid = ((NpgsqlConnection)waiting.Database.GetDbConnection()).ProcessID;
        await using var observer = new IdentityDbContext(options);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            bool blocked = await observer.Database.SqlQuery<bool>($"SELECT cardinality(pg_blocking_pids({pid})) > 0 AS \"Value\"").SingleAsync();
            if (blocked)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The competing transaction did not wait for a PostgreSQL lock.");
    }

    private sealed class StubCurrentUserProvider(CurrentUser user) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => user;
    }
}
