namespace DeliveryPilot.Notification.Domain.Models;

public sealed class Notification
{
    private Notification() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RecipientMemberId { get; private set; }
    public string Producer { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public string Purpose { get; private set; } = null!;
    public string ResourceKind { get; private set; } = null!;
    public Guid ResourceId { get; private set; }
    public Guid? ActorMemberId { get; private set; }
    public string Summary { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public long Version { get; private set; } = 1;

    public static Notification Create(Guid tenantId, Guid recipientMemberId, string producer, Guid eventId,
        string purpose, string resourceKind, Guid resourceId, Guid? actorMemberId, string summary,
        DateTimeOffset occurredAt, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(resourceKind);
        ArgumentNullException.ThrowIfNull(summary);
        if (producer.Length > 80 || purpose.Length > 80 || resourceKind.Length > 32 || summary.Length > 500)
        {
            throw new ArgumentException("Notification fields exceed their maximum lengths.");
        }

        return new Notification
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, RecipientMemberId = recipientMemberId,
            Producer = producer, EventId = eventId, Purpose = purpose, ResourceKind = resourceKind,
            ResourceId = resourceId, ActorMemberId = actorMemberId, Summary = summary,
            OccurredAt = occurredAt.ToUniversalTime(), CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public void MarkRead(DateTimeOffset readAt)
    {
        if (ReadAt is not null) return;
        var utc = readAt.ToUniversalTime();
        if (utc < CreatedAt) throw new ArgumentOutOfRangeException(nameof(readAt));
        long nextVersion = checked(Version + 1);
        ReadAt = utc;
        Version = nextVersion;
    }

    public override string ToString() => $"Notification {Id} [REDACTED]";
}
