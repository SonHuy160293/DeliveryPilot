using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;

public sealed class CheckOrganizationAuthorityQueryHandler(
    IOrganizationAuthorization authorization,
    TimeProvider timeProvider) : IQueryHandler<CheckOrganizationAuthorityQuery, OrganizationAuthorityCheckResult>
{
    public async Task<Result<OrganizationAuthorityCheckResult>> HandleAsync(
        CheckOrganizationAuthorityQuery request,
        CancellationToken cancellationToken)
    {
        OrganizationMemberSecurityContext? context = await authorization.EvaluateActorAsync(
            request.OrganizationId,
            request.UserId,
            request.SessionId,
            request.RequiredPermissionCode,
            cancellationToken);

        DateTimeOffset evaluatedAt = timeProvider.GetUtcNow();
        if (context is null || !context.IsEligibleActiveMember)
        {
            // Deliberately no reason, membership or role detail: callers cannot enumerate workspaces or members.
            return Result.Success(OrganizationAuthorityCheckResult.Denied(evaluatedAt));
        }

        return Result.Success(new OrganizationAuthorityCheckResult(
            AuthorityDecision.Allowed,
            evaluatedAt,
            context.OrganizationId,
            context.OrganizationVersion,
            context.MembershipId,
            context.MembershipVersion,
            context.IsSystemOwner,
            context.IsSystemAdmin,
            context.EffectivePermissions.Order(StringComparer.Ordinal).ToArray()));
    }
}
