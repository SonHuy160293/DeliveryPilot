using System.Net;
using System.Globalization;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DeliveryPilot.AspNetCore.DependencyInjection;
using DeliveryPilot.AspNetCore.Filters;
using DeliveryPilot.AspNetCore.Middleware;
using DeliveryPilot.AspNetCore.Security;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.API.Configuration;
using DeliveryPilot.Identity.API.Controllers;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.RecordDeliveryStatus;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.DepedencyInjection;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Messaging.Security;
using DeliveryPilot.SharedKernel.Cqrs;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class InvitationManagementHttpTests
{
    [PostgreSqlFact]
    public async Task MutationBodyCeilingIsEnforcedByKestrel()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync(useKestrel: true);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Add("X-Test-User", fixture.Owner.Id.ToString());
        foreach (string suffix in new[] { string.Empty, $"/{Guid.NewGuid()}/resend", $"/{Guid.NewGuid()}/reissue", $"/{Guid.NewGuid()}/cancel" })
        {
            using var content = new StringContent(JsonSerializer.Serialize(new { recipientEmail = new string('a', 17000), roleIds = new[] { fixture.Role.Id }, expectedVersion = 1, reason = "Reason" }), Encoding.UTF8, "application/json");
            Assert.Equal("Request.TooLarge", await ErrorAsync(await client.PostAsync(fixture.Route + suffix, content), HttpStatusCode.RequestEntityTooLarge));
        }
    }

    [PostgreSqlFact]
    public async Task ManagerJourneyUsesImmutableSnapshotsCurrentDeliveryAndAtomicEncryptedStaging()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient client = Fixture.Client(app, fixture.Owner.Id);
        string route = fixture.Route;
        JsonElement issued = await DataAsync(await client.PostAsJsonAsync(route, new { recipientEmail = " PERSON@example.test ", roleIds = new[] { fixture.Role.Id, fixture.Seed.MemberRole.Id } }), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        Assert.Equal("person@example.test", issued.GetProperty("recipientEmail").GetString());
        Assert.Equal("pending", issued.GetProperty("status").GetString());
        Assert.Equal(2, issued.GetProperty("offeredRoles").GetArrayLength());
        Assert.Equal(fixture.Clock.Now.AddDays(7), issued.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal("queued", issued.GetProperty("delivery").GetProperty("status").GetString());
        Assert.Equal(8, issued.EnumerateObject().Count());
        await using var db = fixture.NewDb();
        OrganizationInvitation original = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        byte[] verifier = original.TokenVerifier;
        string ciphertext = await db.Set<OutboxMessage>().Where(x => x.MessageId == original.CurrentDeliveryMessageId).Select(x => x.PayloadJson).SingleAsync();
        string proof = fixture.ReadProof(ciphertext);
        Assert.True(OrganizationInvitationProofCodec.Verify(fixture.Organization.Id, id, proof, verifier));
        Assert.DoesNotContain("person@example.test", ciphertext, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(proof, ciphertext, StringComparison.Ordinal);
        Assert.DoesNotContain(proof, issued.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Logs, line => line.Contains(proof, StringComparison.Ordinal));
        Assert.DoesNotContain(await db.IdentityAuditEvents.Where(x => x.OrganizationId == fixture.Organization.Id).Select(x => x.Details).ToListAsync(), details => details.Contains(proof, StringComparison.Ordinal));
        Assert.DoesNotContain("TokenVerifier", issued.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("proof", issued.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, await db.OrganizationMemberships.CountAsync(x => x.OrganizationId == fixture.Organization.Id));

        Assert.Equal("Invitation.PendingOfferExists", await ErrorAsync(await client.PostAsJsonAsync(route, new { recipientEmail = "PERSON@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Conflict));
        using (HttpResponseMessage limited = await client.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 1 }))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.Equal(60, limited.Headers.RetryAfter!.Delta!.Value.TotalSeconds);
        }

        Assert.Equal(1, await db.OrganizationInvitationDeliveries.CountAsync(x => x.InvitationId == id));

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        OrganizationRole role = await db.OrganizationRoles.SingleAsync(x => x.Id == fixture.Role.Id);
        role.UpdateDetails("Renamed", null, fixture.Clock.Now);
        await db.SaveChangesAsync();
        JsonElement resent = await DataAsync(await client.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 1, reason = "Requested again" }), HttpStatusCode.OK);
        Assert.Equal(2, resent.GetProperty("version").GetInt64());
        Assert.Equal("Original", resent.GetProperty("offeredRoles").EnumerateArray().Single(x => x.GetProperty("roleId").GetGuid() == role.Id).GetProperty("name").GetString());
        OrganizationInvitation current = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(verifier.SequenceEqual(current.TokenVerifier));
        Assert.False(OrganizationInvitationProofCodec.Verify(fixture.Organization.Id, id, proof, current.TokenVerifier));
        Assert.NotEqual(original.CurrentDeliveryMessageId, current.CurrentDeliveryMessageId);
        Assert.Equal(original.IssuedByUserId, current.IssuedByUserId);
        Assert.Equal(fixture.Clock.Now.AddDays(7), current.ExpiresAt);

        await fixture.RecordStatusAsync(app, current.CurrentDeliveryMessageId!.Value, 2, "failed");
        await fixture.RecordStatusAsync(app, current.CurrentDeliveryMessageId.Value, 1, "provider-accepted");
        await fixture.RecordStatusAsync(app, original.CurrentDeliveryMessageId!.Value, 3, "delivered");
        JsonElement detail = await DataAsync(await client.GetAsync($"{route}/{id}"), HttpStatusCode.OK);
        Assert.Equal("failed", detail.GetProperty("delivery").GetProperty("status").GetString());
        Assert.Equal("pending", detail.GetProperty("status").GetString());
        Assert.Equal(current.ExpiresAt, detail.GetProperty("expiresAt").GetDateTimeOffset());

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        role.AdvancePermissionSetVersion(fixture.Clock.Now);
        role.AdvancePermissionSetVersion(fixture.Clock.Now);
        await db.SaveChangesAsync();
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await client.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 2 }), HttpStatusCode.Conflict));
        JsonElement reissued = await DataAsync(await client.PostAsJsonAsync($"{route}/{id}/reissue", new { expectedVersion = 2, roleIds = new[] { role.Id }, reason = "New offer" }), HttpStatusCode.OK);
        Guid replacementId = reissued.GetProperty("id").GetGuid();
        Assert.NotEqual(id, replacementId);
        Assert.Equal("Renamed", reissued.GetProperty("offeredRoles")[0].GetProperty("name").GetString());
        Assert.Equal(3, reissued.GetProperty("offeredRoles")[0].GetProperty("permissionSetVersion").GetInt64());
        Assert.Equal("New offer", await db.OrganizationInvitations.Where(x => x.Id == id).Select(x => x.CloseReason).SingleAsync());
        Assert.Equal(id, await db.OrganizationInvitations.Where(x => x.Id == replacementId).Select(x => x.ReplacesInvitationId).SingleAsync());
        JsonElement cancelled = await DataAsync(await client.PostAsJsonAsync($"{route}/{replacementId}/cancel", new { expectedVersion = 1, reason = "No longer needed" }), HttpStatusCode.OK);
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal("Invitation.Closed", await ErrorAsync(await client.PostAsJsonAsync($"{route}/{replacementId}/resend", new { expectedVersion = 2 }), HttpStatusCode.Conflict));

        JsonElement acceptedFixture = await DataAsync(await client.PostAsJsonAsync(route, new { recipientEmail = "accepted@example.test", roleIds = new[] { role.Id } }), HttpStatusCode.Created);
        Guid acceptedId = acceptedFixture.GetProperty("id").GetGuid();
        OrganizationInvitation accepted = await db.OrganizationInvitations.SingleAsync(x => x.Id == acceptedId);
        accepted.Close("accepted", fixture.Ordinary.Id, fixture.Clock.Now);
        await db.SaveChangesAsync();
        Assert.Equal("Invitation.Closed", await ErrorAsync(await client.PostAsJsonAsync($"{route}/{acceptedId}/cancel", new { expectedVersion = 2, reason = "Cannot revoke membership" }), HttpStatusCode.Conflict));
        Assert.Equal("active", await db.OrganizationMemberships.Where(x => x.UserId == fixture.Ordinary.Id && x.OrganizationId == fixture.Organization.Id).Select(x => x.Status).SingleAsync());

        JsonElement expiring = await DataAsync(await client.PostAsJsonAsync(route, new { recipientEmail = "expired@example.test", roleIds = new[] { role.Id } }), HttpStatusCode.Created);
        Guid expiredId = expiring.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(8);
        Assert.Equal("Invitation.Closed", await ErrorAsync(await client.PostAsJsonAsync($"{route}/{expiredId}/resend", new { expectedVersion = 1 }), HttpStatusCode.Conflict));
        await DataAsync(await client.PostAsJsonAsync(route, new { recipientEmail = "expired@example.test", roleIds = new[] { role.Id } }), HttpStatusCode.Created);
        Assert.Equal("expired", await db.OrganizationInvitations.Where(x => x.Id == expiredId).Select(x => x.Status).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task DelegatedActionsPreservePrivacyAndReadsReauthorizeEachPage()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        using HttpClient delegated = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient ordinary = Fixture.Client(app, fixture.Ordinary.Id);
        using HttpClient anonymous = Fixture.Client(app, null);
        string route = fixture.Route;
        var payload = new { recipientEmail = "delegate-target@example.test", roleIds = new[] { fixture.Role.Id } };
        await ErrorAsync(await anonymous.PostAsJsonAsync(route, payload), HttpStatusCode.Unauthorized);
        await ErrorAsync(await ordinary.PostAsJsonAsync(route, payload), HttpStatusCode.Forbidden);
        JsonElement issued = await DataAsync(await delegated.PostAsJsonAsync(route, payload), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        await ErrorAsync(await delegated.GetAsync(route), HttpStatusCode.Forbidden);
        await ErrorAsync(await delegated.GetAsync($"{route}/{id}"), HttpStatusCode.Forbidden);
        string duplicate = await ErrorAsync(await delegated.PostAsJsonAsync(route, payload), HttpStatusCode.Conflict);
        string unavailable = await ErrorAsync(await delegated.PostAsJsonAsync(route, new { recipientEmail = fixture.Ordinary.PrimaryEmail, roleIds = payload.roleIds }), HttpStatusCode.Conflict);
        Assert.Equal("Invitation.RecipientUnavailable", duplicate);
        Assert.Equal(duplicate, unavailable);
        await ErrorAsync(await delegated.PostAsJsonAsync(route, new { recipientEmail = "foreign@example.test", roleIds = new[] { fixture.ForeignRole.Id } }), HttpStatusCode.Forbidden);
        await ErrorAsync(await delegated.PostAsJsonAsync(route, new { recipientEmail = "owner@example.test", roleIds = new[] { fixture.Seed.OwnerRole.Id } }), HttpStatusCode.Forbidden);
        await ErrorAsync(await delegated.PostAsJsonAsync($"{route}/{Guid.NewGuid()}/resend", new { expectedVersion = 1 }), HttpStatusCode.Forbidden);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{Guid.NewGuid()}/resend", new { expectedVersion = 1 }), HttpStatusCode.NotFound);
        await ErrorAsync(await owner.GetAsync($"/api/v1/organizations/{fixture.ForeignOrganization.Id}/invitations/{id}"), HttpStatusCode.Forbidden);
        await ErrorAsync(await owner.PostAsJsonAsync(route, new { recipientEmail = fixture.Suspended.PrimaryEmail, roleIds = payload.roleIds }), HttpStatusCode.Conflict);
        await DataAsync(await owner.PostAsJsonAsync(route, new { recipientEmail = fixture.Removed.PrimaryEmail, roleIds = payload.roleIds }), HttpStatusCode.Created);

        await using var db = fixture.NewDb();
        string[] capabilities = [OrganizationCapabilityCatalog.InvitationsIssue, OrganizationCapabilityCatalog.InvitationsResend, OrganizationCapabilityCatalog.InvitationsReissue, OrganizationCapabilityCatalog.InvitationsCancel, OrganizationCapabilityCatalog.RolesManage];
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        foreach (string capability in capabilities)
        {
            await db.OrganizationRolePermissions.Where(x => x.RoleId == fixture.DelegateRole.Id && x.PermissionCode == capability).ExecuteDeleteAsync();
            HttpResponseMessage denied = capability switch
            {
                OrganizationCapabilityCatalog.InvitationsResend => await delegated.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 1 }),
                OrganizationCapabilityCatalog.InvitationsReissue => await delegated.PostAsJsonAsync($"{route}/{id}/reissue", new { expectedVersion = 1, roleIds = payload.roleIds, reason = "Updated offer" }),
                OrganizationCapabilityCatalog.InvitationsCancel => await delegated.PostAsJsonAsync($"{route}/{id}/cancel", new { expectedVersion = 1, reason = "Close" }),
                _ => await delegated.PostAsJsonAsync(route, new { recipientEmail = "action@example.test", roleIds = payload.roleIds })
            };
            await ErrorAsync(denied, HttpStatusCode.Forbidden);
            db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, capability));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, OrganizationCapabilityCatalog.InvitationsRead));
        await db.SaveChangesAsync();
        JsonElement page = await DataAsync(await delegated.GetAsync($"{route}?limit=1"), HttpStatusCode.OK);
        string cursor = page.GetProperty("nextCursor").GetString()!;
        Guid firstId = page.GetProperty("items")[0].GetProperty("id").GetGuid();
        JsonElement next = await DataAsync(await delegated.GetAsync($"{route}?limit=1&cursor={Uri.EscapeDataString(cursor)}"), HttpStatusCode.OK);
        Assert.NotEqual(firstId, next.GetProperty("items")[0].GetProperty("id").GetGuid());
        await db.OrganizationRolePermissions.Where(x => x.RoleId == fixture.DelegateRole.Id && x.PermissionCode == OrganizationCapabilityCatalog.InvitationsRead).ExecuteDeleteAsync();
        await ErrorAsync(await delegated.GetAsync($"{route}?limit=1&cursor={Uri.EscapeDataString(cursor)}"), HttpStatusCode.Forbidden);
        foreach (string query in new[] { "?status=invalid", "?limit=0", "?limit=101", "?cursor=invalid", "?cursor=", $"?cursor={new string('a', 1025)}" })
        {
            await ErrorAsync(await owner.GetAsync(route + query), HttpStatusCode.BadRequest);
        }

        object[] invalidIssues =
        [
            new { recipientEmail = "not-an-email", roleIds = payload.roleIds },
            new { recipientEmail = "Display <a@example.test>", roleIds = payload.roleIds },
            new { recipientEmail = "valid@example.test", roleIds = Array.Empty<Guid>() },
            new { recipientEmail = "valid@example.test", roleIds = new[] { Guid.Empty } },
            new { recipientEmail = "valid@example.test", roleIds = new[] { fixture.Role.Id, fixture.Role.Id } },
            new { recipientEmail = "valid@example.test", roleIds = payload.roleIds, extra = true }
        ];
        foreach (object invalid in invalidIssues)
        {
            await ErrorAsync(await owner.PostAsJsonAsync(route, invalid), HttpStatusCode.BadRequest);
        }

        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 0 }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/resend", new { expectedVersion = 1, reason = " " }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/cancel", new { expectedVersion = 1 }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/cancel", new { expectedVersion = 1, reason = new string('a', 1001) }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/reissue", new { expectedVersion = 1, roleIds = payload.roleIds }), HttpStatusCode.BadRequest);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/reissue", new { expectedVersion = 1, roleIds = payload.roleIds, reason = "Valid", extra = true }), HttpStatusCode.BadRequest);
        await db.OrganizationRoles.Where(x => x.Id == fixture.Role.Id).ExecuteUpdateAsync(x => x.SetProperty(role => role.RetiredAt, fixture.Clock.Now));
        await ErrorAsync(await owner.PostAsJsonAsync(route, new { recipientEmail = "retired@example.test", roleIds = payload.roleIds }), HttpStatusCode.Forbidden);
        await ErrorAsync(await owner.PostAsJsonAsync($"{route}/{id}/reissue", new { expectedVersion = 1, roleIds = payload.roleIds, reason = "Retired offer" }), HttpStatusCode.Forbidden);
    }

    [PostgreSqlFact]
    public async Task ResendRequiresCurrentOriginalSponsorAndReissueRepairsLostAuthority()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient delegated = Fixture.Client(app, fixture.Delegate.Id);
        using HttpClient owner = Fixture.Client(app, fixture.Owner.Id);
        JsonElement issued = await DataAsync(await delegated.PostAsJsonAsync(fixture.Route, new { recipientEmail = "sponsor@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        await using var db = fixture.NewDb();
        OrganizationInvitation before = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        foreach (string capability in new[] { OrganizationCapabilityCatalog.RolesManage, OrganizationCapabilityCatalog.MembersRead })
        {
            await db.OrganizationRolePermissions.Where(x => x.RoleId == fixture.DelegateRole.Id && x.PermissionCode == capability).ExecuteDeleteAsync();
            Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await owner.PostAsJsonAsync($"{fixture.Route}/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.Conflict));
            db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, capability));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        await db.OrganizationRolePermissions.Where(x => x.RoleId == fixture.DelegateRole.Id && x.PermissionCode == OrganizationCapabilityCatalog.InvitationsIssue).ExecuteDeleteAsync();
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await owner.PostAsJsonAsync($"{fixture.Route}/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.Conflict));
        OrganizationInvitation unchanged = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(before.Version, unchanged.Version);
        Assert.Equal(before.ExpiresAt, unchanged.ExpiresAt);
        Assert.Equal(before.TokenVerifier, unchanged.TokenVerifier);
        Assert.Equal(1, await db.OrganizationInvitationDeliveries.CountAsync(x => x.InvitationId == id));
        JsonElement reissued = await DataAsync(await delegated.PostAsJsonAsync($"{fixture.Route}/{id}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Fresh authorized offer" }), HttpStatusCode.OK);
        Guid replacementId = reissued.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);

        // This sponsor can reissue but cannot issue; the replacement retains the matching authority requirement.
        await DataAsync(await owner.PostAsJsonAsync($"{fixture.Route}/{replacementId}/resend", new { expectedVersion = 1 }), HttpStatusCode.OK);

        // Account eligibility must remain locked until the invitation transaction completes.
        await using (var lockedDb = fixture.NewDb())
        {
            await using var transaction = await lockedDb.Database.BeginTransactionAsync();
            var guard = new OrganizationMutationGuard(lockedDb, new CurrentProvider(fixture.Owner.Id), fixture.Clock);
            OrganizationMutationScope scope = await guard.AcquireForInvitationAsync(fixture.Organization.Id, replacementId);
            Assert.Equal(fixture.Delegate.Id, scope.Sponsor!.Actor.UserId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task<int> suspend = db.Users.Where(x => x.Id == fixture.Delegate.Id).ExecuteUpdateAsync(x => x.SetProperty(user => user.Status, "suspended"), timeout.Token);
            Assert.NotSame(suspend, await Task.WhenAny(suspend, Task.Delay(150, timeout.Token)));
            await transaction.CommitAsync();
            Assert.Equal(1, await suspend);
        }

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        Assert.Equal("Invitation.OfferChanged", await ErrorAsync(await owner.PostAsJsonAsync($"{fixture.Route}/{replacementId}/resend", new { expectedVersion = 2 }), HttpStatusCode.Conflict));
    }

    [PostgreSqlFact]
    public async Task FailuresRollbackAllStagedStateAndRacesKeepOneCurrentOffer()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync();
        using HttpClient client = Fixture.Client(app, fixture.Owner.Id);
        JsonElement issued = await DataAsync(await client.PostAsJsonAsync(fixture.Route, new { recipientEmail = "rollback@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        Guid id = issued.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        await using var db = fixture.NewDb();
        OrganizationInvitation before = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        foreach (string failure in new[] { "audit", "outbox", "ledger", "configuration" })
        {
            await using WebApplication failing = await fixture.StartAppAsync(failure);
            using HttpClient failedClient = Fixture.Client(failing, fixture.Owner.Id);
            await ErrorAsync(await failedClient.PostAsJsonAsync(fixture.Route, new { recipientEmail = "failure@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.InternalServerError);
            Assert.False(await db.OrganizationInvitations.AnyAsync(x => x.OrganizationId == fixture.Organization.Id && x.RecipientEmail == "failure@example.test"));
            await ErrorAsync(await failedClient.PostAsJsonAsync($"{fixture.Route}/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.InternalServerError);
            await ErrorAsync(await failedClient.PostAsJsonAsync($"{fixture.Route}/{id}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Atomic replacement" }), HttpStatusCode.InternalServerError);
            OrganizationInvitation after = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.Version, after.Version);
            Assert.Equal(before.TokenVerifier, after.TokenVerifier);
            Assert.Equal(before.CurrentDeliveryMessageId, after.CurrentDeliveryMessageId);
            Assert.Equal(1, await db.OrganizationInvitationDeliveries.CountAsync(x => x.OrganizationId == fixture.Organization.Id));
        }

        HttpResponseMessage[] issues = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(fixture.Route, new { recipientEmail = "race@example.test", roleIds = new[] { fixture.Role.Id } })));
        HttpResponseMessage winner = Assert.Single(issues, response => response.StatusCode == HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(issues, response => response.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        JsonElement winnerData = await DataAsync(winner, HttpStatusCode.Created);
        Guid raceId = winnerData.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        HttpResponseMessage[] mutations = await Task.WhenAll(
            client.PostAsJsonAsync($"{fixture.Route}/{raceId}/resend", new { expectedVersion = 1 }),
            client.PostAsJsonAsync($"{fixture.Route}/{raceId}/reissue", new { expectedVersion = 1, roleIds = new[] { fixture.Role.Id }, reason = "Concurrent replacement" }),
            client.PostAsJsonAsync($"{fixture.Route}/{raceId}/cancel", new { expectedVersion = 1, reason = "Concurrent close" }));
        Assert.Single(mutations, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(2, mutations.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        foreach (HttpResponseMessage response in mutations)
        {
            response.Dispose();
        }

        Assert.InRange(await db.OrganizationInvitations.CountAsync(x => x.OrganizationId == fixture.Organization.Id && x.RecipientEmail == "race@example.test" && x.Status == "pending"), 0, 1);
        Assert.Equal(2, await db.OrganizationInvitations.Where(x => x.Id == raceId).Select(x => x.Version).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task RateLimitsReturnSafeRetryAfterAndDoNotChangeProofOrDeadline()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        await using WebApplication app = await fixture.StartAppAsync(hourlyLimit: 3, dailyLimit: 2);
        using HttpClient client = Fixture.Client(app, fixture.Owner.Id);
        JsonElement first = await DataAsync(await client.PostAsJsonAsync(fixture.Route, new { recipientEmail = "limits@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        Guid id = first.GetProperty("id").GetGuid();
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        await DataAsync(await client.PostAsJsonAsync($"{fixture.Route}/{id}/resend", new { expectedVersion = 1 }), HttpStatusCode.OK);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        await using var db = fixture.NewDb();
        OrganizationInvitation before = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        using (HttpResponseMessage daily = await client.PostAsJsonAsync($"{fixture.Route}/{id}/reissue", new { expectedVersion = 2, roleIds = new[] { fixture.Role.Id }, reason = "Rate limited" }))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, daily.StatusCode);
            Assert.Equal(86280, daily.Headers.RetryAfter!.Delta!.Value.TotalSeconds);
        }

        OrganizationInvitation after = await db.OrganizationInvitations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(before.TokenVerifier, after.TokenVerifier);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Equal(before.Version, after.Version);
        await DataAsync(await client.PostAsJsonAsync(fixture.Route, new { recipientEmail = "second@example.test", roleIds = new[] { fixture.Role.Id } }), HttpStatusCode.Created);
        using (HttpResponseMessage hourly = await client.PostAsJsonAsync(fixture.Route, new { recipientEmail = "third@example.test", roleIds = new[] { fixture.Role.Id } }))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, hourly.StatusCode);
            Assert.Equal(3480, hourly.Headers.RetryAfter!.Delta!.Value.TotalSeconds);
        }

        Assert.False(await db.OrganizationInvitations.AnyAsync(x => x.OrganizationId == fixture.Organization.Id && x.RecipientEmail == "third@example.test"));

        fixture.Clock.Now = fixture.Clock.Now.AddDays(2);
        await using WebApplication parallelApp = await fixture.StartAppAsync(hourlyLimit: 1);
        using HttpClient parallelClient = Fixture.Client(parallelApp, fixture.Owner.Id);
        HttpResponseMessage[] attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => parallelClient.PostAsJsonAsync(fixture.Route, new { recipientEmail = $"quota-race-{index}@example.test", roleIds = new[] { fixture.Role.Id } })));
        await DataAsync(Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created);
        await ErrorAsync(Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.TooManyRequests), HttpStatusCode.TooManyRequests);
        Assert.Equal(1, await db.OrganizationInvitations.CountAsync(x => x.OrganizationId == fixture.Organization.Id && x.RecipientEmail.StartsWith("quota-race-")));
    }

    internal static async Task<JsonElement> DataAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            return json.RootElement.GetProperty("data").Clone();
        }
    }

    internal static async Task<string> ErrorAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
            Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
            return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
        }
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string _connection = Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")!;
        private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"dp-invitation-{Guid.NewGuid():N}.pem");
        private readonly RSA _key = RSA.Create(3072);
        private readonly List<Guid> _statusMessageIds = [];
        public ConcurrentQueue<string> Logs { get; } = new();
        public TestClock Clock { get; } = new(DateTimeOffset.UtcNow.AddMinutes(-5));
        public User Owner { get; } = User.Create($"invite-owner-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        public User Delegate { get; } = User.Create($"invite-delegate-{Guid.NewGuid():N}@example.test", "Delegate", status: "active");
        public User Ordinary { get; } = User.Create($"invite-member-{Guid.NewGuid():N}@example.test", "Private member", status: "active");
        public User Removed { get; } = User.Create($"invite-removed-{Guid.NewGuid():N}@example.test", "Removed", status: "active");
        public User Suspended { get; } = User.Create($"invite-suspended-{Guid.NewGuid():N}@example.test", "Suspended", status: "active");
        public User Foreign { get; } = User.Create($"invite-foreign-{Guid.NewGuid():N}@example.test", "Foreign", status: "active");
        public Organization Organization { get; private set; } = null!;
        public Organization ForeignOrganization { get; private set; } = null!;
        public OrganizationBuiltInRoleSeed Seed { get; private set; } = null!;
        public OrganizationRole Role { get; private set; } = null!;
        public OrganizationRole DelegateRole { get; private set; } = null!;
        public OrganizationRole ForeignRole { get; private set; } = null!;
        public string Route => $"/api/v1/organizations/{Organization.Id}/invitations";

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await File.WriteAllTextAsync(fixture._publicKeyPath, fixture._key.ExportSubjectPublicKeyInfoPem());
            fixture.Organization = Organization.Create("Workspace", fixture.Owner.Id, fixture.Clock.Now);
            fixture.ForeignOrganization = Organization.Create("Foreign", fixture.Foreign.Id, fixture.Clock.Now);
            fixture.Seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(fixture.Organization.Id, fixture.Clock.Now);
            fixture.Role = OrganizationRole.CreateCustom(fixture.Organization.Id, "Original", fixture.Owner.Id, fixture.Clock.Now);
            fixture.DelegateRole = OrganizationRole.CreateCustom(fixture.Organization.Id, "Delegate", fixture.Owner.Id, fixture.Clock.Now);
            fixture.ForeignRole = OrganizationRole.CreateCustom(fixture.ForeignOrganization.Id, "Foreign", fixture.Foreign.Id, fixture.Clock.Now);
            await using var db = fixture.NewDb();
            db.Users.AddRange(fixture.Owner, fixture.Delegate, fixture.Ordinary, fixture.Removed, fixture.Suspended, fixture.Foreign);
            db.Organizations.AddRange(fixture.Organization, fixture.ForeignOrganization);
            db.OrganizationRoles.AddRange(fixture.Seed.Roles);
            db.OrganizationRoles.AddRange(fixture.Role, fixture.DelegateRole, fixture.ForeignRole);
            db.OrganizationRolePermissions.AddRange(fixture.Seed.Permissions);
            db.OrganizationRolePermissions.Add(OrganizationRolePermission.Create(fixture.Organization.Id, fixture.Role.Id, OrganizationCapabilityCatalog.MembersRead));
            string[] codes = [OrganizationCapabilityCatalog.InvitationsIssue, OrganizationCapabilityCatalog.InvitationsResend, OrganizationCapabilityCatalog.InvitationsReissue, OrganizationCapabilityCatalog.InvitationsCancel, OrganizationCapabilityCatalog.RolesManage, OrganizationCapabilityCatalog.MembersRead];
            db.OrganizationRolePermissions.AddRange(codes.Select(code => OrganizationRolePermission.Create(fixture.Organization.Id, fixture.DelegateRole.Id, code)));
            foreach (User user in new[] { fixture.Owner, fixture.Delegate, fixture.Ordinary, fixture.Removed, fixture.Suspended })
            {
                var membership = OrganizationMembership.Create(fixture.Organization.Id, user.Id, fixture.Owner.Id, fixture.Clock.Now);
                db.OrganizationMemberships.Add(membership);
                Guid roleId = user.Id == fixture.Owner.Id ? fixture.Seed.OwnerRole.Id : user.Id == fixture.Delegate.Id ? fixture.DelegateRole.Id : fixture.Seed.MemberRole.Id;
                db.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(fixture.Organization.Id, membership.Id, roleId, fixture.Owner.Id, fixture.Clock.Now));
            }

            await db.SaveChangesAsync();
            await db.OrganizationMemberships.Where(x => x.OrganizationId == fixture.Organization.Id && x.UserId == fixture.Removed.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "removed"));
            await db.OrganizationMemberships.Where(x => x.OrganizationId == fixture.Organization.Id && x.UserId == fixture.Suspended.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, "suspended"));
            return fixture;
        }

        public IdentityDbContext NewDb() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_connection).Options);

        public string ReadProof(string requestJson)
        {
            var request = JsonSerializer.Deserialize<OrganizationInvitationEmailRequestedV1>(requestJson)!;
            var payload = new CipherEnvelope(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext);
            byte[] plaintext = EnvelopeEncryption.Decrypt(payload, request.AssociatedData(), _key.ExportPkcs8PrivateKeyPem());
            try
            {
                return JsonSerializer.Deserialize<OrganizationInvitationEmailData>(plaintext)!.Proof;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        public static HttpClient Client(WebApplication app, Guid? userId)
        {
            HttpClient client = app.GetTestClient();
            if (userId.HasValue)
            {
                client.DefaultRequestHeaders.Add("X-Test-User", userId.Value.ToString());
            }

            return client;
        }

        public async Task RecordStatusAsync(WebApplication app, Guid deliveryId, long sequence, string status)
        {
            Guid messageId = Guid.CreateVersion7();
            _statusMessageIds.Add(messageId);
            using IServiceScope scope = app.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var message = new OrganizationInvitationEmailStatusV1(messageId, deliveryId, "notification", sequence, Clock.Now, status, null);
            await sender.SendAsync(new RecordDeliveryStatusCommand(message), CancellationToken.None);
        }

        public async Task<WebApplication> StartAppAsync(string? failure = null, int hourlyLimit = 100, int dailyLimit = 10, bool useKestrel = false, Action<WebApplicationBuilder>? configure = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            if (useKestrel)
            {
                builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            }
            else
            {
                builder.WebHost.UseTestServer();
            }

            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new CapturingLoggerProvider(Logs)).SetMinimumLevel(LogLevel.Trace);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDb"] = _connection,
                ["OrganizationInvitationIssuance:PairDailyLimit"] = dailyLimit.ToString(CultureInfo.InvariantCulture),
                ["OrganizationInvitationIssuance:WorkspaceHourlyLimit"] = hourlyLimit.ToString(CultureInfo.InvariantCulture),
                ["InvitationDeliveryProducer:KeyId"] = "test",
                ["InvitationDeliveryProducer:PublicKeyFile"] = failure == "configuration" ? null : _publicKeyPath
            });
            builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton<TimeProvider>(Clock);
            if (failure == "audit")
            {
                builder.Services.RemoveAll<IIdentityAuditWriter>();
                builder.Services.AddScoped<IIdentityAuditWriter, FailingAudit>();
            }

            if (failure == "outbox")
            {
                builder.Services.AddScoped<InvitationDeliveryStager>();
                builder.Services.RemoveAll<IInvitationDeliveryStager>();
                builder.Services.AddScoped<IInvitationDeliveryStager, FailingStager>();
            }

            if (failure == "ledger")
            {
                builder.Services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailingLedger()));
            }

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddConfiguredApiVersioning();
            builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(OrganizationInvitationsController).Assembly);
            configure?.Invoke(builder);
            WebApplication app = builder.Build();
            app.UseGlobalExceptionHandling();
            app.UseMiddleware<InvitationRateLimitMiddleware>();
            app.UseApiStatusCodeEnvelope();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return app;
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = NewDb();
            Guid[] ids = [Organization.Id, ForeignOrganization.Id];
            Guid[] deliveries = await db.OrganizationInvitationDeliveries.Where(x => ids.Contains(x.OrganizationId)).Select(x => x.DeliveryMessageId).ToArrayAsync();
            await db.Set<OutboxMessage>().Where(x => deliveries.Contains(x.MessageId)).ExecuteDeleteAsync();
            await db.Set<InboxMessage>().Where(x => _statusMessageIds.Contains(x.MessageId)).ExecuteDeleteAsync();
            await db.IdentityAuditEvents.Where(x => x.OrganizationId != null && ids.Contains(x.OrganizationId.Value)).ExecuteDeleteAsync();
            await db.OrganizationInvitationDeliveries.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationOwnershipNominations.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationInvitationRoleOffers.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationInvitations.Where(x => ids.Contains(x.OrganizationId) && x.ReplacesInvitationId != null).ExecuteDeleteAsync();
            await db.OrganizationInvitations.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRoleAssignments.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRolePermissions.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationRoles.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.OrganizationMemberships.Where(x => ids.Contains(x.OrganizationId)).ExecuteDeleteAsync();
            await db.Organizations.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            Guid[] users = [Owner.Id, Delegate.Id, Ordinary.Id, Removed.Id, Suspended.Id, Foreign.Id];
            await db.AuthSessions.Where(x => users.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(x => users.Contains(x.Id)).ExecuteDeleteAsync();
            File.Delete(_publicKeyPath);
            _key.Dispose();
        }
    }

    internal sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(now.UtcTicks - (now.UtcTicks % 10), TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CurrentProvider(Guid userId) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => new(userId.ToString());
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(logs);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => logs.Enqueue(formatter(state, exception) + exception?.ToString());
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out Guid id))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, id.ToString())];
            if (Request.Headers.TryGetValue("X-Test-Session", out var session))
            {
                claims.Add(new Claim(CustomClaims.SessionId, session.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class FailingAudit : IIdentityAuditWriter
    {
        public Task<IdentityAuditEvent> StageAsync(IdentityAuditEntry entry, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Test audit failure");
        public void RecordDenialTelemetry(string eventType, Guid? organizationId, Guid? actorUserId, string correlationId, string denialCode) { }
    }

    private sealed class FailingStager(InvitationDeliveryStager inner) : IInvitationDeliveryStager
    {
        public async Task StageAsync(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery, InvitationProof proof, string correlationId, CancellationToken cancellationToken)
        {
            await inner.StageAsync(invitation, offers, delivery, proof, correlationId, cancellationToken);
            throw new InvalidOperationException("Test outbox failure");
        }
    }

    private sealed class FailingLedger : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<OrganizationInvitationDelivery>().Any(entry => entry.State == EntityState.Added))
            {
                throw new InvalidOperationException("Test ledger persistence failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
