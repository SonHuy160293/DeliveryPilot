using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LogoutSession;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.RefreshSession;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Users;

/// <summary>
/// TASK-DP-002-005: refresh and logout through real CQRS, HTTP and PostgreSQL.
/// D-006: logout wins a race with refresh and always answers 204.
/// </summary>
public sealed class SessionRefreshLogoutHttpTests
{
    private const string TestSecretKey = "test-only-hs256-signing-key-with-at-least-32-bytes";
    private const string TestIssuer = "DeliveryPilot.Identity.Tests";
    private const string TestAudience = "DeliveryPilot.Tests";
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string LogoutPath = "/api/v1/auth/logout";
    private const string ProtectedPath = "/api/v1/me/organizations";
    private const string Password = " CorrectHorse!234 ";

    [Fact]
    public void CommandDiagnosticsDoNotExposeRefreshToken()
    {
        const string secret = "0123456789abcdef0123456789abcdef.secret-value";
        Assert.DoesNotContain(secret, new RefreshSessionCommand(secret).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, new LogoutSessionCommand(Guid.NewGuid(), Guid.NewGuid(), secret).ToString(), StringComparison.Ordinal);
    }

    [PostgreSqlFact]
    public async Task RefreshRotatesCookieKeepsDeadlineAndRejectsOldTokenWithoutEndingSession()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser user = await RegisterAsync(client);
        try
        {
            LoginSession first = await LoginAsync(client, user);
            Assert.Equal(HttpStatusCode.OK, await GetProtectedStatusAsync(client, first.AccessToken));

            using HttpResponseMessage refresh = await PostAsync(client, RefreshPath, cookie: first.RefreshCookie);
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            Assert.Equal("no-store", refresh.Headers.CacheControl?.ToString());
            string setCookie = Assert.Single(refresh.Headers.GetValues("Set-Cookie"));
            string rotatedCookie = ExtractCookieValue(setCookie);
            Assert.NotEqual(first.RefreshCookie, rotatedCookie);
            Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ExtractExpires(first.SetCookieHeader), ExtractExpires(setCookie));

            string body = await refresh.Content.ReadAsStringAsync();
            Assert.DoesNotContain(rotatedCookie, body, StringComparison.Ordinal);
            Assert.DoesNotContain(first.RefreshCookie, body, StringComparison.Ordinal);
            using JsonDocument json = JsonDocument.Parse(body);
            JsonElement data = json.RootElement.GetProperty("data");
            Assert.Equal(user.Id, data.GetProperty("userId").GetGuid());
            Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
            string refreshedAccessToken = data.GetProperty("accessToken").GetString()!;
            Assert.Equal(HttpStatusCode.OK, await GetProtectedStatusAsync(client, refreshedAccessToken));

            using HttpResponseMessage reuse = await PostAsync(client, RefreshPath, cookie: first.RefreshCookie);
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
            AssertCookieCleared(reuse);
            Assert.Equal("Authentication.InvalidRefreshToken", await ReadErrorCodeAsync(reuse));

