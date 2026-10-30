using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OwnershipNominations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeclineNominationRequest(long ExpectedVersion)
{
    public override string ToString() => "DeclineNominationRequest";
}
