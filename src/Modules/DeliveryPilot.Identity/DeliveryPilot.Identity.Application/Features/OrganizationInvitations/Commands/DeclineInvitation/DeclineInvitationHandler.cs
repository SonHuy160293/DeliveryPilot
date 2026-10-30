using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.DeclineInvitation;

public sealed class DeclineInvitationHandler(InvitationResponseService service) : ICommandHandler<DeclineInvitationCommand, InvitationDecline>
{
    public async Task<Result<InvitationDecline>> HandleAsync(DeclineInvitationCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.DeclineAsync(request.InvitationId, request.Proof, request.ExpectedVersion, request.CorrelationId, cancellationToken));
}
