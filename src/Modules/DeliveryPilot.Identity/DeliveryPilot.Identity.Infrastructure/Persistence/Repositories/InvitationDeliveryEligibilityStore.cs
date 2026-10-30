using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class InvitationDeliveryEligibilityStore(IdentityDbContext db, TimeProvider clock) : IInvitationDeliveryEligibilityStore
{
    public async Task<InvitationDeliveryEligibility> GetAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset now = clock.GetUtcNow();
            DateTimeOffset? expiry = await (from delivery in db.OrganizationInvitationDeliveries.AsNoTracking()
                join invitation in db.OrganizationInvitations.AsNoTracking() on new { delivery.OrganizationId, Id = delivery.InvitationId } equals new { invitation.OrganizationId, invitation.Id }
                join organization in db.Organizations.AsNoTracking() on invitation.OrganizationId equals organization.Id
                where delivery.DeliveryMessageId == deliveryId && invitation.CurrentDeliveryMessageId == deliveryId
                    && invitation.Status == "pending" && organization.Status == "active" && invitation.ExpiresAt > now
                    && delivery.ExpiresAt == invitation.ExpiresAt
                select (DateTimeOffset?)invitation.ExpiresAt).SingleOrDefaultAsync(cancellationToken);
            return expiry is null ? new(false, null) : new(true, expiry);
        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }
}
