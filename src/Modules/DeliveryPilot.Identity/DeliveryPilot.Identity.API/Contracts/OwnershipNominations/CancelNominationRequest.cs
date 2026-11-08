using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OwnershipNominations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelNominationRequest(long ExpectedVersion, string? Reason)
{
    public override string ToString() => "CancelNominationRequest";
}
