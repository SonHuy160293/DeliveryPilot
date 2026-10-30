using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ReissueInvitation;

public sealed class ReissueInvitationHandler(InvitationIssuanceService issuance, IInvitationStore store) : ICommandHandler<ReissueInvitationCommand, InvitationSummary>
{
    public async Task<Result<InvitationSummary>> HandleAsync(ReissueInvitationCommand request, CancellationToken cancellationToken)
    {
        OrganizationInvitation invitation = await issuance.ReissueAsync(request.OrganizationId, request.InvitationId, request.ExpectedVersion, request.RoleIds!, request.Reason!, request.CorrelationId, cancellationToken);
        InvitationSummary summary = await store.FindSummaryAsync(invitation.OrganizationId, invitation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Invitation response is unavailable.");
        return Result.Success(summary);
    }
}
