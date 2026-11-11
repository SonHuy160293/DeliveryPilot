using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.ConfirmEngagementLink;

public sealed class ConfirmEngagementLinkHandler(EngagementLinkConfirmationService service) : ICommandHandler<ConfirmEngagementLinkCommand, EngagementLinkPreview>
{
    public async Task<Result<EngagementLinkPreview>> HandleAsync(ConfirmEngagementLinkCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.ExecuteAsync(request.OrganizationId, request.Code!, true, cancellationToken));
}
