using DeliveryPilot.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationRecord = DeliveryPilot.Notification.Domain.Models.Notification;

namespace DeliveryPilot.Notification.Infrastructure.Persistence.Configurations.Inbox;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("Notifications", "notification", table =>
        {
            table.HasCheckConstraint("CkNotificationRead", "\"ReadAt\" IS NULL OR \"ReadAt\" >= \"CreatedAt\"");
            table.HasCheckConstraint("CkNotificationsVersion", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Producer).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ResourceKind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("UqNotificationTenantId");
        builder.HasAlternateKey(x => new { x.Producer, x.EventId, x.TenantId, x.RecipientMemberId, x.Purpose }).HasName("UqNotificationEventRecipient");
        builder.HasIndex(x => new { x.TenantId, x.RecipientMemberId, x.CreatedAt, x.Id })
            .HasDatabaseName("IxNotificationsInbox").IsDescending(false, false, true, false);
        builder.HasIndex(x => new { x.TenantId, x.RecipientMemberId, x.CreatedAt })
            .HasDatabaseName("IxNotificationsUnread").IsDescending(false, false, true).HasFilter("\"ReadAt\" IS NULL");
    }
}
