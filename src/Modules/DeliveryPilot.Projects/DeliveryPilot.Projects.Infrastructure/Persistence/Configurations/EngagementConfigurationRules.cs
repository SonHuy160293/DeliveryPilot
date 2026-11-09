using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

internal static class EngagementConfigurationRules
{
    internal static void Key<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.ToTable(tableName, "engagement", table =>
            table.HasCheckConstraint($"Ck{tableName}Id", "uuid_extract_version(\"Id\") = 7"));
        builder.HasKey("Id").HasName($"Pk{tableName}");
        builder.Property<Guid>("Id").ValueGeneratedNever();
    }

    internal static void Child<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.HasAlternateKey("OrganizationId", "EngagementId", "Id").HasName($"Ak{tableName}OrganizationEngagementId");
        builder.HasOne<Engagement>().WithMany()
            .HasForeignKey("OrganizationId", "EngagementId")
            .HasPrincipalKey(engagement => new { engagement.OrganizationId, engagement.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"Fk{tableName}Engagements");
    }

    internal static void Version<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.Property<long>("Version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.ToTable(tableName, "engagement", table =>
            table.HasCheckConstraint($"Ck{tableName}Version", "\"Version\" > 0"));
    }
}
