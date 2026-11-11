using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ConfirmEngagementLink;

public sealed record ConfirmEngagementLinkCommand(Guid OrganizationId, string? Code) : ICommand<EngagementLinkPreview>
{
    public override string ToString() => nameof(ConfirmEngagementLinkCommand);
}
