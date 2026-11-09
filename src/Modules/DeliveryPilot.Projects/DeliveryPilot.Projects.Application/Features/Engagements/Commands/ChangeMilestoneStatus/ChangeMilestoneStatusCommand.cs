using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneStatus;

public sealed record ChangeMilestoneStatusCommand(Guid OrganizationId, Guid EngagementId, Guid MilestoneId, long? ExpectedVersion, string? Status, string? Reason) : ICommand<MilestoneResult>
{
    public override string ToString() => nameof(ChangeMilestoneStatusCommand);
}
