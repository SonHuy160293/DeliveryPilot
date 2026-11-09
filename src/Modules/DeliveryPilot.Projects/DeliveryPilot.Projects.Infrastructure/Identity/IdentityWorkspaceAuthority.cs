using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

/// <summary>
/// Uncached workspace authority lookup against Identity (ADR-001 D-03/D-04). Denials surface as
/// <see cref="ForbiddenException"/>; an unavailable or malformed answer fails closed with
/// <see cref="ServiceUnavailableException"/>.
/// </summary>
public sealed class IdentityWorkspaceAuthority(IIdentityAuthorityClient client) : IWorkspaceAuthority
{
    public async Task<WorkspaceAuthority> RequireAsync(
        Guid userId,
        Guid organizationId,
        Guid? sessionId,
        string? requiredPermissionCode,
        CancellationToken cancellationToken)
    {
        var result = await client.CheckOrganizationAuthorityAsync(
            new OrganizationAuthorityCheckRequest(userId, organizationId, sessionId, requiredPermissionCode),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            throw new ServiceUnavailableException("Identity.Unavailable", "Workspace authority could not be confirmed.");
        }

        OrganizationAuthorityCheckResult data = result.Data;
        if (data.Decision != AuthorityDecision.Allowed)
        {
            throw new ForbiddenException("Workspace.Forbidden", "You do not have access to this workspace action.");
        }

        if (data.OrganizationId != organizationId || data.MembershipId is null)
        {
            throw new ServiceUnavailableException("Identity.InvalidResponse", "Workspace authority could not be confirmed.");
        }

        var permissions = new HashSet<string>(data.EffectivePermissions ?? [], StringComparer.Ordinal);
        if (requiredPermissionCode is not null && !permissions.Contains(requiredPermissionCode))
        {
            throw new ForbiddenException("Workspace.Forbidden", "You do not have access to this workspace action.");
        }

        return new WorkspaceAuthority(
            organizationId,
            data.MembershipId.Value,
            data.IsOwner ?? false,
            data.IsAdmin ?? false,
            permissions,
            data.EvaluatedAt);
    }
}
