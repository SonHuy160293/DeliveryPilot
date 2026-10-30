using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;
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
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;
using Xunit.Abstractions;
using static DeliveryPilot.Identity.Tests.Organizations.InvitationManagementHttpTests;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationAuditHttpTests(ITestOutputHelper output)
{
    [PostgreSqlFact]
    public async Task ReadersNeedCurrentAuditGrantAndEveryPageReauthorizesWithoutCrossScopeLeakage()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 4);
        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.AuditRead));
        Guid adminId = (await MemberAsync(db, fixture, fixture.Removed)).Id;
        await db.OrganizationMemberships.Where(item => item.Id == adminId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "active"));
        db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, adminId, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now));
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient admin = Fixture.Client(app, fixture.Removed.Id);
        using HttpClient delegated = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient ordinary = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient foreign = Fixture.Client(app, fixture.Foreign.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        await DataAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.OK);
        await DataAsync(await admin.GetAsync(Route(fixture)), HttpStatusCode.OK);
        JsonElement first = await DataAsync(await delegated.GetAsync(Route(fixture) + "?limit=1"), HttpStatusCode.OK);
        string cursor = first.GetProperty("nextCursor").GetString()!;
        await ErrorAsync(await anonymous.GetAsync(Route(fixture)), HttpStatusCode.Unauthorized);
        await ErrorAsync(await ordinary.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);
        await ErrorAsync(await foreign.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);
        await ErrorAsync(await owner.GetAsync($"/api/v1/organizations/{fixture.ForeignOrganization.Id}/audit-events"), HttpStatusCode.Forbidden);
        await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.AuditRead).ExecuteDeleteAsync();
        Assert.Equal("Organization.AccessDenied", await ErrorAsync(await delegated.GetAsync(Route(fixture) + "?cursor=" + Uri.EscapeDataString(cursor)), HttpStatusCode.Forbidden));
        await DataAsync(await delegated.GetAsync($"/api/v1/organizations/{fixture.Organization.Id}/members"), HttpStatusCode.OK);
        await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.Seed.OwnerRole.Id && item.PermissionCode == OrganizationCapabilityCatalog.AuditRead).ExecuteDeleteAsync();
        await ErrorAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);

        // System Owner status does not bypass a removed catalog-backed read grant.
        await DataAsync(await admin.GetAsync(Route(fixture)), HttpStatusCode.OK);
        await db.OrganizationMemberships.Where(item => item.Id == adminId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "removed"));
        await ErrorAsync(await admin.GetAsync(Route(fixture)), HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task StableKeysetsUseTimeOrMembershipRevisionAndFiltersComposeWithUtcBoundaries()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 17);
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        Guid[] timeIds = await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id && item.Outcome == "succeeded")
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Equal(timeIds, await ReadAllAsync(owner, Route(fixture) + "?limit=3"));
        Guid[] revisionIds = await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id && item.MembershipId == target.Id && item.Outcome == "succeeded")
            .OrderBy(item => item.MembershipVersion).ThenBy(item => item.EventOrdinal).ThenBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Equal(revisionIds, await ReadAllAsync(owner, Route(fixture) + $"?membershipId={target.Id}&limit=2"));
        Assert.NotEqual(timeIds, revisionIds);
        IdentityAuditEvent chosen = await db.IdentityAuditEvents.AsNoTracking().Where(item => item.OrganizationId == fixture.Organization.Id && item.Outcome == "succeeded").OrderBy(item => item.MembershipVersion).FirstAsync();
        string filter = $"?actorUserId={chosen.ActorUserId}&membershipId={target.Id}&targetId={chosen.AggregateId}&action={chosen.EventType}";
        DateTimeOffset from = chosen.OccurredAt;
        DateTimeOffset to = from.AddSeconds(1);
        string window = "&from=" + Uri.EscapeDataString(from.ToOffset(TimeSpan.FromHours(7)).ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
        JsonElement filtered = await DataAsync(await owner.GetAsync(Route(fixture) + filter + window), HttpStatusCode.OK);
        Guid[] expected = await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id && item.Outcome == "succeeded" && item.ActorUserId == chosen.ActorUserId &&
            item.MembershipId == target.Id && item.AggregateId == chosen.AggregateId && item.EventType == chosen.EventType && item.OccurredAt >= from && item.OccurredAt < to)
            .OrderBy(item => item.MembershipVersion).ThenBy(item => item.EventOrdinal).ThenBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Equal(expected, filtered.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("eventId").GetGuid()));
        foreach (string unmatched in new[] { "actorUserId", "membershipId", "targetId" })
        {
            JsonElement empty = await DataAsync(await owner.GetAsync(Route(fixture) + $"?{unmatched}={Guid.CreateVersion7()}"), HttpStatusCode.OK);
            Assert.Empty(empty.GetProperty("items").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, empty.GetProperty("nextCursor").ValueKind);
        }

        Assert.Equal(17, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id && item.Outcome == "succeeded"));
    }

    [PostgreSqlFact]
    public async Task InvalidFiltersAndScopeModeOrFilterChangesRejectCursorsWithoutPayloadDisclosure()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 5);
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        foreach (string invalid in new[]
        {
            "actorUserId=", "actorUserId=bad", "membershipId=", "targetId=", $"targetId={Guid.Empty}", "action=", "action=bad", "limit=0", "limit=101", "limit=x",
            "from=", "from=2026-11-05", "from=2026-11-05T00:00:00", "to=bad", "from=2026-11-06T00:00:00Z&to=2026-11-05T00:00:00Z",
            "from=2026-11-05T00:00:00Z&to=2026-11-05T00:00:00Z", "cursor=", "cursor=bad", "cursor=" + new string('a', 1025),
        })
        {
            await ErrorAsync(await owner.GetAsync(Route(fixture) + "?" + invalid), HttpStatusCode.BadRequest);
        }

        JsonElement page = await DataAsync(await owner.GetAsync(Route(fixture) + "?limit=1"), HttpStatusCode.OK);
        string cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        foreach (string changed in new[] { $"membershipId={target.Id}", $"actorUserId={fixture.Owner.Id}", $"targetId={target.Id}", $"action={IdentityAuditSchemaCatalog.MembershipStatusChanged}", "from=2026-01-01T00:00:00Z", "to=2027-01-01T00:00:00Z" })
        {
            await ErrorAsync(await owner.GetAsync(Route(fixture) + $"?cursor={cursor}&{changed}"), HttpStatusCode.BadRequest);
        }

        JsonElement memberPage = await DataAsync(await owner.GetAsync(Route(fixture) + $"?membershipId={target.Id}&limit=1"), HttpStatusCode.OK);
        await ErrorAsync(await owner.GetAsync(Route(fixture) + "?cursor=" + Uri.EscapeDataString(memberPage.GetProperty("nextCursor").GetString()!)), HttpStatusCode.BadRequest);
        AuditEventFilter otherScope = new(Guid.CreateVersion7(), null, null, null, null, null, null);
        string foreignCursor = AuditEventCursor.Encode(otherScope, new AuditPageKey(Guid.CreateVersion7(), fixture.Clock.Now));
        await ErrorAsync(await owner.GetAsync(Route(fixture) + "?cursor=" + Uri.EscapeDataString(foreignCursor)), HttpStatusCode.BadRequest);
        Assert.Equal(6, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task RealLifecycleHistoryIncludesRejoinRegrantAndNominationWhileHistoricalActorsRemainAttributable()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using IdentityDbContext db = fixture.NewDb();
        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient person = Fixture.Client(app, fixture.Ordinary.Id);
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/remove", new { expectedMembershipVersion = 1, reason = "Contract completed" }), HttpStatusCode.OK);
        JsonElement issued = await DataAsync(await owner.PostAsJsonAsync(fixture.Route, new { recipientEmail = fixture.Ordinary.PrimaryEmail, roleIds = new[] { fixture.Seed.MemberRole.Id, fixture.Role.Id } }), HttpStatusCode.Created);
        Guid invitationId = issued.GetProperty("id").GetGuid();
        Guid delivery = (await db.OrganizationInvitations.AsNoTracking().SingleAsync(item => item.Id == invitationId)).CurrentDeliveryMessageId!.Value;
        string proof = fixture.ReadProof(await db.Set<OutboxMessage>().Where(item => item.MessageId == delivery).Select(item => item.PayloadJson).SingleAsync());
        await DataAsync(await person.PostAsJsonAsync($"/api/v1/organization-invitations/{invitationId}/accept", new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        long version = await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync();
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/roles/{fixture.Role.Id}/revoke", new { expectedMembershipVersion = version, reason = "Role no longer required" }), HttpStatusCode.OK);
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/roles/{fixture.Role.Id}/grant", new { expectedMembershipVersion = version + 1, reason = "Fresh authorized period" }), HttpStatusCode.OK);
        JsonElement nominated = await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/ownership-nominations", new { membershipId = target }), HttpStatusCode.Created);
        await DataAsync(await person.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/ownership-nominations/{nominated.GetProperty("id").GetGuid()}/accept", new { expectedVersion = 1 }), HttpStatusCode.OK);
        version = await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Version).SingleAsync();
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/remove", new { expectedMembershipVersion = version, reason = "Later contract completion" }), HttpStatusCode.OK);
        await db.Users.Where(item => item.Id == fixture.Ordinary.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.DisplayName, "Former colleague").SetProperty(item => item.Status, "suspended"));
        JsonElement history = await DataAsync(await owner.GetAsync(Route(fixture) + $"?membershipId={target}"), HttpStatusCode.OK);
        JsonElement[] rows = history.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(rows, item => item.GetProperty("action").GetString() == IdentityAuditSchemaCatalog.RoleAssignmentRegranted);
        Assert.Contains(rows, item => item.GetProperty("action").GetString() == IdentityAuditSchemaCatalog.OwnershipNominationClosed);
        Assert.Contains(rows, item => item.GetProperty("changes").TryGetProperty("previousStatus", out JsonElement previous) && previous.GetString() == "removed");
        Assert.Contains(rows, item => item.GetProperty("reason").GetString() == "Contract completed");
        Assert.All(rows.Where(item => item.GetProperty("actorUserId").GetGuid() == fixture.Ordinary.Id), item => Assert.Equal("Former colleague", item.GetProperty("actorDisplayName").GetString()));
        Assert.All(rows, item => Assert.Equal(14, item.EnumerateObject().Count()));
        JsonElement all = await DataAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.OK);
        string raw = all.GetRawText();
        Assert.DoesNotContain(proof, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Ordinary.PrimaryEmail, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenVerifier", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlationId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actorReference", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal));
        Assert.Equal("removed", await db.OrganizationMemberships.Where(item => item.Id == target).Select(item => item.Status).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ProjectionRedactsLegacyPersonalTextAndFailsSafelyForCorruptOrUnsupportedDetails()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 1);
        Guid eventId = await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id && item.Outcome == "succeeded").Select(item => item.Id).SingleAsync();
        const string marker = "private@example.test";
        await db.IdentityAuditEvents.Where(item => item.Id == eventId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, marker));
        await db.Users.Where(item => item.Id == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.DisplayName, marker));
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        JsonElement safe = await DataAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, safe.GetProperty("items")[0].GetProperty("reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, safe.GetProperty("items")[0].GetProperty("actorDisplayName").ValueKind);
        Assert.DoesNotContain(marker, safe.GetRawText(), StringComparison.Ordinal);
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE shared."IdentityAuditEvents" SET "Details" = jsonb_build_object('password', {marker}) WHERE "Id" = {eventId}""");
        using HttpResponseMessage corrupt = await owner.GetAsync(Route(fixture));
        string raw = await corrupt.Content.ReadAsStringAsync();
        await ErrorAsync(corrupt, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(marker, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(marker, StringComparison.Ordinal));
        await db.IdentityAuditEvents.Where(item => item.Id == eventId).ExecuteUpdateAsync(update => update.SetProperty(item => item.DetailsSchemaVersion, (short)2));
        await ErrorAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.InternalServerError);
    }

    [PostgreSqlFact]
    public async Task SystemActorsHaveNullUserFieldsAndRolledBackSuccessEventsAreAbsent()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync())
        {
            var writer = new IdentityAuditWriter(db, fixture.Clock, NullLogger<IdentityAuditWriter>.Instance);
            await writer.StageAsync(IdentityAuditEntry.ForOrganizationCreated(fixture.Organization, IdentityAuditActor.System("bootstrap-fixture"), fixture.Clock.Now, "system-actor-fixture"));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Guid target = (await MemberAsync(db, fixture, fixture.Ordinary)).Id;
        await using WebApplication failing = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit())));
        using HttpClient failureClient = Fixture.Client(failing, fixture.Owner.Id);
        await ErrorAsync(await failureClient.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{target}/remove", new { expectedMembershipVersion = 1, reason = "Rolled back removal" }), HttpStatusCode.InternalServerError);
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        JsonElement events = await DataAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.OK);
        JsonElement entry = Assert.Single(events.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("actorUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("actorDisplayName").ValueKind);
        Assert.DoesNotContain("bootstrap-fixture", events.GetRawText(), StringComparison.Ordinal);
        JsonElement member = await DataAsync(await owner.GetAsync(Route(fixture) + $"?membershipId={target}"), HttpStatusCode.OK);
        Assert.Empty(member.GetProperty("items").EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task RealBearerSessionAndAuthorityStoreFailuresUseSafeNoStoreEnvelopes()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 2);
        AuthSession session = AuthSession.Create(Guid.CreateVersion7(), fixture.Owner.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "audit-tests-only-signing-key-at-least-32-bytes",
                ["Jwt:Issuer"] = "audit-tests", ["Jwt:Audience"] = "audit-tests",
                ["Jwt:AccessTokenExpirationMinutes"] = "10", ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
            builder.Services.AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment);
        });
        using HttpClient owner = app.GetTestClient();
        string token = app.Services.GetRequiredService<JwtAccessTokenIssuer>().Issue(fixture.Owner.Id, session.Id, fixture.Clock.Now).Token;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.OK);
        await db.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
        await ErrorAsync(await owner.GetAsync(Route(fixture)), HttpStatusCode.Unauthorized);
        await using WebApplication unavailable = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new UnavailableStore())));
        using HttpClient caller = Fixture.Client(unavailable, fixture.Owner.Id);
        Assert.Equal("Organization.AuthorityUnavailable", await ErrorAsync(await caller.GetAsync(Route(fixture)), HttpStatusCode.ServiceUnavailable));
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(token, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task PostgreSqlPlansUseExistingIndexesAndReadsHaveNoTrackingOrTransaction()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using IdentityDbContext db = fixture.NewDb();
        OrganizationMembership target = await MemberAsync(db, fixture, fixture.Ordinary);
        await SeedAsync(fixture, target, 6000);
        await db.Database.ExecuteSqlRawAsync("ANALYZE shared.\"IdentityAuditEvents\"");
        var capture = new CaptureAuditQuery();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(capture)));
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        foreach (bool membershipMode in new[] { false, true })
        {
            string query = membershipMode ? $"?membershipId={target.Id}&limit=2" : "?limit=2";
            JsonElement first = await DataAsync(await owner.GetAsync(Route(fixture) + query), HttpStatusCode.OK);
            await DataAsync(await owner.GetAsync(Route(fixture) + query + "&cursor=" + Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!)), HttpStatusCode.OK);
            CapturedCommand command = capture.Last!;
            Assert.Contains("LIMIT", command.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("OFFSET", command.Sql, StringComparison.Ordinal);
            await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
            await connection.OpenAsync();
            await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + command.Sql, connection);
            foreach (var parameter in command.Parameters)
            {
                explain.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value });
            }

            string plan = (string)(await explain.ExecuteScalarAsync())!;
            string index = membershipMode ? "UxIdentityAuditEventsMembershipRevisionOrdinal" : "IxIdentityAuditEventsOrganizationOccurredAtId";
            Assert.Contains(index, plan, StringComparison.Ordinal);
            output.WriteLine($"{(membershipMode ? "Membership" : "Time")} query uses {index}: {plan}");
            Assert.False(capture.Tracked);
            Assert.False(capture.TransactionOpened);
        }
    }

    private static string Route(Fixture fixture) => $"/api/v1/organizations/{fixture.Organization.Id}/audit-events";
    private static Task<OrganizationMembership> MemberAsync(IdentityDbContext db, Fixture fixture, User user)
        => db.OrganizationMemberships.SingleAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == user.Id);

    private static async Task SeedAsync(Fixture fixture, OrganizationMembership membership, int count)
    {
        await using IdentityDbContext db = fixture.NewDb();
        for (int i = 0; i < count; i++)
        {
            long revision = 100 + (i / 3);
            int ordinal = 1 + (i % 3);
            DateTimeOffset time = fixture.Clock.Now.AddSeconds((count - i) / 4);
            db.IdentityAuditEvents.Add(IdentityAuditEvent.Create(
                fixture.Organization.Id,
                membership.Id,
                revision,
                "OrganizationMembership",
                membership.Id,
                revision,
                ordinal,
                IdentityAuditSchemaCatalog.MembershipStatusChanged,
                "succeeded",
                "user",
                i % 2 == 0 ? fixture.Owner.Id : fixture.Delegate.Id,
                null,
                time,
                time,
                "audit-page-fixture",
                JsonSerializer.Serialize(new { userId = membership.UserId, previousStatus = "active", newStatus = "removed" }),
                "Historical transition"));
        }

        db.IdentityAuditEvents.Add(IdentityAuditEvent.Create(
            fixture.Organization.Id,
            membership.Id,
            10000,
            "OrganizationMembership",
            membership.Id,
            10000,
            1,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            "denied",
            "user",
            fixture.Owner.Id,
            null,
            fixture.Clock.Now,
            fixture.Clock.Now,
            "denied-fixture",
            JsonSerializer.Serialize(new { userId = membership.UserId, previousStatus = "active", newStatus = "removed" }),
            "Denied operation"));
        await db.SaveChangesAsync();
    }

    private static async Task<Guid[]> ReadAllAsync(HttpClient client, string route)
    {
        var ids = new List<Guid>();
        string? cursor = null;
        do
        {
            JsonElement page = await DataAsync(await client.GetAsync(route + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor))), HttpStatusCode.OK);
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("eventId").GetGuid()));
            cursor = page.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        return ids.ToArray();
    }

    private sealed record CapturedCommand(string Sql, (string Name, NpgsqlTypes.NpgsqlDbType Type, object Value)[] Parameters);

    private sealed class CaptureAuditQuery : DbCommandInterceptor
    {
        public CapturedCommand? Last { get; private set; }
        public bool Tracked { get; private set; }
        public bool TransactionOpened { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("shared.\"IdentityAuditEvents\"", StringComparison.Ordinal))
            {
                Last = new CapturedCommand(command.CommandText, command.Parameters.Cast<NpgsqlParameter>().Select(parameter => (parameter.ParameterName, parameter.NpgsqlDbType, parameter.Value!)).ToArray());
                Tracked |= eventData.Context!.ChangeTracker.Entries().Any();
                TransactionOpened |= command.Transaction is not null;
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailingCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Injected audit read fixture commit failure.");
    }

    private sealed class UnavailableStore : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Injected audit read authority outage.");
    }
}
