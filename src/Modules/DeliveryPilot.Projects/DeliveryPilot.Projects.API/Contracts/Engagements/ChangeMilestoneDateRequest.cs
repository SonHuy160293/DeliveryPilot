using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeMilestoneDateRequest(long? ExpectedVersion, DateOnly? TargetDate, string? Reason)
{
    public override string ToString() => nameof(ChangeMilestoneDateRequest);
}
