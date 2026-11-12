using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DeliveryPilot.Projects.Tests.Persistence;

public sealed class ProjectModelTests
{
    [Fact]
    public void EveryProjectTableIsScopedAndChildrenHaveCompositeParentKeys()
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        IEntityType[] entities = context.Model.GetEntityTypes().Where(entity => entity.GetSchema() == "project").ToArray();
        Assert.Equal(8, entities.Length);
        foreach (IEntityType entity in entities)
        {
            Assert.NotNull(entity.FindProperty("OrganizationId"));
            Assert.Equal(ValueGenerated.Never, Assert.Single(entity.FindPrimaryKey()!.Properties).ValueGenerated);
            Assert.All(entity.GetForeignKeys(), foreignKey =>
            {
                Assert.Equal("OrganizationId", foreignKey.Properties[0].Name);
                Assert.Equal("OrganizationId", foreignKey.PrincipalKey.Properties[0].Name);
                Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
                Assert.True(foreignKey.PrincipalEntityType.GetSchema() == "project" || foreignKey.PrincipalEntityType.ClrType == typeof(Engagement));
            });

            if (entity.ClrType != typeof(Project))
            {
                IForeignKey parent = Assert.Single(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Project));
                Assert.Equal(["OrganizationId", "ProjectId"], parent.Properties.Select(property => property.Name));
                Assert.Equal(["OrganizationId", "Id"], parent.PrincipalKey.Properties.Select(property => property.Name));
            }

            foreach (IForeignKey link in entity.GetForeignKeys().Where(key => key.PrincipalEntityType.ClrType == typeof(ProjectParticipant) || key.PrincipalEntityType.ClrType == typeof(ProjectGuestInvitation)))
            {
                Assert.Equal(["OrganizationId", "ProjectId", "Id"], link.PrincipalKey.Properties.Select(property => property.Name));
                Assert.Equal(entity.ClrType == typeof(Project) ? "Id" : "ProjectId", link.Properties[1].Name);
            }
        }

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void SixIndependentVersionsAndPhysicalTypesAreMapped()
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        Type[] versioned = [typeof(Project), typeof(ProjectBrief), typeof(ProjectParticipant), typeof(ProjectGuestInvitation), typeof(ProjectGuestInvitationDelivery), typeof(ProjectStakeholder)];
        foreach (Type type in versioned)
        {
            IProperty version = context.Model.FindEntityType(type)!.FindProperty("Version")!;
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(1L, version.GetDefaultValue());
        }

        Assert.Null(context.Model.FindEntityType(typeof(ProjectParticipantRole))!.FindProperty("Version"));
        Assert.Null(context.Model.FindEntityType(typeof(ProjectEvent))!.FindProperty("Version"));
        Assert.Equal("smallint", context.Model.FindEntityType(typeof(Project))!.FindProperty(nameof(Project.SprintLengthWeeks))!.GetColumnType());
        Assert.Equal("jsonb", context.Model.FindEntityType(typeof(ProjectEvent))!.FindProperty(nameof(ProjectEvent.Details))!.GetColumnType());
        Assert.Equal("bytea", context.Model.FindEntityType(typeof(ProjectGuestInvitation))!.FindProperty(nameof(ProjectGuestInvitation.TokenVerifier))!.GetColumnType());
        Assert.Equal("timestamp with time zone", context.Model.FindEntityType(typeof(Project))!.FindProperty(nameof(Project.CreatedAt))!.GetColumnType());
        Assert.DoesNotContain(context.Model.FindEntityType(typeof(ProjectGuestInvitation))!.GetForeignKeys(), key => key.Properties.Any(property => property.Name == nameof(ProjectGuestInvitation.CurrentDeliveryMessageId)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AllSavePathsRejectTrackedProjectHistoryChangesBeforeConnecting(bool remove, bool asynchronous)
    {
        using ProjectsDbContext context = new ProjectsDbContextFactory().CreateDbContext([]);
        var projectId = Guid.CreateVersion7();
        var entry = ProjectEvent.Create(Guid.NewGuid(), projectId, "project.created", "Project", projectId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "{}");
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
