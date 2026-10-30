using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyOrganizations;

public sealed class GetMyOrganizationsQueryHandler(IOrganizationAuthorization authorization, IOrganizationReadStore store) : IQueryHandler<GetMyOrganizationsQuery, OrganizationPageResponse<OrganizationWorkspaceResponse>>
{
    public async Task<Result<OrganizationPageResponse<OrganizationWorkspaceResponse>>> HandleAsync(GetMyOrganizationsQuery request, CancellationToken cancellationToken)
    {
        Guid userId = await authorization.GetAuthenticatedUserIdAsync(cancellationToken);
        OrganizationReadPage<OrganizationReadWorkspace> page = await store.ListWorkspacesAsync(userId, request.Limit, request.Cursor, cancellationToken);
        OrganizationWorkspaceResponse[] items = page.Items.Select(workspace => new OrganizationWorkspaceResponse(
            workspace.OrganizationId,
            workspace.Name,
            workspace.Roles.Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray(),
            workspace.MembershipId,
            workspace.Version,
            workspace.MembershipVersion)).ToArray();
        return Result.Success(new OrganizationPageResponse<OrganizationWorkspaceResponse>(items, page.NextCursor));
    }
}
