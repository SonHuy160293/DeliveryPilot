namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public sealed class InvitationProof(string value, byte[] verifier)
{
    public string Value { get; } = value;
    public byte[] Verifier { get; } = (byte[])verifier.Clone();
    public override string ToString() => "[REDACTED]";
}
