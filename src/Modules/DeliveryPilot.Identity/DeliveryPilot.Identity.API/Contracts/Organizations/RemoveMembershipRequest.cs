using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Organizations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveMembershipRequest(long ExpectedMembershipVersion, string? Reason)
{
    public override string ToString() => "RemoveMembershipRequest";
}
