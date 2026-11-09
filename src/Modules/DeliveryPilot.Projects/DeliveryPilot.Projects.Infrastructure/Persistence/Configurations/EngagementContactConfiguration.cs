using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class EngagementContactConfiguration : IEntityTypeConfiguration<EngagementContact>
{
    public void Configure(EntityTypeBuilder<EngagementContact> builder)
    {
        EngagementConfigurationRules.Key(builder, "EngagementContacts");
        EngagementConfigurationRules.Child(builder, "EngagementContacts");
        EngagementConfigurationRules.Version(builder, "EngagementContacts");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.ContactRole).HasMaxLength(32).IsRequired();
        builder.ToTable("EngagementContacts", "engagement", table =>
        {
            table.HasCheckConstraint("CkEngagementContactsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkEngagementContactsEmail", "char_length(\"Email\") BETWEEN 3 AND 320 AND \"Email\" = lower(btrim(\"Email\"))");
            table.HasCheckConstraint("CkEngagementContactsRole", "\"ContactRole\" IN ('ClientProductOwner', 'Sponsor', 'Other')");
            table.HasCheckConstraint("CkEngagementContactsRemoval", "(\"RemovedAt\" IS NULL AND \"RemovedByUserId\" IS NULL) OR (\"RemovedAt\" IS NOT NULL AND \"RemovedByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.EngagementId }).IsUnique()
            .HasFilter("\"ContactRole\" = 'ClientProductOwner' AND \"RemovedAt\" IS NULL")
            .HasDatabaseName("UxEngagementContactsClientProductOwner");
    }
}
