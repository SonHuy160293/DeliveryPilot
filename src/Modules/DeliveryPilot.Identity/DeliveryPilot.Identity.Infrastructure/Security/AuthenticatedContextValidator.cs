using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// Uncached per-request check that the token's session exists, belongs to the subject, is not
/// revoked and that the user is still active. Database failures propagate so the bearer handler
/// rejects the request (fail closed). Refresh-deadline expiry does not by itself end access.
/// </summary>
public sealed class AuthenticatedContextValidator(IdentityDbContext dbContext) : IAuthenticatedContextValidator
{
    public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return dbContext.AuthSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId
                    && session.UserId == userId
                    && session.RevokedAt == null
                    && dbContext.Users.Any(user => user.Id == session.UserId && user.Status == AuthSessionService.ActiveUserStatus),
                cancellationToken);
    }
}
