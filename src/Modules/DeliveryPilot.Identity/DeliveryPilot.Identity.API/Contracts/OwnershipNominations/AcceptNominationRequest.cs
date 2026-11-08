using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OwnershipNominations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptNominationRequest(long ExpectedVersion)
{
    public override string ToString() => "AcceptNominationRequest";
}
