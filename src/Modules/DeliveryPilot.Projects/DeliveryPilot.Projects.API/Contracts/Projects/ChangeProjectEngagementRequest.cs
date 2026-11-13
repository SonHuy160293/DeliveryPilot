using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Projects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeProjectEngagementRequest(long? ExpectedVersion, string? Kind, Guid? EngagementId, string? Reason)
{
    public override string ToString() => nameof(ChangeProjectEngagementRequest);
}
