using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.IssueInvitation;

public sealed class IssueInvitationHandler(InvitationIssuanceService issuance, IInvitationStore store) : ICommandHandler<IssueInvitationCommand, InvitationSummary>
{
    public async Task<Result<InvitationSummary>> HandleAsync(IssueInvitationCommand request, CancellationToken cancellationToken)
    {
        OrganizationInvitation invitation = await issuance.IssueAsync(request.OrganizationId, request.RecipientEmail!, request.RoleIds!, request.CorrelationId, cancellationToken);
        InvitationSummary summary = await store.FindSummaryAsync(invitation.OrganizationId, invitation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Invitation response is unavailable.");
        return Result.Success(summary);
    }
}
