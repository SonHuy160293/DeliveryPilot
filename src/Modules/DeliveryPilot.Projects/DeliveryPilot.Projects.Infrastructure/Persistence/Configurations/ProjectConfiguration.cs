using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        ProjectConfigurationRules.Key(builder, "Projects");
        ProjectConfigurationRules.Version(builder, "Projects");
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id }).HasName("AkProjectsOrganizationId");
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.SprintLengthWeeks).HasDefaultValue((short)2);
        builder.Property(x => x.ClientPoWaiverReason).HasMaxLength(2000);
        builder.HasOne<Engagement>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.EngagementId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectsEngagements");
        builder.HasOne<ProjectParticipant>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.Id, x.ClientProductOwnerParticipantId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectsClientProductOwner");
        builder.ToTable("Projects", "project", table =>
        {
            table.HasCheckConstraint("CkProjectsIds", "\"OrganizationId\" <> '00000000-0000-0000-0000-000000000000' AND \"CreatedByUserId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CkProjectsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkProjectsPurpose", "char_length(btrim(\"Purpose\")) BETWEEN 1 AND 500 AND \"Purpose\" = btrim(\"Purpose\")");
            table.HasCheckConstraint("CkProjectsKind", "\"Kind\" IN ('Client', 'Internal')");
            table.HasCheckConstraint("CkProjectsEngagement", "\"Kind\" = 'Client' OR \"EngagementId\" IS NULL");
            table.HasCheckConstraint("CkProjectsStatus", "\"Status\" IN ('Draft', 'Active')");
            table.HasCheckConstraint("CkProjectsSprintLength", "\"SprintLengthWeeks\" BETWEEN 1 AND 4");
            table.HasCheckConstraint("CkProjectsClientPo", "(\"ClientProductOwnerParticipantId\" IS NULL AND \"ClientPoAssignedAt\" IS NULL AND \"ClientPoAssignedByUserId\" IS NULL) OR (\"ClientProductOwnerParticipantId\" IS NOT NULL AND \"ClientPoAssignedAt\" IS NOT NULL AND \"ClientPoAssignedByUserId\" IS NOT NULL)");
            table.HasCheckConstraint("CkProjectsActivation", "(\"Status\" = 'Draft' AND \"ActivatedAt\" IS NULL AND \"ActivatedByUserId\" IS NULL) OR (\"Status\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"ActivatedByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.EngagementId }).HasDatabaseName("IxProjectsOrganizationEngagement");
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAt, x.Id }).HasDatabaseName("IxProjectsOrganizationStatusCreatedId");
        builder.HasIndex(x => new { x.OrganizationId, x.Id, x.ClientProductOwnerParticipantId }).HasDatabaseName("IxProjectsClientProductOwner");
    }
}
