using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationReadPostgresHttpTests
{
    private static readonly string[] _unknownPermission = ["unknown"];

    [PostgreSqlFact]
    public async Task CreateRoleChecksCeilingAndPersistsAuditAtomically()
    {
        string connection = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"role-create-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        var targetUser = User.Create($"role-target-{Guid.NewGuid():N}@example.test", "Target", status: "active");
        var delegateUser = User.Create($"role-manager-{Guid.NewGuid():N}@example.test", "Manager", status: "active");
        var organization = Organization.Create("Role test", user.Id, now);
        var seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        var membership = OrganizationMembership.Create(organization.Id, user.Id, user.Id, now);
        var targetMembership = OrganizationMembership.Create(organization.Id, targetUser.Id, user.Id, now);
        var delegateMembership = OrganizationMembership.Create(organization.Id, delegateUser.Id, user.Id, now);
        var managerRole = OrganizationRole.CreateCustom(organization.Id, "Manager", user.Id, now);
        var assignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, seed.OwnerRole.Id, user.Id, now);
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        await using var db = new IdentityDbContext(options);
        try
        {
            db.Users.Add(user);
            db.Users.Add(targetUser);
            db.Users.Add(delegateUser);
            db.Organizations.Add(organization);
            db.OrganizationMemberships.Add(membership);
            db.OrganizationMemberships.Add(targetMembership);
            db.OrganizationMemberships.Add(delegateMembership);
            db.OrganizationRoles.AddRange(seed.Roles);
            db.OrganizationRoles.Add(managerRole);
            db.OrganizationRolePermissions.AddRange(seed.Permissions);
            db.OrganizationRolePermissions.AddRange(new[] { OrganizationCapabilityCatalog.RolesManage, OrganizationCapabilityCatalog.RolesAssign, OrganizationCapabilityCatalog.MembersRead }.Select(code => OrganizationRolePermission.Create(organization.Id, managerRole.Id, code)));
            db.OrganizationRoleAssignments.Add(assignment);
            db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(organization.Id, delegateMembership.Id, managerRole.Id, user.Id, now));
            await db.SaveChangesAsync();
            await using var app = await StartAppAsync(connection);
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-User", user.Id.ToString());
            string route = $"/api/v1/organizations/{organization.Id}/roles";
            var payload = new { name = " Reader ", description = "test", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead } };
            using var created = await client.PostAsJsonAsync(route, payload);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            JsonElement role = json.RootElement.GetProperty("data");
            Guid roleId = role.GetProperty("id").GetGuid();
            Assert.Equal($"role-{roleId:N}", role.GetProperty("code").GetString());
            Assert.True(await db.OrganizationRolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionCode == OrganizationCapabilityCatalog.MembersRead));
            Assert.True(await db.IdentityAuditEvents.AnyAsync(x => x.AggregateId == roleId && x.EventType == "organization.role.created"));
            using var rolePage = await client.GetAsync($"{route}?limit=1");
            Assert.Equal(HttpStatusCode.OK, rolePage.StatusCode);
            using var pageJson = JsonDocument.Parse(await rolePage.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(pageJson.RootElement.GetProperty("data").GetProperty("nextCursor").GetString()));
            using var catalog = await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions");
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            using var catalogJson = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync());
            Assert.Contains(catalogJson.RootElement.GetProperty("data").GetProperty("codes").EnumerateArray(), code => code.GetString() == OrganizationCapabilityCatalog.MembersRead);
            Assert.DoesNotContain(catalogJson.RootElement.GetProperty("data").GetProperty("codes").EnumerateArray(), code => code.GetString() == OrganizationCapabilityCatalog.OwnershipManage);
            var update = new { name = "New reader", description = "updated", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.AuditRead }, expectedVersion = 1 };
            using var edited = await client.PutAsJsonAsync($"{route}/{roleId}", update);
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            using var editedJson = JsonDocument.Parse(await edited.Content.ReadAsStringAsync());
            Assert.Equal(3, editedJson.RootElement.GetProperty("data").GetProperty("version").GetInt64());
            Assert.Equal(2, editedJson.RootElement.GetProperty("data").GetProperty("permissionSetVersion").GetInt64());
            Assert.True(await db.IdentityAuditEvents.AnyAsync(x => x.AggregateId == roleId && x.EventType == "organization.role.updated"));
            Assert.True(await db.IdentityAuditEvents.AnyAsync(x => x.AggregateId == roleId && x.EventType == "organization.role.permissions-changed"));
            using var reduced = await client.PutAsJsonAsync($"{route}/{roleId}", new { name = "New reader", description = "updated", permissionCodes = payload.permissionCodes, expectedVersion = 3 });
            Assert.Equal(HttpStatusCode.OK, reduced.StatusCode);
            using var restored = await client.PutAsJsonAsync($"{route}/{roleId}", new { name = "New reader", description = "updated", permissionCodes = update.permissionCodes, expectedVersion = 4 });
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            using var restoredJson = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
            Assert.Equal(4, restoredJson.RootElement.GetProperty("data").GetProperty("permissionSetVersion").GetInt64());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{route}/{roleId}", new { name = "Stale", description = "updated", permissionCodes = update.permissionCodes, expectedVersion = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{route}/{seed.OwnerRole.Id}", update)).StatusCode);
            string assignmentRoute = $"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{roleId}";
            using var grant = await client.PostAsJsonAsync($"{assignmentRoute}/grant", new { expectedMembershipVersion = 1 });
            Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
            using var grantJson = JsonDocument.Parse(await grant.Content.ReadAsStringAsync());
            Assert.True(grantJson.RootElement.GetProperty("data").GetProperty("changed").GetBoolean());
            Assert.Equal(2, grantJson.RootElement.GetProperty("data").GetProperty("version").GetInt64());
            using var repeated = await client.PostAsJsonAsync($"{assignmentRoute}/grant", new { expectedMembershipVersion = 1 });
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            using var repeatedJson = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
            Assert.False(repeatedJson.RootElement.GetProperty("data").GetProperty("changed").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{assignmentRoute}/revoke", new { expectedMembershipVersion = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{assignmentRoute}/revoke", new { expectedMembershipVersion = 2 })).StatusCode);
            using var repeatedRevoke = await client.PostAsJsonAsync($"{assignmentRoute}/revoke", new { expectedMembershipVersion = 2 });
            Assert.Equal(HttpStatusCode.OK, repeatedRevoke.StatusCode);
            using var repeatedRevokeJson = JsonDocument.Parse(await repeatedRevoke.Content.ReadAsStringAsync());
            Assert.False(repeatedRevokeJson.RootElement.GetProperty("data").GetProperty("changed").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{assignmentRoute}/grant", new { expectedMembershipVersion = 3 })).StatusCode);
            Assert.Equal(2, await db.OrganizationRoleAssignments.Where(x => x.MembershipId == targetMembership.Id && x.RoleId == roleId).Select(x => x.GrantVersion).SingleAsync());
            Assert.Equal(3, await db.IdentityAuditEvents.CountAsync(x => x.MembershipId == targetMembership.Id));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{seed.OwnerRole.Id}/grant", new { expectedMembershipVersion = 4 })).StatusCode);
            Guid[] raceRoles = new Guid[2];
            for (int i = 0; i < raceRoles.Length; i++)
            {
                using var response = await client.PostAsJsonAsync(route, new { name = $"Race {i}", permissionCodes = payload.permissionCodes });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                raceRoles[i] = responseJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
            }

            HttpResponseMessage[] concurrent = await Task.WhenAll(raceRoles.Select(id => client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{id}/grant", new { expectedMembershipVersion = 4 })));
            Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Conflict);
            foreach (HttpResponseMessage response in concurrent)
            {
                response.Dispose();
            }

            string adminRoute = $"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{seed.AdminRole.Id}";
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{adminRoute}/grant", new { expectedMembershipVersion = 5 })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", targetUser.Id.ToString());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{adminRoute}/revoke", new { expectedMembershipVersion = 6, reason = "Demotion" })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", user.Id.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{adminRoute}/revoke", new { expectedMembershipVersion = 6 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{adminRoute}/revoke", new { expectedMembershipVersion = 6, reason = "Demotion" })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", targetUser.Id.ToString());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", delegateUser.Id.ToString());
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route, new { name = "Delegated", permissionCodes = payload.permissionCodes })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(route, new { name = "Escalation", permissionCodes = new[] { OrganizationCapabilityCatalog.AuditRead } })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{assignmentRoute}/grant", new { expectedMembershipVersion = 7 })).StatusCode);
            Guid remainingRole = await db.OrganizationRoleAssignments.Where(x => x.MembershipId == targetMembership.Id && raceRoles.Contains(x.RoleId)).Select(x => x.RoleId).SingleAsync() == raceRoles[0] ? raceRoles[1] : raceRoles[0];
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{remainingRole}/grant", new { expectedMembershipVersion = 7 })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", user.Id.ToString());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/organizations/{Guid.NewGuid()}/roles", payload)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{route}/{Guid.NewGuid()}", update)).StatusCode);
            await db.OrganizationRoles.Where(x => x.Id == managerRole.Id).ExecuteUpdateAsync(x => x.SetProperty(role => role.RetiredAt, now));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{route}/{managerRole.Id}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members/{targetMembership.Id}/roles/{managerRole.Id}/grant", new { expectedMembershipVersion = 8 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { name = "new reader", permissionCodes = payload.permissionCodes })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { name = "Owner", permissionCodes = payload.permissionCodes })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { name = "Other", permissionCodes = _unknownPermission })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { name = "Other", permissionCodes = new[] { OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.MembersRead } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { name = "Other", permissionCodes = payload.permissionCodes, extra = "unexpected" })).StatusCode);
            await using (var failingApp = await StartAppAsync(connection, failAudit: true))
            {
                using var failingClient = failingApp.GetTestClient();
                failingClient.DefaultRequestHeaders.Add("X-Test-User", user.Id.ToString());
                Assert.Equal(HttpStatusCode.InternalServerError, (await failingClient.PostAsJsonAsync(route, new { name = "Audit failure", permissionCodes = payload.permissionCodes })).StatusCode);
                Assert.False(await db.OrganizationRoles.AnyAsync(x => x.OrganizationId == organization.Id && x.Name == "Audit failure"));
                Assert.Equal(HttpStatusCode.InternalServerError, (await failingClient.PutAsJsonAsync($"{route}/{roleId}", new { name = "Audit failure", description = "updated", permissionCodes = update.permissionCodes, expectedVersion = 5 })).StatusCode);
                Assert.Equal("New reader", await db.OrganizationRoles.Where(x => x.Id == roleId).Select(x => x.Name).SingleAsync());
                Assert.Equal(HttpStatusCode.InternalServerError, (await failingClient.PostAsJsonAsync($"{assignmentRoute}/revoke", new { expectedMembershipVersion = 8 })).StatusCode);
                Assert.Null(await db.OrganizationRoleAssignments.Where(x => x.MembershipId == targetMembership.Id && x.RoleId == roleId).Select(x => x.RevokedAt).SingleAsync());
                Assert.Equal(8, await db.OrganizationMemberships.Where(x => x.Id == targetMembership.Id).Select(x => x.Version).SingleAsync());
            }

            await db.OrganizationRolePermissions.Where(x => x.RoleId == seed.OwnerRole.Id && x.PermissionCode == OrganizationCapabilityCatalog.MembersRead).ExecuteDeleteAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(route, new { name = "No escalation", permissionCodes = payload.permissionCodes })).StatusCode);
            Assert.Equal(5, await db.OrganizationRoles.CountAsync(x => x.OrganizationId == organization.Id && x.Origin == "custom"));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.IdentityAuditEvents.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationRolePermissions.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == user.Id || x.Id == targetUser.Id || x.Id == delegateUser.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RealReadPipelineEnforcesScopeStatusesAndPermissionChangesBetweenPages()
    {
        string connection = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var owner = User.Create($"read-owner-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        var ordinary = User.Create($"read-member-{Guid.NewGuid():N}@example.test", "Member", status: "active");
        var removed = User.Create($"read-removed-{Guid.NewGuid():N}@example.test", "Removed", status: "active");
        var suspended = User.Create($"read-suspended-{Guid.NewGuid():N}@example.test", "Suspended", status: "active");
        var foreign = User.Create($"read-foreign-{Guid.NewGuid():N}@example.test", "Foreign", status: "active");
        var admin = User.Create($"read-admin-{Guid.NewGuid():N}@example.test", "Admin", status: "active");
        var delegateUser = User.Create($"read-delegate-{Guid.NewGuid():N}@example.test", "Delegate", status: "active");
        var first = Organization.Create("Same", owner.Id, now);
        var second = Organization.Create("Same", foreign.Id, now);
        var seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(first.Id, now);
        var foreignSeed = OrganizationCapabilityCatalog.CreateBuiltInRoles(second.Id, now);
        var ownerMembership = OrganizationMembership.Create(first.Id, owner.Id, owner.Id, now);
        var ordinaryMembership = OrganizationMembership.Create(first.Id, ordinary.Id, owner.Id, now);
        var removedMembership = OrganizationMembership.Create(first.Id, removed.Id, owner.Id, now);
        var suspendedMembership = OrganizationMembership.Create(first.Id, suspended.Id, owner.Id, now);
        var foreignMembership = OrganizationMembership.Create(second.Id, foreign.Id, foreign.Id, now);
        var adminMembership = OrganizationMembership.Create(first.Id, admin.Id, owner.Id, now);
        var delegateMembership = OrganizationMembership.Create(first.Id, delegateUser.Id, owner.Id, now);
        var delegateRole = OrganizationRole.Create(first.Id, "reader", "Reader", "custom", null, owner.Id, now);
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        await using var db = new IdentityDbContext(options);
        try
        {
            db.Users.AddRange(owner, ordinary, removed, suspended, foreign, admin, delegateUser);
            db.Organizations.AddRange(first, second);
            db.OrganizationRoles.AddRange(seed.Roles.Concat(foreignSeed.Roles));
            db.OrganizationRoles.Add(delegateRole);
            db.OrganizationRolePermissions.AddRange(seed.Permissions.Concat(foreignSeed.Permissions));
            db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(first.Id, delegateRole.Id, OrganizationCapabilityCatalog.MembersRead));
            db.OrganizationMemberships.AddRange(ownerMembership, ordinaryMembership, removedMembership, suspendedMembership, foreignMembership, adminMembership, delegateMembership);
            db.OrganizationRoleAssignments.AddRange(
                OrganizationRoleAssignment.Create(first.Id, ownerMembership.Id, seed.OwnerRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(first.Id, ordinaryMembership.Id, seed.MemberRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(first.Id, removedMembership.Id, seed.AdminRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(first.Id, suspendedMembership.Id, seed.AdminRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(first.Id, adminMembership.Id, seed.AdminRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(first.Id, delegateMembership.Id, delegateRole.Id, owner.Id, now),
                OrganizationRoleAssignment.Create(second.Id, foreignMembership.Id, foreignSeed.OwnerRole.Id, foreign.Id, now));
            await db.SaveChangesAsync();
            await db.OrganizationMemberships.Where(x => x.Id == removedMembership.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "removed"));
            await db.OrganizationMemberships.Where(x => x.Id == suspendedMembership.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "suspended"));

            await using var app = await StartAppAsync(connection);
            using var client = app.GetTestClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/organizations")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-User", owner.Id.ToString());
            using var list = await client.GetAsync($"/api/v1/organizations/{first.Id}/members?limit=1");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            string cursor = listJson.RootElement.GetProperty("data").GetProperty("nextCursor").GetString()!;
            Assert.False(string.IsNullOrEmpty(cursor));
            string firstMember = listJson.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("membershipId").GetString()!;
            using var secondPage = await client.GetAsync($"/api/v1/organizations/{first.Id}/members?limit=1&cursor={Uri.EscapeDataString(cursor)}");
            Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
            using var secondPageJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());
            Assert.NotEqual(firstMember, secondPageJson.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("membershipId").GetString());
            Assert.NotEqual(JsonValueKind.Null, secondPageJson.RootElement.GetProperty("data").GetProperty("nextCursor").ValueKind);
            foreach (string path in new[] { "?limit=0", "?limit=101", "?cursor=invalid", "?status=all", $"?cursor={new string('a', 1025)}" })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members{path}")).StatusCode);
            }

            var memberIds = new HashSet<Guid> { Guid.Parse(firstMember), secondPageJson.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("membershipId").GetGuid() };
            string? remainingCursor = secondPageJson.RootElement.GetProperty("data").GetProperty("nextCursor").GetString();
            while (remainingCursor is not null)
            {
                using var page = await client.GetAsync($"/api/v1/organizations/{first.Id}/members?limit=1&cursor={Uri.EscapeDataString(remainingCursor)}");
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                using var pageJson = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
                JsonElement pageData = pageJson.RootElement.GetProperty("data");
                Assert.True(memberIds.Add(pageData.GetProperty("items")[0].GetProperty("membershipId").GetGuid()));
                remainingCursor = pageData.GetProperty("nextCursor").GetString();
            }

            Assert.True(memberIds.SetEquals([ownerMembership.Id, ordinaryMembership.Id, adminMembership.Id, delegateMembership.Id]));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{second.Id}/members")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members/{foreignMembership.Id}")).StatusCode);
            foreach (string status in new[] { "removed", "suspended" })
            {
                using var response = await client.GetAsync($"/api/v1/organizations/{first.Id}/members?status={status}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                JsonElement record = json.RootElement.GetProperty("data").GetProperty("items")[0];
                Assert.Empty(record.GetProperty("roles").EnumerateArray());
                Assert.Equal(status, record.GetProperty("status").GetString());
            }

            await db.OrganizationRoleAssignments.Where(x => x.MembershipId == removedMembership.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.RevokedAt, now));
            await db.OrganizationMemberships.Where(x => x.Id == removedMembership.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "active"));
            using (var rejoined = await client.GetAsync($"/api/v1/organizations/{first.Id}/members/{removedMembership.Id}"))
            {
                Assert.Equal(HttpStatusCode.OK, rejoined.StatusCode);
                using var rejoinedJson = JsonDocument.Parse(await rejoined.Content.ReadAsStringAsync());
                JsonElement member = rejoinedJson.RootElement.GetProperty("data");
                Assert.Equal(removedMembership.CreatedAt.ToUnixTimeSeconds(), member.GetProperty("joinedAt").GetDateTimeOffset().ToUnixTimeSeconds());
                Assert.Empty(member.GetProperty("roles").EnumerateArray());
            }

            foreach (Guid permittedUser in new[] { admin.Id, delegateUser.Id })
            {
                client.DefaultRequestHeaders.Remove("X-Test-User");
                client.DefaultRequestHeaders.Add("X-Test-User", permittedUser.ToString());
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members")).StatusCode);
            }

            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", owner.Id.ToString());

            await db.OrganizationRolePermissions.Where(x => x.OrganizationId == first.Id && x.RoleId == seed.OwnerRole.Id && x.PermissionCode == OrganizationCapabilityCatalog.MembersRead).ExecuteDeleteAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members?limit=1&cursor={Uri.EscapeDataString(cursor)}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members/me")).StatusCode);

            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", ordinary.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Forged-Claims", first.Id.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members/{ownerMembership.Id}")).StatusCode);
            await db.OrganizationMemberships.Where(x => x.Id == ordinaryMembership.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "removed"));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/organizations/{first.Id}/members/me")).StatusCode);
        }
        finally
        {
            db.ChangeTracker.Clear();
            Guid[] organizations = [first.Id, second.Id];
            await db.OrganizationRoleAssignments.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRolePermissions.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRoles.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationMemberships.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.Organizations.Where(x => organizations.Contains(x.Id)).ExecuteDeleteAsync();
            Guid[] users = [owner.Id, ordinary.Id, removed.Id, suspended.Id, foreign.Id, admin.Id, delegateUser.Id];
            await db.Users.Where(x => users.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task WorkspacePagesRecheckMembershipAndRolesWithTimestampTies()
    {
        string connection = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"workspace-reader-{Guid.NewGuid():N}@example.test", "Reader", status: "active");
        var emptyUser = User.Create($"workspace-empty-{Guid.NewGuid():N}@example.test", "Empty", status: "active");
        var organizations = Enumerable.Range(0, 3).Select(_ => Organization.Create("Same", user.Id, now)).OrderBy(x => x.Id).ToArray();
        var memberships = organizations.Select(x => OrganizationMembership.Create(x.Id, user.Id, user.Id, now)).ToArray();
        var seeds = organizations.Select(x => OrganizationCapabilityCatalog.CreateBuiltInRoles(x.Id, now)).ToArray();
        var assignments = organizations.Select((organization, i) => OrganizationRoleAssignment.Create(organization.Id, memberships[i].Id, seeds[i].MemberRole.Id, user.Id, now)).ToArray();
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        await using var db = new IdentityDbContext(options);
        try
        {
            db.Users.AddRange(user, emptyUser);
            db.Organizations.AddRange(organizations);
            db.OrganizationMemberships.AddRange(memberships);
            db.OrganizationRoles.AddRange(seeds.SelectMany(x => x.Roles));
            db.OrganizationRolePermissions.AddRange(seeds.SelectMany(x => x.Permissions));
            db.OrganizationRoleAssignments.AddRange(assignments);
            await db.SaveChangesAsync();

            await using var app = await StartAppAsync(connection);
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-User", emptyUser.Id.ToString());
            using var empty = await client.GetAsync("/api/v1/me/organizations");
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
            using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
            Assert.Empty(emptyJson.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
            client.DefaultRequestHeaders.Remove("X-Test-User");
            client.DefaultRequestHeaders.Add("X-Test-User", user.Id.ToString());
            using var first = await client.GetAsync("/api/v1/me/organizations?limit=1");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.True(first.Headers.CacheControl?.NoStore);
            using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            JsonElement firstData = firstJson.RootElement.GetProperty("data");
            Assert.Equal(organizations[0].Id, firstData.GetProperty("items")[0].GetProperty("organizationId").GetGuid());
            Assert.Single(firstData.GetProperty("items")[0].GetProperty("roles").EnumerateArray());
            string cursor = firstData.GetProperty("nextCursor").GetString()!;
            await db.OrganizationMemberships.Where(x => x.Id == memberships[1].Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "removed"));
            await db.OrganizationRoleAssignments.Where(x => x.Id == assignments[2].Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.RevokedAt, now));
            using var last = await client.GetAsync($"/api/v1/me/organizations?limit=1&cursor={Uri.EscapeDataString(cursor)}");
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
            using var lastJson = JsonDocument.Parse(await last.Content.ReadAsStringAsync());
            JsonElement lastData = lastJson.RootElement.GetProperty("data");
            Assert.Equal(organizations[2].Id, lastData.GetProperty("items")[0].GetProperty("organizationId").GetGuid());
            Assert.Empty(lastData.GetProperty("items")[0].GetProperty("roles").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, lastData.GetProperty("nextCursor").ValueKind);
            foreach (string query in new[] { "?limit=0", "?limit=101", "?cursor=invalid", $"?cursor={new string('a', 1025)}" })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/me/organizations{query}")).StatusCode);
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
            Guid[] ids = organizations.Select(x => x.Id).ToArray();
            await db.OrganizationRoleAssignments.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRolePermissions.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRoles.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationMemberships.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.Organizations.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == user.Id || x.Id == emptyUser.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RealReadPipelineFailsClosedWhenDatabaseUnavailable()
    {
        await using var app = await StartAppAsync("Host=127.0.0.1;Port=1;Database=unavailable;Username=postgres;Password=invalid;Timeout=1");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        using var response = await client.GetAsync("/api/v1/me/organizations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Organization.AuthorityUnavailable", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private static async Task<WebApplication> StartAppAsync(string connection, bool failAudit = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ApplicationDb"] = connection });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        if (failAudit)
        {
            builder.Services.RemoveAll<IIdentityAuditWriter>();
            builder.Services.AddScoped<IIdentityAuditWriter, FailingAuditWriter>();
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(OrganizationMembersController).Assembly);
        WebApplication app = builder.Build();
        app.UseGlobalExceptionHandling();
        app.UseApiStatusCodeEnvelope();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out Guid userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], Scheme.Name);
            if (Request.Headers.TryGetValue("X-Test-Forged-Claims", out var forged))
            {
                identity.AddClaim(new Claim(CustomClaims.TenantIds, forged.ToString()));
                identity.AddClaim(new Claim(CustomClaims.Permission, OrganizationCapabilityCatalog.MembersRead));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class FailingAuditWriter : IIdentityAuditWriter
    {
        public Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Test audit failure");

        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode)
        {
        }
    }
}
