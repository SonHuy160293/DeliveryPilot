using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public sealed class InvitationIssuanceGovernor(IdentityDbContext db, IOptions<InvitationIssuanceOptions> options, TimeProvider clock) : IInvitationIssuanceGovernor
{
    public async Task CheckAsync(OrganizationMutationScope scope, string recipientEmail, string requiredCapability, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Issuance limits require the guarded command transaction.");
        }

        Guid organizationId = scope.Organization.Id;
        scope.EnsureCapability(requiredCapability);
        scope.EnsurePrivilegedChangeAllowed();
        string email = User.NormalizeEmail(recipientEmail);
        InvitationIssuanceLimits limits = options.Value.ToLimits();
        DateTimeOffset now = clock.GetUtcNow();
        try
        {
            var pair = from delivery in db.OrganizationInvitationDeliveries.AsNoTracking()
                       join invitation in db.OrganizationInvitations.AsNoTracking() on new { delivery.OrganizationId, Id = delivery.InvitationId } equals new { invitation.OrganizationId, invitation.Id }
                       where delivery.OrganizationId == organizationId && invitation.RecipientEmail == email
                       select delivery;
            TimeSpan pairWindow = limits.PairCooldown > TimeSpan.FromDays(1) ? limits.PairCooldown : TimeSpan.FromDays(1);
            List<DateTimeOffset> pairTimes = await pair.Where(x => x.IssuedAt > now - pairWindow && x.IssuedAt <= now).Select(x => x.IssuedAt).ToListAsync(cancellationToken);
            List<DateTimeOffset> workspaceTimes = await db.OrganizationInvitationDeliveries.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.IssuedAt > now.AddHours(-1) && x.IssuedAt <= now).Select(x => x.IssuedAt).ToListAsync(cancellationToken);
            var stagedInvitations = db.ChangeTracker.Entries<OrganizationInvitation>()
                .Where(x => x.State == EntityState.Added && x.Entity.OrganizationId == organizationId)
                .ToDictionary(x => x.Entity.Id, x => x.Entity.RecipientEmail);
            var staged = db.ChangeTracker.Entries<OrganizationInvitationDelivery>()
                .Where(x => x.State == EntityState.Added && x.Entity.OrganizationId == organizationId)
                .Select(x => x.Entity).ToArray();
            Guid[] existingInvitationIds = staged.Select(x => x.InvitationId).Where(id => !stagedInvitations.ContainsKey(id)).Distinct().ToArray();
            var existingRecipients = await db.OrganizationInvitations.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && existingInvitationIds.Contains(x.Id))
                .Select(x => new { x.Id, x.RecipientEmail }).ToDictionaryAsync(x => x.Id, x => x.RecipientEmail, cancellationToken);
            foreach (OrganizationInvitationDelivery item in staged)
            {
                if (item.IssuedAt > now || item.IssuedAt <= now - pairWindow)
                {
                    continue;
                }

                if (item.IssuedAt > now.AddHours(-1))
                {
                    workspaceTimes.Add(item.IssuedAt);
                }

                string? recipient = stagedInvitations.GetValueOrDefault(item.InvitationId) ?? existingRecipients.GetValueOrDefault(item.InvitationId);
                if (recipient == email)
                {
                    pairTimes.Add(item.IssuedAt);
                }
            }

            int retryAfterSeconds = InvitationRateLimitCalculator.RetryAfterSeconds(now, pairTimes, workspaceTimes, limits);
            if (retryAfterSeconds > 0)
            {
                throw new InvitationRateLimitException(retryAfterSeconds);
            }

        }
        catch (Exception ex) when (OrganizationAuthorizationService.IsStoreUnavailable(ex, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }
}
