using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementConfiguration : IEntityTypeConfiguration<Engagement>
{
    public void Configure(EntityTypeBuilder<Engagement> builder)
    {
        EngagementConfigurationRules.Key(builder, "Engagements");
        EngagementConfigurationRules.Version(builder, "Engagements");
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id }).HasName("AkEngagementsOrganizationIdId");
        builder.Property(x => x.ClientDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientLegalName).HasMaxLength(200);
        builder.Property(x => x.ClientCountryCode).HasMaxLength(2);
        builder.Property(x => x.EngagementName).HasMaxLength(200);
        builder.Property(x => x.AgreementReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AgreementSignedOn).HasColumnType("date");
        builder.Property(x => x.DocumentUrl).HasMaxLength(2000);
        builder.Property(x => x.ContractAmount).HasColumnType("numeric");
        builder.Property(x => x.ContractCurrency).HasMaxLength(3);
        builder.Property(x => x.PlannedEffortPersonDays).HasColumnType("numeric");
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.ToTable("Engagements", "engagement", table =>
        {
            table.HasCheckConstraint("CkEngagementsIds", "\"OrganizationId\" <> '00000000-0000-0000-0000-000000000000' AND \"CreatedByUserId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CkEngagementsClientDisplayName", "char_length(btrim(\"ClientDisplayName\")) BETWEEN 1 AND 200 AND \"ClientDisplayName\" = btrim(\"ClientDisplayName\")");
            table.HasCheckConstraint("CkEngagementsAgreementReference", "char_length(btrim(\"AgreementReference\")) BETWEEN 1 AND 100 AND \"AgreementReference\" = btrim(\"AgreementReference\")");
            table.HasCheckConstraint("CkEngagementsCountry", "\"ClientCountryCode\" IS NULL OR \"ClientCountryCode\" ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint("CkEngagementsCurrency", "\"ContractCurrency\" IS NULL OR \"ContractCurrency\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("CkEngagementsAmount", "\"ContractAmount\" IS NULL OR (\"ContractAmount\" >= 0 AND \"ContractAmount\" < 'Infinity'::numeric AND \"ContractAmount\" = trunc(\"ContractAmount\", 2))");
            table.HasCheckConstraint("CkEngagementsAmountCurrency", "\"ContractAmount\" IS NULL OR \"ContractCurrency\" IS NOT NULL");
            table.HasCheckConstraint("CkEngagementsEffort", "\"PlannedEffortPersonDays\" IS NULL OR (\"PlannedEffortPersonDays\" >= 0 AND \"PlannedEffortPersonDays\" < 'Infinity'::numeric AND \"PlannedEffortPersonDays\" = trunc(\"PlannedEffortPersonDays\", 1))");
            table.HasCheckConstraint("CkEngagementsStatus", "\"Status\" IN ('Active', 'Closed')");
            table.HasCheckConstraint("CkEngagementsClosure", "(\"Status\" = 'Active' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" = 'Closed' AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NOT NULL)");
            table.HasCheckConstraint("CkEngagementsClientLink", "(\"ClientOrganizationId\" IS NULL AND \"ClientLinkedAt\" IS NULL AND \"ClientLinkedByUserId\" IS NULL) OR (\"ClientOrganizationId\" IS NOT NULL AND \"ClientLinkedAt\" IS NOT NULL AND \"ClientLinkedByUserId\" IS NOT NULL)");
            table.HasCheckConstraint("CkEngagementsClientOrganization", "\"ClientOrganizationId\" IS NULL OR \"ClientOrganizationId\" <> \"OrganizationId\"");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAt, x.Id }).HasDatabaseName("IxEngagementsOrganizationStatusCreatedId");
        builder.HasIndex(x => x.ClientOrganizationId).HasFilter("\"ClientOrganizationId\" IS NOT NULL").HasDatabaseName("IxEngagementsClientOrganization");

        // IxEngagementsActiveAgreementReferenceCi is an expression index created by the migration,
        // matching Identity's SQL-only case-insensitive indexes. Do not replace it with a unique index.
    }
}
