using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationRoleAssignmentConfiguration : IEntityTypeConfiguration<OrganizationRoleAssignment>
{
    public void Configure(EntityTypeBuilder<OrganizationRoleAssignment> builder)
    {
        builder.ToTable("OrganizationRoleAssignments", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationRoleAssignmentsVersions", "\"Version\" > 0 AND \"GrantVersion\" > 0");
            table.HasCheckConstraint("CkOrganizationRoleAssignmentsExpiry", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"GrantedAt\"");
            table.HasCheckConstraint("CkOrganizationRoleAssignmentsRevocation", "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"GrantedAt\"");
        });
        builder.HasKey(x => x.Id).HasName("PkOrganizationRoleAssignments");
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.GrantVersion).HasDefaultValue(1L);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrganizationId, x.MembershipId, x.RoleId }).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.RoleId, x.MembershipId });
        builder.HasOne<OrganizationMembership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MembershipId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationRole>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RoleId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
