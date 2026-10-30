using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListOrganizationRoles;

public sealed class ListOrganizationRolesHandler(IOrganizationAuthorization authorization, IOrganizationRoleStore store) : IQueryHandler<ListOrganizationRolesQuery, OrganizationPageResponse<CreateOrganizationRoleResponse>>
{
    public async Task<Result<OrganizationPageResponse<CreateOrganizationRoleResponse>>> HandleAsync(ListOrganizationRolesQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeRoleCatalogReadAsync(request.OrganizationId, cancellationToken);
        OrganizationReadPage<OrganizationRoleRead> page = await store.ListAsync(request.OrganizationId, request.Limit, request.Cursor, cancellationToken);
        CreateOrganizationRoleResponse[] items = page.Items.Select(item => new CreateOrganizationRoleResponse(item.Role.Id, item.Role.Code, item.Role.Name, item.Role.Description, item.Role.Origin, item.Permissions.Select(permission => permission.PermissionCode).Order(StringComparer.Ordinal).ToArray(), item.Role.Version, item.Role.PermissionSetVersion, item.Role.RetiredAt)).ToArray();
        return Result.Success(new OrganizationPageResponse<CreateOrganizationRoleResponse>(items, page.NextCursor));
    }
}
