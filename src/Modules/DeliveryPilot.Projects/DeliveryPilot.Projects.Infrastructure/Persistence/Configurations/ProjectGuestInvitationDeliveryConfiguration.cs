using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectGuestInvitationDeliveryConfiguration : IEntityTypeConfiguration<ProjectGuestInvitationDelivery>
{
    public void Configure(EntityTypeBuilder<ProjectGuestInvitationDelivery> builder)
    {
        builder.HasKey(x => x.DeliveryMessageId).HasName("PkProjectGuestInvitationDeliveries");
        builder.Property(x => x.DeliveryMessageId).ValueGeneratedNever();
        ProjectConfigurationRules.Version(builder, "ProjectGuestInvitationDeliveries");
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.HasOne<Project>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectGuestInvitationDeliveriesProjects");
        builder.HasOne<ProjectGuestInvitation>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId, x.InvitationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectGuestInvitationDeliveriesInvitations");
        builder.ToTable("ProjectGuestInvitationDeliveries", "project", table =>
        {
            table.HasCheckConstraint("CkProjectGuestInvitationDeliveriesId", "uuid_extract_version(\"DeliveryMessageId\") = 7");
            table.HasCheckConstraint("CkProjectGuestInvitationDeliveriesSequence", "\"LastEventSequence\" >= 0");
            table.HasCheckConstraint("CkProjectGuestInvitationDeliveriesExpiry", "\"ExpiresAt\" > \"IssuedAt\"");
            table.HasCheckConstraint("CkProjectGuestInvitationDeliveriesStatus", "\"Status\" IN ('queued', 'retrying', 'provider-accepted', 'delivered', 'bounced', 'complained', 'failed', 'suppressed', 'expired', 'obsolete')");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.IssuedAt }).HasDatabaseName("IxProjectGuestInvitationDeliveriesProjectIssued");
        builder.HasIndex(x => new { x.InvitationId, x.IssuedAt }).HasDatabaseName("IxProjectGuestInvitationDeliveriesInvitationIssued");
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.InvitationId }).HasDatabaseName("IxProjectGuestInvitationDeliveriesInvitation");
    }
}
