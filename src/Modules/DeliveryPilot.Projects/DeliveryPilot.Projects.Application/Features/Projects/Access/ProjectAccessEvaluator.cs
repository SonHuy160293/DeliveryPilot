using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Projects.Access;

public sealed class ProjectAccessEvaluator(IProjectReadStore store, IWorkspaceAuthority authority, ICurrentUserProvider users)
{
    public ProjectIdentity CurrentIdentity()
    {
        CurrentUser? current = users.GetCurrentUser();
        if (!Guid.TryParse(current?.Id, out Guid userId) || userId == Guid.Empty || !Guid.TryParse(current.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        return new ProjectIdentity(userId, sessionId);
    }

    public async Task<ProjectAccess> RequireAsync(Guid organizationId, Guid projectId, ProjectIdentity identity, string? requiredPermission, CancellationToken cancellationToken)
    {
        ProjectParticipation? participation = await store.FindParticipationAsync(organizationId, projectId, identity.UserId, cancellationToken);
        if (participation is null) { throw NotFound(); }
        if (participation.Participant.Kind == ProjectParticipant.MemberKind)
        {
            await RequireMemberWorkspaceAsync(organizationId, identity, cancellationToken);
        }
        else if (participation.Participant.Kind != ProjectParticipant.GuestKind)
        {
            throw NotFound();
        }

        bool guest = participation.Participant.Kind == ProjectParticipant.GuestKind;
        string[] roles = FilterRoles(participation.Participant.Kind, participation.Roles);
        var permissions = roles.Where(ProjectPermissions.ByRole.ContainsKey).SelectMany(role => ProjectPermissions.ByRole[role]).ToHashSet(StringComparer.Ordinal);
        if (guest)
        {
            permissions.ExceptWith(ProjectPermissions.MemberOnly);
            permissions.Remove(ProjectPermissions.AssignClientPo);
            permissions.Remove(ProjectPermissions.InitializeProject);
            permissions.Remove(ProjectPermissions.ManageAdministrators);
        }

        if (requiredPermission is not null && !permissions.Contains(requiredPermission))
        {
            throw new ForbiddenException("Project.Forbidden", "You do not have permission for this project action.");
        }

        return new ProjectAccess(participation.Participant, roles, permissions);
    }

    public async Task RequireMemberWorkspaceAsync(Guid organizationId, ProjectIdentity identity, CancellationToken cancellationToken)
    {
        try { await authority.RequireAsync(identity.UserId, organizationId, identity.SessionId, null, cancellationToken); }
        catch (ForbiddenException) { throw NotFound(); }
    }

    public static NotFoundException NotFound() => new("Project.NotFound", "Project was not found.");

    public static string[] FilterRoles(string participantKind, IEnumerable<string> roles)
        => roles.Where(role => ProjectPermissions.ByRole.ContainsKey(role) && (participantKind != ProjectParticipant.GuestKind || role != ProjectRoles.ProjectAdministrator))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
