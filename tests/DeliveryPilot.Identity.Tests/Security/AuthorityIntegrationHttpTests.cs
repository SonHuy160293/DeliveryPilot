using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Domain.Authorization;
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

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class AuthorityIntegrationHttpTests
{
    private const string ApiKey = "isolated-authority-check-test-key";
    private const string SessionPath = "/api/v1/integrations/authority/session-checks";
    private const string OrganizationPath = "/api/v1/integrations/authority/organization-checks";

    [PostgreSqlFact]
    public async Task AuthorityChecksAnswerFromCurrentStateAndDiscloseNothingOnDenial()
    {
        await using WebApplication app = await StartAsync();
        using HttpClient client = app.GetTestClient();
        using IServiceScope scope = app.Services.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var owner = User.Create($"authority-owner-{Guid.NewGuid():N}@example.test", "Authority Owner", status: "active");
        var member = User.Create($"authority-member-{Guid.NewGuid():N}@example.test", "Authority Member", status: "active");
        var outsider = User.Create($"authority-outsider-{Guid.NewGuid():N}@example.test", "Authority Outsider", status: "active");
        var organization = Organization.Create("Authority Workspace", owner.Id, now);
        var otherOrganization = Organization.Create("Other Workspace", outsider.Id, now);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        OrganizationBuiltInRoleSeed otherBuiltIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(otherOrganization.Id, now);
        var ownerMembership = OrganizationMembership.Create(organization.Id, owner.Id, owner.Id, now);
        var memberMembership = OrganizationMembership.Create(organization.Id, member.Id, owner.Id, now);
        var outsiderMembership = OrganizationMembership.Create(otherOrganization.Id, outsider.Id, outsider.Id, now);
        var ownerSession = AuthSession.Create(Guid.CreateVersion7(), owner.Id, RandomNumberGenerator.GetBytes(32), now, now.AddDays(1));
        var memberSession = AuthSession.Create(Guid.CreateVersion7(), member.Id, RandomNumberGenerator.GetBytes(32), now, now.AddDays(1));

        context.Users.AddRange(owner, member, outsider);
        context.Organizations.AddRange(organization, otherOrganization);
        context.OrganizationRoles.AddRange(builtIns.Roles);
        context.OrganizationRoles.AddRange(otherBuiltIns.Roles);
        context.OrganizationRolePermissions.AddRange(builtIns.Permissions);
        context.OrganizationRolePermissions.AddRange(otherBuiltIns.Permissions);
        context.OrganizationMemberships.AddRange(ownerMembership, memberMembership, outsiderMembership);
        context.OrganizationRoleAssignments.AddRange(
            OrganizationRoleAssignment.Create(organization.Id, ownerMembership.Id, builtIns.OwnerRole.Id, owner.Id, now),
            OrganizationRoleAssignment.Create(organization.Id, memberMembership.Id, builtIns.MemberRole.Id, owner.Id, now),
            OrganizationRoleAssignment.Create(otherOrganization.Id, outsiderMembership.Id, otherBuiltIns.OwnerRole.Id, outsider.Id, now));
        context.AuthSessions.AddRange(ownerSession, memberSession);
        await context.SaveChangesAsync();

        try
        {
            using (HttpResponseMessage missingKey = await client.PostAsJsonAsync(SessionPath, new { userId = owner.Id, sessionId = ownerSession.Id }))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, missingKey.StatusCode);
            }

            client.DefaultRequestHeaders.Add("X-Integration-Key", "invalid-key");
            using (HttpResponseMessage invalidKey = await client.PostAsJsonAsync(OrganizationPath, new { userId = owner.Id, organizationId = organization.Id }))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, invalidKey.StatusCode);
            }

            client.DefaultRequestHeaders.Remove("X-Integration-Key");
            client.DefaultRequestHeaders.Add("X-Integration-Key", ApiKey);

            // Session checks.
            Assert.True(await ReadSessionActiveAsync(client, owner.Id, ownerSession.Id));
            Assert.False(await ReadSessionActiveAsync(client, member.Id, ownerSession.Id));
            Assert.False(await ReadSessionActiveAsync(client, owner.Id, Guid.CreateVersion7()));

            // Allowed: Owner with an active session receives current authority facts.
            JsonElement allowed = await ReadOrganizationCheckAsync(client, new { userId = owner.Id, organizationId = organization.Id, sessionId = ownerSession.Id });
            Assert.Equal("allowed", allowed.GetProperty("decision").GetString());
            Assert.Equal(organization.Id, allowed.GetProperty("organizationId").GetGuid());
            Assert.Equal(ownerMembership.Id, allowed.GetProperty("membershipId").GetGuid());
            Assert.True(allowed.GetProperty("isOwner").GetBoolean());
            Assert.Contains(
                allowed.GetProperty("effectivePermissions").EnumerateArray().Select(item => item.GetString()),
                code => code == OrganizationCapabilityCatalog.MembersRead);
            Assert.Contains(
                allowed.GetProperty("effectivePermissions").EnumerateArray().Select(item => item.GetString()),
                code => code == OrganizationCapabilityCatalog.EngagementsManage);
            Assert.False(allowed.TryGetProperty("primaryEmail", out _));

            // Queued-work reauthorization without a session and with a held permission.
            JsonElement job = await ReadOrganizationCheckAsync(client, new { userId = owner.Id, organizationId = organization.Id, requiredPermissionCode = OrganizationCapabilityCatalog.MembersRead });
            Assert.Equal("allowed", job.GetProperty("decision").GetString());

            // Denied cases share one body with no membership or role details.
            await AssertDeniedAsync(client, new { userId = member.Id, organizationId = organization.Id, sessionId = memberSession.Id, requiredPermissionCode = OrganizationCapabilityCatalog.MembersRead });
            await AssertDeniedAsync(client, new { userId = owner.Id, organizationId = otherOrganization.Id, sessionId = ownerSession.Id });
            await AssertDeniedAsync(client, new { userId = outsider.Id, organizationId = organization.Id });
            await AssertDeniedAsync(client, new { userId = owner.Id, organizationId = Guid.CreateVersion7() });
            await AssertDeniedAsync(client, new { userId = member.Id, organizationId = organization.Id, sessionId = ownerSession.Id });

            // Committed revocations deny the next decision (ACCESS-02).
            await context.AuthSessions.Where(item => item.Id == ownerSession.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
            Assert.False(await ReadSessionActiveAsync(client, owner.Id, ownerSession.Id));
            await AssertDeniedAsync(client, new { userId = owner.Id, organizationId = organization.Id, sessionId = ownerSession.Id });

            JsonElement memberBeforeRemoval = await ReadOrganizationCheckAsync(client, new { userId = member.Id, organizationId = organization.Id, sessionId = memberSession.Id });
            Assert.Equal("allowed", memberBeforeRemoval.GetProperty("decision").GetString());
            Assert.Empty(memberBeforeRemoval.GetProperty("effectivePermissions").EnumerateArray());
            await context.OrganizationMemberships.Where(item => item.Id == memberMembership.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "removed"));
            await AssertDeniedAsync(client, new { userId = member.Id, organizationId = organization.Id, sessionId = memberSession.Id });

            // Validation failures keep the trace ID.
            using HttpResponseMessage invalid = await client.PostAsJsonAsync(OrganizationPath, new { userId = Guid.Empty, organizationId = organization.Id, sessionId = Guid.Empty, requiredPermissionCode = "Not A Code" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using JsonDocument invalidBody = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(invalidBody.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
            using HttpResponseMessage invalidSession = await client.PostAsJsonAsync(SessionPath, new { userId = owner.Id, sessionId = Guid.Empty });
            Assert.Equal(HttpStatusCode.BadRequest, invalidSession.StatusCode);
        }
        finally
        {
            context.ChangeTracker.Clear();
            Guid[] organizationIds = [organization.Id, otherOrganization.Id];
            Guid[] userIds = [owner.Id, member.Id, outsider.Id];
            await context.AuthSessions.Where(item => userIds.Contains(item.UserId)).ExecuteDeleteAsync();
            await context.OrganizationRoleAssignments.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
            await context.OrganizationRolePermissions.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
            await context.OrganizationRoles.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
            await context.OrganizationMemberships.Where(item => organizationIds.Contains(item.OrganizationId)).ExecuteDeleteAsync();
            await context.Organizations.Where(item => organizationIds.Contains(item.Id)).ExecuteDeleteAsync();
            await context.Users.Where(item => userIds.Contains(item.Id)).ExecuteDeleteAsync();
        }
    }

    private static async Task<WebApplication> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDb"] = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"),
            ["IntegrationApi:ApiKeys:0"] = ApiKey,
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        builder.Services.AddConfiguredAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>())
            .AddApplicationPart(typeof(AuthorityController).Assembly);
        WebApplication app = builder.Build();
        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static async Task<bool> ReadSessionActiveAsync(HttpClient client, Guid userId, Guid sessionId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionPath, new { userId, sessionId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").GetProperty("active").GetBoolean();
    }

    private static async Task<JsonElement> ReadOrganizationCheckAsync(HttpClient client, object request)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(OrganizationPath, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").Clone();
    }

    private static async Task AssertDeniedAsync(HttpClient client, object request)
    {
        JsonElement denied = await ReadOrganizationCheckAsync(client, request);
        Assert.Equal("denied", denied.GetProperty("decision").GetString());
        string[] names = denied.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["decision", "evaluatedAt"], names);
    }
}
