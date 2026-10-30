using System.Text.Json;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class OrganizationInvitationPersistenceTests
{
    [PostgreSqlFact]
    public async Task ScopedOfferSnapshotsAndPendingUniquenessArePersisted()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES")).Options;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create($"invitation-{Guid.NewGuid():N}@example.test", "Owner", status: "active");
        var organization = Organization.Create("Test", user.Id, now);
        var role = OrganizationRole.CreateCustom(organization.Id, "Original name", user.Id, now);
        Guid invitationId = Guid.CreateVersion7();
        Guid messageId = Guid.CreateVersion7();
        (string proof, byte[] verifier) = OrganizationInvitationProofCodec.Create(organization.Id, invitationId);
        var invitation = OrganizationInvitation.CreateWithId(invitationId, organization.Id, " Person@Example.test ", user.Id, now, now.AddDays(7), verifier, messageId);
        using var permissionJson = JsonDocument.Parse("[\"organization.members.read\"]");
        var offer = OrganizationInvitationRoleOffer.Create(organization.Id, invitation.Id, role.Id, role.Name, role.PermissionSetVersion, permissionJson.RootElement);
        var delivery = OrganizationInvitationDelivery.Create(organization.Id, invitation.Id, messageId, now, now.AddDays(7));
        await using var db = new IdentityDbContext(options);
        try
        {
            db.Users.Add(user);
            db.Organizations.Add(organization);
            db.OrganizationRoles.Add(role);
            db.OrganizationInvitations.Add(invitation);
            db.OrganizationInvitationRoleOffers.Add(offer);
            db.OrganizationInvitationDeliveries.Add(delivery);
            await db.SaveChangesAsync();
            var eligibility = new InvitationDeliveryEligibilityStore(db, TimeProvider.System);
            Assert.True((await eligibility.GetAsync(messageId, CancellationToken.None)).Allowed);
            Assert.False((await eligibility.GetAsync(Guid.CreateVersion7(), CancellationToken.None)).Allowed);
            var expiredEligibility = new InvitationDeliveryEligibilityStore(db, new FixedClock(now.AddDays(7)));
            Assert.False((await expiredEligibility.GetAsync(messageId, CancellationToken.None)).Allowed);
            role.UpdateDetails("New name", null, now.AddSeconds(1));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal("Original name", await db.OrganizationInvitationRoleOffers.Where(x => x.InvitationId == invitationId).Select(x => x.OfferedRoleName).SingleAsync());
            Assert.True(OrganizationInvitationProofCodec.Verify(organization.Id, invitationId, proof, (await db.OrganizationInvitations.SingleAsync(x => x.Id == invitationId)).TokenVerifier));
            var duplicate = OrganizationInvitation.Create(organization.Id, "PERSON@example.test", user.Id, now, now.AddDays(7), verifier);
            db.OrganizationInvitations.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            OrganizationInvitation stored = await db.OrganizationInvitations.SingleAsync(x => x.Id == invitationId);
            Guid nextMessageId = Guid.CreateVersion7();
            stored.RotateProof(nextMessageId, verifier, now.AddMinutes(1), now.AddDays(7).AddMinutes(1));
            db.OrganizationInvitationDeliveries.Add(OrganizationInvitationDelivery.Create(organization.Id, stored.Id, nextMessageId, now.AddMinutes(1), now.AddDays(7).AddMinutes(1)));
            await db.SaveChangesAsync();
            Assert.False((await eligibility.GetAsync(messageId, CancellationToken.None)).Allowed);
            Assert.True((await eligibility.GetAsync(nextMessageId, CancellationToken.None)).Allowed);
            stored.Close("cancelled", user.Id, now.AddMinutes(2), "Cancelled");
            await db.SaveChangesAsync();
            Assert.False((await eligibility.GetAsync(nextMessageId, CancellationToken.None)).Allowed);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.OrganizationInvitationDeliveries.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationInvitationRoleOffers.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationInvitations.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await db.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
