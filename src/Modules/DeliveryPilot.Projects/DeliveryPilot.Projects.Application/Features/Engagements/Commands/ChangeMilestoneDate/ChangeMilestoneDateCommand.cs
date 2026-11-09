using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ChangeMilestoneDate;

public sealed record ChangeMilestoneDateCommand(Guid OrganizationId, Guid EngagementId, Guid MilestoneId, long? ExpectedVersion, DateOnly? TargetDate, string? Reason) : ICommand<MilestoneResult>
{
    public override string ToString() => nameof(ChangeMilestoneDateCommand);
}
