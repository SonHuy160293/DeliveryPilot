using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DeliveryPilot.Projects.Tests.Persistence;

public sealed class EngagementModelTests
{
    [Fact]
    public void TenantRelationshipsAndConcurrencyAreMappedWithoutExternalForeignKeys()
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        IEntityType[] entities = context.Model.GetEntityTypes().Where(entity => entity.GetSchema() == "engagement").ToArray();
        Assert.Equal(6, entities.Length);

        foreach (IEntityType entity in entities)
        {
            Assert.Equal(ValueGenerated.Never, entity.FindProperty("Id")!.ValueGenerated);
            Assert.NotNull(entity.FindProperty(entity.ClrType == typeof(EngagementLinkAttempt) ? "ActingOrganizationId" : "OrganizationId"));
            Assert.All(entity.GetForeignKeys(), foreignKey => Assert.Equal(typeof(Engagement), foreignKey.PrincipalEntityType.ClrType));
        }

        foreach (Type type in new[] { typeof(EngagementMilestone), typeof(EngagementContact), typeof(EngagementLinkCode), typeof(EngagementEvent) })
        {
            IForeignKey foreignKey = Assert.Single(context.Model.FindEntityType(type)!.GetForeignKeys());
            Assert.Equal(["OrganizationId", "EngagementId"], foreignKey.Properties.Select(property => property.Name));
            Assert.Equal(["OrganizationId", "Id"], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        }

        foreach (Type type in new[] { typeof(Engagement), typeof(EngagementMilestone), typeof(EngagementContact), typeof(EngagementLinkCode) })
        {
            IProperty version = context.Model.FindEntityType(type)!.FindProperty("Version")!;
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(1L, version.GetDefaultValue());
        }

        Assert.Empty(context.Model.FindEntityType(typeof(EngagementLinkAttempt))!.GetForeignKeys());
        Assert.Null(context.Model.FindEntityType(typeof(EngagementLinkAttempt))!.FindProperty("OrganizationId"));
        Assert.Equal(
            ["ActingOrganizationId", "AttemptedAt", "Id", "UserId"],
            context.Model.FindEntityType(typeof(EngagementLinkAttempt))!.GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PhysicalTypesAndOriginalDateImmutabilityAreExplicit()
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        IEntityType engagement = context.Model.FindEntityType(typeof(Engagement))!;
        Assert.Equal("numeric", engagement.FindProperty(nameof(Engagement.ContractAmount))!.GetColumnType());
        Assert.Null(engagement.FindProperty(nameof(Engagement.ContractAmount))!.GetPrecision());
        Assert.Equal("numeric", engagement.FindProperty(nameof(Engagement.PlannedEffortPersonDays))!.GetColumnType());
        Assert.Equal("date", engagement.FindProperty(nameof(Engagement.AgreementSignedOn))!.GetColumnType());
        Assert.Equal("timestamp with time zone", engagement.FindProperty(nameof(Engagement.CreatedAt))!.GetColumnType());
        Assert.Equal("jsonb", context.Model.FindEntityType(typeof(EngagementEvent))!.FindProperty(nameof(EngagementEvent.Details))!.GetColumnType());
        Assert.Equal("bytea", context.Model.FindEntityType(typeof(EngagementLinkCode))!.FindProperty(nameof(EngagementLinkCode.CodeVerifier))!.GetColumnType());
        Assert.Equal(
            PropertySaveBehavior.Throw,
            context.Model.FindEntityType(typeof(EngagementMilestone))!.FindProperty(nameof(EngagementMilestone.OriginalTargetDate))!.GetAfterSaveBehavior());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AllSavePathsRejectTrackedHistoryChangesBeforeConnecting(bool remove, bool asynchronous)
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        var engagementId = Guid.CreateVersion7();
        var entry = EngagementEvent.Create(Guid.NewGuid(), engagementId, "engagement.created", "Engagement", engagementId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "{}");
        context.Attach(entry);
        if (remove)
        {
            context.Remove(entry);
        }
        else
        {
            context.Entry(entry).Property(x => x.Reason).CurrentValue = "Changed";
        }

        if (asynchronous)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(false));
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => context.SaveChanges(false));
            Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        }
    }
}
