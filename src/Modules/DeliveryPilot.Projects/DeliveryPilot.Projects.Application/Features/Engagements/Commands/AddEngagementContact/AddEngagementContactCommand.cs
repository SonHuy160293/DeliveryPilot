using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementContact;

public sealed record AddEngagementContactCommand(Guid OrganizationId, Guid EngagementId, string? Name, string? Email, string? ContactRole) : ICommand<ContactResult>
{
    public override string ToString() => nameof(AddEngagementContactCommand);
}
