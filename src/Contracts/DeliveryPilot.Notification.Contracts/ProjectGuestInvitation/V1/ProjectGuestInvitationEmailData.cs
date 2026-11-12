namespace DeliveryPilot.Notification.Contracts;

public sealed record ProjectGuestInvitationEmailData(string Recipient, string ProjectName, string OfferedRoleName, string InviterDisplayName, string Proof)
{
    public override string ToString() => "ProjectGuestInvitationEmailData [REDACTED]";
}
