using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationInvitationDeliveryConfiguration : IEntityTypeConfiguration<OrganizationInvitationDelivery>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitationDelivery> builder)
    {
        builder.ToTable("OrganizationInvitationDeliveries", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationInvitationDeliveriesId", "uuid_extract_version(\"DeliveryMessageId\") = 7");
            table.HasCheckConstraint("CkOrganizationInvitationDeliveriesVersions", "\"Version\" > 0 AND \"LastEventSequence\" >= 0 AND \"ExpiresAt\" > \"IssuedAt\"");
        });
        builder.HasKey(x => x.DeliveryMessageId);
        builder.Property(x => x.DeliveryMessageId).ValueGeneratedNever();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrganizationId, x.IssuedAt });
        builder.HasIndex(x => new { x.InvitationId, x.IssuedAt });
        builder.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InvitationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
