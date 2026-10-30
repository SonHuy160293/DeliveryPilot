using System.Buffers.Text;
using System.Security.Cryptography;

namespace DeliveryPilot.Identity.Infrastructure.Security;

/// <summary>
/// Opaque refresh credential: <c>{sessionId:N}.{base64url(secret)}</c>. The whole value is secret.
/// Only the SHA-256 verifier of the random secret is persisted.
/// </summary>
public static class RefreshTokenCodec
{
    public const int SecretBytes = 32;
    private const char Separator = '.';
    private const int SessionIdLength = 32;
    private static readonly int _encodedSecretLength = Base64Url.GetEncodedLength(SecretBytes);

    public static (string Token, byte[] Verifier) Create(Guid sessionId)
    {
        byte[] secret = RandomNumberGenerator.GetBytes(SecretBytes);
        try
        {
            string token = string.Concat(sessionId.ToString("N"), Separator.ToString(), Base64Url.EncodeToString(secret));
            return (token, SHA256.HashData(secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static bool TryParse(string? token, out Guid sessionId, out byte[] verifier)
    {
        sessionId = Guid.Empty;
        verifier = [];
        if (token is null || token.Length != SessionIdLength + 1 + _encodedSecretLength || token[SessionIdLength] != Separator)
        {
            return false;
        }

        if (!Guid.TryParseExact(token.AsSpan(0, SessionIdLength), "N", out Guid id) || id == Guid.Empty)
        {
            return false;
        }

        Span<byte> secret = stackalloc byte[SecretBytes];
        try
        {
            if (!Base64Url.TryDecodeFromChars(token.AsSpan(SessionIdLength + 1), secret, out int written) || written != SecretBytes)
            {
                return false;
            }

            sessionId = id;
            verifier = SHA256.HashData(secret);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static bool VerifierMatches(byte[] expected, byte[] actual)
        => CryptographicOperations.FixedTimeEquals(expected, actual);
}
