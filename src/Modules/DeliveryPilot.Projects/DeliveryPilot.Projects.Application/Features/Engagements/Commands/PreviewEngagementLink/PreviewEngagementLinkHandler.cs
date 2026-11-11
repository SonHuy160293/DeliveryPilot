using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.PreviewEngagementLink;

public sealed class PreviewEngagementLinkHandler(EngagementLinkConfirmationService service) : ICommandHandler<PreviewEngagementLinkCommand, EngagementLinkPreview>
{
    public async Task<Result<EngagementLinkPreview>> HandleAsync(PreviewEngagementLinkCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.ExecuteAsync(request.OrganizationId, request.Code!, false, cancellationToken));
}
