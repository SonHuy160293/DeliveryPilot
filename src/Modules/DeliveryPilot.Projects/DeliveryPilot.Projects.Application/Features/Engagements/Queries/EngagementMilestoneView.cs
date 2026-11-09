namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementMilestoneView(Guid Id, string Name, string? Description, DateOnly OriginalTargetDate, DateOnly CurrentTargetDate, string Status, Guid CreatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version)
{
    public override string ToString() => nameof(EngagementMilestoneView);
}
