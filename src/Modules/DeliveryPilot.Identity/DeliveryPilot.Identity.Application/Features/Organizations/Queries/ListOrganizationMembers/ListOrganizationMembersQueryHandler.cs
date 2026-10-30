using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.ListOrganizationMembers;

public sealed class ListOrganizationMembersQueryHandler(IOrganizationAuthorization authorization, IOrganizationReadStore store) : IQueryHandler<ListOrganizationMembersQuery, OrganizationPageResponse<OrganizationMemberResponse>>
{
    public async Task<Result<OrganizationPageResponse<OrganizationMemberResponse>>> HandleAsync(ListOrganizationMembersQuery request, CancellationToken cancellationToken)
    {
        await authorization.AuthorizeCapabilityAsync(request.OrganizationId, OrganizationCapabilityCatalog.MembersRead, cancellationToken);
        OrganizationReadPage<OrganizationReadMember> page = await store.ListMembersAsync(request.OrganizationId, request.Status, request.Limit, request.Cursor, cancellationToken);
        OrganizationMemberResponse[] items = page.Items.Select(member => new OrganizationMemberResponse(
            member.MembershipId, member.UserId, member.DisplayName, member.PrimaryEmail, member.Status,
            member.Roles.Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray(),
            member.JoinedAt, member.Version)).ToArray();
        return Result.Success(new OrganizationPageResponse<OrganizationMemberResponse>(items, page.NextCursor));
    }
}
