using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

/// <summary>
/// Single EF context for the Projects module database (ADR-001 D-08). Each internal context maps its
/// tables into its own schema (<c>engagement</c>, <c>project</c>, <c>backlog</c>, <c>sprint</c>,
/// <c>approval</c>) through configurations in <c>Persistence.Configurations</c>; outbox/inbox tables
/// live in <c>reliability</c>.
/// </summary>
public sealed class ProjectsDbContext : DbContext
{
    public const string ConfigurationsNamespace = "DeliveryPilot.Projects.Infrastructure.Persistence.Configurations";

    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        OutboxAdministration.Configure(modelBuilder);
        ReliabilityModelBuilderExtensions.ApplyReliabilityConfigurations(modelBuilder, ReliabilityDatabaseProvider.PostgreSql);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ProjectsDbContext).Assembly,
            type => type.Namespace == ConfigurationsNamespace);
    }
}
