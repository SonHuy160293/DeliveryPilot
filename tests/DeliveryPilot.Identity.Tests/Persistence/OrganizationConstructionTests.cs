using System.Text.Json;
using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class OrganizationConstructionTests
{
    [Fact]
    public void CustomRoleCodeUsesItsGeneratedIdentity()
    {
        var role = OrganizationRole.CreateCustom(Guid.CreateVersion7(), " Reader ", Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Assert.Equal($"role-{role.Id:N}", role.Code);
        Assert.Equal("Reader", role.Name);
        Assert.Equal("custom", role.Origin);
        Assert.Null(role.TemplateVersion);
    }

    [Fact]
    public void OrganizationStartsActiveWithUtcTimestampsAndVersionOne()
    {
        Guid creator = Guid.CreateVersion7();
        DateTimeOffset instant = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));

        Organization organization = Organization.Create(" Workspace ", creator, instant);

        Assert.Equal("Workspace", organization.Name);
        Assert.Equal("active", organization.Status);
        Assert.Equal(TimeSpan.Zero, organization.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, organization.UpdatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, organization.StatusChangedAt.Offset);
        Assert.Equal(instant.ToUniversalTime(), organization.CreatedAt);
        Assert.Equal(1, organization.Version);
        Assert.Throws<ArgumentException>(() => Organization.Create(" ", creator, instant));
        Assert.Throws<ArgumentException>(() => Organization.Create(new string('x', 161), creator, instant));
        Assert.Throws<ArgumentException>(() => Organization.Create("Workspace", Guid.Empty, instant));
    }

    [Fact]
    public void MembershipRequiresOrganizationAndUser()
    {
        Guid organization = Guid.CreateVersion7();
        Guid user = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));

        OrganizationMembership membership = OrganizationMembership.Create(organization, user, user, now);

        Assert.Equal("active", membership.Status);
        Assert.Equal(TimeSpan.Zero, membership.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, membership.UpdatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, membership.StatusChangedAt.Offset);
        Assert.Equal(1, membership.Version);
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Create(Guid.Empty, user, user, now));
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Create(organization, Guid.Empty, user, now));
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Create(organization, user, Guid.Empty, now));
    }

    [Fact]
    public void RemovedMembershipRejoinsWithSameIdentityOriginalJoinTimeAndNoImplicitAdminRestore()
    {
        Guid id = Guid.CreateVersion7();
        Guid adminRoleId = Guid.CreateVersion7();
        Guid memberRoleId = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));
        OrganizationMembership membership = OrganizationMembership.Create(id, id, id, now);
        Guid membershipId = membership.Id;

        OrganizationRoleAssignment adminAssignment = OrganizationRoleAssignment.Create(id, membership.Id, adminRoleId, id, now);
        OrganizationRoleAssignment memberAssignment = OrganizationRoleAssignment.Create(id, membership.Id, memberRoleId, id, now);

        Assert.Throws<InvalidOperationException>(() => membership.Rejoin(id, now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => membership.Remove(Guid.Empty, "Reason", now.AddMinutes(1), [adminAssignment]));
        Assert.Throws<ArgumentException>(() => membership.Remove(id, " ", now.AddMinutes(1), [adminAssignment]));
        Assert.Throws<ArgumentException>(() => membership.Remove(id, new string('x', 1001), now.AddMinutes(1), [adminAssignment]));
        Assert.Throws<InvalidOperationException>(() => membership.Remove(id, "Backwards", now.AddMinutes(-1), [adminAssignment]));

        membership.Remove(id, " No longer on the team ", now.AddMinutes(1), [adminAssignment, memberAssignment]);
        Assert.Equal("removed", membership.Status);
        Assert.Equal("No longer on the team", membership.StatusReason);
        Assert.Equal(TimeSpan.Zero, membership.UpdatedAt.Offset);
        Assert.NotNull(adminAssignment.RevokedAt);
        Assert.NotNull(memberAssignment.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => membership.Remove(id, "Again", now.AddMinutes(2), [adminAssignment, memberAssignment]));
        Assert.Throws<InvalidOperationException>(() => membership.RecordAssignmentChange(now.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => membership.Rejoin(Guid.Empty, now.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => membership.Rejoin(id, now));

        membership.Rejoin(id, now.AddMinutes(2));

        Assert.Equal(membershipId, membership.Id);
        Assert.Equal(now.ToUniversalTime(), membership.CreatedAt);
        Assert.Equal("active", membership.Status);
        Assert.NotNull(adminAssignment.RevokedAt);
        Assert.Equal(1, adminAssignment.GrantVersion);
        Assert.NotNull(memberAssignment.RevokedAt);
        Assert.Null(membership.StatusReason);
        Assert.Equal(3, membership.Version);

        memberAssignment.Regrant(id, now.AddMinutes(3));
        membership.RecordAssignmentChange(now.AddMinutes(3));
        Assert.NotNull(adminAssignment.RevokedAt);
        Assert.Equal(1, adminAssignment.GrantVersion);
        Assert.Null(memberAssignment.RevokedAt);
        Assert.Equal(2, memberAssignment.GrantVersion);
        Assert.Equal(4, membership.Version);
    }

    [Fact]
    public void RevokedAssignmentRegrantsAsANewGrantPeriod()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));
        OrganizationRoleAssignment assignment = OrganizationRoleAssignment.Create(id, id, id, id, now);
        Guid assignmentId = assignment.Id;

        Assert.Equal(TimeSpan.Zero, assignment.GrantedAt.Offset);
        Assert.Throws<InvalidOperationException>(() => assignment.Regrant(id, now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => assignment.Revoke(Guid.Empty, now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => assignment.Revoke(id, now.AddMinutes(-1)));

        assignment.Revoke(id, now.AddMinutes(1), "Temporary revoke");
        Assert.Equal(TimeSpan.Zero, assignment.RevokedAt!.Value.Offset);
        Assert.Throws<InvalidOperationException>(() => assignment.Revoke(id, now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => assignment.Regrant(Guid.Empty, now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => assignment.Regrant(id, now));
        Assert.Throws<InvalidOperationException>(() => assignment.Regrant(id, now.AddMinutes(2), now.AddMinutes(2)));

        assignment.Regrant(id, now.AddMinutes(2), now.AddMinutes(10), "Regranted");

        Assert.Equal(assignmentId, assignment.Id);
        Assert.Equal(now.ToUniversalTime(), assignment.CreatedAt);
        Assert.Equal(TimeSpan.Zero, assignment.GrantedAt.Offset);
        Assert.Equal(TimeSpan.Zero, assignment.ExpiresAt!.Value.Offset);
        Assert.Equal(2, assignment.GrantVersion);
        Assert.Equal(3, assignment.Version);
        Assert.Null(assignment.RevokedAt);
        Assert.Null(assignment.RevokedByUserId);
        Assert.Null(assignment.RevocationReason);
    }

    [Fact]
    public void RemovalRejectsForeignAssignmentsWithoutChangingMembershipOrGrants()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OrganizationMembership membership = OrganizationMembership.Create(id, id, id, now);
        OrganizationRoleAssignment valid = OrganizationRoleAssignment.Create(id, membership.Id, id, id, now);
        OrganizationRoleAssignment foreign = OrganizationRoleAssignment.Create(id, Guid.CreateVersion7(), id, id, now);

        Assert.Throws<InvalidOperationException>(() => membership.Remove(id, "Removal", now.AddMinutes(1), [valid, foreign]));
        Assert.Equal("active", membership.Status);
        Assert.Null(valid.RevokedAt);
        Assert.Equal(1, membership.Version);
    }

    [Fact]
    public void RoleCreationAndTransitionsEnforceOriginImmutabilityAndVersioning()
    {
        Guid organization = Guid.CreateVersion7();
        Guid creator = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(7));

        OrganizationRole customRole = OrganizationRole.Create(organization, " custom ", " Custom ", "custom", null, creator, now);
        OrganizationRole systemRole = OrganizationRole.Create(organization, "admin", "Admin", "system", 1, null, now);

        Assert.Equal("custom", customRole.Code);
        Assert.Equal("Custom", customRole.Name);
        Assert.Equal(TimeSpan.Zero, customRole.CreatedAt.Offset);
        Assert.Equal(1, customRole.PermissionSetVersion);
        Assert.Equal(1, customRole.Version);

        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(Guid.Empty, "custom", "Custom", "custom", null, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, " ", "Custom", "custom", null, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, new string('x', 81), "Custom", "custom", null, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", " ", "custom", null, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", new string('x', 161), "custom", null, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", "Custom", "custom", 1, creator, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "system", "System", "system", null, null, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "system", "System", "system", 0, null, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", "Custom", "custom", null, null, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", "Custom", "custom", null, Guid.Empty, now));
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", "Custom", "invalid", null, creator, now));

        Assert.Throws<InvalidOperationException>(() => systemRole.UpdateDetails("Renamed", null, now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => systemRole.AdvancePermissionSetVersion(now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => systemRole.Retire(now.AddMinutes(1)));

        Assert.Throws<ArgumentException>(() => customRole.UpdateDetails(" ", null, now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => customRole.UpdateDetails("Renamed", null, now.AddMinutes(-1)));
        customRole.UpdateDetails(" Renamed ", "Updated description", now.AddMinutes(1));
        Assert.Equal("custom", customRole.Code);
        Assert.Equal("Renamed", customRole.Name);
        Assert.Equal("Updated description", customRole.Description);
        Assert.Equal(1, customRole.PermissionSetVersion);
        Assert.Equal(2, customRole.Version);
        Assert.Equal(TimeSpan.Zero, customRole.UpdatedAt.Offset);

        customRole.AdvancePermissionSetVersion(now.AddMinutes(2));
        Assert.Equal(2, customRole.PermissionSetVersion);
        Assert.Equal(3, customRole.Version);

        customRole.Retire(now.AddMinutes(3));
        Assert.NotNull(customRole.RetiredAt);
        Assert.Equal(TimeSpan.Zero, customRole.RetiredAt!.Value.Offset);
        Assert.Equal(4, customRole.Version);
        Assert.Throws<InvalidOperationException>(() => customRole.UpdateDetails("Again", null, now.AddMinutes(4)));
        Assert.Throws<InvalidOperationException>(() => customRole.AdvancePermissionSetVersion(now.AddMinutes(4)));
        Assert.Throws<InvalidOperationException>(() => customRole.Retire(now.AddMinutes(4)));
    }

    [Fact]
    public void RoleDescriptionAllowsAtMostOneThousandCharacters()
    {
        Guid organization = Guid.CreateVersion7();
        Guid creator = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string description = new('x', 1000);

        OrganizationRole role = OrganizationRole.Create(organization, "custom", "Custom", "custom", null, creator, now, description);

        Assert.Equal(description, role.Description);
        Assert.Throws<ArgumentException>(() => OrganizationRole.Create(organization, "custom", "Custom", "custom", null, creator, now, description + "x"));
    }

    [Fact]
    public void InvitationOfferClonesAndValidatesPermissionSnapshot()
    {
        Guid id = Guid.CreateVersion7();
        OrganizationInvitationRoleOffer offer;
        using (JsonDocument document = JsonDocument.Parse("[\"a\",\"b\"]"))
        {
            offer = OrganizationInvitationRoleOffer.Create(id, id, id, "Reader", 1, document.RootElement);
        }

        Assert.Equal("a", offer.OfferedPermissionCodes[0].GetString());
        using JsonDocument duplicates = JsonDocument.Parse("[\"a\",\"a\"]");
        Assert.Throws<ArgumentException>(() => OrganizationInvitationRoleOffer.Create(id, id, id, "Reader", 1, duplicates.RootElement));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationInvitationRoleOffer.Create(id, id, id, "Reader", 0, duplicates.RootElement));
    }

    [Fact]
    public void InvitationCopiesVerifierAndRequiresAnExpiryAfterIssuance()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] verifier = new byte[32];
        verifier[0] = 1;
        OrganizationInvitation invitation = OrganizationInvitation.Create(id, " User@Example.com ", id, now, now.AddHours(1), verifier);
        verifier[0] = 2;
        byte[] observed = invitation.TokenVerifier;
        observed[0] = 3;

        Assert.Equal(1, invitation.TokenVerifier[0]);
        Assert.Equal("user@example.com", invitation.RecipientEmail);
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(id, "a@b", id, now, now, verifier));
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(id, "a@b", id, now, now.AddHours(1), new byte[31]));
    }

    [Fact]
    public void AssignmentsRequireValidGrantInputs()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OrganizationRoleAssignment organizationGrant = OrganizationRoleAssignment.Create(id, id, id, id, now);
        PlatformRoleAssignment platformGrant = PlatformRoleAssignment.Create(id, "administrator", null, null, "bootstrap-review", now, "bootstrap");

        Assert.Equal(1, organizationGrant.GrantVersion);
        Assert.Equal(1, platformGrant.GrantVersion);
        Assert.Throws<ArgumentException>(() => OrganizationRoleAssignment.Create(Guid.Empty, id, id, id, now));
        Assert.Throws<ArgumentException>(() => OrganizationRoleAssignment.Create(id, Guid.Empty, id, id, now));
        Assert.Throws<ArgumentException>(() => OrganizationRoleAssignment.Create(id, id, Guid.Empty, id, now));
        Assert.Throws<ArgumentException>(() => OrganizationRoleAssignment.Create(id, id, id, Guid.Empty, now));
        Assert.Throws<ArgumentException>(() => OrganizationRoleAssignment.Create(id, id, id, id, now, now));
        Assert.Throws<ArgumentException>(() => PlatformRoleAssignment.Create(id, "administrator", null, null, " ", now, "bootstrap"));
        Assert.Throws<ArgumentException>(() => PlatformRoleAssignment.Create(id, "administrator", null, null, "review", now, "bootstrap", now));
    }
}
