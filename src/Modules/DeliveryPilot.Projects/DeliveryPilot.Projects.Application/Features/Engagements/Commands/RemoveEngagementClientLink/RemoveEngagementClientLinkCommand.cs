using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementClientLink;

public sealed record RemoveEngagementClientLinkCommand(Guid OrganizationId, Guid EngagementId, long? ExpectedVersion, string? Reason) : ICommand<EngagementStateResult>
{
    public override string ToString() => nameof(RemoveEngagementClientLinkCommand);
}
