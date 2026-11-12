using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationMembership;

public sealed class CheckOrganizationMembershipQueryHandler(IOrganizationAuthorization authorization, TimeProvider timeProvider) : IQueryHandler<CheckOrganizationMembershipQuery, OrganizationMembershipCheckResult>
{
    public async Task<Result<OrganizationMembershipCheckResult>> HandleAsync(CheckOrganizationMembershipQuery request, CancellationToken cancellationToken)
    {
        Guid[] ids = request.UserIds!.Distinct().ToArray();
        IReadOnlySet<Guid> active = await authorization.GetActiveMemberUserIdsAsync(request.OrganizationId!.Value, ids, cancellationToken);
        return Result.Success(new OrganizationMembershipCheckResult(timeProvider.GetUtcNow(), ids.Select(id => new OrganizationMembershipCheckItem(id, active.Contains(id))).ToArray()));
    }
}
