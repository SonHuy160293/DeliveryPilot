using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationReadStoreTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(101, null)]
    [InlineData(50, "")]
    [InlineData(50, "not-a-cursor")]
    public async Task InvalidPagesAreRejectedBeforeQuerying(int limit, string? cursor)
    {
        await using var context = CreateContext("Host=127.0.0.1;Port=1;Database=unavailable;Username=postgres;Password=invalid;Timeout=1");
        var store = new OrganizationReadStore(context, TimeProvider.System);
        await Assert.ThrowsAsync<BadRequestException>(() => store.ListWorkspacesAsync(Guid.NewGuid(), limit, cursor, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidMembershipStatusIsRejectedBeforeQuerying()
    {
        await using var context = CreateContext("Host=127.0.0.1;Port=1;Database=unavailable;Username=postgres;Password=invalid;Timeout=1");
        var store = new OrganizationReadStore(context, TimeProvider.System);
        await Assert.ThrowsAsync<BadRequestException>(() => store.ListMembersAsync(Guid.NewGuid(), "all", 50, null, CancellationToken.None));
    }

    [Fact]
    public void PostgreSqlProviderTranslatesTimestampAndUuidKeyset()
    {
        using var context = CreateContext("Host=127.0.0.1;Database=unused;Username=postgres;Password=unused");
        var after = DateTimeOffset.UtcNow;
        Guid id = Guid.NewGuid();
        string sql = context.OrganizationMemberships.AsNoTracking()
            .Where(x => x.CreatedAt > after || (x.CreatedAt == after && x.Id.CompareTo(id) > 0))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(51).ToQueryString();
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
    }

    [PostgreSqlFact]
    public async Task WorkspacePagesUseStableKeysAndExcludeOtherUsers()
    {
        await using var context = CreateContext(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!);
        var now = DateTimeOffset.UtcNow;
        var user = User.Create($"read-{Guid.NewGuid():N}@example.test", "Reader", status: "active");
        var other = User.Create($"read-{Guid.NewGuid():N}@example.test", "Other", status: "active");
        var first = Organization.Create("Same", user.Id, now);
        var second = Organization.Create("Same", user.Id, now);
        var foreign = Organization.Create("Foreign", other.Id, now);
        context.Users.AddRange(user, other);
        context.Organizations.AddRange(first, second, foreign);
        var firstMembership = OrganizationMembership.Create(first.Id, user.Id, user.Id, now);
        var secondMembership = OrganizationMembership.Create(second.Id, user.Id, user.Id, now);
        var foreignMembership = OrganizationMembership.Create(foreign.Id, other.Id, other.Id, now);
        context.OrganizationMemberships.AddRange(firstMembership, secondMembership, foreignMembership);
        try
        {
            await context.SaveChangesAsync();
            var store = new OrganizationReadStore(context, TimeProvider.System);
            var page1 = await store.ListWorkspacesAsync(user.Id, 1, null, CancellationToken.None);
            Assert.Single(page1.Items);
            Assert.NotNull(page1.NextCursor);
            var page2 = await store.ListWorkspacesAsync(user.Id, 1, page1.NextCursor, CancellationToken.None);
            Assert.Single(page2.Items);
            Assert.Null(page2.NextCursor);
            Assert.Equal(new[] { first.Id, second.Id }.Order(), page1.Items.Concat(page2.Items).Select(x => x.OrganizationId).Order());
            var members = await store.ListMembersAsync(first.Id, "active", 50, null, CancellationToken.None);
            var member = Assert.Single(members.Items);
            Assert.Equal(firstMembership.Id, member.MembershipId);
            Assert.Empty(member.Roles);
            Assert.Null(await store.GetMemberAsync(first.Id, foreignMembership.Id, CancellationToken.None));
            Assert.Empty((await store.ListMembersAsync(first.Id, "removed", 50, null, CancellationToken.None)).Items);
        }
        finally
        {
            context.ChangeTracker.Clear();
            Guid[] ids = [first.Id, second.Id, foreign.Id];
            await context.OrganizationMemberships.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await context.Organizations.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            await context.Users.Where(x => x.Id == user.Id || x.Id == other.Id).ExecuteDeleteAsync();
        }
    }

    private static IdentityDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString).Options);
}
