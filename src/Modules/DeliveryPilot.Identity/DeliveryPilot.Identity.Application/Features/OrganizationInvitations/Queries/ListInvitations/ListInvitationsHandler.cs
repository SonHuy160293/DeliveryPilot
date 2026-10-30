using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.ListInvitations;

public sealed class ListInvitationsHandler(IOrganizationAuthorization authorization, IInvitationStore store) : IQueryHandler<ListInvitationsQuery, OrganizationPageResponse<InvitationSummary>>
{
    public async Task<Result<OrganizationPageResponse<InvitationSummary>>> HandleAsync(ListInvitationsQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeCapabilityAsync(request.OrganizationId, OrganizationCapabilityCatalog.InvitationsRead, cancellationToken);
        OrganizationReadPage<InvitationSummary> page = await store.ListAsync(request.OrganizationId, request.Status, request.Limit, request.Cursor, cancellationToken);
        return Result.Success(new OrganizationPageResponse<InvitationSummary>(page.Items, page.NextCursor));
    }
}
