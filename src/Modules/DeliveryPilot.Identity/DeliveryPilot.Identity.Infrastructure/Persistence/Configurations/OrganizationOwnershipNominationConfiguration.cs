using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationOwnershipNominationConfiguration : IEntityTypeConfiguration<OrganizationOwnershipNomination>
{
    public void Configure(EntityTypeBuilder<OrganizationOwnershipNomination> builder)
    {
        builder.ToTable("OrganizationOwnershipNominations", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationOwnershipNominationsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationOwnershipNominationsTimes", "\"ExpiresAt\" = \"CreatedAt\" + interval '168 hours' AND \"UpdatedAt\" >= \"CreatedAt\" AND \"Version\" > 0");
            table.HasCheckConstraint("CkOrganizationOwnershipNominationsReason", "\"Reason\" IS NULL OR (char_length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Status\" = 'cancelled')");
            table.HasCheckConstraint("CkOrganizationOwnershipNominationsClosure", "(\"Status\" = 'pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL AND \"Reason\" IS NULL AND \"Version\" = 1 AND \"UpdatedAt\" = \"CreatedAt\") OR (\"Status\" IN ('accepted','declined','cancelled','expired') AND \"ClosedAt\" IS NOT NULL AND \"ClosedAt\" >= \"CreatedAt\" AND \"UpdatedAt\" = \"ClosedAt\" AND \"Version\" = 2 AND ((\"Status\" = 'expired' AND \"ClosedByUserId\" IS NULL AND \"ClosedAt\" >= \"ExpiresAt\") OR (\"Status\" <> 'expired' AND \"ClosedByUserId\" IS NOT NULL AND \"ClosedAt\" < \"ExpiresAt\")))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasMaxLength(16).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => new { item.OrganizationId, item.MembershipId }).IsUnique().HasFilter("\"Status\" = 'pending'").HasDatabaseName("UxOwnershipNominationsPendingMember");
        builder.HasIndex(item => new { item.OrganizationId, item.Status, item.CreatedAt, item.Id });
        builder.HasIndex(item => new { item.MembershipId, item.Status, item.CreatedAt, item.Id });
        builder.HasIndex(item => new { item.Status, item.ExpiresAt });
        builder.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMembership>().WithMany().HasForeignKey(item => new { item.OrganizationId, item.MembershipId }).HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.NominatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
