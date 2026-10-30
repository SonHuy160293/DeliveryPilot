using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListDelegablePermissions;

public sealed class ListDelegablePermissionsHandler(IOrganizationAuthorization authorization) : IQueryHandler<ListDelegablePermissionsQuery, DelegablePermissionsResponse>
{
    public async Task<Result<DelegablePermissionsResponse>> HandleAsync(ListDelegablePermissionsQuery request, CancellationToken cancellationToken)
    {
        OrganizationMemberSecurityContext actor = await authorization.AuthorizeRoleCatalogReadAsync(request.OrganizationId, cancellationToken);
        string[] codes = OrganizationCapabilityCatalog.DelegablePermissionCodes.Where(actor.EffectivePermissions.Contains).ToArray();
        return Result.Success(new DelegablePermissionsResponse(codes));
    }
}
