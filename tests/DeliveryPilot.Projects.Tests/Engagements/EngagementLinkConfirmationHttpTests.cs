using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DeliveryPilot.Projects.Tests.Engagements;

[Collection("Projects PostgreSQL")]
[SuppressMessage("Design", "CA1001", Justification = "xUnit disposes the factory through IAsyncLifetime.")]
public sealed class EngagementLinkConfirmationHttpTests : IAsyncLifetime
{
    private readonly Guid _vendorOrganizationId = Guid.NewGuid();
    private readonly Guid _clientOrganizationId = Guid.NewGuid();
    private readonly Guid _vendorUserId = Guid.NewGuid();
    private readonly Guid _clientUserId = Guid.NewGuid();
    private readonly MutableClock _clock = new();
    private readonly ConfirmationFactory _factory;
    private readonly AuthorityClient _authority;

    public EngagementLinkConfirmationHttpTests()
    {
        _authority = new AuthorityClient(_vendorOrganizationId, _clientOrganizationId, _vendorUserId);
        _factory = new ConfirmationFactory(_authority, _clock);
    }

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await using ProjectsDbContext db = Context();
        await db.EngagementLinkAttempts.Where(x => x.ActingOrganizationId == _clientOrganizationId || x.ActingOrganizationId == _vendorOrganizationId).ExecuteDeleteAsync();
        await db.EngagementEvents.Where(x => x.OrganizationId == _vendorOrganizationId).ExecuteDeleteAsync();
        await db.EngagementLinkCodes.Where(x => x.OrganizationId == _vendorOrganizationId).ExecuteDeleteAsync();
        await db.Engagements.Where(x => x.OrganizationId == _vendorOrganizationId).ExecuteDeleteAsync();
    }

    [PostgreSqlTheory]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task ClientOwnerOrAdminConfirmsOnceAndPreservesVendorAttribution(string role)
    {
        _authority.Role = role;
        SeededCode seeded = await SeedAsync();
        using HttpResponseMessage response = await SubmitAsync("confirm", seeded.Code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        await AssertDisplayOnlyAsync(response);
        await using ProjectsDbContext db = Context();
        Engagement engagement = await db.Engagements.SingleAsync(x => x.Id == seeded.EngagementId);
        EngagementLinkCode code = await db.EngagementLinkCodes.SingleAsync(x => x.Id == seeded.CodeId);
        Assert.Equal(_clientOrganizationId, engagement.ClientOrganizationId);
        Assert.Equal(_clientUserId, engagement.ClientLinkedByUserId);
        Assert.Equal(2, engagement.Version);
        Assert.Equal(EngagementLinkCode.ConsumedStatus, code.Status);
        Assert.Equal(_clientOrganizationId, code.ConsumedForOrganizationId);
        Assert.Equal(_clientUserId, code.ConsumedByUserId);
        EngagementEvent entry = await db.EngagementEvents.SingleAsync(x => x.EngagementId == seeded.EngagementId);
        Assert.Equal("link.confirmed", entry.EventType);
        Assert.Equal(_clientOrganizationId, entry.ActorOrganizationId);
        Assert.Equal(_clientUserId, entry.ActorUserId);
        Assert.Equal(2, entry.EngagementVersion);
        using HttpResponseMessage detail = await VendorAsync(HttpMethod.Get, seeded.EngagementId, string.Empty);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using JsonDocument detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        Assert.Equal(_clientOrganizationId, detailBody.RootElement.GetProperty("data").GetProperty("clientOrganizationId").GetGuid());
        AssertSecretsAbsent(seeded, entry.Details);
    }

    [PostgreSqlFact]
    public async Task PreviewShowsOnlyDisplayFieldsAndDoesNotConsumeOrWriteHistory()
    {
        SeededCode seeded = await SeedAsync();
        using HttpResponseMessage response = await SubmitAsync("preview", seeded.Code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertDisplayOnlyAsync(response);
        await using ProjectsDbContext db = Context();
        Assert.Equal(EngagementLinkCode.OutstandingStatus, (await db.EngagementLinkCodes.SingleAsync(x => x.Id == seeded.CodeId)).Status);
        Assert.Null((await db.Engagements.SingleAsync(x => x.Id == seeded.EngagementId)).ClientOrganizationId);
        Assert.Empty(await db.EngagementEvents.Where(x => x.OrganizationId == _vendorOrganizationId).ToArrayAsync());
        Assert.Empty(await db.EngagementLinkAttempts.Where(x => x.ActingOrganizationId == _clientOrganizationId).ToArrayAsync());
        AssertSecretsAbsent(seeded, string.Empty);
    }

    [PostgreSqlTheory]
    [InlineData("preview")]
    [InlineData("confirm")]
    public async Task PlainClientMemberCannotSubmitCodes(string action)
    {
        SeededCode seeded = await SeedAsync();
        _authority.Role = "member";
        using HttpResponseMessage response = await SubmitAsync(action, seeded.Code);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, _factory.Queries.VerifierLookups);
        await using ProjectsDbContext db = Context();
        Assert.Empty(await db.EngagementLinkAttempts.Where(x => x.ActingOrganizationId == _clientOrganizationId).ToArrayAsync());
    }

    [PostgreSqlTheory]
    [InlineData("blank")]
    [InlineData("oversized")]
    public async Task InvalidInputIsRejectedBeforeHashLookup(string input)
    {
        using HttpResponseMessage response = await SubmitAsync("confirm", input == "blank" ? " " : new string('A', 65));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Queries.VerifierLookups);
    }

    [PostgreSqlFact]
    public async Task CodeIsUnusableAtItsExactExpiryInstant()
    {
        SeededCode seeded = await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(7));
        using HttpResponseMessage response = await SubmitAsync("confirm", seeded.Code);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        (string code, _) = await ErrorAsync(response);
        Assert.Equal("EngagementLink.InvalidCode", code);
        await using ProjectsDbContext db = Context();
        Assert.Equal(1, await db.EngagementLinkAttempts.CountAsync(x => x.UserId == _clientUserId));
    }

    [PostgreSqlFact]
    public async Task EveryUnusableCodeHasTheSameFailureAndCommitsOneIndependentAttempt()
    {
        string? expectedMessage = null;
        string[] cases = ["unknown", "expired", "consumed", "invalidated", "closed", "linked", "same-organization"];
        foreach (string scenario in cases)
        {
            SeededCode seeded = await SeedAsync();
            await SetUnusableAsync(seeded, scenario);
            Guid actingOrganization = scenario == "same-organization" ? _vendorOrganizationId : _clientOrganizationId;
            _authority.AllowClientOnVendor = scenario == "same-organization";
            using HttpResponseMessage response = await SubmitAsync("confirm", scenario == "unknown" ? NewCode() : seeded.Code, Guid.NewGuid(), actingOrganization);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            (string code, string message) = await ErrorAsync(response);
            Assert.Equal("EngagementLink.InvalidCode", code);
            expectedMessage ??= message;
            Assert.Equal(expectedMessage, message);
            _clock.Advance(TimeSpan.Zero);
        }

        await using ProjectsDbContext db = Context();
        Assert.Equal(7, await db.EngagementLinkAttempts.CountAsync(x => x.ActingOrganizationId == _clientOrganizationId || x.ActingOrganizationId == _vendorOrganizationId));
        Assert.Empty(await db.EngagementEvents.Where(x => x.OrganizationId == _vendorOrganizationId).ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task SixthFailureIsLimitedBeforeLookupAndAnotherUserHasAnIndependentAllowance()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage response = await SubmitAsync(attempt % 2 == 0 ? "preview" : "confirm", NewCode());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        int lookups = _factory.Queries.VerifierLookups;
        using HttpResponseMessage limited = await SubmitAsync("confirm", NewCode());
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.CacheControl?.NoStore);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(lookups, _factory.Queries.VerifierLookups);
        using HttpResponseMessage otherUser = await SubmitAsync("preview", NewCode(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, otherUser.StatusCode);
        await using ProjectsDbContext db = Context();
        Assert.Equal(5, await db.EngagementLinkAttempts.CountAsync(x => x.UserId == _clientUserId));
        Assert.Equal(6, await db.EngagementLinkAttempts.CountAsync(x => x.ActingOrganizationId == _clientOrganizationId));
    }

    [PostgreSqlFact]
    public async Task ParallelGuessesAcrossApiInstancesCannotExceedTheFailureAllowance()
    {
        await using var secondFactory = new ConfirmationFactory(_authority, _clock);
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(index => SubmitAsync("confirm", NewCode(), factory: index % 2 == 0 ? _factory : secondFactory)));
        try
        {
            Assert.Equal(5, responses.Count(x => x.StatusCode == HttpStatusCode.NotFound));
            Assert.Equal(5, responses.Count(x => x.StatusCode == HttpStatusCode.TooManyRequests));
            await using ProjectsDbContext db = Context();
            Assert.Equal(5, await db.EngagementLinkAttempts.CountAsync(x => x.UserId == _clientUserId));
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [PostgreSqlFact]
    public async Task ParallelConfirmationsConsumeCodeOnlyOnce()
    {
        SeededCode seeded = await SeedAsync();
        HttpResponseMessage[] responses = await Task.WhenAll(
            SubmitAsync("confirm", seeded.Code),
            SubmitAsync("confirm", seeded.Code, Guid.NewGuid()));
        try
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.NotFound));
            await using ProjectsDbContext db = Context();
            Assert.Single(await db.EngagementEvents.Where(x => x.EngagementId == seeded.EngagementId).ToArrayAsync());
            Assert.Equal(1, await db.EngagementLinkAttempts.CountAsync(x => x.ActingOrganizationId == _clientOrganizationId));
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [PostgreSqlFact]
    public async Task AConfirmedLinkGrantsNoAccessToVendorEngagementRoutes()
    {
        SeededCode seeded = await SeedAsync();
        using HttpResponseMessage confirmation = await SubmitAsync("confirm", seeded.Code);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        using HttpClient client = Client(_factory, _clientUserId);
        using HttpResponseMessage detail = await client.GetAsync($"/api/v1/organizations/{_vendorOrganizationId:D}/engagements/{seeded.EngagementId:D}");
        Assert.True(detail.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        using HttpResponseMessage history = await client.GetAsync($"/api/v1/organizations/{_vendorOrganizationId:D}/engagements/{seeded.EngagementId:D}/history");
        Assert.True(history.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    [PostgreSqlTheory]
    [InlineData("rotation")]
    [InlineData("close")]
    public async Task ConfirmationRechecksEligibilityAfterWaitingForTheEngagementLock(string lifecycleChange)
    {
        SeededCode seeded = await SeedAsync();
        await using ProjectsDbContext blocker = Context();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM engagement."Engagements" WHERE "OrganizationId" = {_vendorOrganizationId} AND "Id" = {seeded.EngagementId} FOR UPDATE
            """);
        Task<HttpResponseMessage> confirming = SubmitAsync("confirm", seeded.Code);
        await _factory.Queries.EngagementLockObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await blocker.EngagementLinkCodes.Where(x => x.Id == seeded.CodeId).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Status, EngagementLinkCode.InvalidatedStatus)
                .SetProperty(x => x.InvalidatedAt, _clock.GetUtcNow())
                .SetProperty(x => x.Version, x => x.Version + 1));
        if (lifecycleChange == "rotation")
        {
            blocker.EngagementLinkCodes.Add(EngagementLinkCode.Create(_vendorOrganizationId, seeded.EngagementId, _vendorUserId, _clock.GetUtcNow(), SHA256.HashData(Encoding.UTF8.GetBytes(NewCode()))));
        }
        else
        {
            Engagement engagement = await blocker.Engagements.SingleAsync(x => x.Id == seeded.EngagementId);
            engagement.Close(_vendorUserId, "Closed while confirming", _clock.GetUtcNow());
        }

        await blocker.SaveChangesAsync();
        await transaction.CommitAsync();
        using HttpResponseMessage response = await confirming;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        (string code, _) = await ErrorAsync(response);
        Assert.Equal("EngagementLink.InvalidCode", code);
        await using ProjectsDbContext verify = Context();
        Assert.Null((await verify.Engagements.SingleAsync(x => x.Id == seeded.EngagementId)).ClientOrganizationId);
        Assert.Equal(1, await verify.EngagementLinkAttempts.CountAsync(x => x.UserId == _clientUserId));
    }

    [PostgreSqlFact]
    public async Task ConfirmationRechecksClientAuthorityAfterWaitingForTheEngagementLock()
    {
        SeededCode seeded = await SeedAsync();
        await using ProjectsDbContext blocker = Context();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM engagement."Engagements" WHERE "OrganizationId" = {_vendorOrganizationId} AND "Id" = {seeded.EngagementId} FOR UPDATE
            """);
        Task<HttpResponseMessage> confirming = SubmitAsync("confirm", seeded.Code);
        await _factory.Queries.EngagementLockObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _authority.Role = "member";
        await transaction.CommitAsync();
        using HttpResponseMessage response = await confirming;
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using ProjectsDbContext verify = Context();
        Assert.Null((await verify.Engagements.SingleAsync(x => x.Id == seeded.EngagementId)).ClientOrganizationId);
        Assert.Equal(EngagementLinkCode.OutstandingStatus, (await verify.EngagementLinkCodes.SingleAsync(x => x.Id == seeded.CodeId)).Status);
        Assert.Empty(await verify.EngagementLinkAttempts.Where(x => x.UserId == _clientUserId).ToArrayAsync());
    }

    private async Task<SeededCode> SeedAsync()
    {
        string code = NewCode();
        var engagement = Engagement.Create(_vendorOrganizationId, _vendorUserId, _clock.GetUtcNow(), "Client Display", "REF", new DateOnly(2026, 1, 1), engagementName: "Engagement Display");
        var linkCode = EngagementLinkCode.Create(_vendorOrganizationId, engagement.Id, _vendorUserId, _clock.GetUtcNow(), SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        await using ProjectsDbContext db = Context();
        db.Engagements.Add(engagement);
        db.EngagementLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();
        return new SeededCode(engagement.Id, linkCode.Id, code);
    }

    private async Task SetUnusableAsync(SeededCode seeded, string scenario)
    {
        await using ProjectsDbContext db = Context();
        switch (scenario)
        {
            case "expired":
                _clock.Advance(TimeSpan.FromDays(8));
                break;
            case "consumed":
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE engagement."EngagementLinkCodes" SET "Status" = 'Consumed', "ConsumedAt" = {_clock.GetUtcNow()},
                        "ConsumedByUserId" = {_clientUserId}, "ConsumedForOrganizationId" = {_clientOrganizationId}
                    WHERE "Id" = {seeded.CodeId}
                    """);
                break;
            case "invalidated":
                EngagementLinkCode code = await db.EngagementLinkCodes.SingleAsync(x => x.Id == seeded.CodeId);
                code.Invalidate(_clock.GetUtcNow());
                await db.SaveChangesAsync();
                break;
            case "closed":
                Engagement engagement = await db.Engagements.SingleAsync(x => x.Id == seeded.EngagementId);
                engagement.Close(_vendorUserId, "Closed", _clock.GetUtcNow());
                await db.SaveChangesAsync();
                break;
            case "linked":
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE engagement."Engagements" SET "ClientOrganizationId" = {_clientOrganizationId},
                        "ClientLinkedAt" = {_clock.GetUtcNow()}, "ClientLinkedByUserId" = {_clientUserId}
                    WHERE "Id" = {seeded.EngagementId}
                    """);
                break;
        }
    }

    private async Task<HttpResponseMessage> SubmitAsync(string action, string code, Guid? userId = null, Guid? organizationId = null, ConfirmationFactory? factory = null)
    {
        using HttpClient client = Client(factory ?? _factory, userId ?? _clientUserId);
        return await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId ?? _clientOrganizationId:D}/engagement-links/{action}", new { code });
    }

    private async Task<HttpResponseMessage> VendorAsync(HttpMethod method, Guid engagementId, string suffix, object? body = null)
    {
        using HttpClient client = Client(_factory, _vendorUserId);
        using var request = new HttpRequestMessage(method, $"/api/v1/organizations/{_vendorOrganizationId:D}/engagements/{engagementId:D}{suffix}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertDisplayOnlyAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement data = body.RootElement.GetProperty("data");
        Assert.Equal(2, data.EnumerateObject().Count());
        Assert.Equal("Engagement Display", data.GetProperty("engagementName").GetString());
        Assert.Equal("Client Display", data.GetProperty("clientDisplayName").GetString());
    }

    private static async Task<(string Code, string Message)> ErrorAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    private void AssertSecretsAbsent(SeededCode seeded, string eventDetails)
    {
        byte[] verifier = SHA256.HashData(Encoding.UTF8.GetBytes(seeded.Code));
        string hex = Convert.ToHexString(verifier);
        string base64 = Convert.ToBase64String(verifier);
        string[] inspected = [eventDetails, .. _factory.Logs];
        bool leaked = inspected.Any(value => value.Contains(seeded.Code, StringComparison.Ordinal)
            || value.Contains(hex, StringComparison.OrdinalIgnoreCase) || value.Contains(base64, StringComparison.Ordinal));
        Assert.False(leaked);
    }

    private HttpClient Client(ConfirmationFactory factory, Guid userId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId, _clock.GetUtcNow()));
        return client;
    }

    private static string Token(Guid userId, DateTimeOffset now)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = userId, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = now.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        string signingKey = TestConfiguration.Host()["Jwt:SecretKey"] ?? throw new InvalidOperationException("Test signing key is missing.");
        return message + "." + Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(message)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string NewCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private sealed class SeededCode(Guid engagementId, Guid codeId, string code)
    {
        public Guid EngagementId { get; } = engagementId;
        public Guid CodeId { get; } = codeId;
        public string Code { get; } = code;
        public override string ToString() => nameof(SeededCode);
    }

    private sealed class MutableClock : TimeProvider
    {
        private readonly DateTimeOffset _initial = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        private TimeSpan _elapsed;
        public override DateTimeOffset GetUtcNow() => _initial + _elapsed;
        public void Advance(TimeSpan elapsed) => _elapsed = elapsed;
    }

    private sealed class ConfirmationFactory(AuthorityClient authority, MutableClock clock) : WebApplicationFactory<Program>
    {
        internal ConcurrentQueue<string> Logs { get; } = new();
        internal QuerySpy Queries { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in TestConfiguration.Host())
            {
                builder.UseSetting(key, value);
            }

            builder.UseSetting("ConnectionStrings:ApplicationDb", PostgreSqlFactAttribute.ConnectionString);
            builder.UseSetting("EngagementLinks:Attempts:MaxFailures", "5");
            builder.UseSetting("EngagementLinks:Attempts:Window", "00:15:00");
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuthorityClient>();
                services.AddSingleton<IIdentityAuthorityClient>(authority);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddDbContext<ProjectsDbContext>(options => options.AddInterceptors(Queries));
            });
        }
    }

    private sealed class QuerySpy : DbCommandInterceptor
    {
        private int _verifierLookups;
        public int VerifierLookups => Volatile.Read(ref _verifierLookups);
        internal TaskCompletionSource EngagementLockObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"EngagementLinkCodes\"", StringComparison.Ordinal) && command.CommandText.Contains("\"CodeVerifier\" =", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _verifierLookups);
            }

            if (command.CommandText.Contains("\"Engagements\"", StringComparison.Ordinal) && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                EngagementLockObserved.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class AuthorityClient(Guid vendorOrganizationId, Guid clientOrganizationId, Guid vendorUserId) : IIdentityAuthorityClient
    {
        internal string Role { get; set; } = "owner";
        internal bool AllowClientOnVendor { get; set; }

        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
            => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));

        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            bool vendor = request.OrganizationId == vendorOrganizationId && request.UserId == vendorUserId;
            bool client = request.OrganizationId == clientOrganizationId || (request.OrganizationId == vendorOrganizationId && AllowClientOnVendor);
            OrganizationAuthorityCheckResult result = !vendor && !client
                ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow)
                : new OrganizationAuthorityCheckResult(
                    AuthorityDecision.Allowed,
                    DateTimeOffset.UtcNow,
                    request.OrganizationId,
                    1,
                    Guid.NewGuid(),
                    1,
                    client && Role == "owner",
                    client && Role == "admin",
                    vendor ? [WorkspacePermissions.EngagementsManage, WorkspacePermissions.EngagementsReadAll] : []);
            return Task.FromResult(IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
