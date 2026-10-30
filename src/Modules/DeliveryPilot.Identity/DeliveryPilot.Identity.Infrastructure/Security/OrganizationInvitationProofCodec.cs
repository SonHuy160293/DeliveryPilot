using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public static class OrganizationInvitationProofCodec
{
    private static readonly byte[] _purpose = Encoding.UTF8.GetBytes("deliverypilot.organization-invitation.v1");

    public static (string Proof, byte[] Verifier) Create(Guid organizationId, Guid invitationId)
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            return (Base64Url.EncodeToString(secret), Compute(organizationId, invitationId, secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static bool Verify(Guid organizationId, Guid invitationId, string? proof, byte[] expectedVerifier)
    {
        if (organizationId == Guid.Empty || invitationId == Guid.Empty || proof is null || proof.Length != Base64Url.GetEncodedLength(32) || expectedVerifier is null || expectedVerifier.Length != 32)
        {
            return false;
        }

        Span<byte> secret = stackalloc byte[32];
        try
        {
            if (!Base64Url.TryDecodeFromChars(proof, secret, out int written) || written != 32 || Base64Url.EncodeToString(secret) != proof)
            {
                return false;
            }

            byte[] actual = Compute(organizationId, invitationId, secret);
            try
            {
                return CryptographicOperations.FixedTimeEquals(expectedVerifier, actual);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static byte[] Compute(Guid organizationId, Guid invitationId, ReadOnlySpan<byte> secret)
    {
        if (organizationId == Guid.Empty || invitationId == Guid.Empty)
        {
            throw new ArgumentException("Scoped invitation identifiers are required.");
        }

        byte[] bytes = new byte[_purpose.Length + 32 + secret.Length];
        _purpose.CopyTo(bytes, 0);
        organizationId.TryWriteBytes(bytes.AsSpan(_purpose.Length, 16));
        invitationId.TryWriteBytes(bytes.AsSpan(_purpose.Length + 16, 16));
        secret.CopyTo(bytes.AsSpan(_purpose.Length + 32));
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
