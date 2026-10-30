using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyMembership;

public sealed class GetMyMembershipQueryHandler(IOrganizationAuthorization authorization, IOrganizationReadStore store) : IQueryHandler<GetMyMembershipQuery, OrganizationMemberResponse>
{
    public async Task<Result<OrganizationMemberResponse>> HandleAsync(GetMyMembershipQuery request, CancellationToken cancellationToken)
    {
        OrganizationMemberSecurityContext scope = await authorization.AuthorizeMemberScopeAsync(request.OrganizationId, cancellationToken);
        OrganizationReadMember? member = await store.GetMemberAsync(request.OrganizationId, scope.MembershipId, cancellationToken);
        if (member is null)
        {
            throw new ServiceUnavailableException("Organization.AuthorityUnavailable", "Workspace authorization state is currently unavailable.");
        }

        return Result.Success(new OrganizationMemberResponse(
            member.MembershipId, member.UserId, member.DisplayName, member.PrimaryEmail, member.Status,
            member.Roles.Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray(),
            member.JoinedAt, member.Version));
    }
}
