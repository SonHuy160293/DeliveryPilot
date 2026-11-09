using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditMilestoneRequest(long? ExpectedVersion, string? Name, string? Description)
{
    public override string ToString() => nameof(EditMilestoneRequest);
}
