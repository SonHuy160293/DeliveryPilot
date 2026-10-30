using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkOrganizationsStatus", "\"Status\" IN ('active', 'suspended', 'archived')");
            table.HasCheckConstraint("CkOrganizationsVersion", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id).HasName("PkOrganizations");
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).HasDefaultValue("active").IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Status, x.Id }).HasDatabaseName("IxOrganizationsStatusId");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.StatusChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
