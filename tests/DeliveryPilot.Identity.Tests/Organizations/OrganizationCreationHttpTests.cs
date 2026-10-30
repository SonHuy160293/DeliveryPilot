using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationCreationHttpTests
{
    [PostgreSqlFact]
    public async Task AuthenticatedCreationReplayValidationAndDisclosureUseHttpContract()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"),
            ["IntegrationApi:ApiKeys:0"] = "test-integration-key",
            ["Jwt:SecretKey"] = "test-only-signing-key-at-least-thirty-two-bytes",
            ["Jwt:Issuer"] = "creation-test",
            ["Jwt:Audience"] = "creation-test-client",
            ["Jwt:AccessTokenExpirationMinutes"] = "10",
            ["Jwt:RefreshTokenExpirationDays"] = "7",
        });
        builder.Services.AddCurrentUserProvider().AddApplication().AddInfrastructure(builder.Configuration)
            .AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment)
            .AddConfiguredAuthorization().AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(OrganizationsController).Assembly);
        await using var app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = User.Create($"{Guid.NewGuid():N}@example.test", "Creator", status: "active");
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.Users.Add(user);
        db.AuthSessions.Add(AuthSession.Create(sessionId, user.Id, new byte[32], now, now.AddDays(1)));
        await db.SaveChangesAsync();
        string token = scope.ServiceProvider.GetRequiredService<JwtAccessTokenIssuer>().Issue(user.Id, sessionId, now).Token;
        using var client = app.GetTestClient();

        using (var unauthenticated = await PostAsync(client, "{\"name\":\"Name\"}", Guid.NewGuid().ToString()))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        foreach (string body in new[] { "{}", "{\"name\":\"   \"}", "{\"name\":\"Name\",\"userId\":\"00000000-0000-0000-0000-000000000001\"}", "{\"name\":\"Name\",\"role\":\"Owner\"}" })
        {
            using HttpResponseMessage invalid = await PostAsync(client, body, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        using (var invalidKey = await PostAsync(client, "{\"name\":\"Name\"}", "bad-key"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidKey.StatusCode);
        }

        string key = Guid.NewGuid().ToString();
        using HttpResponseMessage created = await PostAsync(client, "{\"name\":\"  North  \"}", key);
        string createdBody = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        using JsonDocument createdJson = JsonDocument.Parse(createdBody);
        JsonElement data = createdJson.RootElement.GetProperty("data");
        Assert.Equal("North", data.GetProperty("name").GetString());
        Assert.Equal("creationSnapshot", data.GetProperty("resultKind").GetString());
        Assert.Equal("Owner", data.GetProperty("roles")[0].GetString());

        using HttpResponseMessage replay = await PostAsync(client, "{\"name\":\"North\"}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        using JsonDocument replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(data.GetRawText(), replayJson.RootElement.GetProperty("data").GetRawText());
        using HttpResponseMessage conflict = await PostAsync(client, "{\"name\":\"Different\"}", key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using JsonDocument conflictJson = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("Organization.CreationAttemptConflict", conflictJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        User unrelated = User.Create($"{Guid.NewGuid():N}@example.test", "Other", status: "active");
        Guid otherSession = Guid.NewGuid();
        db.Users.Add(unrelated);
        db.AuthSessions.Add(AuthSession.Create(otherSession, unrelated.Id, new byte[32], now, now.AddDays(1)));
        await db.SaveChangesAsync();
        string otherToken = scope.ServiceProvider.GetRequiredService<JwtAccessTokenIssuer>().Issue(unrelated.Id, otherSession, now).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using HttpResponseMessage otherResult = await PostAsync(client, "{\"name\":\"North\"}", key);
        Assert.Equal(HttpStatusCode.Created, otherResult.StatusCode);
        using JsonDocument otherJson = JsonDocument.Parse(await otherResult.Content.ReadAsStringAsync());
        Assert.NotEqual(data.GetProperty("organizationId").GetGuid(), otherJson.RootElement.GetProperty("data").GetProperty("organizationId").GetGuid());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid membershipId = data.GetProperty("membershipId").GetGuid();
        await db.OrganizationMemberships.Where(membership => membership.Id == membershipId)
            .ExecuteUpdateAsync(update => update.SetProperty(membership => membership.Status, "removed"));
        using HttpResponseMessage denied = await PostAsync(client, "{\"name\":\"North\"}", key);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using JsonDocument deniedJson = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal("Organization.AccessDenied", deniedJson.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain(data.GetProperty("organizationId").GetString()!, deniedJson.RootElement.ToString(), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string json, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organizations")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
