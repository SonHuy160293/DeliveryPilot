using DeliveryPilot.Identity.Application.Abstractions.Invitations;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public sealed class InvitationProofService : IInvitationProofService
{
    public InvitationProof Create(Guid organizationId, Guid invitationId)
    {
        (string proof, byte[] verifier) = OrganizationInvitationProofCodec.Create(organizationId, invitationId);
        return new InvitationProof(proof, verifier);
    }

    public bool Verify(Guid organizationId, Guid invitationId, string? proof, byte[] verifier)
        => OrganizationInvitationProofCodec.Verify(organizationId, invitationId, proof, verifier);
}
