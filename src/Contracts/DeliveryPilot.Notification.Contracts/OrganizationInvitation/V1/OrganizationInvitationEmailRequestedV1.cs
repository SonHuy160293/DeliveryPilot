using System.Globalization;
using System.Text;

namespace DeliveryPilot.Notification.Contracts;

public sealed record OrganizationInvitationEmailRequestedV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    string Producer,
    Guid OrganizationId,
    Guid InvitationId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    EncryptedEmailPayload Payload)
{
    public const string EventType = "notification.organization-invitation-email-requested.v1";
    public const string TemplateId = "organization-invitation.v1";
    public const string Purpose = "identity.organization-invitation";

    public byte[] AssociatedData()
        => Encoding.UTF8.GetBytes(string.Join('|', MessageId.ToString("D"), CorrelationId.ToString("D"), CausationId?.ToString("D") ?? string.Empty, Producer, Purpose, TemplateId, OrganizationId.ToString("D"), InvitationId.ToString("D"), IssuedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), ExpiresAt.UtcTicks.ToString(CultureInfo.InvariantCulture), Payload.KeyId, Payload.Version.ToString(CultureInfo.InvariantCulture)));

    public void Validate()
    {
        if (MessageId.Version != 7 || CorrelationId == Guid.Empty || CausationId == Guid.Empty || Producer != "identity" || OrganizationId == Guid.Empty || InvitationId.Version != 7 || IssuedAt.Offset != TimeSpan.Zero || ExpiresAt.Offset != TimeSpan.Zero || ExpiresAt - IssuedAt != TimeSpan.FromDays(7) || Payload is null || Payload.Version != 1 || Payload.KeyId is null || Payload.KeyId.Length is < 1 or > 64 || Payload.KeyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || Payload.Ciphertext is null || Payload.Ciphertext.Length is < 1 or > 32768 || Payload.WrappedKey is null || Payload.WrappedKey.Length is < 1 or > 2048 || Payload.Nonce is null || Payload.Nonce.Length is < 1 or > 32 || Payload.Tag is null || Payload.Tag.Length is < 1 or > 32)
        {
            throw new ArgumentException("Invalid invitation delivery contract.");
        }
    }

    public override string ToString() => $"OrganizationInvitationEmailRequestedV1 {MessageId} [REDACTED]";
}
