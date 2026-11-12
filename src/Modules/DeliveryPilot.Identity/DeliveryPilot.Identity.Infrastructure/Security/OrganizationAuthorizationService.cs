using System.Data.Common;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Abstractions.Exceptions;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public sealed class OrganizationAuthorizationService(
    IdentityDbContext dbContext,
    ICurrentUserProvider? currentUserProvider = null,
    TimeProvider? timeProvider = null) : IOrganizationAuthorization
{
    public const string AuthorityUnavailableErrorCode = "Organization.AuthorityUnavailable";
    public const string AuthorityUnavailableMessage = "Workspace authorization state is currently unavailable.";

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private static readonly string[] RoleCatalogReaderPermissions =
    [
        OrganizationCapabilityCatalog.RolesManage,
        OrganizationCapabilityCatalog.RolesAssign,
        OrganizationCapabilityCatalog.InvitationsRead,
        OrganizationCapabilityCatalog.InvitationsIssue,
        OrganizationCapabilityCatalog.InvitationsReissue,
    ];

    public async Task<Guid> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken = default)
    {
        (Guid userId, Guid? sessionId) = ResolveCurrentPrincipal();

        try
        {
            bool isEligible = sessionId.HasValue
                ? await dbContext.AuthSessions
                    .AsNoTracking()
                    .AnyAsync(
                        session => session.Id == sessionId.Value
                            && session.UserId == userId
                            && session.RevokedAt == null
                            && dbContext.Users.Any(user => user.Id == session.UserId && user.Status == AuthSessionService.ActiveUserStatus),
                        cancellationToken)
                : await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == userId && user.Status == AuthSessionService.ActiveUserStatus, cancellationToken);

            if (!isEligible)
            {
                throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
            }

            return userId;
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    public async Task<IReadOnlyList<OrganizationWorkspaceBrief>> ListEligibleWorkspacesAsync(
        CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);

        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();

            var eligibleMemberships = await (
                from membership in dbContext.OrganizationMemberships.AsNoTracking()
                join organization in dbContext.Organizations.AsNoTracking()
                    on membership.OrganizationId equals organization.Id
                where membership.UserId == userId
                    && membership.Status == "active"
                    && organization.Status == "active"
                orderby organization.CreatedAt, organization.Id
                select new
                {
                    Organization = organization,
                    Membership = membership,
                }).ToListAsync(cancellationToken);

            if (eligibleMemberships.Count == 0)
            {
                return [];
            }

            Guid[] organizationIds = eligibleMemberships.Select(x => x.Organization.Id).Distinct().ToArray();
            Guid[] membershipIds = eligibleMemberships.Select(x => x.Membership.Id).Distinct().ToArray();

            List<OrganizationRoleAssignment> assignments = await dbContext.OrganizationRoleAssignments
                .AsNoTracking()
                .Where(assignment => organizationIds.Contains(assignment.OrganizationId)
                    && membershipIds.Contains(assignment.MembershipId)
                    && assignment.RevokedAt == null
                    && (assignment.ExpiresAt == null || assignment.ExpiresAt > now))
                .ToListAsync(cancellationToken);

            Guid[] roleIds = assignments.Select(x => x.RoleId).Distinct().ToArray();
            List<OrganizationRole> roles = roleIds.Length == 0
                ? []
                : await dbContext.OrganizationRoles
                    .AsNoTracking()
                    .Where(role => organizationIds.Contains(role.OrganizationId)
                        && roleIds.Contains(role.Id)
                        && role.RetiredAt == null)
                    .OrderBy(role => role.CreatedAt)
                    .ThenBy(role => role.Id)
                    .ToListAsync(cancellationToken);

            var result = new List<OrganizationWorkspaceBrief>(eligibleMemberships.Count);
            foreach (var item in eligibleMemberships)
            {
                HashSet<Guid> memberRoleIds = assignments
                    .Where(a => a.OrganizationId == item.Organization.Id && a.MembershipId == item.Membership.Id)
                    .Select(a => a.RoleId)
                    .ToHashSet();

                List<OrganizationRoleBrief> roleBriefs = roles
                    .Where(r => r.OrganizationId == item.Organization.Id && memberRoleIds.Contains(r.Id))
                    .Select(MapRoleBrief)
                    .ToList();

                result.Add(new OrganizationWorkspaceBrief(
                    item.Organization.Id,
                    item.Organization.Name,
                    item.Organization.Version,
                    item.Membership.Id,
                    item.Membership.Version,
                    item.Membership.CreatedAt,
                    roleBriefs));
            }

            return result;
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationMemberSecurityContext> AuthorizeMemberScopeAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        (Guid userId, Guid? sessionId) = ResolveCurrentPrincipal();
        return await LoadAndAuthorizeActorAsync(organizationId, userId, sessionId, requiredPermissionCode: null, cancellationToken);
    }

    public async Task<OrganizationMemberSecurityContext> AuthorizeCapabilityAsync(
        Guid organizationId,
        string requiredPermissionCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermissionCode);
        (Guid userId, Guid? sessionId) = ResolveCurrentPrincipal();
        return await LoadAndAuthorizeActorAsync(organizationId, userId, sessionId, requiredPermissionCode, cancellationToken);
    }

    public async Task<OrganizationMemberSecurityContext> AuthorizeRoleCatalogReadAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        OrganizationMemberSecurityContext context = await AuthorizeMemberScopeAsync(organizationId, cancellationToken);
        if (!RoleCatalogReaderPermissions.Any(context.HasPermission))
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        return context;
    }

    public async Task<OrganizationMemberSecurityContext> ReauthorizeJobActorAsync(
        Guid organizationId,
        Guid actorUserId,
        string? requiredPermissionCode = null,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        return await LoadAndAuthorizeActorAsync(organizationId, actorUserId, sessionId: null, requiredPermissionCode, cancellationToken);
    }

    public async Task<OrganizationMemberSecurityContext?> LoadMemberSecurityContextAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || membershipId == Guid.Empty)
        {
            return null;
        }

        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();

            Organization? organization = await dbContext.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(org => org.Id == organizationId && org.Status == "active", cancellationToken);
            if (organization is null)
            {
                return null;
            }

            OrganizationMembership? membership = await dbContext.OrganizationMemberships
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.OrganizationId == organizationId && m.Id == membershipId, cancellationToken);
            if (membership is null)
            {
                return null;
            }

            User? user = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == membership.UserId, cancellationToken);
            if (user is null)
            {
                return null;
            }

            return await BuildMemberSecurityContextAsync(organization, membership, user.Status, now, cancellationToken);
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    public bool CanAccessProjectResource(OrganizationMemberSecurityContext memberContext, Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(memberContext);
        _ = projectId;

        // FR-DP-003-007: Organization membership, Owner/Admin roles, and platform assignments never implicitly authorize project access.
        return false;
    }

    public async Task<bool> IsSessionActiveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return false;
        }

        try
        {
            // Same predicate as AuthenticatedContextValidator: uncached, owned session, not revoked, active account.
            return await dbContext.AuthSessions
                .AsNoTracking()
                .AnyAsync(
                    session => session.Id == sessionId
                        && session.UserId == userId
                        && session.RevokedAt == null
                        && dbContext.Users.Any(user => user.Id == session.UserId && user.Status == AuthSessionService.ActiveUserStatus),
                    cancellationToken);
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    public async Task<IReadOnlySet<Guid>> GetActiveMemberUserIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        try
        {
            List<Guid> ids = await (from membership in dbContext.OrganizationMemberships.AsNoTracking()
                                    join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                                    join organization in dbContext.Organizations.AsNoTracking() on membership.OrganizationId equals organization.Id
                                    where membership.OrganizationId == organizationId && userIds.Contains(membership.UserId)
                                        && membership.Status == "active" && user.Status == AuthSessionService.ActiveUserStatus
                                        && organization.Status == "active"
                                    select membership.UserId).ToListAsync(cancellationToken);
            return ids.ToHashSet();
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationMemberSecurityContext?> EvaluateActorAsync(
        Guid organizationId,
        Guid userId,
        Guid? sessionId,
        string? requiredPermissionCode,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return null;
        }

        try
        {
            if (sessionId.HasValue)
            {
                bool activeSession = await dbContext.AuthSessions
                    .AsNoTracking()
                    .AnyAsync(
                        session => session.Id == sessionId.Value
                            && session.UserId == userId
                            && session.RevokedAt == null,
                        cancellationToken);
                if (!activeSession)
                {
                    return null;
                }
            }

            User? user = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null || !string.Equals(user.Status, AuthSessionService.ActiveUserStatus, StringComparison.Ordinal))
            {
                return null;
            }

            Organization? organization = await dbContext.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(org => org.Id == organizationId && org.Status == "active", cancellationToken);
            if (organization is null)
            {
                return null;
            }

            OrganizationMembership? membership = await dbContext.OrganizationMemberships
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    m => m.OrganizationId == organizationId
                        && m.UserId == userId
                        && m.Status == "active",
                    cancellationToken);
            if (membership is null)
            {
                return null;
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            OrganizationMemberSecurityContext context = await BuildMemberSecurityContextAsync(
                organization,
                membership,
                user.Status,
                now,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(requiredPermissionCode) && !context.HasPermission(requiredPermissionCode))
            {
                return null;
            }

            return context;
        }
        catch (Exception ex) when (IsStoreUnavailable(ex, cancellationToken))
        {
            throw CreateAuthorityUnavailable();
        }
    }

    internal static OrganizationRoleBrief MapRoleBrief(OrganizationRole role)
        => new(role.Id, role.Code, role.Name, role.Origin, role.Version, role.PermissionSetVersion);

    internal static bool IsStoreUnavailable(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DomainException)
        {
            return false;
        }

        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is DbException or TimeoutException or InvalidOperationException or DbUpdateException;
    }

    internal static ServiceUnavailableException CreateAuthorityUnavailable()
        => new(AuthorityUnavailableErrorCode, AuthorityUnavailableMessage);

    private (Guid UserId, Guid? SessionId) ResolveCurrentPrincipal()
    {
        // Only authenticated subject and session ID are read; TenantIds, BranchIds and Permissions claims are ignored.
        CurrentUser? currentUser = currentUserProvider?.GetCurrentUser();
        if (currentUser is null
            || !Guid.TryParse(currentUser.Id, out Guid userId)
            || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        Guid? sessionId = null;
        if (!string.IsNullOrWhiteSpace(currentUser.SessionId))
        {
            if (!Guid.TryParse(currentUser.SessionId, out Guid parsedSessionId) || parsedSessionId == Guid.Empty)
            {
                throw new UnauthorizedException("Authentication.InvalidSession", "The current authentication session is invalid.");
            }

            sessionId = parsedSessionId;
        }

        return (userId, sessionId);
    }

    private async Task<OrganizationMemberSecurityContext> LoadAndAuthorizeActorAsync(
        Guid organizationId,
        Guid userId,
        Guid? sessionId,
        string? requiredPermissionCode,
        CancellationToken cancellationToken)
    {
        OrganizationMemberSecurityContext? context = await EvaluateActorAsync(
            organizationId,
            userId,
            sessionId,
            requiredPermissionCode,
            cancellationToken);

        return context ?? throw OrganizationMutationScope.CreateAccessDenied();
    }

    private async Task<OrganizationMemberSecurityContext> BuildMemberSecurityContextAsync(
        Organization organization,
        OrganizationMembership membership,
        string userStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(userStatus, AuthSessionService.ActiveUserStatus, StringComparison.Ordinal)
            || !string.Equals(membership.Status, "active", StringComparison.Ordinal))
        {
            return new OrganizationMemberSecurityContext(
                organization.Id,
                organization.Version,
                membership.Id,
                membership.UserId,
                userStatus,
                membership.Status,
                membership.CreatedAt,
                membership.Version,
                IsSystemOwner: false,
                IsSystemAdmin: false,
                EffectiveRoles: [],
                EffectivePermissions: new HashSet<string>(StringComparer.Ordinal));
        }

        List<OrganizationRoleAssignment> assignments = await dbContext.OrganizationRoleAssignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organization.Id
                && a.MembershipId == membership.Id
                && a.RevokedAt == null
                && (a.ExpiresAt == null || a.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        Guid[] roleIds = assignments.Select(a => a.RoleId).Distinct().ToArray();
        List<OrganizationRole> activeRoles = roleIds.Length == 0
            ? []
            : await dbContext.OrganizationRoles
                .AsNoTracking()
                .Where(r => r.OrganizationId == organization.Id
                    && roleIds.Contains(r.Id)
                    && r.RetiredAt == null)
                .ToListAsync(cancellationToken);

        Guid[] activeRoleIds = activeRoles.Select(r => r.Id).ToArray();
        List<OrganizationRolePermission> rolePermissions = activeRoleIds.Length == 0
            ? []
            : await dbContext.OrganizationRolePermissions
                .AsNoTracking()
                .Where(p => p.OrganizationId == organization.Id && activeRoleIds.Contains(p.RoleId))
                .ToListAsync(cancellationToken);

        EffectiveOrganizationAuthority authority = OrganizationCapabilityCatalog.ComputeEffectiveAuthority(
            organization.Id,
            activeRoles,
            rolePermissions);

        return new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            membership.Id,
            membership.UserId,
            userStatus,
            membership.Status,
            membership.CreatedAt,
            membership.Version,
            authority.IsSystemOwner,
            authority.IsSystemAdmin,
            authority.EffectiveRoles.Select(MapRoleBrief).ToList(),
            authority.EffectivePermissions);
    }
}
