using System.Data.Common;
using DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserLookupStore(IdentityDbContext dbContext) : IUserLookupStore
{
    public async Task<IReadOnlyList<UserIntegrationDto>> LookupAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Users.AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .Select(user => new UserIntegrationDto(user.Id, user.DisplayName, user.AvatarUrl, user.Status))
                .ToArrayAsync(cancellationToken);
        }
        catch (DbException)
        {
            throw new ServiceUnavailableException("UserLookup.Unavailable", "User display data is temporarily unavailable.");
        }
        catch (TimeoutException)
        {
            throw new ServiceUnavailableException("UserLookup.Unavailable", "User display data is temporarily unavailable.");
        }
    }
}
