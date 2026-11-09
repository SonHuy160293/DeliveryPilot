using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public sealed record MilestoneResult(Guid Id, Guid EngagementId, string Name, string? Description, DateOnly OriginalTargetDate, DateOnly CurrentTargetDate, string Status, long Version)
{
    public static MilestoneResult From(EngagementMilestone milestone)
        => new(milestone.Id, milestone.EngagementId, milestone.Name, milestone.Description, milestone.OriginalTargetDate, milestone.CurrentTargetDate, milestone.Status, milestone.Version);

    public override string ToString() => nameof(MilestoneResult);
}
