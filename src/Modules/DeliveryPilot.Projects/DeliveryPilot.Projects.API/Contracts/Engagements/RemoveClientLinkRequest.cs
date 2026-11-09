using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveClientLinkRequest(string? Reason)
{
    public override string ToString() => nameof(RemoveClientLinkRequest);
}
