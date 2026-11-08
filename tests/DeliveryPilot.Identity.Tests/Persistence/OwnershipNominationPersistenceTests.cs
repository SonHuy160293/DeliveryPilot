using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static DeliveryPilot.Identity.Tests.Organizations.InvitationManagementHttpTests;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class OwnershipNominationPersistenceTests
{
    [Fact]
    public void DomainLifecycleUsesFixedUtcDeadlineAndRejectsInvalidTransitionsWithoutMutation()
    {
        Guid actor = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 11, 5, 12, 0, 0, TimeSpan.FromHours(7));
        OrganizationOwnershipNomination New() => OrganizationOwnershipNomination.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), actor, now);
        OrganizationOwnershipNomination pending = New();
        Assert.Equal(TimeSpan.Zero, pending.CreatedAt.Offset);
        Assert.Equal(now.ToUniversalTime().AddDays(7), pending.ExpiresAt);
        Assert.Equal(1, pending.Version);
        Assert.Throws<ArgumentException>(() => OrganizationOwnershipNomination.Create(Guid.Empty, pending.MembershipId, actor, now));
        foreach (string status in new[] { "accepted", "declined", "cancelled" })
        {
            OrganizationOwnershipNomination invalid = New();
            Assert.Throws<InvalidOperationException>(() => invalid.Close(status, actor, invalid.ExpiresAt));
            Assert.Throws<InvalidOperationException>(() => invalid.Close(status, null, now));
            Assert.Throws<InvalidOperationException>(() => invalid.Close(status, actor, now.AddTicks(-1)));
            Assert.Equal("pending", invalid.Status);
            Assert.Equal(1, invalid.Version);
            Assert.Null(invalid.ClosedAt);
            invalid.Close(status, actor, invalid.ExpiresAt.AddTicks(-1), status == "cancelled" ? "Cancelled after review" : null);
            Assert.Equal(2, invalid.Version);
            Assert.Equal(actor, invalid.ClosedByUserId);
            Assert.Throws<InvalidOperationException>(() => invalid.Close(status, actor, invalid.ExpiresAt.AddTicks(-1)));
        }

        Assert.Throws<InvalidOperationException>(() => pending.Close("expired", null, pending.ExpiresAt.AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => pending.Close("expired", actor, pending.ExpiresAt));
        pending.Close("expired", null, pending.ExpiresAt);
        Assert.Equal("expired", pending.Status);
        Assert.Null(pending.ClosedByUserId);
        Assert.Equal(2, pending.Version);
    }

    [PostgreSqlFact]
    public async Task DatabaseEnforcesScopedReferencesPendingUniquenessClosureAndRestrictedDeletes()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using var db = fixture.NewDb();
        Guid target = await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Ordinary.Id).Select(item => item.Id).SingleAsync();
        var nomination = OrganizationOwnershipNomination.Create(fixture.Organization.Id, target, fixture.Owner.Id, fixture.Clock.Now);
        db.OrganizationOwnershipNominations.Add(nomination);
        await db.SaveChangesAsync();
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, async context =>
        {
            context.OrganizationOwnershipNominations.Add(OrganizationOwnershipNomination.Create(fixture.Organization.Id, target, fixture.Owner.Id, fixture.Clock.Now));
            await context.SaveChangesAsync();
        });
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, async context =>
        {
            context.OrganizationOwnershipNominations.Add(OrganizationOwnershipNomination.Create(fixture.ForeignOrganization.Id, target, fixture.Foreign.Id, fixture.Clock.Now));
            await context.SaveChangesAsync();
        });
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, context => context.OrganizationOwnershipNominations.Where(item => item.Id == nomination.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.ExpiresAt, nomination.CreatedAt.AddDays(8))));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, context => context.OrganizationOwnershipNominations.Where(item => item.Id == nomination.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "accepted")));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, context => context.OrganizationOwnershipNominations.Where(item => item.Id == nomination.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, 0L)));
        await AssertSqlStateAsync(PostgresErrorCodes.RestrictViolation, context => context.OrganizationMemberships.Where(item => item.Id == target).ExecuteDeleteAsync());
        await AssertSqlStateAsync(PostgresErrorCodes.RestrictViolation, context => context.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteDeleteAsync());
        nomination.Close("declined", fixture.Ordinary.Id, fixture.Clock.Now);
        await db.SaveChangesAsync();
        db.OrganizationOwnershipNominations.Add(OrganizationOwnershipNomination.Create(fixture.Organization.Id, target, fixture.Owner.Id, fixture.Clock.Now));
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.OrganizationOwnershipNominations.CountAsync(item => item.OrganizationId == fixture.Organization.Id));

        async Task AssertSqlStateAsync(string expected, Func<IdentityDbContext, Task> action)
        {
            await using var context = fixture.NewDb();
            Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => action(context));
            PostgresException postgres = exception as PostgresException ?? Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(expected, postgres.SqlState);
        }
    }
}
