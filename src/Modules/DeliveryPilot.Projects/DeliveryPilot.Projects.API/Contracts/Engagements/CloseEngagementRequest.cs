using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CloseEngagementRequest(long? ExpectedVersion, string? Reason)
{
    public override string ToString() => nameof(CloseEngagementRequest);
}
