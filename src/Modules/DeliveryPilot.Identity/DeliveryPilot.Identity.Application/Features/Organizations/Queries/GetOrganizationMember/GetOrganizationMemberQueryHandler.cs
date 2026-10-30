using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetOrganizationMember;

public sealed class GetOrganizationMemberQueryHandler(IOrganizationAuthorization authorization, IOrganizationReadStore store) : IQueryHandler<GetOrganizationMemberQuery, OrganizationMemberResponse>
{
    public async Task<Result<OrganizationMemberResponse>> HandleAsync(GetOrganizationMemberQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeCapabilityAsync(request.OrganizationId, OrganizationCapabilityCatalog.MembersRead, cancellationToken);
        OrganizationReadMember? member = await store.GetMemberAsync(request.OrganizationId, request.MembershipId, cancellationToken);
        if (member is null)
        {
            throw new NotFoundException("Membership.NotFound", "Membership was not found.");
        }

        return Result.Success(new OrganizationMemberResponse(
            member.MembershipId, member.UserId, member.DisplayName, member.PrimaryEmail, member.Status,
            member.Roles.Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray(),
            member.JoinedAt, member.Version));
    }
}
