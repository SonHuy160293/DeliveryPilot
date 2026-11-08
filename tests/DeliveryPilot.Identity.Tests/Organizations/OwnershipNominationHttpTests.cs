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

public sealed class OwnershipNominationHttpTests
{
    [PostgreSqlFact]
    public async Task NominateAcceptAndSeparateStepDownPreserveRolesAndAuditAttribution()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        Guid ownerMembership = (await MemberAsync(db, fixture, fixture.Owner)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        JsonElement created = await DataAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = target.Id }), HttpStatusCode.Created);
        Guid id = created.GetProperty("id").GetGuid();
        Assert.Equal(9, created.EnumerateObject().Count());
        Assert.Equal(fixture.Clock.Now.AddDays(7), created.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.False(created.GetProperty("canAccept").GetBoolean());
        Assert.Equal(1, await db.OrganizationRoleAssignments.CountAsync(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.Seed.OwnerRole.Id && item.RevokedAt == null));
        Assert.Equal(1, await db.OrganizationMemberships.Where(item => item.Id == target.Id).Select(item => item.Version).SingleAsync());
        JsonElement personal = await DataAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
        Assert.True(personal.GetProperty("items")[0].GetProperty("canAccept").GetBoolean());
        await ErrorAsync(await nominee.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);
        await ErrorAsync(await nominee.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members"), HttpStatusCode.Forbidden);
        JsonElement accepted = await DataAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.OK);
        Assert.Equal("accepted", accepted.GetProperty("status").GetString());
        Assert.Equal(2, accepted.GetProperty("version").GetInt64());
        Assert.False(accepted.GetProperty("canAccept").GetBoolean());
        OrganizationRoleAssignment grant = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == target.Id && item.RoleId == fixture.Seed.OwnerRole.Id);
        Assert.Equal(fixture.Owner.Id, grant.GrantedByUserId);
        Assert.Equal(2, await db.OrganizationRoleAssignments.CountAsync(item => item.MembershipId == target.Id && item.RevokedAt == null));
        List<IdentityAuditEvent> events = await db.IdentityAuditEvents.Where(item => item.MembershipId == target.Id && item.MembershipVersion == 2).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, entry => Assert.Equal(fixture.Ordinary.Id, entry.ActorUserId));
        Assert.Single(events.Select(entry => entry.CorrelationId).Distinct());
        Assert.Equal(Enumerable.Range(1, 2), events.Select(entry => entry.EventOrdinal).Order());
        int audits = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        await DataAsync(await RespondAsync(nominee, fixture, id, "accept", 999), HttpStatusCode.OK);
        Assert.Equal(audits, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        Assert.Equal(2, await db.OrganizationMemberships.Where(item => item.Id == target.Id).Select(item => item.Version).SingleAsync());
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{ownerMembership}/demote-owner", new { expectedMembershipVersion = 1, reason = "Explicit step down after acceptance" }), HttpStatusCode.OK);
        Assert.NotNull((await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == ownerMembership && item.RoleId == fixture.Seed.OwnerRole.Id)).RevokedAt);
        Assert.Null((await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == ownerMembership && item.RoleId == fixture.Seed.MemberRole.Id)).RevokedAt);

        // Sponsor loss after acceptance does not change a currently authorized replay.
        await DataAsync(await RespondAsync(nominee, fixture, id, "accept", 1), HttpStatusCode.OK);
        Assert.Equal(1, await db.OrganizationRoleAssignments.CountAsync(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.Seed.OwnerRole.Id && item.RevokedAt == null));
        string auditJson = string.Join(" ", await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id).Select(item => item.Details).ToListAsync());
        Assert.DoesNotContain(fixture.Ordinary.PrimaryEmail, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("proof", created.GetRawText() + accepted.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task StableOwnerRegrantAndLaterDemotionOrRemovalCannotBeRestoredByReplay()
    {
        foreach (string later in new[] { "demote-owner", "remove", "suspend" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
            var old = OrganizationRoleAssignment.Create(fixture.Organization.Id, target, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now);
            old.Revoke(fixture.Owner.Id, fixture.Clock.Now, "Historical Owner grant ended");
            db.OrganizationRoleAssignments.Add(old);
            await db.SaveChangesAsync();
            Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
            using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            await DataAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.OK);
            OrganizationRoleAssignment fresh = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.Id == old.Id);
            Assert.Equal(2, fresh.GrantVersion);
            Assert.Equal(3, fresh.Version);
            Assert.Null(fresh.RevokedAt);
            if (later == "suspend")
            {
                await db.OrganizationMemberships.Where(item => item.Id == target).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            }
            else
            {
                await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/{later}", new { expectedMembershipVersion = 2, reason = "Subsequent authorized reduction" }), HttpStatusCode.OK);
            }

            long version = await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync();
            int audits = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
            Assert.Equal("Organization.AccessDenied", await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Forbidden));
            Assert.Equal(version, await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync());
            Assert.Equal(audits, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        }
    }

    [PostgreSqlFact]
    public async Task CurrentSponsorLossBlocksAcceptanceButNotNomineeDeclineOrReplay()
    {
        foreach (string loss in new[] { "role", "membership", "account" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid sponsor = (await MemberAsync(db, fixture, fixture.Owner)).Id;
            Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
            if (loss == "role")
            {
                await db.OrganizationRoleAssignments.Where(item => item.MembershipId == sponsor && item.RoleId == fixture.Seed.OwnerRole.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
            }
            else if (loss == "membership")
            {
                await db.OrganizationMemberships.Where(item => item.Id == sponsor).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "removed"));
            }
            else
            {
                await db.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            }

            using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
            JsonElement page = await DataAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
            Assert.False(page.GetProperty("items")[0].GetProperty("canAccept").GetBoolean());
            Assert.Equal("OwnershipNomination.SponsorUnavailable", await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Conflict));
            await DataAsync(await RespondAsync(nominee, fixture, id, "decline"), HttpStatusCode.OK);
            int audits = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
            await DataAsync(await RespondAsync(nominee, fixture, id, "decline", 999), HttpStatusCode.OK);
            Assert.Equal(audits, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
            Assert.Equal("OwnershipNomination.Closed", await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Conflict));
        }
    }

    [PostgreSqlFact]
    public async Task AnotherCurrentOwnerMayCancelButOnlyOriginalClosingSubjectMayReplay()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid another = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
        db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, another, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
        await db.SaveChangesAsync();
        Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
        using HttpClient other = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient sponsor = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        JsonElement cancelled = await DataAsync(await other.PostAsJsonAsync($"{Route(fixture)}/{id}/cancel", new { expectedVersion = 1, reason = "Nomination no longer required" }), HttpStatusCode.OK);
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        await DataAsync(await RespondAsync(other, fixture, id, "cancel", 999), HttpStatusCode.OK);
        Assert.Equal("OwnershipNomination.Closed", await ErrorAsync(await RespondAsync(sponsor, fixture, id, "cancel"), HttpStatusCode.Conflict));
        await ErrorAsync(await RespondAsync(nominee, fixture, id, "cancel"), HttpStatusCode.Forbidden);
        Assert.Equal("OwnershipNomination.Closed", await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Conflict));
        OrganizationOwnershipNomination row = await db.OrganizationOwnershipNominations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(fixture.Delegate.Id, row.ClosedByUserId);
        Assert.Equal("Nomination no longer required", row.Reason);
        Assert.Equal(1, await db.OrganizationMemberships.Where(item => item.UserId == fixture.Ordinary.Id && item.OrganizationId == fixture.Organization.Id).Select(item => item.Version).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ExpiryBoundaryAndExpiredReplacementAreAtomicAndHistoryIsRetained()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(7).AddTicks(-10);
        JsonElement page = await DataAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
        Assert.True(page.GetProperty("items")[0].GetProperty("canAccept").GetBoolean());
        fixture.Clock.Now = fixture.Clock.Now.AddTicks(10);
        foreach (string action in new[] { "accept", "decline" })
        {
            Assert.Equal("OwnershipNomination.Closed", await ErrorAsync(await RespondAsync(nominee, fixture, id, action), HttpStatusCode.Conflict));
        }

        await using WebApplication failing = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit())));
        using HttpClient failingOwner = Fixture.Client(failing, fixture.Owner.Id);
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        await ErrorAsync(await failingOwner.PostAsJsonAsync(Route(fixture), new { membershipId = target }), HttpStatusCode.InternalServerError);
        Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        Assert.Equal(1, await db.OrganizationOwnershipNominations.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        Guid replacement = await NominateAsync(fixture, app, fixture.Ordinary);
        Assert.NotEqual(id, replacement);
        OrganizationOwnershipNomination old = await db.OrganizationOwnershipNominations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal("expired", old.Status);
        Assert.Null(old.ClosedByUserId);
        Assert.Equal(2, old.Version);
        Assert.Equal(3, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        OrganizationOwnershipNomination current = await db.OrganizationOwnershipNominations.AsNoTracking().SingleAsync(item => item.Id == replacement);
        Assert.Equal(fixture.Clock.Now.AddDays(7), current.ExpiresAt);
        fixture.Clock.Now = current.ExpiresAt.AddTicks(-10);
        await DataAsync(await RespondAsync(nominee, fixture, replacement, "accept"), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task DuplicateAndAlreadyOwnerNominationAndTerminalRacesHaveNoRepeatedEffects()
    {
        foreach (string competitor in new[] { "accept", "decline", "cancel" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
            HttpResponseMessage[] offers = await Task.WhenAll(owner.PostAsJsonAsync(Route(fixture), new { membershipId = target }), owner.PostAsJsonAsync(Route(fixture), new { membershipId = target }));
            Guid id;
            try
            {
                Assert.Single(offers, response => response.StatusCode == HttpStatusCode.Created);
                id = (await DataAsync(offers.Single(response => response.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created)).GetProperty("id").GetGuid();
                Assert.Equal("OwnershipNomination.PendingExists", await ErrorAsync(offers.Single(response => response.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict));
            }
            finally
            {
                foreach (HttpResponseMessage response in offers)
                {
                    response.Dispose();
                }
            }

            HttpResponseMessage[] responses = await Task.WhenAll(RespondAsync(nominee, fixture, id, "accept"), RespondAsync(competitor == "cancel" ? owner : nominee, fixture, id, competitor));
            try
            {
                Assert.Equal(competitor == "accept" ? 2 : 1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
                Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
                OrganizationOwnershipNomination row = await db.OrganizationOwnershipNominations.AsNoTracking().SingleAsync(item => item.Id == id);
                Assert.Equal(2, row.Version);
                Assert.Equal(row.Status == "accepted" ? 1 : 0, await db.OrganizationRoleAssignments.CountAsync(item => item.MembershipId == target && item.RoleId == fixture.Seed.OwnerRole.Id && item.RevokedAt == null));
                if (row.Status == "accepted")
                {
                    Assert.Equal("OwnershipNomination.AlreadyOwner", await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = target }), HttpStatusCode.Conflict));
                }
            }
            finally
            {
                foreach (HttpResponseMessage response in responses)
                {
                    response.Dispose();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task ValidationDisclosureAndKestrelLimitsApplyToEveryOperation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient wrong = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient foreign = Fixture.Client(app, fixture.Foreign.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        await ErrorAsync(await anonymous.PostAsJsonAsync(Route(fixture), new { membershipId = target }), HttpStatusCode.Unauthorized);
        foreach (HttpClient actor in new[] { nominee, wrong, foreign })
        {
            await ErrorAsync(await actor.PostAsJsonAsync(Route(fixture), new { membershipId = target }), HttpStatusCode.Forbidden);
            await ErrorAsync(await actor.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);
        }

        await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = Guid.Empty }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = target, sponsorId = fixture.Owner.Id }), HttpStatusCode.BadRequest);
        foreach (User ineligible in new[] { fixture.Removed, fixture.Suspended })
        {
            Assert.Equal("OwnershipNomination.RecipientUnavailable", await ErrorAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = (await MemberAsync(db, fixture, ineligible)).Id }), HttpStatusCode.Conflict));
        }

        Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
        foreach (string action in new[] { "accept", "decline", "cancel" })
        {
            HttpClient actor = action == "cancel" ? owner : nominee;
            await ErrorAsync(await RespondAsync(anonymous, fixture, id, action), HttpStatusCode.Unauthorized);
            foreach (HttpClient invalid in new[] { wrong, foreign })
            {
                Assert.Equal("Organization.AccessDenied", await ErrorAsync(await RespondAsync(invalid, fixture, id, action), HttpStatusCode.Forbidden));
            }

            await ErrorAsync(await RespondAsync(actor, fixture, Guid.CreateVersion7(), action), HttpStatusCode.Forbidden);
            foreach (long version in new long[] { 0, -1 })
            {
                await ErrorAsync(await RespondAsync(actor, fixture, id, action, version), HttpStatusCode.BadRequest);
            }

            Assert.Equal("OwnershipNomination.Stale", await ErrorAsync(await RespondAsync(actor, fixture, id, action, 2), HttpStatusCode.Conflict));
            await ErrorAsync(await actor.PostAsJsonAsync($"{Route(fixture)}/{id}/{action}", new { expectedVersion = 1, roleId = fixture.Seed.OwnerRole.Id }), HttpStatusCode.BadRequest);
            using var malformed = new StringContent("{\"expectedVersion\":\"bad\"}", Encoding.UTF8, "application/json");
            await ErrorAsync(await actor.PostAsync($"{Route(fixture)}/{id}/{action}", malformed), HttpStatusCode.BadRequest);
        }

        foreach (string reason in new[] { "  ", new string('a', 1001), "Contact private@example.test", "https://example.test/private" })
        {
            await ErrorAsync(await owner.PostAsJsonAsync($"{Route(fixture)}/{id}/cancel", new { expectedVersion = 1, reason }), HttpStatusCode.BadRequest);
        }

        foreach (string query in new[] { "?status=bad", "?status=", "?limit=0", "?limit=101", "?cursor=", "?cursor=bad", "?cursor=" + new string('a', 1025) })
        {
            await ErrorAsync(await owner.GetAsync(Route(fixture) + query), HttpStatusCode.BadRequest);
            await ErrorAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations" + query), HttpStatusCode.BadRequest);
        }

        await using WebApplication kestrel = await fixture.StartAppAsync(useKestrel: true);
        using var network = new HttpClient { BaseAddress = new Uri(kestrel.Urls.Single()) };
        network.DefaultRequestHeaders.Add("X-Test-User", fixture.Owner.Id.ToString());
        foreach (string suffix in new[] { string.Empty, $"/{id}/accept", $"/{id}/decline", $"/{id}/cancel" })
        {
            Assert.Equal("Request.TooLarge", await ErrorAsync(await network.PostAsJsonAsync(Route(fixture) + suffix, new { membershipId = target, expectedVersion = 1, reason = new string('a', 17000) }), HttpStatusCode.RequestEntityTooLarge));
        }

        Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ListsUseScopedKeysetsReauthorizeEachPageAndFilterPersonalEligibility()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        Guid first = await NominateAsync(fixture, app, fixture.Ordinary);
        Guid second = await NominateAsync(fixture, app, fixture.Delegate);
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient ordinary = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient delegated = Fixture.Client(app, fixture.Delegate.Id);
        JsonElement page = await DataAsync(await owner.GetAsync(Route(fixture) + "?limit=1"), HttpStatusCode.OK);
        Assert.Equal(first, page.GetProperty("items")[0].GetProperty("id").GetGuid());
        string cursor = page.GetProperty("nextCursor").GetString()!;
        JsonElement next = await DataAsync(await owner.GetAsync(Route(fixture) + "?limit=1&cursor=" + Uri.EscapeDataString(cursor)), HttpStatusCode.OK);
        Assert.Equal(second, next.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextCursor").ValueKind);
        JsonElement personal = await DataAsync(await ordinary.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
        Assert.Single(personal.GetProperty("items").EnumerateArray());
        Assert.Equal(first, personal.GetProperty("items")[0].GetProperty("id").GetGuid());
        JsonElement own = await DataAsync(await delegated.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
        Assert.Equal(second, own.GetProperty("items")[0].GetProperty("id").GetGuid());
        await DataAsync(await RespondAsync(ordinary, fixture, first, "decline"), HttpStatusCode.OK);
        JsonElement history = await DataAsync(await ordinary.GetAsync("/api/v1/me/ownership-nominations?status=declined"), HttpStatusCode.OK);
        Assert.Equal(first, history.GetProperty("items")[0].GetProperty("id").GetGuid());
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        await db.OrganizationMemberships.Where(item => item.Id == target).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "removed"));
        JsonElement hidden = await DataAsync(await ordinary.GetAsync("/api/v1/me/ownership-nominations?status=declined"), HttpStatusCode.OK);
        Assert.Empty(hidden.GetProperty("items").EnumerateArray());
        await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "removed"));
        await ErrorAsync(await owner.GetAsync(Route(fixture) + "?limit=1&cursor=" + Uri.EscapeDataString(cursor)), HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task RemovedSuspendedAndForeignNomineesCannotRespondOrSeeRestrictedNominations()
    {
        foreach (string state in new[] { "removed", "suspended", "account", "workspace" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
            if (state == "account")
            {
                await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            }
            else if (state == "workspace")
            {
                await db.Organizations.Where(item => item.Id == fixture.Organization.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
            }
            else
            {
                await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, state));
            }

            using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
            foreach (string action in new[] { "accept", "decline" })
            {
                await ErrorAsync(await RespondAsync(nominee, fixture, id, action), HttpStatusCode.Forbidden);
            }

            Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        }
    }

    [PostgreSqlFact]
    public async Task RequiredAuditAndCommitFailuresRollbackEveryMutation()
    {
        foreach (string action in new[] { "nominate", "accept", "decline", "cancel" })
        {
            foreach (bool commit in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using WebApplication issuer = await fixture.StartAppAsync();
                await using var db = fixture.NewDb();
                Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
                Guid id = action == "nominate" ? Guid.Empty : await NominateAsync(fixture, issuer, fixture.Ordinary);
                int audits = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
                await using WebApplication failing = await fixture.StartAppAsync(configure: builder =>
                {
                    if (commit)
                    {
                        builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit()));
                    }
                    else
                    {
                        builder.Services.AddScoped<IdentityAuditWriter>();
                        builder.Services.RemoveAll<IIdentityAuditWriter>();
                        builder.Services.AddScoped<IIdentityAuditWriter, FailingNominationAudit>();
                    }
                });
                using HttpClient actor = Fixture.Client(failing, action is "nominate" or "cancel" ? fixture.Owner.Id : fixture.Ordinary.Id);
                await ErrorAsync(action == "nominate" ? await actor.PostAsJsonAsync(Route(fixture), new { membershipId = target }) : await RespondAsync(actor, fixture, id, action), HttpStatusCode.InternalServerError);
                Assert.Equal(audits, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
                Assert.Equal(1, await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync());
                Assert.False(await db.OrganizationRoleAssignments.AnyAsync(item => item.MembershipId == target && item.RoleId == fixture.Seed.OwnerRole.Id));
                Assert.Equal(action == "nominate" ? 0 : 1, await db.OrganizationOwnershipNominations.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
                if (id != Guid.Empty)
                {
                    Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task AcceptanceRacingSponsorDemotionAndNomineeRemovalCannotRestoreRevokedAccess()
    {
        foreach (string reduction in new[] { "sponsor", "nominee" })
        {
            foreach (bool reduceFirst in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using var db = fixture.NewDb();
                Guid sponsor = (await MemberAsync(db, fixture, fixture.Owner)).Id;
                Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
                Guid another = (await MemberAsync(db, fixture, fixture.Delegate)).Id;
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, another, fixture.Seed.OwnerRole.Id, fixture.Owner.Id, fixture.Clock.Now));
                await db.SaveChangesAsync();
                await using WebApplication normal = await fixture.StartAppAsync();
                Guid id = await NominateAsync(fixture, normal, fixture.Ordinary);
                var gate = new CommitGate();
                await using WebApplication gated = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
                using HttpClient nominee = Fixture.Client(reduceFirst ? normal : gated, fixture.Ordinary.Id);
                using HttpClient reducer = Fixture.Client(reduceFirst ? gated : normal, fixture.Delegate.Id);
                Task<HttpResponseMessage> ReduceAsync() => reducer.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{(reduction == "sponsor" ? sponsor : target)}/{(reduction == "sponsor" ? "demote-owner" : "remove")}", new { expectedMembershipVersion = !reduceFirst && reduction == "nominee" ? 2 : 1, reason = "Independent protected reduction" });
                try
                {
                    Task<HttpResponseMessage> first = reduceFirst ? ReduceAsync() : RespondAsync(nominee, fixture, id, "accept");
                    await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Task<HttpResponseMessage> second = reduceFirst ? RespondAsync(nominee, fixture, id, "accept") : ReduceAsync();
                    await AssertBlockedAsync(db, gate.Pid, true);
                    gate.Release.TrySetResult();
                    await DataAsync(await first, HttpStatusCode.OK);
                    if (reduceFirst)
                    {
                        await ErrorAsync(await second, reduction == "sponsor" ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden);
                    }
                    else
                    {
                        await DataAsync(await second, HttpStatusCode.OK);
                    }

                    Assert.Equal(reduceFirst ? "pending" : "accepted", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
                    if (reduction == "nominee")
                    {
                        Assert.Equal("removed", await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Status).SingleAsync());
                    }
                }
                finally
                {
                    gate.Release.TrySetResult();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task AccountAndSessionChangesSerializeThroughAcceptanceCommitInBothOrders()
    {
        foreach (string authority in new[] { "sponsor", "nominee", "session" })
        {
            foreach (bool changeFirst in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using var db = fixture.NewDb();
                var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Ordinary.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
                db.AuthSessions.Add(session);
                await db.SaveChangesAsync();
                await using WebApplication issuer = await fixture.StartAppAsync();
                Guid id = await NominateAsync(fixture, issuer, fixture.Ordinary);
                var gate = new CommitGate();
                await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
                using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
                nominee.DefaultRequestHeaders.Add("X-Test-Session", session.Id.ToString());
                await using var changer = fixture.NewDb();
                await using var transaction = await changer.Database.BeginTransactionAsync();
                int pid = ((NpgsqlConnection)changer.Database.GetDbConnection()).ProcessID;
                Task<int> ChangeAsync() => authority == "session"
                    ? changer.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now))
                    : changer.Users.Where(item => item.Id == (authority == "sponsor" ? fixture.Owner.Id : fixture.Ordinary.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
                try
                {
                    if (changeFirst)
                    {
                        await ChangeAsync();
                        Task<HttpResponseMessage> pending = RespondAsync(nominee, fixture, id, "accept");
                        await AssertBlockedAsync(db, pid, true);
                        await transaction.CommitAsync();
                        await ErrorAsync(await pending, authority == "sponsor" ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden);
                    }
                    else
                    {
                        Task<HttpResponseMessage> pending = RespondAsync(nominee, fixture, id, "accept");
                        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Task<int> change = ChangeAsync();
                        await AssertBlockedAsync(db, pid, false);
                        Assert.False(change.IsCompleted);
                        gate.Release.TrySetResult();
                        await DataAsync(await pending, HttpStatusCode.OK);
                        await change;
                        await transaction.CommitAsync();
                    }

                    Assert.Equal(changeFirst ? "pending" : "accepted", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
                }
                finally
                {
                    gate.Release.TrySetResult();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task SponsorGrantOrNominationExpiryDuringLockWaitIsRechecked()
    {
        foreach (bool grantExpiry in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            await using var db = fixture.NewDb();
            Guid id = await NominateAsync(fixture, app, fixture.Ordinary);
            if (grantExpiry)
            {
                Guid sponsor = (await MemberAsync(db, fixture, fixture.Owner)).Id;
                await db.OrganizationRoleAssignments.Where(item => item.MembershipId == sponsor && item.RoleId == fixture.Seed.OwnerRole.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.ExpiresAt, fixture.Clock.Now.AddSeconds(1)));
            }
            else
            {
                fixture.Clock.Now = fixture.Clock.Now.AddDays(7).AddSeconds(-1);
            }

            await using var blocker = fixture.NewDb();
            await using var transaction = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync($"""SELECT * FROM shared."Users" WHERE "Id" = {fixture.Ordinary.Id} FOR UPDATE""");
            int pid = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
            using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
            Task<HttpResponseMessage> pending = RespondAsync(nominee, fixture, id, "accept");
            await AssertBlockedAsync(db, pid, true);
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
            await transaction.CommitAsync();
            Assert.Equal(grantExpiry ? "OwnershipNomination.SponsorUnavailable" : "OwnershipNomination.Closed", await ErrorAsync(await pending, HttpStatusCode.Conflict));
            Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        }
    }

    [PostgreSqlFact]
    public async Task SnapshotListsAreReadOnlyAndAcceptanceRechecksSponsorChanges()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuer = await fixture.StartAppAsync();
        Guid id = await NominateAsync(fixture, issuer, fixture.Ordinary);
        var probe = new SnapshotProbe();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(probe)));
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        Task<HttpResponseMessage> pending = nominee.GetAsync("/api/v1/me/ownership-nominations");
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var db = fixture.NewDb();
            await db.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        }
        finally
        {
            probe.Release.TrySetResult();
        }

        JsonElement page = await DataAsync(await pending, HttpStatusCode.OK);
        Assert.True(page.GetProperty("items")[0].GetProperty("canAccept").GetBoolean());
        Assert.Equal("OwnershipNomination.SponsorUnavailable", await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Conflict));
    }

    [PostgreSqlFact]
    public async Task RealBearerRequiresCurrentAccountAndOwnedUnrevokedSession()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuerApp = await fixture.StartAppAsync();
        Guid id = await NominateAsync(fixture, issuerApp, fixture.Ordinary);
        await using var db = fixture.NewDb();
        var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Ordinary.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        var foreign = AuthSession.Create(Guid.CreateVersion7(), fixture.Foreign.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.AuthSessions.AddRange(session, foreign);
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "nomination-tests-only-signing-key-at-least-32-bytes",
                ["Jwt:Issuer"] = "nomination-tests",
                ["Jwt:Audience"] = "nomination-tests",
                ["Jwt:AccessTokenExpirationMinutes"] = "10",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
            builder.Services.AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment);
        });
        JwtAccessTokenIssuer tokens = app.Services.GetRequiredService<JwtAccessTokenIssuer>();
        using HttpClient nominee = app.GetTestClient();
        string token = tokens.Issue(fixture.Ordinary.Id, session.Id, fixture.Clock.Now).Token;
        nominee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.OK);
        nominee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Issue(fixture.Ordinary.Id, foreign.Id, fixture.Clock.Now).Token);
        await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Unauthorized);
        nominee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.OK);
        await db.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
        await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.Unauthorized);
        await db.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, (DateTimeOffset?)null));
        await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        await ErrorAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(token, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task StoreOutageReturnsSafeUnavailableWithoutMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuer = await fixture.StartAppAsync();
        Guid id = await NominateAsync(fixture, issuer, fixture.Ordinary);
        await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new UnavailableStore())));
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient nominee = Fixture.Client(app, fixture.Ordinary.Id);
        await ErrorAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.ServiceUnavailable);
        await ErrorAsync(await nominee.GetAsync("/api/v1/me/ownership-nominations"), HttpStatusCode.ServiceUnavailable);
        await ErrorAsync(await RespondAsync(nominee, fixture, id, "accept"), HttpStatusCode.ServiceUnavailable);
        await using var db = fixture.NewDb();
        Assert.Equal("pending", await db.OrganizationOwnershipNominations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
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

        Assert.Fail("The nomination transition and authority change did not serialize on PostgreSQL locks.");
    }

    private static string Route(Fixture fixture) => $"/api/v1/organizations/{fixture.Organization.Id}/ownership-nominations";
    private static Task<OrganizationMembership> MemberAsync(IdentityDbContext db, Fixture fixture, User user)
        => db.OrganizationMemberships.SingleAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == user.Id);
    private static Task<HttpResponseMessage> RespondAsync(HttpClient client, Fixture fixture, Guid id, string action, long version = 1)
        => client.PostAsJsonAsync($"{Route(fixture)}/{id}/{action}", new { expectedVersion = version });
    private static async Task<Guid> NominateAsync(Fixture fixture, WebApplication app, User nominee)
    {
        await using var db = fixture.NewDb();
        Guid membership = (await MemberAsync(db, fixture, nominee)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        return (await DataAsync(await owner.PostAsJsonAsync(Route(fixture), new { membershipId = membership }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }

    private sealed class FailingCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Injected nomination commit failure.");
    }

    private sealed class FailingNominationAudit(IdentityAuditWriter inner) : IIdentityAuditWriter
    {
        public async Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
        {
            IdentityAuditEvent result = await inner.StageAsync(entry, cancellationToken);
            if (entry.AggregateType == IdentityAuditSchemaCatalog.OrganizationOwnershipNominationAggregate)
            {
                throw new InvalidOperationException("Injected failure after staging nomination audit.");
            }

            return result;
        }

        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode) { }
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

    private sealed class SnapshotProbe : DbCommandInterceptor
    {
        private int _observed;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM shared.\"OrganizationOwnershipNominations\"", StringComparison.Ordinal) && Interlocked.Exchange(ref _observed, 1) == 0)
            {
                Assert.Equal(System.Data.IsolationLevel.RepeatableRead, command.Transaction!.IsolationLevel);
                Assert.Empty(eventData.Context!.ChangeTracker.Entries());
                await using DbCommand check = command.Connection!.CreateCommand();
                check.Transaction = command.Transaction;
                check.CommandText = "SHOW transaction_read_only";
                Assert.Equal("on", await check.ExecuteScalarAsync(cancellationToken));
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }

    private sealed class UnavailableStore : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Injected nomination store outage.");
    }
}
