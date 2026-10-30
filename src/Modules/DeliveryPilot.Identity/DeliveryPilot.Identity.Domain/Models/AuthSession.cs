namespace DeliveryPilot.Identity.Domain.Models;

/// <summary>
/// An Identity-owned authenticated session. It stores only a protected verifier of the
/// current refresh secret, a refresh deadline fixed at creation and an optional revocation.
/// </summary>
public sealed class AuthSession
{
    public const int RefreshVerifierLength = 32;

    private AuthSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset RefreshExpiresAt { get; private set; }
    public byte[] RefreshTokenVerifier { get; private set; } = null!;
    public DateTimeOffset? RefreshRotatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static AuthSession Create(
        Guid id,
        Guid userId,
        byte[] refreshTokenVerifier,
        DateTimeOffset createdAt,
        DateTimeOffset refreshExpiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        ValidateVerifier(refreshTokenVerifier);
        DateTimeOffset created = createdAt.ToUniversalTime();
        DateTimeOffset deadline = refreshExpiresAt.ToUniversalTime();
        if (deadline <= created)
        {
            throw new ArgumentException("Refresh deadline must be after the session creation instant.", nameof(refreshExpiresAt));
        }

        return new AuthSession
        {
            Id = id,
            UserId = userId,
            CreatedAt = created,
            RefreshExpiresAt = deadline,
            RefreshTokenVerifier = (byte[])refreshTokenVerifier.Clone(),
        };
    }

    /// <summary>Returns true when the refresh deadline has not been reached at <paramref name="now"/>.</summary>
    public bool CanRefreshAt(DateTimeOffset now) => !IsRevoked && now.ToUniversalTime() < RefreshExpiresAt;

    /// <summary>
    /// Replaces the current refresh verifier. The fixed refresh deadline is intentionally unchanged.
    /// </summary>
    public void RotateRefreshToken(byte[] refreshTokenVerifier, DateTimeOffset now)
    {
        ValidateVerifier(refreshTokenVerifier);
        if (!CanRefreshAt(now))
        {
            throw new InvalidOperationException("An ended or expired session cannot rotate its refresh token.");
        }

        RefreshTokenVerifier = (byte[])refreshTokenVerifier.Clone();
        RefreshRotatedAt = now.ToUniversalTime();
    }

    /// <summary>Ends the session. Revocation is idempotent and keeps the first revocation instant.</summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now.ToUniversalTime();
    }

    public override string ToString() => $"{nameof(AuthSession)} {{ Id = {Id}, UserId = {UserId} }}";

    private static void ValidateVerifier(byte[] refreshTokenVerifier)
    {
        ArgumentNullException.ThrowIfNull(refreshTokenVerifier);
        if (refreshTokenVerifier.Length != RefreshVerifierLength)
        {
            throw new ArgumentException("Refresh verifier must be a SHA-256 digest.", nameof(refreshTokenVerifier));
        }
    }
}
