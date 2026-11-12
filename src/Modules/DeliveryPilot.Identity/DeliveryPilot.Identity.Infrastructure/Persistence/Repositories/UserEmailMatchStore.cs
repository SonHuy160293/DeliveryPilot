using System.Data.Common;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserEmailMatchStore(IdentityDbContext dbContext) : IUserEmailMatchStore
{
    public async Task<bool> IsActiveUserWithPrimaryEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.Status == AuthSessionService.ActiveUserStatus && user.PrimaryEmail == normalizedEmail, cancellationToken);
        }
        catch (DbException)
        {
            throw new ServiceUnavailableException("UserEmailMatch.Unavailable", "User email matching is temporarily unavailable.");
        }
        catch (TimeoutException)
        {
            throw new ServiceUnavailableException("UserEmailMatch.Unavailable", "User email matching is temporarily unavailable.");
        }
        catch (InvalidOperationException ex) when (ex.InnerException is DbException or TimeoutException)
        {
            throw new ServiceUnavailableException("UserEmailMatch.Unavailable", "User email matching is temporarily unavailable.");
        }
    }
}
