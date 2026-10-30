using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationOwnershipNomination> OrganizationOwnershipNominations => Set<OrganizationOwnershipNomination>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();
    public DbSet<OrganizationRoleAssignment> OrganizationRoleAssignments => Set<OrganizationRoleAssignment>();
    public DbSet<IdentityAuditEvent> IdentityAuditEvents => Set<IdentityAuditEvent>();
    public DbSet<OrganizationCreationAttempt> OrganizationCreationAttempts => Set<OrganizationCreationAttempt>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<OrganizationInvitationRoleOffer> OrganizationInvitationRoleOffers => Set<OrganizationInvitationRoleOffer>();
    public DbSet<OrganizationInvitationDelivery> OrganizationInvitationDeliveries => Set<OrganizationInvitationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        DeliveryPilot.Messaging.Outbox.OutboxAdministration.Configure(modelBuilder);
        DeliveryPilot.EntityFrameworkCore.Reliability.ReliabilityModelBuilderExtensions.ApplyReliabilityConfigurations(
            modelBuilder, DeliveryPilot.EntityFrameworkCore.Reliability.ReliabilityDatabaseProvider.PostgreSql);
        modelBuilder.Entity<DeliveryPilot.Identity.Infrastructure.Messaging.EmailDeliveryReceipt>(b =>
        {
            b.ToTable("EmailDeliveryReceipts", "reliability");
            b.HasKey(x => x.DeliveryMessageId);
            b.Property(x => x.DeliveryMessageId).ValueGeneratedNever();
        });
        modelBuilder.Entity<DeliveryPilot.Identity.Infrastructure.Messaging.OutboxRecovery>(b =>
        {
            b.ToTable("OutboxRecoveries", "reliability");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly,
            type => type.Namespace == "DeliveryPilot.Identity.Infrastructure.Persistence.Configurations");
    }
}
