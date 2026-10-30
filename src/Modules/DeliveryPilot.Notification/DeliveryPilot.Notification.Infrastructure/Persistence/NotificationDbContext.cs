using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Notification.Infrastructure.Persistence;
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
    public DbSet<EmailDeliveryAttempt> EmailDeliveryAttempts => Set<EmailDeliveryAttempt>();
    public DbSet<EmailSuppression> EmailSuppressions => Set<EmailSuppression>();
    public DbSet<EmailWebhookReceipt> EmailWebhookReceipts => Set<EmailWebhookReceipt>();
    public DbSet<EmailDeliveryControl> EmailDeliveryControls => Set<EmailDeliveryControl>();
    public DbSet<EmailOperation> EmailOperations => Set<EmailOperation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyReliabilityConfigurations(ReliabilityDatabaseProvider.PostgreSql);
        DeliveryPilot.Messaging.Outbox.OutboxAdministration.Configure(model);
        model.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly,
            type => type.Namespace == "DeliveryPilot.Notification.Infrastructure.Persistence.Configurations");
    }
}
