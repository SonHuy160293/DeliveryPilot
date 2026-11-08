using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Security;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using static DeliveryPilot.Identity.Tests.Organizations.InvitationManagementHttpTests;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class MembershipChangeHttpTests
{
    [PostgreSqlFact]
    public async Task RemovalClosesAllGrantsAndPreservesHistoryIdentitySessionsAndOtherWorkspaces()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        OrganizationMembership membership = await MemberAsync(db, fixture, fixture.Ordinary);
        DateTimeOffset joinedAt = membership.CreatedAt;
        var other = OrganizationMembership.Create(fixture.ForeignOrganization.Id, fixture.Ordinary.Id, fixture.Foreign.Id, fixture.Clock.Now);
        var otherGrant = OrganizationRoleAssignment.Create(fixture.ForeignOrganization.Id, other.Id, fixture.ForeignRole.Id, fixture.Foreign.Id, fixture.Clock.Now);
        var grant = OrganizationRoleAssignment.Create(fixture.Organization.Id, membership.Id, fixture.Role.Id, fixture.Owner.Id, fixture.Clock.Now);
        var expired = OrganizationRoleAssignment.Create(fixture.Organization.Id, membership.Id, fixture.DelegateRole.Id, fixture.Owner.Id, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(1));
        var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Ordinary.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.OrganizationMemberships.Add(other);
        db.OrganizationRoleAssignments.AddRange(otherGrant, grant, expired);
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient departing = Fixture.Client(app, fixture.Ordinary.Id);
        await DataAsync(await departing.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/me"), HttpStatusCode.OK);
        JsonElement removed = await DataAsync(await PostAsync(owner, fixture, membership.Id, "remove"), HttpStatusCode.OK);
        Assert.Equal(5, removed.EnumerateObject().Count());
        Assert.Equal("removed", removed.GetProperty("status").GetString());
        Assert.Equal(2, removed.GetProperty("version").GetInt64());
        Assert.Empty(removed.GetProperty("roles").EnumerateArray());
        Assert.True(removed.GetProperty("changed").GetBoolean());
        OrganizationMembership after = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == membership.Id);
        Assert.Equal(joinedAt, after.CreatedAt);
        Assert.Equal(fixture.Owner.Id, after.StatusChangedByUserId);
        Assert.All(await db.OrganizationRoleAssignments.AsNoTracking().Where(item => item.MembershipId == membership.Id).ToListAsync(), assignment =>
        {
            Assert.Equal(fixture.Clock.Now, assignment.RevokedAt);
            Assert.Equal(fixture.Owner.Id, assignment.RevokedByUserId);
            Assert.Equal(1, assignment.GrantVersion);
            Assert.Equal(2, assignment.Version);
        });
        Assert.Null((await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.Id == otherGrant.Id)).RevokedAt);
        Assert.Equal("active", (await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == other.Id)).Status);
        Assert.Equal("active", (await db.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Ordinary.Id)).Status);
        Assert.Null((await db.AuthSessions.AsNoTracking().SingleAsync(item => item.Id == session.Id)).RevokedAt);
        List<IdentityAuditEvent> events = await db.IdentityAuditEvents.Where(item => item.MembershipId == membership.Id).ToListAsync();
        Assert.Equal(4, events.Count);
        Assert.All(events, entry =>
        {
            Assert.Equal(fixture.Owner.Id, entry.ActorUserId);
            Assert.Equal(2, entry.MembershipVersion);
            Assert.Equal("Contract completed", entry.Reason);
        });
        Assert.Single(events.Select(entry => entry.CorrelationId).Distinct());
        Assert.Equal(Enumerable.Range(1, 4), events.Select(entry => entry.EventOrdinal).Order());
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await departing.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/me"), HttpStatusCode.Forbidden));
        await DataAsync(await departing.GetAsync($"/api/v1/organizations/{fixture.ForeignOrganization.Id}/members/me"), HttpStatusCode.OK);
        JsonElement replay = await DataAsync(await PostAsync(owner, fixture, membership.Id, "remove", 999), HttpStatusCode.OK);
        Assert.False(replay.GetProperty("changed").GetBoolean());
        Assert.Equal(2, replay.GetProperty("version").GetInt64());
        Assert.Equal(4, await db.IdentityAuditEvents.CountAsync(item => item.MembershipId == membership.Id));
    }

    [PostgreSqlFact]
    public async Task ProtectedTargetsAndDelegationCeilingCannotBeBypassedIncludingInactiveAccounts()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        OrganizationMembership delegated = await MemberAsync(db, fixture, fixture.Delegate);
        db.OrganizationRolePermissions.AddRange(
            OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove),
            OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.RolesAssign));
        var admin = OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now);
        db.OrganizationRoleAssignments.Add(admin);
        await db.SaveChangesAsync();
        using HttpClient manager = Fixture.Client(app, fixture.Delegate.Id);
        foreach (string accountStatus in new[] { "active", "suspended" })
        {
            await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, accountStatus));
            Assert.Equal("Organization.AccessDenied", await ErrorAsync(await PostAsync(manager, fixture, target.Id, "remove"), HttpStatusCode.Forbidden));
            Assert.Equal("Organization.AccessDenied", await ErrorAsync(await manager.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target.Id}/roles/{fixture.Seed.AdminRole.Id}/revoke", new { expectedMembershipVersion = 1, reason = "Attempted reduction" }), HttpStatusCode.Forbidden));
            Assert.Equal("Organization.AccessDenied", await ErrorAsync(await PostAsync(manager, fixture, target.Id, "demote-owner"), HttpStatusCode.Forbidden));
        }

        await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "active"));
        await db.OrganizationRoleAssignments.Where(item => item.Id == admin.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));

        // Ordinary targets still cannot exceed a removal delegate's effective permissions.
        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.Role.Id, OrganizationCapabilityCatalog.AuditRead));
        db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Role.Id, fixture.Owner.Id, fixture.Clock.Now));
        await db.SaveChangesAsync();
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await PostAsync(manager, fixture, target.Id, "remove"), HttpStatusCode.Forbidden));
        await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.Role.Id && item.PermissionCode == OrganizationCapabilityCatalog.AuditRead).ExecuteDeleteAsync();
        await DataAsync(await PostAsync(manager, fixture, target.Id, "remove"), HttpStatusCode.OK);
        await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.MembersRead).ExecuteDeleteAsync();

        // A successful action did not give the delegate general member inspection/replay rights.
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await PostAsync(manager, fixture, target.Id, "remove"), HttpStatusCode.Forbidden));
        Assert.Equal(1, (await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == delegated.Id)).Version);
    }

    [PostgreSqlFact]
    public async Task AdminAndActionOnlyDelegateMayRemoveOrdinaryMembersButReplayRequiresRead()
    {
        foreach (bool admin in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid actor = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
            Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
            if (admin)
            {
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, actor, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            }
            else
            {
                db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
                await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.MembersRead).ExecuteDeleteAsync();
            }

            await db.SaveChangesAsync();
            using HttpClient manager = Fixture.Client(app, fixture.Delegate.Id);
            await DataAsync(await PostAsync(manager, fixture, target, "remove"), HttpStatusCode.OK);
            if (admin)
            {
                JsonElement replay = await DataAsync(await PostAsync(manager, fixture, target, "remove"), HttpStatusCode.OK);
                Assert.False(replay.GetProperty("changed").GetBoolean());
            }
            else
            {
                await ErrorAsync(await PostAsync(manager, fixture, target, "remove"), HttpStatusCode.Forbidden);
            }

            Guid owner = (await MemberAsync(db, fixture, fixture.Owner)).Id;
            await ErrorAsync(await PostAsync(manager, fixture, owner, "demote-owner"), HttpStatusCode.Forbidden);
        }
    }

    [PostgreSqlFact]
    public async Task OwnerCanRemoveAdminAndInactiveOwnerProtectionDoesNotConferActorAuthority()
    {
        foreach (bool ownerTarget in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
            db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, ownerTarget ? fixture.Seed.OwnerRole.Id : fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, (await MemberAsync(db, fixture, fixture.Delegate)).Id, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            await db.SaveChangesAsync();
            await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            using HttpClient admin = Fixture.Client(app, fixture.Delegate.Id);
            await ErrorAsync(await PostAsync(admin, fixture, target.Id, "remove"), HttpStatusCode.Forbidden);
            using HttpClient inactive = Fixture.Client(app, fixture.Ordinary.Id);
            await ErrorAsync(await PostAsync(inactive, fixture, (await MemberAsync(db, fixture, fixture.Delegate)).Id, "remove"), HttpStatusCode.Forbidden);
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            await DataAsync(await PostAsync(owner, fixture, target.Id, "remove"), HttpStatusCode.OK);
            Assert.Equal("removed", await db.OrganizationMemberships.Where(item => item.Id == target.Id).Select(item => item.Status).SingleAsync());
            Assert.All(await db.OrganizationRoleAssignments.AsNoTracking().Where(item => item.MembershipId == target.Id).ToListAsync(), assignment => Assert.NotNull(assignment.RevokedAt));
        }
    }

    [PostgreSqlFact]
    public async Task DemotionRetainsOtherRolesOrCreatesAndRegrantsMemberInOneRevision()
    {
        foreach (string remaining in new[] { "none", "revoked-member", "expired-member", "custom", "admin" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
            OrganizationRoleAssignment member = await db.OrganizationRoleAssignments.SingleAsync(item => item.MembershipId == target.Id);
            member.Revoke(fixture.Owner.Id, fixture.Clock.Now, "Previous period ended");
            if (remaining == "none")
            {
                db.OrganizationRoleAssignments.Remove(member);
            }
            else if (remaining == "expired-member")
            {
                member.Regrant(fixture.Owner.Id, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(1));
            }
            else if (remaining is "custom" or "admin")
            {
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, remaining == "custom" ? fixture.Role.Id : fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            }

            db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            await db.SaveChangesAsync();
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
            Assert.Equal(0, await db.AuthSessions.CountAsync(item => item.UserId == fixture.Owner.Id || item.UserId == fixture.Ordinary.Id));
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            JsonElement changed = await DataAsync(await PostAsync(owner, fixture, target.Id, "demote-owner"), HttpStatusCode.OK);
            Assert.Equal("active", changed.GetProperty("status").GetString());
            Assert.Equal(2, changed.GetProperty("version").GetInt64());
            Assert.Single(changed.GetProperty("roles").EnumerateArray());
            Guid expectedRole = remaining == "custom" ? fixture.Role.Id : remaining == "admin" ? fixture.Seed.AdminRole.Id : fixture.Seed.MemberRole.Id;
            Assert.Equal(expectedRole, changed.GetProperty("roles")[0].GetProperty("id").GetGuid());
            Assert.NotNull((await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == target.Id && item.RoleId == fixture.Seed.OwnerRole.Id)).RevokedAt);
            Assert.Equal(target.CreatedAt, (await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == target.Id)).CreatedAt);
            List<IdentityAuditEvent> events = await db.IdentityAuditEvents.Where(item => item.MembershipId == target.Id).ToListAsync();
            Assert.Equal(remaining is "custom" or "admin" ? 1 : 2, events.Count);
            Assert.All(events, entry => Assert.Equal(2, entry.MembershipVersion));
            if (remaining is "revoked-member" or "expired-member")
            {
                OrganizationRoleAssignment regranted = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.Id == member.Id);
                Assert.Equal(remaining == "expired-member" ? 3 : 2, regranted.GrantVersion);
                Assert.Null(regranted.RevokedAt);
            }

            Assert.Equal("Organization.MembershipInvalidTransition", await ErrorAsync(await PostAsync(owner, fixture, target.Id, "demote-owner", 2), HttpStatusCode.Conflict));
        }
    }

    [PostgreSqlFact]
    public async Task LastOwnerAndNoEligibleOwnerCannotMutateWhileOrdinaryReadsRemainAuthorized()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid ownerId = (await MemberAsync(db, fixture, fixture.Owner)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            Assert.Equal("Organization.OwnerContinuityRequired", await ErrorAsync(await PostAsync(owner, fixture, ownerId, action), HttpStatusCode.Conflict));
        }

        Guid adminId = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
        db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, adminId, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
        await db.SaveChangesAsync();
        await db.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        using HttpClient admin = Fixture.Client(app, fixture.Delegate.Id);
        Assert.Equal("Organization.OwnerContinuityRequired", await ErrorAsync(await PostAsync(admin, fixture, (await MemberAsync(db, fixture, fixture.Ordinary)).Id, "remove"), HttpStatusCode.Conflict));
        await DataAsync(await admin.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members"), HttpStatusCode.OK);
        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task TwoOwnersCanStepDownOrRemoveConcurrentlyButCannotOrphanTheWorkspace()
    {
        foreach (string first in new[] { "remove", "demote-owner" })
        {
            foreach (string second in new[] { "remove", "demote-owner" })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using WebApplication app = await fixture.StartAppAsync();
                await using var db = fixture.NewDb();
                OrganizationMembership one = await MemberAsync(db, fixture, fixture.Owner);
                OrganizationMembership two = await MemberAsync(db, fixture, fixture.Ordinary);
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, two.Id, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
                await db.SaveChangesAsync();
                using HttpClient a = Fixture.Client(app, fixture.Owner.Id);
                using HttpClient b = Fixture.Client(app, fixture.Ordinary.Id);
                HttpResponseMessage[] results = await Task.WhenAll(PostAsync(a, fixture, one.Id, first), PostAsync(b, fixture, two.Id, second));
                try
                {
                    Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
                    Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
                    Assert.Equal(1, await db.OrganizationRoleAssignments.CountAsync(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.Seed.OwnerRole.Id && item.RevokedAt == null));
                }
                finally
                {
                    foreach (HttpResponseMessage response in results)
                    {
                        response.Dispose();
                    }
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task ValidationPrivacyAndKestrelBodyLimitsApplyToBothActions()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient delegateClient = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        using HttpClient foreign = Fixture.Client(app, fixture.Foreign.Id);
        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
        await db.SaveChangesAsync();
        await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.MembersRead).ExecuteDeleteAsync();
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            await ErrorAsync(await PostAsync(anonymous, fixture, target, action), HttpStatusCode.Unauthorized);
            await ErrorAsync(await PostAsync(foreign, fixture, target, action), HttpStatusCode.Forbidden);
            Assert.Equal("Membership.NotFound", await ErrorAsync(await PostAsync(owner, fixture, Guid.CreateVersion7(), action), HttpStatusCode.NotFound));
            await ErrorAsync(await PostAsync(delegateClient, fixture, Guid.CreateVersion7(), action), HttpStatusCode.Forbidden);
            foreach (long version in new long[] { 0, -1 })
            {
                await ErrorAsync(await PostAsync(owner, fixture, target, action, version), HttpStatusCode.BadRequest);
            }

            foreach (string? reason in new string?[] { null, string.Empty, "  ", new string('a', 1001), "Person at private@example.test" })
            {
                await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture, target, action), new { expectedMembershipVersion = 1, reason }), HttpStatusCode.BadRequest);
            }

            await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture, target, action), new { expectedMembershipVersion = 1, reason = "Reason", roleId = fixture.Role.Id }), HttpStatusCode.BadRequest);
            using var malformed = new StringContent("{\"expectedMembershipVersion\":\"x\",\"reason\":\"Reason\"}", Encoding.UTF8, "application/json");
            await ErrorAsync(await owner.PostAsync(Route(fixture, target, action), malformed), HttpStatusCode.BadRequest);
        }

        Assert.Equal("Organization.MembershipStale", await ErrorAsync(await PostAsync(owner, fixture, target, "remove", 2), HttpStatusCode.Conflict));
        Assert.Equal("Organization.MembershipInvalidTransition", await ErrorAsync(await PostAsync(owner, fixture, (await MemberAsync(db, fixture, fixture.Suspended)).Id, "remove"), HttpStatusCode.Conflict));
        await using WebApplication kestrel = await fixture.StartAppAsync(useKestrel: true);
        using var network = new HttpClient { BaseAddress = new Uri(kestrel.Urls.Single()) };
        network.DefaultRequestHeaders.Add("X-Test-User", fixture.Owner.Id.ToString());
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            Assert.Equal("Request.TooLarge", await ErrorAsync(await network.PostAsJsonAsync(Route(fixture, target, action), new { expectedMembershipVersion = 1, reason = new string('a', 17000) }), HttpStatusCode.RequestEntityTooLarge));
        }

        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task AuditAndCommitFailureRollbackRemovalDemotionAndFallbackTogether()
    {
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            foreach (bool commit in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using var db = fixture.NewDb();
                OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
                OrganizationRoleAssignment member = await db.OrganizationRoleAssignments.SingleAsync(item => item.MembershipId == target.Id);
                if (action == "demote-owner")
                {
                    member.Revoke(fixture.Owner.Id, fixture.Clock.Now, "Historical Member grant ended");
                    db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
                }
                else
                {
                    db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Role.Id, fixture.Owner.Id, fixture.Clock.Now));
                }

                await db.SaveChangesAsync();
                long memberVersion = member.Version;
                DateTimeOffset? revokedAt = member.RevokedAt;
                await using WebApplication app = await fixture.StartAppAsync(configure: builder =>
                {
                    if (commit)
                    {
                        builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit()));
                    }
                    else
                    {
                        builder.Services.AddScoped<IdentityAuditWriter>();
                        builder.Services.RemoveAll<IIdentityAuditWriter>();
                        builder.Services.AddScoped<IIdentityAuditWriter, FailSecondAudit>();
                    }
                });
                using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
                await ErrorAsync(await PostAsync(owner, fixture, target.Id, action), HttpStatusCode.InternalServerError);
                OrganizationMembership after = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == target.Id);
                Assert.Equal("active", after.Status);
                Assert.Equal(1, after.Version);
                OrganizationRoleAssignment grantAfter = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.Id == member.Id);
                Assert.Equal(memberVersion, grantAfter.Version);
                Assert.Equal(revokedAt, grantAfter.RevokedAt);
                Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
                if (action == "demote-owner")
                {
                    Assert.Null((await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == target.Id && item.RoleId == fixture.Seed.OwnerRole.Id)).RevokedAt);
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task SelfRemovalDoesNotAuthorizeReplayOrRevokeGlobalBearerSession()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using var db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Delegate);
        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
        var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Delegate.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync(configure: ConfigureBearer);
        using HttpClient client = app.GetTestClient();
        string token = app.Services.GetRequiredService<JwtAccessTokenIssuer>().Issue(fixture.Delegate.Id, session.Id, fixture.Clock.Now).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await PostAsync(client, fixture, target.Id, "remove"), HttpStatusCode.OK);
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await PostAsync(client, fixture, target.Id, "remove"), HttpStatusCode.Forbidden));
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await client.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/me"), HttpStatusCode.Forbidden));
        Assert.Null((await db.AuthSessions.AsNoTracking().SingleAsync(item => item.Id == session.Id)).RevokedAt);
        Assert.Equal("active", (await db.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Delegate.Id)).Status);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(token, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task RealBearerRejectsUnownedRevokedAndInactiveActorSessionsBeforeBothActions()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using var db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        var ownerSession = AuthSession.Create(Guid.CreateVersion7(), fixture.Owner.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        var otherSession = AuthSession.Create(Guid.CreateVersion7(), fixture.Ordinary.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.AuthSessions.AddRange(ownerSession, otherSession);
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync(configure: ConfigureBearer);
        JwtAccessTokenIssuer issuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();
        using HttpClient client = app.GetTestClient();
        foreach (string invalid in new[] { "unowned", "revoked", "inactive" })
        {
            if (invalid == "revoked")
            {
                await db.AuthSessions.Where(item => item.Id == ownerSession.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
            }
            else if (invalid == "inactive")
            {
                await db.AuthSessions.Where(item => item.Id == ownerSession.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, (DateTimeOffset?)null));
                await db.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(fixture.Owner.Id, invalid == "unowned" ? otherSession.Id : ownerSession.Id, fixture.Clock.Now).Token);
            foreach (string action in new[] { "remove", "demote-owner" })
            {
                await ErrorAsync(await PostAsync(client, fixture, target, action), HttpStatusCode.Unauthorized);
            }
        }

        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task AccountAndSessionEligibilityAreLockedThroughCommitInBothOrders()
    {
        foreach (string authority in new[] { "actor", "session", "target", "owner" })
        {
            foreach (bool changeFirst in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using var db = fixture.NewDb();
                OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
                db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
                var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Delegate.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
                db.AuthSessions.Add(session);
                if (authority == "target")
                {
                    db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target.Id, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
                }

                await db.SaveChangesAsync();
                var gate = new CommitGate();
                await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
                using HttpClient actor = Fixture.Client(app, authority == "target" ? fixture.Owner.Id : fixture.Delegate.Id);
                if (authority != "target")
                {
                    actor.DefaultRequestHeaders.Add("X-Test-Session", session.Id.ToString());
                }

                await using var changer = fixture.NewDb();
                await using var transaction = await changer.Database.BeginTransactionAsync();
                int pid = ((NpgsqlConnection)changer.Database.GetDbConnection()).ProcessID;
                Guid changedUser = authority == "actor" ? fixture.Delegate.Id : authority == "target" ? fixture.Ordinary.Id : fixture.Owner.Id;
                Task<int> ChangeAsync() => authority == "session"
                    ? changer.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now))
                    : changer.Users.Where(item => item.Id == changedUser).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
                try
                {
                    Task<HttpResponseMessage> pending;
                    if (changeFirst)
                    {
                        await ChangeAsync();
                        pending = PostAsync(actor, fixture, target.Id, "remove");
                        await AssertBlockedAsync(db, pid, true);
                        await transaction.CommitAsync();
                        if (authority == "target")
                        {
                            gate.Release.TrySetResult();
                            await DataAsync(await pending, HttpStatusCode.OK);
                        }
                        else
                        {
                            Assert.Equal(
                                authority == "owner" ? "Organization.OwnerContinuityRequired" : "Organization.AccessDenied",
                                await ErrorAsync(await pending, authority == "owner" ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden));
                        }
                    }
                    else
                    {
                        pending = PostAsync(actor, fixture, target.Id, "remove");
                        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Task<int> change = ChangeAsync();
                        await AssertBlockedAsync(db, pid, false);
                        Assert.False(change.IsCompleted);
                        gate.Release.TrySetResult();
                        await DataAsync(await pending, HttpStatusCode.OK);
                        await change;
                        await transaction.CommitAsync();
                    }

                    Assert.Equal(
                        changeFirst && authority != "target" ? "active" : "removed",
                        await db.OrganizationMemberships.Where(item => item.Id == target.Id).Select(item => item.Status).SingleAsync());
                }
                finally
                {
                    gate.Release.TrySetResult();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task GrantExpiryWhileWaitingForLocksCannotAuthorizeRemovalOrOrphanAnOwner()
    {
        foreach (bool ownerExpiry in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using var db = fixture.NewDb();
            OrganizationMembership target = await MemberAsync(db, fixture, ownerExpiry ? fixture.Owner : fixture.Ordinary);
            Guid lockedUser;
            if (ownerExpiry)
            {
                Guid secondOwner = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, secondOwner, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(1)));
                lockedUser = fixture.Ordinary.Id;
            }
            else
            {
                db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
                Guid delegated = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
                await db.OrganizationRoleAssignments.Where(item => item.MembershipId == delegated).ExecuteUpdateAsync(update => update.SetProperty(item => item.ExpiresAt, fixture.Clock.Now.AddSeconds(1)));
                lockedUser = fixture.Delegate.Id;
            }

            await db.SaveChangesAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            using HttpClient actor = Fixture.Client(app, ownerExpiry ? fixture.Owner.Id : fixture.Delegate.Id);
            await using var blocker = fixture.NewDb();
            await using var transaction = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync($"""SELECT * FROM shared."Users" WHERE "Id" = {lockedUser} FOR UPDATE""");
            int pid = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
            Task<HttpResponseMessage> pending = PostAsync(actor, fixture, target.Id, ownerExpiry ? "demote-owner" : "remove");
            await AssertBlockedAsync(db, pid, true);
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
            await transaction.CommitAsync();
            Assert.Equal(
                ownerExpiry ? "Organization.OwnerContinuityRequired" : "Organization.AccessDenied",
                await ErrorAsync(await pending, ownerExpiry ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden));
            Assert.Equal(1, await db.OrganizationMemberships.Where(item => item.Id == target.Id).Select(item => item.Version).SingleAsync());
            Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
        }
    }

    [PostgreSqlFact]
    public async Task RoleRevocationAndProtectedMutationSerializeInBothOrders()
    {
        foreach (bool revokeFirst in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using var db = fixture.NewDb();
            Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
            Guid delegated = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
            db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.MembersRemove));
            await db.SaveChangesAsync();
            var gate = new CommitGate();
            await using WebApplication normal = await fixture.StartAppAsync();
            await using WebApplication gated = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
            using HttpClient remover = Fixture.Client(revokeFirst ? normal : gated, fixture.Delegate.Id);
            using HttpClient revoker = Fixture.Client(revokeFirst ? gated : normal, fixture.Owner.Id);
            Task<HttpResponseMessage> RevokeAsync() => revoker.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{delegated}/roles/{fixture.DelegateRole.Id}/revoke", new { expectedMembershipVersion = 1, reason = "Authority ended" });
            try
            {
                Task<HttpResponseMessage> first = revokeFirst ? RevokeAsync() : PostAsync(remover, fixture, target, "remove");
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Task<HttpResponseMessage> second = revokeFirst ? PostAsync(remover, fixture, target, "remove") : RevokeAsync();
                await AssertBlockedAsync(db, gate.Pid, true);
                gate.Release.TrySetResult();
                await DataAsync(await first, HttpStatusCode.OK);
                if (revokeFirst)
                {
                    Assert.Equal("Organization.AccessDenied", await ErrorAsync(await second, HttpStatusCode.Forbidden));
                }
                else
                {
                    await DataAsync(await second, HttpStatusCode.OK);
                }

                Assert.Equal(revokeFirst ? "active" : "removed", await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Status).SingleAsync());
            }
            finally
            {
                gate.Release.TrySetResult();
            }
        }
    }

    [PostgreSqlFact]
    public async Task ConcurrentRoleEditAndRemoveOrDemoteRejectStaleDifferingIntent()
    {
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
            db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, target, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
            await db.SaveChangesAsync();
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            HttpResponseMessage[] results = await Task.WhenAll(
                PostAsync(owner, fixture, target, action),
                owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/roles/{fixture.Seed.MemberRole.Id}/revoke", new { expectedMembershipVersion = 1, reason = "Independent role reduction" }));
            try
            {
                Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
                Assert.Single(results, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden);
                Assert.Equal(2, await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync());
            }
            finally
            {
                foreach (HttpResponseMessage response in results)
                {
                    response.Dispose();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task AdmittedReadMayFinishButQueuedWorkAndNewDecisionsReauthorizeAfterRemoval()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        var authorization = new OrganizationAuthorizationService(db, new Principal(fixture.Ordinary.Id), fixture.Clock);
        OrganizationMemberSecurityContext admitted = await authorization.AuthorizeMemberScopeAsync(fixture.Organization.Id);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Guid> FinishAdmittedReadAsync()
        {
            await release.Task;
            return admitted.MembershipId;
        }

        Task<Guid> read = FinishAdmittedReadAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        await DataAsync(await PostAsync(owner, fixture, target, "remove"), HttpStatusCode.OK);
        release.SetResult();
        Assert.Equal(target, await read);
        await Assert.ThrowsAsync<ForbiddenException>(() => authorization.AuthorizeMemberScopeAsync(fixture.Organization.Id));
        int effects = 0;
        async Task RunQueuedWorkAsync()
        {
            await authorization.ReauthorizeJobActorAsync(fixture.Organization.Id, fixture.Ordinary.Id);
            effects++;
        }

        await Assert.ThrowsAsync<ForbiddenException>(RunQueuedWorkAsync);
        Assert.Equal(0, effects);
    }

    [PostgreSqlFact]
    public async Task MissingRequiredFactsFailClosedWithoutMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using var db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new UnavailableStore())));
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        foreach (string action in new[] { "remove", "demote-owner" })
        {
            Assert.Equal("Organization.AuthorityUnavailable", await ErrorAsync(await PostAsync(owner, fixture, target, action), HttpStatusCode.ServiceUnavailable));
        }

        Assert.Equal(1, await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync());
        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    private static void ConfigureBearer(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "membership-tests-only-signing-key-at-least-32-bytes",
            ["Jwt:Issuer"] = "membership-tests",
            ["Jwt:Audience"] = "membership-tests",
            ["Jwt:AccessTokenExpirationMinutes"] = "10",
            ["Jwt:RefreshTokenExpirationDays"] = "7"
        });
        builder.Services.AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    }

    private static async Task AssertBlockedAsync(IdentityDbContext observer, int pid, bool blocker)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            bool blocked = blocker
                ? await observer.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT FROM pg_stat_activity WHERE {pid} = ANY(pg_blocking_pids(pid))) AS \"Value\"").SingleAsync()
                : await observer.Database.SqlQuery<bool>($"SELECT cardinality(pg_blocking_pids({pid})) > 0 AS \"Value\"").SingleAsync();
            if (blocked)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The membership mutation and authority change did not serialize on a PostgreSQL lock.");
    }

    private static string Route(Fixture fixture, Guid membershipId, string action) => $"/api/v1/organizations/{fixture.Organization.Id}/members/{membershipId}/{action}";
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Fixture fixture, Guid membershipId, string action, long version = 1)
        => client.PostAsJsonAsync(Route(fixture, membershipId, action), new { expectedMembershipVersion = version, reason = "Contract completed" });
    private static Task<OrganizationMembership> MemberAsync(IdentityDbContext db, Fixture fixture, User user)
        => db.OrganizationMemberships.SingleAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == user.Id);

    private sealed class FailSecondAudit(IdentityAuditWriter inner) : IIdentityAuditWriter
    {
        private int _count;
        public async Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
        {
            IdentityAuditEvent result = await inner.StageAsync(entry, cancellationToken);
            if (++_count == 2)
            {
                throw new InvalidOperationException("Injected failure after staging the second audit event.");
            }

            return result;
        }

        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode) { }
    }

    private sealed class FailingCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Injected transaction commit failure.");
    }

    private sealed class CommitGate : DbTransactionInterceptor
    {
        public int Pid { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Pid = ((NpgsqlConnection)transaction.Connection!).ProcessID;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private sealed class Principal(Guid id) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => new(id.ToString());
    }

    private sealed class UnavailableStore : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Injected authority store outage.");
    }
}
