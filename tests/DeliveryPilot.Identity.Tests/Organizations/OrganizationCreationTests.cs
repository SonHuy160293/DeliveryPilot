using System.Text.Json;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationCreationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidatorChecksNameAndAttemptBoundaries()
    {
        var validator = new CreateOrganizationValidator();
        await validator.ValidateAsync(new CreateOrganizationCommand(" " + new string('a', 160) + " ", Guid.NewGuid(), "test"), CancellationToken.None);
        await Assert.ThrowsAsync<CqrsValidationException>(() => validator.ValidateAsync(new CreateOrganizationCommand(" ", Guid.NewGuid(), "test"), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() => validator.ValidateAsync(new CreateOrganizationCommand(new string('a', 161), Guid.NewGuid(), "test"), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() => validator.ValidateAsync(new CreateOrganizationCommand("Name", Guid.Empty, "test"), CancellationToken.None));
    }

    [PostgreSqlFact]
    public async Task SnapshotReplaysOnlyWithCurrentAuditAuthorityAndNeverRestoresOwner()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid key = Guid.NewGuid();
        var clock = new TestClock(Now);
        CreateOrganizationResult created = await ExecuteAsync(userId, sessionId, key, "  Original  ", clock);
        Assert.False(created.IsReplay);
        Assert.Equal("Original", created.Snapshot.Name);
        await using (var context = Context())
        {
            Assert.Equal(1, await context.OrganizationCreationAttempts.CountAsync(x => x.UserId == userId && x.AttemptId == key));
            Assert.Equal(6, await context.IdentityAuditEvents.CountAsync(x => x.OrganizationId == created.Snapshot.OrganizationId));
            var roles = await context.OrganizationRoles.Where(x => x.OrganizationId == created.Snapshot.OrganizationId).ToListAsync();
            var permissions = await context.OrganizationRolePermissions.Where(x => x.OrganizationId == created.Snapshot.OrganizationId).ToListAsync();
            OrganizationRole ownerRole = Assert.Single(roles, role => role.Code == OrganizationCapabilityCatalog.OwnerRoleCode);
            OrganizationRole adminRole = Assert.Single(roles, role => role.Code == OrganizationCapabilityCatalog.AdminRoleCode);
            OrganizationRole memberRole = Assert.Single(roles, role => role.Code == OrganizationCapabilityCatalog.MemberRoleCode);
            Assert.True(permissions.Where(permission => permission.RoleId == ownerRole.Id).Select(permission => permission.PermissionCode)
                .ToHashSet(StringComparer.Ordinal).SetEquals(OrganizationCapabilityCatalog.AllPermissionCodes));
            Assert.True(permissions.Where(permission => permission.RoleId == adminRole.Id).Select(permission => permission.PermissionCode)
                .ToHashSet(StringComparer.Ordinal).SetEquals(OrganizationCapabilityCatalog.DelegablePermissionCodes));
            Assert.DoesNotContain(permissions, permission => permission.RoleId == memberRole.Id);
            await context.Organizations.Where(x => x.Id == created.Snapshot.OrganizationId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Name, "Changed").SetProperty(x => x.Version, 2L));
        }

        CreateOrganizationResult replay = await ExecuteAsync(userId, sessionId, key, "Original", clock);
        Assert.True(replay.IsReplay);
        Assert.Equal(JsonSerializer.Serialize(created.Snapshot), JsonSerializer.Serialize(replay.Snapshot));
        ConflictException changed = await Assert.ThrowsAsync<ConflictException>(() => ExecuteAsync(userId, sessionId, key, "Different", clock));
        Assert.Equal("Organization.CreationAttemptConflict", changed.Code);

        await using (var context = Context())
        {
            OrganizationRoleAssignment assignment = await context.OrganizationRoleAssignments.SingleAsync(x => x.MembershipId == created.Snapshot.MembershipId);
            assignment.Revoke(userId, Now.AddMinutes(1));
            Guid memberRole = await context.OrganizationRoles.Where(x => x.OrganizationId == created.Snapshot.OrganizationId && x.Code == "member").Select(x => x.Id).SingleAsync();
            context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(created.Snapshot.OrganizationId, created.Snapshot.MembershipId, memberRole, userId, Now.AddMinutes(1)));
            await context.SaveChangesAsync();
        }

        ForbiddenException denied = await Assert.ThrowsAsync<ForbiddenException>(() => ExecuteAsync(userId, sessionId, key, "Original", clock));
        Assert.Equal("Organization.AccessDenied", denied.Code);
        await using (var context = Context())
        {
            Guid adminRole = await context.OrganizationRoles.Where(x => x.OrganizationId == created.Snapshot.OrganizationId && x.Code == "admin").Select(x => x.Id).SingleAsync();
            context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(created.Snapshot.OrganizationId, created.Snapshot.MembershipId, adminRole, userId, Now.AddMinutes(2)));
            await context.SaveChangesAsync();
        }

        replay = await ExecuteAsync(userId, sessionId, key, "Original", clock);
        Assert.Equal(JsonSerializer.Serialize(created.Snapshot), JsonSerializer.Serialize(replay.Snapshot));
        clock.UtcNow = Now.AddHours(24);
        ConflictException expired = await Assert.ThrowsAsync<ConflictException>(() => ExecuteAsync(userId, sessionId, key, "Different", clock));
        Assert.Equal("Organization.CreationAttemptExpired", expired.Code);
        await using (var context = Context())
        {
            await context.OrganizationMemberships.Where(x => x.Id == created.Snapshot.MembershipId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "removed"));
        }

        await Assert.ThrowsAsync<ForbiddenException>(() => ExecuteAsync(userId, sessionId, key, "Original", clock));
    }

    [PostgreSqlFact]
    public async Task SameKeyConcurrentRequestsCreateOnlyOneCompleteOutcome()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid key = Guid.NewGuid();
        var clock = new TestClock(Now);
        CreateOrganizationResult[] results = await Task.WhenAll(
            ExecuteAsync(userId, sessionId, key, "Team", clock),
            ExecuteAsync(userId, sessionId, key, "Team", clock));
        Assert.Single(results.Where(result => result.IsReplay));
        Assert.Equal(results[0].Snapshot.OrganizationId, results[1].Snapshot.OrganizationId);
        await using var context = Context();
        Guid organizationId = results[0].Snapshot.OrganizationId;
        Assert.Equal(1, await context.OrganizationCreationAttempts.CountAsync(x => x.UserId == userId && x.AttemptId == key));
        Assert.Equal(1, await context.OrganizationMemberships.CountAsync(x => x.OrganizationId == organizationId));
        Assert.Equal(1, await context.OrganizationRoleAssignments.CountAsync(x => x.OrganizationId == organizationId));
        Assert.Equal(3, await context.OrganizationRoles.CountAsync(x => x.OrganizationId == organizationId));
        Assert.Equal(6, await context.IdentityAuditEvents.CountAsync(x => x.OrganizationId == organizationId));
        CreateOrganizationResult independent = await ExecuteAsync(userId, sessionId, Guid.NewGuid(), "Team", clock);
        Assert.NotEqual(organizationId, independent.Snapshot.OrganizationId);
    }

    [PostgreSqlFact]
    public async Task AccountAndSessionChangesCommittedBeforeLockDenyCreation()
    {
        foreach (bool revokeSession in new[] { false, true })
        {
            (Guid userId, Guid sessionId) = await SeedAsync();
            await using var other = Context();
            await using var transaction = await other.Database.BeginTransactionAsync();
            if (revokeSession)
            {
                await other.AuthSessions.Where(x => x.Id == sessionId).ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, Now));
            }
            else
            {
                await other.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "suspended"));
            }

            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<CreateOrganizationResult> creation = ExecuteAsync(userId, sessionId, Guid.NewGuid(), "Blocked", new TestClock(Now),
                inner => new ObservedStore(inner, beforeEligibility: () => entered.TrySetResult()));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<TimeoutException>(() => creation.WaitAsync(TimeSpan.FromMilliseconds(200)));
            await transaction.CommitAsync();
            await Assert.ThrowsAsync<UnauthorizedException>(() => creation);
            await using var verify = Context();
            Assert.Equal(0, await verify.Organizations.CountAsync(x => x.CreatedByUserId == userId));
        }
    }

    [PostgreSqlFact]
    public async Task AccountAndSessionChangesWaitForCreationCommit()
    {
        foreach (bool revokeSession in new[] { false, true })
        {
            (Guid userId, Guid sessionId) = await SeedAsync();
            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<CreateOrganizationResult> creation = ExecuteAsync(userId, sessionId, Guid.NewGuid(), "Protected", new TestClock(Now),
                inner => new ObservedStore(inner, afterEligibility: async () => { locked.TrySetResult(); await release.Task; }));
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var other = Context();
            Task<int> change = revokeSession
                ? other.AuthSessions.Where(x => x.Id == sessionId).ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, Now))
                : other.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "suspended"));
            await Assert.ThrowsAsync<TimeoutException>(() => change.WaitAsync(TimeSpan.FromMilliseconds(200)));
            release.TrySetResult();
            CreateOrganizationResult result = await creation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, await change.WaitAsync(TimeSpan.FromSeconds(10)));
            await using var verify = Context();
            Assert.Equal(1, await verify.OrganizationCreationAttempts.CountAsync(x => x.OrganizationId == result.Snapshot.OrganizationId));
        }
    }

    [PostgreSqlFact]
    public async Task ReplayAndMembershipRevocationSerializeInBothOrders()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid key = Guid.NewGuid();
        var clock = new TestClock(Now);
        CreateOrganizationResult created = await ExecuteAsync(userId, sessionId, key, "Race", clock);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CreateOrganizationResult> replay = ExecuteAsync(userId, sessionId, key, "Race", clock,
            inner => new ObservedStore(inner, afterEligibility: async () =>
            {
                locked.TrySetResult();
                await release.Task;
            }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var other = Context();
        await using var transaction = await other.Database.BeginTransactionAsync();
        Guid organizationId = created.Snapshot.OrganizationId;
        Task<int> workspaceLock = other.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM shared."Organizations" WHERE "Id" = {organizationId} FOR UPDATE""");
        await Assert.ThrowsAsync<TimeoutException>(() => workspaceLock.WaitAsync(TimeSpan.FromMilliseconds(200)));
        release.TrySetResult();
        Assert.True((await replay.WaitAsync(TimeSpan.FromSeconds(10))).IsReplay);
        await workspaceLock.WaitAsync(TimeSpan.FromSeconds(10));
        await other.OrganizationMemberships.Where(x => x.Id == created.Snapshot.MembershipId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "removed"));
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => ExecuteAsync(userId, sessionId, key, "Race", clock));

        (userId, sessionId) = await SeedAsync();
        key = Guid.NewGuid();
        created = await ExecuteAsync(userId, sessionId, key, "Other race", clock);
        await using var revokeContext = Context();
        await using var revokeTransaction = await revokeContext.Database.BeginTransactionAsync();
        organizationId = created.Snapshot.OrganizationId;
        await revokeContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM shared."Organizations" WHERE "Id" = {organizationId} FOR UPDATE""");
        Task<CreateOrganizationResult> blockedReplay = ExecuteAsync(userId, sessionId, key, "Other race", clock);
        await Assert.ThrowsAsync<TimeoutException>(() => blockedReplay.WaitAsync(TimeSpan.FromMilliseconds(200)));
        OrganizationRoleAssignment ownerAssignment = await revokeContext.OrganizationRoleAssignments
            .SingleAsync(x => x.MembershipId == created.Snapshot.MembershipId);
        ownerAssignment.Revoke(userId, Now.AddMinutes(1));
        Guid memberRoleId = await revokeContext.OrganizationRoles
            .Where(x => x.OrganizationId == organizationId && x.Code == "member").Select(x => x.Id).SingleAsync();
        revokeContext.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            organizationId, created.Snapshot.MembershipId, memberRoleId, userId, Now.AddMinutes(1)));
        await revokeContext.SaveChangesAsync();
        await revokeTransaction.CommitAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => blockedReplay.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [PostgreSqlFact]
    public async Task NewCreationAndReplayForTheSameUserDoNotInvertWorkspaceLocks()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid existingKey = Guid.NewGuid();
        var clock = new TestClock(Now);
        CreateOrganizationResult original = await ExecuteAsync(userId, sessionId, existingKey, "Existing", clock);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CreateOrganizationResult> newCreation = ExecuteAsync(userId, sessionId, Guid.NewGuid(), "New", clock,
            inner => new ObservedStore(inner, afterEligibility: async () =>
            {
                locked.TrySetResult();
                await release.Task;
            }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task<CreateOrganizationResult> replay = ExecuteAsync(userId, sessionId, existingKey, "Existing", clock);
        await Assert.ThrowsAsync<TimeoutException>(() => replay.WaitAsync(TimeSpan.FromMilliseconds(200)));
        release.TrySetResult();
        CreateOrganizationResult created = await newCreation.WaitAsync(TimeSpan.FromSeconds(10));
        CreateOrganizationResult replayed = await replay.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotEqual(original.Snapshot.OrganizationId, created.Snapshot.OrganizationId);
        Assert.Equal(original.Snapshot.OrganizationId, replayed.Snapshot.OrganizationId);
        Assert.True(replayed.IsReplay);
    }

    [PostgreSqlFact]
    public async Task AuditAndReceiptPersistenceFailuresRollBackAllCreationState()
    {
        foreach (bool failReceipt in new[] { false, true })
        {
            (Guid userId, Guid sessionId) = await SeedAsync();
            Guid key = Guid.NewGuid();
            Task<CreateOrganizationResult> creation = ExecuteAsync(userId, sessionId, key, "Rollback", new TestClock(Now),
                failReceipt ? inner => new ObservedStore(inner, corruptReceipt: true) : null, failAudit: !failReceipt);
            if (failReceipt)
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => creation);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => creation);
            }
            await using var verify = Context();
            Assert.Equal(0, await verify.Organizations.CountAsync(x => x.CreatedByUserId == userId));
            Assert.Equal(0, await verify.OrganizationCreationAttempts.CountAsync(x => x.UserId == userId));
            Assert.Equal(0, await verify.OrganizationMemberships.CountAsync(x => x.UserId == userId));
            Assert.Equal(0, await verify.IdentityAuditEvents.CountAsync(x => x.ActorUserId == userId));
        }
    }

    [PostgreSqlFact]
    public async Task CancellationReleasesAttemptLockWithoutPersistingFragments()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid key = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        await using var holder = Context();
        await using var transaction = await holder.Database.BeginTransactionAsync();
        await new OrganizationCreationStore(holder).LockAttemptAsync(userId, key, CancellationToken.None);
        Task<CreateOrganizationResult> waiting = ExecuteAsync(userId, sessionId, key, "Cancelled", new TestClock(Now),
            inner => new ObservedStore(inner, beforeAttempt: () => entered.TrySetResult()), cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await transaction.CommitAsync();
        CreateOrganizationResult result = await ExecuteAsync(userId, sessionId, key, "Cancelled", new TestClock(Now));
        Assert.False(result.IsReplay);
        await using var verify = Context();
        Assert.Equal(1, await verify.OrganizationCreationAttempts.CountAsync(x => x.UserId == userId && x.AttemptId == key));
    }

    [PostgreSqlFact]
    public async Task ReceiptConstraintsRejectInvalidFingerprintDeadlineAndSnapshot()
    {
        (Guid userId, Guid sessionId) = await SeedAsync();
        Guid key = Guid.NewGuid();
        await ExecuteAsync(userId, sessionId, key, "Constraints", new TestClock(Now));
        string[] updates =
        [
            "\"RequestFingerprint\" = decode('00', 'hex')",
            "\"ReplayUntil\" = \"CreatedAt\" + interval '23 hours'",
            "\"ResponseSchemaVersion\" = 2",
            "\"ResultSnapshot\" = jsonb_set(\"ResultSnapshot\", '{organizationId}', '\"00000000-0000-0000-0000-000000000001\"'::jsonb)",
            "\"ResultSnapshot\" = jsonb_set(\"ResultSnapshot\", '{roles}', '[]'::jsonb)",
            "\"ResultSnapshot\" = \"ResultSnapshot\" || '{\"secret\":\"not-allowed\"}'::jsonb",
        ];
        foreach (string update in updates)
        {
            await using var context = Context();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await using var command = (NpgsqlCommand)context.Database.GetDbConnection().CreateCommand();
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
            command.CommandText = $"UPDATE shared.\"OrganizationCreationAttempts\" SET {update} WHERE \"UserId\" = @userId AND \"AttemptId\" = @attemptId";
            command.Parameters.AddWithValue("userId", userId);
            command.Parameters.AddWithValue("attemptId", key);
            PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            await transaction.RollbackAsync();
        }
    }

    private static IdentityDbContext Context() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options);

    private static async Task<(Guid UserId, Guid SessionId)> SeedAsync()
    {
        await using var context = Context();
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Creator", status: "active");
        Guid sessionId = Guid.NewGuid();
        context.Users.Add(user);
        context.AuthSessions.Add(AuthSession.Create(sessionId, user.Id, new byte[32], Now, Now.AddDays(7)));
        await context.SaveChangesAsync();
        return (user.Id, sessionId);
    }

    private static async Task<CreateOrganizationResult> ExecuteAsync(Guid userId, Guid sessionId, Guid key, string name,
        TimeProvider clock, Func<IOrganizationCreationStore, IOrganizationCreationStore>? wrap = null,
        bool failAudit = false, CancellationToken cancellationToken = default)
    {
        await using var context = Context();
        var current = new StubCurrentUserProvider(new CurrentUser(userId.ToString(), SessionId: sessionId.ToString()));
        IOrganizationCreationStore store = new OrganizationCreationStore(context);
        store = wrap?.Invoke(store) ?? store;
        IIdentityAuditWriter audit = new IdentityAuditWriter(context, clock, NullLogger<IdentityAuditWriter>.Instance);
        if (failAudit)
        {
            audit = new FailingAuditWriter(audit);
        }

        var handler = new CreateOrganizationHandler(store, new OrganizationMutationGuard(context, current, clock),
            new OrganizationAuthorizationService(context, current, clock), audit, clock, current);
        var unit = new IdentityUnitOfWork(context);
        var response = await unit.ExecuteInTransactionAsync(ct => handler.HandleAsync(new CreateOrganizationCommand(name, key, "test"), ct), cancellationToken);
        return response.Value;
    }

    private sealed class StubCurrentUserProvider(CurrentUser user) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => user;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class ObservedStore(IOrganizationCreationStore inner, Action? beforeEligibility = null, Func<Task>? afterEligibility = null,
        Action? beforeAttempt = null, bool corruptReceipt = false) : IOrganizationCreationStore
    {
        public Task LockAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken)
        {
            beforeAttempt?.Invoke();
            return inner.LockAttemptAsync(userId, attemptId, cancellationToken);
        }

        public Task<OrganizationCreationAttempt?> FindAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken) => inner.FindAttemptAsync(userId, attemptId, cancellationToken);

        public async Task LockUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            beforeEligibility?.Invoke();
            await inner.LockUserAsync(userId, cancellationToken);
        }

        public async Task LockSessionAndCheckEligibilityAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            await inner.LockSessionAndCheckEligibilityAsync(userId, sessionId, cancellationToken);
            if (afterEligibility is not null)
            {
                await afterEligibility();
            }
        }

        public void Stage(Organization organization, IReadOnlyCollection<OrganizationRole> roles, IReadOnlyCollection<OrganizationRolePermission> permissions,
            OrganizationMembership membership, OrganizationRoleAssignment assignment, OrganizationCreationAttempt attempt)
        {
            inner.Stage(organization, roles, permissions, membership, assignment, attempt);
            if (corruptReceipt)
            {
                typeof(OrganizationCreationAttempt).GetProperty(nameof(OrganizationCreationAttempt.ResponseSchemaVersion))!.SetValue(attempt, (short)2);
            }
        }
    }

    private sealed class FailingAuditWriter(IIdentityAuditWriter inner) : IIdentityAuditWriter
    {
        private int _count;

        public async Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (++_count == 2)
            {
                throw new InvalidOperationException("Injected audit failure.");
            }

            return await inner.StageAsync(entry, cancellationToken);
        }

        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode)
            => inner.RecordDenialTelemetry(eventType, organizationId, actorUserId, correlationId, denialCode);
    }
}
