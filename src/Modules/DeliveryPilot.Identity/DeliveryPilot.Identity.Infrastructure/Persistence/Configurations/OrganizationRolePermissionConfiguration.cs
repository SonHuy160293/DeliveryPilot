using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationRolePermissionConfiguration : IEntityTypeConfiguration<OrganizationRolePermission>
{
    public void Configure(EntityTypeBuilder<OrganizationRolePermission> builder)
    {
        builder.ToTable("OrganizationRolePermissions", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationRolePermissionsCode", "char_length(btrim(\"PermissionCode\")) BETWEEN 1 AND 160 AND \"PermissionCode\" = btrim(\"PermissionCode\")");
            table.HasCheckConstraint("CkOrganizationRolePermissionsCatalogPolicyVersion", "\"CatalogPolicyVersion\" > 0");
        });
        builder.HasKey(x => new { x.RoleId, x.PermissionCode }).HasName("PkOrganizationRolePermissions");
        builder.Property(x => x.PermissionCode).HasMaxLength(160).IsRequired();
        builder.Property(x => x.CatalogPolicyVersion).HasDefaultValue(1);
        builder.HasOne<OrganizationRole>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RoleId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
