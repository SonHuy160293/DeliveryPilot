namespace DeliveryPilot.Notification.Contracts;

public sealed record OrganizationInvitationEmailData(string Recipient, string WorkspaceName, IReadOnlyList<string> OfferedRoleNames, string Proof)
{
    public override string ToString() => "OrganizationInvitationEmailData [REDACTED]";
}
