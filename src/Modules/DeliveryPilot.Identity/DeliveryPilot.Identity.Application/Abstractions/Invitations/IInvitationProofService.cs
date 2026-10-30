namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationProofService
{
    InvitationProof Create(Guid organizationId, Guid invitationId);
    bool Verify(Guid organizationId, Guid invitationId, string? proof, byte[] verifier);
}
