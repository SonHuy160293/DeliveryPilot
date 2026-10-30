using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Security;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class AuthSessionPersistenceTests
{
    private const string TestSecretKey = "postgres-test-hs256-signing-key-with-at-least-32-bytes";

    private static readonly IOptions<AuthTokenOptions> _options = Options.Create(new AuthTokenOptions
    {
        SecretKey = TestSecretKey,
        Issuer = "DeliveryPilot.Identity",
        Audience = "DeliveryPilot",
        AccessTokenExpirationMinutes = 10,
        RefreshTokenExpirationDays = 7,
    });

    [PostgreSqlFact]
    public async Task ConcurrentRefreshAndRevokeSerializeAgainstPostgreSqlSessionRow()
    {
        var clock = new JwtContextValidityTests.ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        var user = User.Create(email, "Session User", status: "active");

        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        try
        {
            IssuedAuthSession initial;
            await using (var createContext = CreateContext())
            {
                var unitOfWork = new IdentityUnitOfWork(createContext);
                var service = CreateService(createContext, clock);
                initial = (await unitOfWork.ExecuteInTransactionAsync(ct => service.CreateAsync(user.Id, ct)))!;
                Assert.NotNull(initial);
            }

            await using (var checkContext = CreateContext())
            {
                var validator = new AuthenticatedContextValidator(checkContext);
                Assert.True(await validator.IsActiveAsync(user.Id, initial.SessionId, CancellationToken.None));
            }

            // Concurrent refresh attempts with the same refresh token: exactly one succeeds.
            clock.Advance(TimeSpan.FromDays(1));
            IssuedAuthSession?[] concurrentRefresh = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var context = CreateContext();
                var unitOfWork = new IdentityUnitOfWork(context);
                var service = CreateService(context, clock);
                return await unitOfWork.ExecuteInTransactionAsync(ct => service.RefreshAsync(initial.RefreshToken, ct));
            }));

            IssuedAuthSession rotated = Assert.Single(concurrentRefresh, item => item is not null)!;
            Assert.Equal(3, concurrentRefresh.Count(item => item is null));
            Assert.Equal(initial.RefreshTokenExpiresAt, rotated.RefreshTokenExpiresAt);

            // Reusing the old rotated token fails without revoking the active session.
            await using (var reuseContext = CreateContext())
            {
                var unitOfWork = new IdentityUnitOfWork(reuseContext);
                var service = CreateService(reuseContext, clock);
                Assert.Null(await unitOfWork.ExecuteInTransactionAsync(ct => service.RefreshAsync(initial.RefreshToken, ct)));

                var validator = new AuthenticatedContextValidator(reuseContext);
                Assert.True(await validator.IsActiveAsync(user.Id, initial.SessionId, CancellationToken.None));
            }

            // Revoke commits against the same row; subsequent validation and refresh fail immediately.
            await using (var revokeContext = CreateContext())
            {
                var unitOfWork = new IdentityUnitOfWork(revokeContext);
                var service = CreateService(revokeContext, clock);
                Assert.True(await unitOfWork.ExecuteInTransactionAsync(ct => service.RevokeAsync(user.Id, initial.SessionId, ct)));
            }

            await using (var afterRevokeContext = CreateContext())
            {
                var validator = new AuthenticatedContextValidator(afterRevokeContext);
                Assert.False(await validator.IsActiveAsync(user.Id, initial.SessionId, CancellationToken.None));

                var unitOfWork = new IdentityUnitOfWork(afterRevokeContext);
                var service = CreateService(afterRevokeContext, clock);
                Assert.Null(await unitOfWork.ExecuteInTransactionAsync(ct => service.RefreshAsync(rotated.RefreshToken, ct)));
            }
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.AuthSessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync();
            await cleanup.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RolledBackSessionCreationNeverLeavesUsableAuthenticatedContext()
    {
        var clock = new JwtContextValidityTests.ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 13, 0, 0, TimeSpan.Zero));
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        var user = User.Create(email, "Rollback User", status: "active");

        await using var context = CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        try
        {
            IssuedAuthSession? staged = null;
            var unitOfWork = new IdentityUnitOfWork(context);
            var service = CreateService(context, clock);
            Result result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                staged = await service.CreateAsync(user.Id, ct);
                return Result.Failure(new Error("Auth.Failed", "Simulated failure after staging session."));
            });

            Assert.True(result.IsFailure);
            Assert.NotNull(staged);
            var validator = new AuthenticatedContextValidator(context);
            Assert.False(await validator.IsActiveAsync(user.Id, staged!.SessionId, CancellationToken.None));
            Assert.False(await context.AuthSessions.AnyAsync(s => s.Id == staged.SessionId));
        }
        finally
        {
            await context.AuthSessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync();
            await context.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private static IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"))
            .Options);

    private static AuthSessionService CreateService(IdentityDbContext context, TimeProvider clock)
        => new(context, new JwtAccessTokenIssuer(_options), _options, clock);
}
