using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext dbContext) : RepositoryBase<User, Guid>(dbContext), IUserRepository
{
    public async Task<bool> ExistsForRegistrationAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Registration requires the pipeline-owned transaction.");
        }

        string email = User.NormalizeEmail(normalizedEmail);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(lower({email}), 0))",
            cancellationToken);

        return await dbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM shared.\"Users\" WHERE lower(\"PrimaryEmail\") = lower({email})")
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public async Task<User?> FindByEmailForLoginAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string email = User.NormalizeEmail(normalizedEmail);
        List<User> matches = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM shared.\"Users\" WHERE lower(\"PrimaryEmail\") = lower({email})")
            .AsTracking()
            .ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }
}
