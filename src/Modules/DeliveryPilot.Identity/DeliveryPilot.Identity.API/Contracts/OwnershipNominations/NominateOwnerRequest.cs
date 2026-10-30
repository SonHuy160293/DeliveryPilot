using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OwnershipNominations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NominateOwnerRequest(Guid MembershipId)
{
    public override string ToString() => "NominateOwnerRequest";
}
