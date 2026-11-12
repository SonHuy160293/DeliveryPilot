using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectParticipantRoleConfiguration : IEntityTypeConfiguration<ProjectParticipantRole>
{
    public void Configure(EntityTypeBuilder<ProjectParticipantRole> builder)
    {
        ProjectConfigurationRules.Key(builder, "ProjectParticipantRoles");
        ProjectConfigurationRules.Child(builder, "ProjectParticipantRoles");
        builder.Property(x => x.RoleCode).HasMaxLength(32).IsRequired();
        builder.HasOne<ProjectParticipant>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId, x.ParticipantId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectParticipantRolesParticipants");
        builder.ToTable("ProjectParticipantRoles", "project", table =>
        {
            table.HasCheckConstraint("CkProjectParticipantRolesCode", "\"RoleCode\" IN ('ProjectAdministrator', 'ProjectManager', 'BusinessAnalyst', 'Developer', 'QualityAssurance', 'DevOps', 'Viewer')");
            table.HasCheckConstraint("CkProjectParticipantRolesRevocation", "(\"RevokedAt\" IS NULL AND \"RevokedByUserId\" IS NULL) OR (\"RevokedAt\" IS NOT NULL AND \"RevokedByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ParticipantId, x.RoleCode }).IsUnique()
            .HasFilter("\"RevokedAt\" IS NULL").HasDatabaseName("UxProjectParticipantRolesActive");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.RoleCode })
            .HasFilter("\"RevokedAt\" IS NULL").HasDatabaseName("IxProjectParticipantRolesActiveProjectRole");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.ParticipantId }).HasDatabaseName("IxProjectParticipantRolesParticipant");
    }
}
