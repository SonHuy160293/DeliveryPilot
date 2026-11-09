using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementLinkCodeConfiguration : IEntityTypeConfiguration<EngagementLinkCode>
{
    public void Configure(EntityTypeBuilder<EngagementLinkCode> builder)
    {
        EngagementConfigurationRules.Key(builder, "EngagementLinkCodes");
        EngagementConfigurationRules.Child(builder, "EngagementLinkCodes");
        EngagementConfigurationRules.Version(builder, "EngagementLinkCodes");
        builder.Property(x => x.CodeVerifier).HasField("_codeVerifier").UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("bytea").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.ToTable("EngagementLinkCodes", "engagement", table =>
        {
            table.HasCheckConstraint("CkEngagementLinkCodesVerifier", "octet_length(\"CodeVerifier\") = 32");

            // Hours make this an exact elapsed interval even when the database session uses a DST zone.
            table.HasCheckConstraint("CkEngagementLinkCodesExpiry", "\"ExpiresAt\" = \"IssuedAt\" + interval '168 hours'");
            table.HasCheckConstraint("CkEngagementLinkCodesStatus", "\"Status\" IN ('Outstanding', 'Consumed', 'Invalidated')");
            table.HasCheckConstraint("CkEngagementLinkCodesConsumption", "(\"Status\" = 'Consumed' AND \"ConsumedAt\" IS NOT NULL AND \"ConsumedByUserId\" IS NOT NULL AND \"ConsumedForOrganizationId\" IS NOT NULL) OR (\"Status\" <> 'Consumed' AND \"ConsumedAt\" IS NULL AND \"ConsumedByUserId\" IS NULL AND \"ConsumedForOrganizationId\" IS NULL)");
            table.HasCheckConstraint("CkEngagementLinkCodesInvalidation", "(\"Status\" = 'Invalidated' AND \"InvalidatedAt\" IS NOT NULL) OR (\"Status\" <> 'Invalidated' AND \"InvalidatedAt\" IS NULL)");
        });
        builder.HasIndex(x => x.CodeVerifier).IsUnique().HasDatabaseName("UxEngagementLinkCodesVerifier");
        builder.HasIndex(x => new { x.OrganizationId, x.EngagementId }).IsUnique()
            .HasFilter("\"Status\" = 'Outstanding'").HasDatabaseName("UxEngagementLinkCodesOutstanding");
    }
}
