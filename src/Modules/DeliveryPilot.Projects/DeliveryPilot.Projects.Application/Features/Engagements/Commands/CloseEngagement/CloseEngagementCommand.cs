using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CloseEngagement;

public sealed record CloseEngagementCommand(Guid OrganizationId, Guid EngagementId, long? ExpectedVersion, string? Reason) : ICommand<EngagementStateResult>
{
    public override string ToString() => nameof(CloseEngagementCommand);
}
