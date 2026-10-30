using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ResendInvitation;

public sealed class ResendInvitationHandler(InvitationIssuanceService issuance, IInvitationStore store) : ICommandHandler<ResendInvitationCommand, InvitationSummary>
{
    public async Task<Result<InvitationSummary>> HandleAsync(ResendInvitationCommand request, CancellationToken cancellationToken)
    {
        OrganizationInvitation invitation = await issuance.ResendAsync(request.OrganizationId, request.InvitationId, request.ExpectedVersion, request.CorrelationId, request.Reason, cancellationToken);
        InvitationSummary summary = await store.FindSummaryAsync(invitation.OrganizationId, invitation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Invitation response is unavailable.");
        return Result.Success(summary);
    }
}
