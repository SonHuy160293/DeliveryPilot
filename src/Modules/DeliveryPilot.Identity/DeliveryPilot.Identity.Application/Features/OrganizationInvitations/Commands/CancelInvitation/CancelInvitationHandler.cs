using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.CancelInvitation;

public sealed class CancelInvitationHandler(InvitationIssuanceService issuance, IInvitationStore store) : ICommandHandler<CancelInvitationCommand, InvitationSummary>
{
    public async Task<Result<InvitationSummary>> HandleAsync(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        OrganizationInvitation invitation = await issuance.CancelAsync(request.OrganizationId, request.InvitationId, request.ExpectedVersion, request.Reason!, request.CorrelationId, cancellationToken);
        InvitationSummary summary = await store.FindSummaryAsync(invitation.OrganizationId, invitation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Invitation response is unavailable.");
        return Result.Success(summary);
    }
}
