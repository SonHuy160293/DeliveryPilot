using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Projects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProjectRequest(string? Name, string? Purpose, string? Kind, Guid? EngagementId)
{
    public override string ToString() => nameof(CreateProjectRequest);
}
