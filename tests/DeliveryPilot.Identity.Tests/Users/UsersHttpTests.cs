using System.Net;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
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
using Xunit;

namespace DeliveryPilot.Identity.Tests.Users;

public sealed class UsersHttpTests
{
    [PostgreSqlFact]
    public async Task UserLookupKeepsAuthenticationValidationAndFailureResponses()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"),
            ["IntegrationApi:ApiKeys:0"] = "isolated-user-lookup-test-key",
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        builder.Services.AddConfiguredAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(UsersController).Assembly);
        await using var app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = User.Create(Guid.NewGuid().ToString("N") + "@example.test", "User lookup");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        string path = "/api/v1/integrations/users/" + user.Id;
        try
        {
            using var unauthorized = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            client.DefaultRequestHeaders.Add("X-Integration-Key", "invalid-key");
            using var invalidKey = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidKey.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Integration-Key");
            client.DefaultRequestHeaders.Add("X-Integration-Key", "isolated-user-lookup-test-key");
            using var success = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            using var body = JsonDocument.Parse(await success.Content.ReadAsStringAsync());
            Assert.Equal(user.Id, body.RootElement.GetProperty("data").GetProperty("id").GetGuid());
            Assert.Equal("User lookup", body.RootElement.GetProperty("data").GetProperty("displayName").GetString());
            Assert.False(body.RootElement.GetProperty("data").TryGetProperty("primaryEmail", out _));

            using var missing = await client.GetAsync("/api/v1/integrations/users/" + Guid.CreateVersion7());
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
            Assert.Equal("User.NotFound", missingBody.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(missingBody.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
            using var invalid = await client.GetAsync("/api/v1/integrations/users/" + Guid.Empty);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally
        {
            await context.Users.Where(item => item.Id == user.Id).ExecuteDeleteAsync();
        }
    }
}
