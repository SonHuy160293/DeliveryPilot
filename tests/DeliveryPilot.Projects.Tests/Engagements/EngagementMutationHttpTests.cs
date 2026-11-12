using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Infrastructure.Persistence;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DeliveryPilot.Projects.Tests.Engagements;

[Collection("Projects PostgreSQL")]
[SuppressMessage("Design", "CA1001", Justification = "xUnit disposes the factory through IAsyncLifetime.")]
public sealed class EngagementMutationHttpTests : IAsyncLifetime
{
    private static readonly string[] _expectedClosureEvents = ["milestone.added", "engagement.closed", "engagement.reopened", "milestone.changed"];
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly AuthorityClient _authority = new();
    private readonly MutationFactory _factory;
    private Guid _engagementId;

    public EngagementMutationHttpTests() => _factory = new MutationFactory(_authority);

    public async Task InitializeAsync()
    {
        await using ProjectsDbContext db = Context();
        await db.Database.MigrateAsync();
        Engagement engagement = Engagement.Create(_organizationId, _userId, DateTimeOffset.UtcNow, "Client", "REF-1", new DateOnly(2026, 1, 1));
        _engagementId = engagement.Id;
        db.Engagements.Add(engagement);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using ProjectsDbContext db = Context();
        IQueryable<Guid> engagementIds = db.Engagements.Where(x => x.OrganizationId == _organizationId).Select(x => x.Id);
        await db.Set<DeliveryPilot.EntityFrameworkCore.Reliability.IdempotencyRecord>().Where(x => x.ResultResourceId != null && engagementIds.Contains(x.ResultResourceId.Value)).ExecuteDeleteAsync();
        await db.EngagementEvents.Where(x => x.OrganizationId == _organizationId).ExecuteDeleteAsync();
        await db.EngagementMilestones.Where(x => x.OrganizationId == _organizationId).ExecuteDeleteAsync();
        await db.EngagementContacts.Where(x => x.OrganizationId == _organizationId).ExecuteDeleteAsync();
        await db.EngagementLinkCodes.Where(x => x.OrganizationId == _organizationId).ExecuteDeleteAsync();
        await db.Engagements.Where(x => x.OrganizationId == _organizationId).ExecuteDeleteAsync();
        await _factory.DisposeAsync();
    }

