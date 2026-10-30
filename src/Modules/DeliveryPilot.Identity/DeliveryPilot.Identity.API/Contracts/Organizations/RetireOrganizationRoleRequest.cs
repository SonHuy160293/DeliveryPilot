using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Organizations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RetireOrganizationRoleRequest(long ExpectedVersion, string? Reason)
{
    public override string ToString() => nameof(RetireOrganizationRoleRequest);
}
