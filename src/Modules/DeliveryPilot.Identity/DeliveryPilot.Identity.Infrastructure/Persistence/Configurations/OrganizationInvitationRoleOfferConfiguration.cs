using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationInvitationRoleOfferConfiguration : IEntityTypeConfiguration<OrganizationInvitationRoleOffer>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitationRoleOffer> builder)
    {
        builder.ToTable("OrganizationInvitationRoleOffers", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationInvitationRoleOffersName", "char_length(btrim(\"OfferedRoleName\")) BETWEEN 1 AND 160 AND \"OfferedRoleName\" = btrim(\"OfferedRoleName\")");
            table.HasCheckConstraint("CkOrganizationInvitationRoleOffersSnapshot", "\"OfferedPermissionSetVersion\" > 0 AND jsonb_typeof(\"OfferedPermissionCodes\") = 'array'");
        });
        builder.HasKey(x => new { x.InvitationId, x.RoleId });
        builder.Property(x => x.OfferedRoleName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.OfferedPermissionCodes).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.OrganizationId, x.RoleId, x.InvitationId });
        builder.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InvitationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationRole>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RoleId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
