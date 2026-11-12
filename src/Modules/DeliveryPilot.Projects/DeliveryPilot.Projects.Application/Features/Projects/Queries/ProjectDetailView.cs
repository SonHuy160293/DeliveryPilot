using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectDetailView(Guid Id, Guid OrganizationId, string Name, string Purpose, string Kind, string Status, short SprintLengthWeeks, Guid? ClientProductOwnerParticipantId, DateTimeOffset? ActivatedAt, Guid? ActivatedByUserId, long Version, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EngagementId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientPoWaiverReason { get; init; }
}
