using System.Net;
using System.Net.Http.Headers;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
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

namespace DeliveryPilot.Projects.Tests.Projects;

[Collection("Projects PostgreSQL")]
public sealed class ProjectReadHttpTests : IAsyncLifetime
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly Guid _otherOrganization = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _guest = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();
    private readonly AuthorityClient _identity = new();
    private readonly ReadTransactionObserver _transactions = new();
    private readonly ApiFactory _factory;
    private Guid _project;
    private Guid _otherProject;
    private Guid _foreignProject;
    private Guid _engagement;
    private Guid _memberParticipant;
    private Guid _guestParticipant;
    private Guid _memberProject;

    public ProjectReadHttpTests() => _factory = new ApiFactory(_identity, _transactions);

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
        DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Engagement engagement = Engagement.Create(_organization, _otherUser, now, "Client", "R", new DateOnly(2026, 1, 1));
        Project first = Project.Create(_organization, _otherUser, now, "Client project", "Purpose", Project.ClientKind, engagement.Id);
        Project second = Project.Create(_organization, _otherUser, now, "Other project", "Purpose", Project.InternalKind);
        Project third = Project.Create(_organization, _otherUser, now, "Member project", "Purpose", Project.InternalKind);
        Project foreign = Project.Create(_otherOrganization, _otherUser, now, "Foreign project", "Purpose", Project.InternalKind);
        _project = first.Id;
        _otherProject = second.Id;
        _foreignProject = foreign.Id;
        _memberProject = third.Id;
        _engagement = engagement.Id;
        db.Engagements.Add(engagement);
        db.Projects.AddRange(first, second, third, foreign);
        ProjectParticipant member = ProjectParticipant.CreateMember(_organization, first.Id, _member, now);
        _memberParticipant = member.Id;
        ProjectParticipant otherMember = ProjectParticipant.CreateMember(_organization, third.Id, _member, now);
        ProjectParticipant owner = ProjectParticipant.CreateMember(_organization, first.Id, _otherUser, now);
        ProjectParticipant foreignMember = ProjectParticipant.CreateMember(_otherOrganization, foreign.Id, _member, now);
        ProjectGuestInvitation invite = ProjectGuestInvitation.Issue(_organization, first.Id, _otherUser, now, "guest@example.test", ProjectRoles.ProjectManager, new byte[32]);
        ProjectGuestInvitation foreignInvite = ProjectGuestInvitation.Issue(_otherOrganization, foreign.Id, _otherUser, now, "guest@example.test", ProjectRoles.Viewer, new byte[32]);
        ProjectParticipant guest = ProjectParticipant.CreateGuest(_organization, first.Id, _guest, now, invite.Id);
        _guestParticipant = guest.Id;
        ProjectParticipant foreignGuest = ProjectParticipant.CreateGuest(_otherOrganization, foreign.Id, _guest, now, foreignInvite.Id);
        db.ProjectGuestInvitations.AddRange(invite, foreignInvite);
        db.ProjectParticipants.AddRange(member, otherMember, owner, foreignMember, guest, foreignGuest);
        db.ProjectParticipantRoles.AddRange(
            ProjectParticipantRole.Grant(member, ProjectRoles.BusinessAnalyst, _otherUser, now),
            ProjectParticipantRole.Grant(member, ProjectRoles.QualityAssurance, _otherUser, now),
            ProjectParticipantRole.Grant(otherMember, ProjectRoles.Developer, _otherUser, now),
            ProjectParticipantRole.Grant(owner, ProjectRoles.ProjectAdministrator, _otherUser, now),
            ProjectParticipantRole.Grant(foreignMember, ProjectRoles.Viewer, _otherUser, now),
            ProjectParticipantRole.Grant(guest, ProjectRoles.ProjectManager, _otherUser, now),
            ProjectParticipantRole.Grant(foreignGuest, ProjectRoles.Viewer, _otherUser, now));
        await db.SaveChangesAsync();
        await db.Projects.Where(x => x.Id == first.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.ClientPoWaiverReason, "Private waiver"));
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext db = Context();
        Guid[] organizations = [_organization, _otherOrganization];
        await db.ProjectParticipantRoles.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.ProjectParticipants.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.ProjectGuestInvitations.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.Projects.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.Engagements.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlFact]
    public async Task NonparticipantsAndCrossTenantRequestsAlwaysSeeNotFound()
    {
        _identity.IsOwner = true;
        using HttpResponseMessage hidden = await GetAsync(_otherUser, _organization, _otherProject);
        using HttpResponseMessage unknown = await GetAsync(_otherUser, _organization, Guid.NewGuid());
        using HttpResponseMessage foreign = await GetAsync(_member, _organization, _foreignProject);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await ErrorCodeAsync(unknown), await ErrorCodeAsync(hidden));
        Assert.Equal(await ErrorCodeAsync(unknown), await ErrorCodeAsync(foreign));
        using HttpClient client = Client(_otherUser);
        using HttpResponseMessage list = await client.GetAsync($"/api/v1/organizations/{_organization:D}/projects");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument page = await DocumentAsync(list);
        Assert.DoesNotContain(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == _otherProject);
    }

    [PostgreSqlFact]
    public async Task MemberAndGuestSeeOnlyTheirProjectsAndGuestDetailHidesFields()
    {
        using HttpResponseMessage memberResponse = await GetAsync(_member, _organization, _project);
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        using JsonDocument memberDoc = await DocumentAsync(memberResponse);
        JsonElement member = memberDoc.RootElement.GetProperty("data");
        Assert.Equal(_engagement, member.GetProperty("engagementId").GetGuid());
        Assert.Equal("Private waiver", member.GetProperty("clientPoWaiverReason").GetString());
        string[] roles = member.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { ProjectRoles.BusinessAnalyst, ProjectRoles.QualityAssurance }, roles);
        string[] permissions = member.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Contains(ProjectPermissions.EditBrief, permissions);
        Assert.DoesNotContain(ProjectPermissions.ManageParticipants, permissions);
        using (HttpResponseMessage developerResponse = await GetAsync(_member, _organization, _memberProject))
        {
            using JsonDocument developerDoc = await DocumentAsync(developerResponse);
            string[] developerPermissions = developerDoc.RootElement.GetProperty("data").GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToArray();
            Assert.DoesNotContain(ProjectPermissions.ManageParticipants, developerPermissions);
        }
        int before = _identity.WorkspaceCalls;
        _identity.Mode = "denied";
        using HttpResponseMessage guestResponse = await GetAsync(_guest, _organization, _project);
        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);
        Assert.Equal(before, _identity.WorkspaceCalls);
        using JsonDocument guestDoc = await DocumentAsync(guestResponse);
        JsonElement guest = guestDoc.RootElement.GetProperty("data");
        Assert.False(guest.TryGetProperty("engagementId", out _));
        Assert.False(guest.TryGetProperty("clientPoWaiverReason", out _));
        string[] guestPermissions = guest.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Contains(ProjectPermissions.EditBrief, guestPermissions);
        Assert.Contains(ProjectPermissions.EditStakeholders, guestPermissions);
        Assert.DoesNotContain(guestPermissions, ProjectPermissions.MemberOnly.Contains);
        Assert.DoesNotContain(ProjectPermissions.AssignClientPo, guestPermissions);
        Assert.DoesNotContain(ProjectPermissions.InitializeProject, guestPermissions);
        Assert.DoesNotContain(ProjectPermissions.ManageAdministrators, guestPermissions);
        using HttpResponseMessage other = await GetAsync(_guest, _organization, _otherProject);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using HttpClient guestClient = Client(_guest);
        using HttpResponseMessage list = await guestClient.GetAsync("/api/v1/me/guest-projects");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument page = await DocumentAsync(list);
        Guid[] ids = page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(_project, ids);
        Assert.Contains(_foreignProject, ids);
        Assert.DoesNotContain(_otherProject, ids);
    }

    [PostgreSqlFact]
    public async Task RoleRevocationAndInvalidGuestAdministratorGrantFailClosed()
    {
        await using (ProjectsDbContext db = Context())
        {
            await db.ProjectParticipantRoles.Where(x => x.ParticipantId == _memberParticipant && x.RoleCode == ProjectRoles.BusinessAnalyst).ExecuteUpdateAsync(x => x.SetProperty(r => r.RevokedAt, DateTimeOffset.UtcNow).SetProperty(r => r.RevokedByUserId, _otherUser));
        }
        using HttpResponseMessage revised = await GetAsync(_member, _organization, _project);
        using JsonDocument revisedBody = await DocumentAsync(revised);
        Assert.DoesNotContain(revisedBody.RootElement.GetProperty("data").GetProperty("permissions").EnumerateArray(), x => x.GetString() == ProjectPermissions.EditBrief);
        await using (ProjectsDbContext db = Context())
        {
            await db.ProjectParticipantRoles.Where(x => x.ParticipantId == _memberParticipant && x.RoleCode == ProjectRoles.QualityAssurance).ExecuteUpdateAsync(x => x.SetProperty(r => r.RevokedAt, DateTimeOffset.UtcNow).SetProperty(r => r.RevokedByUserId, _otherUser));
            await db.ProjectParticipantRoles.Where(x => x.ParticipantId == _guestParticipant).ExecuteUpdateAsync(x => x.SetProperty(r => r.RoleCode, ProjectRoles.ProjectAdministrator));
        }
        using HttpResponseMessage noRole = await GetAsync(_member, _organization, _project);
        Assert.Equal(HttpStatusCode.Forbidden, noRole.StatusCode);
        using HttpResponseMessage corruptGuest = await GetAsync(_guest, _organization, _project);
        Assert.Equal(HttpStatusCode.Forbidden, corruptGuest.StatusCode);
        using HttpClient guest = Client(_guest);
        using HttpResponseMessage list = await guest.GetAsync("/api/v1/me/guest-projects");
        using JsonDocument page = await DocumentAsync(list);
        Assert.DoesNotContain(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == _project);
    }

    [PostgreSqlFact]
    public async Task ReadQueriesOpenNoCommandTransactions()
    {
        using HttpClient member = Client(_member);
        using HttpResponseMessage list = await member.GetAsync($"/api/v1/organizations/{_organization:D}/projects");
        using HttpResponseMessage detail = await member.GetAsync($"/api/v1/organizations/{_organization:D}/projects/{_project:D}");
        using HttpResponseMessage participants = await member.GetAsync($"/api/v1/organizations/{_organization:D}/projects/{_project:D}/participants");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, participants.StatusCode);
        Assert.Equal(0, _transactions.Started);
    }

    [PostgreSqlFact]
    public async Task CurrentMembershipAndParticipationAreRecheckedAndDirectoryFailureIsTolerated()
    {
        using HttpClient client = Client(_member);
        string path = $"/api/v1/organizations/{_organization:D}/projects/{_project:D}/participants";
        using HttpResponseMessage participants = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, participants.StatusCode);
        using JsonDocument body = await DocumentAsync(participants);
        Assert.Contains(body.RootElement.GetProperty("data").EnumerateArray(), item => item.GetProperty("userId").GetGuid() == _member && item.GetProperty("displayName").GetString() == "Known user");
        _identity.LookupUnavailable = true;
        using HttpResponseMessage namesUnavailable = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, namesUnavailable.StatusCode);
        using JsonDocument nameless = await DocumentAsync(namesUnavailable);
        Assert.All(nameless.RootElement.GetProperty("data").EnumerateArray(), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("displayName").ValueKind));
        _identity.Mode = "denied";
        using HttpResponseMessage denied = await GetAsync(_member, _organization, _project);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        _identity.Mode = "unavailable";
        using HttpResponseMessage unavailable = await GetAsync(_member, _organization, _project);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        _identity.Mode = "allowed";
        await using (ProjectsDbContext db = Context())
        {
            await db.ProjectParticipants.Where(x => x.Id == _memberParticipant).ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, ProjectParticipant.RemovedStatus).SetProperty(p => p.EndedAt, DateTimeOffset.UtcNow).SetProperty(p => p.EndedByUserId, _otherUser));
        }
        using HttpResponseMessage removed = await GetAsync(_member, _organization, _project);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        using HttpResponseMessage list = await client.GetAsync($"/api/v1/organizations/{_organization:D}/projects");
        using JsonDocument page = await DocumentAsync(list);
        Assert.DoesNotContain(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == _project);
    }

    [PostgreSqlFact]
    public async Task MemberPagingIsTenantAndUserBound()
    {
        using HttpClient member = Client(_member);
        string route = $"/api/v1/organizations/{_organization:D}/projects";
        using HttpResponseMessage first = await member.GetAsync(route + "?limit=1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using JsonDocument page = await DocumentAsync(first);
        JsonElement data = page.RootElement.GetProperty("data");
        Guid firstId = data.GetProperty("items")[0].GetProperty("id").GetGuid();
        string cursor = data.GetProperty("nextCursor").GetString()!;
        using HttpResponseMessage second = await member.GetAsync(route + "?limit=1&cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using JsonDocument next = await DocumentAsync(second);
        Assert.NotEqual(firstId, next.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid());
        using HttpResponseMessage wrongTenant = await member.GetAsync($"/api/v1/organizations/{_otherOrganization:D}/projects?cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, wrongTenant.StatusCode);
        using HttpClient other = Client(_otherUser);
        using HttpResponseMessage wrongUser = await other.GetAsync(route + "?cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, wrongUser.StatusCode);
        using HttpResponseMessage invalid = await member.GetAsync(route + "?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid user, Guid organization, Guid project)
    {
        using HttpClient client = Client(user);
        return await client.GetAsync($"/api/v1/organizations/{organization:D}/projects/{project:D}");
    }

    private HttpClient Client(Guid user)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        return client;
    }

    private static async Task<JsonDocument> DocumentAsync(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) { using JsonDocument body = await DocumentAsync(response); return body.RootElement.GetProperty("error").GetProperty("code").GetString(); }
    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);
    private static string Token(Guid userId)
    {
        static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = userId, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        return message + "." + Base64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestConfiguration.Host()["Jwt:SecretKey"]!), Encoding.UTF8.GetBytes(message)));
    }

    private sealed class ApiFactory(AuthorityClient identity, ReadTransactionObserver transactions) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in TestConfiguration.Host()) { builder.UseSetting(key, value); }
            builder.UseSetting("ConnectionStrings:ApplicationDb", PostgreSqlFactAttribute.ConnectionString);
            builder.ConfigureServices(services => { services.RemoveAll<IIdentityAuthorityClient>(); services.AddSingleton<IIdentityAuthorityClient>(identity); services.AddDbContext<ProjectsDbContext>(options => options.AddInterceptors(transactions)); });
        }
    }

    private sealed class ReadTransactionObserver : DbTransactionInterceptor
    {
        internal int Started { get; private set; }
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            Started++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AuthorityClient : IIdentityAuthorityClient
    {
        internal string Mode { get; set; } = "allowed";
        internal bool IsOwner { get; set; }
        internal bool LookupUnavailable { get; set; }
        internal int WorkspaceCalls { get; private set; }
        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken) => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));
        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            WorkspaceCalls++;
            if (Mode == "unavailable") { return Task.FromResult(FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>()); }
            OrganizationAuthorityCheckResult result = Mode == "denied" ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow) : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, IsOwner, false, ["organization.engagements.read-all"]);
            return Task.FromResult(IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }

        public Task<IntegrationResult<UserLookupResult>> LookupUsersAsync(UserLookupRequest request, CancellationToken cancellationToken)
            => Task.FromResult(LookupUnavailable ? FakeIdentityAuthorityClient.Unavailable<UserLookupResult>() : IntegrationResult<UserLookupResult>.Success(new UserLookupResult(request.UserIds!.Select(id => new UserIntegrationDto(id, "Known user", null, "active")).ToArray()), HttpStatusCode.OK));
    }
}
