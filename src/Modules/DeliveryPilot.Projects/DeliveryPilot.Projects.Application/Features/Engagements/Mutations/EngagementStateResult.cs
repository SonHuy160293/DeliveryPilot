using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public sealed record EngagementStateResult(Guid Id, string Status, long Version, DateTimeOffset? ClosedAt, Guid? ClosedByUserId)
{
    public static EngagementStateResult From(Engagement engagement)
        => new(engagement.Id, engagement.Status, engagement.Version, engagement.ClosedAt, engagement.ClosedByUserId);
}
