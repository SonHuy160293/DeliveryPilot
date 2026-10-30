using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliveryPilot.Notification.Infrastructure.Persistence;
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb") ?? "Host=localhost;Database=DeliveryPilotNotification;Username=deliverypilot_notification").Options);
}
