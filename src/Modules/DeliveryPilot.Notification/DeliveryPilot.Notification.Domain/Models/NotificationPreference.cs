namespace DeliveryPilot.Notification.Domain.Models;

public sealed class NotificationPreference
{
    private NotificationPreference() { }

    public Guid TenantId { get; private set; }
    public Guid RecipientMemberId { get; private set; }
    public string Purpose { get; private set; } = null!;
    public string Channel { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; } = 1;

    public static NotificationPreference Create(Guid tenantId, Guid recipientMemberId, string purpose,
        string channel, bool enabled, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        if (purpose.Length > 80) throw new ArgumentException("Purpose exceeds its maximum length.", nameof(purpose));
        if (channel is not ("email" or "in_app")) throw new ArgumentException("Invalid channel.", nameof(channel));
        return new NotificationPreference
        {
            TenantId = tenantId, RecipientMemberId = recipientMemberId, Purpose = purpose,
            Channel = channel, Enabled = enabled, UpdatedAt = updatedAt.ToUniversalTime()
        };
    }

    public void SetEnabled(bool enabled, DateTimeOffset updatedAt)
    {
        if (Enabled == enabled) return;
        long nextVersion = checked(Version + 1);
        Enabled = enabled;
        UpdatedAt = updatedAt.ToUniversalTime();
        Version = nextVersion;
    }
}
