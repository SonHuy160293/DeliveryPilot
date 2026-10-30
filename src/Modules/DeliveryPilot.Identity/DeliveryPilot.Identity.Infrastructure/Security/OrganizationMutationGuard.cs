using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// Serializes all workspace security mutations inside the pipeline-owned command transaction using
/// a deterministic lock order:
/// 1. <c>shared."Organizations"</c> row (<c>FOR UPDATE</c>).
/// 2. <c>shared."Users"</c> rows for the actor, optional target/sponsor, and current active Owners (<c>ORDER BY "Id" FOR UPDATE</c>).
/// 3. The authenticated actor's <c>shared."AuthSessions"</c> row (<c>FOR UPDATE</c>), when present.
/// 4. <c>shared."OrganizationMemberships"</c> rows in the workspace (<c>ORDER BY "Id" FOR UPDATE</c>).
/// </summary>
public sealed class OrganizationMutationGuard(
    IdentityDbContext dbContext,
    ICurrentUserProvider? currentUserProvider = null,
    TimeProvider? timeProvider = null) : IOrganizationMutationGuard
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<OrganizationMutationScope> AcquireForInvitationAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        EnsureCommandTransaction();
        (Guid actorUserId, Guid? sessionId) = ResolveCurrentPrincipal();
        return AcquireInternalAsync(organizationId, actorUserId, sessionId, null, cancellationToken, invitationId);
    }

    public Task<OrganizationMutationScope> AcquireAsync(
        Guid organizationId,
        Guid? targetMembershipId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCommandTransaction();
        (Guid actorUserId, Guid? sessionId) = ResolveCurrentPrincipal();
        return AcquireInternalAsync(organizationId, actorUserId, sessionId, targetMembershipId, cancellationToken);
    }

    public Task<OrganizationMutationScope> AcquireForActorAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid? targetMembershipId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCommandTransaction();
        if (actorUserId == Guid.Empty)
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        return AcquireInternalAsync(organizationId, actorUserId, sessionId: null, targetMembershipId, cancellationToken);
    }

    private async Task<OrganizationMutationScope> AcquireInternalAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid? sessionId,
        Guid? targetMembershipId,
        CancellationToken cancellationToken,
        Guid? invitationId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            Guid targetLookupId = targetMembershipId ?? Guid.Empty;

            // Step 1: Lock the workspace row first so all competing workspace mutations serialize here.
            List<Organization> organizations = await dbContext.Organizations
                .FromSqlInterpolated($"""SELECT * FROM shared."Organizations" WHERE "Id" = {organizationId} FOR UPDATE""")
                .AsTracking()
                .ToListAsync(cancellationToken);

            if (organizations.Count != 1 || !string.Equals(organizations[0].Status, "active", StringComparison.Ordinal))
            {
                throw OrganizationMutationScope.CreateAccessDenied();
            }

            Organization organization = organizations[0];

            // Resolve the immutable sponsor only after workspace serialization, before any account locks.
            Guid sponsorUserId = invitationId.HasValue
                ? await dbContext.OrganizationInvitations.AsNoTracking()
                    .Where(invitation => invitation.OrganizationId == organizationId && invitation.Id == invitationId.Value)
                    .Select(invitation => invitation.IssuedByUserId).SingleOrDefaultAsync(cancellationToken)
                : Guid.Empty;

            // Step 2: Lock active account eligibility rows in ascending Id order so competing account-state changes cannot invalidate the decision before commit.
            List<User> lockedUsers = await dbContext.Users
                .FromSqlInterpolated($"""
                    SELECT * FROM shared."Users"
                    WHERE "Id" = {actorUserId} OR "Id" = {sponsorUserId}
                       OR "Id" IN (
                           SELECT m."UserId"
                           FROM shared."OrganizationMemberships" AS m
                           WHERE m."OrganizationId" = {organizationId}
                             AND (
                                 m."Id" = {targetLookupId}
                                 OR (
                                     m."Status" = 'active'
                                     AND EXISTS (
                                         SELECT 1
                                         FROM shared."OrganizationRoleAssignments" AS a
                                         INNER JOIN shared."OrganizationRoles" AS r
                                             ON r."OrganizationId" = a."OrganizationId" AND r."Id" = a."RoleId"
                                         WHERE a."OrganizationId" = m."OrganizationId"
                                             AND a."MembershipId" = m."Id"
                                             AND a."RevokedAt" IS NULL
                                             AND (a."ExpiresAt" IS NULL OR a."ExpiresAt" > {now})
                                             AND r."Origin" = 'system'
                                             AND r."Code" = 'owner'
                                             AND r."RetiredAt" IS NULL
                                     )
                                 )
                             )
                       )
                    ORDER BY "Id"
                    FOR UPDATE
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            if (sessionId.HasValue)
            {
                List<AuthSession> sessions = await dbContext.AuthSessions
                    .FromSqlInterpolated($"""SELECT * FROM shared."AuthSessions" WHERE "Id" = {sessionId.Value} FOR UPDATE""")
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                if (sessions.Count != 1 || sessions[0].UserId != actorUserId || sessions[0].RevokedAt is not null)
                {
                    throw OrganizationMutationScope.CreateAccessDenied();
                }
            }

            Dictionary<Guid, User> usersById = lockedUsers.ToDictionary(u => u.Id);
            if (!usersById.TryGetValue(actorUserId, out User? actorUser)
                || !string.Equals(actorUser.Status, AuthSessionService.ActiveUserStatus, StringComparison.Ordinal))
            {
                throw OrganizationMutationScope.CreateAccessDenied();
            }

            // Step 4: Lock workspace memberships in ascending Id order.
            List<OrganizationMembership> lockedMemberships = await dbContext.OrganizationMemberships
                .FromSqlInterpolated($"""
                    SELECT * FROM shared."OrganizationMemberships"
                    WHERE "OrganizationId" = {organizationId}
                      AND ("UserId" = {actorUserId} OR "UserId" = {sponsorUserId} OR "Id" = {targetLookupId} OR "Status" = 'active')
                    ORDER BY "Id"
                    FOR UPDATE
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            OrganizationMembership? actorMembership = lockedMemberships
                .SingleOrDefault(m => m.UserId == actorUserId && string.Equals(m.Status, "active", StringComparison.Ordinal));
            if (actorMembership is null)
            {
                throw OrganizationMutationScope.CreateAccessDenied();
            }

            // Load current workspace roles, permissions, and assignments after acquiring locks.
            List<OrganizationRole> roles = await dbContext.OrganizationRoles
                .AsTracking()
                .Where(r => r.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

            List<OrganizationRolePermission> rolePermissions = await dbContext.OrganizationRolePermissions
                .AsTracking()
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

            List<OrganizationRoleAssignment> assignments = await dbContext.OrganizationRoleAssignments
                .AsTracking()
                .Where(a => a.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

            OrganizationMemberSecurityContext actorContext = BuildContextFromLoadedState(
                organization,
                actorMembership,
                actorUser.Status,
                roles,
                rolePermissions,
                assignments,
                now);

            OrganizationMembership? targetMembership = null;
            OrganizationMemberSecurityContext? targetContext = null;
            if (targetMembershipId.HasValue && targetMembershipId.Value != Guid.Empty)
            {
                targetMembership = lockedMemberships.SingleOrDefault(m => m.Id == targetMembershipId.Value);
                if (targetMembership is not null)
                {
                    string targetUserStatus = usersById.TryGetValue(targetMembership.UserId, out User? targetUser)
                        ? targetUser.Status
                        : "unknown";
                    targetContext = BuildContextFromLoadedState(
                        organization,
                        targetMembership,
                        targetUserStatus,
                        roles,
                        rolePermissions,
                        assignments,
                        now);
                }
            }

            int activeEligibleOwnerCount = CountActiveEligibleOwners(
                lockedMemberships,
                usersById,
                roles,
                assignments,
                now);

            OrganizationMutationScope? sponsor = null;
            OrganizationMembership? sponsorMembership = lockedMemberships.SingleOrDefault(membership => membership.UserId == sponsorUserId);
            if (sponsorMembership is not null && usersById.TryGetValue(sponsorUserId, out User? sponsorUser))
            {
                OrganizationMemberSecurityContext sponsorContext = BuildContextFromLoadedState(
                    organization, sponsorMembership, sponsorUser.Status, roles, rolePermissions, assignments, now);
                sponsor = new OrganizationMutationScope(organization, sponsorMembership, sponsorContext, activeEligibleOwnerCount);
            }

            return new OrganizationMutationScope(
                organization,
                actorMembership,
                actorContext,
                activeEligibleOwnerCount,
                targetMembership,
                targetContext,
                sponsor);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    internal static OrganizationMemberSecurityContext BuildContextFromLoadedState(
        Organization organization,
        OrganizationMembership membership,
        string userStatus,
        IReadOnlyCollection<OrganizationRole> roles,
        IReadOnlyCollection<OrganizationRolePermission> rolePermissions,
        IReadOnlyCollection<OrganizationRoleAssignment> assignments,
        DateTimeOffset now)
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

        HashSet<Guid> activeRoleIds = assignments
            .Where(a => a.MembershipId == membership.Id
                && a.RevokedAt is null
                && (a.ExpiresAt is null || a.ExpiresAt > now))
            .Select(a => a.RoleId)
            .ToHashSet();

        List<OrganizationRole> activeRoles = roles
            .Where(r => activeRoleIds.Contains(r.Id) && r.RetiredAt is null)
            .ToList();

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
            authority.EffectiveRoles.Select(OrganizationAuthorizationService.MapRoleBrief).ToList(),
            authority.EffectivePermissions);
    }

    internal static int CountActiveEligibleOwners(
        IReadOnlyCollection<OrganizationMembership> memberships,
        IReadOnlyDictionary<Guid, User> usersById,
        IReadOnlyCollection<OrganizationRole> roles,
        IReadOnlyCollection<OrganizationRoleAssignment> assignments,
        DateTimeOffset now)
    {
        HashSet<Guid> activeSystemOwnerRoleIds = roles
            .Where(r => r.RetiredAt is null && OrganizationCapabilityCatalog.IsSystemOwnerRole(r))
            .Select(r => r.Id)
            .ToHashSet();

        if (activeSystemOwnerRoleIds.Count == 0)
        {
            return 0;
        }

        HashSet<Guid> membershipsWithOwnerGrant = assignments
            .Where(a => activeSystemOwnerRoleIds.Contains(a.RoleId)
                && a.RevokedAt is null
                && (a.ExpiresAt is null || a.ExpiresAt > now))
            .Select(a => a.MembershipId)
            .ToHashSet();

        int count = 0;
        foreach (OrganizationMembership membership in memberships)
        {
            if (string.Equals(membership.Status, "active", StringComparison.Ordinal)
                && membershipsWithOwnerGrant.Contains(membership.Id)
                && usersById.TryGetValue(membership.UserId, out User? user)
                && string.Equals(user.Status, AuthSessionService.ActiveUserStatus, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private (Guid UserId, Guid? SessionId) ResolveCurrentPrincipal()
    {
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

    private void EnsureCommandTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Workspace security mutations require the pipeline-owned command transaction.");
        }
    }
}
