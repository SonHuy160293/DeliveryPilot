using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.Gateway;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static DeliveryPilot.Identity.Tests.Organizations.InvitationManagementHttpTests;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationGatewayJourneyTests
{
    private const string Api = "/identity/api/v1";

    [PostgreSqlFact]
    public async Task RoleInputBoundariesBuiltInsAndDelegatedSelfEscalationAreEnforcedAcrossGateway()
    {
        await using var rig = await Journey.CreateAsync();
        Fixture fixture = rig.Fixture;
        using HttpClient owner = await rig.AuthenticatedAsync(fixture.Owner);
        string roles = Org(fixture) + "/roles";
        foreach (string name in new[] { " ", new string('a', 161) })
        {
            await ErrorAsync(await owner.PostAsJsonAsync(roles, new { name, permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.BadRequest);
            await ErrorAsync(await owner.PutAsJsonAsync(roles + $"/{fixture.Role.Id}", new { name, expectedVersion = 1, permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.BadRequest);
        }

        await ErrorAsync(await owner.PostAsJsonAsync(roles, new { name = "Long description", description = new string('a', 1001), permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.BadRequest);
        await DataAsync(await owner.PostAsJsonAsync(roles, new { name = new string('a', 160), description = new string('a', 1000), permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Created);
        JsonElement trimmed = await DataAsync(await owner.PostAsJsonAsync(roles, new { name = "  Unique reviewer  ", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Created);
        Assert.Equal("Unique reviewer", trimmed.GetProperty("name").GetString());
        await ErrorAsync(await owner.PostAsJsonAsync(roles, new { name = "UNIQUE REVIEWER", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Conflict);
        foreach (OrganizationRole builtIn in fixture.Seed.Roles)
        {
            await ErrorAsync(await owner.PostAsJsonAsync(roles, new { name = builtIn.Name, permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Conflict);
            await ErrorAsync(await owner.PutAsJsonAsync(roles + $"/{builtIn.Id}", new { name = "Changed built in", expectedVersion = 1, permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Conflict);
        }

        await using IdentityDbContext db = fixture.NewDb();
        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.RolesAssign));
        await db.SaveChangesAsync();
        using HttpClient delegated = await rig.AuthenticatedAsync(fixture.Delegate);
        JsonElement created = await DataAsync(await delegated.PostAsJsonAsync(roles, new { name = "Delegated reviewer", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Created);
        Guid member = await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Ordinary.Id).Select(item => item.Id).SingleAsync();
        await DataAsync(await delegated.PostAsJsonAsync(Org(fixture) + $"/members/{member}/roles/{created.GetProperty("id").GetGuid()}/grant", new { expectedMembershipVersion = 1 }), HttpStatusCode.OK);
        string[] codes = await db.OrganizationRolePermissions.Where(item => item.RoleId == fixture.DelegateRole.Id).Select(item => item.PermissionCode).ToArrayAsync();
        await ErrorAsync(await delegated.PutAsJsonAsync(roles + $"/{fixture.DelegateRole.Id}", new { name = fixture.DelegateRole.Name, expectedVersion = 1, permissionCodes = codes.Append(OrganizationCapabilityCatalog.AuditRead).ToArray() }), HttpStatusCode.Forbidden);
        Assert.False(await db.OrganizationRolePermissions.AnyAsync(item => item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.AuditRead));
    }

    [PostgreSqlFact]
    public async Task EveryPublishedActionReachesBearerAuthenticationAndCurrentSessionsAndScopesRemainAuthoritative()
    {
        await using var rig = await Journey.CreateAsync();
        using HttpClient anonymous = rig.Client();
        JsonNode config = JsonNode.Parse(await File.ReadAllTextAsync(rig.RoutesFile))!;
        foreach ((string id, JsonNode? value) in config["ReverseProxy"] !["Routes"] !.AsObject())
        {
            if (id is "identity-register" or "identity-login" or "identity-refresh" or "identity-logout" or "notification-resend")
            {
                continue;
            }

            string path = System.Text.RegularExpressions.Regex.Replace(value!["Match"] !["Path"] !.GetValue<string>(), @"\{\w+:guid\}", rig.Fixture.Organization.Id.ToString());
            string method = value["Match"] !["Methods"] ![0] !.GetValue<string>();
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
            await ErrorAsync(await anonymous.SendAsync(request), HttpStatusCode.Unauthorized);
        }

        using HttpClient owner = await rig.AuthenticatedAsync(rig.Fixture.Owner);
        using HttpClient foreign = await rig.AuthenticatedAsync(rig.Fixture.Foreign);
        await DataAsync(await owner.GetAsync(Org(rig.Fixture) + "/members"), HttpStatusCode.OK);
        await ErrorAsync(await foreign.GetAsync(Org(rig.Fixture) + "/members"), HttpStatusCode.Forbidden);
        await ErrorAsync(await owner.GetAsync($"{Api}/organizations/{rig.Fixture.ForeignOrganization.Id}/audit-events"), HttpStatusCode.Forbidden);
        await using IdentityDbContext db = rig.Fixture.NewDb();
        await db.AuthSessions.Where(item => item.UserId == rig.Fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, rig.Fixture.Clock.Now));
        await ErrorAsync(await owner.GetAsync(Org(rig.Fixture) + "/members"), HttpStatusCode.Unauthorized);
        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == rig.Fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task CreationAndReplayThroughGatewayPreserveIdempotencyAndWorkspaceIsolation()
    {
        await using var rig = await Journey.CreateAsync();
        using HttpClient creator = await rig.AuthenticatedAsync(rig.Fixture.Foreign);
        JsonElement before = await DataAsync(await creator.GetAsync(Api + "/me/organizations"), HttpStatusCode.OK);
        Assert.Empty(before.GetProperty("items").EnumerateArray());
        creator.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        JsonElement created = await DataAsync(await creator.PostAsJsonAsync(Api + "/organizations", new { name = "Gateway workspace" }), HttpStatusCode.Created);
        Guid organizationId = created.GetProperty("organizationId").GetGuid();
        rig.CreatedOrganizations.Add(organizationId);
        JsonElement replay = await DataAsync(await creator.PostAsJsonAsync(Api + "/organizations", new { name = "Gateway workspace" }), HttpStatusCode.OK);
        Assert.Equal(created.GetRawText(), replay.GetRawText());
        await ErrorAsync(await creator.PostAsJsonAsync(Api + "/organizations", new { name = "Changed intent" }), HttpStatusCode.Conflict);
        await DataAsync(await creator.GetAsync(Api + "/me/organizations"), HttpStatusCode.OK);
        await DataAsync(await creator.GetAsync($"{Api}/organizations/{organizationId}/members/me"), HttpStatusCode.OK);
        await DataAsync(await creator.GetAsync($"{Api}/organizations/{organizationId}/permissions"), HttpStatusCode.OK);
        using HttpClient unrelated = await rig.AuthenticatedAsync(rig.Fixture.Owner);
        await ErrorAsync(await unrelated.GetAsync($"{Api}/organizations/{organizationId}/members"), HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task ManagerRecipientRejoinOwnershipStepDownAndAuditJourneyUsesOnlyPublicGatewayRoutes()
    {
        await using var rig = await Journey.CreateAsync();
        Fixture fixture = rig.Fixture;
        using HttpClient owner = await rig.AuthenticatedAsync(fixture.Owner);
        using HttpClient recipient = await rig.AuthenticatedAsync(fixture.Foreign);
        string org = Org(fixture);
        JsonElement role = await DataAsync(await owner.PostAsJsonAsync(org + "/roles", new { name = "Gateway reviewer", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } }), HttpStatusCode.Created);
        Guid roleId = role.GetProperty("id").GetGuid();
        await DataAsync(await owner.PutAsJsonAsync(org + $"/roles/{roleId}", new { name = "Gateway observer", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead }, expectedVersion = 1 }), HttpStatusCode.OK);
        await ErrorAsync(await owner.PutAsJsonAsync(org + $"/roles/{roleId}", new { name = "Stale change", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead }, expectedVersion = 1 }), HttpStatusCode.Conflict);
        await DataAsync(await owner.GetAsync(org + "/roles?limit=2"), HttpStatusCode.OK);
        JsonElement issued = await DataAsync(await owner.PostAsJsonAsync(org + "/invitations", new { recipientEmail = fixture.Foreign.PrimaryEmail, roleIds = new[] { fixture.Seed.AdminRole.Id, roleId } }), HttpStatusCode.Created);
        Guid invitation = issued.GetProperty("id").GetGuid();
        string proof = await rig.ProofAsync(invitation);
        JsonElement preview = await DataAsync(await recipient.PostAsJsonAsync(Recipient(invitation, "preview"), new { proof }), HttpStatusCode.OK);
        Assert.True(preview.GetProperty("canAccept").GetBoolean());
        JsonElement accepted = await DataAsync(await recipient.PostAsJsonAsync(Recipient(invitation, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        Guid membership = accepted.GetProperty("membershipId").GetGuid();
        await DataAsync(await recipient.PostAsJsonAsync(Recipient(invitation, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        await using IdentityDbContext db = fixture.NewDb();
        DateTimeOffset joinedAt = await db.OrganizationMemberships.Where(item => item.Id == membership).Select(item => item.CreatedAt).SingleAsync();
        long version = accepted.GetProperty("membershipVersion").GetInt64();
        await DataAsync(await owner.GetAsync(org + $"/members/{membership}"), HttpStatusCode.OK);
        await DataAsync(await owner.PostAsJsonAsync(org + $"/members/{membership}/remove", new { expectedMembershipVersion = version, reason = "Contract completed" }), HttpStatusCode.OK);
        await ErrorAsync(await recipient.GetAsync(org + "/members/me"), HttpStatusCode.Forbidden);
        await DataAsync(await owner.PostAsJsonAsync(org + $"/roles/{roleId}/retire", new { expectedVersion = 2, reason = "Retired after removal" }), HttpStatusCode.OK);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        JsonElement fresh = await DataAsync(await owner.PostAsJsonAsync(org + "/invitations", new { recipientEmail = fixture.Foreign.PrimaryEmail, roleIds = new[] { fixture.Seed.MemberRole.Id } }), HttpStatusCode.Created);
        Guid freshId = fresh.GetProperty("id").GetGuid();
        string freshProof = await rig.ProofAsync(freshId);
        JsonElement rejoined = await DataAsync(await recipient.PostAsJsonAsync(Recipient(freshId, "accept"), new { proof = freshProof, expectedVersion = 1 }), HttpStatusCode.OK);
        Assert.Equal(membership, rejoined.GetProperty("membershipId").GetGuid());
        Assert.Single(rejoined.GetProperty("roles").EnumerateArray());
        Assert.Equal("Member", rejoined.GetProperty("roles")[0].GetProperty("name").GetString());
        Assert.Equal(joinedAt, await db.OrganizationMemberships.Where(item => item.Id == membership).Select(item => item.CreatedAt).SingleAsync());
        version = rejoined.GetProperty("membershipVersion").GetInt64();
        string assignment = org + $"/members/{membership}/roles/{fixture.Role.Id}";
        JsonElement grant = await DataAsync(await owner.PostAsJsonAsync(assignment + "/grant", new { expectedMembershipVersion = version }), HttpStatusCode.OK);
        await DataAsync(await owner.PostAsJsonAsync(assignment + "/revoke", new { expectedMembershipVersion = grant.GetProperty("version").GetInt64() }), HttpStatusCode.OK);
        JsonElement nomination = await DataAsync(await owner.PostAsJsonAsync(org + "/ownership-nominations", new { membershipId = membership }), HttpStatusCode.Created);
        Guid nominationId = nomination.GetProperty("id").GetGuid();
        await DataAsync(await recipient.GetAsync(Api + "/me/ownership-nominations"), HttpStatusCode.OK);
        await DataAsync(await owner.GetAsync(org + "/ownership-nominations"), HttpStatusCode.OK);
        await DataAsync(await recipient.PostAsJsonAsync(org + $"/ownership-nominations/{nominationId}/accept", new { expectedVersion = 1 }), HttpStatusCode.OK);
        Guid ownerMembership = await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Owner.Id).Select(item => item.Id).SingleAsync();
        await DataAsync(await owner.PostAsJsonAsync(org + $"/members/{ownerMembership}/demote-owner", new { expectedMembershipVersion = 1, reason = "Explicit step down" }), HttpStatusCode.OK);
        long nomineeVersion = await db.OrganizationMemberships.Where(item => item.Id == membership).Select(item => item.Version).SingleAsync();
        Assert.Equal("Organization.OwnerContinuityRequired", await ErrorAsync(await recipient.PostAsJsonAsync(org + $"/members/{membership}/demote-owner", new { expectedMembershipVersion = nomineeVersion, reason = "Last Owner" }), HttpStatusCode.Conflict));
        JsonElement history = await DataAsync(await recipient.GetAsync(org + $"/audit-events?membershipId={membership}&limit=2"), HttpStatusCode.OK);
        Assert.NotEqual(JsonValueKind.Null, history.GetProperty("nextCursor").ValueKind);
        string continuation = org + $"/audit-events?membershipId={membership}&limit=2&cursor=" + Uri.EscapeDataString(history.GetProperty("nextCursor").GetString()!);
        await DataAsync(await recipient.GetAsync(continuation), HttpStatusCode.OK);
        await ErrorAsync(await owner.GetAsync(continuation), HttpStatusCode.Forbidden);
        Assert.DoesNotContain(proof, history.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal) || line.Contains(freshProof, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task ResendReissueDeclineCancelAndRateLimitRemainSafeAcrossGateway()
    {
        await using var rig = await Journey.CreateAsync();
        Fixture fixture = rig.Fixture;
        using HttpClient owner = await rig.AuthenticatedAsync(fixture.Owner);
        using HttpClient recipient = await rig.AuthenticatedAsync(fixture.Removed);
        string org = Org(fixture);
        JsonElement issued = await DataAsync(await owner.PostAsJsonAsync(org + "/invitations", new { recipientEmail = fixture.Removed.PrimaryEmail, roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        using (HttpResponseMessage limited = await owner.PostAsJsonAsync(org + $"/invitations/{id}/resend", new { expectedVersion = 1 }))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.CacheControl?.NoStore);
            Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        }

        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        JsonElement resent = await DataAsync(await owner.PostAsJsonAsync(org + $"/invitations/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.OK);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        JsonElement reissued = await DataAsync(await owner.PostAsJsonAsync(org + $"/invitations/{id}/reissue", new { expectedVersion = resent.GetProperty("version").GetInt64(), roleIds = new[] { fixture.Seed.MemberRole.Id }, reason = "New role offer" }), HttpStatusCode.OK);
        Guid replacement = reissued.GetProperty("id").GetGuid();
        string proof = await rig.ProofAsync(replacement);
        await DataAsync(await recipient.PostAsJsonAsync(Recipient(replacement, "decline"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        JsonElement cancellable = await DataAsync(await owner.PostAsJsonAsync(org + "/invitations", new { recipientEmail = "gateway-cancel@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        Guid cancelId = cancellable.GetProperty("id").GetGuid();
        await using IdentityDbContext db = fixture.NewDb();
        Guid delivery = await db.OrganizationInvitations.Where(item => item.Id == cancelId).Select(item => item.CurrentDeliveryMessageId!.Value).SingleAsync();
        await fixture.RecordStatusAsync(rig.Identity, delivery, 2, "failed");
        JsonElement detail = await DataAsync(await owner.GetAsync(org + $"/invitations/{cancelId}"), HttpStatusCode.OK);
        Assert.Equal("pending", detail.GetProperty("status").GetString());
        Assert.Equal("failed", detail.GetProperty("delivery").GetProperty("status").GetString());
        await DataAsync(await owner.PostAsJsonAsync(org + $"/invitations/{cancelId}/cancel", new { expectedVersion = 1, reason = "Offer withdrawn" }), HttpStatusCode.OK);
        await DataAsync(await owner.GetAsync(org + "/invitations?status=cancelled&limit=1"), HttpStatusCode.OK);
    }

    private static string Org(Fixture fixture) => Api + $"/organizations/{fixture.Organization.Id}";
    private static string Recipient(Guid id, string action) => Api + $"/organization-invitations/{id}/{action}";

    private sealed class Journey : IAsyncDisposable
    {
        public Fixture Fixture { get; private set; } = null!;
        public WebApplication Identity { get; private set; } = null!;
        public WebApplication Gateway { get; private set; } = null!;
        public List<Guid> CreatedOrganizations { get; } = [];
        public string RoutesFile { get; } = Path.Combine(Path.GetTempPath(), "dp-organization-gateway-" + Guid.NewGuid().ToString("N") + ".json");

        public static async Task<Journey> CreateAsync()
        {
            var journey = new Journey();
            try
            {
                journey.Fixture = await Fixture.CreateAsync();
                journey.Identity = await journey.Fixture.StartAppAsync(useKestrel: true, configure: builder =>
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:SecretKey"] = "gateway-tests-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "gateway-tests", ["Jwt:Audience"] = "gateway-tests",
                        ["Jwt:AccessTokenExpirationMinutes"] = "30", ["Jwt:RefreshTokenExpirationDays"] = "7",
                    });
                    builder.Services.AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment);
                });
                string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DeliveryPilot.Gateway"));
                JsonNode routes = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "proxy.json")))!;
                routes["ReverseProxy"] !["Clusters"] !["identity"] !["Destinations"] !["identity-api"] !["Address"] = journey.Identity.Urls.Single() + "/";
                await File.WriteAllTextAsync(journey.RoutesFile, routes.ToJsonString());
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Development", ApplicationName = typeof(GatewayApplication).Assembly.FullName });
                builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:RoutesFile"] = journey.RoutesFile, ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1" });
                journey.Gateway = GatewayApplication.Build(builder);
                await journey.Gateway.StartAsync();
                return journey;
            }
            catch
            {
                await journey.DisposeAsync();
                throw;
            }
        }

        public HttpClient Client() => new() { BaseAddress = new Uri(Gateway.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };

        public async Task<HttpClient> AuthenticatedAsync(User user)
        {
            await using IdentityDbContext db = Fixture.NewDb();
            var session = AuthSession.Create(Guid.CreateVersion7(), user.Id, RandomNumberGenerator.GetBytes(32), Fixture.Clock.Now, Fixture.Clock.Now.AddDays(1));
            db.AuthSessions.Add(session);
            await db.SaveChangesAsync();
            HttpClient client = Client();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Identity.Services.GetRequiredService<JwtAccessTokenIssuer>().Issue(user.Id, session.Id, Fixture.Clock.Now).Token);
            return client;
        }

        public async Task<string> ProofAsync(Guid id)
        {
            await using IdentityDbContext db = Fixture.NewDb();
            Guid delivery = await db.OrganizationInvitations.Where(item => item.Id == id).Select(item => item.CurrentDeliveryMessageId!.Value).SingleAsync();
            return Fixture.ReadProof(await db.Set<OutboxMessage>().Where(item => item.MessageId == delivery).Select(item => item.PayloadJson).SingleAsync());
        }

        public async ValueTask DisposeAsync()
        {
            if (Gateway is not null)
            {
                await Gateway.DisposeAsync();
            }

            if (Identity is not null)
            {
                await Identity.DisposeAsync();
            }

            if (Fixture is not null)
            {
                await using IdentityDbContext db = Fixture.NewDb();
                foreach (Guid id in CreatedOrganizations)
                {
                    await db.OrganizationCreationAttempts.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.IdentityAuditEvents.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.OrganizationRoleAssignments.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.OrganizationRolePermissions.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.OrganizationMemberships.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.OrganizationRoles.Where(item => item.OrganizationId == id).ExecuteDeleteAsync();
                    await db.Organizations.Where(item => item.Id == id).ExecuteDeleteAsync();
                }

                await Fixture.DisposeAsync();
            }

            File.Delete(RoutesFile);
        }
    }
}
