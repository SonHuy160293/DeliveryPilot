using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.PreviewInvitation;

public sealed class PreviewInvitationHandler(InvitationResponseService service) : IQueryHandler<PreviewInvitationQuery, InvitationPreview>
{
    public async Task<Result<InvitationPreview>> HandleAsync(PreviewInvitationQuery request, CancellationToken cancellationToken)
        => Result.Success(await service.PreviewAsync(request.InvitationId, request.Proof, cancellationToken));
}
