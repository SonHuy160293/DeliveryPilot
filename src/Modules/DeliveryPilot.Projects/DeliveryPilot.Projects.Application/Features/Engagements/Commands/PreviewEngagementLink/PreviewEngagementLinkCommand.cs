using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.PreviewEngagementLink;

public sealed record PreviewEngagementLinkCommand(Guid OrganizationId, string? Code) : ICommand<EngagementLinkPreview>
{
    public override string ToString() => nameof(PreviewEngagementLinkCommand);
}