            using HttpResponseMessage current = await PostAsync(client, RefreshPath, cookie: rotatedCookie);
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        }
        finally
        {
            await CleanupAsync(app, user);
        }
    }

    [PostgreSqlFact]
    public async Task RefreshWithoutValidCookieOrForInactiveUserIsGeneric401()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser user = await RegisterAsync(client);
        try
        {
            using HttpResponseMessage missing = await PostAsync(client, RefreshPath);
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
            AssertCookieCleared(missing);

            using HttpResponseMessage malformed = await PostAsync(client, RefreshPath, cookie: "not-a-token");
            Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);

            LoginSession session = await LoginAsync(client, user);
            using (IServiceScope scope = app.Services.CreateScope())
            {
                IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "suspended"));
            }

            using HttpResponseMessage inactive = await PostAsync(client, RefreshPath, cookie: session.RefreshCookie);
            Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
            Assert.Equal(await ReadErrorCodeAsync(missing), await ReadErrorCodeAsync(inactive));
        }
        finally
        {
            await CleanupAsync(app, user);
        }
    }

    [PostgreSqlFact]
    public async Task LogoutWithBearerEndsOnlyThatSessionAndIsIdempotent()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser user = await RegisterAsync(client);
        try
        {
            LoginSession ended = await LoginAsync(client, user);
            LoginSession other = await LoginAsync(client, user);

            using HttpResponseMessage logout = await PostAsync(client, LogoutPath, ended.AccessToken, ended.RefreshCookie);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            Assert.Equal("no-store", logout.Headers.CacheControl?.ToString());
            AssertCookieCleared(logout);

            Assert.Equal(HttpStatusCode.Unauthorized, await GetProtectedStatusAsync(client, ended.AccessToken));
            using HttpResponseMessage refreshEnded = await PostAsync(client, RefreshPath, cookie: ended.RefreshCookie);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshEnded.StatusCode);

            Assert.Equal(HttpStatusCode.OK, await GetProtectedStatusAsync(client, other.AccessToken));
            using HttpResponseMessage refreshOther = await PostAsync(client, RefreshPath, cookie: other.RefreshCookie);
            Assert.Equal(HttpStatusCode.OK, refreshOther.StatusCode);

            using HttpResponseMessage repeat = await PostAsync(client, LogoutPath, ended.AccessToken, ended.RefreshCookie);
            Assert.Equal(HttpStatusCode.NoContent, repeat.StatusCode);
            AssertCookieCleared(repeat);
            using HttpResponseMessage anonymous = await PostAsync(client, LogoutPath);
            Assert.Equal(HttpStatusCode.NoContent, anonymous.StatusCode);
            using HttpResponseMessage malformed = await PostAsync(client, LogoutPath, "not-a-jwt", "not-a-token");
            Assert.Equal(HttpStatusCode.NoContent, malformed.StatusCode);

            using IServiceScope scope = app.Services.CreateScope();
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            List<AuthSession> sessions = await db.AuthSessions.AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync();
            Assert.Single(sessions, x => x.IsRevoked);
            Assert.Single(sessions, x => !x.IsRevoked);
            Assert.NotNull(await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.Id));
        }
        finally
        {
            await CleanupAsync(app, user);
        }
    }

    [PostgreSqlFact]
    public async Task LogoutWithRefreshCookieOnlyEndsTheSession()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser user = await RegisterAsync(client);
        try
        {
            LoginSession session = await LoginAsync(client, user);

            using HttpResponseMessage logout = await PostAsync(client, LogoutPath, cookie: session.RefreshCookie);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            AssertCookieCleared(logout);

            Assert.Equal(HttpStatusCode.Unauthorized, await GetProtectedStatusAsync(client, session.AccessToken));
            using HttpResponseMessage refresh = await PostAsync(client, RefreshPath, cookie: session.RefreshCookie);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        finally
        {
            await CleanupAsync(app, user);
        }
    }

    [PostgreSqlFact]
    public async Task LogoutCannotEndAnotherUsersSessionOrUseARotatedCookie()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser alice = await RegisterAsync(client);
        TestUser bob = await RegisterAsync(client);
        try
        {
            LoginSession aliceSession = await LoginAsync(client, alice);
            LoginSession bobSession = await LoginAsync(client, bob);

            using HttpResponseMessage rotate = await PostAsync(client, RefreshPath, cookie: bobSession.RefreshCookie);
            Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);

            // Alice presents her own bearer with Bob's old, rotated-away cookie: only Alice's session ends.
            using HttpResponseMessage logout = await PostAsync(client, LogoutPath, aliceSession.AccessToken, bobSession.RefreshCookie);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, await GetProtectedStatusAsync(client, aliceSession.AccessToken));
            Assert.Equal(HttpStatusCode.OK, await GetProtectedStatusAsync(client, bobSession.AccessToken));

            // A rotated-away cookie alone cannot end Bob's session either.
            using HttpResponseMessage staleOnly = await PostAsync(client, LogoutPath, cookie: bobSession.RefreshCookie);
            Assert.Equal(HttpStatusCode.NoContent, staleOnly.StatusCode);
            Assert.Equal(HttpStatusCode.OK, await GetProtectedStatusAsync(client, bobSession.AccessToken));
        }
        finally
        {
            await CleanupAsync(app, alice);
            await CleanupAsync(app, bob);
        }
    }

    [PostgreSqlFact]
    public async Task ConcurrentRefreshAndLogoutAlwaysEndTheSession()
    {
        await using WebApplication app = await StartAppAsync();
        using HttpClient client = app.GetTestClient();
        TestUser user = await RegisterAsync(client);
        try
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                LoginSession session = await LoginAsync(client, user);
                Task<HttpResponseMessage> refreshTask = PostAsync(client, RefreshPath, cookie: session.RefreshCookie);
                Task<HttpResponseMessage> logoutTask = PostAsync(client, LogoutPath, session.AccessToken, session.RefreshCookie);
                using HttpResponseMessage refresh = await refreshTask;
                using HttpResponseMessage logout = await logoutTask;

                Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
                Assert.Contains(refresh.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
                Assert.Equal(HttpStatusCode.Unauthorized, await GetProtectedStatusAsync(client, session.AccessToken));

                if (refresh.StatusCode == HttpStatusCode.OK)
                {
                    string rotated = ExtractCookieValue(Assert.Single(refresh.Headers.GetValues("Set-Cookie")));
                    using JsonDocument json = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
                    string refreshedAccess = json.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
                    Assert.Equal(HttpStatusCode.Unauthorized, await GetProtectedStatusAsync(client, refreshedAccess));
                    using HttpResponseMessage reuse = await PostAsync(client, RefreshPath, cookie: rotated);
                    Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
                }
            }

            using IServiceScope scope = app.Services.CreateScope();
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.All(await db.AuthSessions.AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync(), x => Assert.True(x.IsRevoked));
        }
        finally
        {
            await CleanupAsync(app, user);
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? bearer = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{AuthController.RefreshTokenCookieName}={cookie}");
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpStatusCode> GetProtectedStatusAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<TestUser> RegisterAsync(HttpClient client)
    {
        string email = $"session-{Guid.NewGuid():N}@example.test";
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "Session User", email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new TestUser(json.RootElement.GetProperty("data").GetProperty("userId").GetGuid(), email);
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, TestUser user)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string accessToken = json.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        return new LoginSession(accessToken, ExtractCookieValue(setCookie), setCookie);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private static void AssertCookieCleared(HttpResponseMessage response)
    {
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(AuthController.RefreshTokenCookieName + "=;", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCookieValue(string setCookieHeader)
    {
        string prefix = AuthController.RefreshTokenCookieName + "=";
        Assert.StartsWith(prefix, setCookieHeader, StringComparison.Ordinal);
        int end = setCookieHeader.IndexOf(';', StringComparison.Ordinal);
        return end < 0 ? setCookieHeader[prefix.Length..] : setCookieHeader[prefix.Length..end];
    }

    private static string ExtractExpires(string setCookieHeader)
    {
        const string marker = "expires=";
        int start = setCookieHeader.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0);
        start += marker.Length;
        int end = setCookieHeader.IndexOf(';', start);
        return end < 0 ? setCookieHeader[start..] : setCookieHeader[start..end];
    }

    private static async Task CleanupAsync(WebApplication app, TestUser user)
    {
        Guid userId = user.Id;
        using IServiceScope scope = app.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.AuthSessions.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await db.PasswordCredentials.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync();
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")
                ?? "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["IntegrationApi:ApiKeys:0"] = "isolated-session-test-key",
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

        WebApplication app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed record TestUser(Guid Id, string Email);

    private sealed record LoginSession(string AccessToken, string RefreshCookie, string SetCookieHeader);
}
