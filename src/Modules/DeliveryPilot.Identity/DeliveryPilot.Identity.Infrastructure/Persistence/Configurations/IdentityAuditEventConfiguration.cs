using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityAuditEventConfiguration : IEntityTypeConfiguration<IdentityAuditEvent>
{
    public void Configure(EntityTypeBuilder<IdentityAuditEvent> builder)
    {
        builder.ToTable("IdentityAuditEvents", "shared", table =>
        {
            table.HasCheckConstraint("CkIdentityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkIdentityAuditEventsAggregateType", "char_length(btrim(\"AggregateType\")) BETWEEN 1 AND 80 AND \"AggregateType\" = btrim(\"AggregateType\")");
            table.HasCheckConstraint("CkIdentityAuditEventsEventType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
            table.HasCheckConstraint("CkIdentityAuditEventsOutcome", "\"Outcome\" IN ('succeeded', 'denied', 'failed')");
            table.HasCheckConstraint("CkIdentityAuditEventsActorKind", "\"ActorKind\" IN ('user', 'system', 'operator')");
            table.HasCheckConstraint("CkIdentityAuditEventsActor", "(\"ActorKind\" = 'user' AND \"ActorUserId\" IS NOT NULL AND \"ActorReference\" IS NULL) OR (\"ActorKind\" IN ('system', 'operator') AND \"ActorUserId\" IS NULL AND \"ActorReference\" IS NOT NULL AND char_length(btrim(\"ActorReference\")) BETWEEN 1 AND 200 AND \"ActorReference\" = btrim(\"ActorReference\"))");
            table.HasCheckConstraint("CkIdentityAuditEventsMembership", "(\"MembershipId\" IS NULL AND \"MembershipVersion\" IS NULL) OR (\"OrganizationId\" IS NOT NULL AND \"MembershipId\" IS NOT NULL AND \"MembershipVersion\" IS NOT NULL AND \"MembershipVersion\" > 0)");
            table.HasCheckConstraint("CkIdentityAuditEventsVersions", "\"AggregateVersion\" > 0 AND \"EventOrdinal\" > 0 AND \"DetailsSchemaVersion\" > 0");
            table.HasCheckConstraint("CkIdentityAuditEventsCorrelationId", "char_length(btrim(\"CorrelationId\")) BETWEEN 1 AND 100 AND \"CorrelationId\" = btrim(\"CorrelationId\")");
            table.HasCheckConstraint("CkIdentityAuditEventsReason", "\"Reason\" IS NULL OR (char_length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\"))");
            table.HasCheckConstraint("CkIdentityAuditEventsDetailsObject", "jsonb_typeof(\"Details\") = 'object'");
        });

        builder.HasKey(x => x.Id).HasName("PkIdentityAuditEvents");
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.AggregateType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ActorKind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ActorReference).HasMaxLength(200);
        builder.Property(x => x.RecordedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DetailsSchemaVersion).HasDefaultValue((short)1);
        builder.Property(x => x.Details).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();

        builder.HasIndex(x => new { x.AggregateType, x.AggregateId, x.AggregateVersion, x.EventOrdinal })
            .IsUnique()
            .HasDatabaseName("UxIdentityAuditEventsAggregateRevisionOrdinal");
        builder.HasIndex(x => new { x.MembershipId, x.MembershipVersion, x.EventOrdinal })
            .IsUnique()
            .HasFilter("\"MembershipId\" IS NOT NULL")
            .HasDatabaseName("UxIdentityAuditEventsMembershipRevisionOrdinal");
        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAt, x.Id })
            .HasDatabaseName("IxIdentityAuditEventsOrganizationOccurredAtId");
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt, x.Id })
            .HasDatabaseName("IxIdentityAuditEventsActorOccurredAtId");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.MembershipId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
