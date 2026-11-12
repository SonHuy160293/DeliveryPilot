namespace DeliveryPilot.Identity.Contracts.Authority;

public sealed record OrganizationMembershipCheckItem(Guid UserId, bool IsActiveMember)
{
    public override string ToString() => nameof(OrganizationMembershipCheckItem);
}
