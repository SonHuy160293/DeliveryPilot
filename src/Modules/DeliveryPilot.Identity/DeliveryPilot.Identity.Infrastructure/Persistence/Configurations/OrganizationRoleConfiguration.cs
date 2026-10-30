using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationRoleConfiguration : IEntityTypeConfiguration<OrganizationRole>
{
    public void Configure(EntityTypeBuilder<OrganizationRole> builder)
    {
        builder.ToTable("OrganizationRoles", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationRolesId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationRolesName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkOrganizationRolesCode", "char_length(btrim(\"Code\")) BETWEEN 1 AND 80 AND \"Code\" = btrim(\"Code\")");
            table.HasCheckConstraint("CkOrganizationRolesDescription", "\"Description\" IS NULL OR char_length(\"Description\") <= 1000");
            table.HasCheckConstraint("CkOrganizationRolesOrigin", "(\"Origin\" = 'system' AND \"TemplateVersion\" IS NOT NULL AND \"TemplateVersion\" > 0) OR (\"Origin\" = 'custom' AND \"TemplateVersion\" IS NULL AND \"CreatedByUserId\" IS NOT NULL)");
            table.HasCheckConstraint("CkOrganizationRolesVersions", "\"Version\" > 0 AND \"PermissionSetVersion\" > 0");
        });
        builder.HasKey(x => x.Id).HasName("PkOrganizationRoles");
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Origin).HasMaxLength(16).IsRequired();
        builder.Property(x => x.PermissionSetVersion).HasDefaultValue(1L);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Case-insensitive scoped indexes are created in the migration using SQL expressions.
    }
}
