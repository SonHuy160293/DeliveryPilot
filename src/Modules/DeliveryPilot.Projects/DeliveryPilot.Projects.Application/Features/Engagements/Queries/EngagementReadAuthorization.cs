using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed class EngagementReadAuthorization(IWorkspaceAuthority authority, ICurrentUserProvider users)
{
    public async Task<ReadAccess> RequireAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        CurrentUser? principal = users.GetCurrentUser();
        if (!Guid.TryParse(principal?.Id, out Guid userId) || userId == Guid.Empty
            || !Guid.TryParse(principal.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("Engagement.InvalidOrganization", "A nonempty workspace identifier is required.");
        }

        WorkspaceAuthority facts = await authority.RequireAsync(userId, organizationId, sessionId, null, cancellationToken);
        bool all = facts.HasPermission(WorkspacePermissions.EngagementsReadAll) || facts.HasPermission(WorkspacePermissions.EngagementsManage);
        return new ReadAccess(userId, all);
    }
}
