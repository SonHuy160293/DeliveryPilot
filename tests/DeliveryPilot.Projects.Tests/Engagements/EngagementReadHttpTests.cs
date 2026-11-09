using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryPilot.Projects.Tests.Engagements;

[Collection("Projects PostgreSQL")]
[SuppressMessage("Design", "CA1001", Justification = "xUnit disposes the factory through IAsyncLifetime.")]
public sealed class EngagementReadHttpTests : IAsyncLifetime
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly Guid _otherOrganization = Guid.NewGuid();
    private readonly Guid _creator = Guid.NewGuid();
    private readonly Guid _otherCreator = Guid.NewGuid();
    private readonly AuthorityClient _authority = new();
    private readonly ReadFactory _factory;
    private Guid _caller;
    private Guid _owned;
    private Guid _otherOwned;
    private Guid _foreign;
    private string _verifier = string.Empty;

    public EngagementReadHttpTests()
    {
        _caller = _creator;
        _factory = new ReadFactory(_authority);
    }

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
        DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Engagement own = Engagement.Create(_organization, _creator, now, "Own client", "OWN", new DateOnly(2026, 1, 1), contractAmount: 100m, contractCurrency: "USD", plannedEffortPersonDays: 4.5m);
        Engagement other = Engagement.Create(_organization, _otherCreator, now, "Other client", "OTHER", new DateOnly(2026, 1, 1));
        Engagement third = Engagement.Create(_organization, _otherCreator, now, "Third client", "THIRD", new DateOnly(2026, 1, 1));
        Engagement closed = Engagement.Create(_organization, _otherCreator, now.AddDays(-1), "Closed client", "CLOSED", new DateOnly(2026, 1, 1));
        closed.Close(_otherCreator, "Finished", now.AddDays(2));
        Engagement foreign = Engagement.Create(_otherOrganization, _otherCreator, now.AddDays(1), "Foreign client", "FOREIGN", new DateOnly(2026, 1, 1));
        _owned = own.Id;
        _otherOwned = other.Id;
        _foreign = foreign.Id;
        db.Engagements.AddRange(own, other, third, closed, foreign);
        EngagementContact active = EngagementContact.Create(_organization, own.Id, _creator, now, "Active contact", "active@example.test", EngagementContact.OtherRole);
        EngagementContact removed = EngagementContact.Create(_organization, own.Id, _creator, now, "Removed contact", "removed@example.test", EngagementContact.OtherRole);
        db.EngagementContacts.AddRange(active, removed);
        var milestone = EngagementMilestone.Create(_organization, own.Id, _creator, now, "Commitment", new DateOnly(2026, 5, 1));
        db.EngagementMilestones.Add(milestone);
        byte[] verifier = RandomNumberGenerator.GetBytes(32);
        _verifier = Convert.ToHexString(verifier);
        db.EngagementLinkCodes.Add(EngagementLinkCode.Create(_organization, own.Id, _creator, now, verifier));
        for (int i = 0; i < 3; i++)
        {
            db.EngagementEvents.Add(EngagementEvent.Create(_organization, own.Id, "milestone.date-changed", "Milestone", milestone.Id, _creator, _organization, now.AddDays(3), i + 1, "{\"previous\":\"2026-05-01\",\"new\":\"2026-06-01\"}", "Changed date"));
        }

        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE engagement."EngagementContacts" SET "RemovedAt" = {now}, "RemovedByUserId" = {_creator}
            WHERE "OrganizationId" = {_organization} AND "Id" = {removed.Id}
            """);
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext db = Context();
        Guid[] organizations = [_organization, _otherOrganization];
        await db.EngagementEvents.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementContacts.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementMilestones.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementLinkCodes.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.Engagements.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlTheory]
    [InlineData("read-all")]
    [InlineData("manage")]
    public async Task PrivilegedReadersSeeWorkspaceFullDetailWithoutVerifier(string mode)
    {
        _authority.Mode = mode;
        using HttpResponseMessage list = await GetAsync("?status=all");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.True(list.Headers.CacheControl?.NoStore);
        using JsonDocument page = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        JsonElement.ArrayEnumerator items = page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray();
        JsonElement[] values = items.ToArray();
        Assert.Equal(4, values.Length);
        Assert.DoesNotContain(values, x => x.GetProperty("id").GetGuid() == _foreign);
        JsonElement own = values.Single(x => x.GetProperty("id").GetGuid() == _owned);
        Assert.Equal(100m, own.GetProperty("contractAmount").GetDecimal());
        Assert.Equal("USD", own.GetProperty("contractCurrency").GetString());
        Assert.Equal(4.5m, own.GetProperty("plannedEffortPersonDays").GetDecimal());
        Assert.True(own.GetProperty("hasOutstandingLinkCode").GetBoolean());
        Assert.Equal(1, own.GetProperty("contacts").GetArrayLength());
        Assert.Equal("Active contact", own.GetProperty("contacts")[0].GetProperty("name").GetString());
        Assert.Equal("Commitment", own.GetProperty("milestones")[0].GetProperty("name").GetString());
        Assert.DoesNotContain("codeVerifier", page.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_verifier, page.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using HttpResponseMessage detail = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [PostgreSqlFact]
    public async Task CreatorSeesOnlyOwnEngagementAndNoTierSeesNothing()
    {
        using HttpResponseMessage list = await GetAsync("?status=all");
        using JsonDocument page = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        JsonElement items = page.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(_owned, items[0].GetProperty("id").GetGuid());
        Assert.Equal(100m, items[0].GetProperty("contractAmount").GetDecimal());
        Assert.Equal("USD", items[0].GetProperty("contractCurrency").GetString());
        using HttpResponseMessage own = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using HttpResponseMessage ownHistory = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.OK, ownHistory.StatusCode);
        Assert.DoesNotContain(_verifier, await ownHistory.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using HttpResponseMessage hidden = await GetAsync(_otherOwned.ToString());
        using HttpResponseMessage unknown = await GetAsync(Guid.NewGuid().ToString());
        using HttpResponseMessage foreign = await GetAsync(_foreign.ToString());
        await AssertSameNotFoundAsync(hidden, unknown);
        await AssertSameNotFoundAsync(foreign, unknown);
        _caller = Guid.NewGuid();
        using HttpResponseMessage none = await GetAsync("?status=all");
        using JsonDocument empty = await JsonDocument.ParseAsync(await none.Content.ReadAsStreamAsync());
        Assert.Equal(0, empty.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        using HttpResponseMessage noHistory = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.NotFound, noHistory.StatusCode);
    }

    [PostgreSqlFact]
    public async Task PagedListsAndHistoryHaveStableTiesAndBoundCursors()
    {
        _authority.Mode = "read-all";
        var seen = new HashSet<Guid>();
        string? cursor = null;
        for (int i = 0; i < 4; i++)
        {
            string query = "?status=all&limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using HttpResponseMessage response = await GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument page = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement data = page.RootElement.GetProperty("data");
            Assert.True(seen.Add(data.GetProperty("items")[0].GetProperty("id").GetGuid()));
            cursor = data.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : data.GetProperty("nextCursor").GetString();
            if (i == 0)
            {
                using HttpResponseMessage wrongFilter = await GetAsync("?status=active&limit=1&cursor=" + Uri.EscapeDataString(cursor!));
                Assert.Equal(HttpStatusCode.BadRequest, wrongFilter.StatusCode);
            }
        }

        Assert.Equal(4, seen.Count);
        Assert.Null(cursor);
        var events = new HashSet<Guid>();
        cursor = null;
        for (int i = 0; i < 3; i++)
        {
            string query = _owned + "/history?limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using HttpResponseMessage response = await GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument page = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement data = page.RootElement.GetProperty("data");
            JsonElement entry = data.GetProperty("items")[0];
            Assert.True(events.Add(entry.GetProperty("id").GetGuid()));
            Assert.Equal(_creator, entry.GetProperty("actorUserId").GetGuid());
            Assert.Equal("Changed date", entry.GetProperty("reason").GetString());
            Assert.Equal("2026-06-01", entry.GetProperty("details").GetProperty("new").GetString());
            cursor = data.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : data.GetProperty("nextCursor").GetString();
        }

        Assert.Equal(3, events.Count);
        Assert.Null(cursor);
    }

    [PostgreSqlTheory]
    [InlineData("?status=unknown")]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=not-a-number")]
    [InlineData("?limit=")]
    [InlineData("?status=")]
    [InlineData("?cursor=invalid")]
    [InlineData("?cursor=")]
    public async Task InvalidPagingAndStatusAreBadRequests(string query)
    {
        _authority.Mode = "read-all";
        using HttpResponseMessage response = await GetAsync(query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [PostgreSqlFact]
    public async Task StatusFiltersAndAuthorityRevocationApplyOnEveryRequest()
    {
        _authority.Mode = "manage";
        using HttpResponseMessage active = await GetAsync(string.Empty);
        using JsonDocument activePage = await JsonDocument.ParseAsync(await active.Content.ReadAsStreamAsync());
        Assert.Equal(3, activePage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        using HttpResponseMessage closed = await GetAsync("?status=closed");
        using JsonDocument closedPage = await JsonDocument.ParseAsync(await closed.Content.ReadAsStreamAsync());
        Assert.Equal(1, closedPage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        _authority.Mode = "denied";
        using HttpResponseMessage denied = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        _authority.Mode = "unavailable";
        using HttpResponseMessage unavailable = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.True(unavailable.Headers.CacheControl?.NoStore);
    }

    [PostgreSqlFact]
    public async Task AnonymousRequestsCannotReadListsDetailsOrHistory()
    {
        using HttpClient client = _factory.CreateClient();
        string root = $"/api/v1/organizations/{_organization:D}/engagements";
        foreach (string suffix in new[] { string.Empty, "/" + _owned, "/" + _owned + "/history" })
        {
            using HttpResponseMessage response = await client.GetAsync(root + suffix);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    private async Task<HttpResponseMessage> GetAsync(string suffix)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        string separator = suffix.StartsWith('?') || suffix.Length == 0 ? string.Empty : "/";
        return await client.GetAsync($"/api/v1/organizations/{_organization:D}/engagements{separator}{suffix}");
    }

    private static async Task AssertSameNotFoundAsync(HttpResponseMessage actual, HttpResponseMessage expected)
    {
        Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expected.StatusCode);
        using JsonDocument a = JsonDocument.Parse(await actual.Content.ReadAsStringAsync());
        using JsonDocument b = JsonDocument.Parse(await expected.Content.ReadAsStringAsync());
        Assert.Equal(b.RootElement.GetProperty("error").GetProperty("code").GetString(), a.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(b.RootElement.GetProperty("error").GetProperty("message").GetString(), a.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private string Token()
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = _caller, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        string key = TestConfiguration.Host()["Jwt:SecretKey"] ?? throw new InvalidOperationException("Test signing key is missing.");
        return message + "." + Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ReadFactory(AuthorityClient authority) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in TestConfiguration.Host())
            {
                builder.UseSetting(key, value);
            }

            builder.UseSetting("ConnectionStrings:ApplicationDb", PostgreSqlFactAttribute.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuthorityClient>();
                services.AddSingleton<IIdentityAuthorityClient>(authority);
            });
        }
    }

    private sealed class AuthorityClient : IIdentityAuthorityClient
    {
        internal string Mode { get; set; } = "none";

        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
            => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));

        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            string[] permissions = Mode switch
            {
                "read-all" => [WorkspacePermissions.EngagementsReadAll],
                "manage" => [WorkspacePermissions.EngagementsManage],
                _ => [],
            };
            OrganizationAuthorityCheckResult result = Mode == "denied"
                ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow)
                : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, false, false, permissions);
            return Task.FromResult(Mode == "unavailable" ? FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>() : IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }
}
