namespace DeliveryPilot.Identity.Application.Abstractions.Security;

/// <summary>
/// Identity-owned session issuance, refresh rotation and revocation.
/// Every method stages changes in the caller's command transaction; the pipeline-owned
/// unit of work commits them. Callers must not deliver returned credentials unless that
/// commit succeeds, which the transaction pipeline guarantees by throwing on failure.
/// </summary>
public interface IAuthSessionService
{
    /// <summary>
    /// Creates a session for a user identity already verified by the server. Returns null when
    /// the user is missing or not active. Request data must never select the subject.
    /// </summary>
    Task<IssuedAuthSession?> CreateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Rotates the presented refresh token. Returns null when the token is malformed, unknown,
    /// already rotated, past the fixed deadline, revoked, or belongs to an inactive user.
    /// Rejecting an old rotated token does not revoke the current session.
    /// </summary>
    Task<IssuedAuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes one session owned by <paramref name="userId"/>. Returns false when no such session exists.
    /// Other sessions of the same user are unaffected.
    /// </summary>
    Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
