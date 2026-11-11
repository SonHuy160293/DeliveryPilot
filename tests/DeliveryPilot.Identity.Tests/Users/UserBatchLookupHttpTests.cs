using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Users;

public sealed class UserBatchLookupHttpTests
{
    private const string Path = "/api/v1/integrations/users/lookup";
    private const string Key = "isolated-batch-user-lookup-key";

    [Fact]
    public async Task GatewayDoesNotPublishTheInternalLookup()
    {
        string configPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DeliveryPilot.Gateway/proxy.json"));
        using JsonDocument config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        foreach (JsonProperty route in config.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject())
        {
            JsonElement match = route.Value.GetProperty("Match");
            if (match.TryGetProperty("Methods", out JsonElement methods) && !methods.EnumerateArray().Any(method => method.GetString() == "POST"))
            {
                continue;
            }

            var matcher = new TemplateMatcher(TemplateParser.Parse(match.GetProperty("Path").GetString()!), new RouteValueDictionary());
            Assert.False(matcher.TryMatch("/identity" + Path, new RouteValueDictionary()));
        }
    }

    [Fact]
    public async Task BatchValidatesInputBeforeLookupAndRequiresAnIntegrationKey()
    {
        var store = new FakeLookupStore();
        await using WebApplication app = await StartAsync(store);
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage missingKey = await client.PostAsJsonAsync(Path, new { userIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.Unauthorized, missingKey.StatusCode);
        client.DefaultRequestHeaders.Add("X-Integration-Key", "wrong-key");
        using HttpResponseMessage wrongKey = await client.PostAsJsonAsync(Path, new { userIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Integration-Key");
        client.DefaultRequestHeaders.Add("X-Integration-Key", Key);

        Guid[][] invalid = [[], [Guid.Empty], Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray()];
        foreach (Guid[] ids in invalid)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(Path, new { userIds = ids });
            await AssertErrorAsync(response, HttpStatusCode.BadRequest);
        }

        using HttpResponseMessage absent = await client.PostAsJsonAsync(Path, new { });
        await AssertErrorAsync(absent, HttpStatusCode.BadRequest);
        using var unknownBody = new StringContent("{\"userIds\":[\"00000000-0000-0000-0000-000000000001\"],\"email\":\"hidden@example.test\"}", Encoding.UTF8, "application/json");
        using HttpResponseMessage unknown = await client.PostAsync(Path, unknownBody);
        await AssertErrorAsync(unknown, HttpStatusCode.BadRequest);
        Assert.Empty(store.Requests);

        Guid[] maximum = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        using HttpResponseMessage accepted = await client.PostAsJsonAsync(Path, new { userIds = maximum });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(maximum, Assert.Single(store.Requests));
        Guid duplicate = Guid.NewGuid();
        using HttpResponseMessage collapsed = await client.PostAsJsonAsync(Path, new { userIds = new[] { duplicate, duplicate } });
        Assert.Equal(HttpStatusCode.OK, collapsed.StatusCode);
        Assert.Equal(duplicate, Assert.Single(store.Requests[1]));
    }

    [PostgreSqlFact]
    public async Task BatchReturnsKnownUsersOfEveryStatusInOneProjectionWithoutEmailOrATransaction()
    {
        var commands = new LookupCommandInterceptor();
        await using WebApplication app = await StartAsync(interceptor: commands);
        using HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Integration-Key", Key);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User[] users = [
            User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Active user", "https://example.test/avatar", status: "active"),
            User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Suspended user", status: "suspended"),
            User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Deleted user", status: "deleted"),
            User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Pending user")];
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        Guid[] ids = users.Select(user => user.Id).ToArray();
        try
        {
            commands.Reads.Clear();
            using HttpResponseMessage response = await client.PostAsJsonAsync(Path, new { userIds = ids.Concat([ids[0], Guid.NewGuid()]).ToArray() });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement[] found = body.RootElement.GetProperty("data").GetProperty("users").EnumerateArray().ToArray();
            Assert.Equal(users.Length, found.Length);
            foreach (User user in users)
            {
                JsonElement item = Assert.Single(found, item => item.GetProperty("id").GetGuid() == user.Id);
                Assert.Equal(user.DisplayName, item.GetProperty("displayName").GetString());
                Assert.Equal(user.Status, item.GetProperty("status").GetString());
                Assert.Equal(user.AvatarUrl, item.GetProperty("avatarUrl").GetString());
                Assert.Equal(4, item.EnumerateObject().Count());
                Assert.DoesNotContain(user.PrimaryEmail, body.RootElement.GetRawText(), StringComparison.Ordinal);
            }

            string sql = Assert.Single(commands.Reads);
            Assert.DoesNotContain("PrimaryEmail", sql, StringComparison.Ordinal);
            Assert.False(commands.HadTransaction);
            commands.FailReads = true;
            using HttpResponseMessage unavailable = await client.PostAsJsonAsync(Path, new { userIds = ids });
            await AssertErrorAsync(unavailable, HttpStatusCode.ServiceUnavailable);
            Assert.DoesNotContain("simulated-private-storage-details", await unavailable.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            commands.FailReads = false;
            await context.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
        }
    }

    private static async Task<WebApplication> StartAsync(FakeLookupStore? store = null, LookupCommandInterceptor? interceptor = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES") ?? "Host=127.0.0.1;Database=unused;Username=unused",
            ["IntegrationApi:ApiKeys:0"] = Key,
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        if (store is not null)
        {
            builder.Services.RemoveAll<IUserLookupStore>();
            builder.Services.AddSingleton<IUserLookupStore>(store);
        }

        if (interceptor is not null)
        {
            builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(interceptor));
        }

        builder.Services.AddConfiguredAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(UsersController).Assembly);
        WebApplication app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
    }

    private sealed class FakeLookupStore : IUserLookupStore
    {
        internal List<Guid[]> Requests { get; } = [];

        public Task<IReadOnlyList<UserIntegrationDto>> LookupAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        {
            Requests.Add(userIds.ToArray());
            return Task.FromResult<IReadOnlyList<UserIntegrationDto>>([]);
        }
    }

    private sealed class LookupCommandInterceptor : DbCommandInterceptor
    {
        internal List<string> Reads { get; } = [];
        internal bool HadTransaction { get; private set; }
        internal bool FailReads { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal))
            {
                if (FailReads)
                {
                    throw new NpgsqlException("simulated-private-storage-details");
                }

                Reads.Add(command.CommandText);
                HadTransaction |= command.Transaction is not null;
            }

            return ValueTask.FromResult(result);
        }
    }
}
