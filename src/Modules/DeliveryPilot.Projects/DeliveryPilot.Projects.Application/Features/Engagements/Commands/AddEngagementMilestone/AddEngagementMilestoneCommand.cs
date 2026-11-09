using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementMilestone;

public sealed record AddEngagementMilestoneCommand(Guid OrganizationId, Guid EngagementId, string? Name, string? Description, DateOnly? TargetDate) : ICommand<MilestoneResult>
{
    public override string ToString() => nameof(AddEngagementMilestoneCommand);
}
