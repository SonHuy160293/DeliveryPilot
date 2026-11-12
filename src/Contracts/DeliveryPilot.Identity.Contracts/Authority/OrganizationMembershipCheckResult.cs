namespace DeliveryPilot.Identity.Contracts.Authority;

public sealed record OrganizationMembershipCheckResult(DateTimeOffset EvaluatedAt, IReadOnlyList<OrganizationMembershipCheckItem> Members)
{
    public override string ToString() => nameof(OrganizationMembershipCheckResult);
}
