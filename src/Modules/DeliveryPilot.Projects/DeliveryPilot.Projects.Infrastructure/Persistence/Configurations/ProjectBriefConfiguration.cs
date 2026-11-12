using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectBriefConfiguration : IEntityTypeConfiguration<ProjectBrief>
{
    public void Configure(EntityTypeBuilder<ProjectBrief> builder)
    {
        builder.HasKey(x => x.ProjectId).HasName("PkProjectBriefs");
        builder.Property(x => x.ProjectId).ValueGeneratedNever();
        builder.HasOne<Project>().WithOne()
            .HasForeignKey<ProjectBrief>(x => new { x.OrganizationId, x.ProjectId })
            .HasPrincipalKey<Project>(x => new { x.OrganizationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectBriefsProjects");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId }).IsUnique().HasDatabaseName("UxProjectBriefsOrganizationProject");
        foreach (string name in new[]
        {
            nameof(ProjectBrief.Problem), nameof(ProjectBrief.ObjectivesAndSuccessCriteria), nameof(ProjectBrief.ScopeAndExclusions),
            nameof(ProjectBrief.Assumptions), nameof(ProjectBrief.ConstraintsAndDependencies), nameof(ProjectBrief.OpenQuestions), nameof(ProjectBrief.ProductGoal),
        })
        {
            builder.Property<string?>(name).HasMaxLength(4000);
        }

        ProjectConfigurationRules.Version(builder, "ProjectBriefs");
        builder.ToTable("ProjectBriefs", "project", table =>
            table.HasCheckConstraint("CkProjectBriefsUpdate", "(\"UpdatedAt\" IS NULL AND \"UpdatedByUserId\" IS NULL) OR (\"UpdatedAt\" IS NOT NULL AND \"UpdatedByUserId\" IS NOT NULL)"));
    }
}
