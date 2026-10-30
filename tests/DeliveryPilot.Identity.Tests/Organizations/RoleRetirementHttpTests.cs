using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static DeliveryPilot.Identity.Tests.Organizations.InvitationManagementHttpTests;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class RoleRetirementHttpTests
{
    [PostgreSqlFact]
    public async Task MissingFactsAndKestrelBodyLimitFailSafelyWithoutMutation()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using (WebApplication unavailable = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new UnavailableStore()))))
        {
            using HttpClient owner = Fixture.Client(unavailable, fixture.Owner.Id);
            await ErrorAsync(await RetireAsync(owner, fixture), HttpStatusCode.ServiceUnavailable);
        }

        await using WebApplication kestrel = await fixture.StartAppAsync(useKestrel: true);
        using var client = new HttpClient { BaseAddress = new Uri(kestrel.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Test-User", fixture.Owner.Id.ToString());
        await ErrorAsync(await RetireAsync(client, fixture, reason: new string('a', 17000)), HttpStatusCode.RequestEntityTooLarge);
        await using IdentityDbContext db = fixture.NewDb();
        Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.AggregateId == fixture.Role.Id));
        Assert.Null((await db.OrganizationRoles.SingleAsync(item => item.Id == fixture.Role.Id)).RetiredAt);
    }

    [PostgreSqlFact]
    public async Task RetirementRequiresClearedReferencesAndPreservesHistoryAndRejectsNewUse()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        await using IdentityDbContext db = fixture.NewDb();
        Guid member = await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Ordinary.Id).Select(item => item.Id).SingleAsync();
        string grant = $"/api/v1/organizations/{fixture.Organization.Id}/members/{member}/roles/{fixture.Role.Id}";
        await DataAsync(await owner.PostAsJsonAsync(grant + "/grant", new { expectedMembershipVersion = 1 }), HttpStatusCode.OK);
        Assert.Equal("Organization.RoleInUse", await ErrorAsync(await RetireAsync(owner, fixture), HttpStatusCode.Conflict));
        await DataAsync(await owner.PostAsJsonAsync(grant + "/revoke", new { expectedMembershipVersion = 2 }), HttpStatusCode.OK);
        JsonElement invitation = await DataAsync(await IssueAsync(owner, fixture, fixture.Role.Id), HttpStatusCode.Created);
        await ErrorAsync(await RetireAsync(owner, fixture), HttpStatusCode.Conflict);
        await DataAsync(await owner.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/invitations/{invitation.GetProperty("id").GetGuid()}/cancel", new { expectedVersion = 1, reason = "Offer withdrawn" }), HttpStatusCode.OK);
        JsonElement retired = await DataAsync(await RetireAsync(owner, fixture), HttpStatusCode.OK);
        Assert.Equal(2, retired.GetProperty("version").GetInt64());
        Assert.Equal(1, retired.GetProperty("permissionSetVersion").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, retired.GetProperty("retiredAt").ValueKind);
        Assert.Equal(1, await db.IdentityAuditEvents.CountAsync(item => item.AggregateId == fixture.Role.Id && item.EventType == IdentityAuditSchemaCatalog.RoleRetired));
        Assert.Equal("No longer needed", await db.IdentityAuditEvents.Where(item => item.AggregateId == fixture.Role.Id).Select(item => item.Reason).SingleAsync());
        Assert.True(await db.OrganizationRoleAssignments.AnyAsync(item => item.RoleId == fixture.Role.Id && item.RevokedAt != null));
        Assert.True(await db.OrganizationInvitationRoleOffers.AnyAsync(item => item.RoleId == fixture.Role.Id));
        await ErrorAsync(await owner.PostAsJsonAsync(grant + "/grant", new { expectedMembershipVersion = 3 }), HttpStatusCode.Forbidden);
        await ErrorAsync(await IssueAsync(owner, fixture, fixture.Role.Id, "other@example.test"), HttpStatusCode.Forbidden);
        await ErrorAsync(await RetireAsync(owner, fixture, version: 2), HttpStatusCode.Conflict);
    }

    [PostgreSqlFact]
    public async Task ExpiredStoredPendingOfferBlocksUntilLifecycleClosesIt()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        await DataAsync(await IssueAsync(owner, fixture, fixture.Role.Id), HttpStatusCode.Created);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        await ErrorAsync(await RetireAsync(owner, fixture), HttpStatusCode.Conflict);
        await DataAsync(await IssueAsync(owner, fixture, fixture.Seed.MemberRole.Id), HttpStatusCode.Created);
        await DataAsync(await RetireAsync(owner, fixture), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task ValidationAuthorizationScopeAndVersionsHaveSafeNoStoreErrors()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient ordinary = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        await ErrorAsync(await RetireAsync(anonymous, fixture), HttpStatusCode.Unauthorized);
        await ErrorAsync(await RetireAsync(ordinary, fixture), HttpStatusCode.Forbidden);
        await ErrorAsync(await RetireAsync(owner, fixture, fixture.ForeignRole.Id), HttpStatusCode.NotFound);
        await ErrorAsync(await RetireAsync(ordinary, fixture, Guid.NewGuid()), HttpStatusCode.Forbidden);
        await ErrorAsync(await RetireAsync(owner, fixture, fixture.Seed.OwnerRole.Id), HttpStatusCode.Conflict);
        await ErrorAsync(await RetireAsync(owner, fixture, fixture.Seed.AdminRole.Id), HttpStatusCode.Conflict);
        await ErrorAsync(await RetireAsync(owner, fixture, fixture.Seed.MemberRole.Id), HttpStatusCode.Conflict);
        Assert.Equal("Organization.RoleStale", await ErrorAsync(await RetireAsync(owner, fixture, version: 2), HttpStatusCode.Conflict));
        foreach (string body in new[] { "{}", "{\"expectedVersion\":0,\"reason\":\"Required\"}", "{\"expectedVersion\":1,\"reason\":\" \"}", "{\"expectedVersion\":1,\"reason\":\"private@example.test\"}", "{\"expectedVersion\":1,\"reason\":\"Valid\",\"actor\":\"forged\"}" })
        {
            await ErrorAsync(await owner.PostAsync(Route(fixture), new StringContent(body, Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        }

        await ErrorAsync(await RetireAsync(owner, fixture, reason: new string('a', 1001)), HttpStatusCode.BadRequest);
        using HttpClient delegated = Fixture.Client(app, fixture.Delegate.Id);
        await DataAsync(await RetireAsync(delegated, fixture), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task AuditAndCommitFailureRollBackRoleAndAuditTogether()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        foreach (bool commit in new[] { false, true })
        {
            await using WebApplication app = await fixture.StartAppAsync(commit ? null : "audit", configure: builder =>
            {
                if (commit)
                {
                    builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit()));
                }
            });
            using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
            await ErrorAsync(await RetireAsync(owner, fixture), HttpStatusCode.InternalServerError);
            await using IdentityDbContext db = fixture.NewDb();
            OrganizationRole role = await db.OrganizationRoles.SingleAsync(item => item.Id == fixture.Role.Id);
            Assert.Null(role.RetiredAt);
            Assert.Equal(1, role.Version);
            Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.AggregateId == role.Id));
        }
    }

    [PostgreSqlFact]
    public async Task RetirementAndAssignmentIssueOrReissueSerializeInBothOrders()
    {
        foreach (string operation in new[] { "grant", "issue", "reissue" })
        {
            foreach (bool retirementFirst in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using WebApplication normal = await fixture.StartAppAsync();
                using HttpClient other = Fixture.Client(normal, fixture.Owner.Id);
                await using IdentityDbContext db = fixture.NewDb();
                Guid member = await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Ordinary.Id).Select(item => item.Id).SingleAsync();
                Guid invitation = Guid.Empty;
                if (operation == "reissue")
                {
                    JsonElement issued = await DataAsync(await IssueAsync(other, fixture, fixture.Seed.MemberRole.Id), HttpStatusCode.Created);
                    invitation = issued.GetProperty("id").GetGuid();
                    fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
                }

                Task<HttpResponseMessage> ReferenceAsync(HttpClient client) => operation switch
                {
                    "grant" => client.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/members/{member}/roles/{fixture.Role.Id}/grant", new { expectedMembershipVersion = 1 }),
                    "issue" => IssueAsync(client, fixture, fixture.Role.Id),
                    _ => client.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/invitations/{invitation}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Updated offer" }),
                };
                var gate = new CommitGate();
                await using WebApplication gated = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
                using HttpClient first = Fixture.Client(gated, fixture.Owner.Id);
                Task<HttpResponseMessage> firstRequest = retirementFirst ? RetireAsync(first, fixture) : ReferenceAsync(first);
                Task<HttpResponseMessage>? secondRequest = null;
                try
                {
                    await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    secondRequest = retirementFirst ? ReferenceAsync(other) : RetireAsync(other, fixture);
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (!await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT FROM pg_stat_activity WHERE {gate.Pid} = ANY(pg_blocking_pids(pid))) AS \"Value\"").SingleAsync(deadline.Token))
                    {
                        await Task.Delay(20, deadline.Token);
                    }
                }
                finally
                {
                    gate.Release.TrySetResult();
                }

                await DataAsync(await firstRequest, !retirementFirst && operation == "issue" ? HttpStatusCode.Created : HttpStatusCode.OK);
                await ErrorAsync(await secondRequest!, retirementFirst ? HttpStatusCode.Forbidden : HttpStatusCode.Conflict);
                Assert.Equal(retirementFirst, await db.OrganizationRoles.Where(item => item.Id == fixture.Role.Id).Select(item => item.RetiredAt != null).SingleAsync());
                Assert.Equal(retirementFirst ? 1 : 0, await db.IdentityAuditEvents.CountAsync(item => item.AggregateId == fixture.Role.Id && item.EventType == IdentityAuditSchemaCatalog.RoleRetired));
            }
        }
    }

    private static string Route(Fixture fixture) => $"/api/v1/organizations/{fixture.Organization.Id}/roles/{fixture.Role.Id}/retire";
    private static Task<HttpResponseMessage> RetireAsync(HttpClient client, Fixture fixture, Guid? role = null, long version = 1, string reason = "No longer needed")
        => client.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/roles/{role ?? fixture.Role.Id}/retire", new { expectedVersion = version, reason });
    private static Task<HttpResponseMessage> IssueAsync(HttpClient client, Fixture fixture, Guid role, string email = "retirement-recipient@example.test")
        => client.PostAsJsonAsync($"/api/v1/organizations/{fixture.Organization.Id}/invitations", new { recipientEmail = email, roleIds = new[] { role } });

    private sealed class FailingCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Injected commit failure.");
    }

    private sealed class UnavailableStore : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Injected unavailable store.");
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
}
