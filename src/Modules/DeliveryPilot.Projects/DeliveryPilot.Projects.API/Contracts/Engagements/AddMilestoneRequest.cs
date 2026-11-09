using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddMilestoneRequest(string? Name, string? Description, DateOnly? TargetDate)
{
    public override string ToString() => nameof(AddMilestoneRequest);
}
