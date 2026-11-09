using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementMilestoneConfiguration : IEntityTypeConfiguration<EngagementMilestone>
{
    public void Configure(EntityTypeBuilder<EngagementMilestone> builder)
    {
        EngagementConfigurationRules.Key(builder, "EngagementMilestones");
        EngagementConfigurationRules.Child(builder, "EngagementMilestones");
        EngagementConfigurationRules.Version(builder, "EngagementMilestones");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.OriginalTargetDate).HasColumnType("date").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(x => x.CurrentTargetDate).HasColumnType("date");
        builder.ToTable("EngagementMilestones", "engagement", table =>
        {
            table.HasCheckConstraint("CkEngagementMilestonesName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkEngagementMilestonesStatus", "\"Status\" IN ('Planned', 'Achieved', 'Missed', 'Retired')");
        });
    }
}
