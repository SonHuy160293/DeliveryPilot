using System.Data;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class InvitationRecipientStore(
    IdentityDbContext dbContext,
    TimeProvider clock,
    ICurrentUserProvider? currentUserProvider = null) : IInvitationRecipientStore
{
    public async Task<InvitationRecipientContext?> ReadAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            // One advisory snapshot; a command must acquire and recheck all facts independently.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            InvitationRecipientContext? context = await LoadAsync(invitationId, false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return context;
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public async Task<InvitationRecipientContext?> AcquireAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Invitation responses require the pipeline-owned command transaction.");
        }

        try
        {
            return await LoadAsync(invitationId, true, cancellationToken);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public void AddMembership(OrganizationMembership membership) => dbContext.OrganizationMemberships.Add(membership);
    public void AddAssignment(OrganizationRoleAssignment assignment) => dbContext.OrganizationRoleAssignments.Add(assignment);

    private async Task<InvitationRecipientContext?> LoadAsync(Guid invitationId, bool guarded, CancellationToken cancellationToken)
    {
        CurrentUser? principal = currentUserProvider?.GetCurrentUser();
        if (principal is null || !Guid.TryParse(principal.Id, out Guid recipientId) || recipientId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        Guid? sessionId = null;
        if (!string.IsNullOrWhiteSpace(principal.SessionId))
        {
            if (!Guid.TryParse(principal.SessionId, out Guid parsedSessionId) || parsedSessionId == Guid.Empty)
            {
                return null;
            }

            sessionId = parsedSessionId;
        }

        Guid organizationId = await dbContext.OrganizationInvitations.AsNoTracking()
            .Where(invitation => invitation.Id == invitationId).Select(invitation => invitation.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (organizationId == Guid.Empty)
        {
            return null;
        }

        Organization? organization = guarded
            ? (await dbContext.Organizations.FromSqlInterpolated($"""SELECT * FROM shared."Organizations" WHERE "Id" = {organizationId} FOR UPDATE""")
                .AsTracking().ToListAsync(cancellationToken)).SingleOrDefault()
            : await dbContext.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
        if (organization is null || organization.Status != "active")
        {
            return null;
        }

        OrganizationInvitation? invitation = await dbContext.OrganizationInvitations.AsTracking(guarded ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking)
            .SingleOrDefaultAsync(item => item.Id == invitationId && item.OrganizationId == organizationId, cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        Guid sponsorId = invitation.IssuedByUserId;
        DateTimeOffset now = clock.GetUtcNow();

        // Resolve the sponsor after organization serialization, then lock all authority accounts together.
        FormattableString usersSql = $"""
            SELECT * FROM shared."Users"
            WHERE "Id" = {recipientId} OR "Id" = {sponsorId} OR "Id" IN (
                SELECT m."UserId" FROM shared."OrganizationMemberships" m
                JOIN shared."OrganizationRoleAssignments" a ON a."OrganizationId" = m."OrganizationId" AND a."MembershipId" = m."Id"
                JOIN shared."OrganizationRoles" r ON r."OrganizationId" = a."OrganizationId" AND r."Id" = a."RoleId"
                WHERE m."OrganizationId" = {organizationId} AND m."Status" = 'active'
                    AND a."RevokedAt" IS NULL AND (a."ExpiresAt" IS NULL OR a."ExpiresAt" > {now})
                    AND r."Origin" = 'system' AND r."Code" = 'owner' AND r."RetiredAt" IS NULL)
            ORDER BY "Id" FOR UPDATE
            """;
        List<User> users = guarded
            ? await dbContext.Users.FromSqlInterpolated(usersSql).AsNoTracking().ToListAsync(cancellationToken)
            : await dbContext.Users.AsNoTracking().Where(user => user.Id == recipientId || user.Id == sponsorId ||
                dbContext.OrganizationMemberships.Any(membership => membership.OrganizationId == organizationId && membership.UserId == user.Id && membership.Status == "active"))
                .ToListAsync(cancellationToken);

        if (sessionId.HasValue)
        {
            List<AuthSession> sessions = guarded
                ? await dbContext.AuthSessions.FromSqlInterpolated($"""SELECT * FROM shared."AuthSessions" WHERE "Id" = {sessionId.Value} FOR UPDATE""")
                    .AsNoTracking().ToListAsync(cancellationToken)
                : await dbContext.AuthSessions.AsNoTracking().Where(session => session.Id == sessionId.Value).ToListAsync(cancellationToken);
            if (sessions.Count != 1 || sessions[0].UserId != recipientId || sessions[0].RevokedAt is not null)
            {
                return null;
            }
        }

        User? recipient = users.SingleOrDefault(user => user.Id == recipientId);
        if (recipient is null || recipient.Status != "active")
        {
            return null;
        }

        List<OrganizationMembership> memberships = guarded
            ? await dbContext.OrganizationMemberships.FromSqlInterpolated($"""
                SELECT * FROM shared."OrganizationMemberships" WHERE "OrganizationId" = {organizationId}
                AND ("UserId" = {recipientId} OR "UserId" = {sponsorId} OR "Status" = 'active') ORDER BY "Id" FOR UPDATE
                """).AsTracking().ToListAsync(cancellationToken)
            : await dbContext.OrganizationMemberships.AsNoTracking().Where(item => item.OrganizationId == organizationId).ToListAsync(cancellationToken);
        List<OrganizationRole> roles = await dbContext.OrganizationRoles.AsNoTracking().Where(item => item.OrganizationId == organizationId).ToListAsync(cancellationToken);
        List<OrganizationRolePermission> permissions = await dbContext.OrganizationRolePermissions.AsNoTracking().Where(item => item.OrganizationId == organizationId).ToListAsync(cancellationToken);
        List<OrganizationRoleAssignment> assignments = await dbContext.OrganizationRoleAssignments.AsTracking(guarded ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking)
            .Where(item => item.OrganizationId == organizationId).ToListAsync(cancellationToken);
        List<OrganizationInvitationRoleOffer> offers = await dbContext.OrganizationInvitationRoleOffers.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.InvitationId == invitationId).ToListAsync(cancellationToken);
        bool superseded = await dbContext.OrganizationInvitations.AsNoTracking()
            .AnyAsync(item => item.OrganizationId == organizationId && item.ReplacesInvitationId == invitationId, cancellationToken);
        OrganizationMutationScope? sponsor = null;
        OrganizationMembership? sponsorMembership = memberships.SingleOrDefault(item => item.UserId == sponsorId);
        User? sponsorUser = users.SingleOrDefault(user => user.Id == sponsorId);
        if (sponsorMembership is not null && sponsorUser is not null)
        {
            // Lock waits can cross a grant deadline; evaluate authority after the locks and reads.
            DateTimeOffset authorityTime = clock.GetUtcNow();
            OrganizationMemberSecurityContext authority = OrganizationMutationGuard.BuildContextFromLoadedState(
                organization, sponsorMembership, sponsorUser.Status, roles, permissions, assignments, authorityTime);
            int owners = OrganizationMutationGuard.CountActiveEligibleOwners(memberships, users.ToDictionary(user => user.Id), roles, assignments, authorityTime);
            sponsor = new OrganizationMutationScope(organization, sponsorMembership, authority, owners);
        }

        return new InvitationRecipientContext(
            organization,
            invitation,
            recipient,
            memberships.SingleOrDefault(item => item.UserId == recipientId),
            offers,
            roles,
            permissions,
            assignments,
            sponsor,
            superseded);
    }
}
