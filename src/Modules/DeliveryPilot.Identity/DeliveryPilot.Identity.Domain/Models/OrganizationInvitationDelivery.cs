namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationInvitationDelivery
{
    private OrganizationInvitationDelivery() { }

    public Guid DeliveryMessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid InvitationId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Status { get; private set; } = null!;
    public long LastEventSequence { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static OrganizationInvitationDelivery Create(Guid organizationId, Guid invitationId, Guid deliveryMessageId, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        DateTimeOffset issued = issuedAt.ToUniversalTime();
        DateTimeOffset expires = expiresAt.ToUniversalTime();
        if (organizationId == Guid.Empty || invitationId == Guid.Empty || deliveryMessageId == Guid.Empty || expires <= issued)
        {
            throw new ArgumentException("A scoped delivery and a valid deadline are required.");
        }

        return new OrganizationInvitationDelivery { OrganizationId = organizationId, InvitationId = invitationId, DeliveryMessageId = deliveryMessageId, IssuedAt = issued, ExpiresAt = expires, Status = "queued", UpdatedAt = issued, Version = 1 };
    }

    public bool ApplyStatus(long sequence, string status, string? failureCode, DateTimeOffset occurredAt)
    {
        if (sequence <= LastEventSequence)
        {
            return false;
        }

        if (sequence <= 0 || status is not ("queued" or "retrying" or "provider-accepted" or "delivered" or "bounced" or "complained" or "failed" or "suppressed" or "expired" or "obsolete") || failureCode?.Length > 64)
        {
            throw new ArgumentException("Invalid delivery status.");
        }

        LastEventSequence = sequence;
        Status = status;
        FailureCode = failureCode;
        UpdatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
        return true;
    }
}
