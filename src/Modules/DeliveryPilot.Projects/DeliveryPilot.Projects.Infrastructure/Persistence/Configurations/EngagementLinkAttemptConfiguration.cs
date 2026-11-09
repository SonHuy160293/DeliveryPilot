using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementLinkAttemptConfiguration : IEntityTypeConfiguration<EngagementLinkAttempt>
{
    public void Configure(EntityTypeBuilder<EngagementLinkAttempt> builder)
    {
        EngagementConfigurationRules.Key(builder, "EngagementLinkAttempts");
        builder.HasIndex(x => new { x.UserId, x.AttemptedAt }).HasDatabaseName("IxEngagementLinkAttemptsUserAttemptedAt");
        builder.ToTable("EngagementLinkAttempts", "engagement", table =>
            table.HasCheckConstraint("CkEngagementLinkAttemptsIds", "\"UserId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActingOrganizationId\" <> '00000000-0000-0000-0000-000000000000'"));
    }
}
