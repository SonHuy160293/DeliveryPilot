namespace DeliveryPilot.Notification.Domain.Models;

public sealed class NotificationDispatch
{
    private NotificationDispatch() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public Guid? EmailDeliveryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; } = 1;

    public static NotificationDispatch Create(Guid tenantId, Guid notificationId, string channel,
        DateTimeOffset createdAt, Guid? emailDeliveryId = null)
    {
        if (channel is not ("email" or "in_app")) throw new ArgumentException("Invalid channel.", nameof(channel));
        if (emailDeliveryId is not null && channel != "email") throw new ArgumentException("Only email dispatches can reference an email delivery.", nameof(emailDeliveryId));
        var utc = createdAt.ToUniversalTime();
        return new NotificationDispatch
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, NotificationId = notificationId,
            Channel = channel, Status = "pending", EmailDeliveryId = emailDeliveryId,
            CreatedAt = utc, UpdatedAt = utc
        };
    }
}
