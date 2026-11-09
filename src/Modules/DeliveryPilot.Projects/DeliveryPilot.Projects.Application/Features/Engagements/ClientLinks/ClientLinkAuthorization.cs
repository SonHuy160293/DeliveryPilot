using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed class ClientLinkAuthorization(IWorkspaceAuthority authority, ICurrentUserProvider users)
{
    public async Task<Guid> RequireAsync(Guid organizationId, CancellationToken cancellationToken)
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
        if (!facts.IsOwner && !facts.IsAdmin)
        {
            throw new ForbiddenException("EngagementLink.Forbidden", "Only an organization owner or administrator may manage links.");
        }

        return userId;
    }
}
