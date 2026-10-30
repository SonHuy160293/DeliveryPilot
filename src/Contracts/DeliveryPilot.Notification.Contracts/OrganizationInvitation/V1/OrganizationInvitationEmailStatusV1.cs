namespace DeliveryPilot.Notification.Contracts;

public sealed record OrganizationInvitationEmailStatusV1(Guid MessageId, Guid DeliveryMessageId, string Producer, long Sequence, DateTimeOffset OccurredAt, string Status, string? FailureCode)
{
    public const string EventType = "notification.organization-invitation-email-status.v1";

    public void Validate()
    {
        if (MessageId.Version != 7 || DeliveryMessageId.Version != 7 || Producer != "notification" || Sequence <= 0 || OccurredAt.Offset != TimeSpan.Zero || Status is not ("queued" or "retrying" or "provider-accepted" or "delivered" or "bounced" or "complained" or "failed" or "suppressed" or "expired" or "obsolete") || !IsAllowedFailureCode(FailureCode))
        {
            throw new ArgumentException("Invalid invitation delivery status contract.");
        }
    }

    public static bool IsAllowedFailureCode(string? code) => code is null or "AuthorityUnavailable" or "ProviderTimeout" or "ProviderNetwork" or "EncryptionUnavailable" or "InvalidTemplatePayload" or "DeliveryDisabled" or "ProviderConfiguration" or "ProviderRejected" or "ProviderUnavailable" or "AcceptanceUnknown" or "Expired" or "Obsolete" or "Other";
}
