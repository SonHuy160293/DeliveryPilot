using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Options;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class JwtContextValidityTests
{
    private const string TestSecretKey = "test-only-hs256-signing-key-with-at-least-32-bytes";
    private const string TestIssuer = "DeliveryPilot.Identity.Tests";
    private const string TestAudience = "DeliveryPilot.Tests";

    [Fact]
    public void AppsettingsProvidesTenMinuteAccessAndSevenDayRefreshWithNoHardcodedFallback()
    {
        var jwtDefaults = new JwtOptions();
        var issuerDefaults = new AuthTokenOptions();
        Assert.Equal(0, jwtDefaults.AccessTokenExpirationMinutes);
        Assert.Equal(0, issuerDefaults.AccessTokenExpirationMinutes);
        Assert.Equal(0, issuerDefaults.RefreshTokenExpirationDays);

        string appsettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/appsettings.json"));
        Assert.True(File.Exists(appsettingsPath));

        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: false);
        Assert.Equal("10", configuration["Jwt:AccessTokenExpirationMinutes"]);
        Assert.Equal("7", configuration["Jwt:RefreshTokenExpirationDays"]);
        Assert.Null(configuration["Jwt:SecretKey"]);
    }

    [Theory]
    [InlineData("Development", "", TestIssuer, TestAudience, "10", "7")]
    [InlineData("Production", "short-key-under-32-bytes", TestIssuer, TestAudience, "10", "7")]
    [InlineData("Development", TestSecretKey, "", TestAudience, "10", "7")]
    [InlineData("Production", TestSecretKey, TestIssuer, "", "10", "7")]
    [InlineData("Development", TestSecretKey, TestIssuer, TestAudience, "0", "7")]
    [InlineData("Production", TestSecretKey, TestIssuer, TestAudience, "-5", "7")]
    [InlineData("Development", TestSecretKey, TestIssuer, TestAudience, "10", "0")]
    [InlineData("Production", TestSecretKey, TestIssuer, TestAudience, "10", "-1")]
    public async Task InvalidOrMissingConfigurationFailsStartupWithoutLeakingKeyMaterial(
        string environment,
        string secretKey,
        string issuer,
        string audience,
        string accessMinutes,
        string refreshDays)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["Jwt:SecretKey"] = secretKey,
            ["Jwt:Issuer"] = issuer,
            ["Jwt:Audience"] = audience,
            ["Jwt:AccessTokenExpirationMinutes"] = accessMinutes,
            ["Jwt:RefreshTokenExpirationDays"] = refreshDays,
        });

        builder.Services
            .AddPersistence(builder.Configuration)
            .AddAuthSessions(builder.Configuration)
            .AddConfiguredAuthentication(builder.Configuration, builder.Environment);

        await using var app = builder.Build();
        Exception exception = await Record.ExceptionAsync(() => app.StartAsync())
            ?? throw new InvalidOperationException("Expected startup validation to fail.");
        Assert.True(
            exception is OptionsValidationException
            || (exception is AggregateException aggregate && aggregate.InnerExceptions.All(inner => inner is OptionsValidationException)));
        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            Assert.DoesNotContain(secretKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task IssuedJwtValidatesAndResolvesVerifiedSubjectAndSessionOnly()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");

        IssuedAuthSession? issued = await harness.Sessions.CreateAsync(userId, CancellationToken.None);
        Assert.NotNull(issued);
        Assert.NotEqual(Guid.Empty, issued.SessionId);
        Assert.Equal(clock.GetUtcNow().AddMinutes(10), issued.AccessTokenExpiresAt);
        Assert.Equal(clock.GetUtcNow().AddDays(7), issued.RefreshTokenExpiresAt);
        Assert.Equal(TimeSpan.Zero, issued.AccessTokenExpiresAt.Offset);
        Assert.Equal(TimeSpan.Zero, issued.RefreshTokenExpiresAt.Offset);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(issued.AccessToken);
        Assert.Equal(JwtAccessTokenIssuer.SigningAlgorithm, jwt.Alg);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(issued.SessionId.ToString(), jwt.GetClaim(JwtRegisteredClaimNames.Sid).Value);
        Assert.Equal(TestIssuer, jwt.Issuer);
        Assert.Equal(TestAudience, Assert.Single(jwt.Audiences));
        Assert.True(Guid.TryParse(jwt.Id, out Guid jti) && jti != Guid.Empty);
        Assert.False(jwt.TryGetClaim(CustomClaims.Permission, out _));
        Assert.False(jwt.TryGetClaim(CustomClaims.TenantIds, out _));
        Assert.False(jwt.TryGetClaim(CustomClaims.BranchIds, out _));

        IssuedAuthSession? second = await harness.Sessions.CreateAsync(userId, CancellationToken.None);
        Assert.NotNull(second);
        Assert.NotEqual(jwt.Id, new JsonWebTokenHandler().ReadJsonWebToken(second.AccessToken).Id);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/test/protected/mutate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.AccessToken);
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        using var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, harness.MutationCount);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = doc.RootElement.GetProperty("data");
        Assert.Equal(userId.ToString(), data.GetProperty("userId").GetString());
        Assert.Equal(issued.SessionId.ToString(), data.GetProperty("sessionId").GetString());
        Assert.Empty(data.GetProperty("tenantIds").EnumerateArray());
        Assert.Empty(data.GetProperty("branchIds").EnumerateArray());
        Assert.Empty(data.GetProperty("permissions").EnumerateArray());
    }

    [Fact]
    public async Task ExactExpiryBoundariesEnforceZeroClockSkewAndPreventProtectedMutations()
    {
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(start);
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");

        IssuedAuthSession issued = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;

        // 1 second before expiry -> admitted.
        clock.SetUtcNow(issued.AccessTokenExpiresAt.AddSeconds(-1));
        using (var beforeExpiry = await harness.SendProtectedMutationAsync(issued.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);
        }

        Assert.Equal(1, harness.MutationCount);

        // Exact expiry instant -> rejected with 401 and IS-TOKEN-EXPIRED header; no mutation.
        clock.SetUtcNow(issued.AccessTokenExpiresAt);
        using (var atExpiry = await harness.SendProtectedMutationAsync(issued.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, atExpiry.StatusCode);
            Assert.Equal("True", Assert.Single(atExpiry.Headers.GetValues("IS-TOKEN-EXPIRED")));
            await AssertUnauthorizedEnvelopeAsync(atExpiry);
        }

        Assert.Equal(1, harness.MutationCount);
        Assert.Null(harness.LastObservedUnauthenticatedUser);

        // 1 second after expiry -> rejected; no mutation.
        clock.SetUtcNow(issued.AccessTokenExpiresAt.AddSeconds(1));
        using (var afterExpiry = await harness.SendProtectedMutationAsync(issued.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, afterExpiry.StatusCode);
            await AssertUnauthorizedEnvelopeAsync(afterExpiry);
        }

        Assert.Equal(1, harness.MutationCount);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ForgedInvalidAlgorithmIssuerAudienceSubjectAndQueryTokensAreRejected(string environment)
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock, environment);
        Guid userA = Guid.CreateVersion7();
        Guid userB = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userA, "active");
        harness.Store.SetUserStatus(userB, "active");
        IssuedAuthSession sessionA = (await harness.Sessions.CreateAsync(userA, CancellationToken.None))!;

        string wrongKeyToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: "different-secret-key-with-at-least-32-bytes-length",
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: TestIssuer,
            audience: TestAudience,
            sub: userA.ToString(),
            sid: sessionA.SessionId.ToString());
        string wrongAlgToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha384,
            issuer: TestIssuer,
            audience: TestAudience,
            sub: userA.ToString(),
            sid: sessionA.SessionId.ToString());
        string wrongIssuerToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: "Other.Issuer",
            audience: TestAudience,
            sub: userA.ToString(),
            sid: sessionA.SessionId.ToString());
        string wrongAudienceToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: TestIssuer,
            audience: "Other.Audience",
            sub: userA.ToString(),
            sid: sessionA.SessionId.ToString());
        string emptySubToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: TestIssuer,
            audience: TestAudience,
            sub: Guid.Empty.ToString(),
            sid: sessionA.SessionId.ToString());
        string nonGuidSidToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: TestIssuer,
            audience: TestAudience,
            sub: userA.ToString(),
            sid: "not-a-guid");
        string mismatchedSubSidToken = MintCustomJwt(
            clock.GetUtcNow(),
            key: TestSecretKey,
            algorithm: SecurityAlgorithms.HmacSha256,
            issuer: TestIssuer,
            audience: TestAudience,
            sub: userB.ToString(),
            sid: sessionA.SessionId.ToString());
        string unsignedToken = $"{Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""")}.{Base64UrlEncoder.Encode($"{{\"sub\":\"{userA}\",\"sid\":\"{sessionA.SessionId}\",\"iss\":\"{TestIssuer}\",\"aud\":\"{TestAudience}\",\"exp\":{clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds()}}}")}.";

        foreach (string token in new[]
        {
            wrongKeyToken,
            wrongAlgToken,
            wrongIssuerToken,
            wrongAudienceToken,
            emptySubToken,
            nonGuidSidToken,
            mismatchedSubSidToken,
            unsignedToken,
        })
        {
            using var response = await harness.SendProtectedMutationAsync(token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertUnauthorizedEnvelopeAsync(response);
        }

        // Credentials in query strings are ignored and rejected.
        using var queryAttempt = await harness.Client.PostAsync(
            "/test/protected/mutate?access_token=" + Uri.EscapeDataString(sessionA.AccessToken),
            content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, queryAttempt.StatusCode);
        Assert.Equal(0, harness.MutationCount);
    }

    [Fact]
    public async Task RefreshRotatesVerifierKeepsFixedSevenDayDeadlineRejectsOldTokenWithoutRevokingSessionAndSerializesConcurrency()
    {
        var loginTime = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(loginTime);
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");

        IssuedAuthSession initial = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;
        DateTimeOffset fixedDeadline = initial.RefreshTokenExpiresAt;
        Assert.Equal(loginTime.AddDays(7), fixedDeadline);

        // Advance 2 days and rotate.
        clock.Advance(TimeSpan.FromDays(2));
        IssuedAuthSession? rotated = await harness.Sessions.RefreshAsync(initial.RefreshToken, CancellationToken.None);
        Assert.NotNull(rotated);
        Assert.Equal(initial.SessionId, rotated.SessionId);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        Assert.Equal(fixedDeadline, rotated.RefreshTokenExpiresAt);
        Assert.Equal(clock.GetUtcNow().AddMinutes(10), rotated.AccessTokenExpiresAt);

        // Reusing the old rotated refresh token is rejected WITHOUT revoking the current session.
        IssuedAuthSession? replayOld = await harness.Sessions.RefreshAsync(initial.RefreshToken, CancellationToken.None);
        Assert.Null(replayOld);

        using (var currentAccessStillValid = await harness.SendProtectedMutationAsync(rotated.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, currentAccessStillValid.StatusCode);
        }

        // Two simultaneous refresh attempts with the current token: exactly one succeeds.
        IssuedAuthSession?[] concurrent = await Task.WhenAll(
            harness.Sessions.RefreshAsync(rotated.RefreshToken, CancellationToken.None),
            harness.Sessions.RefreshAsync(rotated.RefreshToken, CancellationToken.None));
        IssuedAuthSession winner = Assert.Single(concurrent, item => item is not null)!;
        Assert.Single(concurrent, item => item is null);
        Assert.Equal(fixedDeadline, winner.RefreshTokenExpiresAt);

        // Issue a fresh access token 2 minutes before the 7-day refresh deadline.
        clock.SetUtcNow(fixedDeadline.AddMinutes(-2));
        IssuedAuthSession? beforeDeadline = await harness.Sessions.RefreshAsync(winner.RefreshToken, CancellationToken.None);
        Assert.NotNull(beforeDeadline);
        Assert.Equal(fixedDeadline, beforeDeadline.RefreshTokenExpiresAt);

        // At the 7-day refresh deadline, refresh is rejected, but the unexpired access token remains valid until its own exp.
        clock.SetUtcNow(fixedDeadline);
        Assert.Null(await harness.Sessions.RefreshAsync(beforeDeadline.RefreshToken, CancellationToken.None));
        using (var accessStillValid = await harness.SendProtectedMutationAsync(beforeDeadline.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, accessStillValid.StatusCode);
        }
    }

    [Fact]
    public async Task LogoutRevokesOnlyTargetSessionImmediatelyRejectsAccessTokensAllowsInFlightWorkAndResolvesRefreshRaces()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");

        IssuedAuthSession session1 = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;
        IssuedAuthSession session2 = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;

        // Start an admitted in-flight request on session1, then revoke session1 while it is running.
        using var inFlightRequest = new HttpRequestMessage(HttpMethod.Post, "/test/protected/in-flight");
        inFlightRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session1.AccessToken);
        Task<HttpResponseMessage> inFlightTask = harness.Client.SendAsync(inFlightRequest);
        await harness.InFlightAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(await harness.Sessions.RevokeAsync(userId, session1.SessionId, CancellationToken.None));
        harness.ReleaseInFlight.TrySetResult(true);

        using HttpResponseMessage inFlightResponse = await inFlightTask;
        Assert.Equal(HttpStatusCode.OK, inFlightResponse.StatusCode);

        // Subsequent request with session1's access token is immediately rejected before mutation.
        int mutationsBefore = harness.MutationCount;
        using (var revokedAccess = await harness.SendProtectedMutationAsync(session1.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, revokedAccess.StatusCode);
        }

        Assert.Equal(mutationsBefore, harness.MutationCount);
        Assert.Null(await harness.Sessions.RefreshAsync(session1.RefreshToken, CancellationToken.None));

        // Session2 of the same user remains valid for both access and refresh.
        using (var otherSessionAccess = await harness.SendProtectedMutationAsync(session2.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, otherSessionAccess.StatusCode);
        }

        IssuedAuthSession? rotatedSession2 = await harness.Sessions.RefreshAsync(session2.RefreshToken, CancellationToken.None);
        Assert.NotNull(rotatedSession2);

        // Race ordering: if refresh commits first and logout commits second, the resulting session is revoked.
        Assert.True(await harness.Sessions.RevokeAsync(userId, rotatedSession2.SessionId, CancellationToken.None));
        using (var postLogoutRefreshAccess = await harness.SendProtectedMutationAsync(rotatedSession2.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, postLogoutRefreshAccess.StatusCode);
        }

        Assert.Null(await harness.Sessions.RefreshAsync(rotatedSession2.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task InactiveAccountIsRejectedOnIssuanceRefreshAndEveryProtectedRequest()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");

        IssuedAuthSession session = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;
        harness.Store.SetUserStatus(userId, "suspended");

        using (var suspendedAccess = await harness.SendProtectedMutationAsync(session.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, suspendedAccess.StatusCode);
        }

        Assert.Equal(0, harness.MutationCount);
        Assert.Null(await harness.Sessions.RefreshAsync(session.RefreshToken, CancellationToken.None));
        Assert.Null(await harness.Sessions.CreateAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task ValidIdentityWithoutResourceGrantReceives403AndExistingIntegrationRouteKeepsKeyRequirement()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 16, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");
        IssuedAuthSession session = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;

        using var forbiddenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/test/protected/tenants/{Guid.NewGuid()}/approve");
        forbiddenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var forbiddenResponse = await harness.Client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(0, harness.MutationCount);

        using var forbiddenDoc = JsonDocument.Parse(await forbiddenResponse.Content.ReadAsStringAsync());
        Assert.False(forbiddenDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Request.Forbidden", forbiddenDoc.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(forbiddenDoc.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ValidatorOutageFailsClosedAndLogsAndDiagnosticsNeverExposeSecrets()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 17, 0, 0, TimeSpan.Zero));
        await using var harness = await TestHostHarness.StartAsync(clock);
        Guid userId = Guid.CreateVersion7();
        harness.Store.SetUserStatus(userId, "active");
        IssuedAuthSession session = (await harness.Sessions.CreateAsync(userId, CancellationToken.None))!;

        // Simulate a validator dependency failure containing sensitive connection details.
        const string simulatedSecretInError = "Password=super-secret-db-password";
        harness.Store.FailureToThrow = new InvalidOperationException(simulatedSecretInError);

        using (var outageResponse = await harness.SendProtectedMutationAsync(session.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, outageResponse.StatusCode);
            string body = await outageResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain(simulatedSecretInError, body, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSecretKey, body, StringComparison.Ordinal);
            Assert.DoesNotContain(session.AccessToken, body, StringComparison.Ordinal);
            Assert.DoesNotContain(session.RefreshToken, body, StringComparison.Ordinal);
        }

        Assert.Equal(0, harness.MutationCount);

        // Verify diagnostics ToString() and captured logs never contain secrets.
        var authOptions = new AuthTokenOptions
        {
            SecretKey = TestSecretKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
            AccessTokenExpirationMinutes = 10,
            RefreshTokenExpirationDays = 7,
        };
        var domainSession = AuthSession.Create(
            session.SessionId,
            userId,
            new byte[AuthSession.RefreshVerifierLength],
            clock.GetUtcNow(),
            clock.GetUtcNow().AddDays(7));

        string combinedDiagnostics = string.Join(
            "\n",
            authOptions.ToString(),
            session.ToString(),
            domainSession.ToString(),
            string.Join("\n", harness.Logs));

        Assert.DoesNotContain(TestSecretKey, combinedDiagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(session.AccessToken, combinedDiagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, combinedDiagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(simulatedSecretInError, combinedDiagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingValidatorAndUncommittedSessionFailClosed()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new AuthTokenOptions
        {
            SecretKey = TestSecretKey,
            Issuer = TestIssuer,
            Audience = TestAudience,
            AccessTokenExpirationMinutes = 10,
            RefreshTokenExpirationDays = 7,
        });
        var issuer = new JwtAccessTokenIssuer(options);

        // 1. AuthSessionService refuses to stage or rotate without an active command transaction.
        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        await using (var untransactedDb = new IdentityDbContext(dbOptions))
        {
            var service = new AuthSessionService(untransactedDb, issuer, options, clock);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.CreateVersion7(), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync("unused", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeAsync(Guid.CreateVersion7(), Guid.NewGuid(), CancellationToken.None));
        }

        // 2. Host without IAuthenticatedContextValidator fails closed even for a cryptographically valid JWT.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = TestSecretKey,
            ["Jwt:Issuer"] = TestIssuer,
            ["Jwt:Audience"] = TestAudience,
            ["Jwt:AccessTokenExpirationMinutes"] = "10",
            ["IntegrationApi:ApiKeys:0"] = "test-integration-key",
        });
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddConfiguredAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", () => Results.Ok()).RequireAuthorization();
        await app.StartAsync();

        (string token, _) = issuer.Issue(Guid.CreateVersion7(), Guid.NewGuid(), clock.GetUtcNow());
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task AssertUnauthorizedEnvelopeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Request.Unauthorized", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
    }

    private static string MintCustomJwt(
        DateTimeOffset now,
        string key,
        string algorithm,
        string issuer,
        string audience,
        string sub,
        string sid)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                algorithm),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = sub,
                [JwtRegisteredClaimNames.Sid] = sid,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    internal sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private long _utcTicks = initialUtcNow.ToUniversalTime().UtcTicks;

        public override DateTimeOffset GetUtcNow()
            => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

        public void SetUtcNow(DateTimeOffset value)
            => Interlocked.Exchange(ref _utcTicks, value.ToUniversalTime().UtcTicks);

        public void Advance(TimeSpan delta)
            => SetUtcNow(GetUtcNow().Add(delta));
    }

    private sealed class InMemorySessionStore(JwtAccessTokenIssuer issuer, IOptions<AuthTokenOptions> options, TimeProvider timeProvider)
        : IAuthSessionService, IAuthenticatedContextValidator
    {
        private readonly Lock _sync = new();
        private readonly ConcurrentDictionary<Guid, string> _userStatuses = new();
        private readonly Dictionary<Guid, AuthSession> _sessions = [];

        public Exception? FailureToThrow { get; set; }

        public void SetUserStatus(Guid userId, string status) => _userStatuses[userId] = status;

        public Task<IssuedAuthSession?> CreateAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (userId == Guid.Empty || !_userStatuses.TryGetValue(userId, out string? status) || status != "active")
                {
                    return Task.FromResult<IssuedAuthSession?>(null);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                var sessionId = Guid.NewGuid();
                (string refreshToken, byte[] verifier) = RefreshTokenCodec.Create(sessionId);
                var session = AuthSession.Create(sessionId, userId, verifier, now, now.Add(options.Value.RefreshTokenLifetime));
                _sessions[sessionId] = session;
                (string accessToken, DateTimeOffset accessExpiresAt) = issuer.Issue(userId, sessionId, now);
                return Task.FromResult<IssuedAuthSession?>(
                    new IssuedAuthSession(sessionId, accessToken, accessExpiresAt, refreshToken, session.RefreshExpiresAt));
            }
        }

        public Task<IssuedAuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RefreshTokenCodec.TryParse(refreshToken, out Guid sessionId, out byte[] presentedVerifier))
            {
                return Task.FromResult<IssuedAuthSession?>(null);
            }

            lock (_sync)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                if (!_sessions.TryGetValue(sessionId, out AuthSession? session)
                    || !RefreshTokenCodec.VerifierMatches(session.RefreshTokenVerifier, presentedVerifier)
                    || !session.CanRefreshAt(now)
                    || !_userStatuses.TryGetValue(session.UserId, out string? status)
                    || status != "active")
                {
                    return Task.FromResult<IssuedAuthSession?>(null);
                }

                (string nextRefreshToken, byte[] nextVerifier) = RefreshTokenCodec.Create(session.Id);
                session.RotateRefreshToken(nextVerifier, now);
                (string accessToken, DateTimeOffset accessExpiresAt) = issuer.Issue(session.UserId, session.Id, now);
                return Task.FromResult<IssuedAuthSession?>(
                    new IssuedAuthSession(session.Id, accessToken, accessExpiresAt, nextRefreshToken, session.RefreshExpiresAt));
            }
        }

        public Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_sessions.TryGetValue(sessionId, out AuthSession? session) || session.UserId != userId)
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
            if (FailureToThrow is not null)
            {
                throw FailureToThrow;
            }

            lock (_sync)
            {
                return Task.FromResult(
                    _sessions.TryGetValue(sessionId, out AuthSession? session)
                    && session.UserId == userId
                    && !session.IsRevoked
                    && _userStatuses.TryGetValue(userId, out string? status)
                    && status == "active");
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

    private sealed class TestHostHarness(
        WebApplication app,
        HttpClient client,
        InMemorySessionStore store,
        ConcurrentQueue<string> logs) : IAsyncDisposable
    {
        private int _mutationCount;

        public HttpClient Client { get; private set; } = client;
        public InMemorySessionStore Store => store;
        public IAuthSessionService Sessions => store;
        public IReadOnlyCollection<string> Logs => logs;
        public int MutationCount => Volatile.Read(ref _mutationCount);
        public CurrentUser? LastObservedUnauthenticatedUser { get; private set; }
        public TaskCompletionSource<bool> InFlightAdmitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseInFlight { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static async Task<TestHostHarness> StartAsync(TimeProvider clock, string environment = "Development")
        {
            var logs = new ConcurrentQueue<string>();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ListLoggerProvider(logs));
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Jwt:SecretKey"] = TestSecretKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "10",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["IntegrationApi:ApiKeys:0"] = "test-integration-key",
            });

            builder.Services.AddSingleton(clock);
            builder.Services.AddCurrentUserProvider();
            builder.Services.AddPersistence(builder.Configuration);
            builder.Services.AddAuthSessions(builder.Configuration);
            builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
            builder.Services.AddConfiguredAuthorization();
            builder.Services.AddSingleton<InMemorySessionStore>();
            builder.Services.AddScoped<IAuthSessionService>(sp => sp.GetRequiredService<InMemorySessionStore>());
            builder.Services.AddScoped<IAuthenticatedContextValidator>(sp => sp.GetRequiredService<InMemorySessionStore>());
            builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
                .AddApplicationPart(typeof(TestProtectedScopeController).Assembly);

            var app = builder.Build();
            var harness = new TestHostHarness(
                app,
                null!,
                app.Services.GetRequiredService<InMemorySessionStore>(),
                logs);

            app.UseCorrelationId();
            app.UseGlobalExceptionHandling();
            app.UseApiStatusCodeEnvelope();
            app.UseAuthentication();
            app.Use(async (ctx, next) =>
            {
                await next();
                if (ctx.Response.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    harness.LastObservedUnauthenticatedUser = ctx.RequestServices
                        .GetRequiredService<ICurrentUserProvider>()
                        .GetCurrentUser();
                }
            });
            app.UseAuthorization();

            app.MapPost("/test/protected/mutate", (ICurrentUserProvider currentUserProvider) =>
            {
                Interlocked.Increment(ref harness._mutationCount);
                CurrentUser current = currentUserProvider.GetCurrentUser()!;
                return Results.Json(DeliveryPilot.SharedKernel.Abstractions.Shared.ApiResponse.Ok(new
                {
                    userId = current.Id,
                    sessionId = current.SessionId,
                    tenantIds = current.TenantIds,
                    branchIds = current.BranchIds,
                    permissions = current.Permissions,
                }));
            }).RequireAuthorization();

            app.MapPost("/test/protected/in-flight", async (ICurrentUserProvider currentUserProvider) =>
            {
                harness.InFlightAdmitted.TrySetResult(true);
                await harness.ReleaseInFlight.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Interlocked.Increment(ref harness._mutationCount);
                return Results.Json(DeliveryPilot.SharedKernel.Abstractions.Shared.ApiResponse.Ok(new
                {
                    userId = currentUserProvider.GetCurrentUser()!.Id,
                }));
            }).RequireAuthorization();

            app.MapControllers();
            await app.StartAsync();
            harness.Client = app.GetTestClient();
            return harness;
        }

        public Task<HttpResponseMessage> SendProtectedMutationAsync(string bearerToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/test/protected/mutate");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}

[ApiController]
[Route("test/protected/tenants/{tenantId:guid}")]
public sealed class TestProtectedScopeController : ControllerBase
{
    [HttpPost("approve")]
    [PermissionAuthorize(Functions.Tenants, ActionType.Update, tenantIdCheck: true)]
    public IActionResult Approve(Guid tenantId) => Ok(new { tenantId });
}
