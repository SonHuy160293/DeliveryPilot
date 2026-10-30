using DeliveryPilot.Identity.Application.Abstractions.Persistence;
using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IUserRepository : IRepositoryBase<User>
{
    Task<bool> ExistsForRegistrationAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByEmailForLoginAsync(string normalizedEmail, CancellationToken cancellationToken);
}
