using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementMilestone;

public sealed record EditEngagementMilestoneCommand(Guid OrganizationId, Guid EngagementId, Guid MilestoneId, long? ExpectedVersion, string? Name, string? Description) : ICommand<MilestoneResult>
{
    public override string ToString() => nameof(EditEngagementMilestoneCommand);
}
