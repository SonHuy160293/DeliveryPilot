using System.Diagnostics.CodeAnalysis;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
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

namespace DeliveryPilot.Projects.Tests.Engagements;

[Collection("Projects PostgreSQL")]
[SuppressMessage("Design", "CA1001", Justification = "xUnit disposes the factory through IAsyncLifetime.")]
public sealed class EngagementReadHttpTests : IAsyncLifetime
{
    private readonly Guid _organization = Guid.NewGuid();
    private readonly Guid _otherOrganization = Guid.NewGuid();
    private readonly Guid _creator = Guid.NewGuid();
    private readonly Guid _otherCreator = Guid.NewGuid();
    private readonly AuthorityClient _authority = new();
    private readonly ReadQueryObserver _queries = new();
    private readonly ReadTransactionObserver _transactions = new();
    private readonly ReadFactory _factory;
    private Guid _caller;
    private Guid _owned;
    private Guid _otherOwned;
    private Guid _foreign;
    private string _verifier = string.Empty;

    public EngagementReadHttpTests()
    {
        _caller = _creator;
        _factory = new ReadFactory(_authority, _queries, _transactions);
    }

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
        DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Engagement own = Engagement.Create(_organization, _creator, now, "Own client", "OWN", new DateOnly(2026, 1, 1), contractAmount: 100m, contractCurrency: "USD", plannedEffortPersonDays: 4.5m);
        Engagement other = Engagement.Create(_organization, _otherCreator, now, "Other client", "OTHER", new DateOnly(2026, 1, 1));
        Engagement third = Engagement.Create(_organization, _otherCreator, now, "Third client", "THIRD", new DateOnly(2026, 1, 1));
        Engagement closed = Engagement.Create(_organization, _otherCreator, now.AddDays(-1), "Closed client", "CLOSED", new DateOnly(2026, 1, 1));
        closed.Close(_otherCreator, "Finished", now.AddDays(2));
        Engagement foreign = Engagement.Create(_otherOrganization, _otherCreator, now.AddDays(1), "Foreign client", "FOREIGN", new DateOnly(2026, 1, 1));
        _owned = own.Id;
        _otherOwned = other.Id;
        _foreign = foreign.Id;
        db.Engagements.AddRange(own, other, third, closed, foreign);
        EngagementContact active = EngagementContact.Create(_organization, own.Id, _creator, now, "Active contact", "active@example.test", EngagementContact.OtherRole);
        EngagementContact removed = EngagementContact.Create(_organization, own.Id, _creator, now, "Removed contact", "removed@example.test", EngagementContact.OtherRole);
        db.EngagementContacts.AddRange(active, removed);
        var milestone = EngagementMilestone.Create(_organization, own.Id, _creator, now, "Commitment", new DateOnly(2026, 5, 1));
        db.EngagementMilestones.Add(milestone);
        byte[] verifier = RandomNumberGenerator.GetBytes(32);
        _verifier = Convert.ToHexString(verifier);
        db.EngagementLinkCodes.Add(EngagementLinkCode.Create(_organization, own.Id, _creator, now, verifier));
        for (int i = 0; i < 3; i++)
        {
            db.EngagementEvents.Add(EngagementEvent.Create(_organization, own.Id, "milestone.date-changed", "Milestone", milestone.Id, _creator, _organization, now.AddDays(3), i + 1, "{\"previous\":\"2026-05-01\",\"new\":\"2026-06-01\"}", "Changed date"));
        }

        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE engagement."EngagementContacts" SET "RemovedAt" = {now}, "RemovedByUserId" = {_creator}
            WHERE "OrganizationId" = {_organization} AND "Id" = {removed.Id}
            """);
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext db = Context();
        Guid[] organizations = [_organization, _otherOrganization];
        await db.ProjectParticipantRoles.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.ProjectParticipants.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.ProjectGuestInvitations.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.Projects.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementEvents.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementContacts.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementMilestones.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.EngagementLinkCodes.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await db.Engagements.Where(x => organizations.Contains(x.OrganizationId)).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlTheory]
    [InlineData("read-all")]
    [InlineData("manage")]
    public async Task PrivilegedReadersSeeWorkspaceFullDetailWithoutVerifier(string mode)
    {
        _authority.Mode = mode;
        using HttpResponseMessage list = await GetAsync("?status=all");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.True(list.Headers.CacheControl?.NoStore);
        using JsonDocument page = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        JsonElement.ArrayEnumerator items = page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray();
        JsonElement[] values = items.ToArray();
        Assert.Equal(4, values.Length);
        Assert.DoesNotContain(values, x => x.GetProperty("id").GetGuid() == _foreign);
        JsonElement own = values.Single(x => x.GetProperty("id").GetGuid() == _owned);
        Assert.Equal(100m, own.GetProperty("contractAmount").GetDecimal());
        Assert.Equal("USD", own.GetProperty("contractCurrency").GetString());
        Assert.Equal(4.5m, own.GetProperty("plannedEffortPersonDays").GetDecimal());
        Assert.True(own.GetProperty("hasOutstandingLinkCode").GetBoolean());
        Assert.Equal(1, own.GetProperty("contacts").GetArrayLength());
        Assert.Equal("Active contact", own.GetProperty("contacts")[0].GetProperty("name").GetString());
        Assert.Equal("Commitment", own.GetProperty("milestones")[0].GetProperty("name").GetString());
        Assert.DoesNotContain("codeVerifier", page.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_verifier, page.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using HttpResponseMessage detail = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [PostgreSqlFact]
    public async Task CreatorSeesOnlyOwnEngagementAndNoTierSeesNothing()
    {
        using HttpResponseMessage list = await GetAsync("?status=all");
        using JsonDocument page = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        JsonElement items = page.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(_owned, items[0].GetProperty("id").GetGuid());
        Assert.Equal(100m, items[0].GetProperty("contractAmount").GetDecimal());
        Assert.Equal("USD", items[0].GetProperty("contractCurrency").GetString());
        using HttpResponseMessage own = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using HttpResponseMessage ownHistory = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.OK, ownHistory.StatusCode);
        Assert.DoesNotContain(_verifier, await ownHistory.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using HttpResponseMessage hidden = await GetAsync(_otherOwned.ToString());
        using HttpResponseMessage unknown = await GetAsync(Guid.NewGuid().ToString());
        using HttpResponseMessage foreign = await GetAsync(_foreign.ToString());
        await AssertSameNotFoundAsync(hidden, unknown);
        await AssertSameNotFoundAsync(foreign, unknown);
        _caller = Guid.NewGuid();
        using HttpResponseMessage none = await GetAsync("?status=all");
        using JsonDocument empty = await JsonDocument.ParseAsync(await none.Content.ReadAsStreamAsync());
        Assert.Equal(0, empty.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        using HttpResponseMessage noHistory = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.NotFound, noHistory.StatusCode);
    }

    [PostgreSqlFact]
    public async Task PagedListsAndHistoryHaveStableTiesAndBoundCursors()
    {
        _authority.Mode = "read-all";
        var seen = new HashSet<Guid>();
        string? cursor = null;
        for (int i = 0; i < 4; i++)
        {
            string query = "?status=all&limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using HttpResponseMessage response = await GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument page = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement data = page.RootElement.GetProperty("data");
            Assert.True(seen.Add(data.GetProperty("items")[0].GetProperty("id").GetGuid()));
            cursor = data.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : data.GetProperty("nextCursor").GetString();
            if (i == 0)
            {
                using HttpResponseMessage wrongFilter = await GetAsync("?status=active&limit=1&cursor=" + Uri.EscapeDataString(cursor!));
                Assert.Equal(HttpStatusCode.BadRequest, wrongFilter.StatusCode);
            }
        }

        Assert.Equal(4, seen.Count);
        Assert.Null(cursor);
        var events = new HashSet<Guid>();
        cursor = null;
        for (int i = 0; i < 3; i++)
        {
            string query = _owned + "/history?limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using HttpResponseMessage response = await GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument page = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement data = page.RootElement.GetProperty("data");
            JsonElement entry = data.GetProperty("items")[0];
            Assert.True(events.Add(entry.GetProperty("id").GetGuid()));
            Assert.Equal(_creator, entry.GetProperty("actorUserId").GetGuid());
            Assert.Equal("Changed date", entry.GetProperty("reason").GetString());
            Assert.Equal("2026-06-01", entry.GetProperty("details").GetProperty("new").GetString());
            cursor = data.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : data.GetProperty("nextCursor").GetString();
        }

        Assert.Equal(3, events.Count);
        Assert.Null(cursor);
    }

    [PostgreSqlTheory]
    [InlineData("?status=unknown")]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=not-a-number")]
    [InlineData("?limit=")]
    [InlineData("?status=")]
    [InlineData("?cursor=invalid")]
    [InlineData("?cursor=")]
    public async Task InvalidPagingAndStatusAreBadRequests(string query)
    {
        _authority.Mode = "read-all";
        using HttpResponseMessage response = await GetAsync(query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [PostgreSqlFact]
    public async Task StatusFiltersAndAuthorityRevocationApplyOnEveryRequest()
    {
        _authority.Mode = "manage";
        using HttpResponseMessage active = await GetAsync(string.Empty);
        using JsonDocument activePage = await JsonDocument.ParseAsync(await active.Content.ReadAsStreamAsync());
        Assert.Equal(3, activePage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        using HttpResponseMessage closed = await GetAsync("?status=closed");
        using JsonDocument closedPage = await JsonDocument.ParseAsync(await closed.Content.ReadAsStreamAsync());
        Assert.Equal(1, closedPage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        _authority.Mode = "denied";
        using HttpResponseMessage denied = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        _authority.Mode = "unavailable";
        using HttpResponseMessage unavailable = await GetAsync(_owned + "/history");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.True(unavailable.Headers.CacheControl?.NoStore);
    }

    [PostgreSqlFact]
    public async Task AnonymousRequestsCannotReadListsDetailsOrHistory()
    {
        using HttpClient client = _factory.CreateClient();
        string root = $"/api/v1/organizations/{_organization:D}/engagements";
        foreach (string suffix in new[] { string.Empty, "/" + _owned, "/" + _owned + "/history" })
        {
            using HttpResponseMessage response = await client.GetAsync(root + suffix);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [PostgreSqlFact]
    public async Task MemberParticipantGetsOnlyTheSummaryAndCannotReadHistory()
    {
        _caller = Guid.NewGuid();
        await AddParticipantAsync(_owned, _caller);
        _authority.DirectoryUnavailable = true;
        using HttpResponseMessage detail = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        using JsonDocument document = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        JsonElement summary = document.RootElement.GetProperty("data");
        AssertSummary(summary);
        Assert.Equal("Own client", summary.GetProperty("clientDisplayName").GetString());
        Assert.Equal("Commitment", summary.GetProperty("milestones")[0].GetProperty("name").GetString());
        using HttpResponseMessage list = await GetAsync("?status=all");
        using JsonDocument page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        JsonElement item = Assert.Single(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
        AssertSummary(item);
        Assert.Equal(_owned, item.GetProperty("id").GetGuid());
        using HttpResponseMessage history = await GetAsync(_owned + "/history");
        using HttpResponseMessage unknown = await GetAsync(Guid.NewGuid() + "/history");
        await AssertSameNotFoundAsync(history, unknown);
        Assert.Empty(_authority.Lookups);
    }

    [PostgreSqlTheory]
    [InlineData("Removed")]
    [InlineData("Left")]
    public async Task EndedParticipationLosesSummaryOnTheNextRequest(string status)
    {
        _caller = Guid.NewGuid();
        Guid participantId = await AddParticipantAsync(_owned, _caller);
        using HttpResponseMessage before = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await using ProjectsDbContext db = Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE project."ProjectParticipants" SET "Status" = {status}, "EndedAt" = now(), "EndedByUserId" = {_creator}
            WHERE "OrganizationId" = {_organization} AND "Id" = {participantId}
            """);
        using HttpResponseMessage after = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        using HttpResponseMessage list = await GetAsync("?status=all");
        using JsonDocument page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Empty(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
    }

    [PostgreSqlTheory]
    [InlineData("none", HttpStatusCode.NotFound)]
    [InlineData("denied", HttpStatusCode.Forbidden)]
    public async Task GuestParticipationGrantsNoEngagementTierEvenIfIdentityAllowsIt(string mode, HttpStatusCode expected)
    {
        _caller = Guid.NewGuid();
        await AddParticipantAsync(_owned, _caller, guest: true);
        _authority.Mode = mode;
        using HttpResponseMessage detail = await GetAsync(_owned.ToString());
        Assert.Equal(expected, detail.StatusCode);
        using HttpResponseMessage list = await GetAsync("?status=all");
        if (mode == "none")
        {
            using JsonDocument page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            Assert.Empty(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        }
    }

    [PostgreSqlTheory]
    [InlineData("other")]
    [InlineData("internal")]
    [InlineData("foreign")]
    public async Task ParticipationDoesNotGrantAccessToOtherEngagementsOrTenants(string kind)
    {
        _caller = Guid.NewGuid();
        await AddParticipantAsync(kind == "foreign" ? _foreign : _otherOwned, _caller, internalProject: kind == "internal");
        using HttpResponseMessage detail = await GetAsync(_owned.ToString());
        using HttpResponseMessage unknown = await GetAsync(Guid.NewGuid().ToString());
        await AssertSameNotFoundAsync(detail, unknown);
        using HttpResponseMessage foreign = await GetAsync(_foreign.ToString());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using HttpResponseMessage list = await GetAsync("?status=all");
        using JsonDocument page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Guid[] ids = page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(kind == "other" ? [_otherOwned] : Array.Empty<Guid>(), ids);
    }

    [PostgreSqlFact]
    public async Task FullTierWinsAndProjectsHaveOnlyNamesStatusesAndCurrentAdministrators()
    {
        await AddParticipantAsync(_owned, _caller);
        DateTimeOffset createdAt = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        await using ProjectsDbContext db = Context();
        Project first = Project.Create(_organization, _creator, createdAt, "First", "Private purpose", "Client", _owned);
        Project second = Project.Create(_organization, _creator, createdAt.AddDays(1), "Second", "Private purpose", "Client", _owned);
        ProjectParticipant alpha = ProjectParticipant.CreateMember(_organization, first.Id, Guid.NewGuid(), createdAt);
        ProjectParticipant zulu = ProjectParticipant.CreateMember(_organization, first.Id, Guid.NewGuid(), createdAt);
        ProjectParticipant missing = ProjectParticipant.CreateMember(_organization, first.Id, Guid.NewGuid(), createdAt);
        ProjectParticipant removed = ProjectParticipant.CreateMember(_organization, first.Id, Guid.NewGuid(), createdAt);
        ProjectParticipant revoked = ProjectParticipant.CreateMember(_organization, first.Id, Guid.NewGuid(), createdAt);
        ProjectParticipantRole revokedRole = ProjectParticipantRole.Grant(revoked, "ProjectAdministrator", _creator, createdAt);
        db.AddRange(first, second, alpha, zulu, missing, removed, revoked, revokedRole);
        foreach (ProjectParticipant participant in new[] { alpha, zulu, missing, removed })
        {
            db.Add(ProjectParticipantRole.Grant(participant, "ProjectAdministrator", _creator, createdAt));
        }

        await db.SaveChangesAsync();
        db.Entry(removed).Property(x => x.Status).CurrentValue = "Removed";
        db.Entry(removed).Property(x => x.EndedAt).CurrentValue = createdAt;
        db.Entry(removed).Property(x => x.EndedByUserId).CurrentValue = _creator;
        db.Entry(revokedRole).Property(x => x.RevokedAt).CurrentValue = createdAt;
        db.Entry(revokedRole).Property(x => x.RevokedByUserId).CurrentValue = _creator;
        await db.SaveChangesAsync();
        _authority.Names[alpha.UserId] = "Alpha";
        _authority.Names[zulu.UserId] = "Zulu";
        using HttpResponseMessage response = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal("full", data.GetProperty("tier").GetString());
        Assert.Equal(100m, data.GetProperty("contractAmount").GetDecimal());
        JsonElement[] projects = data.GetProperty("projects").EnumerateArray().ToArray();
        Assert.Equal(["First", "Second"], projects.Where(project => project.GetProperty("name").GetString() != "Participant project").Select(project => project.GetProperty("name").GetString()));
        JsonElement[] admins = projects.Single(project => project.GetProperty("id").GetGuid() == first.Id).GetProperty("administrators").EnumerateArray().ToArray();
        Assert.Equal([missing.UserId, alpha.UserId, zulu.UserId], admins.Select(admin => admin.GetProperty("userId").GetGuid()));
        Assert.Equal(JsonValueKind.Null, admins[0].GetProperty("displayName").ValueKind);
        Assert.Equal("Alpha", admins[1].GetProperty("displayName").GetString());
        Assert.All(projects, project => Assert.Equal(["administrators", "id", "name", "status"], project.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.All(admins, admin => Assert.Equal(["displayName", "userId"], admin.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.Empty(projects.Single(project => project.GetProperty("id").GetGuid() == second.Id).GetProperty("administrators").EnumerateArray());
        Assert.Single(_authority.Lookups);

        _caller = Guid.NewGuid();
        _authority.Mode = "read-all";
        using HttpResponseMessage readAll = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, readAll.StatusCode);
    }

    [PostgreSqlFact]
    public async Task DisplayNamesAreUncachedAndFailureIs503OnlyWhenNamesAreNeeded()
    {
        await AddParticipantAsync(_owned, Guid.NewGuid(), administrator: true);
        _authority.DirectoryUnavailable = true;
        using HttpResponseMessage unavailable = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.True(unavailable.Headers.CacheControl?.NoStore);
        using JsonDocument error = JsonDocument.Parse(await unavailable.Content.ReadAsStringAsync());
        Assert.Equal("Identity.Unavailable", error.RootElement.GetProperty("error").GetProperty("code").GetString());
        _authority.DirectoryUnavailable = false;
        Guid admin = Assert.Single(_authority.Lookups)[0];
        _authority.Names[admin] = "Before";
        using HttpResponseMessage before = await GetAsync(_owned.ToString());
        Assert.Contains("Before", await before.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        _authority.Names[admin] = "After";
        using HttpResponseMessage after = await GetAsync(_owned.ToString());
        Assert.Contains("After", await after.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(3, _authority.Lookups.Count);

        _authority.Mode = "read-all";
        _authority.DirectoryUnavailable = true;
        _authority.Lookups.Clear();
        using HttpResponseMessage noAdmins = await GetAsync(_otherOwned.ToString());
        Assert.Equal(HttpStatusCode.OK, noAdmins.StatusCode);
        Assert.Empty(_authority.Lookups);
        using HttpResponseMessage list = await GetAsync("?status=all");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.All(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray(), item => Assert.False(item.TryGetProperty("projects", out _)));
        Assert.Empty(_authority.Lookups);
    }

    [PostgreSqlFact]
    public async Task AdministratorLookupDeduplicatesAcrossProjectsAndBatchesAt100()
    {
        await using ProjectsDbContext db = Context();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Project first = Project.Create(_organization, _creator, now, "First", "Purpose", "Client", _owned);
        Project second = Project.Create(_organization, _creator, now, "Second", "Purpose", "Client", _owned);
        db.AddRange(first, second);
        Guid[] users = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        foreach (Guid user in users)
        {
            ProjectParticipant participant = ProjectParticipant.CreateMember(_organization, first.Id, user, now);
            db.AddRange(participant, ProjectParticipantRole.Grant(participant, "ProjectAdministrator", _creator, now));
        }

        ProjectParticipant repeated = ProjectParticipant.CreateMember(_organization, second.Id, users[0], now);
        db.AddRange(repeated, ProjectParticipantRole.Grant(repeated, "ProjectAdministrator", _creator, now));
        await db.SaveChangesAsync();
        using HttpResponseMessage response = await GetAsync(_owned.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([100, 1], _authority.Lookups.Select(batch => batch.Length));
        Guid[] lookedUp = _authority.Lookups.SelectMany(batch => batch).ToArray();
        Assert.Equal(101, lookedUp.Distinct().Count());
        Assert.Equal(users.Order(), lookedUp.Order());
    }

    [PostgreSqlFact]
    public async Task MixedFullAndSummaryPagesKeepStableOrderCursorsAndFilters()
    {
        _caller = _otherCreator;
        await AddParticipantAsync(_owned, _caller);
        _authority.DirectoryUnavailable = true;
        var actual = new List<Guid>();
        string? cursor = null;
        for (int i = 0; i < 4; i++)
        {
            string query = "?status=all&limit=1" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using HttpResponseMessage response = await GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement page = document.RootElement.GetProperty("data");
            JsonElement item = Assert.Single(page.GetProperty("items").EnumerateArray());
            Guid id = item.GetProperty("id").GetGuid();
            actual.Add(id);
            Assert.Equal(id == _owned ? "summary" : "full", item.GetProperty("tier").GetString());
            Assert.False(item.TryGetProperty("projects", out _));
            if (id == _owned)
            {
                AssertSummary(item);
            }

            cursor = page.GetProperty("nextCursor").GetString();
        }

        Assert.Null(cursor);
        await using ProjectsDbContext db = Context();
        Guid[] expected = await db.Engagements.Where(row => row.OrganizationId == _organization).OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id).Select(row => row.Id).ToArrayAsync();
        Assert.Equal(expected, actual);
        using HttpResponseMessage active = await GetAsync("?status=active");
        using JsonDocument activePage = JsonDocument.Parse(await active.Content.ReadAsStringAsync());
        Assert.Equal(3, activePage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        using HttpResponseMessage closed = await GetAsync("?status=closed");
        using JsonDocument closedPage = JsonDocument.Parse(await closed.Content.ReadAsStringAsync());
        Assert.Equal(1, closedPage.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.Empty(_authority.Lookups);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PagesBatchChildReadsAndNeverOpenCommandTransactions(bool full)
    {
        _caller = Guid.NewGuid();
        await AddParticipantAsync(_owned, _caller);
        await AddParticipantAsync(_otherOwned, _caller);
        _authority.Mode = full ? "read-all" : "none";
        using HttpResponseMessage response = await GetAsync("?status=all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(full ? 4 : 2, _queries.Commands.Count);
        Assert.Single(_queries.Commands, sql => sql.Contains("FROM engagement.\"EngagementMilestones\"", StringComparison.Ordinal));
        Assert.DoesNotContain(_queries.Commands, sql => sql.Contains("CodeVerifier", StringComparison.Ordinal));
        if (!full)
        {
            Assert.DoesNotContain(_queries.Commands, sql => sql.Contains("EngagementContacts", StringComparison.Ordinal) || sql.Contains("EngagementLinkCodes", StringComparison.Ordinal));
        }

        Assert.Equal(0, _transactions.Started);
        Assert.Empty(_authority.Lookups);
    }

    private static void AssertSummary(JsonElement summary)
        => Assert.Equal(["agreementReference", "clientDisplayName", "engagementName", "id", "milestones", "status", "tier", "version"], summary.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

    private async Task<Guid> AddParticipantAsync(Guid engagementId, Guid userId, bool guest = false, bool internalProject = false, bool administrator = false)
    {
        await using ProjectsDbContext db = Context();
        Engagement engagement = await db.Engagements.SingleAsync(row => row.Id == engagementId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Project project = Project.Create(engagement.OrganizationId, _creator, now, "Participant project", "Purpose", internalProject ? "Internal" : "Client", internalProject ? null : engagementId);
        ProjectParticipant participant;
        db.Add(project);
        if (guest)
        {
            ProjectGuestInvitation invitation = ProjectGuestInvitation.Issue(engagement.OrganizationId, project.Id, _creator, now, "guest@example.test", "Viewer", RandomNumberGenerator.GetBytes(32));
            db.Add(invitation);
            participant = ProjectParticipant.CreateGuest(engagement.OrganizationId, project.Id, userId, now, invitation.Id);
        }
        else
        {
            participant = ProjectParticipant.CreateMember(engagement.OrganizationId, project.Id, userId, now);
        }

        db.Add(participant);
        if (administrator)
        {
            db.Add(ProjectParticipantRole.Grant(participant, "ProjectAdministrator", _creator, now));
        }

        await db.SaveChangesAsync();
        return participant.Id;
    }

    private async Task<HttpResponseMessage> GetAsync(string suffix)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        string separator = suffix.StartsWith('?') || suffix.Length == 0 ? string.Empty : "/";
        return await client.GetAsync($"/api/v1/organizations/{_organization:D}/engagements{separator}{suffix}");
    }

    private static async Task AssertSameNotFoundAsync(HttpResponseMessage actual, HttpResponseMessage expected)
    {
        Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expected.StatusCode);
        using JsonDocument a = JsonDocument.Parse(await actual.Content.ReadAsStringAsync());
        using JsonDocument b = JsonDocument.Parse(await expected.Content.ReadAsStringAsync());
        Assert.Equal(b.RootElement.GetProperty("error").GetProperty("code").GetString(), a.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(b.RootElement.GetProperty("error").GetProperty("message").GetString(), a.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private string Token()
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = _caller, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        string key = TestConfiguration.Host()["Jwt:SecretKey"] ?? throw new InvalidOperationException("Test signing key is missing.");
        return message + "." + Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ReadFactory(AuthorityClient authority, ReadQueryObserver queries, ReadTransactionObserver transactions) : WebApplicationFactory<Program>
    {
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
                services.AddSingleton<IIdentityAuthorityClient>(authority);
                services.AddDbContext<ProjectsDbContext>(options => options.AddInterceptors(queries, transactions));
            });
        }
    }

    private sealed class ReadQueryObserver : DbCommandInterceptor
    {
        internal List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
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
        internal string Mode { get; set; } = "none";
        internal bool DirectoryUnavailable { get; set; }
        internal Dictionary<Guid, string> Names { get; } = [];
        internal List<Guid[]> Lookups { get; } = [];

        public Task<IntegrationResult<UserLookupResult>> LookupUsersAsync(UserLookupRequest request, CancellationToken cancellationToken)
        {
            Guid[] ids = request.UserIds!.ToArray();
            Lookups.Add(ids);
            UserIntegrationDto[] users = ids.Where(Names.ContainsKey).Select(id => new UserIntegrationDto(id, Names[id], null, "active")).ToArray();
            return Task.FromResult(DirectoryUnavailable
                ? FakeIdentityAuthorityClient.Unavailable<UserLookupResult>()
                : IntegrationResult<UserLookupResult>.Success(new UserLookupResult(users), HttpStatusCode.OK));
        }

        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
            => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));

        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            string[] permissions = Mode switch
            {
                "read-all" => [WorkspacePermissions.EngagementsReadAll],
                "manage" => [WorkspacePermissions.EngagementsManage],
                _ => [],
            };
            OrganizationAuthorityCheckResult result = Mode == "denied"
                ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow)
                : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, false, false, permissions);
            return Task.FromResult(Mode == "unavailable" ? FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>() : IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }
}
