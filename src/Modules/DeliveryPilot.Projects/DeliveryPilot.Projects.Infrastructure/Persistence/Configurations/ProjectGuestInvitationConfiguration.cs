using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectGuestInvitationConfiguration : IEntityTypeConfiguration<ProjectGuestInvitation>
{
    public void Configure(EntityTypeBuilder<ProjectGuestInvitation> builder)
    {
        ProjectConfigurationRules.Key(builder, "ProjectGuestInvitations");
        ProjectConfigurationRules.Child(builder, "ProjectGuestInvitations");
        ProjectConfigurationRules.Version(builder, "ProjectGuestInvitations");
        builder.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.OfferedRoleCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.TokenVerifier).HasField("_tokenVerifier").UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("bytea").IsRequired();
        builder.HasOne<ProjectGuestInvitation>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId, x.ReplacesInvitationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectGuestInvitationsReplacedInvitation");
        builder.ToTable("ProjectGuestInvitations", "project", table =>
        {
            table.HasCheckConstraint("CkProjectGuestInvitationsEmail", "char_length(\"RecipientEmail\") BETWEEN 3 AND 320 AND \"RecipientEmail\" = lower(btrim(\"RecipientEmail\"))");
            table.HasCheckConstraint("CkProjectGuestInvitationsRole", "\"OfferedRoleCode\" IN ('ProjectManager', 'BusinessAnalyst', 'Developer', 'QualityAssurance', 'DevOps', 'Viewer')");
            table.HasCheckConstraint("CkProjectGuestInvitationsVerifier", "octet_length(\"TokenVerifier\") = 32");

            // An exact elapsed week, independent of the database session's DST rules.
            table.HasCheckConstraint("CkProjectGuestInvitationsExpiry", "\"ExpiresAt\" = \"LastIssuedAt\" + interval '168 hours'");
            table.HasCheckConstraint("CkProjectGuestInvitationsStatus", "\"Status\" IN ('Pending', 'Accepted', 'Declined', 'Cancelled', 'Expired')");
            table.HasCheckConstraint("CkProjectGuestInvitationsClosure", "(\"Status\" = 'Pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" = 'Expired' AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" IN ('Accepted', 'Declined', 'Cancelled') AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.RecipientEmail }).IsUnique()
            .HasFilter("\"Status\" = 'Pending'").HasDatabaseName("UxProjectGuestInvitationsPending");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.ReplacesInvitationId }).HasDatabaseName("IxProjectGuestInvitationsReplacement");
    }
}
