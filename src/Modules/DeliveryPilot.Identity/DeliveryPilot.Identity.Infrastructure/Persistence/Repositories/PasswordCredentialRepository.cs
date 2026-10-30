using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class PasswordCredentialRepository(IdentityDbContext dbContext) : IPasswordCredentialRepository
{
    public async Task AddAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await dbContext.PasswordCredentials.AddAsync(credential, cancellationToken);
    }

    public Task<PasswordCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty)
        {
            return Task.FromResult<PasswordCredential?>(null);
        }

        return dbContext.PasswordCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(credential => credential.UserId == userId, cancellationToken);
    }
}
