using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryPilot.Projects.Tests.Projects;

[Collection("Projects PostgreSQL")]
public sealed class ProjectCreationHttpTests : IAsyncLifetime
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly AuthorityClient _identity = new();
    private readonly ApiFactory _factory;

    public ProjectCreationHttpTests() => _factory = new ApiFactory(_identity);

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext db = Context();
        Guid[] ids = await db.Projects.Where(x => x.OrganizationId == _organization).Select(x => x.Id).ToArrayAsync();
        await db.Set<IdempotencyRecord>().Where(x => x.ResultResourceId != null && ids.Contains(x.ResultResourceId.Value)).ExecuteDeleteAsync();
        await db.ProjectEvents.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.ProjectParticipantRoles.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.ProjectParticipants.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.ProjectBriefs.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.Projects.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.EngagementEvents.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await db.Engagements.Where(x => x.OrganizationId == _organization).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlFact]
    public async Task CreateClientAndInternalProjectsWithAtomicAdministratorAndStableReplay()
    {
        Engagement engagement = await SeedEngagementAsync(_organization, _user);
        Guid key = Guid.NewGuid();
        object body = new { name = " Project ", purpose = " Purpose ", kind = "Client", engagementId = engagement.Id };
        using HttpResponseMessage response = await CreateAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        ProjectCreationSnapshot snapshot = await SnapshotAsync(response);
        Assert.Equal("Draft", snapshot.Status);
        Assert.Equal((short)2, snapshot.SprintLengthWeeks);
        Assert.Equal(engagement.Id, snapshot.EngagementId);
        await using ProjectsDbContext db = Context();
        Project project = await db.Projects.SingleAsync(x => x.Id == snapshot.Id);
        Assert.Equal("Project", project.Name);
        Assert.Equal("Purpose", project.Purpose);
        Assert.Equal(_user, project.CreatedByUserId);
        Assert.NotNull(await db.ProjectBriefs.SingleOrDefaultAsync(x => x.ProjectId == project.Id));
        ProjectParticipant member = await db.ProjectParticipants.SingleAsync(x => x.ProjectId == project.Id);
        Assert.Equal(_user, member.UserId);
        Assert.Equal(ProjectParticipant.MemberKind, member.Kind);
        Assert.Equal(ProjectParticipant.ActiveStatus, member.Status);
        Assert.Equal(ProjectRoles.ProjectAdministrator, (await db.ProjectParticipantRoles.SingleAsync(x => x.ProjectId == project.Id)).RoleCode);
        ProjectEvent entry = await db.ProjectEvents.SingleAsync(x => x.ProjectId == project.Id);
        Assert.Equal("project.created", entry.EventType);
        Assert.Equal(_user, entry.ActorUserId);
        await db.Projects.Where(x => x.Id == project.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Later name").SetProperty(x => x.Version, 2));
        using HttpResponseMessage replay = await CreateAsync(body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(snapshot, await SnapshotAsync(replay));
        using HttpResponseMessage conflict = await CreateAsync(new { name = "Changed", purpose = "Purpose", kind = "Client", engagementId = engagement.Id }, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using HttpResponseMessage internalProject = await CreateAsync(new { name = "Internal", purpose = "Purpose", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, internalProject.StatusCode);
        Assert.Null((await SnapshotAsync(internalProject)).EngagementId);
        Assert.Equal(2, await db.Projects.CountAsync(x => x.OrganizationId == _organization));
    }

    [PostgreSqlFact]
    public async Task RejectsInvisibleForeignClosedInvalidAndUnauthorizedRequests()
    {
        Engagement hidden = await SeedEngagementAsync(_organization, Guid.NewGuid());
        using HttpResponseMessage invisible = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = hidden.Id }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        _identity.ReadAll = true;
        using HttpResponseMessage visible = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = hidden.Id }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, visible.StatusCode);
        using HttpResponseMessage unknown = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = Guid.NewGuid() }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Guid foreignOrganization = Guid.NewGuid();
        Engagement foreign = await SeedEngagementAsync(foreignOrganization, _user);
        try
        {
            using HttpResponseMessage wrongTenant = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = foreign.Id }, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.NotFound, wrongTenant.StatusCode);
        }
        finally
        {
            await using ProjectsDbContext foreignDb = Context();
            await foreignDb.Engagements.Where(x => x.Id == foreign.Id).ExecuteDeleteAsync();
        }
        await using (ProjectsDbContext db = Context())
        {
            Engagement row = await db.Engagements.SingleAsync(x => x.Id == hidden.Id);
            row.Close(_user, "Close", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        using HttpResponseMessage closed = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = hidden.Id }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        using HttpResponseMessage missing = await CreateAsync(new { name = "A", purpose = "B", kind = "Client" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using HttpResponseMessage invalid = await CreateAsync(new { name = new string('a', 161), purpose = " ", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using HttpResponseMessage internalWithEngagement = await CreateAsync(new { name = "A", purpose = "B", kind = "Internal", engagementId = hidden.Id }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, internalWithEngagement.StatusCode);
        _identity.Mode = "missing-permission";
        using HttpResponseMessage missingPermission = await CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, missingPermission.StatusCode);
        _identity.Mode = "denied";
        using HttpResponseMessage denied = await CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        _identity.Mode = "unavailable";
        using HttpResponseMessage unavailable = await CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    [PostgreSqlFact]
    public async Task ConstraintFailureRollsBackAndConcurrentKeysReplay()
    {
        _factory.FailRole = true;
        using HttpResponseMessage failed = await CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using ProjectsDbContext db = Context();
        Assert.False(await db.Projects.AnyAsync(x => x.OrganizationId == _organization));
        Assert.False(await db.ProjectBriefs.AnyAsync(x => x.OrganizationId == _organization));
        Assert.False(await db.ProjectParticipants.AnyAsync(x => x.OrganizationId == _organization));
        Assert.False(await db.ProjectParticipantRoles.AnyAsync(x => x.OrganizationId == _organization));
        Assert.False(await db.ProjectEvents.AnyAsync(x => x.OrganizationId == _organization));
        _factory.FailRole = false;
        Guid key = Guid.NewGuid();
        HttpResponseMessage[] responses = await Task.WhenAll(CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, key), CreateAsync(new { name = "A", purpose = "B", kind = "Internal" }, key));
        try
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(await SnapshotAsync(responses[0]), await SnapshotAsync(responses[1]));
        }
        finally { foreach (HttpResponseMessage response in responses) { response.Dispose(); } }
    }

    [PostgreSqlFact]
    public async Task CreationWaitsForClosingEngagementAndRejectsTheClosedRow()
    {
        Engagement engagement = await SeedEngagementAsync(_organization, _user);
        await using ProjectsDbContext db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Engagement locked = await db.Engagements.FromSqlInterpolated($"SELECT * FROM engagement.\"Engagements\" WHERE \"OrganizationId\" = {_organization} AND \"Id\" = {engagement.Id} FOR UPDATE").SingleAsync();
        _factory.LockAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<HttpResponseMessage> pending = CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = engagement.Id }, Guid.NewGuid());
        await _factory.LockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        locked.Close(_user, "Close", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        using HttpResponseMessage result = await pending.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        await using ProjectsDbContext verify = Context();
        Assert.False(await verify.Projects.AnyAsync(x => x.OrganizationId == _organization));
    }

    [PostgreSqlFact]
    public async Task ClosingEngagementLeavesItsActiveProjectAndParticipantUnchanged()
    {
        _identity.ReadAll = true;
        Engagement engagement = await SeedEngagementAsync(_organization, _user);
        using HttpResponseMessage created = await CreateAsync(new { name = "A", purpose = "B", kind = "Client", engagementId = engagement.Id }, Guid.NewGuid());
        ProjectCreationSnapshot project = await SnapshotAsync(created);
        await using (ProjectsDbContext db = Context())
        {
            await db.Projects.Where(x => x.Id == project.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, Project.ActiveStatus).SetProperty(x => x.ActivatedAt, DateTimeOffset.UtcNow).SetProperty(x => x.ActivatedByUserId, _user));
        }
        using HttpClient client = Client();
        using HttpResponseMessage closed = await client.PostAsJsonAsync($"/api/v1/organizations/{_organization:D}/engagements/{engagement.Id:D}/close", new { expectedVersion = 1, reason = "Completed" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        await using ProjectsDbContext verify = Context();
        Project stored = await verify.Projects.SingleAsync(x => x.Id == project.Id);
        Assert.Equal(Project.ActiveStatus, stored.Status);
        Assert.Equal(engagement.Id, stored.EngagementId);
        Assert.Equal(ProjectParticipant.ActiveStatus, (await verify.ProjectParticipants.SingleAsync(x => x.ProjectId == project.Id)).Status);
    }

    private async Task<Engagement> SeedEngagementAsync(Guid organizationId, Guid creator)
    {
        var engagement = Engagement.Create(organizationId, creator, DateTimeOffset.UtcNow, "Client", Guid.NewGuid().ToString("N"), new DateOnly(2026, 1, 1));
        await using ProjectsDbContext db = Context();
        db.Engagements.Add(engagement);
        await db.SaveChangesAsync();
        return engagement;
    }

    private async Task<HttpResponseMessage> CreateAsync(object body, Guid key)
    {
        using HttpClient client = Client();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{_organization:D}/projects") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return await client.SendAsync(request);
    }

    private HttpClient Client()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(_user));
        return client;
    }

    private static async Task<ProjectCreationSnapshot> SnapshotAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<ProjectCreationSnapshot>(body.RootElement.GetProperty("data"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);
    private static string Token(Guid userId)
    {
        static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = userId, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        return message + "." + Base64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestConfiguration.Host()["Jwt:SecretKey"]!), Encoding.UTF8.GetBytes(message)));
    }

    private sealed class ApiFactory(AuthorityClient identity) : WebApplicationFactory<Program>
    {
        internal bool FailRole { get; set; }
        internal TaskCompletionSource? LockAttempted { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in TestConfiguration.Host()) { builder.UseSetting(key, value); }
            builder.UseSetting("ConnectionStrings:ApplicationDb", PostgreSqlFactAttribute.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuthorityClient>();
                services.AddSingleton<IIdentityAuthorityClient>(identity);
                services.RemoveAll<IProjectCreationStore>();
                services.AddScoped<IProjectCreationStore>(provider => new StoreHook(provider.GetRequiredService<ProjectsDbContext>(), () => FailRole, () => LockAttempted?.TrySetResult()));
            });
        }
    }

    private sealed class StoreHook(ProjectsDbContext db, Func<bool> fail, Action lockAttempted) : IProjectCreationStore
    {
        private readonly ProjectCreationStore _inner = new(db);
        public Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken) => _inner.LockAttemptAsync(organizationId, userId, key, cancellationToken);
        public Task<ProjectCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, ProjectCreationDetails details, CancellationToken cancellationToken) => _inner.FindReplayAsync(organizationId, userId, key, details, cancellationToken);
        public Task<Engagement?> LockEngagementAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
        {
            lockAttempted();
            return _inner.LockEngagementAsync(organizationId, engagementId, cancellationToken);
        }
        public void Stage(ProjectCreation creation, ProjectEvent entry, Guid userId, Guid key, ProjectCreationDetails details, DateTimeOffset now)
        {
            _inner.Stage(creation, entry, userId, key, details, now);
            if (fail()) { db.Entry(creation.AdministratorRole).Property(x => x.RoleCode).CurrentValue = "InvalidRole"; }
        }
    }

    private sealed class AuthorityClient : IIdentityAuthorityClient
    {
        internal string Mode { get; set; } = "allowed";
        internal bool ReadAll { get; set; }
        public Task<IntegrationResult<DeliveryPilot.Identity.Contracts.UserLookupResult>> LookupUsersAsync(DeliveryPilot.Identity.Contracts.UserLookupRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken) => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));
        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            if (Mode == "unavailable") { return Task.FromResult(FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>()); }
            OrganizationAuthorityCheckResult result = Mode == "denied" ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow) : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, false, false, ReadAll ? [WorkspacePermissions.ProjectsCreate, WorkspacePermissions.EngagementsReadAll, WorkspacePermissions.EngagementsManage] : [WorkspacePermissions.ProjectsCreate, WorkspacePermissions.EngagementsManage]);
            if (Mode == "missing-permission") { result = result with { EffectivePermissions = [] }; }
            else if (!ReadAll && Mode == "allowed") { result = result with { EffectivePermissions = [WorkspacePermissions.ProjectsCreate] }; }
            return Task.FromResult(IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }
}
