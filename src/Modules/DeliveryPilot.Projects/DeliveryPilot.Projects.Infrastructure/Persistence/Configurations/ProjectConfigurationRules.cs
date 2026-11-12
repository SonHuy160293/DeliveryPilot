using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

internal static class ProjectConfigurationRules
{
    internal static void Key<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.ToTable(tableName, "project", table =>
            table.HasCheckConstraint($"Ck{tableName}Id", "uuid_extract_version(\"Id\") = 7"));
        builder.HasKey("Id").HasName($"Pk{tableName}");
        builder.Property<Guid>("Id").ValueGeneratedNever();
    }

    internal static void Child<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.HasAlternateKey("OrganizationId", "ProjectId", "Id").HasName($"Ak{tableName}OrganizationProjectId");
        builder.HasOne<Project>().WithMany()
            .HasForeignKey("OrganizationId", "ProjectId")
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"Fk{tableName}Projects");
    }

    internal static void Version<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.Property<long>("Version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.ToTable(tableName, "project", table =>
            table.HasCheckConstraint($"Ck{tableName}Version", "\"Version\" > 0"));
    }
}
