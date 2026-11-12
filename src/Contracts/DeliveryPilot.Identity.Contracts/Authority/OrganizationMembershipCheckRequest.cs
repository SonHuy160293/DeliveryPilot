using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.Contracts.Authority;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizationMembershipCheckRequest(Guid? OrganizationId, IReadOnlyList<Guid>? UserIds)
{
    public override string ToString() => nameof(OrganizationMembershipCheckRequest);
}
