using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("OrganizationInvitations", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationInvitationsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationInvitationsEmail", "char_length(\"RecipientEmail\") BETWEEN 1 AND 320 AND \"RecipientEmail\" = lower(btrim(\"RecipientEmail\"))");
            table.HasCheckConstraint("CkOrganizationInvitationsVerifier", "octet_length(\"TokenVerifier\") = 32");
            table.HasCheckConstraint("CkOrganizationInvitationsTimes", "\"ExpiresAt\" > \"LastIssuedAt\" AND \"LastIssuedAt\" >= \"CreatedAt\" AND \"Version\" > 0");
            table.HasCheckConstraint("CkOrganizationInvitationsReplacement", "\"ReplacesInvitationId\" IS NULL OR \"ReplacesInvitationId\" <> \"Id\"");
            table.HasCheckConstraint("CkOrganizationInvitationsClosure", "(\"Status\" = 'pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL AND \"CloseReason\" IS NULL) OR (\"Status\" IN ('accepted','declined','cancelled','expired') AND \"ClosedAt\" IS NOT NULL AND \"ClosedAt\" >= \"LastIssuedAt\" AND (\"Status\" NOT IN ('accepted','declined') OR \"ClosedByUserId\" IS NOT NULL) AND (\"Status\" <> 'expired' OR \"ClosedByUserId\" IS NULL))");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CloseReason).HasMaxLength(1000);
        builder.Property(x => x.TokenVerifier).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrganizationId, x.RecipientEmail }).IsUnique().HasFilter("\"Status\" = 'pending'").HasDatabaseName("UxOrganizationInvitationsPendingRecipient");
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.ExpiresAt, x.Id });
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReplacesInvitationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
