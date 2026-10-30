using DeliveryPilot.Notification.Domain.Models;
using DeliveryPilot.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using NotificationRecord = DeliveryPilot.Notification.Domain.Models.Notification;

namespace DeliveryPilot.Notification.Tests;

public sealed class NotificationModelTests
{
    [Fact]
    public void Model_ContainsTenantScopedRelationshipsAndConcurrencyTokens()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var notification = model.FindEntityType(typeof(NotificationRecord))!;
        var preference = model.FindEntityType(typeof(NotificationPreference))!;
        var dispatch = model.FindEntityType(typeof(NotificationDispatch))!;
        var email = model.FindEntityType(typeof(EmailDelivery))!;

        Assert.Equal("Notifications", notification.GetTableName());
        Assert.Equal("notification", notification.GetSchema());
        Assert.True(notification.FindProperty(nameof(NotificationRecord.Version))!.IsConcurrencyToken);
        Assert.True(notification.FindProperty(nameof(NotificationRecord.ReadAt))!.IsNullable);
        Assert.Equal(new[] { "TenantId", "RecipientMemberId", "Purpose", "Channel" }, preference.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.True(preference.FindProperty(nameof(NotificationPreference.Version))!.IsConcurrencyToken);
        Assert.True(dispatch.FindProperty(nameof(NotificationDispatch.Version))!.IsConcurrencyToken);
        Assert.Equal(typeof(Guid), email.FindProperty(nameof(EmailDelivery.Version))!.ClrType);
        Assert.True(email.FindProperty(nameof(EmailDelivery.Version))!.IsConcurrencyToken);

        var notificationForeignKey = Assert.Single(dispatch.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(NotificationRecord));
        Assert.Equal(new[] { "TenantId", "NotificationId" }, notificationForeignKey.Properties.Select(x => x.Name));
        Assert.Equal(DeleteBehavior.Restrict, notificationForeignKey.DeleteBehavior);
        var emailForeignKey = Assert.Single(dispatch.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(EmailDelivery));
        Assert.True(emailForeignKey.Properties.Single().IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, emailForeignKey.DeleteBehavior);

        string ddl = context.Database.GenerateCreateScript();
        Assert.Contains("UqNotificationEventRecipient", ddl);
        Assert.Contains("IxNotificationsUnread", ddl);
        Assert.Contains("UxDispatchEmailJob", ddl);
        Assert.Contains("CkDispatchEmailChannel", ddl);
    }

    [Fact]
    public void NotificationRead_IsIdempotentAndRejectsEarlierTime()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var notification = NotificationRecord.Create(Guid.NewGuid(), Guid.NewGuid(), "tasks", Guid.NewGuid(),
            "assignment", "work_item", Guid.NewGuid(), null, "Assigned", now, now);

        Assert.Throws<ArgumentOutOfRangeException>(() => notification.MarkRead(now.AddSeconds(-1)));
        Assert.Null(notification.ReadAt);
        notification.MarkRead(now);
        notification.MarkRead(now.AddSeconds(1));
        Assert.Equal(now, notification.ReadAt);
        Assert.Equal(2, notification.Version);
        Assert.DoesNotContain("Assigned", notification.ToString());
    }

    [Fact]
    public void PreferenceAndDispatch_EnforceChannelRules()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => NotificationPreference.Create(Guid.NewGuid(), Guid.NewGuid(), "mention", "sms", true, now));
        Assert.Throws<ArgumentException>(() => NotificationDispatch.Create(Guid.NewGuid(), Guid.NewGuid(), "in_app", now, Guid.NewGuid()));
        var preference = NotificationPreference.Create(Guid.NewGuid(), Guid.NewGuid(), "mention", "email", true, now);
        preference.SetEnabled(false, now.AddSeconds(1));
        preference.SetEnabled(false, now.AddSeconds(2));
        Assert.Equal(2, preference.Version);
    }

    [Fact]
    public void DeliveryContext_RemainsInSyncWithItsMigrationSnapshot()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options;
        using var context = new NotificationDbContext(options);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Null(context.Model.FindEntityType(typeof(NotificationRecord)));
    }

    private static NotificationInboxDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationInboxDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options;
        return new NotificationInboxDbContext(options);
    }
}