    [PostgreSqlFact]
    public async Task ContractValuesRespectCurrencyLockClearingAndNoOpHistory()
    {
        using HttpResponseMessage first = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1, contractAmount = 120000.00m, contractCurrency = " usd ", plannedEffortPersonDays = 400m });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using JsonDocument firstBody = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        Assert.Equal("USD", firstBody.RootElement.GetProperty("data").GetProperty("contractCurrency").GetString());
        using HttpResponseMessage noOp = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 2, contractAmount = 120000m, contractCurrency = "USD", plannedEffortPersonDays = 400.0m });
        Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
        using HttpResponseMessage locked = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 2, contractAmount = (decimal?)null, contractCurrency = "EUR", plannedEffortPersonDays = 400m });
        await AssertErrorAsync(locked, HttpStatusCode.Conflict, "Engagement.CurrencyLocked");
        using HttpResponseMessage clear = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 2, contractAmount = (decimal?)null, contractCurrency = (string?)null, plannedEffortPersonDays = (decimal?)null, reason = "Clear values" });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        using HttpResponseMessage euro = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 3, contractAmount = 10m, contractCurrency = "EUR", plannedEffortPersonDays = 1.2m });
        Assert.Equal(HttpStatusCode.OK, euro.StatusCode);
        await using ProjectsDbContext db = Context();
        Engagement actual = await db.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Equal(4, actual.Version);
        Assert.Equal(10m, actual.ContractAmount);
        Assert.Equal("EUR", actual.ContractCurrency);
        EngagementEvent[] history = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.All(history, x => Assert.Equal("engagement.value-changed", x.EventType));
        Assert.Equal(_userId, history[1].ActorUserId);
        Assert.Equal(_organizationId, history[1].ActorOrganizationId);
        Assert.Equal("Clear values", history[1].Reason);
        using JsonDocument details = JsonDocument.Parse(history[1].Details);
        Assert.Equal(120000m, details.RootElement.GetProperty("previous").GetProperty("contractAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, details.RootElement.GetProperty("new").GetProperty("contractAmount").ValueKind);
    }

    [PostgreSqlTheory]
    [InlineData("negative")]
    [InlineData("amount-precision")]
    [InlineData("effort-precision")]
    [InlineData("currency")]
    [InlineData("missing-currency")]
    [InlineData("missing-version")]
    [InlineData("unknown-member")]
    public async Task InvalidContractValuesAreRejectedWithoutChanges(string scenario)
    {
        var body = new Dictionary<string, object?> { ["expectedVersion"] = 1, ["contractAmount"] = 10m, ["contractCurrency"] = "USD", ["plannedEffortPersonDays"] = 1m };
        switch (scenario)
        {
            case "negative": body["contractAmount"] = -1m; break;
            case "amount-precision": body["contractAmount"] = 10.123m; break;
            case "effort-precision": body["plannedEffortPersonDays"] = 1.25m; break;
            case "currency": body["contractCurrency"] = "XXA"; break;
            case "missing-currency": body["contractCurrency"] = null; break;
            case "missing-version": body.Remove("expectedVersion"); break;
            case "unknown-member": body["organizationId"] = Guid.NewGuid(); break;
        }

        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, "contract-value", body);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest);
        await using ProjectsDbContext db = Context();
        Assert.Equal(1, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Empty(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlTheory]
    [InlineData("1e-29", "1")]
    [InlineData("1.000000000000000000000000001", "1")]
    [InlineData("79228162514264337593543950335.1", "1")]
    [InlineData("1234567890123456789012345678.99", "1")]
    [InlineData("1", "1e-29")]
    [InlineData("1", "0.10000000000000000000000000001")]
    public async Task JsonNumbersCannotRoundAwayInvalidPrecision(string amount, string effort)
    {
        string json = $"{{\"expectedVersion\":1,\"contractAmount\":{amount},\"contractCurrency\":\"USD\",\"plannedEffortPersonDays\":{effort}}}";
        using HttpResponseMessage response = await SendRawAsync("contract-value", json);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest);
        await using ProjectsDbContext db = Context();
        Assert.Equal(1, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Empty(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task ExactScientificNumbersAndTrailingZerosAreAccepted()
    {
        using HttpResponseMessage response = await SendRawAsync("contract-value", "{\"expectedVersion\":1,\"contractAmount\":1.2300e1,\"contractCurrency\":\"USD\",\"plannedEffortPersonDays\":0.1000}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using ProjectsDbContext db = Context();
        Engagement engagement = await db.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Equal(12.3m, engagement.ContractAmount);
        Assert.Equal(0.1m, engagement.PlannedEffortPersonDays);
    }

    [PostgreSqlFact]
    public async Task ConcurrentContractChangesFromSameVersionHaveOneWinner()
    {
        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1, contractAmount = 1m, contractCurrency = "USD" }),
            SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1, contractAmount = 2m, contractCurrency = "USD" }));
        try
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            await using ProjectsDbContext db = Context();
            Assert.Equal(1, await db.EngagementEvents.CountAsync());
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [PostgreSqlTheory]
    [InlineData("denied", HttpStatusCode.Forbidden)]
    [InlineData("missing-permission", HttpStatusCode.Forbidden)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("revoked-after-lock", HttpStatusCode.Forbidden)]
    public async Task MutationsFailClosedBeforeAnyChanges(string mode, HttpStatusCode expected)
    {
        _authority.Mode = mode;
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1, contractAmount = 1m, contractCurrency = "USD" });
        await AssertErrorAsync(response, expected);
        await using ProjectsDbContext db = Context();
        Assert.Empty(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task OtherWorkspaceDoesNotRevealEngagement()
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1 }, Guid.NewGuid());
        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [PostgreSqlFact]
    public async Task MilestoneDateChangesPreserveOriginalAndAppendAttributedHistory()
    {
        Guid id = await AddMilestoneAsync("First");
        using HttpResponseMessage missingReason = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-date", new { expectedVersion = 1, targetDate = "2027-02-02", reason = " " });
        await AssertErrorAsync(missingReason, HttpStatusCode.BadRequest);
        using HttpResponseMessage changed = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-date", new { expectedVersion = 1, targetDate = "2027-02-02", reason = " Client requested " });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using JsonDocument result = await JsonDocument.ParseAsync(await changed.Content.ReadAsStreamAsync());
        JsonElement milestone = result.RootElement.GetProperty("data");
        Assert.Equal("2027-01-01", milestone.GetProperty("originalTargetDate").GetString());
        Assert.Equal("2027-02-02", milestone.GetProperty("currentTargetDate").GetString());
        Assert.Equal(2, milestone.GetProperty("version").GetInt64());
        using HttpResponseMessage stale = await SendAsync(HttpMethod.Put, $"milestones/{id}", new { expectedVersion = 1, name = "Changed" });
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "Milestone.VersionConflict");
        await using ProjectsDbContext db = Context();
        EngagementEvent entry = await db.EngagementEvents.SingleAsync(x => x.EventType == "milestone.date-changed");
        Assert.Equal("Client requested", entry.Reason);
        Assert.Equal(_userId, entry.ActorUserId);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
        Assert.Equal(3, entry.EngagementVersion);
        using JsonDocument details = JsonDocument.Parse(entry.Details);
        Assert.Equal("2027-01-01", details.RootElement.GetProperty("originalTargetDate").GetString());
        Assert.Equal("2027-01-01", details.RootElement.GetProperty("previous").GetString());
        Assert.Equal("2027-02-02", details.RootElement.GetProperty("new").GetString());
    }

    [PostgreSqlTheory]
    [InlineData("Planned", "Achieved")]
    [InlineData("Planned", "Missed")]
    [InlineData("Planned", "Retired")]
    [InlineData("Missed", "Achieved")]
    [InlineData("Missed", "Retired")]
    public async Task PermittedMilestoneTransitionsSucceed(string start, string target)
    {
        Guid id = await AddMilestoneAsync("Transition");
        int version = 1;
        if (start == "Missed")
        {
            using HttpResponseMessage missed = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = version++, status = start });
            Assert.Equal(HttpStatusCode.OK, missed.StatusCode);
        }

        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = version, status = target });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using ProjectsDbContext db = Context();
        Assert.Equal(target, (await db.EngagementMilestones.SingleAsync(x => x.Id == id)).Status);
    }

    [PostgreSqlTheory]
    [InlineData("Achieved")]
    [InlineData("Retired")]
    public async Task TerminalMilestonesRejectEveryKindOfEdit(string terminal)
    {
        Guid id = await AddMilestoneAsync("Terminal");
        using HttpResponseMessage terminalResponse = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = 1, status = terminal });
        Assert.Equal(HttpStatusCode.OK, terminalResponse.StatusCode);
        using HttpResponseMessage status = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = 2, status = "Planned" });
        using HttpResponseMessage edit = await SendAsync(HttpMethod.Put, $"milestones/{id}", new { expectedVersion = 2, name = "Changed" });
        using HttpResponseMessage date = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-date", new { expectedVersion = 2, targetDate = "2027-02-02", reason = "Change" });
        await AssertErrorAsync(status, HttpStatusCode.Conflict, "Milestone.InvalidTransition");
        await AssertErrorAsync(edit, HttpStatusCode.Conflict, "Milestone.InvalidTransition");
        await AssertErrorAsync(date, HttpStatusCode.Conflict, "Milestone.InvalidTransition");
        await using ProjectsDbContext db = Context();
        Assert.Equal(2, await db.EngagementEvents.CountAsync());
    }

    [PostgreSqlFact]
    public async Task DifferentMilestonesCanBeEditedConcurrentlyAndUnknownChildIsHidden()
    {
        Guid first = await AddMilestoneAsync("First");
        Guid second = await AddMilestoneAsync("Second");
        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(HttpMethod.Put, $"milestones/{first}", new { expectedVersion = 1, name = "First revised" }),
            SendAsync(HttpMethod.Put, $"milestones/{second}", new { expectedVersion = 1, name = "Second revised" }));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        using HttpResponseMessage missing = await SendAsync(HttpMethod.Put, $"milestones/{Guid.NewGuid()}", new { expectedVersion = 1, name = "Missing" });
        await AssertErrorAsync(missing, HttpStatusCode.NotFound);
        await using ProjectsDbContext db = Context();
        Assert.Equal(5, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Equal(4, await db.EngagementEvents.CountAsync());
    }

    [PostgreSqlFact]
    public async Task CloseInvalidatesOutstandingCodeBlocksAllExistingEditsAndReopenRestoresThem()
    {
        Guid milestoneId = await AddMilestoneAsync("Existing milestone");
        Guid linkedOrganization = Guid.NewGuid();
        Guid codeId;
        await using (ProjectsDbContext setup = Context())
        {
            var code = EngagementLinkCode.Create(_organizationId, _engagementId, _userId, DateTimeOffset.UtcNow, RandomNumberGenerator.GetBytes(32));
            codeId = code.Id;
            setup.EngagementLinkCodes.Add(code);
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE engagement."Engagements"
                SET "ClientOrganizationId" = {linkedOrganization}, "ClientLinkedAt" = now(), "ClientLinkedByUserId" = {_userId}
                WHERE "OrganizationId" = {_organizationId} AND "Id" = {_engagementId}
                """);
        }

        using HttpResponseMessage missingReason = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = " " });
        await AssertErrorAsync(missingReason, HttpStatusCode.BadRequest);
        using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = " Completed " });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using JsonDocument closeBody = await JsonDocument.ParseAsync(await close.Content.ReadAsStreamAsync());
        Assert.Equal("Closed", closeBody.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(_userId, closeBody.RootElement.GetProperty("data").GetProperty("closedByUserId").GetGuid());
        using HttpResponseMessage stale = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = "Again" });
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "Engagement.VersionConflict");
        using HttpResponseMessage noOp = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 3, reason = "Again" });
        Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
        using HttpResponseMessage value = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 3, contractAmount = 2m, contractCurrency = "USD" });
        using HttpResponseMessage add = await SendAsync(HttpMethod.Post, "milestones", new { name = "Blocked", targetDate = "2027-01-01" });
        using HttpResponseMessage edit = await SendAsync(HttpMethod.Put, $"milestones/{milestoneId}", new { expectedVersion = 1, name = "Blocked" });
        using HttpResponseMessage date = await SendAsync(HttpMethod.Post, $"milestones/{milestoneId}/change-date", new { expectedVersion = 1, targetDate = "2027-02-02", reason = "Blocked" });
        using HttpResponseMessage status = await SendAsync(HttpMethod.Post, $"milestones/{milestoneId}/change-status", new { expectedVersion = 1, status = "Missed" });
        foreach (HttpResponseMessage blocked in new[] { value, add, edit, date, status })
        {
            await AssertErrorAsync(blocked, HttpStatusCode.Conflict, "Engagement.Closed");
        }

        using HttpResponseMessage reopen = await SendAsync(HttpMethod.Post, "reopen", new { expectedVersion = 3, reason = " Resume " });
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        using HttpResponseMessage reopenNoOp = await SendAsync(HttpMethod.Post, "reopen", new { expectedVersion = 4, reason = "Again" });
        Assert.Equal(HttpStatusCode.OK, reopenNoOp.StatusCode);
        using HttpResponseMessage after = await SendAsync(HttpMethod.Put, $"milestones/{milestoneId}", new { expectedVersion = 1, name = "Reopened" });
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        await using ProjectsDbContext db = Context();
        Engagement engagement = await db.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Equal("Active", engagement.Status);
        Assert.Null(engagement.ClosedAt);
        Assert.Null(engagement.ClosedByUserId);
        Assert.Equal(linkedOrganization, engagement.ClientOrganizationId);
        EngagementLinkCode codeAfter = await db.EngagementLinkCodes.SingleAsync(x => x.Id == codeId);
        Assert.Equal("Invalidated", codeAfter.Status);
        Assert.NotNull(codeAfter.InvalidatedAt);
        Assert.Equal(2, codeAfter.Version);
        EngagementEvent[] entries = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
        Assert.Equal(_expectedClosureEvents, entries.Select(x => x.EventType));
        Assert.Equal("Completed", entries[1].Reason);
        Assert.Equal("Resume", entries[2].Reason);
        Assert.Equal(_userId, entries[2].ActorUserId);
        using JsonDocument closeDetails = JsonDocument.Parse(entries[1].Details);
        Assert.Equal(codeId, closeDetails.RootElement.GetProperty("invalidatedLinkCodeId").GetGuid());
        Assert.Equal("Active", closeDetails.RootElement.GetProperty("previousStatus").GetString());
        Assert.Equal("Closed", closeDetails.RootElement.GetProperty("newStatus").GetString());
        Assert.DoesNotContain("verifier", entries[1].Details, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task ConcurrentCloseAndMilestoneEditCannotCommitAnEditAfterClose()
    {
        Guid milestoneId = await AddMilestoneAsync("Race");
        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = "Closing" }),
            SendAsync(HttpMethod.Put, $"milestones/{milestoneId}", new { expectedVersion = 1, name = "Raced edit" }));
        try
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            await using ProjectsDbContext db = Context();
            EngagementEvent[] events = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
            EngagementEvent? closure = events.SingleOrDefault(x => x.EventType == "engagement.closed");
            if (closure is not null)
            {
                Assert.DoesNotContain(events, x => x.EventType == "milestone.changed" && x.EngagementVersion > closure.EngagementVersion);
                Assert.Equal("Closed", (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Status);
            }
            else
            {
                Assert.Contains(events, x => x.EventType == "milestone.changed");
                Assert.Equal("Active", (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Status);
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

    [PostgreSqlFact]
    public async Task MilestoneBelongingToAnotherEngagementIsNotFound()
    {
        Guid otherMilestoneId;
        await using (ProjectsDbContext db = Context())
        {
            Engagement other = Engagement.Create(_organizationId, _userId, DateTimeOffset.UtcNow, "Other", "OTHER", new DateOnly(2026, 1, 1));
            EngagementMilestone otherMilestone = EngagementMilestone.Create(_organizationId, other.Id, _userId, DateTimeOffset.UtcNow, "Other milestone", new DateOnly(2027, 1, 1));
            otherMilestoneId = otherMilestone.Id;
            db.Engagements.Add(other);
            db.EngagementMilestones.Add(otherMilestone);
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"milestones/{otherMilestoneId}", new { expectedVersion = 1, name = "No" });
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "Milestone.NotFound");
    }

    [PostgreSqlFact]
    public async Task NoOpMilestoneEditsDoNotIncrementVersionsOrAppendHistory()
    {
        Guid id = await AddMilestoneAsync("No-op");
        using HttpResponseMessage edit = await SendAsync(HttpMethod.Put, $"milestones/{id}", new { expectedVersion = 1, name = " No-op ", description = (string?)null });
        using HttpResponseMessage date = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-date", new { expectedVersion = 1, targetDate = "2027-01-01", reason = "Same date" });
        using HttpResponseMessage status = await SendAsync(HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = 1, status = "Planned" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, date.StatusCode);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        await using ProjectsDbContext db = Context();
        Assert.Equal(1, (await db.EngagementMilestones.SingleAsync(x => x.Id == id)).Version);
        Assert.Equal(2, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Single(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task EveryMutationChecksAuthorityAndUnavailableIdentityFailsClosed()
    {
        Guid id = await AddMilestoneAsync("Protected");
        _authority.Mode = "missing-permission";
        var requests = new (HttpMethod Method, string Path, object Body)[]
        {
            (HttpMethod.Put, "contract-value", new { expectedVersion = 2 }),
            (HttpMethod.Post, "milestones", new { name = "No", targetDate = "2027-01-01" }),
            (HttpMethod.Put, $"milestones/{id}", new { expectedVersion = 1, name = "No" }),
            (HttpMethod.Post, $"milestones/{id}/change-date", new { expectedVersion = 1, targetDate = "2027-02-01", reason = "No" }),
            (HttpMethod.Post, $"milestones/{id}/change-status", new { expectedVersion = 1, status = "Missed" }),
            (HttpMethod.Post, "close", new { expectedVersion = 2, reason = "No" }),
            (HttpMethod.Post, "reopen", new { expectedVersion = 2, reason = "No" }),
        };
        foreach ((HttpMethod method, string path, object body) in requests)
        {
            using HttpResponseMessage response = await SendAsync(method, path, body);
            await AssertErrorAsync(response, HttpStatusCode.Forbidden);
        }

        _authority.Mode = "unavailable";
        using HttpResponseMessage unavailable = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = "No" });
        await AssertErrorAsync(unavailable, HttpStatusCode.ServiceUnavailable);
        await using ProjectsDbContext db = Context();
        Assert.Equal(2, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Single(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task FailureAfterCodeInvalidationRollsBackCloseAndAllowsRetry()
    {
        Guid codeId;
        await using (ProjectsDbContext db = Context())
        {
            var code = EngagementLinkCode.Create(_organizationId, _engagementId, _userId, DateTimeOffset.UtcNow, RandomNumberGenerator.GetBytes(32));
            codeId = code.Id;
            db.EngagementLinkCodes.Add(code);
            await db.SaveChangesAsync();
        }

        _factory.FailStaging = true;
        using HttpResponseMessage failed = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Close" });
        await AssertErrorAsync(failed, HttpStatusCode.InternalServerError);
        await using (ProjectsDbContext db = Context())
        {
            Assert.Equal("Active", (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Status);
            Assert.Equal("Outstanding", (await db.EngagementLinkCodes.SingleAsync(x => x.Id == codeId)).Status);
            Assert.Empty(await db.EngagementEvents.ToArrayAsync());
        }

        _factory.FailStaging = false;
        using HttpResponseMessage retry = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Close" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [PostgreSqlFact]
    public async Task IssuingAndRotatingCodesStoresOnlyVerifiersAndKeepsOneOutstanding()
    {
        using HttpResponseMessage first = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(first.Headers.CacheControl?.NoStore);
        using JsonDocument firstBody = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        string firstCode = firstBody.RootElement.GetProperty("data").GetProperty("code").GetString()!;
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Assert.True(firstCode.Length == 32 && firstCode.All(alphabet.Contains));
        using HttpResponseMessage second = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using JsonDocument secondBody = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());
        string secondCode = secondBody.RootElement.GetProperty("data").GetProperty("code").GetString()!;
        Assert.False(string.Equals(firstCode, secondCode, StringComparison.Ordinal));
        await using ProjectsDbContext db = Context();
        EngagementLinkCode[] rows = await db.EngagementLinkCodes.OrderBy(x => x.IssuedAt).ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.Equal("Invalidated", rows[0].Status);
        Assert.Equal("Outstanding", rows[1].Status);
        Assert.True(CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(firstCode)), rows[0].CodeVerifier));
        Assert.True(CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secondCode)), rows[1].CodeVerifier));
        Assert.Equal(TimeSpan.FromDays(7), rows[1].ExpiresAt - rows[1].IssuedAt);
        EngagementEvent[] history = await db.EngagementEvents.ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.All(history, entry => Assert.Equal("link-code.issued", entry.EventType));
        Assert.All(history, entry => Assert.False(entry.Details.Contains(firstCode, StringComparison.Ordinal) || entry.Details.Contains(secondCode, StringComparison.Ordinal)));
        using HttpResponseMessage detail = await ReadAsync(string.Empty);
        string detailBody = await detail.Content.ReadAsStringAsync();
        Assert.False(detailBody.Contains(firstCode, StringComparison.Ordinal) || detailBody.Contains(secondCode, StringComparison.Ordinal));
        string firstVerifier = Convert.ToHexString(rows[0].CodeVerifier);
        string secondVerifier = Convert.ToHexString(rows[1].CodeVerifier);
        string firstBase64 = Convert.ToBase64String(rows[0].CodeVerifier);
        string secondBase64 = Convert.ToBase64String(rows[1].CodeVerifier);
        bool leaked = _factory.Logs.Any(line => line.Contains(firstCode, StringComparison.Ordinal) || line.Contains(secondCode, StringComparison.Ordinal)
            || line.Contains(firstVerifier, StringComparison.OrdinalIgnoreCase) || line.Contains(secondVerifier, StringComparison.OrdinalIgnoreCase)
            || line.Contains(firstBase64, StringComparison.Ordinal) || line.Contains(secondBase64, StringComparison.Ordinal));
        Assert.False(leaked);
        using JsonDocument detailJson = JsonDocument.Parse(detailBody);
        Assert.True(detailJson.RootElement.GetProperty("data").GetProperty("hasOutstandingLinkCode").GetBoolean());
        Assert.Equal(rows[1].ExpiresAt, detailJson.RootElement.GetProperty("data").GetProperty("linkCodeExpiresAt").GetDateTimeOffset());
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VendorCanRemoveLinkWhileActiveOrClosed(bool closeFirst)
    {
        Guid client = Guid.NewGuid();
        await using (ProjectsDbContext db = Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE engagement."Engagements" SET "ClientOrganizationId" = {client}, "ClientLinkedAt" = now(), "ClientLinkedByUserId" = {_userId}
                WHERE "Id" = {_engagementId}
                """);
        }

        if (closeFirst)
        {
            using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Closed" });
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        }

        long version = closeFirst ? 2 : 1;
        using HttpResponseMessage stale = await SendAsync(HttpMethod.Post, "client-link/remove", new { expectedVersion = version + 1 });
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "Engagement.VersionConflict");
        using HttpResponseMessage remove = await SendAsync(HttpMethod.Post, "client-link/remove", new { expectedVersion = version, reason = "Requested" });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        using HttpResponseMessage missing = await SendAsync(HttpMethod.Post, "client-link/remove", new { expectedVersion = version + 1 });
        await AssertErrorAsync(missing, HttpStatusCode.Conflict, "Engagement.NotLinked");
        await using ProjectsDbContext verify = Context();
        Engagement actual = await verify.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Null(actual.ClientOrganizationId);
        Assert.Null(actual.ClientLinkedAt);
        Assert.Null(actual.ClientLinkedByUserId);
        Assert.Equal(closeFirst ? Engagement.ClosedStatus : Engagement.ActiveStatus, actual.Status);
        EngagementEvent entry = await verify.EngagementEvents.SingleAsync(x => x.EventType == "link.removed");
        Assert.Equal(_organizationId, entry.ActorOrganizationId);
        Assert.Equal("Requested", entry.Reason);
        using JsonDocument details = JsonDocument.Parse(entry.Details);
        Assert.Equal(client, details.RootElement.GetProperty("clientOrganizationId").GetGuid());
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientAdminCanListAndRemoveLinkWhileActiveOrClosed(bool closeFirst)
    {
        Guid clientOrganization = Guid.NewGuid();
        await using (ProjectsDbContext db = Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE engagement."Engagements" SET "ClientOrganizationId" = {clientOrganization}, "ClientLinkedAt" = now(), "ClientLinkedByUserId" = {_userId}
                WHERE "Id" = {_engagementId}
                """);
        }

        if (closeFirst)
        {
            using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Closed" });
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        }

        _authority.Mode = "client-admin";
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        string path = $"/api/v1/organizations/{clientOrganization:D}/engagement-links";
        using HttpResponseMessage list = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument listBody = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        JsonElement item = Assert.Single(listBody.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(_engagementId, item.GetProperty("id").GetGuid());
        Assert.Equal(3, item.EnumerateObject().Count());
        using HttpResponseMessage wrongClient = await client.PostAsJsonAsync($"/api/v1/organizations/{Guid.NewGuid():D}/engagement-links/{_engagementId:D}/remove", new { reason = "Requested" });
        await AssertErrorAsync(wrongClient, HttpStatusCode.NotFound);
        using HttpResponseMessage remove = await client.PostAsJsonAsync($"{path}/{_engagementId:D}/remove", new { reason = "Requested" });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        await using ProjectsDbContext verify = Context();
        Engagement actual = await verify.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Null(actual.ClientOrganizationId);
        Assert.Equal(closeFirst ? Engagement.ClosedStatus : Engagement.ActiveStatus, actual.Status);
        EngagementEvent entry = await verify.EngagementEvents.SingleAsync(x => x.EventType == "link.removed");
        Assert.Equal(clientOrganization, entry.ActorOrganizationId);
    }

    [PostgreSqlFact]
    public async Task ClientMemberCannotListOrRemoveLinks()
    {
        Guid clientOrganization = Guid.NewGuid();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        string path = $"/api/v1/organizations/{clientOrganization:D}/engagement-links";
        using HttpResponseMessage list = await client.GetAsync(path);
        await AssertErrorAsync(list, HttpStatusCode.Forbidden);
        using HttpResponseMessage remove = await client.PostAsJsonAsync($"{path}/{_engagementId:D}/remove", new { reason = "Requested" });
        await AssertErrorAsync(remove, HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task FailedCodeRotationRestoresPreviousOutstandingCode()
    {
        using HttpResponseMessage first = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        _factory.FailStaging = true;
        using HttpResponseMessage failed = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 });
        await AssertErrorAsync(failed, HttpStatusCode.InternalServerError);
        await using ProjectsDbContext db = Context();
        Assert.Equal(2, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Single(await db.EngagementLinkCodes.Where(x => x.Status == EngagementLinkCode.OutstandingStatus).ToArrayAsync());
        Assert.Single(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task ParallelCodeRotationHasOneWinnerAndOneOutstandingCode()
    {
        using HttpResponseMessage first = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 }),
            SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 }));
        try
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            await using ProjectsDbContext db = Context();
            Assert.Equal(1, await db.EngagementLinkCodes.CountAsync(x => x.Status == EngagementLinkCode.OutstandingStatus));
            Assert.Equal(2, await db.EngagementEvents.CountAsync());
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [PostgreSqlTheory]
    [InlineData("missing-permission", HttpStatusCode.Forbidden)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task CodeIssuanceRequiresCurrentManageAuthority(string mode, HttpStatusCode expected)
    {
        _authority.Mode = mode;
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 1 });
        await AssertErrorAsync(response, expected);
        await using ProjectsDbContext db = Context();
        Assert.Empty(await db.EngagementLinkCodes.ToArrayAsync());
        Assert.Empty(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task CodeIssuanceRejectsWrongWorkspaceAndStaleVersion()
    {
        using HttpResponseMessage foreign = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 1 }, Guid.NewGuid());
        await AssertErrorAsync(foreign, HttpStatusCode.NotFound);
        using HttpResponseMessage stale = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 });
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "Engagement.VersionConflict");
        await using ProjectsDbContext db = Context();
        Assert.Empty(await db.EngagementLinkCodes.ToArrayAsync());
        Assert.Empty(await db.EngagementEvents.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task CodeIssuanceFailsForClosedOrAlreadyLinkedEngagements()
    {
        using HttpResponseMessage closed = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Closed" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using HttpResponseMessage closedIssue = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 2 });
        await AssertErrorAsync(closedIssue, HttpStatusCode.Conflict, "Engagement.Closed");
        using HttpResponseMessage reopened = await SendAsync(HttpMethod.Post, "reopen", new { expectedVersion = 2, reason = "Resume" });
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        await using (ProjectsDbContext db = Context())
        {
            Guid client = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE engagement."Engagements" SET "ClientOrganizationId" = {client}, "ClientLinkedAt" = now(), "ClientLinkedByUserId" = {_userId}
                WHERE "OrganizationId" = {_organizationId} AND "Id" = {_engagementId}
                """);
        }

        using HttpResponseMessage linked = await SendAsync(HttpMethod.Post, "link-codes", new { expectedVersion = 3 });
        await AssertErrorAsync(linked, HttpStatusCode.Conflict, "Engagement.AlreadyLinked");
    }

    [PostgreSqlFact]
    public async Task WrittenValuesMilestonesAndAttributionAreVisibleThroughDetailAndHistory()
    {
        using HttpResponseMessage value = await SendAsync(HttpMethod.Put, "contract-value", new { expectedVersion = 1, contractAmount = 120000m, contractCurrency = "USD", plannedEffortPersonDays = 400m });
        Assert.Equal(HttpStatusCode.OK, value.StatusCode);
        Guid milestone = await AddMilestoneAsync("Delivery");
        using HttpResponseMessage shift = await SendAsync(HttpMethod.Post, $"milestones/{milestone}/change-date", new { expectedVersion = 1, targetDate = "2027-02-02", reason = "Customer request" });
        Assert.Equal(HttpStatusCode.OK, shift.StatusCode);
        using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 4, reason = "Complete" });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using HttpResponseMessage closedDetail = await ReadAsync(string.Empty);
        using HttpResponseMessage closedHistory = await ReadAsync("/history");
        Assert.Equal(HttpStatusCode.OK, closedDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closedHistory.StatusCode);
        using JsonDocument closedBody = await JsonDocument.ParseAsync(await closedDetail.Content.ReadAsStreamAsync());
        Assert.Equal("Closed", closedBody.RootElement.GetProperty("data").GetProperty("status").GetString());
        using HttpResponseMessage reopen = await SendAsync(HttpMethod.Post, "reopen", new { expectedVersion = 5, reason = "Continue" });
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);

        using HttpResponseMessage detail = await ReadAsync(string.Empty);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using JsonDocument detailBody = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        JsonElement data = detailBody.RootElement.GetProperty("data");
        Assert.Equal(120000m, data.GetProperty("contractAmount").GetDecimal());
        Assert.Equal("USD", data.GetProperty("contractCurrency").GetString());
        Assert.Equal(400m, data.GetProperty("plannedEffortPersonDays").GetDecimal());
        JsonElement milestoneView = data.GetProperty("milestones")[0];
        Assert.Equal("2027-01-01", milestoneView.GetProperty("originalTargetDate").GetString());
        Assert.Equal("2027-02-02", milestoneView.GetProperty("currentTargetDate").GetString());

        using HttpResponseMessage history = await ReadAsync("/history");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using JsonDocument historyBody = await JsonDocument.ParseAsync(await history.Content.ReadAsStreamAsync());
        JsonElement[] entries = historyBody.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(5, entries.Length);
        JsonElement dateEntry = entries.Single(x => x.GetProperty("eventType").GetString() == "milestone.date-changed");
        Assert.Equal("Customer request", dateEntry.GetProperty("reason").GetString());
        Assert.Equal(_userId, dateEntry.GetProperty("actorUserId").GetGuid());
        Assert.Equal(TimeSpan.Zero, dateEntry.GetProperty("occurredAt").GetDateTimeOffset().Offset);
        Assert.Equal("2027-01-01", dateEntry.GetProperty("details").GetProperty("originalTargetDate").GetString());
        Assert.Contains(entries, x => x.GetProperty("eventType").GetString() == "engagement.closed");
        Assert.Contains(entries, x => x.GetProperty("eventType").GetString() == "engagement.reopened" && x.GetProperty("actorUserId").GetGuid() == _userId);
    }

    [PostgreSqlFact]
    public async Task ConfiguredFrontendCanPreflightPutMutations()
    {
        using HttpClient client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, $"/api/v1/organizations/{_organizationId:D}/engagements/{_engagementId:D}/contract-value");
        request.Headers.TryAddWithoutValidation("Origin", "https://app.example.test");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "PUT");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");
        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("PUT", response.Headers.GetValues("Access-Control-Allow-Methods").SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries)));
    }

    [PostgreSqlFact]
    public async Task ContactsReplaceDesignationAndKeepOneCombinedHistoryEntry()
    {
        Guid first = await AddContactAsync(" First Person ", " FIRST@Example.com ", "ClientProductOwner");
        Guid second = await AddContactAsync("Second Person", "second@example.com", "ClientProductOwner");
        await using (ProjectsDbContext db = Context())
        {
            EngagementContact[] contacts = await db.EngagementContacts.OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(2, contacts.Length);
            Assert.Equal("first@example.com", contacts.Single(x => x.Id == first).Email);
            Assert.Equal("Other", contacts.Single(x => x.Id == first).ContactRole);
            Assert.Equal(2, contacts.Single(x => x.Id == first).Version);
            Assert.Equal("ClientProductOwner", contacts.Single(x => x.Id == second).ContactRole);
            EngagementEvent[] history = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
            Assert.Equal(2, history.Length);
            Assert.Equal("contact.designated-client-po", history[1].EventType);
            using JsonDocument details = JsonDocument.Parse(history[1].Details);
            Assert.Equal(second, details.RootElement.GetProperty("new").GetProperty("id").GetGuid());
            JsonElement demoted = details.RootElement.GetProperty("demoted");
            Assert.Equal(first, demoted.GetProperty("contactId").GetGuid());
            Assert.Equal("ClientProductOwner", demoted.GetProperty("previousRole").GetString());
            Assert.Equal("Other", demoted.GetProperty("newRole").GetString());
        }

        using HttpResponseMessage detail = await ReadAsync(string.Empty);
        using JsonDocument body = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        Assert.Equal(2, body.RootElement.GetProperty("data").GetProperty("contacts").GetArrayLength());
        using HttpResponseMessage historyResponse = await ReadAsync("/history");
        using JsonDocument historyBody = await JsonDocument.ParseAsync(await historyResponse.Content.ReadAsStreamAsync());
        JsonElement replacement = historyBody.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("eventType").GetString() == "contact.designated-client-po");
        Assert.Equal(first, replacement.GetProperty("details").GetProperty("demoted").GetProperty("contactId").GetGuid());
        Assert.Equal(second, replacement.GetProperty("details").GetProperty("new").GetProperty("id").GetGuid());
        Assert.DoesNotContain(_factory.Logs, x => x.Contains("first@example.com", StringComparison.OrdinalIgnoreCase) || x.Contains("Second Person", StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task ContactEditRemovalAndNoOpRespectVersionsAndHistory()
    {
        Guid first = await AddContactAsync("First", "first@example.com", "ClientProductOwner");
        Guid second = await AddContactAsync("Second", "second@example.com", "Sponsor");
        using HttpResponseMessage edit = await SendAsync(HttpMethod.Put, $"contacts/{second:D}", new { expectedVersion = 1, name = "Second", email = "second@example.com", contactRole = "ClientProductOwner" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using HttpResponseMessage staleDemoted = await SendAsync(HttpMethod.Put, $"contacts/{first:D}", new { expectedVersion = 1, name = "First", email = "first@example.com", contactRole = "Other" });
        await AssertErrorAsync(staleDemoted, HttpStatusCode.Conflict, "Contact.VersionConflict");
        using HttpResponseMessage unchanged = await SendAsync(HttpMethod.Put, $"contacts/{second:D}", new { expectedVersion = 2, name = " Second ", email = "SECOND@example.com", contactRole = "ClientProductOwner" });
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        using HttpResponseMessage stale = await SendAsync(HttpMethod.Post, $"contacts/{second:D}/remove", new { expectedVersion = 1, reason = "Left" });
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, "Contact.VersionConflict");
        using HttpResponseMessage remove = await SendAsync(HttpMethod.Post, $"contacts/{second:D}/remove", new { expectedVersion = 2, reason = "Left" });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        using HttpResponseMessage missing = await SendAsync(HttpMethod.Post, $"contacts/{second:D}/remove", new { expectedVersion = 3 });
        await AssertErrorAsync(missing, HttpStatusCode.NotFound);
        await using (ProjectsDbContext db = Context())
        {
            EngagementContact old = await db.EngagementContacts.SingleAsync(x => x.Id == first);
            Assert.Equal("Other", old.ContactRole);
            Assert.NotNull((await db.EngagementContacts.SingleAsync(x => x.Id == second)).RemovedAt);
            EngagementEvent[] events = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
            Assert.Equal(4, events.Length);
            Assert.Equal("contact.designated-client-po", events[2].EventType);
            Assert.Equal("contact.removed", events[3].EventType);
            Assert.Equal("Left", events[3].Reason);
            Assert.Equal(5, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        }

        using HttpResponseMessage detail = await ReadAsync(string.Empty);
        using JsonDocument body = await JsonDocument.ParseAsync(await detail.Content.ReadAsStreamAsync());
        Assert.Single(body.RootElement.GetProperty("data").GetProperty("contacts").EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task ParallelContactDesignationsSerializeAndRollbackRestoresPreviousDesignation()
    {
        Guid original = await AddContactAsync("Original", "original@example.com", "ClientProductOwner");
        _factory.FailStaging = true;
        using HttpResponseMessage failure = await SendAsync(HttpMethod.Post, "contacts", new { name = "Failed", email = "failed@example.com", contactRole = "ClientProductOwner" });
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        await using (ProjectsDbContext db = Context())
        {
            Assert.Equal("ClientProductOwner", (await db.EngagementContacts.SingleAsync(x => x.Id == original)).ContactRole);
            Assert.Single(await db.EngagementEvents.ToArrayAsync());
        }

        _factory.FailStaging = false;
        HttpResponseMessage[] results = await Task.WhenAll(
            SendAsync(HttpMethod.Post, "contacts", new { name = "One", email = "one@example.com", contactRole = "ClientProductOwner" }),
            SendAsync(HttpMethod.Post, "contacts", new { name = "Two", email = "two@example.com", contactRole = "ClientProductOwner" }));
        try
        {
            Assert.All(results, result => Assert.Equal(HttpStatusCode.Created, result.StatusCode));
            await using ProjectsDbContext db = Context();
            Assert.Equal(1, await db.EngagementContacts.CountAsync(x => x.ContactRole == "ClientProductOwner" && x.RemovedAt == null));
            Assert.Equal(3, await db.EngagementEvents.CountAsync());
        }
        finally
        {
            foreach (HttpResponseMessage result in results)
            {
                result.Dispose();
            }
        }
    }

    [PostgreSqlFact]
    public async Task ContactRequestsRejectInvalidUnauthorizedCrossTenantAndClosedChanges()
    {
        using HttpResponseMessage invalid = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "not-an-email", contactRole = "Other" });
        await AssertErrorAsync(invalid, HttpStatusCode.BadRequest);
        using HttpResponseMessage badRole = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "name@example.com", contactRole = "Unknown" });
        await AssertErrorAsync(badRole, HttpStatusCode.BadRequest);
        Guid contact = await AddContactAsync("Name", "name@example.com", "Other");
        _authority.Mode = "missing-permission";
        using HttpResponseMessage denied = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "name@example.com", contactRole = "Other" });
        await AssertErrorAsync(denied, HttpStatusCode.Forbidden);
        _authority.Mode = "unavailable";
        using HttpResponseMessage unavailable = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "name@example.com", contactRole = "Other" });
        await AssertErrorAsync(unavailable, HttpStatusCode.ServiceUnavailable);
        _authority.Mode = "allowed";
        using HttpResponseMessage otherTenant = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "name@example.com", contactRole = "Other" }, Guid.NewGuid());
        await AssertErrorAsync(otherTenant, HttpStatusCode.NotFound);
        using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 2, reason = "Closed" });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using HttpResponseMessage addClosed = await SendAsync(HttpMethod.Post, "contacts", new { name = "Name", email = "name@example.com", contactRole = "Other" });
        await AssertErrorAsync(addClosed, HttpStatusCode.Conflict, "Engagement.Closed");
        using HttpResponseMessage editClosed = await SendAsync(HttpMethod.Put, $"contacts/{contact:D}", new { expectedVersion = 1, name = "Name", email = "name@example.com", contactRole = "Other" });
        await AssertErrorAsync(editClosed, HttpStatusCode.Conflict, "Engagement.Closed");
        using HttpResponseMessage removeClosed = await SendAsync(HttpMethod.Post, $"contacts/{contact:D}/remove", new { expectedVersion = 1 });
        await AssertErrorAsync(removeClosed, HttpStatusCode.Conflict, "Engagement.Closed");
    }

    [PostgreSqlFact]
    public async Task ContactDesignationGrantsNoEngagementAccess()
    {
        await AddContactAsync("Client Person", "client@example.com", "ClientProductOwner");
        _authority.Mode = "missing-permission";
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid()));
        using HttpResponseMessage detail = await client.GetAsync($"/api/v1/organizations/{_organizationId:D}/engagements/{_engagementId:D}");
        await AssertErrorAsync(detail, HttpStatusCode.NotFound);
        using HttpResponseMessage history = await client.GetAsync($"/api/v1/organizations/{_organizationId:D}/engagements/{_engagementId:D}/history");
        await AssertErrorAsync(history, HttpStatusCode.NotFound);
    }

    [PostgreSqlFact]
    public async Task DetailUpdatesRecordOnlyChangedFieldsAndNormalizeNoOps()
    {
        using HttpResponseMessage changed = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(reference: " REF-2 ", legalName: " Client Ltd ", reason: "Amendment"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using HttpResponseMessage noOp = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(version: 2, reference: "REF-2", legalName: "Client Ltd"));
        Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
        using JsonDocument noOpBody = await JsonDocument.ParseAsync(await noOp.Content.ReadAsStreamAsync());
        Assert.Equal(2, noOpBody.RootElement.GetProperty("data").GetProperty("version").GetInt64());
        using HttpResponseMessage cleared = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(version: 2, reference: "REF-2"));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        await using ProjectsDbContext db = Context();
        Engagement actual = await db.Engagements.SingleAsync(x => x.Id == _engagementId);
        Assert.Null(actual.ClientLegalName);
        Assert.Equal(3, actual.Version);
        EngagementEvent[] history = await db.EngagementEvents.OrderBy(x => x.EngagementVersion).ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.All(history, entry => Assert.Equal("engagement.details-changed", entry.EventType));
        Assert.Equal(_userId, history[0].ActorUserId);
        Assert.Equal(_organizationId, history[0].ActorOrganizationId);
        Assert.Equal(TimeSpan.Zero, history[0].OccurredAt.Offset);
        Assert.Equal("Amendment", history[0].Reason);
        using JsonDocument eventDetails = JsonDocument.Parse(history[0].Details);
        JsonElement changes = eventDetails.RootElement.GetProperty("changed");
        Assert.Equal(2, changes.EnumerateObject().Count());
        Assert.Equal("REF-1", changes.GetProperty("agreementReference").GetProperty("previous").GetString());
        Assert.Equal("REF-2", changes.GetProperty("agreementReference").GetProperty("new").GetString());
        using HttpResponseMessage read = await ReadAsync("/history");
        using JsonDocument readBody = await JsonDocument.ParseAsync(await read.Content.ReadAsStreamAsync());
        JsonElement[] entries = readBody.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
        JsonElement clearEntry = entries.Single(x => x.GetProperty("engagementVersion").GetInt64() == 3);
        Assert.Equal(JsonValueKind.Null, clearEntry.GetProperty("details").GetProperty("changed").GetProperty("clientLegalName").GetProperty("new").ValueKind);
    }

    [PostgreSqlTheory]
    [InlineData("reference-reason")]
    [InlineData("date-reason")]
    [InlineData("country")]
    [InlineData("url")]
    [InlineData("long-name")]
    [InlineData("required")]
    [InlineData("version")]
    [InlineData("unknown")]
    public async Task DetailUpdatesRejectInvalidInputsWithoutChanges(string scenario)
    {
        Dictionary<string, object?> body = DetailsBody();
        switch (scenario)
        {
            case "reference-reason": body["agreementReference"] = "Different"; break;
            case "date-reason": body["agreementSignedOn"] = "2027-01-01"; break;
            case "country": body["clientCountryCode"] = "ZZ"; break;
            case "url": body["documentUrl"] = "http://example.test"; break;
            case "long-name": body["clientDisplayName"] = new string('X', 201); break;
            case "required": body.Remove("clientDisplayName"); break;
            case "version": body.Remove("expectedVersion"); break;
            case "unknown": body["organizationId"] = _organizationId; break;
        }

        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, string.Empty, body);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest);
        await using ProjectsDbContext db = Context();
        Assert.Equal(1, (await db.Engagements.SingleAsync(x => x.Id == _engagementId)).Version);
        Assert.Empty(await db.EngagementEvents.Where(x => x.OrganizationId == _organizationId).ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task DetailUpdatesWarnForOtherActiveReferencesButAllowDuplicates()
    {
        var other = Engagement.Create(_organizationId, _userId, DateTimeOffset.UtcNow, "Other", "MATCH", new DateOnly(2026, 1, 1));
        await using (ProjectsDbContext db = Context())
        {
            db.Engagements.Add(other);
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage duplicate = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(reference: " match ", reason: "Correct"));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        using JsonDocument duplicateBody = await JsonDocument.ParseAsync(await duplicate.Content.ReadAsStreamAsync());
        Assert.True(duplicateBody.RootElement.GetProperty("data").GetProperty("duplicateAgreementReference").GetBoolean());
        using HttpResponseMessage noOp = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(version: 2, reference: "match"));
        using JsonDocument noOpBody = await JsonDocument.ParseAsync(await noOp.Content.ReadAsStreamAsync());
        Assert.False(noOpBody.RootElement.GetProperty("data").GetProperty("duplicateAgreementReference").GetBoolean());
        await using (ProjectsDbContext db = Context())
        {
            Engagement current = await db.Engagements.SingleAsync(x => x.Id == other.Id);
            current.Close(_userId, "Complete", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage closedOnly = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(version: 2, reference: "MATCH", reason: "Case correction"));
        Assert.Equal(HttpStatusCode.OK, closedOnly.StatusCode);
        using JsonDocument closedBody = await JsonDocument.ParseAsync(await closedOnly.Content.ReadAsStreamAsync());
        Assert.False(closedBody.RootElement.GetProperty("data").GetProperty("duplicateAgreementReference").GetBoolean());
        Assert.Equal(3, closedBody.RootElement.GetProperty("data").GetProperty("version").GetInt64());
    }

    [PostgreSqlFact]
    public async Task DetailUpdatesHaveOneConcurrentWinnerAndRollbackOnEventFailure()
    {
        _factory.FailStaging = true;
        using HttpResponseMessage failed = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(reference: "Fail", reason: "Correct"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        _factory.FailStaging = false;
        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(HttpMethod.Put, string.Empty, DetailsBody(name: "One")),
            SendAsync(HttpMethod.Put, string.Empty, DetailsBody(name: "Two")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using ProjectsDbContext db = Context();
            Engagement engagement = await db.Engagements.SingleAsync(x => x.Id == _engagementId);
            Assert.Equal("REF-1", engagement.AgreementReference);
            Assert.Equal(2, engagement.Version);
            Assert.Single(await db.EngagementEvents.Where(x => x.OrganizationId == _organizationId).ToArrayAsync());
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
    public async Task DetailUpdatesDenyUnauthorizedCrossTenantAndClosedRequests()
    {
        _authority.Mode = "missing-permission";
        using HttpResponseMessage denied = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(name: "Changed"));
        await AssertErrorAsync(denied, HttpStatusCode.Forbidden);
        _authority.Mode = "unavailable";
        using HttpResponseMessage unavailable = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(name: "Changed"));
        await AssertErrorAsync(unavailable, HttpStatusCode.ServiceUnavailable);
        _authority.Mode = "allowed";
        using HttpResponseMessage crossTenant = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(name: "Changed"), Guid.NewGuid());
        await AssertErrorAsync(crossTenant, HttpStatusCode.NotFound);
        using HttpResponseMessage close = await SendAsync(HttpMethod.Post, "close", new { expectedVersion = 1, reason = "Closed" });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using HttpResponseMessage closed = await SendAsync(HttpMethod.Put, string.Empty, DetailsBody(version: 2, name: "Changed"));
        await AssertErrorAsync(closed, HttpStatusCode.Conflict, "Engagement.Closed");
    }

    [PostgreSqlFact]
    public async Task DetailUpdateAndCreationShareReferenceSerialization()
    {
        using HttpClient creator = _factory.CreateClient();
        creator.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        creator.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        HttpResponseMessage[] responses = await Task.WhenAll(
            creator.PostAsJsonAsync($"/api/v1/organizations/{_organizationId:D}/engagements", new { clientDisplayName = "Other", agreementReference = "Shared", agreementSignedOn = "2026-01-01" }),
            SendAsync(HttpMethod.Put, string.Empty, DetailsBody(reference: "shared", reason: "Correct")));
        try
        {
            Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode);
            Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
            using JsonDocument created = await JsonDocument.ParseAsync(await responses[0].Content.ReadAsStreamAsync());
            using JsonDocument updated = await JsonDocument.ParseAsync(await responses[1].Content.ReadAsStreamAsync());
            bool creationWarning = created.RootElement.GetProperty("data").GetProperty("duplicateAgreementReference").GetBoolean();
            bool updateWarning = updated.RootElement.GetProperty("data").GetProperty("duplicateAgreementReference").GetBoolean();
            Assert.True(creationWarning ^ updateWarning);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static Dictionary<string, object?> DetailsBody(long version = 1, string reference = "REF-1", string? legalName = null, string? reason = null, string? name = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["expectedVersion"] = version,
            ["clientDisplayName"] = "Client",
            ["agreementReference"] = reference,
            ["agreementSignedOn"] = "2026-01-01",
            ["reason"] = reason,
            ["engagementName"] = name,
        };
        if (legalName is not null)
        {
            body["clientLegalName"] = legalName;
        }

        return body;
    }

    private async Task<Guid> AddContactAsync(string name, string email, string role)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "contacts", new { name, email, contactRole = role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> AddMilestoneAsync(string name)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "milestones", new { name, targetDate = "2027-01-01" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement data = body.RootElement.GetProperty("data");
        Assert.Equal("Planned", data.GetProperty("status").GetString());
        Assert.Equal(data.GetProperty("originalTargetDate").GetString(), data.GetProperty("currentTargetDate").GetString());
        return data.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, Guid? organizationId = null)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var request = new HttpRequestMessage(method, $"/api/v1/organizations/{organizationId ?? _organizationId:D}/engagements/{_engagementId:D}/{path}") { Content = JsonContent.Create(body) };
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendRawAsync(string path, string json)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/organizations/{_organizationId:D}/engagements/{_engagementId:D}/{path}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> ReadAsync(string suffix)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        return await client.GetAsync($"/api/v1/organizations/{_organizationId:D}/engagements/{_engagementId:D}{suffix}");
    }

    private static ProjectsDbContext Context() => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(PostgreSqlFactAttribute.ConnectionString).Options);

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using JsonDocument envelope = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (code is not null)
        {
            Assert.Equal(code, envelope.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        Assert.False(string.IsNullOrWhiteSpace(envelope.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
    }

    private string Token(Guid? userId = null)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = "DeliveryPilot.Identity", aud = "DeliveryPilot", sub = userId ?? _userId, sid = Guid.NewGuid(), jti = Guid.NewGuid(), exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        string message = header + "." + payload;
        string signingKey = TestConfiguration.Host()["Jwt:SecretKey"] ?? throw new InvalidOperationException("Test signing key is missing.");
        return message + "." + Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(message)));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class MutationFactory(AuthorityClient authority) : WebApplicationFactory<Program>
    {
        internal bool FailStaging { get; set; }
        internal ConcurrentQueue<string> Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach ((string key, string? value) in TestConfiguration.Host())
            {
                builder.UseSetting(key, value);
            }

            builder.UseSetting("ConnectionStrings:ApplicationDb", PostgreSqlFactAttribute.ConnectionString);
            builder.UseSetting("Cors:AllowedMethods:1", "PUT");
            builder.UseSetting("Cors:AllowedHeaders:1", "Content-Type");
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuthorityClient>();
                services.AddSingleton<IIdentityAuthorityClient>(authority);
                services.RemoveAll<IEngagementMutationStore>();
                services.AddScoped<IEngagementMutationStore>(provider => new FailingStore(new EngagementMutationStore(provider.GetRequiredService<ProjectsDbContext>()), () => FailStaging));
            });
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

    private sealed class FailingStore(IEngagementMutationStore inner, Func<bool> shouldFail) : IEngagementMutationStore
    {
        public Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken) => inner.LockReferenceAsync(organizationId, reference, cancellationToken);
        public Task<bool> HasOtherActiveReferenceAsync(Guid organizationId, string reference, Guid excludeEngagementId, CancellationToken cancellationToken) => inner.HasOtherActiveReferenceAsync(organizationId, reference, excludeEngagementId, cancellationToken);
        public Task<Engagement?> LockAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken) => inner.LockAsync(organizationId, engagementId, cancellationToken);
        public Task<EngagementMilestone?> FindMilestoneAsync(Guid organizationId, Guid engagementId, Guid milestoneId, CancellationToken cancellationToken) => inner.FindMilestoneAsync(organizationId, engagementId, milestoneId, cancellationToken);
        public Task<EngagementContact?> FindContactAsync(Guid organizationId, Guid engagementId, Guid contactId, CancellationToken cancellationToken) => inner.FindContactAsync(organizationId, engagementId, contactId, cancellationToken);
        public Task<Guid?> DemoteClientProductOwnerAsync(Guid organizationId, Guid engagementId, Guid exceptContactId, DateTimeOffset occurredAt, CancellationToken cancellationToken) => inner.DemoteClientProductOwnerAsync(organizationId, engagementId, exceptContactId, occurredAt, cancellationToken);
        public Task<EngagementLinkCode?> FindOutstandingCodeAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken) => inner.FindOutstandingCodeAsync(organizationId, engagementId, cancellationToken);
        public Task<Guid?> InvalidateOutstandingCodeAsync(Guid organizationId, Guid engagementId, DateTimeOffset occurredAt, CancellationToken cancellationToken) => inner.InvalidateOutstandingCodeAsync(organizationId, engagementId, occurredAt, cancellationToken);
        public void AddLinkCode(EngagementLinkCode code) => inner.AddLinkCode(code);
        public void AddMilestone(EngagementMilestone milestone) => inner.AddMilestone(milestone);
        public void AddContact(EngagementContact contact) => inner.AddContact(contact);

        public void AddEvent(EngagementEvent entry)
        {
            inner.AddEvent(entry);
            if (shouldFail())
            {
                throw new InvalidOperationException("Simulated failure after event staging.");
            }
        }
    }

    private sealed class AuthorityClient : IIdentityAuthorityClient
    {
        private int _checks;
        internal string Mode { get; set; } = "allowed";

        public Task<IntegrationResult<DeliveryPilot.Identity.Contracts.UserLookupResult>> LookupUsersAsync(DeliveryPilot.Identity.Contracts.UserLookupRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Mutations must not look up display names.");

        public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
            => Task.FromResult(IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK));

        public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(OrganizationAuthorityCheckRequest request, CancellationToken cancellationToken)
        {
            int checks = Interlocked.Increment(ref _checks);
            OrganizationAuthorityCheckResult result = Mode == "denied" || (Mode == "revoked-after-lock" && checks > 1)
                ? OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow)
                : new OrganizationAuthorityCheckResult(AuthorityDecision.Allowed, DateTimeOffset.UtcNow, request.OrganizationId, 1, Guid.NewGuid(), 1, false, Mode == "client-admin", Mode == "missing-permission" ? [] : [WorkspacePermissions.EngagementsManage, WorkspacePermissions.EngagementsCreate]);
            return Task.FromResult(Mode == "unavailable" ? FakeIdentityAuthorityClient.Unavailable<OrganizationAuthorityCheckResult>() : IntegrationResult<OrganizationAuthorityCheckResult>.Success(result, HttpStatusCode.OK));
        }
    }
}
