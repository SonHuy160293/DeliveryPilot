using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementEventConfiguration : IEntityTypeConfiguration<EngagementEvent>
{
    public void Configure(EntityTypeBuilder<EngagementEvent> builder)
    {
        EngagementConfigurationRules.Key(builder, "EngagementEvents");
        EngagementConfigurationRules.Child(builder, "EngagementEvents");
        builder.Property(x => x.EventType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.SubjectType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        builder.ToTable("EngagementEvents", "engagement", table =>
        {
            table.HasCheckConstraint("CkEngagementEventsType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
            table.HasCheckConstraint("CkEngagementEventsSubject", "\"SubjectType\" IN ('Engagement', 'Milestone', 'Contact', 'LinkCode')");
            table.HasCheckConstraint("CkEngagementEventsVersion", "\"EngagementVersion\" > 0");
            table.HasCheckConstraint("CkEngagementEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.EngagementId, x.OccurredAt, x.Id }).HasDatabaseName("IxEngagementEventsOrganizationEngagementOccurredId");
    }
}
