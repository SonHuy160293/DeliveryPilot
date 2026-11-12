using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
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

    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<EngagementMilestone> EngagementMilestones => Set<EngagementMilestone>();
    public DbSet<EngagementContact> EngagementContacts => Set<EngagementContact>();
    public DbSet<EngagementLinkCode> EngagementLinkCodes => Set<EngagementLinkCode>();
    public DbSet<EngagementLinkAttempt> EngagementLinkAttempts => Set<EngagementLinkAttempt>();
    public DbSet<EngagementEvent> EngagementEvents => Set<EngagementEvent>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectBrief> ProjectBriefs => Set<ProjectBrief>();
    public DbSet<ProjectParticipant> ProjectParticipants => Set<ProjectParticipant>();
    public DbSet<ProjectParticipantRole> ProjectParticipantRoles => Set<ProjectParticipantRole>();
    public DbSet<ProjectGuestInvitation> ProjectGuestInvitations => Set<ProjectGuestInvitation>();
    public DbSet<ProjectGuestInvitationDelivery> ProjectGuestInvitationDeliveries => Set<ProjectGuestInvitationDelivery>();
    public DbSet<ProjectStakeholder> ProjectStakeholders => Set<ProjectStakeholder>();
    public DbSet<ProjectEvent> ProjectEvents => Set<ProjectEvent>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureHistoryIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureHistoryIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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

    private void EnsureHistoryIsAppendOnly()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<EngagementEvent>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Engagement history is append-only.");
        }

        if (ChangeTracker.Entries<ProjectEvent>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Project history is append-only.");
        }
    }
}
