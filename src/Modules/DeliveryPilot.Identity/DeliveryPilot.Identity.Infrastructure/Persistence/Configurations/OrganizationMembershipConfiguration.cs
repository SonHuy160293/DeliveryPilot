using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationMembershipsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationMembershipsStatus", "\"Status\" IN ('active', 'suspended', 'removed')");
            table.HasCheckConstraint("CkOrganizationMembershipsVersion", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id).HasName("PkOrganizationMemberships");
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Status).HasMaxLength(16).HasDefaultValue("active").IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique().HasDatabaseName("UxOrganizationMembershipsOrganizationUser");
        builder.HasIndex(x => new { x.UserId, x.Status, x.Id });
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.Id });
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.StatusChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
