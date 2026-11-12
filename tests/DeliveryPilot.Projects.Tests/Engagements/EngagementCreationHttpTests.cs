using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;
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
[SuppressMessage("Design", "CA1001", Justification = "xUnit disposes the factory through IAsyncLifetime.DisposeAsync.")]
public sealed class EngagementCreationHttpTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly List<Guid> _organizations = [];
    private readonly AuthorityClient _identity = new();
    private readonly ApiFactory _factory;

    public EngagementCreationHttpTests()
    {
        _organizations.Add(_organizationId);
        _factory = new ApiFactory(_identity);
    }

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext context = Context();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext context = Context();
        Guid[] ids = await context.Engagements.Where(row => _organizations.Contains(row.OrganizationId)).Select(row => row.Id).ToArrayAsync();
        await context.Set<IdempotencyRecord>().Where(row => row.ResultResourceId != null && ids.Contains(row.ResultResourceId.Value)).ExecuteDeleteAsync();
        await context.EngagementEvents.Where(row => _organizations.Contains(row.OrganizationId)).ExecuteDeleteAsync();
        await context.Engagements.Where(row => _organizations.Contains(row.OrganizationId)).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlFact]
    public async Task CreateStoresAuthorizedTenantAndOneEventAndReplaysOriginalSnapshot()
    {
        var key = Guid.NewGuid();
        using HttpResponseMessage created = await SendAsync(Body(), key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        EngagementCreationSnapshot snapshot = await SnapshotAsync(created);
        Assert.Equal("Client", snapshot.ClientDisplayName);
        Assert.Equal("VN", snapshot.ClientCountryCode);
        Assert.Equal(Engagement.ActiveStatus, snapshot.Status);
        Assert.Equal(1, snapshot.Version);
        Assert.False(snapshot.DuplicateAgreementReference);
        await using ProjectsDbContext context = Context();
        Engagement engagement = await context.Engagements.SingleAsync(row => row.Id == snapshot.Id);
        Assert.Equal(_organizationId, engagement.OrganizationId);
        Assert.Equal(_userId, engagement.CreatedByUserId);
        Assert.Null(engagement.ContractAmount);
        Assert.Null(engagement.ClientOrganizationId);
        EngagementEvent entry = await context.EngagementEvents.SingleAsync(row => row.EngagementId == snapshot.Id);
        Assert.Equal("engagement.created", entry.EventType);
        Assert.Equal(_userId, entry.ActorUserId);
        Assert.Equal(_organizationId, entry.ActorOrganizationId);
        Assert.Equal(snapshot.CreatedAt, entry.OccurredAt);

        // Simulate a later task's detail edit: the replay is the original creation representation.
        context.Entry(engagement).Property(row => row.ClientDisplayName).CurrentValue = "Later name";
        context.Entry(engagement).Property(row => row.Version).CurrentValue = 2L;
        await context.SaveChangesAsync();
        using HttpResponseMessage replay = await SendAsync(Body(), key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(snapshot, await SnapshotAsync(replay));
        Assert.Equal(1, await context.Engagements.CountAsync(row => row.OrganizationId == _organizationId));
        Assert.Equal(1, await context.EngagementEvents.CountAsync(row => row.OrganizationId == _organizationId));
    }

    [PostgreSqlFact]
    public async Task NormalizationAllowsEquivalentReplayAndChangedContentConflicts()
    {
        var key = Guid.NewGuid();
        using HttpResponseMessage first = await SendAsync(Body(), key);
        Dictionary<string, object?> normalized = Body();
        normalized["clientDisplayName"] = "Client";
        normalized["agreementReference"] = "FP-1";
        normalized["clientCountryCode"] = "VN";
        normalized["engagementName"] = null;
        using HttpResponseMessage replay = await SendAsync(normalized, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await SnapshotAsync(first), await SnapshotAsync(replay));
        normalized["clientDisplayName"] = "Different";
        using HttpResponseMessage conflict = await SendAsync(normalized, key);
        await AssertErrorAsync(conflict, HttpStatusCode.Conflict, "Idempotency.KeyReused");
    }

    [PostgreSqlFact]
    public async Task ConcurrentSameKeyCreatesOneResourceAndDifferentKeysWarn()
    {
        var key = Guid.NewGuid();
        HttpResponseMessage[] sameKey = await Task.WhenAll(SendAsync(Body(), key), SendAsync(Body(), key));
        try
        {
            Assert.Equal(1, sameKey.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, sameKey.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(await SnapshotAsync(sameKey[0]), await SnapshotAsync(sameKey[1]));
        }
        finally
        {
            foreach (HttpResponseMessage response in sameKey)
            {
                response.Dispose();
            }
        }

        Dictionary<string, object?> distinctReference = Body();
        distinctReference["agreementReference"] = "Parallel";
        HttpResponseMessage[] differentKeys = await Task.WhenAll(SendAsync(distinctReference, Guid.NewGuid()), SendAsync(distinctReference, Guid.NewGuid()));
        try
        {
            EngagementCreationSnapshot[] snapshots = await Task.WhenAll(differentKeys.Select(SnapshotAsync));
            Assert.All(differentKeys, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            Assert.NotEqual(snapshots[0].Id, snapshots[1].Id);
            Assert.Single(snapshots, snapshot => snapshot.DuplicateAgreementReference);
        }
        finally
        {
            foreach (HttpResponseMessage response in differentKeys)
            {
                response.Dispose();
            }
        }
    }

    [PostgreSqlFact]
    public async Task DuplicateWarningIgnoresClosedAndOtherTenantReferences()
    {
        using HttpResponseMessage first = await SendAsync(Body(), Guid.NewGuid());
        Dictionary<string, object?> lower = Body();
        lower["agreementReference"] = "fp-1";
        using HttpResponseMessage duplicate = await SendAsync(lower, Guid.NewGuid());
        Assert.True((await SnapshotAsync(duplicate)).DuplicateAgreementReference);
        await using ProjectsDbContext context = Context();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE engagement."Engagements" SET "Status" = 'Closed', "ClosedAt" = now(), "ClosedByUserId" = {_userId}
            WHERE "OrganizationId" = {_organizationId}
            """);
        using HttpResponseMessage afterClose = await SendAsync(Body(), Guid.NewGuid());
        Assert.False((await SnapshotAsync(afterClose)).DuplicateAgreementReference);
        var otherOrganization = Guid.NewGuid();
        _organizations.Add(otherOrganization);
        using HttpResponseMessage otherTenant = await SendAsync(Body(), Guid.NewGuid(), organizationId: otherOrganization);
        Assert.False((await SnapshotAsync(otherTenant)).DuplicateAgreementReference);
    }

    [PostgreSqlTheory]
    [InlineData("blank")]
    [InlineData("long-name")]
    [InlineData("http")]
    [InlineData("country")]
    [InlineData("date")]
    [InlineData("malformed-date")]
    [InlineData("unknown-member")]
    [InlineData("missing-key")]
    [InlineData("malformed-key")]
    [InlineData("empty-key")]
    [InlineData("multiple-keys")]
    public async Task InvalidInputIs400AndStoresNothing(string scenario)
    {
        Dictionary<string, object?> body = Body();
        switch (scenario)
        {
            case "blank": body["clientDisplayName"] = " "; break;
            case "long-name": body["clientDisplayName"] = new string('x', 201); break;
            case "http": body["documentUrl"] = "http://example.test"; break;
            case "country": body["clientCountryCode"] = "ZZ"; break;
            case "date": body.Remove("agreementSignedOn"); break;
            case "malformed-date": body["agreementSignedOn"] = "not-a-date"; break;
            case "unknown-member": body["organizationId"] = Guid.NewGuid(); break;
        }

        IReadOnlyList<string>? header = scenario switch
        {
            "missing-key" => null,
            "malformed-key" => ["invalid"],
            "empty-key" => [Guid.Empty.ToString()],
            "multiple-keys" => [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()],
            _ => [Guid.NewGuid().ToString()],
        };
        using HttpResponseMessage response = await SendAsync(body, headers: header);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest);
        await AssertEmptyAsync();
    }

    [PostgreSqlTheory]
    [InlineData("denied", HttpStatusCode.Forbidden)]
    [InlineData("missing-permission", HttpStatusCode.Forbidden)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("session-denied", HttpStatusCode.Unauthorized)]
    [InlineData("session-unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("revoked-after-wait", HttpStatusCode.Forbidden)]
    public async Task AuthorizationFailuresFailClosed(string mode, HttpStatusCode status)
    {
        _identity.Mode = mode;
        using HttpResponseMessage response = await SendAsync(Body(), Guid.NewGuid());
        await AssertErrorAsync(response, status);
        await AssertEmptyAsync();
    }

    [PostgreSqlFact]
    public async Task AnonymousAndRevokedReplayCannotCreateOrReadReceipt()
    {
        using HttpClient anonymous = _factory.CreateClient();
        using HttpResponseMessage rejected = await anonymous.PostAsJsonAsync(Route(_organizationId), Body());
        await AssertErrorAsync(rejected, HttpStatusCode.Unauthorized);
        var key = Guid.NewGuid();
        using HttpResponseMessage created = await SendAsync(Body(), key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        _identity.Mode = "denied";
        using HttpResponseMessage replay = await SendAsync(Body(), key);
        await AssertErrorAsync(replay, HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task SameKeyIsIndependentAcrossUsersAndOrganizations()
    {
        var key = Guid.NewGuid();
        var otherOrganization = Guid.NewGuid();
        _organizations.Add(otherOrganization);
        using HttpResponseMessage first = await SendAsync(Body(), key);
        using HttpResponseMessage otherUser = await SendAsync(Body(), key, userId: Guid.NewGuid());
        using HttpResponseMessage otherTenant = await SendAsync(Body(), key, organizationId: otherOrganization);
        Assert.All(new[] { first, otherUser, otherTenant }, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.NotEqual((await SnapshotAsync(first)).Id, (await SnapshotAsync(otherUser)).Id);
        Assert.NotEqual((await SnapshotAsync(first)).Id, (await SnapshotAsync(otherTenant)).Id);
    }

    [PostgreSqlFact]
    public async Task FailureAfterStagingRollsEverythingBackAndRetrySucceeds()
    {
        var key = Guid.NewGuid();
        _factory.FailStaging = true;
        using HttpResponseMessage failed = await SendAsync(Body(), key);
        await AssertErrorAsync(failed, HttpStatusCode.InternalServerError);
        await AssertEmptyAsync();
        _factory.FailStaging = false;
        using HttpResponseMessage retried = await SendAsync(Body(), key);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(Dictionary<string, object?> body, Guid? key = null, Guid? userId = null, Guid? organizationId = null, IReadOnlyList<string>? headers = null)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId ?? _userId));
        using var request = new HttpRequestMessage(HttpMethod.Post, Route(organizationId ?? _organizationId)) { Content = JsonContent.Create(body) };
        IReadOnlyList<string>? keys = key.HasValue ? [key.Value.ToString()] : headers;
        if (keys is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", keys);
        }

        return await client.SendAsync(request);
    }

    private async Task AssertEmptyAsync()
    {
        await using ProjectsDbContext context = Context();
        Assert.False(await context.Engagements.AnyAsync(row => row.OrganizationId == _organizationId));
        Assert.False(await context.EngagementEvents.AnyAsync(row => row.OrganizationId == _organizationId));
        string scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"v1:{_organizationId:D}:{_userId:D}")));
        Assert.False(await context.Set<IdempotencyRecord>().AnyAsync(row => row.OwnerScopeHash == scope));
    }

    private static string Route(Guid organizationId) => $"/api/v1/organizations/{organizationId:D}/engagements";
    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);
    private static Dictionary<string, object?> Body() => new() { ["clientDisplayName"] = " Client ", ["agreementReference"] = " FP-1 ", ["agreementSignedOn"] = "2026-11-01", ["clientCountryCode"] = " vn ", ["engagementName"] = " " };

    private static async Task<EngagementCreationSnapshot> SnapshotAsync(HttpResponseMessage response)
    {
        using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(envelope.RootElement.GetProperty("success").GetBoolean());
        return JsonSerializer.Deserialize<EngagementCreationSnapshot>(envelope.RootElement.GetProperty("data"), _jsonOptions)!;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(envelope.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(envelope.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
        if (code is not null)
        {
            Assert.Equal(code, envelope.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }

    private static string Token(Guid userId)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = "DeliveryPilot.Identity",
            aud = "DeliveryPilot",
            sub = userId,
            sid = Guid.NewGuid(),
            jti = Guid.NewGuid(),
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
        }));
        string message = header + "." + payload;
        string signingKey = TestConfiguration.Host()["Jwt:SecretKey"] ?? throw new InvalidOperationException("Test signing key is missing.");
        return message + "." + Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(message)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ApiFactory(AuthorityClient identity) : WebApplicationFactory<Program>
    {
        internal bool FailStaging { get; set; }

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
                services.AddSingleton<IIdentityAuthorityClient>(identity);
                services.RemoveAll<IEngagementCreationStore>();
                services.AddScoped<IEngagementCreationStore>(provider => new StagingHook(new EngagementCreationStore(provider.GetRequiredService<ProjectsDbContext>()), () => FailStaging));
            });
        }
    }

    private sealed class StagingHook(IEngagementCreationStore inner, Func<bool> shouldFail) : IEngagementCreationStore
    {
        public Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken) => inner.LockAttemptAsync(organizationId, userId, key, cancellationToken);
        public Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken) => inner.LockReferenceAsync(organizationId, reference, cancellationToken);
        public Task<EngagementCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, EngagementCreationDetails details, CancellationToken cancellationToken) => inner.FindReplayAsync(organizationId, userId, key, details, cancellationToken);
        public Task<bool> HasActiveReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken) => inner.HasActiveReferenceAsync(organizationId, reference, cancellationToken);

        public void Stage(Engagement engagement, EngagementEvent entry, Guid userId, Guid key, EngagementCreationDetails details, DateTimeOffset now)
        {
            inner.Stage(engagement, entry, userId, key, details, now);
            if (shouldFail())
            {
                throw new InvalidOperationException("Simulated failure after staging.");
            }
        }
    }

    private sealed class AuthorityClient : IIdentityAuthorityClient
    {
        private int _checks;
        internal string Mode { get; set; } = "allowed";

        public Task<IntegrationResult<DeliveryPilot.Identity.Contracts.UserLookupResult>> LookupUsersAsync(DeliveryPilot.Identity.Contracts.UserLookupRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Creation must not look up display names.");

        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
            => Task.FromResult(Mode == "session-unavailable" ? FakeIdentityAuthorityClient.Unavailable<SessionCheckResult>() : IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(Mode != "session-denied"), HttpStatusCode.OK));

        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            int count = Interlocked.Increment(ref _checks);
            OrganizationAuthorityCheckResult result = Mode == "denied" || (Mode == "revoked-after-wait" && count > 1)
                ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow)
                : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, false, false, Mode == "missing-permission" ? [] : [WorkspacePermissions.EngagementsCreate]);
            return Task.FromResult(Mode == "unavailable" ? FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>() : IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }
}
