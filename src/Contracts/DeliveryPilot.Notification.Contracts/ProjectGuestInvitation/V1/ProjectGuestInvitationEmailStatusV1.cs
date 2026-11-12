namespace DeliveryPilot.Notification.Contracts;

public sealed record ProjectGuestInvitationEmailStatusV1(Guid MessageId, Guid DeliveryMessageId, string Producer, long Sequence, DateTimeOffset OccurredAt, string Status, string? FailureCode)
{
    public const string EventType = "notification.project-guest-invitation-email-status.v1";

    public void Validate()
    {
        if (MessageId.Version != 7 || DeliveryMessageId.Version != 7 || Producer != "notification" || Sequence <= 0 || OccurredAt.Offset != TimeSpan.Zero || Status is not ("queued" or "retrying" or "provider-accepted" or "delivered" or "bounced" or "complained" or "failed" or "suppressed" or "expired" or "obsolete") || !OrganizationInvitationEmailStatusV1.IsAllowedFailureCode(FailureCode))
        {
            throw new ArgumentException("Invalid project invitation delivery status contract.");
        }
    }
}
