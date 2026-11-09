using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementContact;

public sealed record RemoveEngagementContactCommand(Guid OrganizationId, Guid EngagementId, Guid ContactId, long? ExpectedVersion, string? Reason) : ICommand<ContactResult>
{
    public override string ToString() => nameof(RemoveEngagementContactCommand);
}
