using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IssueEngagementLinkCodeRequest(long? ExpectedVersion)
{
    public override string ToString() => nameof(IssueEngagementLinkCodeRequest);
}
