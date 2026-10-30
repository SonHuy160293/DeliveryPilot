using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetInvitation;

public sealed class GetInvitationHandler(IOrganizationAuthorization authorization, IInvitationStore store) : IQueryHandler<GetInvitationQuery, InvitationSummary>
{
    public async Task<Result<InvitationSummary>> HandleAsync(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeCapabilityAsync(request.OrganizationId, OrganizationCapabilityCatalog.InvitationsRead, cancellationToken);
        InvitationSummary summary = await store.FindSummaryAsync(request.OrganizationId, request.InvitationId, cancellationToken)
            ?? throw new NotFoundException("Invitation.NotFound", "Invitation was not found.");
        return Result.Success(summary);
    }
}
