using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngagementLinkCodeRequest(string? Code)
{
    public override string ToString() => nameof(EngagementLinkCodeRequest);
}
