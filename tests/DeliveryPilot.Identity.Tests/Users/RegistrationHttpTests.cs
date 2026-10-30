using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Contracts.Auth;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Users;

public sealed class RegistrationHttpTests
{
    [Fact]
    public async Task PasswordHashIsSaltedVerifiableAndDiagnosticsDoNotExposeSecret()
    {
        const string password = " secret with spaces ";
        var user = User.Create("alice@example.test", "Alice");
        var service = new PasswordHashService();
        string first = await service.HashAsync(user, password, CancellationToken.None);
        string second = await service.HashAsync(user, password, CancellationToken.None);
        Assert.NotEqual(first, second);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(user, first, password));
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<User>().VerifyHashedPassword(user, first, password.Trim()));
        Assert.DoesNotContain(password, new RegisterUserCommand("Alice", "alice@example.test", password).ToString());
        Assert.DoesNotContain(password, new RegisterUserRequest("Alice", "alice@example.test", password).ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"displayName\":\"Alice\",\"email\":\"bad\",\"password\":\"secret\"}")]
    [InlineData("{\"displayName\":\"Alice\",\"email\":\"alice@example.test\",\"password\":\"  \"}")]
    [InlineData("{\"displayName\":\"Alice\",\"email\":\"alice@example.test\",\"password\":\"secret\",\"role\":\"admin\"}")]
    [InlineData("{\"displayName\":42}")]
    [InlineData("{")]
    public async Task InvalidRequestsAreRejectedBeforeDatabaseAccess(string json)
    {
        await using var app = await StartApp();
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/v1/auth/register", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body);
    }

    [Fact]
    public async Task ValidatorEnforcesTrimmedFieldBoundaries()
    {
        var validator = new RegisterUserValidator();
        string validEmail = new string('a', 307) + "@example.test";
        await validator.ValidateAsync(new RegisterUserCommand(" " + new string('n', 160) + " ", " " + validEmail + " ", " password "), CancellationToken.None);
        await Assert.ThrowsAsync<DeliveryPilot.SharedKernel.Abstractions.Message.CqrsValidationException>(() =>
            validator.ValidateAsync(new RegisterUserCommand(new string('n', 161), validEmail, "password"), CancellationToken.None));
        await Assert.ThrowsAsync<DeliveryPilot.SharedKernel.Abstractions.Message.CqrsValidationException>(() =>
            validator.ValidateAsync(new RegisterUserCommand("name", "a" + validEmail, "password"), CancellationToken.None));
    }

    [PostgreSqlFact]
    public async Task ConcurrentRegistrationCreatesOneAccountAndPreservesCredential()
    {
        await using var app = await StartApp();
        using var client = app.GetTestClient();
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        var attempts = Enumerable.Range(0, 2).Select(i => client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = i == 0 ? " Alice " : "Other",
            email = i == 0 ? " " + email.ToUpperInvariant() + " " : email,
            password = i == 0 ? " first password " : "second password",
        }));
        HttpResponseMessage[] responses = await Task.WhenAll(attempts);
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            using var error = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
            Assert.Equal("Registration.EmailExists", error.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal("This email is exist", error.RootElement.GetProperty("error").GetProperty("message").GetString());
            using var success = JsonDocument.Parse(await responses.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadAsStringAsync());
            Guid id = success.RootElement.GetProperty("data").GetProperty("userId").GetGuid();
            Assert.Single(success.RootElement.GetProperty("data").EnumerateObject());
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            try
            {
                var user = await db.Users.SingleAsync(x => x.Id == id);
                var credential = await db.PasswordCredentials.SingleAsync(x => x.UserId == id);
                Assert.Equal(email, user.PrimaryEmail);
                Assert.Equal("active", user.Status);
                Assert.Null(user.EmailVerifiedAt);
                Assert.Null(user.LastLoginAt);
                string expectedPassword = user.DisplayName == "Alice" ? " first password " : "second password";
                Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(user, credential.PasswordHash, expectedPassword));
                using var retry = await client.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "Changed", email, password = "changed" });
                Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
                Assert.Equal(credential.PasswordHash, (await db.PasswordCredentials.AsNoTracking().SingleAsync(x => x.UserId == id)).PasswordHash);
            }
            finally
            {
                await db.PasswordCredentials.Where(x => x.UserId == id).ExecuteDeleteAsync();
                await db.Users.Where(x => x.Id == id).ExecuteDeleteAsync();
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static async Task<WebApplication> StartApp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES") ?? "Host=localhost;Database=unused;Username=unused;Password=unused",
            ["IntegrationApi:ApiKeys:0"] = "isolated-registration-test-key",
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        builder.Services.AddConfiguredAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(AuthController).Assembly);
        var app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }
}
