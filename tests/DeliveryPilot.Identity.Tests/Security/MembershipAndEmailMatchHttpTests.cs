using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
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
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class MembershipAndEmailMatchHttpTests
{
    private const string MembershipPath = "/api/v1/integrations/authority/membership-checks";
    private const string EmailPath = "/api/v1/integrations/authority/email-matches";
    private const string Key = "isolated-membership-email-test-key";

    [Fact]
    public async Task GatewayDoesNotPublishEitherRoute()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DeliveryPilot.Gateway/proxy.json"));
        using JsonDocument config = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        foreach (JsonProperty route in config.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject())
        {
            JsonElement match = route.Value.GetProperty("Match");
            if (match.TryGetProperty("Methods", out JsonElement methods) && !methods.EnumerateArray().Any(method => method.GetString() == "POST"))
            {
                continue;
            }

            var matcher = new TemplateMatcher(TemplateParser.Parse(match.GetProperty("Path").GetString()!), new RouteValueDictionary());
            Assert.False(matcher.TryMatch("/identity" + MembershipPath, new RouteValueDictionary()));
            Assert.False(matcher.TryMatch("/identity" + EmailPath, new RouteValueDictionary()));
        }
    }

    [Fact]
    public async Task AuthenticationAndValidationRejectBeforeDatabaseAccess()
    {
        await using WebApplication app = await StartAsync();
        using HttpClient client = app.GetTestClient();
        Guid id = Guid.NewGuid();
        foreach (string path in new[] { MembershipPath, EmailPath })
        {
            using HttpResponseMessage missing = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
            client.DefaultRequestHeaders.Add("X-Integration-Key", "wrong");
            using HttpResponseMessage wrong = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Integration-Key");
        }

        client.DefaultRequestHeaders.Add("X-Integration-Key", Key);
        object[] invalidMembers = [new { userIds = new[] { id } }, new { organizationId = Guid.Empty, userIds = new[] { id } }, new { organizationId = id }, new { organizationId = id, userIds = Array.Empty<Guid>() }, new { organizationId = id, userIds = new[] { Guid.Empty } }, new { organizationId = id, userIds = Enumerable.Repeat(id, 101).ToArray() }, new { organizationId = id, userIds = new[] { id }, extra = true }];
        foreach (object request in invalidMembers)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(MembershipPath, request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        object[] invalidEmails = [new { email = "secret@example.test" }, new { userId = Guid.Empty, email = "secret@example.test" }, new { userId = id }, new { userId = id, email = " " }, new { userId = id, email = " ab " }, new { userId = id, email = new string('a', 321) }, new { userId = id, email = "secret@example.test", extra = true }];
        foreach (object request in invalidEmails)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(EmailPath, request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain("secret@example.test", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [PostgreSqlFact]
    public async Task ChecksReadCurrentStateInOneCommandWithoutDisclosureOrTransactions()
    {
        var observer = new ReadObserver();
        var logs = new CapturedLogs();
        await using WebApplication app = await StartAsync(observer, logs);
        using HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Integration-Key", Key);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        User active = User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Active", status: "active");
        User inactive = User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Inactive", status: "suspended");
        User removed = User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Removed", status: "active");
        User outsider = User.Create(Guid.NewGuid().ToString("N") + "@example.test", "Outsider", status: "active");
        Organization organization = Organization.Create("Test workspace", active.Id, now);
        Organization other = Organization.Create("Other workspace", outsider.Id, now);
        OrganizationMembership first = OrganizationMembership.Create(organization.Id, active.Id, active.Id, now);
        OrganizationMembership second = OrganizationMembership.Create(organization.Id, inactive.Id, active.Id, now);
        OrganizationMembership third = OrganizationMembership.Create(organization.Id, removed.Id, active.Id, now);
        OrganizationMembership fourth = OrganizationMembership.Create(other.Id, outsider.Id, outsider.Id, now);
        third.Remove(active.Id, "test", now, []);
        db.Users.AddRange(active, inactive, removed, outsider);
        db.Organizations.AddRange(organization, other);
        db.OrganizationMemberships.AddRange(first, second, third, fourth);
        await db.SaveChangesAsync();
        Guid unknown = Guid.NewGuid();
        try
        {
            observer.Reset();
            using HttpResponseMessage response = await client.PostAsJsonAsync(MembershipPath, new { organizationId = organization.Id, userIds = new[] { removed.Id, active.Id, inactive.Id, outsider.Id, unknown, active.Id } });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement data = body.RootElement.GetProperty("data");
            Assert.Equal(2, data.EnumerateObject().Count());
            JsonElement[] members = data.GetProperty("members").EnumerateArray().ToArray();
            Assert.Equal(new[] { removed.Id, active.Id, inactive.Id, outsider.Id, unknown }, members.Select(x => x.GetProperty("userId").GetGuid()));
            Assert.Equal(new[] { false, true, false, false, false }, members.Select(x => x.GetProperty("isActiveMember").GetBoolean()));
            Assert.All(members, x => Assert.Equal(2, x.EnumerateObject().Count()));
            Assert.Single(observer.Reads);
            Assert.False(observer.HadTransaction);

            using (HttpResponseMessage missingOrganization = await client.PostAsJsonAsync(MembershipPath, new { organizationId = Guid.NewGuid(), userIds = new[] { active.Id } }))
            {
                using JsonDocument missingBody = JsonDocument.Parse(await missingOrganization.Content.ReadAsStringAsync());
                Assert.False(missingBody.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("isActiveMember").GetBoolean());
            }

            await db.Organizations.Where(x => x.Id == organization.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "suspended"));
            using (HttpResponseMessage inactiveOrganization = await client.PostAsJsonAsync(MembershipPath, new { organizationId = organization.Id, userIds = new[] { active.Id } }))
            {
                using JsonDocument inactiveBody = JsonDocument.Parse(await inactiveOrganization.Content.ReadAsStringAsync());
                Assert.False(inactiveBody.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("isActiveMember").GetBoolean());
            }

            await db.Organizations.Where(x => x.Id == organization.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "active"));
            await db.OrganizationMemberships.Where(x => x.Id == first.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "suspended"));
            using (HttpResponseMessage suspended = await client.PostAsJsonAsync(MembershipPath, new { organizationId = organization.Id, userIds = new[] { active.Id } }))
            {
                using JsonDocument suspendedBody = JsonDocument.Parse(await suspended.Content.ReadAsStringAsync());
                Assert.False(suspendedBody.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("isActiveMember").GetBoolean());
            }

            using (HttpResponseMessage maximum = await client.PostAsJsonAsync(MembershipPath, new { organizationId = organization.Id, userIds = Enumerable.Repeat(active.Id, 100).ToArray() }))
            {
                Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
            }

            foreach ((Guid userId, string email, bool expected) in new[] { (active.Id, active.PrimaryEmail, true), (active.Id, "  " + active.PrimaryEmail.ToUpperInvariant() + "  ", true), (active.Id, outsider.PrimaryEmail, false), (inactive.Id, inactive.PrimaryEmail, false), (unknown, active.PrimaryEmail, false) })
            {
                observer.Reset();
                using HttpResponseMessage match = await client.PostAsJsonAsync(EmailPath, new { userId, email });
                Assert.Equal(HttpStatusCode.OK, match.StatusCode);
                string json = await match.Content.ReadAsStringAsync();
                Assert.DoesNotContain(email.Trim(), json, StringComparison.OrdinalIgnoreCase);
                using JsonDocument result = JsonDocument.Parse(json);
                Assert.Equal(expected, result.RootElement.GetProperty("data").GetProperty("matches").GetBoolean());
                Assert.Equal(2, result.RootElement.GetProperty("data").EnumerateObject().Count());
                Assert.Single(observer.Reads);
                Assert.False(observer.HadTransaction);
            }

            Assert.DoesNotContain(active.PrimaryEmail, string.Join("\n", logs.Messages), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(outsider.PrimaryEmail, string.Join("\n", logs.Messages), StringComparison.OrdinalIgnoreCase);

            foreach (bool timeout in new[] { false, true })
            {
                observer.Fail = true;
                observer.Timeout = timeout;
                using HttpResponseMessage unavailableMembership = await client.PostAsJsonAsync(MembershipPath, new { organizationId = organization.Id, userIds = new[] { active.Id } });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableMembership.StatusCode);
                using HttpResponseMessage unavailableEmail = await client.PostAsJsonAsync(EmailPath, new { userId = active.Id, email = active.PrimaryEmail });
                Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableEmail.StatusCode);
            }
        }
        finally
        {
            observer.Fail = false;
            Guid[] organizations = [organization.Id, other.Id];
            Guid[] users = [active.Id, inactive.Id, removed.Id, outsider.Id];
            await db.OrganizationMemberships.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.Organizations.Where(x => organizations.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Users.Where(x => users.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }

    private static async Task<WebApplication> StartAsync(ReadObserver? observer = null, CapturedLogs? logs = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES") ?? "Host=127.0.0.1;Port=1;Database=unused;Username=unused", ["IntegrationApi:ApiKeys:0"] = Key });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        if (observer is not null)
        {
            builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(observer));
        }

        builder.Services.AddConfiguredAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(AuthorityController).Assembly);
        WebApplication app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class ReadObserver : DbCommandInterceptor
    {
        internal List<string> Reads { get; } = [];
        internal bool HadTransaction { get; private set; }
        internal bool Fail { get; set; }
        internal bool Timeout { get; set; }

        internal void Reset() { Reads.Clear(); HadTransaction = false; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal))
            {
                if (Fail)
                {
                    if (Timeout) { throw new TimeoutException("private timeout"); }
                    throw new NpgsqlException("private store error");
                }
                Reads.Add(command.CommandText);
                HadTransaction |= command.Transaction is not null;
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
