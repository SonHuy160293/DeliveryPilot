using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectParticipantConfiguration : IEntityTypeConfiguration<ProjectParticipant>
{
    public void Configure(EntityTypeBuilder<ProjectParticipant> builder)
    {
        ProjectConfigurationRules.Key(builder, "ProjectParticipants");
        ProjectConfigurationRules.Child(builder, "ProjectParticipants");
        ProjectConfigurationRules.Version(builder, "ProjectParticipants");
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.HasOne<ProjectGuestInvitation>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId, x.AdmittedViaInvitationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectParticipantsInvitations");
        builder.ToTable("ProjectParticipants", "project", table =>
        {
            table.HasCheckConstraint("CkProjectParticipantsUser", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CkProjectParticipantsKind", "\"Kind\" IN ('Member', 'Guest')");
            table.HasCheckConstraint("CkProjectParticipantsStatus", "\"Status\" IN ('Active', 'Removed', 'Revoked', 'Left')");
            table.HasCheckConstraint("CkProjectParticipantsEnd", "(\"Status\" = 'Active' AND \"EndedAt\" IS NULL AND \"EndedByUserId\" IS NULL) OR (\"Status\" <> 'Active' AND \"EndedAt\" IS NOT NULL AND \"EndedByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.UserId }).IsUnique().HasDatabaseName("UxProjectParticipantsUser");
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.Status }).HasDatabaseName("IxProjectParticipantsOrganizationUserStatus");
        builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("IxProjectParticipantsUserStatus");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.AdmittedViaInvitationId }).HasDatabaseName("IxProjectParticipantsInvitation");
    }
}
