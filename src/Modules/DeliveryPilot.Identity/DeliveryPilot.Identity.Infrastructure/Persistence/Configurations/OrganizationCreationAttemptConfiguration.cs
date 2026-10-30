using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationCreationAttemptConfiguration : IEntityTypeConfiguration<OrganizationCreationAttempt>
{
    public void Configure(EntityTypeBuilder<OrganizationCreationAttempt> builder)
    {
        builder.ToTable("OrganizationCreationAttempts", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationCreationAttemptsIds", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AttemptId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OrganizationId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CkOrganizationCreationAttemptsFingerprint", "octet_length(\"RequestFingerprint\") = 32");
            table.HasCheckConstraint("CkOrganizationCreationAttemptsDeadline", "\"ReplayUntil\" = \"CreatedAt\" + interval '24 hours'");
            table.HasCheckConstraint("CkOrganizationCreationAttemptsSnapshot", "(\"ResponseSchemaVersion\" = 1 AND jsonb_typeof(\"ResultSnapshot\") = 'object' AND octet_length(\"ResultSnapshot\"::text) <= 8192 AND \"ResultSnapshot\" ?& ARRAY['organizationId','name','status','membershipId','roles','version','membershipVersion','resultKind','snapshotAt'] AND \"ResultSnapshot\" - ARRAY['organizationId','name','status','membershipId','roles','version','membershipVersion','resultKind','snapshotAt'] = '{}'::jsonb AND \"ResultSnapshot\"->>'organizationId' = \"OrganizationId\"::text AND jsonb_typeof(\"ResultSnapshot\"->'membershipId') = 'string' AND jsonb_typeof(\"ResultSnapshot\"->'name') = 'string' AND jsonb_typeof(\"ResultSnapshot\"->'snapshotAt') = 'string' AND \"ResultSnapshot\"->>'status' = 'active' AND \"ResultSnapshot\"->>'resultKind' = 'creationSnapshot' AND \"ResultSnapshot\"->'roles' = '[\"Owner\"]'::jsonb AND \"ResultSnapshot\"->'version' = '1'::jsonb AND \"ResultSnapshot\"->'membershipVersion' = '1'::jsonb) IS TRUE");
        });
        builder.HasKey(attempt => new { attempt.UserId, attempt.AttemptId }).HasName("PkOrganizationCreationAttempts");
        builder.Property(attempt => attempt.RequestFingerprint).HasColumnType("bytea").IsRequired();
        builder.Property(attempt => attempt.ResultSnapshot).HasColumnType("jsonb").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(attempt => attempt.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(attempt => attempt.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
