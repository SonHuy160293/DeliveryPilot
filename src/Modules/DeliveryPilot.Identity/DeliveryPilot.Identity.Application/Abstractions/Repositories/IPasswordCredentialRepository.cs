using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IPasswordCredentialRepository
{
    Task AddAsync(PasswordCredential credential, CancellationToken cancellationToken);

    Task<PasswordCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
