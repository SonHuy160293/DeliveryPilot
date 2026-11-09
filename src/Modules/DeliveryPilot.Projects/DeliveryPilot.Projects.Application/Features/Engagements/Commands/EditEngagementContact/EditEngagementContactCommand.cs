using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementContact;

public sealed record EditEngagementContactCommand(Guid OrganizationId, Guid EngagementId, Guid ContactId, long? ExpectedVersion, string? Name, string? Email, string? ContactRole) : ICommand<ContactResult>
{
    public override string ToString() => nameof(EditEngagementContactCommand);
}
