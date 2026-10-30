using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.Identity.API.Contracts.InvitationResponses;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.AcceptInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.DeclineInvitation;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.PreviewInvitation;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Auditing;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.SharedKernel.Security;
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

public sealed class InvitationResponseHttpTests
{
    [PostgreSqlFact]
    public async Task FirstJoinPreviewAndReplayArePrivateAtomicAndDoNotVerifyEmail()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id, fixture.Seed.MemberRole.Id);
        using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
        await using var db = fixture.NewDb();
        int auditsBefore = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
        JsonElement preview = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.OK);
        Assert.Equal(9, preview.EnumerateObject().Count());
        Assert.Equal("Workspace", preview.GetProperty("workspaceName").GetString());
        Assert.True(preview.GetProperty("canAccept").GetBoolean());
        Assert.False(preview.GetProperty("requiresReissue").GetBoolean());
        Assert.Equal(2, preview.GetProperty("offeredRoles").GetArrayLength());
        Assert.Equal(auditsBefore, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        Assert.False(await db.OrganizationMemberships.AnyAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id));
        JsonElement accepted = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        Guid membershipId = accepted.GetProperty("membershipId").GetGuid();
        Assert.Equal("accepted", accepted.GetProperty("outcome").GetString());
        Assert.Equal(5, accepted.EnumerateObject().Count());
        Assert.Equal(2, accepted.GetProperty("roles").GetArrayLength());
        OrganizationMembership membership = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == membershipId);
        Assert.Equal(3, membership.Version);
        Assert.Null((await db.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Foreign.Id)).EmailVerifiedAt);
        OrganizationInvitation invitation = await db.OrganizationInvitations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal("accepted", invitation.Status);
        Assert.Equal(fixture.Foreign.Id, invitation.ClosedByUserId);
        Assert.All(await db.OrganizationRoleAssignments.Where(item => item.MembershipId == membershipId).ToListAsync(), assignment => Assert.Equal(fixture.Owner.Id, assignment.GrantedByUserId));
        List<IdentityAuditEvent> events = await db.IdentityAuditEvents.Where(item => item.MembershipId == membershipId).ToListAsync();
        Assert.Equal(4, events.Count);
        Assert.All(events, item => Assert.Equal(fixture.Foreign.Id, item.ActorUserId));
        Assert.Single(events.Select(item => item.CorrelationId).Distinct());
        await AssertUnavailableAsync(recipient, Route(id, "accept"), new { proof = new string('a', 43), expectedVersion = 1 });
        Assert.Equal("Invitation.Closed", await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.Conflict));

        // Replay returns current roles after legitimate changes, even after the original deadline/sponsor changes.
        await db.OrganizationRoleAssignments.Where(item => item.MembershipId == membershipId && item.RoleId == fixture.Role.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
        await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        int auditCount = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
        JsonElement replay = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 999 }), HttpStatusCode.OK);
        Assert.Equal("alreadyAccepted", replay.GetProperty("outcome").GetString());
        Assert.Single(replay.GetProperty("roles").EnumerateArray());
        Assert.Equal(membership.Version, replay.GetProperty("membershipVersion").GetInt64());
        Assert.Equal(auditCount, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
        foreach (string status in new[] { "removed", "suspended" })
        {
            await db.OrganizationMemberships.Where(item => item.Id == membershipId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, status));
            await AssertUnavailableAsync(recipient, Route(id, "accept"), new { proof, expectedVersion = 1 });
        }

        using HttpClient wrong = Fixture.Client(app, fixture.Ordinary.Id);
        await AssertUnavailableAsync(wrong, Route(id, "accept"), new { proof, expectedVersion = 1 });
        Assert.DoesNotContain(proof, preview.GetRawText() + accepted.GetRawText() + replay.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal));
        Assert.DoesNotContain(await db.IdentityAuditEvents.Where(item => item.OrganizationId == fixture.Organization.Id).Select(item => item.Details).ToListAsync(), details => details.Contains(proof, StringComparison.Ordinal));
        Assert.All(new object[] { new PreviewInvitationRequest(proof), new AcceptInvitationRequest(proof, 1), new DeclineInvitationRequest(proof, 1), new PreviewInvitationQuery(id, proof), new AcceptInvitationCommand(id, proof, 1, "test"), new DeclineInvitationCommand(id, proof, 1, "test") }, request => Assert.DoesNotContain(proof, request.ToString()!, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, (await recipient.GetAsync(fixture.Route)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task DisclosureOrderingValidationAndBodyLimitsApplyToEveryRecipientRoute()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id);
        using HttpClient client = Fixture.Client(app, fixture.Foreign.Id);
        using HttpClient wrong = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        foreach (string action in new[] { "preview", "accept", "decline" })
        {
            object valid = action == "preview" ? new { proof } : (object)new { proof, expectedVersion = 1 };
            await ErrorAsync(await anonymous.PostAsJsonAsync(Route(id, action), valid), HttpStatusCode.Unauthorized);
            await AssertUnavailableAsync(wrong, Route(id, action), valid);
            await AssertUnavailableAsync(client, Route(Guid.NewGuid(), action), valid);
            await AssertUnavailableAsync(client, Route(id, action), action == "preview" ? new { proof = "bad" } : (object)new { proof = "bad", expectedVersion = 1 });
            await AssertUnavailableAsync(client, Route(id, action), action == "preview" ? new { } : (object)new { expectedVersion = 1 });
            await ErrorAsync(await client.PostAsJsonAsync(Route(id, action), new { proof, expectedVersion = 1, userId = fixture.Foreign.Id }), HttpStatusCode.BadRequest);
            using var malformed = new StringContent("{\"proof\":5}", Encoding.UTF8, "application/json");
            await ErrorAsync(await client.PostAsync(Route(id, action), malformed), HttpStatusCode.BadRequest);
        }

        foreach (string action in new[] { "accept", "decline" })
        {
            foreach (long version in new long[] { 0, -1 })
            {
                await ErrorAsync(await client.PostAsJsonAsync(Route(id, action), new { proof, expectedVersion = version }), HttpStatusCode.BadRequest);
            }

            Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await client.PostAsJsonAsync(Route(id, action), new { proof, expectedVersion = 2 }), HttpStatusCode.Conflict));
        }

        fixture.Clock.Now = fixture.Clock.Now.AddDays(7);
        foreach (string action in new[] { "preview", "accept", "decline" })
        {
            object valid = action == "preview" ? new { proof } : (object)new { proof, expectedVersion = 1 };
            await AssertUnavailableAsync(wrong, Route(id, action), valid);
            Assert.Equal("Invitation.Closed", await ErrorAsync(await client.PostAsJsonAsync(Route(id, action), valid), HttpStatusCode.Conflict));
        }

        await using WebApplication kestrel = await fixture.StartAppAsync(useKestrel: true);
        using var network = new HttpClient { BaseAddress = new Uri(kestrel.Urls.Single()) };
        network.DefaultRequestHeaders.Add("X-Test-User", fixture.Foreign.Id.ToString());
        foreach (string action in new[] { "preview", "accept", "decline" })
        {
            using var oversized = new StringContent(JsonSerializer.Serialize(new { proof = new string('a', 17000), expectedVersion = 1 }), Encoding.UTF8, "application/json");
            Assert.Equal("Request.TooLarge", await ErrorAsync(await network.PostAsync(Route(id, action), oversized), HttpStatusCode.RequestEntityTooLarge));
        }
    }

    [PostgreSqlFact]
    public async Task SponsorLossAndChangedOffersRequireReissueButStillAllowDecline()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id, issuer: fixture.Delegate);
        using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
        await using var db = fixture.NewDb();
        OrganizationRole role = await db.OrganizationRoles.SingleAsync(item => item.Id == fixture.Role.Id);
        role.UpdateDetails("Renamed", null, fixture.Clock.Now);
        await db.SaveChangesAsync();
        JsonElement renamed = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.OK);
        Assert.Equal("Original", renamed.GetProperty("offeredRoles")[0].GetProperty("name").GetString());
        Assert.True(renamed.GetProperty("canAccept").GetBoolean());

        // Permission changes that return to the same codes still invalidate the captured version.
        role.AdvancePermissionSetVersion(fixture.Clock.Now);
        role.AdvancePermissionSetVersion(fixture.Clock.Now);
        await db.SaveChangesAsync();
        await AssertRequiresReissueAsync(recipient, id, proof);
        await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Delegate.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        await AssertRequiresReissueAsync(recipient, id, proof);
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        JsonElement reissued = await DataAsync(await owner.PostAsJsonAsync($"{fixture.Route}/{id}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Fresh sponsor and offer" }), HttpStatusCode.OK);
        Guid replacementId = reissued.GetProperty("id").GetGuid();
        string freshProof = await ProofAsync(fixture, replacementId);
        await AssertUnavailableAsync(recipient, Route(id, "preview"), new { proof });
        JsonElement fresh = await DataAsync(await recipient.PostAsJsonAsync(Route(replacementId, "preview"), new { proof = freshProof }), HttpStatusCode.OK);
        Assert.Equal("Renamed", fresh.GetProperty("offeredRoles")[0].GetProperty("name").GetString());
        Assert.True(fresh.GetProperty("canAccept").GetBoolean());

        // Revoking current reissue authority invalidates a replacement, even with issue authority retained.
        await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Owner.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        await AssertRequiresReissueAsync(recipient, replacementId, freshProof);
        JsonElement declined = await DataAsync(await recipient.PostAsJsonAsync(Route(replacementId, "decline"), new { proof = freshProof, expectedVersion = 1 }), HttpStatusCode.OK);
        Assert.Equal("declined", declined.GetProperty("outcome").GetString());
        Assert.Equal(2, declined.GetProperty("version").GetInt64());
        Assert.False(await db.OrganizationMemberships.AnyAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id));
        Assert.Equal(2, await db.OrganizationInvitationDeliveries.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
    }

    [PostgreSqlFact]
    public async Task RemovedAdminRejoinsWithOnlyFreshMemberGrantAndPreservesHistory()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        await using var db = fixture.NewDb();
        OrganizationMembership original = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Removed.Id);
        var admin = OrganizationRoleAssignment.Create(fixture.Organization.Id, original.Id, fixture.Seed.AdminRole.Id, fixture.Owner.Id, fixture.Clock.Now);
        db.OrganizationRoleAssignments.Add(admin);
        await db.SaveChangesAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Removed, fixture.Seed.MemberRole.Id);
        using HttpClient recipient = Fixture.Client(app, fixture.Removed.Id);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        JsonElement accepted = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        Assert.Equal(original.Id, accepted.GetProperty("membershipId").GetGuid());
        Assert.Equal("member", accepted.GetProperty("roles")[0].GetProperty("code").GetString());
        OrganizationMembership rejoined = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == original.Id);
        Assert.Equal(original.CreatedAt, rejoined.CreatedAt);
        Assert.True(rejoined.Version > original.Version);
        List<OrganizationRoleAssignment> assignments = await db.OrganizationRoleAssignments.AsNoTracking().Where(item => item.MembershipId == original.Id).ToListAsync();
        Assert.NotNull(assignments.Single(item => item.RoleId == fixture.Seed.AdminRole.Id).RevokedAt);
        OrganizationRoleAssignment member = assignments.Single(item => item.RoleId == fixture.Seed.MemberRole.Id);
        Assert.Null(member.RevokedAt);
        Assert.Equal(2, member.GrantVersion);
        Assert.Equal(fixture.Owner.Id, member.GrantedByUserId);
        Assert.Equal(fixture.Clock.Now, member.GrantedAt);
        List<IdentityAuditEvent> events = await db.IdentityAuditEvents.Where(item => item.MembershipId == original.Id).ToListAsync();
        Assert.Equal(5, events.Count); // rejoin, two closures, member regrant, invitation close
        Assert.All(events, item => Assert.Equal(fixture.Removed.Id, item.ActorUserId));
        Assert.Equal(2, events.Count(item => item.EventType == IdentityAuditSchemaCatalog.RoleAssignmentRevoked));
        Assert.DoesNotContain(events, item => item.Details.Contains(proof, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task PendingAcceptanceDeniesNewlyActiveOrSuspendedMembershipsAndSupersededProofs()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Removed, fixture.Seed.MemberRole.Id);
        using HttpClient recipient = Fixture.Client(app, fixture.Removed.Id);
        await using var db = fixture.NewDb();
        foreach (string status in new[] { "active", "suspended" })
        {
            await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Removed.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, status));
            JsonElement preview = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.OK);
            Assert.False(preview.GetProperty("canAccept").GetBoolean());
            Assert.Equal("Invitation.RecipientUnavailable", await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.Conflict));
        }

        await db.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Removed.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "removed"));
        using HttpClient manager = Fixture.Client(app, fixture.Owner.Id);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        await DataAsync(await manager.PostAsJsonAsync($"{fixture.Route}/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.OK);
        foreach (string action in new[] { "preview", "accept", "decline" })
        {
            await AssertUnavailableAsync(recipient, Route(id, action), action == "preview" ? new { proof } : (object)new { proof, expectedVersion = 1 });
        }

        string current = await ProofAsync(fixture, id);
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof = current, expectedVersion = 1 }), HttpStatusCode.Conflict));
        await DataAsync(await recipient.PostAsJsonAsync(Route(id, "decline"), new { proof = current, expectedVersion = 2 }), HttpStatusCode.OK);
        Assert.Equal("Invitation.Closed", await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof = current, expectedVersion = 2 }), HttpStatusCode.Conflict));
    }

    [PostgreSqlFact]
    public async Task AuditAndCommitFailuresRollbackMembershipGrantsAndOfferConsumption()
    {
        foreach (bool commit in new[] { false, true })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication issuer = await fixture.StartAppAsync();
            (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Foreign, fixture.Role.Id);
            await using var db = fixture.NewDb();
            int auditCount = await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id);
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
                    builder.Services.AddScoped<IIdentityAuditWriter, FailAfterCloseAudit>();
                }
            });
            using HttpClient recipient = Fixture.Client(failing, fixture.Foreign.Id);
            await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.InternalServerError);
            Assert.False(await db.OrganizationMemberships.AnyAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id));
            Assert.Equal(auditCount, await db.IdentityAuditEvents.CountAsync(item => item.OrganizationId == fixture.Organization.Id));
            OrganizationInvitation invitation = await db.OrganizationInvitations.AsNoTracking().SingleAsync(item => item.Id == id);
            Assert.Equal("pending", invitation.Status);
            Assert.Equal(1, invitation.Version);
            Assert.True(OrganizationInvitationProofCodec.Verify(invitation.OrganizationId, id, proof, invitation.TokenVerifier));
            using HttpClient retry = Fixture.Client(issuer, fixture.Foreign.Id);
            await DataAsync(await retry.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        }
    }

    [PostgreSqlFact]
    public async Task RejoinRollbackRetainsHistoricalGrantsAndDeclineRollbackRetainsCurrentProof()
    {
        foreach (string action in new[] { "accept", "decline" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication issuer = await fixture.StartAppAsync();
            (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Removed, fixture.Seed.MemberRole.Id);
            await using var db = fixture.NewDb();
            OrganizationMembership before = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Removed.Id);
            OrganizationRoleAssignment grantBefore = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.MembershipId == before.Id);
            await using WebApplication failing = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingCommit())));
            using HttpClient client = Fixture.Client(failing, fixture.Removed.Id);
            await ErrorAsync(await client.PostAsJsonAsync(Route(id, action), new { proof, expectedVersion = 1 }), HttpStatusCode.InternalServerError);
            OrganizationMembership after = await db.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.Id == before.Id);
            Assert.Equal("removed", after.Status);
            Assert.Equal(before.Version, after.Version);
            OrganizationRoleAssignment grantAfter = await db.OrganizationRoleAssignments.AsNoTracking().SingleAsync(item => item.Id == grantBefore.Id);
            Assert.Equal(grantBefore.Version, grantAfter.Version);
            Assert.Equal(grantBefore.GrantedAt, grantAfter.GrantedAt);
            Assert.Null(grantAfter.RevokedAt);
            Assert.False(await db.IdentityAuditEvents.AnyAsync(item => item.MembershipId == before.Id));
            Assert.Equal("pending", await db.OrganizationInvitations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        }
    }

    [PostgreSqlFact]
    public async Task PreviewUsesOneReadOnlySnapshotAndAcceptRechecksChangedFacts()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuer = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Foreign, fixture.Role.Id);
        var probe = new SnapshotProbe();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(probe)));
        using HttpClient client = Fixture.Client(app, fixture.Foreign.Id);
        Task<HttpResponseMessage> pending = client.PostAsJsonAsync(Route(id, "preview"), new { proof });
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var db = fixture.NewDb();
            OrganizationRole role = await db.OrganizationRoles.SingleAsync(item => item.Id == fixture.Role.Id);
            role.AdvancePermissionSetVersion(fixture.Clock.Now);
            await db.SaveChangesAsync();
        }
        finally
        {
            probe.Release.TrySetResult();
        }

        JsonElement preview = await DataAsync(await pending, HttpStatusCode.OK);
        Assert.True(preview.GetProperty("canAccept").GetBoolean());
        Assert.False(preview.GetProperty("requiresReissue").GetBoolean());
        await AssertRequiresReissueAsync(client, id, proof);
        await DataAsync(await client.PostAsJsonAsync(Route(id, "decline"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task EveryCurrentSponsorCapabilityAndDelegationCeilingIsRequired()
    {
        foreach (string lost in new[] { OrganizationCapabilityCatalog.InvitationsIssue, OrganizationCapabilityCatalog.InvitationsReissue, OrganizationCapabilityCatalog.RolesManage, OrganizationCapabilityCatalog.MembersRead })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id, issuer: fixture.Delegate);
            if (lost == OrganizationCapabilityCatalog.InvitationsReissue)
            {
                fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
                using HttpClient manager = Fixture.Client(app, fixture.Delegate.Id);
                JsonElement reissued = await DataAsync(await manager.PostAsJsonAsync($"{fixture.Route}/{id}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Replacement offer" }), HttpStatusCode.OK);
                id = reissued.GetProperty("id").GetGuid();
                proof = await ProofAsync(fixture, id);
            }

            await using var db = fixture.NewDb();
            await db.OrganizationRolePermissions.Where(item => item.OrganizationId == fixture.Organization.Id && item.RoleId == fixture.DelegateRole.Id && item.PermissionCode == lost).ExecuteDeleteAsync();
            using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
            await AssertRequiresReissueAsync(recipient, id, proof);
            await DataAsync(await recipient.PostAsJsonAsync(Route(id, "decline"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        }
    }

    [PostgreSqlFact]
    public async Task CurrentDatabaseEmailWorkspaceAndAccountDetermineAvailability()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id);
        using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
        await using var db = fixture.NewDb();
        await db.Users.Where(item => item.Id == fixture.Foreign.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.PrimaryEmail, "changed-" + fixture.Foreign.PrimaryEmail));
        await AssertUnavailableAsync(recipient, Route(id, "preview"), new { proof });
        await db.Users.Where(item => item.Id == fixture.Foreign.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.PrimaryEmail, fixture.Foreign.PrimaryEmail).SetProperty(item => item.Status, "suspended"));
        await AssertUnavailableAsync(recipient, Route(id, "decline"), new { proof, expectedVersion = 1 });
        await db.Users.Where(item => item.Id == fixture.Foreign.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "active"));
        await db.Organizations.Where(item => item.Id == fixture.Organization.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        await AssertUnavailableAsync(recipient, Route(id, "accept"), new { proof, expectedVersion = 1 });
        await db.Organizations.Where(item => item.Id == fixture.Organization.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "active"));
        await db.Users.Where(item => item.Id == fixture.Foreign.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.PrimaryEmail, fixture.Foreign.PrimaryEmail.ToUpperInvariant()));
        fixture.Clock.Now = fixture.Clock.Now.AddDays(7).AddTicks(-10);
        await DataAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
    }

    [PostgreSqlFact]
    public async Task AuthorityStoreFailureReturnsSafeUnavailableAndDoesNotConsumeProof()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuer = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Foreign, fixture.Role.Id);
        await using WebApplication unavailable = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new UnavailableStore())));
        using HttpClient recipient = Fixture.Client(unavailable, fixture.Foreign.Id);
        foreach (string action in new[] { "preview", "accept", "decline" })
        {
            await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, action), action == "preview" ? new { proof } : (object)new { proof, expectedVersion = 1 }), HttpStatusCode.ServiceUnavailable);
        }

        await using var db = fixture.NewDb();
        Assert.Equal("pending", await db.OrganizationInvitations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        Assert.False(await db.OrganizationMemberships.AnyAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id));
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task ConcurrentRecipientAndManagerTransitionsHaveOneWinner()
    {
        foreach (string competing in new[] { "accept", "decline", "cancel", "resend", "reissue" })
        {
            await using Fixture fixture = await Fixture.CreateAsync();
            await using WebApplication app = await fixture.StartAppAsync();
            (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id);
            fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
            using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
            using HttpClient manager = Fixture.Client(app, fixture.Owner.Id);
            object managerBody = competing == "reissue"
                ? new { expectedVersion = 1, reason = "Competing change", roleIds = new[] { fixture.Role.Id } }
                : (object)new { expectedVersion = 1, reason = "Competing change" };
            Task<HttpResponseMessage> other = competing is "accept" or "decline"
                ? recipient.PostAsJsonAsync(Route(id, competing), new { proof, expectedVersion = 1 })
                : manager.PostAsJsonAsync($"{fixture.Route}/{id}/{competing}", managerBody);

            HttpResponseMessage[] results = await Task.WhenAll(recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), other);
            try
            {
                if (competing == "accept")
                {
                    Assert.All(results, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
                    string outcomes = string.Join(" ", await Task.WhenAll(results.Select(response => response.Content.ReadAsStringAsync())));
                    Assert.Contains("alreadyAccepted", outcomes, StringComparison.Ordinal);
                }
                else
                {
                    Assert.Single(results, response => response.IsSuccessStatusCode);
                    Assert.All(results.Where(response => !response.IsSuccessStatusCode), response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Conflict }));
                }

                await using var db = fixture.NewDb();
                OrganizationInvitation invitation = await db.OrganizationInvitations.AsNoTracking().SingleAsync(item => item.Id == id);
                int memberships = await db.OrganizationMemberships.CountAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id);
                Assert.Equal(invitation.Status == "accepted" ? 1 : 0, memberships);
                Assert.Equal(2, invitation.Version);
            }
            finally
            {
                foreach (HttpResponseMessage result in results)
                {
                    result.Dispose();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task RealBearerValidationRequiresCurrentActiveAccountAndOwnedSession()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication issuer = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Foreign, fixture.Role.Id);
        await using var db = fixture.NewDb();
        var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Foreign.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        var otherSession = AuthSession.Create(Guid.CreateVersion7(), fixture.Ordinary.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
        db.AuthSessions.AddRange(session, otherSession);
        await db.SaveChangesAsync();
        await using WebApplication app = await fixture.StartAppAsync(configure: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "recipient-tests-only-signing-key-at-least-32-bytes",
                ["Jwt:Issuer"] = "recipient-tests",
                ["Jwt:Audience"] = "recipient-tests",
                ["Jwt:AccessTokenExpirationMinutes"] = "10",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
            builder.Services.AddAuthSessions(builder.Configuration).AddConfiguredAuthentication(builder.Configuration, builder.Environment);
        });
        JwtAccessTokenIssuer tokenIssuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();
        using HttpClient client = app.GetTestClient();
        string token = tokenIssuer.Issue(fixture.Foreign.Id, session.Id, fixture.Clock.Now).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await client.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenIssuer.Issue(fixture.Foreign.Id, otherSession.Id, fixture.Clock.Now).Token);
        await ErrorAsync(await client.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DataAsync(await client.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.OK);
        await db.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now));
        await ErrorAsync(await client.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.Unauthorized);
        await db.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, (DateTimeOffset?)null));
        await db.Users.Where(item => item.Id == fixture.Foreign.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
        await ErrorAsync(await client.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal) || line.Contains(token, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task RecipientAndSponsorEligibilityLocksRemainHeldThroughCommitInEitherOrder()
    {
        foreach (string authority in new[] { "recipient", "session", "sponsor" })
        {
            foreach (bool changeFirst in new[] { false, true })
            {
                await using Fixture fixture = await Fixture.CreateAsync();
                await using WebApplication issuer = await fixture.StartAppAsync();
                (Guid id, string proof) = await IssueAsync(fixture, issuer, fixture.Foreign, fixture.Role.Id, issuer: fixture.Delegate);
                await using var setup = fixture.NewDb();
                var session = AuthSession.Create(Guid.CreateVersion7(), fixture.Foreign.Id, RandomNumberGenerator.GetBytes(32), fixture.Clock.Now, fixture.Clock.Now.AddDays(1));
                setup.AuthSessions.Add(session);
                await setup.SaveChangesAsync();
                var gate = new CommitGate();
                await using WebApplication app = await fixture.StartAppAsync(configure: builder => builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(gate)));
                using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
                recipient.DefaultRequestHeaders.Add("X-Test-Session", session.Id.ToString());
                await using var changer = fixture.NewDb();
                await using var transaction = await changer.Database.BeginTransactionAsync();
                int changerPid = ((NpgsqlConnection)changer.Database.GetDbConnection()).ProcessID;
                Task<int> ChangeAsync() => authority == "session"
                    ? changer.AuthSessions.Where(item => item.Id == session.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, fixture.Clock.Now))
                    : changer.Users.Where(item => item.Id == (authority == "recipient" ? fixture.Foreign.Id : fixture.Delegate.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "suspended"));
                Task<HttpResponseMessage>? pending = null;
                try
                {
                    if (changeFirst)
                    {
                        await ChangeAsync();
                        pending = recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 });
                        await AssertBlockedAsync(setup, changerPid, blocker: true);
                        await transaction.CommitAsync();
                        Assert.Equal(
                            authority == "sponsor" ? "Invitation.OfferChanged" : "Invitation.Unavailable",
                            await ErrorAsync(await pending, authority == "sponsor" ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden));
                    }
                    else
                    {
                        pending = recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 });
                        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Task<int> change = ChangeAsync();
                        await AssertBlockedAsync(setup, changerPid, blocker: false);
                        Assert.False(change.IsCompleted);
                        gate.Release.TrySetResult();
                        await DataAsync(await pending, HttpStatusCode.OK);
                        await change;
                        await transaction.CommitAsync();
                    }

                    OrganizationInvitation invitation = await setup.OrganizationInvitations.AsNoTracking().SingleAsync(item => item.Id == id);
                    Assert.Equal(changeFirst ? "pending" : "accepted", invitation.Status);
                    Assert.Equal(changeFirst ? 1 : 2, invitation.Version);
                }
                finally
                {
                    gate.Release.TrySetResult();
                }
            }
        }
    }

    [PostgreSqlFact]
    public async Task SponsorGrantExpiryDuringALockWaitIsRecheckedBeforeAcceptance()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        (Guid id, string proof) = await IssueAsync(fixture, app, fixture.Foreign, fixture.Role.Id, issuer: fixture.Delegate);
        await using var observer = fixture.NewDb();
        Guid sponsorMembershipId = await observer.OrganizationMemberships.Where(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Delegate.Id).Select(item => item.Id).SingleAsync();
        await observer.OrganizationRoleAssignments.Where(item => item.MembershipId == sponsorMembershipId).ExecuteUpdateAsync(update => update.SetProperty(item => item.ExpiresAt, fixture.Clock.Now.AddSeconds(1)));
        await using var blocker = fixture.NewDb();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"""SELECT * FROM shared."Users" WHERE "Id" = {fixture.Foreign.Id} FOR UPDATE""");
        int pid = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
        using HttpClient recipient = Fixture.Client(app, fixture.Foreign.Id);
        Task<HttpResponseMessage> pending = recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 });
        await AssertBlockedAsync(observer, pid, blocker: true);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
        await transaction.CommitAsync();
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await pending, HttpStatusCode.Conflict));
        Assert.Equal("pending", await observer.OrganizationInvitations.Where(item => item.Id == id).Select(item => item.Status).SingleAsync());
        Assert.False(await observer.OrganizationMemberships.AnyAsync(item => item.OrganizationId == fixture.Organization.Id && item.UserId == fixture.Foreign.Id));
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

        Assert.Fail("The authority change and recipient transaction did not serialize on a PostgreSQL lock.");
    }

    private static string Route(Guid id, string action) => $"/api/v1/organization-invitations/{id}/{action}";

    private static async Task<(Guid Id, string Proof)> IssueAsync(Fixture fixture, WebApplication app, User recipient, Guid roleId, Guid? otherRole = null, User? issuer = null)
    {
        using HttpClient manager = Fixture.Client(app, (issuer ?? fixture.Owner).Id);
        Guid[] roles = otherRole.HasValue ? [roleId, otherRole.Value] : [roleId];
        JsonElement issued = await DataAsync(await manager.PostAsJsonAsync(fixture.Route, new { recipientEmail = recipient.PrimaryEmail, roleIds = roles }), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        return (id, await ProofAsync(fixture, id));
    }

    private static async Task<string> ProofAsync(Fixture fixture, Guid id)
    {
        await using var db = fixture.NewDb();
        Guid messageId = await db.OrganizationInvitations.Where(item => item.Id == id).Select(item => item.CurrentDeliveryMessageId!.Value).SingleAsync();
        string encrypted = await db.Set<OutboxMessage>().Where(item => item.MessageId == messageId).Select(item => item.PayloadJson).SingleAsync();
        return fixture.ReadProof(encrypted);
    }

    private static async Task AssertUnavailableAsync(HttpClient client, string route, object body)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(route, body);
        Assert.Equal("Invitation.Unavailable", await ErrorAsync(response, HttpStatusCode.Forbidden));
    }

    private static async Task AssertRequiresReissueAsync(HttpClient recipient, Guid id, string proof)
    {
        JsonElement preview = await DataAsync(await recipient.PostAsJsonAsync(Route(id, "preview"), new { proof }), HttpStatusCode.OK);
        Assert.False(preview.GetProperty("canAccept").GetBoolean());
        Assert.True(preview.GetProperty("requiresReissue").GetBoolean());
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await recipient.PostAsJsonAsync(Route(id, "accept"), new { proof, expectedVersion = 1 }), HttpStatusCode.Conflict));
    }

    private sealed class FailAfterCloseAudit(IdentityAuditWriter inner) : IIdentityAuditWriter
    {
        public async Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
        {
            IdentityAuditEvent result = await inner.StageAsync(entry, cancellationToken);
            if (entry.AggregateType == "OrganizationInvitation")
            {
                throw new InvalidOperationException("Injected failure after staging invitation closure.");
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
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
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
            if (command.CommandText.Contains("FROM shared.\"OrganizationRoles\"", StringComparison.Ordinal) && Interlocked.Exchange(ref _observed, 1) == 0)
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
            => throw new TimeoutException("Injected authority store outage.");
    }
}
