using System.Data;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OwnershipNominationStore(IdentityDbContext db, IOrganizationAuthorization authorization, TimeProvider clock) : IOwnershipNominationStore
{
    public Task<OrganizationOwnershipNomination?> FindAsync(Guid organizationId, Guid nominationId, CancellationToken cancellationToken)
        => ReadEntityAsync(organizationId, nominationId, null, cancellationToken);

    public Task<OrganizationOwnershipNomination?> FindPendingAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
        => ReadEntityAsync(organizationId, null, membershipId, cancellationToken);

    public async Task CloseExpiredAsync(OrganizationOwnershipNomination nomination, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Nomination replacement requires the pipeline transaction.");
        }

        long oldVersion = nomination.Version;
        nomination.Close("expired", null, now);
        int updated = await db.OrganizationOwnershipNominations.Where(
            item => item.OrganizationId == nomination.OrganizationId && item.Id == nomination.Id && item.Status == "pending" && item.Version == oldVersion)
            .ExecuteUpdateAsync(
                update => update.SetProperty(item => item.Status, nomination.Status)
                .SetProperty(item => item.ClosedAt, nomination.ClosedAt).SetProperty(item => item.UpdatedAt, nomination.UpdatedAt)
                .SetProperty(item => item.Version, nomination.Version), cancellationToken);
        if (updated != 1)
        {
            throw new ConflictException("OwnershipNomination.Stale", "The nomination has changed; refresh before responding.");
        }

        await db.Entry(nomination).ReloadAsync(cancellationToken);
    }

    public void Add(OrganizationOwnershipNomination nomination) => db.OrganizationOwnershipNominations.Add(nomination);

    public async Task<OrganizationReadPage<NominationSummary>> ReadPageAsync(Guid? organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken)
    {
        (DateTimeOffset Time, Guid Id)? key = OrganizationReadStore.ParsePage(limit, cursor);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            Guid userId = await authorization.GetAuthenticatedUserIdAsync(cancellationToken);
            if (organizationId.HasValue)
            {
                OrganizationMemberSecurityContext caller = await authorization.AuthorizeMemberScopeAsync(organizationId.Value, cancellationToken);
                if (!caller.IsSystemOwner)
                {
                    throw OrganizationMutationScope.CreateAccessDenied();
                }
            }

            var query = from nomination in db.OrganizationOwnershipNominations.AsNoTracking()
                        join membership in db.OrganizationMemberships.AsNoTracking() on new { nomination.OrganizationId, Id = nomination.MembershipId } equals new { membership.OrganizationId, membership.Id }
                        join organization in db.Organizations.AsNoTracking() on nomination.OrganizationId equals organization.Id
                        where nomination.Status == status && organization.Status == "active" &&
                            (organizationId.HasValue ? nomination.OrganizationId == organizationId.Value : membership.UserId == userId && membership.Status == "active")
                        select nomination;
            if (key is { } after)
            {
                query = query.Where(item => item.CreatedAt > after.Time || (item.CreatedAt == after.Time && item.Id.CompareTo(after.Id) > 0));
            }

            List<OrganizationOwnershipNomination> rows = await query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Take(limit + 1).ToListAsync(cancellationToken);
            bool more = rows.Count > limit;
            OrganizationOwnershipNomination[] page = rows.Take(limit).ToArray();
            Guid[] organizationIds = page.Select(item => item.OrganizationId).Distinct().ToArray();
            List<OrganizationMembership> memberships = await db.OrganizationMemberships.AsNoTracking().Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync(cancellationToken);
            Guid[] userIds = memberships.Select(item => item.UserId).Concat(page.Select(item => item.NominatedByUserId)).Distinct().ToArray();
            List<User> users = await db.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToListAsync(cancellationToken);
            List<OrganizationRole> roles = await db.OrganizationRoles.AsNoTracking().Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync(cancellationToken);
            List<OrganizationRoleAssignment> assignments = await db.OrganizationRoleAssignments.AsNoTracking().Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync(cancellationToken);
            DateTimeOffset now = clock.GetUtcNow();
            bool Eligible(OrganizationMembership member) => member.Status == "active" && users.Any(user => user.Id == member.UserId && user.Status == "active");
            bool Owner(OrganizationMembership member) => Eligible(member) && assignments.Any(assignment => assignment.OrganizationId == member.OrganizationId &&
                assignment.MembershipId == member.Id && assignment.RevokedAt is null && (assignment.ExpiresAt is null || assignment.ExpiresAt > now) &&
                roles.Any(role => role.OrganizationId == member.OrganizationId && role.Id == assignment.RoleId && role.RetiredAt is null && OrganizationCapabilityCatalog.IsSystemOwnerRole(role)));
            bool CanAccept(OrganizationOwnershipNomination item)
            {
                OrganizationMembership target = memberships.Single(member => member.OrganizationId == item.OrganizationId && member.Id == item.MembershipId);
                return item.Status == "pending" && item.ExpiresAt > now && target.UserId == userId && Eligible(target) && !Owner(target) &&
                    memberships.Any(member => member.OrganizationId == item.OrganizationId && member.UserId == item.NominatedByUserId && Owner(member));
            }

            NominationSummary[] summaries = page.Select(item => OwnershipNominationService.Summary(item, CanAccept(item))).ToArray();
            await transaction.CommitAsync(cancellationToken);
            return new OrganizationReadPage<NominationSummary>(summaries, more ? OrganizationReadStore.Encode(page[^1].CreatedAt, page[^1].Id) : null);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    private async Task<OrganizationOwnershipNomination?> ReadEntityAsync(Guid organizationId, Guid? nominationId, Guid? membershipId, CancellationToken cancellationToken)
    {
        try
        {
            return await db.OrganizationOwnershipNominations.AsTracking().SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId &&
                    (nominationId.HasValue ? item.Id == nominationId.Value : item.MembershipId == membershipId && item.Status == "pending"), cancellationToken);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }
}
