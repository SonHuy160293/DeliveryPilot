using System.Globalization;
using System.Text;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OrganizationReadStore(IdentityDbContext db, TimeProvider clock) : IOrganizationReadStore
{
    public async Task<OrganizationReadPage<OrganizationReadWorkspace>> ListWorkspacesAsync(Guid userId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        (DateTimeOffset Time, Guid Id)? key = ParsePage(limit, cursor);
        try
        {
            var query = from membership in db.OrganizationMemberships.AsNoTracking()
                        join organization in db.Organizations.AsNoTracking() on membership.OrganizationId equals organization.Id
                        where membership.UserId == userId && membership.Status == "active" && organization.Status == "active"
                        select new { Organization = organization, Membership = membership };
            if (key is { } after)
            {
                query = query.Where(x => x.Organization.CreatedAt > after.Time || (x.Organization.CreatedAt == after.Time && x.Organization.Id.CompareTo(after.Id) > 0));
            }

            var rows = await query.OrderBy(x => x.Organization.CreatedAt).ThenBy(x => x.Organization.Id).Take(limit + 1).ToListAsync(cancellationToken);
            bool more = rows.Count > limit;
            var page = rows.Take(limit).ToArray();
            var roles = await LoadRolesAsync(page.Select(x => x.Membership.Id).ToArray(), cancellationToken);
            var items = page.Select(x => new OrganizationReadWorkspace(x.Organization.Id, x.Organization.Name, RolesFor(roles, x.Membership.Id), x.Membership.Id, x.Organization.Version, x.Membership.Version)).ToArray();
            return new(items, more ? Encode(page[^1].Organization.CreatedAt, page[^1].Organization.Id) : null);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationReadMember?> GetMemberAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
    {
        try
        {
            var row = await (from membership in db.OrganizationMemberships.AsNoTracking()
                             join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                             where membership.OrganizationId == organizationId && membership.Id == membershipId
                             select new { Membership = membership, User = user }).SingleOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                return null;
            }

            var roles = await LoadRolesAsync([row.Membership.Id], cancellationToken);
            return MapMember(row.Membership, row.User, roles);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationReadPage<OrganizationReadMember>> ListMembersAsync(Guid organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken)
    {
        (DateTimeOffset Time, Guid Id)? key = ParsePage(limit, cursor);
        if (status != "active" && status != "removed" && status != "suspended")
        {
            throw new BadRequestException("Membership.InvalidStatus", "Invalid membership status.");
        }

        try
        {
            var query = from membership in db.OrganizationMemberships.AsNoTracking()
                        join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                        where membership.OrganizationId == organizationId && membership.Status == status
                        select new { Membership = membership, User = user };
            if (key is { } after)
            {
                query = query.Where(x => x.Membership.CreatedAt > after.Time || (x.Membership.CreatedAt == after.Time && x.Membership.Id.CompareTo(after.Id) > 0));
            }

            var rows = await query.OrderBy(x => x.Membership.CreatedAt).ThenBy(x => x.Membership.Id).Take(limit + 1).ToListAsync(cancellationToken);
            bool more = rows.Count > limit;
            var page = rows.Take(limit).ToArray();
            var roles = await LoadRolesAsync(page.Select(x => x.Membership.Id).ToArray(), cancellationToken);
            var items = page.Select(x => MapMember(x.Membership, x.User, roles)).ToArray();
            return new(items, more ? Encode(page[^1].Membership.CreatedAt, page[^1].Membership.Id) : null);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    private async Task<Dictionary<Guid, List<OrganizationReadRole>>> LoadRolesAsync(Guid[] membershipIds, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        var rows = await (from assignment in db.OrganizationRoleAssignments.AsNoTracking()
                          join role in db.OrganizationRoles.AsNoTracking() on new { assignment.OrganizationId, Id = assignment.RoleId } equals new { role.OrganizationId, role.Id }
                          where membershipIds.Contains(assignment.MembershipId) && assignment.RevokedAt == null
                              && (assignment.ExpiresAt == null || assignment.ExpiresAt > now) && role.RetiredAt == null
                          orderby role.CreatedAt, role.Id
                          select new { assignment.MembershipId, role.Id, role.Code, role.Name }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.MembershipId).ToDictionary(g => g.Key, g => g.Select(x => new OrganizationReadRole(x.Id, x.Code, x.Name)).ToList());
    }

    private static IReadOnlyList<OrganizationReadRole> RolesFor(Dictionary<Guid, List<OrganizationReadRole>> roles, Guid id)
        => roles.TryGetValue(id, out List<OrganizationReadRole>? value) ? value : [];

    private static OrganizationReadMember MapMember(OrganizationMembership membership, User user, Dictionary<Guid, List<OrganizationReadRole>> roles)
        => new(membership.Id, user.Id, user.DisplayName, user.PrimaryEmail, membership.Status,
            membership.Status == "active" && user.Status == "active" ? RolesFor(roles, membership.Id) : [], membership.CreatedAt, membership.Version);

    internal static (DateTimeOffset Time, Guid Id)? ParsePage(int limit, string? cursor)
    {
        if (limit is < 1 or > 100 || cursor is { Length: > 1024 } || cursor == string.Empty)
        {
            throw new BadRequestException("Pagination.Invalid", "Invalid pagination parameters.");
        }

        if (cursor is null)
        {
            return null;
        }

        try
        {
            string base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            string[] parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) || !Guid.TryParseExact(parts[1], "N", out Guid id) || id == Guid.Empty)
            {
                throw new FormatException();
            }

            return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            throw new BadRequestException("Pagination.Invalid", "Invalid pagination parameters.");
        }
    }

    internal static string Encode(DateTimeOffset time, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{time.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id:N}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
