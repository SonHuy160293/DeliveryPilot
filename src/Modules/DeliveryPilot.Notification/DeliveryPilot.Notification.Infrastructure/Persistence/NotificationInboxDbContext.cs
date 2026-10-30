using DeliveryPilot.Notification.Domain.Models;
using DeliveryPilot.Notification.Infrastructure.Persistence.Configurations;
using DeliveryPilot.Notification.Infrastructure.Persistence.Configurations.Inbox;
using Microsoft.EntityFrameworkCore;
using NotificationRecord = DeliveryPilot.Notification.Domain.Models.Notification;

namespace DeliveryPilot.Notification.Infrastructure.Persistence;

/// <summary>
/// Isolates the proposed, unmigrated inbox model from the delivery migration model.
/// </summary>
public sealed class NotificationInboxDbContext(DbContextOptions<NotificationInboxDbContext> options) : DbContext(options)
{
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDispatch> NotificationDispatches => Set<NotificationDispatch>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(NotificationInboxDbContext).Assembly,
            type => type.Namespace == typeof(NotificationConfiguration).Namespace);
        model.ApplyConfiguration(new EmailDeliveryConfiguration());
    }
}
