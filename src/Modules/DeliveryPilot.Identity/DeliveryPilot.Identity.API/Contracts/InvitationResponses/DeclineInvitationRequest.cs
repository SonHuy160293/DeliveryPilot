using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.InvitationResponses;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeclineInvitationRequest(string? Proof, long ExpectedVersion)
{
    public override string ToString() => "DeclineInvitationRequest { Proof = [REDACTED] }";
}
