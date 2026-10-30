using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.AcceptInvitation;

public sealed class AcceptInvitationHandler(InvitationResponseService service) : ICommandHandler<AcceptInvitationCommand, InvitationAcceptance>
{
    public async Task<Result<InvitationAcceptance>> HandleAsync(AcceptInvitationCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.AcceptAsync(request.InvitationId, request.Proof, request.ExpectedVersion, request.CorrelationId, cancellationToken));
}
