using System.Data;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class RegistrationPersistenceTests
{
    private const string ProtectedVerifier = "test-version-1$opaque-protected-verifier";

    [PostgreSqlFact]
    public async Task ConcurrentEquivalentRegistrationsCommitOneUnchangedAccount()
    {
        string email = NewEmail();
        try
        {
            var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
            {
                await using var context = Context();
                try
                {
                    string verifier = ProtectedVerifier + index;
                    Guid id = await new IdentityUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                    {
                        Assert.Equal(IsolationLevel.ReadCommitted, context.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
                        var users = new UserRepository(context);
                        string submitted = index % 2 == 0 ? " " + email.ToUpperInvariant() + " " : email;
                        if (await users.ExistsForRegistrationAsync(submitted, ct))
                        {
                            throw new ConflictException("Registration.EmailExists", "This email is exist");
                        }

                        var user = User.Create(submitted, "User " + index, status: "active");
                        await users.AddAsync(user, ct);
                        await new PasswordCredentialRepository(context).AddAsync(
                            PasswordCredential.Create(user.Id, verifier, DateTimeOffset.UtcNow), ct);
                        return user.Id;
                    });
                    return (Id: (Guid?)id, Verifier: verifier, Error: (ConflictException?)null);
                }
                catch (ConflictException error)
                {
                    return (Id: (Guid?)null, Verifier: "", Error: (ConflictException?)error);
                }
            }));

            var winner = Assert.Single(attempts, result => result.Id.HasValue);
            Assert.All(attempts.Where(result => !result.Id.HasValue), result =>
            {
                Assert.Equal("Registration.EmailExists", result.Error!.Code);
                Assert.Equal("This email is exist", result.Error.Message);
            });
            await using var verify = Context();
            var savedUser = await verify.Users.SingleAsync(user => user.PrimaryEmail == email);
            var savedCredential = await verify.PasswordCredentials.SingleAsync(item => item.UserId == savedUser.Id);
            Assert.Equal(winner.Id, savedUser.Id);
            Assert.Equal(winner.Verifier, savedCredential.PasswordHash);
            Assert.Equal("active", savedUser.Status);
            Assert.Null(savedUser.EmailVerifiedAt);
            Assert.Null(savedUser.LastLoginAt);
            Assert.Equal(TimeSpan.Zero, savedCredential.CreatedAt.Offset);
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [PostgreSqlFact]
    public async Task ResultExceptionAndCancellationFailuresRollBackBothRecords()
    {
        string email = NewEmail();
        await using var context = Context();
        var unitOfWork = new IdentityUnitOfWork(context);
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await StageAsync(context, email, ct);
            return Result.Failure(new Error("Test.Failure", "Expected failure."));
        });
        Assert.True(result.IsFailure);
        Assert.Empty(context.ChangeTracker.Entries());

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
        {
            await StageAsync(context, email, ct);
            throw new InvalidOperationException("Expected failure.");
        }));

        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await StageAsync(context, email, ct);
            cancellation.Cancel();
            return Result.Success();
        }, cancellation.Token));

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == email));
    }

    [PostgreSqlFact]
    public async Task EmailCollisionAtCommitBecomesTypedConflictAndPreservesExistingCredential()
    {
        string email = NewEmail();
        try
        {
            await using (var seed = Context())
            {
                await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(ct => StageAsync(seed, email, ct));
            }

            await using var context = Context();
            var error = await Assert.ThrowsAsync<ConflictException>(() =>
                new IdentityUnitOfWork(context).ExecuteInTransactionAsync(ct => StageAsync(context, email, ct)));
            Assert.Equal("Registration.EmailExists", error.Code);
            Assert.Equal("This email is exist", error.Message);
            Assert.Empty(context.ChangeTracker.Entries());
            var savedUser = await context.Users.SingleAsync(user => user.PrimaryEmail == email);
            Assert.Equal(ProtectedVerifier, (await context.PasswordCredentials.SingleAsync(item => item.UserId == savedUser.Id)).PasswordHash);
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [PostgreSqlFact]
    public async Task CredentialCollisionIsNotMisreportedAsEmailConflictAndRollsBackNewUser()
    {
        string originalEmail = NewEmail();
        string newEmail = NewEmail();
        try
        {
            Guid originalId;
            await using (var seed = Context())
            {
                originalId = await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(ct => StageAsync(seed, originalEmail, ct));
            }

            await using var context = Context();
            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                new IdentityUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    await new UserRepository(context).AddAsync(User.Create(newEmail, "Rolled back"), ct);
                    await new PasswordCredentialRepository(context).AddAsync(
                        PasswordCredential.Create(originalId, ProtectedVerifier + "changed", DateTimeOffset.UtcNow), ct);
                    return true;
                }));
            var databaseError = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, databaseError.SqlState);
            Assert.Equal("PkPasswordCredentials", databaseError.ConstraintName);
            Assert.Empty(context.ChangeTracker.Entries());
            Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == newEmail));
            Assert.Equal(ProtectedVerifier, (await context.PasswordCredentials.SingleAsync(item => item.UserId == originalId)).PasswordHash);
        }
        finally
        {
            await CleanupAsync(originalEmail);
            await CleanupAsync(newEmail);
        }
    }

    [PostgreSqlFact]
    public async Task RegistrationLookupRequiresTransactionAndHonorsCancellationWhileWaiting()
    {
        string email = NewEmail();
        await using (var outside = Context())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UserRepository(outside).ExistsForRegistrationAsync(email, default));
        }

        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = HoldAsync();
        try
        {
            await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var blocked = Context();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new IdentityUnitOfWork(blocked).ExecuteInTransactionAsync(
                ct => new UserRepository(blocked).ExistsForRegistrationAsync(email, ct), cancellation.Token));
        }
        finally
        {
            release.TrySetResult(true);
            await holder.WaitAsync(TimeSpan.FromSeconds(10));
        }

        async Task HoldAsync()
        {
            await using var context = Context();
            await new IdentityUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
            {
                await new UserRepository(context).ExistsForRegistrationAsync(email, ct);
                acquired.TrySetResult(true);
                await release.Task;
                return true;
            });
        }
    }

    [PostgreSqlFact]
    public async Task InternationalEmailNormalizationPreservesDatabaseIdentityEquivalence()
    {
        await using var context = Context();
        foreach (string email in new[] { "ÅLICE@example.test", "İPEK@example.test", "ΟΣ@example.test", "STRAẞE@example.test" })
        {
            string originalKey = await context.Database.SqlQuery<string>($"SELECT lower({email}) AS \"Value\"").SingleAsync();
            string normalized = User.NormalizeEmail(email);
            string normalizedKey = await context.Database.SqlQuery<string>($"SELECT lower({normalized}) AS \"Value\"").SingleAsync();
            Assert.Equal(originalKey, normalizedKey);
        }
    }

    private static IdentityDbContext Context() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options);

    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";

    private static async Task<Guid> StageAsync(IdentityDbContext context, string email, CancellationToken cancellationToken)
    {
        var user = User.Create(email, "Registration", status: "active");
        await new UserRepository(context).AddAsync(user, cancellationToken);
        await new PasswordCredentialRepository(context).AddAsync(
            PasswordCredential.Create(user.Id, ProtectedVerifier, DateTimeOffset.UtcNow), cancellationToken);
        return user.Id;
    }

    private static async Task CleanupAsync(string email)
    {
        await using var context = Context();
        var users = context.Users.Where(user => user.PrimaryEmail == email);
        await context.PasswordCredentials.Where(item => users.Select(user => user.Id).Contains(item.UserId)).ExecuteDeleteAsync();
        await users.ExecuteDeleteAsync();
    }
}
