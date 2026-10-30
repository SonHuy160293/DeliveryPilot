using DeliveryPilot.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Notification.Infrastructure.Persistence.Configurations.Inbox;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "notification", table =>
        {
            table.HasCheckConstraint("CkNotificationPreferencesChannel", "\"Channel\" IN ('email','in_app')");
            table.HasCheckConstraint("CkNotificationPreferencesVersion", "\"Version\" > 0");
        });
        builder.HasKey(x => new { x.TenantId, x.RecipientMemberId, x.Purpose, x.Channel });
        builder.Property(x => x.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
    }
}
