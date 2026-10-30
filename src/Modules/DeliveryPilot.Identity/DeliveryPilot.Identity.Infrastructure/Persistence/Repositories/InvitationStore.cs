using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class InvitationStore(IdentityDbContext db) : IInvitationStore
{
    public async Task<InvitationSummary?> FindSummaryAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            OrganizationInvitation? invitation = db.OrganizationInvitations.Local.SingleOrDefault(x => x.OrganizationId == organizationId && x.Id == invitationId)
                ?? await db.OrganizationInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == invitationId, cancellationToken);
            if (invitation is null)
            {
                return null;
            }

            IReadOnlyList<OrganizationInvitationRoleOffer> offers = await GetOffersAsync(organizationId, invitationId, cancellationToken);
            OrganizationInvitationDelivery? delivery = db.OrganizationInvitationDeliveries.Local.SingleOrDefault(x => x.OrganizationId == organizationId && x.InvitationId == invitationId && x.DeliveryMessageId == invitation.CurrentDeliveryMessageId)
                ?? await db.OrganizationInvitationDeliveries.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.InvitationId == invitationId && x.DeliveryMessageId == invitation.CurrentDeliveryMessageId, cancellationToken);
            return Summarize(invitation, offers, delivery);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public async Task<OrganizationReadPage<InvitationSummary>> ListAsync(Guid organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken)
    {
        (DateTimeOffset Time, Guid Id)? key = OrganizationReadStore.ParsePage(limit, cursor);
        try
        {
            IQueryable<OrganizationInvitation> query = db.OrganizationInvitations.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Status == status);
            if (key is { } after)
            {
                query = query.Where(x => x.CreatedAt > after.Time || (x.CreatedAt == after.Time && x.Id.CompareTo(after.Id) > 0));
            }

            List<OrganizationInvitation> rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
            OrganizationInvitation[] page = rows.Take(limit).ToArray();
            Guid[] ids = page.Select(x => x.Id).ToArray();
            Guid[] messageIds = page.Where(x => x.CurrentDeliveryMessageId.HasValue).Select(x => x.CurrentDeliveryMessageId!.Value).ToArray();
            List<OrganizationInvitationRoleOffer> offers = await db.OrganizationInvitationRoleOffers.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && ids.Contains(x.InvitationId)).OrderBy(x => x.RoleId).ToListAsync(cancellationToken);
            List<OrganizationInvitationDelivery> deliveries = await db.OrganizationInvitationDeliveries.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && ids.Contains(x.InvitationId) && messageIds.Contains(x.DeliveryMessageId)).ToListAsync(cancellationToken);
            InvitationSummary[] items = page.Select(invitation => Summarize(
                invitation,
                offers.Where(x => x.InvitationId == invitation.Id).ToArray(),
                deliveries.SingleOrDefault(x => x.InvitationId == invitation.Id && x.DeliveryMessageId == invitation.CurrentDeliveryMessageId))).ToArray();
            return new OrganizationReadPage<InvitationSummary>(items, rows.Count > limit ? OrganizationReadStore.Encode(page[^1].CreatedAt, page[^1].Id) : null);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    private static InvitationSummary Summarize(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery? delivery)
    {
        if (delivery is null || offers.Count == 0)
        {
            throw new InvalidOperationException("Invitation projection is incomplete.");
        }

        return new InvitationSummary(
            invitation.Id,
            invitation.OrganizationId,
            invitation.RecipientEmail,
            invitation.Status,
            offers.Select(offer => new InvitationRoleSummary(offer.RoleId, offer.OfferedRoleName, offer.OfferedPermissionSetVersion)).ToArray(),
            invitation.ExpiresAt,
            invitation.Version,
            new InvitationDeliverySummary(delivery.Status, delivery.FailureCode, delivery.UpdatedAt));
    }

    public Task<OrganizationInvitation?> FindAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
        => db.OrganizationInvitations.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == invitationId, cancellationToken);

    public async Task<OrganizationInvitation?> FindPendingAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken)
        => db.OrganizationInvitations.Local.SingleOrDefault(x => x.OrganizationId == organizationId && x.RecipientEmail == normalizedEmail && x.Status == "pending")
            ?? await db.OrganizationInvitations.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.RecipientEmail == normalizedEmail && x.Status == "pending", cancellationToken);

    public async Task<IReadOnlyList<OrganizationInvitationRoleOffer>> GetOffersAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        List<OrganizationInvitationRoleOffer> saved = await db.OrganizationInvitationRoleOffers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.InvitationId == invitationId).ToListAsync(cancellationToken);
        return saved.Concat(db.OrganizationInvitationRoleOffers.Local.Where(x => x.OrganizationId == organizationId && x.InvitationId == invitationId))
            .DistinctBy(x => x.RoleId).OrderBy(x => x.RoleId).ToArray();
    }

    public async Task<IReadOnlyList<OrganizationRole>> GetRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        => await db.OrganizationRoles.AsNoTracking().Where(x => x.OrganizationId == organizationId && roleIds.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrganizationRolePermission>> GetPermissionsAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        => await db.OrganizationRolePermissions.AsNoTracking().Where(x => x.OrganizationId == organizationId && roleIds.Contains(x.RoleId)).ToListAsync(cancellationToken);

    public Task<string?> GetRecipientMembershipStatusAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken)
        => (from membership in db.OrganizationMemberships.AsNoTracking()
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId && user.PrimaryEmail == normalizedEmail
            select membership.Status).SingleOrDefaultAsync(cancellationToken);

    public async Task CloseForReplacementAsync(OrganizationInvitation invitation, string status, Guid? actorId, DateTimeOffset now, string? reason, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Invitation transitions require a command transaction.");
        }

        long oldVersion = invitation.Version;
        invitation.Close(status, actorId, now, reason);
        int updated = await db.OrganizationInvitations.Where(x => x.OrganizationId == invitation.OrganizationId && x.Id == invitation.Id && x.Version == oldVersion && x.Status == "pending")
            .ExecuteUpdateAsync(
                x => x.SetProperty(i => i.Status, invitation.Status)
                .SetProperty(i => i.ClosedAt, invitation.ClosedAt)
                .SetProperty(i => i.ClosedByUserId, invitation.ClosedByUserId)
                .SetProperty(i => i.CloseReason, invitation.CloseReason)
                .SetProperty(i => i.UpdatedAt, invitation.UpdatedAt)
                .SetProperty(i => i.Version, invitation.Version),
                cancellationToken);
        if (updated != 1)
        {
            throw new DbUpdateConcurrencyException("Invitation changed during transition.");
        }

        await db.Entry(invitation).ReloadAsync(cancellationToken);
    }

    public void Add(OrganizationInvitation invitation, IReadOnlyCollection<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery)
    {
        db.OrganizationInvitations.Add(invitation);
        db.OrganizationInvitationRoleOffers.AddRange(offers);
        db.OrganizationInvitationDeliveries.Add(delivery);
    }

    public void AddDelivery(OrganizationInvitationDelivery delivery) => db.OrganizationInvitationDeliveries.Add(delivery);
}
