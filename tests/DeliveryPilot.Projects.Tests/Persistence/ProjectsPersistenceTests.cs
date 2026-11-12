using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Tests.Persistence;

[Collection("Projects PostgreSQL")]
public sealed class ProjectsPersistenceTests
{
    [Fact]
    public void ModelHasEngagementProjectAndReliabilityTablesAndAllMigrations()
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);

        string[] schemas = context.Model.GetEntityTypes()
            .Select(entity => entity.GetSchema() ?? "(default)")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["engagement", "project", "reliability"], schemas);
        Assert.Contains(context.Database.GetMigrations(), id => id.EndsWith("_InitialReliability", StringComparison.Ordinal));
        Assert.Contains(context.Database.GetMigrations(), id => id.EndsWith("_AddEngagementPersistence", StringComparison.Ordinal));
        Assert.Contains(context.Database.GetMigrations(), id => id.EndsWith("_AddProjectPersistence", StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task MigrationsApplyAndFailedCommandRollsBack()
    {
        DbContextOptions<ProjectsDbContext> options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(PostgreSqlFactAttribute.ConnectionString)
            .Options;
        await using var context = new ProjectsDbContext(options);
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        var unitOfWork = new ProjectsUnitOfWork(context);
        var messageId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<int>(
            _ =>
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    messageId,
                    "projects.test.v1",
                    1,
                    Guid.NewGuid(),
                    1,
                    "{}",
                    Guid.NewGuid(),
                    null,
                    DateTimeOffset.UtcNow));
                throw new InvalidOperationException("Simulated handler failure.");
            }));

        Assert.False(context.ChangeTracker.HasChanges());
        Assert.False(await context.Set<OutboxMessage>().AnyAsync(message => message.MessageId == messageId));
    }
}
