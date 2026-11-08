using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OrganizationRoleStore(IdentityDbContext db) : IOrganizationRoleStore
{
    public void Add(OrganizationRole role, IReadOnlyCollection<OrganizationRolePermission> permissions)
    {
        db.OrganizationRoles.Add(role);
        db.OrganizationRolePermissions.AddRange(permissions);
    }

    public async Task<OrganizationReadPage<OrganizationRoleRead>> ListAsync(Guid organizationId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        (DateTimeOffset Time, Guid Id)? key = OrganizationReadStore.ParsePage(limit, cursor);
        try
        {
            IQueryable<OrganizationRole> query = db.OrganizationRoles.AsNoTracking().Where(role => role.OrganizationId == organizationId);
            if (key is { } after)
            {
                query = query.Where(role => role.CreatedAt > after.Time || (role.CreatedAt == after.Time && role.Id.CompareTo(after.Id) > 0));
            }

            List<OrganizationRole> rows = await query.OrderBy(role => role.CreatedAt).ThenBy(role => role.Id).Take(limit + 1).ToListAsync(cancellationToken);
            bool more = rows.Count > limit;
            OrganizationRole[] page = rows.Take(limit).ToArray();
            Guid[] ids = page.Select(role => role.Id).ToArray();
            List<OrganizationRolePermission> permissions = await db.OrganizationRolePermissions.AsNoTracking()
                .Where(permission => permission.OrganizationId == organizationId && ids.Contains(permission.RoleId)).ToListAsync(cancellationToken);
            OrganizationRoleRead[] result = page.Select(role => new OrganizationRoleRead(role, permissions.Where(permission => permission.RoleId == role.Id).ToArray())).ToArray();
            return new OrganizationReadPage<OrganizationRoleRead>(result, more ? OrganizationReadStore.Encode(page[^1].CreatedAt, page[^1].Id) : null);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationRoleRead?> FindForUpdateAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            OrganizationRole? role = await db.OrganizationRoles.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == roleId, cancellationToken);
            if (role is null)
            {
                return null;
            }

            List<OrganizationRolePermission> permissions = await db.OrganizationRolePermissions
                .Where(x => x.OrganizationId == organizationId && x.RoleId == roleId).ToListAsync(cancellationToken);
            return new OrganizationRoleRead(role, permissions);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public void UpdatePermissions(IReadOnlyCollection<OrganizationRolePermission> removed, IReadOnlyCollection<OrganizationRolePermission> added)
    {
        db.OrganizationRolePermissions.RemoveRange(removed);
        db.OrganizationRolePermissions.AddRange(added);
    }

    public async Task<OrganizationRoleAssignment?> FindAssignmentAsync(Guid organizationId, Guid membershipId, Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            return await db.OrganizationRoleAssignments.SingleOrDefaultAsync(assignment => assignment.OrganizationId == organizationId && assignment.MembershipId == membershipId && assignment.RoleId == roleId, cancellationToken);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public void AddAssignment(OrganizationRoleAssignment assignment) => db.OrganizationRoleAssignments.Add(assignment);

    public async Task<bool> HasRetirementReferencesAsync(Guid organizationId, Guid roleId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            return await db.OrganizationRoleAssignments.AsNoTracking().AnyAsync(
                item => item.OrganizationId == organizationId && item.RoleId == roleId && item.RevokedAt == null && (item.ExpiresAt == null || item.ExpiresAt > now), cancellationToken)
                || await (from offer in db.OrganizationInvitationRoleOffers.AsNoTracking()
                          join invitation in db.OrganizationInvitations.AsNoTracking() on offer.InvitationId equals invitation.Id
                          where offer.OrganizationId == organizationId && invitation.OrganizationId == organizationId && offer.RoleId == roleId && invitation.Status == "pending"
                          select offer).AnyAsync(cancellationToken);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }
}
