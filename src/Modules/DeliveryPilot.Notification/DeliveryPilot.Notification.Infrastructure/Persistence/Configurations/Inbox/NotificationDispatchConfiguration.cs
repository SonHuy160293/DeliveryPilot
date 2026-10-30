using DeliveryPilot.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationRecord = DeliveryPilot.Notification.Domain.Models.Notification;

namespace DeliveryPilot.Notification.Infrastructure.Persistence.Configurations.Inbox;

public sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
    {
        builder.ToTable("NotificationDispatches", "notification", table =>
        {
            table.HasCheckConstraint("CkNotificationDispatchesChannel", "\"Channel\" IN ('email','in_app')");
            table.HasCheckConstraint("CkNotificationDispatchesStatus", "\"Status\" IN ('pending','accepted','failed','suppressed')");
            table.HasCheckConstraint("CkDispatchEmailChannel", "\"EmailDeliveryId\" IS NULL OR \"Channel\" = 'email'");
            table.HasCheckConstraint("CkNotificationDispatchesVersion", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasAlternateKey(x => new { x.TenantId, x.NotificationId, x.Channel }).HasName("UqNotificationDispatchChannel");
        builder.HasOne<NotificationRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).HasConstraintName("FkDispatchNotification").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmailDelivery>().WithMany().HasForeignKey(x => x.EmailDeliveryId)
            .HasConstraintName("FkDispatchEmail").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Status, x.CreatedAt, x.Id }).HasDatabaseName("IxDispatchPending").HasFilter("\"Status\" = 'pending'");
        builder.HasIndex(x => x.EmailDeliveryId).IsUnique().HasDatabaseName("UxDispatchEmailJob").HasFilter("\"EmailDeliveryId\" IS NOT NULL");
    }
}
