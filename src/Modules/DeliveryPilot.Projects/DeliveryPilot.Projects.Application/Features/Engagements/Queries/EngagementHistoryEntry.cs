using System.Text.Json;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementHistoryEntry(Guid Id, string EventType, string SubjectType, Guid SubjectId, Guid ActorUserId, Guid ActorOrganizationId, DateTimeOffset OccurredAt, string? Reason, JsonElement Details, long EngagementVersion)
{
    public override string ToString() => nameof(EngagementHistoryEntry);
}
