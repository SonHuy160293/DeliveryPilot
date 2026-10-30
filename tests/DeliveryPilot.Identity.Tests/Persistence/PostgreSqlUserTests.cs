using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")))
        {
            Skip = "Set DELIVERYPILOT_TEST_POSTGRES to a provisioned isolated PostgreSQL 18 database.";
        }
    }
}

public sealed class PostgreSqlUserTests
{
    private static IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options);

    [PostgreSqlFact]
    public async Task FailedResultAndExceptionRollBackWithoutPersistingStagedUser()
    {
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        await using var context = CreateContext();
        var unitOfWork = new IdentityUnitOfWork(context);
        Result response = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await context.Users.AddAsync(User.Create(email, "Dependency test"), ct);
            return Result.Failure(new Error("Test.Failure", "Expected test failure."));
        });
        Assert.True(response.IsFailure);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == email));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
        {
            await context.Users.AddAsync(User.Create(email, "Dependency test"), ct);
            throw new InvalidOperationException("Expected test exception.");
        }));
        Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == email));
    }

    [PostgreSqlFact]
    public async Task CaseInsensitiveEmailUniquenessIsEnforced()
    {
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        await using var context = CreateContext();
        try
        {
            context.Users.Add(User.Create(email, "Original"));
            await context.SaveChangesAsync();
            context.Users.Add(User.Create(email.ToUpperInvariant(), "Duplicate"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.Users.Where(user => user.PrimaryEmail == email).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RequiredUniqueEmailIndexExists()
    {
        await using var context = CreateContext();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
              SELECT 1 FROM pg_index i
              JOIN pg_class t ON t.oid = i.indrelid
              JOIN pg_namespace n ON n.oid = t.relnamespace
              WHERE n.nspname = 'shared' AND t.relname = 'Users'
                AND i.indisunique AND i.indisvalid AND i.indpred IS NULL
                AND i.indnkeyatts = 1
                AND pg_get_expr(i.indexprs, i.indrelid) IN
                  ('lower(("PrimaryEmail")::text)', 'lower("PrimaryEmail")')
            )
            """;
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }
}
