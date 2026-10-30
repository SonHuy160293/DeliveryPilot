using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.Identity.API.Contracts.Auth;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Users;

public sealed class LoginHttpTests
{
    private const string TestSecretKey = "test-only-hs256-signing-key-with-at-least-32-bytes";
    private const string TestIssuer = "DeliveryPilot.Identity.Tests";
    private const string TestAudience = "DeliveryPilot.Tests";

    [Fact]
    public async Task PasswordHashServiceVerifiesExactPasswordHandlesRehashNeededAndRedactsDiagnostics()
    {
        const string password = " CorrectHorse!234 ";
        var user = User.Create("alice@example.test", "Alice");
        var service = new PasswordHashService();
        string hash = await service.HashAsync(user, password, CancellationToken.None);

        Assert.True(await service.VerifyAsync(user, hash, password, CancellationToken.None));
        Assert.False(await service.VerifyAsync(user, hash, password.Trim(), CancellationToken.None));
        Assert.False(await service.VerifyAsync(user, hash, "wrong-password", CancellationToken.None));
        Assert.False(await service.VerifyAsync(null, null, password, CancellationToken.None));
        Assert.False(await service.VerifyAsync(user, null, password, CancellationToken.None));
        Assert.False(await service.VerifyAsync(null, hash, password, CancellationToken.None));
        Assert.False(await service.VerifyAsync(user, "not-valid-base64!!!", password, CancellationToken.None));
        Assert.False(await service.VerifyAsync(user, string.Empty, password, CancellationToken.None));

        var lowIterationHasher = new PasswordHasher<User>(
            Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));
        string rehashNeededHash = lowIterationHasher.HashPassword(user, password);
        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            new PasswordHasher<User>().VerifyHashedPassword(user, rehashNeededHash, password));
        Assert.True(await service.VerifyAsync(user, rehashNeededHash, password, CancellationToken.None));

        var expiresAt = new DateTimeOffset(2026, 11, 2, 12, 10, 0, TimeSpan.Zero);
        Assert.DoesNotContain(password, new LoginUserRequest("alice@example.test", password).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, new LoginUserCommand("alice@example.test", password).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-jwt", new LoginUserResponse(user.Id, "secret-jwt", "Bearer", expiresAt).ToString(), StringComparison.Ordinal);
        var result = new LoginUserResult(
            new LoginUserResponse(user.Id, "secret-jwt", "Bearer", expiresAt),
            "secret-refresh",
            expiresAt.AddDays(7));
        Assert.DoesNotContain("secret-refresh", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatorEnforcesTrimmedEmailAndUntrimmedPasswordBoundaries()
    {
        var validator = new LoginUserValidator();
        string validEmail = new string('a', 307) + "@example.test";
        string maxPassword = new string('p', 1024);

        await validator.ValidateAsync(new LoginUserCommand(" " + validEmail + " ", " " + new string('p', 1022) + " "), CancellationToken.None);
        await validator.ValidateAsync(new LoginUserCommand(validEmail, maxPassword), CancellationToken.None);

        await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new LoginUserCommand("a" + validEmail, "password"), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new LoginUserCommand("not-an-email", "password"), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new LoginUserCommand("   ", "password"), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new LoginUserCommand("alice@example.test", "   "), CancellationToken.None));
        await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new LoginUserCommand("alice@example.test", maxPassword + "x"), CancellationToken.None));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"email\":\"bad\",\"password\":\"secret\"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"   \"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"secret\",\"userId\":\"0195f000-0000-7000-8000-000000000001\"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"secret\",\"role\":\"admin\"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"secret\",\"status\":\"active\"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"secret\",\"tenantId\":\"0195f000-0000-7000-8000-000000000002\"}")]
    [InlineData("{\"email\":\"alice@example.test\",\"password\":\"secret\",\"returnUrl\":\"https://evil.example\"}")]
    [InlineData("{\"email\":42,\"password\":\"secret\"}")]
    [InlineData("{")]
    public async Task InvalidLoginRequestsAreRejectedBeforeDatabaseAccessWithoutEchoingPassword(string json)
    {
        await using var app = await StartValidationOnlyAppAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsync(
            "/api/v1/auth/login",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveUnverifiedUserLogsInUpdatesLastLoginIssuesNoStoreTokenAndStrictRefreshCookie()
    {
        var loginTime = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(loginTime);
        await using var harness = await LoginTestHarness.StartAsync(clock);

        const string password = " CorrectHorse!234 ";
        User user = await harness.SeedUserAsync("müller@example.test", "Müller", password, status: "active");
        Assert.Null(user.EmailVerifiedAt);
        Assert.Null(user.LastLoginAt);

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "  MÜLLER@EXAMPLE.TEST  ", password }),
        };
        loginRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stale-or-unrelated-header");
        loginRequest.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString("D"));

        using HttpResponseMessage response = await harness.Client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", response.Headers.Pragma.ToString());

        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("dp_refresh_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        string refreshToken = ExtractCookieValue(setCookie, "dp_refresh_token");

        string rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(refreshToken, rawBody, StringComparison.Ordinal);
        Assert.DoesNotContain(password, rawBody, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(rawBody);
        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.Equal(user.Id, data.GetProperty("userId").GetGuid());
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        Assert.Equal(loginTime.AddMinutes(10), data.GetProperty("expiresAt").GetDateTimeOffset());
        string accessToken = data.GetProperty("accessToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        Assert.Equal(loginTime, user.LastLoginAt);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Equal(1, harness.Store.ActiveSessionCount(user.Id));

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/test/protected/me");
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage protectedResponse = await harness.Client.SendAsync(protectedRequest);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        using var protectedDoc = JsonDocument.Parse(await protectedResponse.Content.ReadAsStringAsync());
        JsonElement me = protectedDoc.RootElement.GetProperty("data");
        Assert.Equal(user.Id, me.GetProperty("userId").GetGuid());
        Guid firstSessionId = me.GetProperty("sessionId").GetGuid();
        Assert.NotEqual(Guid.Empty, firstSessionId);
        Assert.Empty(me.GetProperty("tenantIds").EnumerateArray());
        Assert.Empty(me.GetProperty("branchIds").EnumerateArray());
        Assert.Empty(me.GetProperty("permissions").EnumerateArray());

        IssuedAuthSession? rotated = await harness.Store.RefreshAsync(refreshToken, CancellationToken.None);
        Assert.NotNull(rotated);
        Assert.Equal(firstSessionId, rotated!.SessionId);

        DateTimeOffset secondLoginTime = loginTime.AddMinutes(3);
        clock.SetUtcNow(secondLoginTime);
        using HttpResponseMessage secondLogin = await harness.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "müller@example.test", password });
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        Assert.Equal(secondLoginTime, user.LastLoginAt);
        Assert.Equal(2, harness.Store.ActiveSessionCount(user.Id));
        Assert.True(await harness.Store.IsActiveAsync(user.Id, firstSessionId, CancellationToken.None));

        string allLogs = string.Join('\n', harness.Logs);
        Assert.DoesNotContain(password, allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, allLogs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown", "active", "CorrectHorse!234")]
    [InlineData("wrong-password", "active", "WrongPassword!999")]
    [InlineData("missing-credential", "active", "CorrectHorse!234")]
    [InlineData("pending-user", "pending", "CorrectHorse!234")]
    [InlineData("suspended-user", "suspended", "CorrectHorse!234")]
    [InlineData("deleted-user", "deleted", "CorrectHorse!234")]
    public async Task InvalidOrInactiveLoginsReturnUniform401WithoutCookieSessionOrTimestampUpdate(
        string scenario,
        string status,
        string attemptedPassword)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        await using var harness = await LoginTestHarness.StartAsync(clock);

        User? user = null;
        if (scenario != "unknown")
        {
            user = await harness.SeedUserAsync(
                "user@example.test",
                "User",
                "CorrectHorse!234",
                status: status,
                includeCredential: scenario != "missing-credential");
        }

        using HttpResponseMessage response = await harness.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "user@example.test", password = attemptedPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        string rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(attemptedPassword, rawBody, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(rawBody);
        JsonElement error = doc.RootElement.GetProperty("error");
        Assert.Equal("Authentication.InvalidCredentials", error.GetProperty("code").GetString());
        Assert.Equal("Invalid email or password.", error.GetProperty("message").GetString());

        if (user is not null)
        {
            Assert.Null(user.LastLoginAt);
            Assert.Equal(0, harness.Store.ActiveSessionCount(user.Id));
        }

        string allLogs = string.Join('\n', harness.Logs);
        Assert.DoesNotContain(attemptedPassword, allLogs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SessionCreationOrCommitFailureRollsBackLastLoginAtAndIssuesNoCredentials(bool failOnCommit)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        await using var harness = await LoginTestHarness.StartAsync(clock);

        const string password = "CorrectHorse!234";
        User user = await harness.SeedUserAsync("atomic@example.test", "Atomic", password, status: "active");
        if (failOnCommit)
        {
            harness.Store.FailOnCommit = true;
        }
        else
        {
            harness.Store.FailSessionCreation = true;
        }

        using HttpResponseMessage response = await harness.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "atomic@example.test", password });

        Assert.True((int)response.StatusCode >= 400);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Null(user.LastLoginAt);
        Assert.Equal(0, harness.Store.ActiveSessionCount(user.Id));
    }

    [PostgreSqlFact]
    public async Task PostgreSqlLoginPersistsLastLoginAndSessionAtomicallyAndSupportsParallelLogins()
    {
        await using var app = await StartPostgreSqlAppAsync();
        using var client = app.GetTestClient();

        string email = $"login-{Guid.NewGuid():N}@example.test";
        const string password = " CorrectHorse!234 ";

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { displayName = "Alice", email, password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using var registerJson = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        Guid userId = registerJson.RootElement.GetProperty("data").GetProperty("userId").GetGuid();

        try
        {
            using HttpResponseMessage wrongPasswordResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email, password = password.Trim() });
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
            Assert.False(wrongPasswordResponse.Headers.Contains("Set-Cookie"));

            HttpResponseMessage[] parallelResponses = await Task.WhenAll(
                client.PostAsJsonAsync("/api/v1/auth/login", new { email = "  " + email.ToUpperInvariant() + "  ", password }),
                client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }));

            try
            {
                foreach (HttpResponseMessage loginResponse in parallelResponses)
                {
                    Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
                    Assert.Equal("no-store", loginResponse.Headers.CacheControl?.ToString());
                    Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));
                }

                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                User persistedUser = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
                Assert.NotNull(persistedUser.LastLoginAt);
                Assert.Null(persistedUser.EmailVerifiedAt);

                List<AuthSession> sessions = await db.AuthSessions
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .ToListAsync();
                Assert.Equal(2, sessions.Count);
                Assert.All(sessions, session =>
                {
                    Assert.False(session.IsRevoked);
                    Assert.Equal(32, session.RefreshTokenVerifier.Length);
                });
            }
            finally
            {
                foreach (HttpResponseMessage response in parallelResponses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            using var cleanupScope = app.Services.CreateScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.AuthSessions.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await db.PasswordCredentials.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        string prefix = cookieName + "=";
        int start = setCookieHeader.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += prefix.Length;
        int end = setCookieHeader.IndexOf(';', start);
        return end < 0 ? setCookieHeader[start..] : setCookieHeader[start..end];
    }

    private static async Task<WebApplication> StartValidationOnlyAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["IntegrationApi:ApiKeys:0"] = "isolated-login-validation-test-key",
            ["Jwt:SecretKey"] = TestSecretKey,
            ["Jwt:Issuer"] = TestIssuer,
            ["Jwt:Audience"] = TestAudience,
            ["Jwt:AccessTokenExpirationMinutes"] = "10",
            ["Jwt:RefreshTokenExpirationDays"] = "7",
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddAuthSessions(builder.Configuration)
            .AddConfiguredAuthentication(builder.Configuration, builder.Environment)
            .AddConfiguredAuthorization()
            .AddConfiguredApiVersioning();

        builder.Services
            .AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(AuthController).Assembly);

        var app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartPostgreSqlAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")
                ?? "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["IntegrationApi:ApiKeys:0"] = "isolated-login-postgres-test-key",
            ["Jwt:SecretKey"] = TestSecretKey,
            ["Jwt:Issuer"] = TestIssuer,
            ["Jwt:Audience"] = TestAudience,
            ["Jwt:AccessTokenExpirationMinutes"] = "10",
            ["Jwt:RefreshTokenExpirationDays"] = "7",
        });

        builder.Services
            .AddCurrentUserProvider()
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddAuthSessions(builder.Configuration)
            .AddConfiguredAuthentication(builder.Configuration, builder.Environment)
            .AddConfiguredAuthorization()
            .AddConfiguredApiVersioning();

        builder.Services
            .AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(AuthController).Assembly);

        var app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private long _utcTicks = initialUtcNow.ToUniversalTime().UtcTicks;

        public override DateTimeOffset GetUtcNow()
            => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

        public void SetUtcNow(DateTimeOffset value)
            => Interlocked.Exchange(ref _utcTicks, value.ToUniversalTime().UtcTicks);
    }

    private sealed class InMemoryIdentityStore(
        JwtAccessTokenIssuer issuer,
        IOptions<AuthTokenOptions> options,
        TimeProvider timeProvider)
        : IUserRepository,
          IPasswordCredentialRepository,
          IAuthSessionService,
          IAuthenticatedContextValidator,
          IUnitOfWork
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<Guid, User> _users = [];
        private readonly Dictionary<Guid, PasswordCredential> _credentials = [];
        private readonly Dictionary<Guid, AuthSession> _committedSessions = [];
        private readonly List<AuthSession> _stagedSessions = [];
        private bool _inTransaction;

        public bool FailSessionCreation { get; set; }
        public bool FailOnCommit { get; set; }

        public int ActiveSessionCount(Guid userId)
        {
            lock (_sync)
            {
                return _committedSessions.Values.Count(s => s.UserId == userId && !s.IsRevoked);
            }
        }

        public void Seed(User user, PasswordCredential? credential)
        {
            lock (_sync)
            {
                _users[user.Id] = user;
                if (credential is not null)
                {
                    _credentials[user.Id] = credential;
                }
            }
        }

        public Task<User> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_users[id]);
            }
        }

        public Task<bool> ExistsForRegistrationAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_users.Values.Any(u =>
                    string.Equals(u.PrimaryEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase)));
            }
        }

        public Task<User?> FindByEmailForLoginAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                User? match = _users.Values.FirstOrDefault(u =>
                    string.Equals(u.PrimaryEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(match);
            }
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _users[user.Id] = user;
                return Task.CompletedTask;
            }
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _users[user.Id] = user;
                return Task.CompletedTask;
            }
        }

        public Task<PasswordCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_credentials.GetValueOrDefault(userId));
            }
        }

        public Task AddAsync(PasswordCredential credential, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _credentials[credential.UserId] = credential;
                return Task.CompletedTask;
            }
        }

        public Task<IssuedAuthSession?> CreateAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_inTransaction)
                {
                    throw new InvalidOperationException("Session creation requires an active command transaction.");
                }

                if (FailSessionCreation
                    || !_users.TryGetValue(userId, out User? user)
                    || !string.Equals(user.Status, "active", StringComparison.Ordinal))
                {
                    return Task.FromResult<IssuedAuthSession?>(null);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                var sessionId = Guid.NewGuid();
                (string refreshToken, byte[] verifier) = RefreshTokenCodec.Create(sessionId);
                var session = AuthSession.Create(sessionId, userId, verifier, now, now.Add(options.Value.RefreshTokenLifetime));
                _stagedSessions.Add(session);
                (string accessToken, DateTimeOffset accessExpiresAt) = issuer.Issue(userId, sessionId, now);
                return Task.FromResult<IssuedAuthSession?>(
                    new IssuedAuthSession(sessionId, accessToken, accessExpiresAt, refreshToken, session.RefreshExpiresAt));
            }
        }

        public Task<IssuedAuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RefreshTokenCodec.TryParse(refreshToken, out Guid sessionId, out byte[] verifier))
            {
                return Task.FromResult<IssuedAuthSession?>(null);
            }

            lock (_sync)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                if (!_committedSessions.TryGetValue(sessionId, out AuthSession? session)
                    || !CryptographicOperations.FixedTimeEquals(session.RefreshTokenVerifier, verifier)
                    || !session.CanRefreshAt(now)
                    || !_users.TryGetValue(session.UserId, out User? user)
                    || !string.Equals(user.Status, "active", StringComparison.Ordinal))
                {
                    return Task.FromResult<IssuedAuthSession?>(null);
                }

                (string nextRefresh, byte[] nextVerifier) = RefreshTokenCodec.Create(session.Id);
                session.RotateRefreshToken(nextVerifier, now);
                (string accessToken, DateTimeOffset accessExpiresAt) = issuer.Issue(session.UserId, session.Id, now);
                return Task.FromResult<IssuedAuthSession?>(
                    new IssuedAuthSession(session.Id, accessToken, accessExpiresAt, nextRefresh, session.RefreshExpiresAt));
            }
        }

        public Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_committedSessions.TryGetValue(sessionId, out AuthSession? session) || session.UserId != userId)
                {
                    return Task.FromResult(false);
                }

                session.Revoke(timeProvider.GetUtcNow());
                return Task.FromResult(true);
            }
        }

        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(
                    _committedSessions.TryGetValue(sessionId, out AuthSession? session)
                    && session.UserId == userId
                    && !session.IsRevoked
                    && _users.TryGetValue(userId, out User? user)
                    && string.Equals(user.Status, "active", StringComparison.Ordinal));
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> action,
            CancellationToken cancellationToken = default)
        {
            Dictionary<Guid, DateTimeOffset?> snapshotLastLogin;
            lock (_sync)
            {
                _inTransaction = true;
                _stagedSessions.Clear();
                snapshotLastLogin = _users.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.LastLoginAt);
            }

            try
            {
                TResponse response = await action(cancellationToken);
                if (response is Result { IsFailure: true })
                {
                    RollbackSnapshot(snapshotLastLogin);
                    return response;
                }

                if (FailOnCommit)
                {
                    throw new InvalidOperationException("Simulated database commit failure.");
                }

                lock (_sync)
                {
                    foreach (AuthSession staged in _stagedSessions)
                    {
                        _committedSessions[staged.Id] = staged;
                    }

                    _stagedSessions.Clear();
                    _inTransaction = false;
                }

                return response;
            }
            catch
            {
                RollbackSnapshot(snapshotLastLogin);
                throw;
            }
        }

        private void RollbackSnapshot(Dictionary<Guid, DateTimeOffset?> snapshotLastLogin)
        {
            lock (_sync)
            {
                foreach ((Guid userId, DateTimeOffset? previousLastLoginAt) in snapshotLastLogin)
                {
                    if (_users.TryGetValue(userId, out User? user))
                    {
                        typeof(User)
                            .GetProperty(nameof(User.LastLoginAt))!
                            .SetValue(user, previousLastLoginAt);
                    }
                }

                _stagedSessions.Clear();
                _inTransaction = false;
            }
        }
    }

    private sealed class ListLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, logs);

        public void Dispose()
        {
        }

        private sealed class ListLogger(string category, ConcurrentQueue<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => logs.Enqueue($"[{category}] {formatter(state, exception)} {exception?.Message}");
        }
    }

    private sealed class LoginTestHarness(
        WebApplication app,
        HttpClient client,
        InMemoryIdentityStore store,
        IPasswordHashService passwordHashService,
        ConcurrentQueue<string> logs) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public InMemoryIdentityStore Store => store;
        public IReadOnlyCollection<string> Logs => logs;

        public static async Task<LoginTestHarness> StartAsync(TimeProvider clock)
        {
            var logs = new ConcurrentQueue<string>();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ListLoggerProvider(logs));
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["IntegrationApi:ApiKeys:0"] = "isolated-login-test-key",
                ["Jwt:SecretKey"] = TestSecretKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "10",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
            });

            builder.Services.AddSingleton(clock);
            builder.Services.AddCurrentUserProvider();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddAuthSessions(builder.Configuration);
            builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
            builder.Services.AddConfiguredAuthorization();
            builder.Services.AddConfiguredApiVersioning();

            builder.Services.AddSingleton<InMemoryIdentityStore>();
            builder.Services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<InMemoryIdentityStore>());
            builder.Services.AddScoped<IPasswordCredentialRepository>(sp => sp.GetRequiredService<InMemoryIdentityStore>());
            builder.Services.AddScoped<IAuthSessionService>(sp => sp.GetRequiredService<InMemoryIdentityStore>());
            builder.Services.AddScoped<IAuthenticatedContextValidator>(sp => sp.GetRequiredService<InMemoryIdentityStore>());
            builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<InMemoryIdentityStore>());

            builder.Services
                .AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
                .AddApplicationPart(typeof(AuthController).Assembly);

            var app = builder.Build();
            app.UseCorrelationId();
            app.UseGlobalExceptionHandling();
            app.UseApiStatusCodeEnvelope();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/test/protected/me", (ICurrentUserProvider currentUserProvider) =>
            {
                CurrentUser current = currentUserProvider.GetCurrentUser()!;
                return Results.Json(ApiResponse.Ok(new
                {
                    userId = current.Id,
                    sessionId = current.SessionId,
                    tenantIds = current.TenantIds,
                    branchIds = current.BranchIds,
                    permissions = current.Permissions,
                }));
            }).RequireAuthorization();

            app.MapControllers();
            await app.StartAsync();

            return new LoginTestHarness(
                app,
                app.GetTestClient(),
                app.Services.GetRequiredService<InMemoryIdentityStore>(),
                new PasswordHashService(),
                logs);
        }

        public async Task<User> SeedUserAsync(
            string email,
            string displayName,
            string password,
            string status = "active",
            bool includeCredential = true)
        {
            var user = User.Create(email, displayName, status: status);
            PasswordCredential? credential = null;
            if (includeCredential)
            {
                string hash = await passwordHashService.HashAsync(user, password, CancellationToken.None);
                credential = PasswordCredential.Create(user.Id, hash, user.CreatedAt);
            }

            store.Seed(user, credential);
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }
}
