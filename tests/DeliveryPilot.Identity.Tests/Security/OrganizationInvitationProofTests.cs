using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class OrganizationInvitationProofTests
{
    [Fact]
    public void ProofIsScopedAndRejectsMalformedOrDifferentSecrets()
    {
        Guid organizationId = Guid.CreateVersion7();
        Guid invitationId = Guid.CreateVersion7();
        (string proof, byte[] verifier) = OrganizationInvitationProofCodec.Create(organizationId, invitationId);
        Assert.Equal(43, proof.Length);
        Assert.Equal(32, verifier.Length);
        Assert.True(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, proof, verifier));
        Assert.False(OrganizationInvitationProofCodec.Verify(Guid.CreateVersion7(), invitationId, proof, verifier));
        Assert.False(OrganizationInvitationProofCodec.Verify(organizationId, Guid.CreateVersion7(), proof, verifier));
        Assert.False(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, proof + "=", verifier));
        Assert.False(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, new string('!', 43), verifier));
        (string other, _) = OrganizationInvitationProofCodec.Create(organizationId, invitationId);
        Assert.False(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, other, verifier));
    }

    [Fact]
    public void ResendRotatesVerifierAndExpiredInvitationCannotBeResentOrAccepted()
    {
        Guid organizationId = Guid.CreateVersion7();
        Guid invitationId = Guid.CreateVersion7();
        Guid actorId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (string first, byte[] firstVerifier) = OrganizationInvitationProofCodec.Create(organizationId, invitationId);
        var invitation = OrganizationInvitation.CreateWithId(invitationId, organizationId, "recipient@example.test", actorId, now, now.AddDays(7), firstVerifier, Guid.CreateVersion7());
        (string second, byte[] secondVerifier) = OrganizationInvitationProofCodec.Create(organizationId, invitationId);
        DateTimeOffset resentAt = now.AddMinutes(1);
        invitation.RotateProof(Guid.CreateVersion7(), secondVerifier, resentAt, resentAt.AddDays(7));
        Assert.False(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, first, invitation.TokenVerifier));
        Assert.True(OrganizationInvitationProofCodec.Verify(organizationId, invitationId, second, invitation.TokenVerifier));
        Assert.Throws<InvalidOperationException>(() => invitation.Close("accepted", actorId, invitation.ExpiresAt));
        Assert.Throws<InvalidOperationException>(() => invitation.RotateProof(Guid.CreateVersion7(), secondVerifier, invitation.ExpiresAt, invitation.ExpiresAt.AddDays(7)));
        invitation.Close("expired", null, invitation.ExpiresAt);
        Assert.Throws<InvalidOperationException>(() => invitation.Close("accepted", actorId, invitation.ExpiresAt.AddSeconds(1)));
    }
}
