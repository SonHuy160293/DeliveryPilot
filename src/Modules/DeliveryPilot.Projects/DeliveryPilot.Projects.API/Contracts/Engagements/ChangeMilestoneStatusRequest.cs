using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeMilestoneStatusRequest(long? ExpectedVersion, string? Status, string? Reason)
{
    public override string ToString() => nameof(ChangeMilestoneStatusRequest);
}
