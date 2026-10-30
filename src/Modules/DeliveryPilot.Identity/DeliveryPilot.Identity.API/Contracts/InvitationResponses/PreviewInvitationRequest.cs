using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.InvitationResponses;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewInvitationRequest(string? Proof)
{
    public override string ToString() => "PreviewInvitationRequest { Proof = [REDACTED] }";
}
